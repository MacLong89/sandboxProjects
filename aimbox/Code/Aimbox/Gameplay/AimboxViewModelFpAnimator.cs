using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sandbox;

[Title( "Aimbox Viewmodel FP Animator" )]
[Category( "Aimbox" )]
public sealed class AimboxViewModelFpAnimator : Component
{
	public const float EquipRoutineInitialDelaySeconds = 0.06f;
	public const float DefaultDeployDurationFallbackSeconds = 0.85f;

	[Property] public string DeploySequenceName { get; set; } = "Deploy_Slide";
	[Property] public string IdleSequenceName { get; set; } = "IdlePose";
	[Property] public string AdsSequenceName { get; set; } = "Ironsights_Pose_Normal";
	[Property] public string ReloadSequenceName { get; set; } = "Reload_Empty";
	[Property] public string ReloadFirstShellSequenceName { get; set; } = "";
	[Property] public float DeployDurationFallbackSeconds { get; set; } = 0.85f;
	[Property] public float ReloadDurationFallbackSeconds { get; set; } = 2f;
	[Property] public bool UseGraphIronsightsParameterForAds { get; set; } = true;
	[Property] public float IronsightsBlendPerSecond { get; set; } = 7f;
	[Property] public float AttackHoldRampUpPerSecond { get; set; } = 4f;
	[Property] public float AttackHoldDecayPerSecond { get; set; } = 6f;
	[Property] public float FullAutoAttackHoldRampMultiplier { get; set; } = 2.25f;
	[Property] public float AdsPresentationSpeedMultiplier { get; set; } = 2f;
	[Property] public string MeleeLightAttackSequenceName { get; set; } = "";
	[Property] public string MeleeHeavyAttackSequenceName { get; set; } = "";
	[Property] public float MeleeAttackDurationFallbackSeconds { get; set; } = 0.45f;
	[Property] public float ShellReloadGraphPulseHoldSeconds { get; set; } = 0.08f;
	[Property] public bool IsGrenadePresentation { get; set; }
	[Property] public float GrenadeThrowReleaseDelaySeconds { get; set; } = 0.14f;
	[Property] public float GrenadeThrowAnimHoldSeconds { get; set; } = 0.52f;

	public bool GrenadeReady { get; private set; }
	public bool GrenadeCharging => _grenadeCharging;
	public bool GrenadeThrowAnimRunning => _grenadeThrowAnimRunning;

	public bool PresentationAllowsCombatFire =>
		(!_skin.IsValid() || _equipPlaybackDone) && !_reloadAnimRunning && !_meleeAttackAnimRunning;

	public bool PresentationAllowsAds => !_skin.IsValid() || _equipPlaybackDone;
	/// <summary>Scope PiP only while the viewmodel is in a steady ADS pose (not deploy/reload/melee).</summary>
	public bool PresentationAllowsScopePip => PresentationAllowsCombatFire;

	public float DebugAttackHold01 => _attackHold01;

	const string GraphParamAttack = "b_attack";
	const string GraphParamAttackHold = "attack_hold";
	const string GraphParamIronsights = "ironsights";
	const string GraphParamIronsightsFireScale = "ironsights_fire_scale";
	const string GraphParamCameraRotationScale = "camera_rotation_scale";
	const string GraphParamFiringMode = "firing_mode";
	const string GraphParamSprint = "b_sprint";
	const string GraphParamReloading = "b_reloading";
	const string GraphParamBulkReload = "b_reload";
	const string GraphParamReloadingFirstShell = "b_reloading_first_shell";
	const string GraphParamReloadingShell = "b_reloading_shell";
	const string GraphParamWeaponPose = "weapon_pose";
	const string GraphParamDuckLevel = "duck_level";
	const string GraphParamBDuck = "b_duck";
	const string GraphParamAimPitch = "aim_pitch";
	const string GraphParamAimYaw = "aim_yaw";
	const string GraphParamGrounded = "b_grounded";
	const string GraphParamMoveBob = "move_bob";
	const string GraphParamCharge = "b_charge";
	const string GraphParamPull = "b_pull";
	const string GraphParamThrow = "b_throw";
	const string GraphParamPinRemove = "b_pin_remove";
	const string GraphParamChargeType = "charge_type";
	const string GraphParamThrowType = "throw_type";
	const string GraphParamThrowBlend = "throw_blend";
	SkinnedModelRenderer _skin;
	Model _model;
	CancellationTokenSource _lifecycle;
	readonly List<SkinnedModelRenderer> _linkedSkins = [];
	bool _equipPlaybackDone;
	bool _reloadAnimRunning;
	bool _meleeAttackAnimRunning;
	bool _desiredAdsPose;
	bool _appliedAdsPose;
	float _attackHold01;
	float _ironsightsBlend01;
	bool _shotgunGraphReloadWorkerBusy;
	bool _wantReleaseShotgunReloadingGraphAfterDrain;
	bool _lastSyncedShotgunPumpSession;
	float _presentationSpeedMultiplier = 1f;
	readonly Queue<(float seconds, bool tubeEmpty)> _shotgunGraphShellQueue = new();
	bool _grenadeThrowAnimRunning;
	bool _grenadeThrowReleased;
	bool _grenadeCharging;
	Action _grenadeThrowReleaseCallback;
	Action _grenadeThrowCompleteCallback;

	public float IronsightsBlend01 => _ironsightsBlend01;

	public readonly record struct AdsDebugState(
		float IronsightsBlend01,
		bool GraphHasIronsights,
		bool UseGraphIronsights,
		bool DesiredAdsPose,
		bool AppliedAdsPose,
		bool EquipPlaybackDone,
		bool SkinUsesAnimGraph );

	public AdsDebugState GetAdsDebugState() => new(
		_ironsightsBlend01,
		GraphHasIronsightsParam(),
		UseGraphIronsightsParameterForAds,
		_desiredAdsPose,
		_appliedAdsPose,
		_equipPlaybackDone,
		_skin.IsValid() && _skin.UseAnimGraph );

	public void CancelActivePresentation()
	{
		_lifecycle?.Cancel();
		_lifecycle?.Dispose();
		_lifecycle = null;
		_shotgunGraphShellQueue.Clear();
		_reloadAnimRunning = false;
		_meleeAttackAnimRunning = false;
		_shotgunGraphReloadWorkerBusy = false;
		_wantReleaseShotgunReloadingGraphAfterDrain = false;
		_grenadeThrowAnimRunning = false;
		_grenadeThrowReleased = false;
		_grenadeCharging = false;
		_grenadeThrowReleaseCallback = null;
		_grenadeThrowCompleteCallback = null;
		GrenadeReady = false;
		_equipPlaybackDone = false;
	}

	/// <summary>Clears stuck presentation locks so gameplay fire/ADS can resume (respawn, hidden viewmodel, etc.).</summary>
	public void ForceCombatReady()
	{
		_lifecycle?.Cancel();
		_lifecycle?.Dispose();
		_lifecycle = null;
		_shotgunGraphShellQueue.Clear();
		_reloadAnimRunning = false;
		_meleeAttackAnimRunning = false;
		_shotgunGraphReloadWorkerBusy = false;
		_wantReleaseShotgunReloadingGraphAfterDrain = false;
		_grenadeThrowAnimRunning = false;
		_grenadeThrowReleased = false;
		_grenadeCharging = false;
		_grenadeThrowReleaseCallback = null;
		_grenadeThrowCompleteCallback = null;
		GrenadeReady = false;
		_equipPlaybackDone = true;
		_desiredAdsPose = false;
		_appliedAdsPose = false;
		_attackHold01 = 0f;

		if ( _skin.IsValid() )
			ApplyBaselineGraphParameters();
	}

	public void SetPresentationSpeedMultiplier( float multiplier ) =>
		_presentationSpeedMultiplier = Math.Clamp( multiplier, 0.25f, 1.5f );

	public void BindSkinReadyForCombat( SkinnedModelRenderer skin, Model model, float presentationSpeedMultiplier = 1f )
	{
		SetPresentationSpeedMultiplier( presentationSpeedMultiplier );
		_lifecycle?.Cancel();
		_lifecycle?.Dispose();
		_lifecycle = null;

		_skin = skin;
		_model = model;
		_linkedSkins.Clear();
		_equipPlaybackDone = true;
		_reloadAnimRunning = false;
		_meleeAttackAnimRunning = false;
		_desiredAdsPose = false;
		_appliedAdsPose = false;
		_attackHold01 = 0f;
		_ironsightsBlend01 = 0f;
		ApplyBaselineGraphParameters();
	}

	public void BindAndRunEquipRoutine( SkinnedModelRenderer skin, Model model, float presentationSpeedMultiplier = 1f )
	{
		SetPresentationSpeedMultiplier( presentationSpeedMultiplier );
		_lifecycle?.Cancel();
		_lifecycle?.Dispose();
		_lifecycle = new CancellationTokenSource();

		_skin = skin;
		_model = model;
		_linkedSkins.Clear();
		_equipPlaybackDone = false;
		_reloadAnimRunning = false;
		_meleeAttackAnimRunning = false;
		_desiredAdsPose = false;
		_appliedAdsPose = false;
		_attackHold01 = 0f;
		_ironsightsBlend01 = 0f;

		if ( !_skin.IsValid() || !_model.IsValid() || !ClientFxContext() )
		{
			_equipPlaybackDone = true;
			return;
		}

		_skin.UseAnimGraph = true;
		_ = IsGrenadePresentation ? GrenadeEquipRoutineAsync( _lifecycle.Token ) : EquipRoutineAsync( _lifecycle.Token );
	}

	public void OwnerTickGrenadePresentation(
		bool sprintHeld,
		bool crouching,
		Angles eyeAngles,
		Vector3 velocityWorld,
		bool grounded,
		float runSpeed )
	{
		if ( !_equipPlaybackDone || !_skin.IsValid() )
			return;

		OwnerTickMovementGraphParameters(
			crouching,
			eyeAngles,
			velocityWorld,
			grounded,
			runSpeed,
			sprintHeld,
			aimDownSights: false,
			meleeWeapon: false );
	}

	public bool BeginGrenadeCharge()
	{
		if ( !ClientFxContext() || !_skin.IsValid() || !_equipPlaybackDone || !GrenadeReady
		     || _grenadeCharging || _grenadeThrowAnimRunning )
			return false;

		_grenadeCharging = true;
		SetParamOnSkins( GraphParamCharge, true );
		TrySetGrenadeGraphParam( GraphParamPull, true );
		if ( GraphHasParam( GraphParamChargeType ) )
			SetParamOnSkins( GraphParamChargeType, 0 );
		return true;
	}

	public void ReleaseGrenadeThrow( Action onRelease, Action onComplete )
	{
		if ( !ClientFxContext() || !_skin.IsValid() || !_equipPlaybackDone || !_grenadeCharging
		     || _grenadeThrowAnimRunning )
			return;

		_grenadeThrowReleaseCallback = onRelease;
		_grenadeThrowCompleteCallback = onComplete;
		_grenadeThrowAnimRunning = true;
		_grenadeThrowReleased = false;
		_grenadeCharging = false;
		GrenadeReady = false;

		_ = GrenadeThrowRoutineAsync( _lifecycle?.Token ?? default );
	}

	public void ReleaseGrenadeQuickToss( Action onRelease, Action onComplete )
	{
		if ( !ClientFxContext() || !_skin.IsValid() || !_equipPlaybackDone || !GrenadeReady
		     || _grenadeThrowAnimRunning )
			return;

		_grenadeCharging = false;
		EndGrenadeChargeGraphState();
		_grenadeThrowReleaseCallback = onRelease;
		_grenadeThrowCompleteCallback = onComplete;
		_grenadeThrowAnimRunning = true;
		_grenadeThrowReleased = false;
		GrenadeReady = false;

		_ = GrenadeQuickTossRoutineAsync( _lifecycle?.Token ?? default );
	}

	public void AddLinkedArms( SkinnedModelRenderer arms )
	{
		if ( arms.IsValid() && arms != _skin && !_linkedSkins.Contains( arms ) )
			_linkedSkins.Add( arms );
	}

	public void SetWeaponPose( int pose )
	{
		if ( !_skin.IsValid() || !_model.IsValid() )
			return;

		var graph = _model.AnimGraph;
		if ( graph is null || graph.IsError || !graph.TryGetParameterIndex( GraphParamWeaponPose, out _ ) )
			return;

		SetParamOnSkins( GraphParamWeaponPose, pose );
	}

	public void OwnerTickPresentation(
		bool aimDownSights,
		bool reloadStartedThisTick,
		float reloadSeconds,
		bool primaryFireHeld,
		int firingModeGraphEnum,
		bool meleeWeapon,
		bool sprintHeld,
		float presentationSpeedMultiplier = 1f,
		bool shotgunShellReload = false,
		int ammoBeforeReload = -1,
		bool shotgunPumpSessionHeld = false,
		bool crouching = false,
		Angles eyeAngles = default,
		Vector3 velocityWorld = default,
		bool grounded = true,
		float runSpeed = 320f )
	{
		if ( !_equipPlaybackDone || !_skin.IsValid() )
		{
			if ( !_skin.IsValid() )
				AimboxViewModelMovementDebug.LogTickBlocked( "skin invalid" );
			else
				AimboxViewModelMovementDebug.LogTickBlocked( "equip animation not finished (_equipPlaybackDone=false)" );
			return;
		}

		SetPresentationSpeedMultiplier( presentationSpeedMultiplier );

		var presentationAds = !meleeWeapon && aimDownSights;

		OwnerTickMovementGraphParameters(
			crouching,
			eyeAngles,
			velocityWorld,
			grounded,
			runSpeed,
			sprintHeld,
			presentationAds,
			meleeWeapon );

		SyncShotgunPumpGraphStanceLatch( shotgunPumpSessionHeld );

		var useGraphIronsights = UseGraphIronsightsParameterForAds && GraphHasIronsightsParam();
		_desiredAdsPose = useGraphIronsights
			? presentationAds
			: presentationAds && !string.IsNullOrWhiteSpace( AdsSequenceName );

		if ( reloadStartedThisTick && !meleeWeapon )
		{
			if ( shotgunShellReload )
			{
				var tubeBefore = ammoBeforeReload < 0 ? 1 : ammoBeforeReload;
				var tubeEmpty = tubeBefore <= 0;
				if ( TryUseStockShotgunGraphShellImpulses() )
					EnqueueShotgunGraphShellCycle( reloadSeconds, tubeEmpty );
				else
					_ = ReloadPresentationAsync( reloadSeconds, true, tubeEmpty );
			}
			else
			{
				_ = ReloadPresentationAsync( reloadSeconds );
			}
		}

		OwnerTickCombatGraphParameters( presentationAds, primaryFireHeld, firingModeGraphEnum );
		OwnerTickIronsightsGraphBlend( presentationAds );

		if ( !_reloadAnimRunning && !meleeWeapon )
			TrySwapAdsVersusIdleIfChanged();
	}

	public void OwnerNotifyServerConfirmedFire()
	{
		if ( !ClientFxContext() || !_equipPlaybackDone || !_skin.IsValid() || !_model.IsValid() )
			return;

		var graph = _model.AnimGraph;
		if ( graph is null || graph.IsError || !graph.TryGetParameterIndex( GraphParamAttack, out _ ) )
			return;

		SetParamOnSkins( GraphParamAttack, true );
	}

	public void OwnerNotifyMeleeAttackCommitted( bool heavy )
	{
		if ( !ClientFxContext() || !_skin.IsValid() || !_equipPlaybackDone )
			return;

		var seq = heavy
			? (string.IsNullOrWhiteSpace( MeleeHeavyAttackSequenceName ) ? "Backstab_Attack" : MeleeHeavyAttackSequenceName)
			: (string.IsNullOrWhiteSpace( MeleeLightAttackSequenceName ) ? "Attack_01a" : MeleeLightAttackSequenceName);

		_ = MeleePresentationAsync( seq );
	}

	async Task EquipRoutineAsync( CancellationToken ct )
	{
		await Task.DelayRealtimeSeconds( EquipRoutineInitialDelaySeconds );
		if ( ct.IsCancellationRequested || !_skin.IsValid() )
		{
			_equipPlaybackDone = true;
			return;
		}

		var scene = await WaitForSceneModelAsync( ct, 2.5f );
		if ( !scene.IsValid() )
		{
			_equipPlaybackDone = true;
			return;
		}

		scene.UseAnimGraph = true;
		if ( !string.IsNullOrWhiteSpace( DeploySequenceName ) )
			scene.DirectPlayback.Play( DeploySequenceName );

		await Task.DelayRealtimeSeconds( 0.03f );
		var wait = scene.DirectPlayback.Duration > 0.005f ? scene.DirectPlayback.Duration : DeployDurationFallbackSeconds;
		wait *= _presentationSpeedMultiplier;
		await Task.DelayRealtimeSeconds( wait );
		if ( ct.IsCancellationRequested || !_skin.IsValid() || !scene.IsValid() )
		{
			_equipPlaybackDone = true;
			return;
		}

		scene.DirectPlayback.Cancel();
		ApplyBaselineGraphParameters();
		_equipPlaybackDone = true;
		AimboxViewModelMovementDebug.LogEquipReady(
			_model.ResourceName,
			_skin.UseAnimGraph,
			AimboxViewModelMovementDebug.ProbeGraph( _model ) );
	}

	async Task GrenadeEquipRoutineAsync( CancellationToken ct )
	{
		GrenadeReady = false;
		await Task.DelayRealtimeSeconds( EquipRoutineInitialDelaySeconds );
		if ( ct.IsCancellationRequested || !_skin.IsValid() )
		{
			_equipPlaybackDone = true;
			return;
		}

		var scene = await WaitForSceneModelAsync( ct, 2.5f );
		if ( !scene.IsValid() )
		{
			_equipPlaybackDone = true;
			return;
		}

		scene.UseAnimGraph = true;
		ApplyGrenadeGraphBaselines();

		if ( !string.IsNullOrWhiteSpace( DeploySequenceName ) )
			scene.DirectPlayback.Play( DeploySequenceName );

		await Task.DelayRealtimeSeconds( 0.03f );
		var wait = scene.DirectPlayback.Duration > 0.005f ? scene.DirectPlayback.Duration : DeployDurationFallbackSeconds;
		wait *= _presentationSpeedMultiplier;
		await Task.DelayRealtimeSeconds( wait );
		if ( ct.IsCancellationRequested || !_skin.IsValid() || !scene.IsValid() )
		{
			_equipPlaybackDone = true;
			return;
		}

		scene.DirectPlayback.Cancel();
		ApplyBaselineGraphParameters();
		ApplyGrenadeGraphBaselines();
		GrenadeReady = true;
		_equipPlaybackDone = true;
	}

	async Task GrenadeThrowRoutineAsync( CancellationToken ct ) =>
		await RunGrenadeThrowPresentationAsync( ct );

	async Task GrenadeQuickTossRoutineAsync( CancellationToken ct ) =>
		await RunGrenadeThrowPresentationAsync( ct );

	async Task RunGrenadeThrowPresentationAsync( CancellationToken ct )
	{
		try
		{
			EndGrenadeChargeGraphState();
			SetParamOnSkins( GraphParamAttack, true );
			TrySetGrenadeGraphParam( GraphParamThrow, true );

			var startTime = Time.Now;
			var releaseAt = GrenadeThrowReleaseDelaySeconds * _presentationSpeedMultiplier;
			var holdUntil = GrenadeThrowAnimHoldSeconds * _presentationSpeedMultiplier;
			var deadline = startTime + holdUntil + 0.15f;

			while ( Time.Now < deadline && !ct.IsCancellationRequested )
			{
				var elapsed = Time.Now - startTime;
				if ( !_grenadeThrowReleased && elapsed >= releaseAt )
					InvokeGrenadeThrowRelease();

				if ( elapsed >= holdUntil )
					break;

				await Task.DelayRealtimeSeconds( 0.016f );
			}

			if ( !_grenadeThrowReleased )
				InvokeGrenadeThrowRelease();
		}
		finally
		{
			SetParamOnSkins( GraphParamAttack, false );
			TrySetGrenadeGraphParam( GraphParamThrow, false );
			_grenadeThrowAnimRunning = false;
			var complete = _grenadeThrowCompleteCallback;
			_grenadeThrowCompleteCallback = null;
			complete?.Invoke();
		}
	}

	void EndGrenadeChargeGraphState()
	{
		SetParamOnSkins( GraphParamCharge, false );
		TrySetGrenadeGraphParam( GraphParamPull, false );
	}

	bool GraphHasParam( string name )
	{
		if ( !_model.IsValid() )
			return false;

		var graph = _model.AnimGraph;
		return graph is not null && !graph.IsError && graph.TryGetParameterIndex( name, out _ );
	}

	void TrySetGrenadeGraphParam( string name, bool value )
	{
		if ( GraphHasParam( name ) )
			SetParamOnSkins( name, value );
	}

	void ApplyGrenadeGraphBaselines()
	{
		SetParamOnSkins( GraphParamPinRemove, true );
		SetParamOnSkins( GraphParamChargeType, 1 );
		SetParamOnSkins( GraphParamThrowType, 0 );
		SetParamOnSkins( GraphParamThrowBlend, 0.35f );
	}

	void InvokeGrenadeThrowRelease()
	{
		if ( _grenadeThrowReleased )
			return;

		_grenadeThrowReleased = true;
		var release = _grenadeThrowReleaseCallback;
		_grenadeThrowReleaseCallback = null;
		release?.Invoke();
	}

	async Task ReloadPresentationAsync( float gameplayReloadSeconds, bool shotgunShellReload = false, bool firstShell = false )
	{
		if ( _reloadAnimRunning || TryGetSceneModel() is not { } scene )
			return;

		var seq = ReloadSequenceName;
		if ( shotgunShellReload && firstShell && !string.IsNullOrWhiteSpace( ReloadFirstShellSequenceName ) )
			seq = ReloadFirstShellSequenceName;

		var canUseGraphBulkReload = !shotgunShellReload && GraphHasBulkReloadParam();
		if ( string.IsNullOrWhiteSpace( seq ) && !canUseGraphBulkReload )
			return;

		_reloadAnimRunning = true;
		try
		{
			var ct = _lifecycle?.Token ?? default;
			scene.UseAnimGraph = true;
			if ( shotgunShellReload )
			{
				TrySetShotgunReloadingGraphState( true );
				await PulseShotgunShellGraphAsync( firstShell, ct );
			}

			var usedGraphBulkReload = false;
			if ( canUseGraphBulkReload && string.IsNullOrWhiteSpace( seq ) )
			{
				usedGraphBulkReload = true;
				PulseBulkReloadGraph();
			}
			else if ( !string.IsNullOrWhiteSpace( seq ) )
			{
				scene.DirectPlayback.Play( seq );
				await Task.DelayRealtimeSeconds( 0.03f );
				if ( ct.IsCancellationRequested || !_skin.IsValid() )
					return;

				if ( scene.DirectPlayback.Duration <= 0.005f && canUseGraphBulkReload )
				{
					scene.DirectPlayback.Cancel();
					usedGraphBulkReload = true;
					PulseBulkReloadGraph();
				}
			}

			var gameplayWait = gameplayReloadSeconds * _presentationSpeedMultiplier;
			float wait;
			if ( usedGraphBulkReload )
			{
				wait = gameplayWait;
			}
			else
			{
				var animWait = scene.DirectPlayback.Duration > 0.005f ? scene.DirectPlayback.Duration : ReloadDurationFallbackSeconds;
				animWait *= _presentationSpeedMultiplier;
				wait = MathF.Max( animWait, gameplayWait );
			}

			await Task.DelayRealtimeSeconds( wait );
		}
		finally
		{
			_reloadAnimRunning = false;
			if ( _skin.IsValid() && TryGetSceneModel() is { } sm )
			{
				sm.DirectPlayback.Cancel();
				TrySnapToAdsOrIdle();
			}
		}
	}

	bool GraphHasBulkReloadParam()
	{
		if ( !_model.IsValid() )
			return false;

		var graph = _model.AnimGraph;
		return graph is not null && !graph.IsError && graph.TryGetParameterIndex( GraphParamBulkReload, out _ );
	}

	void PulseBulkReloadGraph() => SetParamOnSkins( GraphParamBulkReload, true );

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
		if ( _shotgunGraphReloadWorkerBusy || !_skin.IsValid() )
			return;

		var ct = _lifecycle?.Token ?? default;
		_shotgunGraphReloadWorkerBusy = true;
		_reloadAnimRunning = true;

		try
		{
			while ( _shotgunGraphShellQueue.Count > 0 && !ct.IsCancellationRequested )
			{
				var ( secs, tubeEmpty ) = _shotgunGraphShellQueue.Dequeue();
				await ExecuteShotgunGraphShellCycleAsync( secs, tubeEmpty, ct );
			}
		}
		finally
		{
			_shotgunGraphReloadWorkerBusy = false;
			_reloadAnimRunning = false;
			TryReleaseShotgunReloadingGraphStanceAfterQueueDrainImmediate();

			if ( _skin.IsValid() && TryGetSceneModel() is { } sm )
			{
				sm.DirectPlayback.Cancel();
				TrySnapToAdsOrIdle();
			}

			if ( _shotgunGraphShellQueue.Count > 0 && !ct.IsCancellationRequested )
				_ = ProcessShotgunGraphShellQueueAsync();
		}
	}

	async Task ExecuteShotgunGraphShellCycleAsync( float gameplayReloadSeconds, bool tubeWasEmptyBeforeShell, CancellationToken ct )
	{
		if ( TryGetSceneModel() is not { } scene )
			return;

		scene.UseAnimGraph = true;
		TrySetShotgunReloadingGraphState( true );
		await PulseShotgunShellGraphAsync( tubeWasEmptyBeforeShell, ct );
		if ( ct.IsCancellationRequested || !_skin.IsValid() )
			return;

		var waitSeconds = MathF.Max(
			gameplayReloadSeconds,
			MathF.Max( ShellReloadGraphPulseHoldSeconds * 2f, 0.02f ) );
		await Task.DelayRealtimeSeconds( waitSeconds );
	}

	void TrySetShotgunReloadingGraphState( bool reloading )
	{
		if ( !_model.IsValid() )
			return;

		var graph = _model.AnimGraph;
		if ( graph is null || graph.IsError || !graph.TryGetParameterIndex( GraphParamReloading, out _ ) )
			return;

		SetParamOnSkins( GraphParamReloading, reloading );
	}

	async Task MeleePresentationAsync( string sequenceName )
	{
		if ( string.IsNullOrWhiteSpace( sequenceName ) || TryGetSceneModel() is not { } scene )
			return;

		_meleeAttackAnimRunning = true;
		try
		{
			scene.UseAnimGraph = true;
			scene.DirectPlayback.Play( sequenceName );
			await Task.DelayRealtimeSeconds( 0.03f );
			var wait = scene.DirectPlayback.Duration > 0.005f ? scene.DirectPlayback.Duration : MeleeAttackDurationFallbackSeconds;
			await Task.DelayRealtimeSeconds( wait );
		}
		finally
		{
			_meleeAttackAnimRunning = false;
			if ( TryGetSceneModel() is { } sm )
			{
				sm.DirectPlayback.Cancel();
				TrySnapToAdsOrIdle();
			}
		}
	}

	void OwnerTickMovementGraphParameters(
		bool crouching,
		Angles eyeAngles,
		Vector3 velocityWorld,
		bool grounded,
		float runSpeed,
		bool sprintHeld,
		bool aimDownSights,
		bool meleeWeapon )
	{
		if ( !_model.IsValid() )
			return;

		var graph = _model.AnimGraph;
		if ( graph is null || graph.IsError )
		{
			AimboxViewModelMovementDebug.LogTickBlocked( "anim graph missing or error on viewmodel" );
			return;
		}

		if ( graph.TryGetParameterIndex( GraphParamDuckLevel, out _ ) )
			SetParamOnSkins( GraphParamDuckLevel, crouching ? 1f : 0f );
		else if ( graph.TryGetParameterIndex( GraphParamBDuck, out _ ) )
			SetParamOnSkins( GraphParamBDuck, crouching );

		if ( graph.TryGetParameterIndex( GraphParamAimPitch, out _ ) )
			SetParamOnSkins( GraphParamAimPitch, eyeAngles.pitch );

		if ( graph.TryGetParameterIndex( GraphParamAimYaw, out _ ) )
			SetParamOnSkins( GraphParamAimYaw, eyeAngles.yaw );

		if ( graph.TryGetParameterIndex( GraphParamGrounded, out _ ) )
			SetParamOnSkins( GraphParamGrounded, grounded );

		var speed = velocityWorld.Length;
		var hasMoveBob = graph.TryGetParameterIndex( GraphParamMoveBob, out _ );
		var moveBob = hasMoveBob
			? speed.Remap( 0f, MathF.Max( 1f, runSpeed * 2f ), 0f, 1f )
			: 0f;
		if ( hasMoveBob )
			SetParamOnSkins( GraphParamMoveBob, moveBob );

		var moving = velocityWorld.LengthSquared > 400f;
		var sprintActive = false;
		if ( graph.TryGetParameterIndex( GraphParamSprint, out _ ) )
		{
			sprintActive = sprintHeld && moving && !aimDownSights && !_reloadAnimRunning;
			if ( meleeWeapon )
				sprintActive &= !_meleeAttackAnimRunning;

			SetParamOnSkins( GraphParamSprint, sprintActive );
		}

		AimboxViewModelMovementDebug.LogMovementTick(
			_model.ResourceName,
			_equipPlaybackDone,
			_skin.UseAnimGraph,
			AimboxViewModelMovementDebug.ProbeGraph( _model ),
			speed,
			sprintHeld,
			moving,
			moveBob,
			sprintActive,
			grounded,
			aimDownSights,
			_reloadAnimRunning );
	}

	void OwnerTickCombatGraphParameters( bool aimDownSights, bool primaryFireHeld, int firingModeGraphEnum )
	{
		if ( !_model.IsValid() )
			return;

		var graph = _model.AnimGraph;
		if ( graph is null || graph.IsError )
			return;

		if ( graph.TryGetParameterIndex( GraphParamFiringMode, out _ ) )
			SetParamOnSkins( GraphParamFiringMode, firingModeGraphEnum );

		var rampUp = AttackHoldRampUpPerSecond;
		if ( firingModeGraphEnum == 3 && primaryFireHeld )
			rampUp *= FullAutoAttackHoldRampMultiplier;

		_attackHold01 = primaryFireHeld
			? Math.Min( 1f, _attackHold01 + Time.Delta * rampUp )
			: Math.Max( 0f, _attackHold01 - Time.Delta * AttackHoldDecayPerSecond );

		if ( graph.TryGetParameterIndex( GraphParamAttackHold, out _ ) )
			SetParamOnSkins( GraphParamAttackHold, _attackHold01 );

		if ( graph.TryGetParameterIndex( GraphParamIronsightsFireScale, out _ ) )
			SetParamOnSkins( GraphParamIronsightsFireScale, aimDownSights ? 0.72f : 1f );

		if ( graph.TryGetParameterIndex( GraphParamCameraRotationScale, out _ ) )
			SetParamOnSkins( GraphParamCameraRotationScale, aimDownSights ? 0.58f : 0.68f );
	}

	void OwnerTickIronsightsGraphBlend( bool aimDownSights )
	{
		var target = aimDownSights ? 1f : 0f;
		var step = Time.Delta * Math.Max( 1f, IronsightsBlendPerSecond * AdsPresentationSpeedMultiplier );

		if ( UseGraphIronsightsParameterForAds && GraphHasIronsightsParam() && _skin.IsValid() && _model.IsValid() )
		{
			_ironsightsBlend01 = target > _ironsightsBlend01
				? Math.Min( target, _ironsightsBlend01 + step )
				: Math.Max( target, _ironsightsBlend01 - step );
			SetParamOnSkins( GraphParamIronsights, _ironsightsBlend01 );
			_appliedAdsPose = _ironsightsBlend01 > 0.5f;
			return;
		}

		// Sequence-based ADS (e.g. M700 scope pose) — still expose a blend for FOV / overlay timing.
		_ironsightsBlend01 = target > _ironsightsBlend01
			? Math.Min( target, _ironsightsBlend01 + step )
			: Math.Max( target, _ironsightsBlend01 - step );
		_appliedAdsPose = _ironsightsBlend01 > 0.5f;
	}

	void TrySwapAdsVersusIdleIfChanged()
	{
		if ( UseGraphIronsightsParameterForAds && GraphHasIronsightsParam() )
			return;

		if ( _desiredAdsPose == _appliedAdsPose || TryGetSceneModel() is not { } scene )
			return;

		var seq = _desiredAdsPose ? AdsSequenceName : IdleSequenceName;
		if ( string.IsNullOrWhiteSpace( seq ) )
			seq = IdleSequenceName;

		scene.UseAnimGraph = true;
		scene.DirectPlayback.Play( seq );
		_appliedAdsPose = _desiredAdsPose;
	}

	void TrySnapToAdsOrIdle()
	{
		if ( UseGraphIronsightsParameterForAds && GraphHasIronsightsParam() )
		{
			_ironsightsBlend01 = _desiredAdsPose ? 1f : 0f;
			SetParamOnSkins( GraphParamIronsights, _ironsightsBlend01 );
			_appliedAdsPose = _desiredAdsPose;
			return;
		}

		TrySwapAdsVersusIdleIfChanged();
	}

	bool GraphHasIronsightsParam()
	{
		if ( !_model.IsValid() )
			return false;

		var graph = _model.AnimGraph;
		return graph is not null && !graph.IsError && graph.TryGetParameterIndex( GraphParamIronsights, out _ );
	}

	async Task PulseShotgunShellGraphAsync( bool firstShell, CancellationToken ct )
	{
		if ( !_model.IsValid() )
			return;

		var graph = _model.AnimGraph;
		if ( graph is null || graph.IsError )
			return;

		if ( graph.TryGetParameterIndex( GraphParamReloading, out _ ) )
			SetParamOnSkins( GraphParamReloading, true );

		var trigger = firstShell && graph.TryGetParameterIndex( GraphParamReloadingFirstShell, out _ )
			? GraphParamReloadingFirstShell
			: graph.TryGetParameterIndex( GraphParamReloadingShell, out _ )
				? GraphParamReloadingShell
				: null;

		if ( trigger is null )
			return;

		SetParamOnSkins( trigger, true );
		await Task.DelayRealtimeSeconds( ShellReloadGraphPulseHoldSeconds );
		if ( !ct.IsCancellationRequested )
			SetParamOnSkins( trigger, false );
	}

	void ApplyBaselineGraphParameters()
	{
		_attackHold01 = 0f;
		_ironsightsBlend01 = 0f;
		SetParamOnSkins( GraphParamAttackHold, 0f );
		SetParamOnSkins( GraphParamIronsights, 0f );
		SetParamOnSkins( GraphParamIronsightsFireScale, 1f );
		SetParamOnSkins( GraphParamCameraRotationScale, 0.68f );
	}

	SceneModel TryGetSceneModel() => _skin.IsValid() && _skin.SceneObject is SceneModel scene && scene.IsValid() ? scene : default;

	async Task<SceneModel> WaitForSceneModelAsync( CancellationToken ct, float maxSeconds )
	{
		var deadline = Time.Now + Math.Max( 0.05f, maxSeconds );
		while ( Time.Now < deadline && !ct.IsCancellationRequested )
		{
			if ( TryGetSceneModel() is { } scene )
				return scene;

			await Task.DelayRealtimeSeconds( 0.033f );
		}

		return TryGetSceneModel();
	}

	void SetParamOnSkins( string name, bool value )
	{
		if ( _skin.IsValid() )
			_skin.Set( name, value );
		foreach ( var linked in _linkedSkins )
		{
			if ( linked.IsValid() )
				linked.Set( name, value );
		}
	}

	void SetParamOnSkins( string name, int value )
	{
		if ( _skin.IsValid() )
			_skin.Set( name, value );
		foreach ( var linked in _linkedSkins )
		{
			if ( linked.IsValid() )
				linked.Set( name, value );
		}
	}

	void SetParamOnSkins( string name, float value )
	{
		if ( _skin.IsValid() )
			_skin.Set( name, value );
		foreach ( var linked in _linkedSkins )
		{
			if ( linked.IsValid() )
				linked.Set( name, value );
		}
	}

	protected override void OnDestroy()
	{
		_lifecycle?.Cancel();
		_lifecycle?.Dispose();
		base.OnDestroy();
	}

	static bool ClientFxContext() => Game.IsPlaying && !Application.IsDedicatedServer && !Application.IsHeadless;
}
