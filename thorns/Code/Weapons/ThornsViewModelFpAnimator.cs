using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sandbox;

/// <summary>
/// Local-only first-person viewmodel animator (never networked — THORNS_EVERYTHING_DOCUMENT § viewer camera / FP presentation).
/// Drives Valve anim graphs via graph parameters (Citizen-style <c>SkinnedModelRenderer.Set</c> extensions) and, when needed,
/// <see cref="SceneModel.DirectPlayback"/> (direct playback node authored inside the mesh graph — not raw sequence polling).
/// </summary>
[Title( "Animator" )]
[Category( "Thorns" )]
[Icon( "animation" )]
[Order( 106 )]
public sealed class ThornsViewModelFpAnimator : Component
{
	[Property] public string AnimGraphResource { get; set; } = "";

	[Property]
	public string DeploySequenceName { get; set; } = "Deploy_Slide";

	[Property]
	public string IdleSequenceName { get; set; } = "IdlePose";

	[Property]
	public string AdsSequenceName { get; set; } = "Ironsights_Pose_Normal";

	[Property]
	public string ReloadSequenceName { get; set; } = "Reload_Empty";

	[Property]
	public string ReloadFirstShellSequenceName { get; set; } = "";

	[Property] public float ShellReloadGraphPulseHoldSeconds { get; set; } = 0.08f;

	[Property] public string DeployParameterName { get; set; } = "deploy";

	[Property] public string HoldParameterName { get; set; } = "hold";

	/// <summary>Default matches <see cref="StockFpDeployHostBlockSeconds"/> math on <see cref="ThornsWeapon"/> — keep prefabs aligned or adjust host constant.</summary>
	[Property] public float DeployDurationFallbackSeconds { get; set; } = DefaultDeployDurationFallbackSeconds;

	/// <summary>Matches <see cref="EquipRoutineAsync"/> delay before deploy starts.</summary>
	public const float EquipRoutineInitialDelaySeconds = 0.06f;

	public const float DefaultDeployDurationFallbackSeconds = 0.85f;

	/// <summary>
	/// Conservative upper bound for equip preamble + DirectPlayback stagger + default deploy wait — host blocks <c>RequestFire</c> this long for stock-FP weapons (<see cref="ThornsWeapon.HostResetCooldownAfterWeaponEquip"/>).
	/// </summary>
	public const float StockFpDeployHostBlockSeconds =
		EquipRoutineInitialDelaySeconds + 0.03f + DefaultDeployDurationFallbackSeconds;

	[Property] public float ReloadDurationFallbackSeconds { get; set; } = 2f;

	[Property]
	public bool PreferGraphParametersFirst { get; set; }

	[Property] public float DeploySequenceVerticalLift { get; set; }

	[Property]
	public bool UseGraphIronsightsParameterForAds { get; set; } = true;

	[Property] public float AttackHoldRampUpPerSecond { get; set; } = 4f;

	[Property] public float AttackHoldDecayPerSecond { get; set; } = 6f;

	[Property] public float IronsightsFireScaleWhileAds { get; set; } = 0.72f;

	[Property] public float HipCameraRotationScale { get; set; } = 0.68f;

	[Property] public float AdsCameraRotationScale { get; set; } = 0.58f;

	[Property]
	public bool LoopIdleWithDirectPlayback { get; set; }

	public const string GraphParamAttack = "b_attack";

	public const string GraphParamAttackHold = "attack_hold";

	public const string GraphParamIronsights = "ironsights";

	public const string GraphParamIronsightsFireScale = "ironsights_fire_scale";

	public const string GraphParamCameraRotationScale = "camera_rotation_scale";

	public const string GraphParamFiringMode = "firing_mode";

	public const string GraphParamSprint = "b_sprint";

	/// <summary>Facepunch FP shotgun graph: latch reload layer while inserting shells.</summary>
	public const string GraphParamReloading = "b_reloading";

	public const string GraphParamReloadingFirstShell = "b_reloading_first_shell";

	public const string GraphParamReloadingShell = "b_reloading_shell";

	[Property] public float FullAutoAttackHoldRampMultiplier { get; set; } = 2.25f;

	[Property] public string MeleeLightAttackSequenceName { get; set; } = "";

	/// <summary>
	/// When set, light melee <see cref="OwnerNotifyMeleeAttackCommitted"/> alternates between this and <see cref="MeleeLightAttackSequenceName"/> each swing (e.g. left / right punch).
	/// </summary>
	[Property] public string MeleeLightAttackAlternateSequenceName { get; set; } = "";

	[Property] public string MeleeHeavyAttackSequenceName { get; set; } = "";

	/// <summary>When true with <see cref="BareHandsLocomotionMoveSequenceName"/>, <see cref="OwnerTickBareHandsLocomotion"/> swaps move vs idle <see cref="IdleSequenceName"/> on planar speed.</summary>
	[Property] public bool BareHandsLocomotionBlendEnabled { get; set; }

	/// <summary>Looped direct playback while local owner moves faster than enter threshold (e.g. <c>Punching_Move</c>).</summary>
	[Property] public string BareHandsLocomotionMoveSequenceName { get; set; } = "";

	[Property] public float BareHandsMoveSpeedEnter { get; set; } = 48f;

	[Property] public float BareHandsMoveSpeedExit { get; set; } = 32f;

	[Property] public float MeleeAttackDurationFallbackSeconds { get; set; } = 0.45f;

	SkinnedModelRenderer _skin;
	Model _model;
	CancellationTokenSource _lifecycle;
	readonly List<SkinnedModelRenderer> _linkedSkins = new();

	float _deployLiftApplied;

	bool _equipPlaybackDone;
	bool _appliedAdsPose;
	bool _reloadAnimRunning;
	bool _meleeAttackAnimRunning;
	bool _meleeLightAlternateNext;
	bool _bareHandsLocomotionIsMoving;
	bool _desiredAdsPose;

	/// <summary>Mirrored from host <c>_hostShotgunPumpReloadSession</c> — blocks FP fire layering between shells.</summary>
	bool _shotgunPumpSessionActiveForPresentation;

	readonly Queue<(float gameplayReloadSeconds, bool tubeWasEmptyBeforeShell)> _shotgunGraphShellQueue = new();

	bool _shotgunGraphReloadWorkerBusy;
	bool _wantReleaseShotgunReloadingGraphAfterDrain;
	bool _lastSyncedShotgunPumpSession;

	float _attackHold01;
	double _nextAttackHoldDebugLogTime;

	/// <summary>False until deploy presentation completes, during reload/pump, or one-shot melee clip — host still validates; UX gate.</summary>
	public bool PresentationAllowsCombatFire =>
		_equipPlaybackDone && !_reloadAnimRunning && !_meleeAttackAnimRunning && !_shotgunPumpSessionActiveForPresentation;

	public void BindAndRunEquipRoutine( SkinnedModelRenderer skin, Model model )
	{
		_lifecycle?.Cancel();
		_lifecycle?.Dispose();
		_lifecycle = new CancellationTokenSource();
		_deployLiftApplied = 0f;
		_equipPlaybackDone = false;
		_appliedAdsPose = false;
		_reloadAnimRunning = false;
		_meleeAttackAnimRunning = false;
		_meleeLightAlternateNext = false;
		_bareHandsLocomotionIsMoving = false;
		_desiredAdsPose = false;
		_attackHold01 = 0f;
		_nextAttackHoldDebugLogTime = 0;
		if ( BareHandsLocomotionBlendEnabled )
			_equipPlaybackDone = true;
		_shotgunPumpSessionActiveForPresentation = false;
		_shotgunGraphReloadWorkerBusy = false;
		_wantReleaseShotgunReloadingGraphAfterDrain = false;
		_shotgunGraphShellQueue.Clear();
		_lastSyncedShotgunPumpSession = false;

		_skin = skin;
		_model = model;
		_linkedSkins.Clear();


		if ( !_skin.IsValid() || !_model.IsValid() || !FpAnimClientGuard() )
			return;

		ApplyOptionalGraphOverride();
		_skin.UseAnimGraph = true;

		_ = EquipRoutineAsync( _lifecycle.Token );
	}

	/// <summary>Add another skinned renderer (e.g. FP arms) that should receive the same anim-graph parameter values as the weapon.</summary>
	public void AddLinkedSkin( SkinnedModelRenderer skin )
	{
		if ( !skin.IsValid() || skin == _skin || _linkedSkins.Contains( skin ) )
			return;
		_linkedSkins.Add( skin );
	}

	void SetParamOnAllSkins( string name, bool value )
	{
		if ( _skin.IsValid() )
			_skin.Set( name, value );
		foreach ( var s in _linkedSkins )
		{
			if ( s.IsValid() )
				s.Set( name, value );
		}
	}

	void SetParamOnAllSkins( string name, int value )
	{
		if ( _skin.IsValid() )
			_skin.Set( name, value );
		foreach ( var s in _linkedSkins )
		{
			if ( s.IsValid() )
				s.Set( name, value );
		}
	}

	void SetParamOnAllSkins( string name, float value )
	{
		if ( _skin.IsValid() )
			_skin.Set( name, value );
		foreach ( var s in _linkedSkins )
		{
			if ( s.IsValid() )
				s.Set( name, value );
		}
	}

	static bool FpAnimClientGuard()
	{
		return Game.IsPlaying && !Application.IsDedicatedServer && !Application.IsHeadless;
	}

	void ApplyOptionalGraphOverride()
	{
		if ( string.IsNullOrWhiteSpace( AnimGraphResource ) )
			return;

		var loaded = AnimationGraph.Load( AnimGraphResource );
		if ( loaded is null || loaded.IsError )
			return;

		_skin.AnimationGraph = loaded;
	}

	protected override void OnDestroy()
	{
		RemoveDeployLift();
		_lifecycle?.Cancel();
		_lifecycle?.Dispose();
		base.OnDestroy();
	}

	async Task EquipRoutineAsync( CancellationToken ct )
	{
		await Task.DelayRealtimeSeconds( EquipRoutineInitialDelaySeconds );

		if ( ct.IsCancellationRequested || !_skin.IsValid() )
			return;

		var graph = _model.AnimGraph;
		LogAnimGraphParameters( graph );

		if ( PreferGraphParametersFirst && graph is not null && !graph.IsError &&
		     await TryDeployWithGraphParametersAsync( graph, ct ) )
			return;

		await RunDirectPlaybackEquipAsync( ct );
	}

	async Task<bool> TryDeployWithGraphParametersAsync( AnimationGraph graph, CancellationToken ct )
	{
		if ( graph.ParamCount <= 0 || !graph.TryGetParameterIndex( DeployParameterName, out _ ) )
			return false;

		AddDeployLift();

		SetParamOnAllSkins( DeployParameterName, true );

		var deployTotalSeconds = DeployDurationFallbackSeconds;

		await Task.DelayRealtimeSeconds( deployTotalSeconds );
		if ( ct.IsCancellationRequested || !_skin.IsValid() )
		{
			RemoveDeployLift();
			return true;
		}

		RemoveDeployLift();

		if ( graph.TryGetParameterIndex( HoldParameterName, out _ ) )
			SetParamOnAllSkins( HoldParameterName, true );

		if ( !ct.IsCancellationRequested )
			_equipPlaybackDone = true;
		return true;
	}

	async Task RunDirectPlaybackEquipAsync( CancellationToken ct )
	{
		var sceneModel = await WaitForFpSceneModelAsync( ct, maxSeconds: 2.5f );
		if ( !sceneModel.IsValid() )
		{
			Log.Warning(
				"[Thorns][FP-Anim] SceneModel not ready for DirectPlayback (deploy/idle) — arms may stay in bind pose. Check viewmodel renderer / overlay timing." );
			_equipPlaybackDone = true;
			return;
		}

		sceneModel.UseAnimGraph = true;

		AddDeployLift();

		sceneModel.DirectPlayback.Play( DeploySequenceName );

		await Task.DelayRealtimeSeconds( 0.03f );

		var waitSeconds = DeployDurationFallbackSeconds;
		var d = sceneModel.DirectPlayback.Duration;
		if ( d > 0.005f )
			waitSeconds = d;

		if ( ct.IsCancellationRequested )
		{
			RemoveDeployLift();
			return;
		}

		await Task.DelayRealtimeSeconds( waitSeconds );

		if ( ct.IsCancellationRequested )
		{
			RemoveDeployLift();
			return;
		}

		if ( !_skin.IsValid() || !sceneModel.IsValid() )
		{
			RemoveDeployLift();
			return;
		}

		RemoveDeployLift();

		if ( LoopIdleWithDirectPlayback )
		{
			sceneModel.DirectPlayback.Play( IdleSequenceName );
		}
		else
		{
			ReleaseDirectPlaybackLayer( sceneModel );
			ApplyBaselineCombatGraphParameters();
		}

		if ( !ct.IsCancellationRequested )
			_equipPlaybackDone = true;
	}

	SceneModel TryGetSceneModel()
	{
		if ( !_skin.IsValid() || !_skin.SceneObject.IsValid() )
			return default;

		return _skin.SceneObject as SceneModel;
	}

	/// <summary>
	/// <see cref="SkinnedModelRenderer.SceneObject"/> is often not a <see cref="SceneModel"/> on the same frame the renderer is created;
	/// published builds can lag the editor. FP deploy / idle / punch use <see cref="SceneModel.DirectPlayback"/> — wait briefly before giving up.
	/// </summary>
	async Task<SceneModel> WaitForFpSceneModelAsync( CancellationToken ct, float maxSeconds )
	{
		var deadline = Time.Now + Math.Max( 0.05f, maxSeconds );
		while ( Time.Now < deadline && !ct.IsCancellationRequested )
		{
			if ( !_skin.IsValid() )
				return default;

			if ( _skin.SceneObject is SceneModel sm && sm.IsValid() )
				return sm;

			await Task.DelayRealtimeSeconds( 0.033f );
		}

		if ( !_skin.IsValid() )
			return default;

		return _skin.SceneObject is SceneModel sm2 && sm2.IsValid() ? sm2 : default;
	}

	/// <summary>
	/// Called every frame by the owning <see cref="ThornsWeapon"/>: ADS / reload / continuous-fire blend (<c>attack_hold</c>) / <c>ironsights</c> graph params.
	/// </summary>
	/// <param name="shellReloadAmmoBeforeRpc">Tube count <b>before</b> this reload RPC resolves (gameplay ammo). Used with <paramref name="shellReloadPresentation"/> to pick FirstShell vs Shell clips.</param>
	/// <param name="shotgunPumpSessionHeld">While true, host keeps the pump session — hold <c>b_reloading</c> (v_spaghellim4) across shells; see Facepunch FP weapons doc.</param>
	public void OwnerTickPresentation( bool aimDownSights, bool reloadGameplayStartedThisTick, float gameplayReloadSeconds, bool primaryFireHeld, int firingModeGraphEnum, bool driveMeleeKnifeExtras, bool sprintHeld, bool shellReloadPresentation = false, int shellReloadAmmoBeforeRpc = -1, bool shotgunPumpSessionHeld = false )
	{
		if ( !_equipPlaybackDone || !_skin.IsValid() )
			return;

		_shotgunPumpSessionActiveForPresentation = shotgunPumpSessionHeld;
		SyncShotgunPumpGraphStanceLatch( shotgunPumpSessionHeld );

		// Knife / melee: Attack2 is secondary swing, not ADS — never blend to ironsights pose or graph scalars.
		var presentationAds = driveMeleeKnifeExtras ? false : aimDownSights;
		_desiredAdsPose = UseGraphIronsightsParameterForAds
			? presentationAds
			: ( presentationAds && !string.IsNullOrWhiteSpace( AdsSequenceName ) );

		if ( reloadGameplayStartedThisTick )
		{
			if ( shellReloadPresentation )
			{
				var tubeBefore = shellReloadAmmoBeforeRpc < 0 ? 1 : shellReloadAmmoBeforeRpc;
				var tubeEmpty = tubeBefore <= 0;
				if ( TryUseStockShotgunGraphShellImpulses() )
					EnqueueShotgunGraphShellCycle( gameplayReloadSeconds, tubeEmpty );
				else if ( !string.IsNullOrWhiteSpace( ReloadFirstShellSequenceName ) || !string.IsNullOrWhiteSpace( ReloadSequenceName ) )
				{
					var holdStanceAfterEachShell = shotgunPumpSessionHeld;
					_ = ReloadPresentationAsync( gameplayReloadSeconds, shotgunShellCycle: true, tubeWasEmptyBeforeThisShell: tubeEmpty, releaseShotgunReloadingStanceInFinally: !holdStanceAfterEachShell );
				}
			}
			else if ( !string.IsNullOrWhiteSpace( ReloadSequenceName ) )
				_ = ReloadPresentationAsync( gameplayReloadSeconds );
		}

		OwnerTickCombatGraphParameters( presentationAds, primaryFireHeld, firingModeGraphEnum );
		OwnerTickMeleeKnifePresentation( driveMeleeKnifeExtras, sprintHeld );

		if ( _reloadAnimRunning )
			return;

		if ( driveMeleeKnifeExtras )
			return;

		TrySwapAdsVersusIdleIfChanged();
	}

	/// <summary>
	/// Bare hands: switch between <see cref="IdleSequenceName"/> (idle pose) and <see cref="BareHandsLocomotionMoveSequenceName"/> from planar speed.
	/// </summary>
	public void OwnerTickBareHandsLocomotion( float planarSpeed )
	{
		if ( !BareHandsLocomotionBlendEnabled
		     || string.IsNullOrWhiteSpace( BareHandsLocomotionMoveSequenceName )
		     || string.IsNullOrWhiteSpace( IdleSequenceName )
		     || !LoopIdleWithDirectPlayback
		     || !_equipPlaybackDone
		     || _reloadAnimRunning
		     || _meleeAttackAnimRunning
		     || !FpAnimClientGuard() )
			return;

		var enter = Math.Max( 1f, BareHandsMoveSpeedEnter );
		var exit = Math.Clamp( BareHandsMoveSpeedExit, 0f, enter - 1f );

		var wantMove = _bareHandsLocomotionIsMoving
			? planarSpeed >= exit
			: planarSpeed >= enter;

		if ( wantMove == _bareHandsLocomotionIsMoving )
			return;

		_bareHandsLocomotionIsMoving = wantMove;

		if ( TryGetSceneModel() is not { } sm )
			return;

		sm.UseAnimGraph = true;
		var seq = wantMove ? BareHandsLocomotionMoveSequenceName : IdleSequenceName;
		if ( string.IsNullOrWhiteSpace( seq ) )
			return;

		sm.DirectPlayback.Play( seq );
	}

	void OwnerTickMeleeKnifePresentation( bool driveMeleeKnifeExtras, bool sprintHeld )
	{
		if ( !driveMeleeKnifeExtras || !_model.IsValid() )
			return;

		var g = _model.AnimGraph;
		if ( g is null || g.IsError || !g.TryGetParameterIndex( GraphParamSprint, out _ ) )
			return;

		if ( _reloadAnimRunning || _meleeAttackAnimRunning )
		{
			SetParamOnAllSkins( GraphParamSprint, false );
			return;
		}

		SetParamOnAllSkins( GraphParamSprint, sprintHeld );
	}

	/// <summary>Host has accepted a round (ammo decremented). Pulses <c>b_attack</c> — local client only, never called from server simulation.</summary>
	public void OwnerNotifyServerConfirmedFire()
	{
		if ( !FpAnimClientGuard() || !_equipPlaybackDone || !_skin.IsValid() || !_model.IsValid() )
			return;

		var g = _model.AnimGraph;
		if ( g is null || g.IsError || !g.TryGetParameterIndex( GraphParamAttack, out _ ) )
			return;

		SetParamOnAllSkins( GraphParamAttack, true );
	}

	/// <summary>FP melee: authoritative hit plays a one-shot <see cref="SceneModel.DirectPlayback"/> clip (<c>Attack_01a</c> / <c>Backstab_Attack</c> etc.).</summary>
	public void OwnerNotifyMeleeAttackCommitted( bool heavy, float lightAttackPresentationDurationMultiplier = 1f )
	{
		if ( !FpAnimClientGuard() || !_skin.IsValid() )
			return;

		if ( !_equipPlaybackDone && !BareHandsLocomotionBlendEnabled )
			return;

		string seq;
		if ( heavy )
			seq = string.IsNullOrWhiteSpace( MeleeHeavyAttackSequenceName ) ? "Backstab_Attack" : MeleeHeavyAttackSequenceName;
		else
		{
			var primary = string.IsNullOrWhiteSpace( MeleeLightAttackSequenceName ) ? "Attack_01a" : MeleeLightAttackSequenceName;
			if ( !string.IsNullOrWhiteSpace( MeleeLightAttackAlternateSequenceName ) )
			{
				seq = _meleeLightAlternateNext ? MeleeLightAttackAlternateSequenceName : primary;
				_meleeLightAlternateNext = !_meleeLightAlternateNext;
			}
			else
				seq = primary;
		}

		_ = PlayMeleeAttackPresentationAsync( seq, heavy, lightAttackPresentationDurationMultiplier );
	}

	async Task PlayMeleeAttackPresentationAsync( string sequenceName, bool heavy, float lightAttackPresentationDurationMultiplier )
	{
		if ( string.IsNullOrWhiteSpace( sequenceName ) || !_skin.IsValid() )
			return;

		var ct = _lifecycle?.Token ?? default;
		var scene = await WaitForFpSceneModelAsync( ct, maxSeconds: 0.85f );
		if ( !scene.IsValid() )
			return;

		_meleeAttackAnimRunning = true;

		try
		{
			scene.UseAnimGraph = true;
			scene.DirectPlayback.Play( sequenceName );
			await Task.DelayRealtimeSeconds( 0.02f );

			if ( ct.IsCancellationRequested || !_skin.IsValid() )
				return;

			var d = scene.DirectPlayback.Duration;
			var wait = d > 0.005f ? d : MeleeAttackDurationFallbackSeconds;
			if ( !heavy )
				wait *= Math.Clamp( lightAttackPresentationDurationMultiplier, 0.05f, 4f );
			await Task.DelayRealtimeSeconds( wait );
		}
		finally
		{
			_meleeAttackAnimRunning = false;

			if ( _skin.IsValid() && GameObject.IsValid() && !_reloadAnimRunning && TryGetSceneModel() is { } sm )
			{
				if ( !LoopIdleWithDirectPlayback )
					ReleaseDirectPlaybackLayer( sm );

				if ( BareHandsLocomotionBlendEnabled
				     && !string.IsNullOrWhiteSpace( BareHandsLocomotionMoveSequenceName )
				     && !string.IsNullOrWhiteSpace( IdleSequenceName )
				     && LoopIdleWithDirectPlayback )
				{
					sm.UseAnimGraph = true;
					var resume = _bareHandsLocomotionIsMoving ? BareHandsLocomotionMoveSequenceName : IdleSequenceName;
					sm.DirectPlayback.Play( resume );
				}
				else
				{
					TrySnapToAdsOrIdleFromDesiredPreference();
					_appliedAdsPose = _desiredAdsPose;
				}
			}
		}
	}

	void OwnerTickCombatGraphParameters( bool aimDownSights, bool primaryFireHeld, int firingModeGraphEnum )
	{
		if ( !_model.IsValid() )
			return;

		var g = _model.AnimGraph;
		if ( g is null || g.IsError )
			return;

		if ( g.TryGetParameterIndex( GraphParamFiringMode, out _ ) )
			SetParamOnAllSkins( GraphParamFiringMode, firingModeGraphEnum );

		var dt = Time.Delta;
		var rampUp = AttackHoldRampUpPerSecond;
		if ( firingModeGraphEnum == 3 && primaryFireHeld )
			rampUp *= FullAutoAttackHoldRampMultiplier;

		if ( primaryFireHeld )
			_attackHold01 = Math.Min( 1f, _attackHold01 + dt * rampUp );
		else
			_attackHold01 = Math.Max( 0f, _attackHold01 - dt * AttackHoldDecayPerSecond );

		if ( g.TryGetParameterIndex( GraphParamAttackHold, out _ ) )
			SetParamOnAllSkins( GraphParamAttackHold, _attackHold01 );

		if ( g.TryGetParameterIndex( GraphParamIronsightsFireScale, out _ ) )
			SetParamOnAllSkins( GraphParamIronsightsFireScale, aimDownSights ? IronsightsFireScaleWhileAds : 1f );

		if ( g.TryGetParameterIndex( GraphParamCameraRotationScale, out _ ) )
			SetParamOnAllSkins( GraphParamCameraRotationScale, aimDownSights ? AdsCameraRotationScale : HipCameraRotationScale );

		if ( Time.Now >= _nextAttackHoldDebugLogTime && ( primaryFireHeld || _attackHold01 > 0.02f ) )
		{
			_nextAttackHoldDebugLogTime = Time.Now + 0.25;
		}
	}

	void TrySwapAdsVersusIdleIfChanged()
	{
		var want = _desiredAdsPose;
		if ( want == _appliedAdsPose )
			return;

		if ( UseGraphIronsightsParameterForAds && TryApplyIronsightsGraphParameter( want ) )
		{
			_appliedAdsPose = want;
			return;
		}

		if ( TryGetSceneModel() is not { } scene )
			return;

		scene.UseAnimGraph = true;
		var seq = want ? AdsSequenceName : IdleSequenceName;
		if ( string.IsNullOrWhiteSpace( seq ) )
			seq = IdleSequenceName;

		scene.DirectPlayback.Play( seq );
		_appliedAdsPose = want;
	}

	bool TryApplyIronsightsGraphParameter( bool ads )
	{
		if ( !_skin.IsValid() || !_model.IsValid() )
			return false;

		var g = _model.AnimGraph;
		if ( g is null || g.IsError || !g.TryGetParameterIndex( GraphParamIronsights, out _ ) )
			return false;

		// Graph may wire ironsights as float blend or enum-as-float (see stock weapon docs).
		SetParamOnAllSkins( GraphParamIronsights, ads ? 1f : 0f );
		return true;
	}

	/// <summary>
	/// Clears AnimGraph direct playback (<see cref="AnimGraphDirectPlayback.Cancel"/>) so graph parameter layers (fire/ADS) evaluate.
	/// </summary>
	void ReleaseDirectPlaybackLayer( SceneModel scene )
	{
		if ( !scene.IsValid() )
			return;

		scene.DirectPlayback.Cancel();
	}

	void ApplyBaselineCombatGraphParameters()
	{
		_attackHold01 = 0f;

		if ( !_skin.IsValid() || !_model.IsValid() )
			return;

		var g = _model.AnimGraph;
		if ( g is null || g.IsError )
			return;

		if ( g.TryGetParameterIndex( GraphParamAttackHold, out _ ) )
			SetParamOnAllSkins( GraphParamAttackHold, 0f );

		if ( g.TryGetParameterIndex( GraphParamIronsights, out _ ) )
			SetParamOnAllSkins( GraphParamIronsights, 0f );

		if ( g.TryGetParameterIndex( GraphParamIronsightsFireScale, out _ ) )
			SetParamOnAllSkins( GraphParamIronsightsFireScale, 1f );

		if ( g.TryGetParameterIndex( GraphParamCameraRotationScale, out _ ) )
			SetParamOnAllSkins( GraphParamCameraRotationScale, HipCameraRotationScale );
	}

	async Task ReloadPresentationAsync( float gameplayReloadSeconds ) =>
		await ReloadPresentationAsync( gameplayReloadSeconds, shotgunShellCycle: false, tubeWasEmptyBeforeThisShell: false, releaseShotgunReloadingStanceInFinally: true );

	/// <param name="tubeWasEmptyBeforeThisShell">When <paramref name="shotgunShellCycle"/> is true: tube ammo before this shell RPC (0 ⇒ <see cref="ReloadFirstShellSequenceName"/> / <c>b_reloading_first_shell</c>).</param>
	/// <param name="releaseShotgunReloadingStanceInFinally">When false (tube pump mirroring active), caller drops <see cref="GraphParamReloading"/> when pump ends instead.</param>
	async Task ReloadPresentationAsync( float gameplayReloadSeconds, bool shotgunShellCycle, bool tubeWasEmptyBeforeThisShell, bool releaseShotgunReloadingStanceInFinally )
	{
		if ( _reloadAnimRunning )
			return;

		if ( TryGetSceneModel() is not { } scene )
			return;

		string sequenceName;
		if ( shotgunShellCycle )
		{
			if ( tubeWasEmptyBeforeThisShell && !string.IsNullOrWhiteSpace( ReloadFirstShellSequenceName ) )
				sequenceName = ReloadFirstShellSequenceName;
			else if ( !string.IsNullOrWhiteSpace( ReloadSequenceName ) )
				sequenceName = ReloadSequenceName;
			else
				return;
		}
		else
		{
			if ( string.IsNullOrWhiteSpace( ReloadSequenceName ) )
				return;
			sequenceName = ReloadSequenceName;
		}

		_reloadAnimRunning = true;

		try
		{
			var ct = _lifecycle?.Token ?? default;

			scene.UseAnimGraph = true;

			if ( shotgunShellCycle )
			{
				// FP shotgun doc order: enable b_reloading stance, then fire self-resetting b_reloading_shell / b_reloading_first_shell.
				TrySetShotgunReloadingGraphState( true );
				await PulseShotgunShellReloadGraphAsync( tubeWasEmptyBeforeThisShell, ct );
			}

			scene.DirectPlayback.Play( sequenceName );

			await Task.DelayRealtimeSeconds( 0.03f );

			if ( ct.IsCancellationRequested || !_skin.IsValid() )
				return;

			var d = scene.DirectPlayback.Duration;
			var clipSeconds = d > 0.005f ? d : ReloadDurationFallbackSeconds;
			var waitSeconds = Math.Max( clipSeconds, gameplayReloadSeconds );

			await Task.DelayRealtimeSeconds( waitSeconds );
		}
		finally
		{
			if ( shotgunShellCycle && releaseShotgunReloadingStanceInFinally )
				TrySetShotgunReloadingGraphState( false );

			_reloadAnimRunning = false;

			if ( _skin.IsValid() && GameObject.IsValid() && _equipPlaybackDone && TryGetSceneModel() is { } sm )
			{
				if ( !LoopIdleWithDirectPlayback )
					ReleaseDirectPlaybackLayer( sm );

				if ( UseGraphIronsightsParameterForAds )
				{
					TryApplyIronsightsGraphParameter( _desiredAdsPose );
					_appliedAdsPose = _desiredAdsPose;
				}
				else
				{
					TrySnapToAdsOrIdleFromDesiredPreference();
					_appliedAdsPose = _desiredAdsPose;
				}
			}
		}
	}

	/// <summary>v_spaghellim4-style graphs: impulses on <see cref="GraphParamReloadingShell"/> / <see cref="GraphParamReloadingFirstShell"/>.</summary>
	bool TryUseStockShotgunGraphShellImpulses()
	{
		if ( !_model.IsValid() )
			return false;

		var graph = _model.AnimGraph;
		if ( graph is null || graph.IsError )
			return false;

		return graph.TryGetParameterIndex( GraphParamReloadingShell, out _ )
		       || graph.TryGetParameterIndex( GraphParamReloadingFirstShell, out _ );
	}

	void SyncShotgunPumpGraphStanceLatch( bool shotgunPumpSessionHeld )
	{
		if ( shotgunPumpSessionHeld == _lastSyncedShotgunPumpSession )
			return;

		_lastSyncedShotgunPumpSession = shotgunPumpSessionHeld;

		if ( shotgunPumpSessionHeld )
		{
			_wantReleaseShotgunReloadingGraphAfterDrain = false;
			TrySetShotgunReloadingGraphState( true );
		}
		else
		{
			_shotgunGraphShellQueue.Clear();
			_wantReleaseShotgunReloadingGraphAfterDrain = true;
			TryReleaseShotgunReloadingGraphStanceAfterQueueDrainImmediate();
		}
	}

	void TryReleaseShotgunReloadingGraphStanceAfterQueueDrainImmediate()
	{
		if ( _wantReleaseShotgunReloadingGraphAfterDrain && !_shotgunGraphReloadWorkerBusy && _shotgunGraphShellQueue.Count <= 0 )
		{
			TrySetShotgunReloadingGraphState( false );
			_wantReleaseShotgunReloadingGraphAfterDrain = false;
		}
	}

	void EnqueueShotgunGraphShellCycle( float gameplayReloadSeconds, bool tubeWasEmptyBeforeShell )
	{
		_shotgunGraphShellQueue.Enqueue( ( gameplayReloadSeconds, tubeWasEmptyBeforeShell ) );
		_ = ProcessShotgunGraphShellQueueAsync();
	}

	async Task ProcessShotgunGraphShellQueueAsync()
	{
		if ( _shotgunGraphReloadWorkerBusy )
			return;

		if ( !_skin.IsValid() )
			return;

		var ct = _lifecycle?.Token ?? default;
		_shotgunGraphReloadWorkerBusy = true;
		_reloadAnimRunning = true;

		try
		{
			while ( _shotgunGraphShellQueue.Count > 0 && !ct.IsCancellationRequested )
			{
				var ( secs, tubeEmpty ) = _shotgunGraphShellQueue.Dequeue();
				await ExecuteShotgunGraphOnlyShellCycleAsync( secs, tubeEmpty, ct );
			}
		}
		finally
		{
			_shotgunGraphReloadWorkerBusy = false;
			_reloadAnimRunning = false;
			TryReleaseShotgunReloadingGraphStanceAfterQueueDrainImmediate();

			if ( _skin.IsValid() && GameObject.IsValid() && _equipPlaybackDone && TryGetSceneModel() is { } sm )
			{
				if ( !LoopIdleWithDirectPlayback )
					ReleaseDirectPlaybackLayer( sm );

				if ( UseGraphIronsightsParameterForAds )
				{
					TryApplyIronsightsGraphParameter( _desiredAdsPose );
					_appliedAdsPose = _desiredAdsPose;
				}
				else
				{
					TrySnapToAdsOrIdleFromDesiredPreference();
					_appliedAdsPose = _desiredAdsPose;
				}
			}

			if ( _shotgunGraphShellQueue.Count > 0 && !ct.IsCancellationRequested )
				_ = ProcessShotgunGraphShellQueueAsync();
		}
	}

	async Task ExecuteShotgunGraphOnlyShellCycleAsync( float gameplayReloadSeconds, bool tubeWasEmptyBeforeShell, CancellationToken ct )
	{
		if ( TryGetSceneModel() is not { } scene )
			return;

		scene.UseAnimGraph = true;
		TrySetShotgunReloadingGraphState( true );
		await PulseShotgunShellReloadGraphAsync( tubeWasEmptyBeforeShell, ct );

		if ( ct.IsCancellationRequested || !_skin.IsValid() )
			return;

		var waitSeconds = MathF.Max(
			gameplayReloadSeconds,
			MathF.Max( ShellReloadGraphPulseHoldSeconds * 2f, 0.02f ) );
		await Task.DelayRealtimeSeconds( waitSeconds );
	}

	async Task PulseShotgunShellReloadGraphAsync( bool firstShellIntoEmptyTube, CancellationToken ct )
	{
		if ( !_model.IsValid() )
			return;

		var g = _model.AnimGraph;
		if ( g is null || g.IsError )
			return;

		string trigger = null;
		if ( firstShellIntoEmptyTube && g.TryGetParameterIndex( GraphParamReloadingFirstShell, out _ ) )
			trigger = GraphParamReloadingFirstShell;
		else if ( g.TryGetParameterIndex( GraphParamReloadingShell, out _ ) )
			trigger = GraphParamReloadingShell;

		if ( trigger is null )
			return;

		SetParamOnAllSkins( trigger, true );
		await Task.DelayRealtimeSeconds( ShellReloadGraphPulseHoldSeconds );
		if ( ct.IsCancellationRequested )
			return;
		SetParamOnAllSkins( trigger, false );
	}

	void TrySetShotgunReloadingGraphState( bool reloading )
	{
		if ( !_model.IsValid() )
			return;

		var g = _model.AnimGraph;
		if ( g is null || g.IsError || !g.TryGetParameterIndex( GraphParamReloading, out _ ) )
			return;

		SetParamOnAllSkins( GraphParamReloading, reloading );
	}

	void TrySnapToAdsOrIdleFromDesiredPreference()
	{
		var want = _desiredAdsPose;

		if ( UseGraphIronsightsParameterForAds && TryApplyIronsightsGraphParameter( want ) )
			return;

		if ( TryGetSceneModel() is not { } scene )
			return;

		scene.UseAnimGraph = true;
		var seq = want ? AdsSequenceName : IdleSequenceName;
		if ( string.IsNullOrWhiteSpace( seq ) )
			seq = IdleSequenceName;

		scene.DirectPlayback.Play( seq );
	}

	static void LogAnimGraphParameters( AnimationGraph g )
	{
		if ( g is null || g.IsError || g.ParamCount <= 0 )
			return;

		var sb = new StringBuilder( 128 );
		for ( var i = 0; i < g.ParamCount; i++ )
		{
			sb.Append( g.GetParameterName( i ) );
			if ( i + 1 < g.ParamCount )
				sb.Append( ", " );
		}

	}

	void AddDeployLift()
	{
		var lift = DeploySequenceVerticalLift;
		if ( MathF.Abs( lift ) < 0.0001f )
			return;

		GameObject.LocalPosition += new Vector3( 0f, lift, 0f );
		_deployLiftApplied = lift;
	}

	void RemoveDeployLift()
	{
		if ( MathF.Abs( _deployLiftApplied ) < 0.0001f )
			return;

		GameObject.LocalPosition -= new Vector3( 0f, _deployLiftApplied, 0f );
		_deployLiftApplied = 0f;
	}


}
