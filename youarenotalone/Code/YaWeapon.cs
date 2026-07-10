using System;
using System.Collections.Generic;

namespace Sandbox;

/// <summary>
/// Weapon on the player network root. THORNS_EVERYTHING_DOCUMENT §3 (host hitscan, fire rate, ammo, durability per shot),
/// §13 server authority. Client sends intent only via RPCs.
/// </summary>
[Title( "YouAreNotAlone — Weapon" )]
[Category( "YouAreNotAlone" )]
[Icon( "swords" )]
[Order( 200 )]
public sealed class YaWeapon : Component
{
	public const string WorldVisualChildName = "WeaponWorld";

	/// <summary>s&amp;box <c>.sound</c> asset. Owner hears when host confirms a hitscan round.</summary>
	public const string M4FireSoundResource = "sounds/m4_shot.sound";

	public const string ShotgunFireSoundResource = "sounds/shotgun_shot.sound";

	/// <summary>FP deploy cue when equipping M4 or shotgun (local owner).</summary>
	public const string GunDeploySoundResource = "sounds/gun_deploy.sound";

	public const string M4ReloadSoundResource = "sounds/m4_reload.sound";

	public const string ShotgunReloadSoundResource = "sounds/shotgun_reload.sound";

	public const string KnifeStabLightSoundResource = "sounds/knife_stab_light.sound";

	public const string KnifeStabHeavySoundResource = "sounds/knife_stab_heavy.sound";

	/// <summary>Alone knife swing with no damageable hit — owner hears after host confirms the round (<see cref="RpcFireOutcome"/>).</summary>
	public const string MeleeMissSoundResource = "sounds/melee_miss.sound";

	/// <summary>Optional kill-confirm sting — assign a <c>.sound</c> resource to enable (presentation only).</summary>
	public const string KillConfirmSoundResource = "sounds/knife_stab_heavy.sound";

	public const string HeadshotConfirmSoundResource = "sounds/knife_stab_light.sound";

	/// <summary><see cref="WorldVisualChildName"/> is created with a dev box; that mesh is huge in source units.</summary>
	public static readonly Vector3 WorldMeshLocalScaleDevBox = new( 0.1f, 0.1f, 0.28f );

	/// <summary>Uniform scale for loaded third-person <c>w_*</c> meshes parented under Citizen <c>Body</c> (tune if weapons read small/large vs rig).</summary>
	public static readonly Vector3 WorldMeshLocalScaleWeapon = new( 2f, 2f, 2f );

	/// <summary>
	/// When <see cref="YaWeaponResourceLoad.LoadWeaponModelOrFallback"/> falls back to <c>models/dev/box.vmdl</c> — <b>do not</b> use
	/// <see cref="WorldMeshLocalScaleDevBox"/> (that is only for the initial networked stub); 0.1 scale makes the orange placeholder microscopic.
	/// </summary>
	public static readonly Vector3 WorldMeshLocalScaleWeaponLoadFailed = new( 2f, 2f, 2f );

	/// <summary>Default offset when parented under Citizen <c>Body</c> — copy/tune the same values on <see cref="YaWeaponWorldVisual"/> in the inspector for live edits.</summary>
	public static readonly Vector3 WorldMeshLocalPositionRelBody = new( 12f, -8f, 32.5f );

	/// <summary>Default fallback if <c>Body</c> is missing (Z-up from feet). Mirrored on <see cref="YaWeaponWorldVisual.TpWeaponManualLocalPositionIfNoBody"/>.</summary>
	public static readonly Vector3 WorldMeshLocalPositionIfNoBody = new( 0f, 0f, 40f );

	/// <summary>
	/// Citizen <see cref="SkinnedModelRenderer.TryGetBoneTransform"/> names to try for third-person carry (right-hand grip).
	/// Order matters — first match wins. Tune if your rig differs (see Facepunch citizen model source under <c>addons/citizen</c>).
	/// </summary>
	public static readonly string[] CitizenTpWeaponRightHandBoneCandidates =
	{
		"hand_R",
		"wrist_R",
		"Hold_R",
		"weapon_hand_R"
	};

	const float AimDotMin = 0.55f;

	[Property] public string WeaponDefinitionId { get; set; } = "dev_placeholder";

	[Property] public string ViewModelAsset { get; set; } = "models/dev/box.vmdl";

	[Property] public Vector3 ViewModelLocalPosition { get; set; } = new Vector3( 12f, 4f, -10f );

	[Property] public Vector3 ViewModelLocalScale { get; set; } = new Vector3( 0.018f, 0.022f, 0.08f );

	double _nextFireAllowedHostTime;
	double _nextMeleeHeavyAllowedHostTime;

	// Host-only: authoritative Valorant-style spray index + bloom ordinal (reset after RecoilResetDelaySeconds gap).
	double _hostRecoilLastShotTime;
	int _hostRecoilPatternIndex;
	int _hostRecoilSprayOrdinal;

	/// <summary>Owner-client mirror of combat def id from equip RPC (non-authoritative).</summary>
	string _ownerMirrorCombatWeaponDefinitionId = "";

	// --- Host reload transient (not persisted — THORNS doc: reload in progress is session logic) ---
	bool _hostReloadInProgress;
	/// <summary>Tube-style reload: one R press loads shells until full — keeps fire blocked even when <see cref="_hostReloadInProgress"/> is briefly false for FP reload edges.</summary>
	bool _hostShotgunPumpReloadSession;
	int _hostReloadHotbarSlot = -1;
	string _hostReloadWeaponInstanceId = "";

	const float PumpReloadHudPulseSeconds = 0.05f;

	// --- Owner HUD mirror (Rpc.Owner) ---
	int _clientLoadedAmmo;
	int _clientReserveAmmo;
	bool _clientWeaponBroken;
	bool _clientReloading;

	/// <summary>Mirrored from host shotgun pump RPC — latch <see cref="YaViewModelFpAnimator"/> <c>b_reloading</c>; independent from brief <see cref="_clientReloading"/> HUD pulses.</summary>
	bool _clientShotgunPumpHud;

	/// <summary>Prior frame <see cref="_clientReloading"/> — for FP reload anim rising edge.</summary>
	bool _fpVmLastReloadHud;

	/// <summary>Client-side throttle for auto fire RPCs (aligned with <see cref="YaWeaponDefinitions.WeaponDefinition.FireIntervalSeconds"/>).</summary>
	double _clientNextAutoFireIntentTime;

	const float AloneM2ChargeHoldSeconds = 1.1f;
	float _aloneM2Charge01;
	bool _wasMeleeAttack2Down;
	double _nextAutoReloadAttemptAt;

	/// <summary>Local owner: M2 charge 0–1 while holding RMB (Alone + melee). For HUD only.</summary>
	public float LocalAloneM2ChargeNormalized { get; private set; }

	/// <summary>Local-only UX gate — server always re-validates.</summary>
	public bool ClientMirrorMayFireIntent()
	{
		if ( !YaRoundGate.MayUseWeapons() )
			return false;
		if ( string.IsNullOrWhiteSpace( ClientMirrorCombatDefinitionId ) )
			return false;
		if ( _clientWeaponBroken )
			return false;
		if ( _clientReloading )
			return false;

		var cidMirror = ClientMirrorCombatDefinitionId;
		var defMirror = YaWeaponDefinitions.Get( cidMirror );
		if ( !YaWeaponDefinitions.TreatsAsMeleeWeapon( defMirror, cidMirror )
		     && YaWeaponDefinitions.UsesPerShellReloadCycle( defMirror, cidMirror )
		     && _clientShotgunPumpHud )
			return false;
		if ( !YaWeaponDefinitions.TreatsAsMeleeWeapon( defMirror, cidMirror ) )
		{
			if ( _clientLoadedAmmo <= 0 )
				return false;
		}

		if ( YaViewModelController.CombatDefinitionUsesStockFpAnimator( ClientMirrorCombatDefinitionId ) )
		{
			var fp = ResolveLocalFpAnimator();
			if ( !fp.IsValid() || !fp.PresentationAllowsCombatFire )
				return false;
		}

		return true;
	}

	// --- Debug UI / HUD (owner mirror only, non-authoritative) ---

	public int ClientMirrorLoadedAmmo => _clientLoadedAmmo;
	public int ClientMirrorReserveAmmo => _clientReserveAmmo;
	public bool ClientMirrorWeaponBroken => _clientWeaponBroken;
	public bool ClientMirrorReloading => _clientReloading;
	/// <summary>Best-known combat def for local UX (ADS, melee alt-fire, HUD). While Alone + mimic, uses owner hotbar RPC mirror only so FP stays the real weapon; observers may reflect mimic for third person.</summary>
	public string ClientMirrorCombatDefinitionId
	{
		get
		{
			var equip = Components.Get<YaHotbarEquipment>();
			var roleCmp = Components.Get<YaPlayerRoleComponent>( FindMode.EnabledInSelf );
			var aloneMech = Components.Get<YaAloneMechanics>( FindMode.EnabledInSelf );
			if ( YaPawn.IsLocalConnectionOwner( this )
			     && roleCmp is { IsValid: true, Role: YaPlayerRole.Alone }
			     && aloneMech is { IsValid: true, MimicPresentationActive: true }
			     && equip.IsValid()
			     && !string.IsNullOrWhiteSpace( equip.ClientMirrorEquippedCombatWeaponDefinitionId ) )
				return equip.ClientMirrorEquippedCombatWeaponDefinitionId;

			if ( !string.IsNullOrWhiteSpace( _ownerMirrorCombatWeaponDefinitionId ) )
				return _ownerMirrorCombatWeaponDefinitionId;

			if ( equip.IsValid() && !string.IsNullOrWhiteSpace( equip.ObserversCombatWeaponDefinitionId ) )
				return equip.ObserversCombatWeaponDefinitionId;

			return "";
		}
	}

	protected override void OnStart()
	{
		var ownerId = GameObject.Network.OwnerId;
		var local = Connection.Local;
		var isLocalPawn = YaPawn.IsLocalConnectionOwner( this );
		Log.Info( $"[YA] Weapon ownership check: root='{GameObject.Name}', ownerId={ownerId}, localConn={(local is null ? "null" : local.Id.ToString())}, isLocalPawn={isLocalPawn}, def={WeaponDefinitionId}" );

		// First-person viewmodel is driven only after server-authoritative equip.
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying )
			return;

		if ( !YaPawn.IsLocalConnectionOwner( this ) )
			return;

		var hp = Components.Get<YaPlayerHealth>();
		if ( hp.IsValid() && hp.IsDeadState )
			return;

		var roleEarly = Components.Get<YaPlayerRoleComponent>( FindMode.EnabledInSelf );
		var roleEarlyVal = roleEarly.IsValid() ? roleEarly.Role : YaPlayerRole.Unassigned;
		var gsEarly = YaGameStateSystem.Instance;
		if ( gsEarly is { IsValid: true, CurrentState: YaGameState.InRound } && roleEarlyVal == YaPlayerRole.Unassigned )
			return;

		TryRefreshFirstPersonPresentationForAloneMimic();

		TickFpViewmodelSequences();

		if ( (Input.Pressed( "reload" ) || Input.Pressed( "Reload" )) && Connection.Local is not null )
		{
			Log.Info( "[YA] Reload request (client intent)" );
			RequestReload();
		}

		var attack1PressedForConsumable =
			Input.Pressed( "Attack1" ) || Input.Pressed( "attack1" );
		if ( attack1PressedForConsumable && TryRequestUseEquippedConsumableFromPrimaryAttack() )
			return;

		var cidUx = ClientMirrorCombatDefinitionId;
		var combatDef = YaWeaponDefinitions.Get( cidUx );
		var roleCmp = Components.Get<YaPlayerRoleComponent>( FindMode.EnabledInSelf );
		var role = roleCmp.IsValid() ? roleCmp.Role : YaPlayerRole.Unassigned;

		if ( YaWeaponDefinitions.TreatsAsMeleeWeapon( combatDef, cidUx ) )
		{
			if ( role == YaPlayerRole.Alone )
			{
				var a2Down = Input.Down( "Attack2" ) || Input.Down( "attack2" );
				if ( a2Down )
					_aloneM2Charge01 = Math.Min( 1f, _aloneM2Charge01 + (float)Time.Delta / AloneM2ChargeHoldSeconds );
				else
				{
					if ( _wasMeleeAttack2Down && _aloneM2Charge01 >= 0.92f )
						TryLocalMeleeHeavyIntent();
					_aloneM2Charge01 = 0f;
				}

				_wasMeleeAttack2Down = a2Down;
				LocalAloneM2ChargeNormalized = _aloneM2Charge01;

				if ( !(Input.Pressed( "Attack1" ) || Input.Pressed( "attack1" )) )
					return;

				TryLocalFireIntent();
				return;
			}

			LocalAloneM2ChargeNormalized = 0f;
			_wasMeleeAttack2Down = false;
			_aloneM2Charge01 = 0f;

			if ( YaWeaponDefinitions.HasSecondaryMeleeResolved( combatDef, cidUx )
			     && (Input.Pressed( "Attack2" ) || Input.Pressed( "attack2" )) )
			{
				TryLocalMeleeHeavyIntent();
				return;
			}

			if ( !(Input.Pressed( "Attack1" ) || Input.Pressed( "attack1" )) )
				return;

			TryLocalFireIntent();
			return;
		}

		LocalAloneM2ChargeNormalized = 0f;

		var autoFire = string.Equals( combatDef.FireMode, "auto", StringComparison.OrdinalIgnoreCase );
		var attackDown = Input.Down( "Attack1" ) || Input.Down( "attack1" );
		var attackPressed = Input.Pressed( "Attack1" ) || Input.Pressed( "attack1" );

		if ( autoFire )
		{
			if ( !attackDown )
			{
				TryAutoReloadIfEmpty();
				return;
			}

			if ( Time.Now < _clientNextAutoFireIntentTime )
			{
				TryAutoReloadIfEmpty();
				return;
			}

			_clientNextAutoFireIntentTime = Time.Now + combatDef.FireIntervalSeconds * 0.92;
		}
		else if ( !attackPressed )
		{
			return;
		}

		TryLocalFireIntent();
		TryAutoReloadIfEmpty();
	}

	void TryAutoReloadIfEmpty()
	{
		if ( _clientReloading || _clientShotgunPumpHud )
			return;

		if ( !YaRoundGate.MayUseWeapons() )
			return;

		if ( ClientMirrorLoadedAmmo > 0 || ClientMirrorReserveAmmo <= 0 )
			return;

		var role = Components.Get<YaPlayerRoleComponent>( FindMode.EnabledInSelf );
		if ( !role.IsValid() || role.Role != YaPlayerRole.NotAlone )
			return;

		var cid = ClientMirrorCombatDefinitionId ?? "";
		var def = YaWeaponDefinitions.Get( cid );
		if ( YaWeaponDefinitions.TreatsAsMeleeWeapon( def, cid ) )
			return;

		if ( Time.Now < _nextAutoReloadAttemptAt )
			return;

		_nextAutoReloadAttemptAt = Time.Now + 0.4;
		RequestReload();
	}

	/// <summary>Debug HUD only — forwards reload intent RPC (same path as input).</summary>
	public void DebugUiSendReloadIntent()
	{
		if ( !YaPawn.IsLocalConnectionOwner( this ) )
			return;

		Log.Info( "[YA] UI: reload intent forwarded to RequestReload" );
		RequestReload();
	}

	/// <summary>
	/// Selected hotbar is a usable consumable — mirror <see cref="ThornsConsumableUseInput"/> gates; primary attack consumes like the dedicated use binding.
	/// </summary>
	bool TryRequestUseEquippedConsumableFromPrimaryAttack() => false;

	void TryLocalFireIntent()
	{
		var hp = Components.Get<YaPlayerHealth>();
		if ( hp.IsValid() && ( !hp.IsAlive || hp.IsDeadState ) )
			return;

		if ( !ClientMirrorMayFireIntent() )
			return;

		if ( !YaCombatAuthority.TryGetAuthoritativeEye( GameObject, out var eyePos, out var eyeRot ) )
			return;

		var dir = eyeRot.Forward.Normal;

		Log.Info( $"[YA] Local fire intent (client presentation only): dir={dir}" );

		PlayLocalFireFeedback();

		var defForAds = YaWeaponDefinitions.Get( ClientMirrorCombatDefinitionId );
		var adsHeld = !YaWeaponDefinitions.IsMeleeWeapon( defForAds )
			&& (Input.Down( "Attack2" ) || Input.Down( "attack2" ));
		RequestFire( dir, attackVariant: 0, aimDownSights: adsHeld );
	}

	void TryLocalMeleeHeavyIntent()
	{
		var hp = Components.Get<YaPlayerHealth>();
		if ( hp.IsValid() && ( !hp.IsAlive || hp.IsDeadState ) )
			return;

		if ( !ClientMirrorMayFireIntent() )
			return;

		if ( !YaCombatAuthority.TryGetAuthoritativeEye( GameObject, out _, out var eyeRot ) )
			return;

		var dir = eyeRot.Forward.Normal;

		Log.Info( "[YA] Local melee heavy intent (Attack2)" );
		RequestFire( dir, attackVariant: 1, aimDownSights: false );
	}

	void PlayOwnerConfirmedGunshotSoundIfApplicable( int fpAttackPresentationKind, bool damageAppliedToTarget )
	{
		if ( fpAttackPresentationKind is 1 or 2 )
		{
			var cidMelee = ClientMirrorCombatDefinitionId ?? "";
			if ( !string.Equals( cidMelee, "m9_bayonet", StringComparison.OrdinalIgnoreCase ) )
				return;

			if ( damageAppliedToTarget )
			{
				var pathMelee = fpAttackPresentationKind == 2 ? KnifeStabHeavySoundResource : KnifeStabLightSoundResource;
				PlayOwnerWeaponSoundAtEar( pathMelee );
				return;
			}

			var roleCmp = Components.Get<YaPlayerRoleComponent>( FindMode.EnabledInSelf );
			if ( roleCmp is { IsValid: true, Role: YaPlayerRole.Alone } )
				PlayOwnerWeaponSoundAtEar( MeleeMissSoundResource );

			return;
		}

		if ( fpAttackPresentationKind != 0 )
			return;
		// Gunshots also play as owner-local first-person confirmation.
		// Host still broadcasts positional world audio so everyone (including the attacker) hears distance-based shots.
		var cid = ClientMirrorCombatDefinitionId ?? "";
		if ( string.Equals( cid, "m4", StringComparison.OrdinalIgnoreCase ) )
			PlayOwnerWeaponSoundAtEar( M4FireSoundResource );
		else if ( string.Equals( cid, "shotgun", StringComparison.OrdinalIgnoreCase ) )
			PlayOwnerWeaponSoundAtEar( ShotgunFireSoundResource );
	}

	void PlayOwnerWeaponSoundAtEar( string resourcePath )
	{
		if ( string.IsNullOrWhiteSpace( resourcePath ) )
			return;
		var path = resourcePath.Trim();
		var paranoiaMul = YaHunterParanoia.LocalOwnedPawnSoundVolumeScale( GameObject );
		if ( YaCombatAuthority.TryGetAuthoritativeEye( GameObject, out var ear, out _ ) )
		{
			var h = Sound.Play( path, ear );
			if ( h is { IsValid: true } snd && paranoiaMul < 0.999f )
				snd.Volume *= paranoiaMul;
		}
		else
		{
			var h = Sound.Play( path, GameObject.WorldPosition );
			if ( h is { IsValid: true } snd && paranoiaMul < 0.999f )
				snd.Volume *= paranoiaMul;
		}
	}

	/// <summary>Host → everyone: world-space weapon cue (used for shared fire/reload audio).</summary>
	void SendWorldWeaponSound( string resourcePath, float volume = 1f )
	{
		if ( string.IsNullOrWhiteSpace( resourcePath ) || !Networking.IsHost )
			return;

		var pos = YaCombatAuthority.TryGetAuthoritativeEye( GameObject, out var ear, out _ )
			? ear
			: GameObject.WorldPosition + Vector3.Up * 40f;
		RpcPlayWorldWeaponSound( resourcePath.Trim(), pos, Math.Clamp( volume, 0f, 2f ) );
	}

	[Rpc.Broadcast]
	void RpcPlayWorldWeaponSound( string resourcePath, Vector3 worldPos, float volume )
	{
		if ( string.IsNullOrWhiteSpace( resourcePath ) )
			return;
		var h = Sound.Play( resourcePath.Trim(), worldPos );
		if ( h is { IsValid: true } snd )
			snd.Volume *= Math.Clamp( volume, 0f, 2f );
	}

	void PlayLocalFireFeedback()
	{
		// Optional prediction FX — gun shot audio plays only after RpcFireOutcome (ammo spent) so dry-fire matches reality.
	}

	[Rpc.Host]
	void RequestReload()
	{
		Log.Info( $"[YA] Reload RPC received caller={Rpc.Caller?.Id}" );

		if ( !Networking.IsHost )
			return;

		if ( !ValidateRpcCallerOwnsPawn() )
		{
			Log.Warning( "[YA] Reload rejected: caller does not own pawn" );
			return;
		}

		var hpReload = Components.Get<YaPlayerHealth>();
		if ( hpReload.IsValid() && ( hpReload.IsDeadState || !hpReload.IsAlive ) )
		{
			Log.Warning( "[YA] Reload rejected: dead" );
			ClientNotifyReloadFailed( "dead" );
			return;
		}

		if ( !YaRoundGate.MayUseWeapons() )
		{
			ClientNotifyReloadFailed( "round inactive" );
			return;
		}

		var inv = Components.Get<YaGameInventory>();
		var equip = Components.Get<YaHotbarEquipment>();
		if ( !inv.IsValid() || !equip.IsValid() )
		{
			Log.Warning( "[YA] Reload rejected: missing inventory or equipment" );
			return;
		}

		var hotbar = equip.ServerGetSelectedHotbarIndex();
		if ( hotbar < 0 || !inv.TryGetHostSlot( hotbar, out var slot ) || slot.IsEmpty )
		{
			Log.Warning( "[YA] Reload rejected: no weapon equipped / empty hotbar slot" );
			ClientNotifyReloadFailed( "no_weapon" );
			return;
		}

		if ( !TryResolveWeaponItemDefResilient( slot.ItemId, out var itemDef ) )
		{
			Log.Warning( "[YA] Reload rejected: active slot is not a weapon" );
			ClientNotifyReloadFailed( "not_weapon" );
			return;
		}

		var combatKey = string.IsNullOrEmpty( itemDef.CombatWeaponDefinitionId ) ? slot.ItemId : itemDef.CombatWeaponDefinitionId;
		combatKey = combatKey?.Trim() ?? "";
		var wdef = YaWeaponDefinitions.Get( combatKey );

		if ( YaWeaponDefinitions.IsMeleeWeapon( wdef ) || YaWeaponDefinitions.IsKnownMeleeCombatId( combatKey ) )
		{
			Log.Info( "[YA] Reload ignored: melee weapon" );
			return;
		}

		if ( IsWeaponBrokenInSlot( slot ) )
		{
			Log.Warning( "[YA] Reload rejected: weapon broken" );
			ClientNotifyReloadFailed( "broken" );
			return;
		}

		if ( _hostReloadInProgress || _hostShotgunPumpReloadSession )
		{
			Log.Warning( "[YA] Reload rejected: already reloading" );
			ClientNotifyReloadFailed( "already_reloading" );
			return;
		}

		if ( slot.WeaponLoadedAmmo >= wdef.ClipSize )
		{
			Log.Warning( "[YA] Reload rejected: clip already full" );
			ClientNotifyReloadFailed( "clip_full" );
			return;
		}

		var reserve = inv.ServerCountAmmoMatchingType( wdef.AmmoTypeId );
		if ( reserve <= 0 )
		{
			Log.Warning( $"[YA] Reload rejected: no ammo matching type '{wdef.AmmoTypeId}'" );
			ClientNotifyReloadFailed( "no_ammo" );
			return;
		}

		_hostReloadInProgress = true;
		_hostReloadHotbarSlot = hotbar;
		_hostReloadWeaponInstanceId = slot.WeaponInstanceId ?? "";

		var perShell = YaWeaponDefinitions.UsesPerShellReloadCycle( wdef, combatKey );
		if ( perShell )
			_hostShotgunPumpReloadSession = true;

		Log.Info(
			perShell
				? $"[YA] Shell reload started: slot={hotbar} tube={slot.WeaponLoadedAmmo}/{wdef.ClipSize} reserve={reserve} gate={YaWeaponDefinitions.ShellReloadGameplayGateSeconds( wdef, combatKey ):F2}s per shell ×{Math.Max( 1, wdef.ReloadShellCountPerRpc )}"
				: $"[YA] Reload started: slot={hotbar} clip={slot.WeaponLoadedAmmo}/{wdef.ClipSize} reserve={reserve} time={wdef.ReloadTimeSeconds:F2}s" );

		PushWeaponHudToOwnerHost();

		_ = HostReloadAsync( hotbar, _hostReloadWeaponInstanceId, combatKey );
	}

	async Task HostReloadAsync( int hotbarSlot, string weaponInstanceAtStart, string combatKey )
	{
		var wdefInitial = YaWeaponDefinitions.Get( combatKey );
		var usesPerShellReload = YaWeaponDefinitions.UsesPerShellReloadCycle( wdefInitial, combatKey );

		try
		{
			if ( usesPerShellReload )
			{
				var firstShell = true;

				while ( Networking.IsHost && GameObject.IsValid() && _hostShotgunPumpReloadSession )
				{
					var wdefLoop = YaWeaponDefinitions.Get( combatKey );
					if ( !YaWeaponDefinitions.UsesPerShellReloadCycle( wdefLoop, combatKey ) )
					{
						Log.Info( "[YA] Shell reload session ended: weapon no longer uses per-shell cycle" );
						break;
					}

					if ( !firstShell )
					{
						_hostReloadInProgress = false;
						PushWeaponHudToOwnerHost();
						await Task.DelayRealtimeSeconds( PumpReloadHudPulseSeconds );
						if ( !Networking.IsHost || !GameObject.IsValid() || !_hostShotgunPumpReloadSession )
							break;
						_hostReloadInProgress = true;
						PushWeaponHudToOwnerHost();
					}

					var gate = YaWeaponDefinitions.ShellReloadGameplayGateSeconds( wdefLoop, combatKey );
					await Task.DelayRealtimeSeconds( gate );

					if ( !Networking.IsHost || !GameObject.IsValid() || !_hostShotgunPumpReloadSession )
						break;

					var inv = Components.Get<YaGameInventory>();
					var equip = Components.Get<YaHotbarEquipment>();
					if ( !inv.IsValid() || !equip.IsValid() )
					{
						Log.Warning( "[YA] Shell reload aborted: inventory/equip missing" );
						break;
					}

					if ( equip.ServerGetSelectedHotbarIndex() != hotbarSlot )
					{
						Log.Info( "[YA] Shell reload cancelled: hotbar selection changed" );
						break;
					}

					if ( !inv.TryGetHostSlot( hotbarSlot, out var slot ) || slot.IsEmpty )
					{
						Log.Warning( "[YA] Shell reload aborted: slot empty" );
						break;
					}

					if ( (slot.WeaponInstanceId ?? "") != weaponInstanceAtStart )
					{
						Log.Info( "[YA] Shell reload cancelled: weapon instance changed" );
						break;
					}

					if ( IsWeaponBrokenInSlot( slot ) )
					{
						Log.Warning( "[YA] Shell reload aborted: weapon broken" );
						break;
					}

					var space = wdefLoop.ClipSize - slot.WeaponLoadedAmmo;
					if ( space <= 0 )
					{
						Log.Info( $"[YA] Shell reload complete: tube full {slot.WeaponLoadedAmmo}/{wdefLoop.ClipSize}" );
						break;
					}

					var reserve = inv.ServerCountAmmoMatchingType( wdefLoop.AmmoTypeId );
					var toLoad = Math.Min( Math.Max( 1, wdefLoop.ReloadShellCountPerRpc ), Math.Min( space, reserve ) );
					if ( toLoad <= 0 )
					{
						Log.Info( "[YA] Shell reload complete: reserve empty" );
						break;
					}

					var removed = inv.ServerRemoveAmmoMatchingType( wdefLoop.AmmoTypeId, toLoad );
					if ( removed <= 0 )
					{
						Log.Warning( "[YA] Shell reload aborted: could not remove ammo" );
						break;
					}

					if ( !inv.TryGetHostSlot( hotbarSlot, out slot ) )
						break;

					slot.WeaponLoadedAmmo += removed;
					if ( slot.WeaponLoadedAmmo > wdefLoop.ClipSize )
						slot.WeaponLoadedAmmo = wdefLoop.ClipSize;

					inv.ServerWriteSlot( hotbarSlot, slot );

					Log.Info( $"[YA] Shell reload tick: slot={hotbarSlot} tube={slot.WeaponLoadedAmmo}/{wdefLoop.ClipSize} shellsThisStep={removed}" );

					if ( string.Equals( combatKey, "shotgun", StringComparison.OrdinalIgnoreCase ) )
						SendWorldWeaponSound( ShotgunReloadSoundResource, 0.9f );

					PushWeaponHudToOwnerHost();

					var stillFullOrDry = slot.WeaponLoadedAmmo >= wdefLoop.ClipSize
						|| inv.ServerCountAmmoMatchingType( wdefLoop.AmmoTypeId ) <= 0;
					if ( stillFullOrDry )
						break;

					firstShell = false;
				}
			}
			else
			{
				if ( string.Equals( combatKey, "m4", StringComparison.OrdinalIgnoreCase ) )
					SendWorldWeaponSound( M4ReloadSoundResource, 0.92f );

				await Task.DelayRealtimeSeconds( Math.Max( 0.01f, wdefInitial.ReloadTimeSeconds ) );

				if ( !Networking.IsHost || !GameObject.IsValid() )
					return;

				var inv = Components.Get<YaGameInventory>();
				var equip = Components.Get<YaHotbarEquipment>();
				if ( !inv.IsValid() || !equip.IsValid() )
				{
					Log.Warning( "[YA] Reload aborted: inventory/equip missing after delay" );
					return;
				}

				if ( equip.ServerGetSelectedHotbarIndex() != hotbarSlot )
				{
					Log.Info( "[YA] Reload cancelled: hotbar selection changed during reload" );
					return;
				}

				if ( !inv.TryGetHostSlot( hotbarSlot, out var slot ) || slot.IsEmpty )
				{
					Log.Warning( "[YA] Reload aborted: slot empty after delay" );
					return;
				}

				var inst = slot.WeaponInstanceId ?? "";
				if ( inst != weaponInstanceAtStart )
				{
					Log.Info( "[YA] Reload cancelled: weapon instance changed during reload" );
					return;
				}

				var wdef = YaWeaponDefinitions.Get( combatKey );

				if ( IsWeaponBrokenInSlot( slot ) )
				{
					Log.Warning( "[YA] Reload aborted: weapon broke during reload" );
					return;
				}

				var space = wdef.ClipSize - slot.WeaponLoadedAmmo;
				if ( space <= 0 )
				{
					Log.Info( "[YA] Reload complete (noop): clip full" );
					return;
				}

				var reserve = inv.ServerCountAmmoMatchingType( wdef.AmmoTypeId );
				var toLoad = Math.Min( space, reserve );
				if ( toLoad <= 0 )
				{
					Log.Warning( "[YA] Reload aborted: reserve empty after delay" );
					return;
				}

				var removed = inv.ServerRemoveAmmoMatchingType( wdef.AmmoTypeId, toLoad );
				if ( removed <= 0 )
				{
					Log.Warning( "[YA] Reload aborted: could not remove ammo" );
					return;
				}

				if ( !inv.TryGetHostSlot( hotbarSlot, out slot ) )
					return;

				slot.WeaponLoadedAmmo += removed;
				if ( slot.WeaponLoadedAmmo > wdef.ClipSize )
					slot.WeaponLoadedAmmo = wdef.ClipSize;

				inv.ServerWriteSlot( hotbarSlot, slot );

				Log.Info( $"[YA] Reload completed: slot={hotbarSlot} loaded={slot.WeaponLoadedAmmo}/{wdef.ClipSize} movedFromReserve={removed}" );
			}
		}
		finally
		{
			_hostReloadInProgress = false;
			_hostShotgunPumpReloadSession = false;
			_hostReloadHotbarSlot = -1;
			_hostReloadWeaponInstanceId = "";
			PushWeaponHudToOwnerHost();
		}
	}

	[Rpc.Owner]
	void ClientNotifyReloadFailed( string reason )
	{
		Log.Warning( $"[YA] Reload failed (owner notify): {reason}" );
	}

	/// <summary>Host: refresh owner HUD mirror (loaded / reserve / broken / reloading). Other players never receive this.</summary>
	public void HostPushWeaponHudFromInventory()
	{
		if ( !Networking.IsHost )
			return;

		PushWeaponHudToOwnerHost();
	}

	void PushWeaponHudToOwnerHost()
	{
		if ( !Networking.IsHost )
			return;

		var inv = Components.Get<YaGameInventory>();
		var equip = Components.Get<YaHotbarEquipment>();
		if ( !inv.IsValid() || !equip.IsValid() )
		{
			ClientReceiveWeaponHudState( 0, 0, 0, 0, 0 );
			return;
		}

		var idx = equip.ServerGetSelectedHotbarIndex();
		if ( idx < 0 || idx >= YaGameInventory.HotbarSlotCount || !inv.TryGetHostSlot( idx, out var slot ) || slot.IsEmpty )
		{
			ClientReceiveWeaponHudState( 0, 0, 0, 0, 0 );
			return;
		}

		if ( !TryResolveWeaponItemDefResilient( slot.ItemId, out var idef ) )
		{
			ClientReceiveWeaponHudState( 0, 0, 0, 0, 0 );
			return;
		}

		var combatId = string.IsNullOrEmpty( idef.CombatWeaponDefinitionId ) ? slot.ItemId : idef.CombatWeaponDefinitionId;
		combatId = combatId?.Trim() ?? "";
		var wdef = YaWeaponDefinitions.Get( combatId );
		var reserve = inv.ServerCountAmmoMatchingType( wdef.AmmoTypeId );
		var broken = IsWeaponBrokenInSlot( slot );
		var reloading = _hostReloadInProgress;
		var pump = _hostShotgunPumpReloadSession ? 1 : 0;

		if ( YaWeaponDefinitions.IsMeleeWeapon( wdef ) || YaWeaponDefinitions.IsKnownMeleeCombatId( combatId ) )
		{
			ClientReceiveWeaponHudState( -1, -1, broken ? 1 : 0, reloading ? 1 : 0, pump );
			return;
		}

		ClientReceiveWeaponHudState( slot.WeaponLoadedAmmo, reserve, broken ? 1 : 0, reloading ? 1 : 0, pump );
	}

	[Rpc.Owner]
	void ClientReceiveWeaponHudState( int loadedAmmo, int reserveAmmo, int weaponBrokenInt, int reloadingInt, int shotgunPumpReloadSessionInt )
	{
		_clientLoadedAmmo = loadedAmmo;
		_clientReserveAmmo = reserveAmmo;
		_clientWeaponBroken = weaponBrokenInt != 0;
		_clientReloading = reloadingInt != 0;
		_clientShotgunPumpHud = shotgunPumpReloadSessionInt != 0;

		Log.Info( $"[YA] Owner weapon HUD mirror: loaded={loadedAmmo} reserve={reserveAmmo} broken={_clientWeaponBroken} reloading={_clientReloading} pump={_clientShotgunPumpHud}" );
	}

	/// <summary>Host: reset fire-rate gate when switching equipped weapon.</summary>
	public void HostResetCooldownAfterWeaponEquip()
	{
		if ( !Networking.IsHost )
			return;

		_nextFireAllowedHostTime = 0;
		_nextMeleeHeavyAllowedHostTime = 0;
		_hostRecoilLastShotTime = 0;
		_hostRecoilPatternIndex = 0;
		_hostRecoilSprayOrdinal = 0;
		_hostReloadInProgress = false;
		_hostShotgunPumpReloadSession = false;
		_hostReloadHotbarSlot = -1;
		_hostReloadWeaponInstanceId = "";
	}

	/// <summary>Host: non-weapon hotbar selection — cancel reload and clear weapon HUD numbers.</summary>
	public void HostOnSelectedNonWeapon()
	{
		if ( !Networking.IsHost )
			return;

		_hostReloadInProgress = false;
		_hostShotgunPumpReloadSession = false;
		_hostReloadHotbarSlot = -1;
		_hostReloadWeaponInstanceId = "";
		ClientReceiveWeaponHudState( 0, 0, 0, 0, 0 );
	}

	[Rpc.Host]
	void RequestFire( Vector3 directionWorld, int attackVariant, bool aimDownSights )
	{
		var isHeavyMeleeAttack = attackVariant == 1;
		Log.Info( $"[YA] Fire request received on host from caller={Rpc.Caller?.Id}, pawn='{GameObject.Name}', variant={attackVariant}" );

		if ( !Networking.IsHost )
			return;

		var equip = Components.Get<YaHotbarEquipment>();

		if ( attackVariant != 0 && attackVariant != 1 )
		{
			Log.Warning( "[YA] Fire rejected: unknown attackVariant" );
			SendRpcFireOutcome( false, false, 0f, false, 0, 0f, 0f, false, null, YaWeaponImpactSurfaceKind.None, false );
			return;
		}

		if ( !YaPawn.ValidateHostRpcCallerOwnsPawnRoot( GameObject ) )
		{
			Log.Warning( $"[YA] Fire rejected: owner mismatch owner={GameObject.Network.OwnerId} caller={Rpc.Caller?.Id} local={Connection.Local?.Id}" );
			SendRpcFireOutcome( false, false, 0f, false, 0, 0f, 0f, false, null, YaWeaponImpactSurfaceKind.None, false );
			return;
		}

		var health = Components.Get<YaPlayerHealth>();
		if ( !health.IsValid() || !health.IsAlive || health.IsDeadState )
		{
			Log.Warning( $"[YA] Fire rejected: no health, dead, or death state '{GameObject.Name}'" );
			SendRpcFireOutcome( false, false, 0f, false, 0, 0f, 0f, false, null, YaWeaponImpactSurfaceKind.None, false );
			return;
		}

		if ( !YaRoundGate.MayUseWeapons() )
		{
			SendRpcFireOutcome( false, false, 0f, false, 0, 0f, 0f, false, null, YaWeaponImpactSurfaceKind.None, false );
			return;
		}

		var dir = directionWorld.Normal;
		if ( dir.Length < 0.95f )
		{
			Log.Warning( "[YA] Fire rejected: direction not normalized" );
			SendRpcFireOutcome( false, false, 0f, false, 0, 0f, 0f, false, null, YaWeaponImpactSurfaceKind.None, false );
			return;
		}

		if ( !YaCombatAuthority.TryGetAuthoritativeEye( GameObject, out var eyePos, out var eyeRot ) )
		{
			Log.Warning( "[YA] Fire rejected: could not resolve eye transform" );
			SendRpcFireOutcome( false, false, 0f, false, 0, 0f, 0f, false, null, YaWeaponImpactSurfaceKind.None, false );
			return;
		}

		if ( !YaCombatAuthority.IsDirectionWithinAimTolerance( dir, eyeRot, AimDotMin ) )
		{
			Log.Warning( $"[YA] Fire rejected: aim outside tolerance (dot vs camera forward)" );
			SendRpcFireOutcome( false, false, 0f, false, 0, 0f, 0f, false, null, YaWeaponImpactSurfaceKind.None, false );
			return;
		}

		if ( !IsOriginPlausible( eyePos ) )
		{
			Log.Warning( "[YA] Fire rejected: origin sanity" );
			SendRpcFireOutcome( false, false, 0f, false, 0, 0f, 0f, false, null, YaWeaponImpactSurfaceKind.None, false );
			return;
		}

		var inv = Components.Get<YaGameInventory>();
		if ( !inv.IsValid() || !equip.IsValid() )
		{
			Log.Warning( "[YA] Fire rejected: missing inventory or equipment" );
			SendRpcFireOutcome( false, false, 0f, false, 0, 0f, 0f, false, null, YaWeaponImpactSurfaceKind.None, false );
			return;
		}

		var hotbar = equip.ServerGetSelectedHotbarIndex();
		if ( hotbar < 0 || !inv.TryGetHostSlot( hotbar, out var slot ) || slot.IsEmpty )
		{
			Log.Warning( "[YA] Fire rejected: no hotbar row for equipped weapon" );
			SendRpcFireOutcome( false, false, 0f, false, 0, 0f, 0f, false, null, YaWeaponImpactSurfaceKind.None, false );
			return;
		}

		if ( !TryResolveWeaponItemDefResilient( slot.ItemId, out var itemDef ) )
		{
			Log.Warning( "[YA] Fire rejected: equipped slot is not a weapon item" );
			SendRpcFireOutcome( false, false, 0f, false, 0, 0f, 0f, false, null, YaWeaponImpactSurfaceKind.None, false );
			return;
		}

		var authoritativeCombatId = string.IsNullOrEmpty( itemDef.CombatWeaponDefinitionId )
			? slot.ItemId
			: itemDef.CombatWeaponDefinitionId;
		authoritativeCombatId = authoritativeCombatId?.Trim() ?? "";
		if ( string.IsNullOrEmpty( authoritativeCombatId ) )
		{
			Log.Warning( "[YA] Fire rejected: authoritative combat id missing for active hotbar row" );
			SendRpcFireOutcome( false, false, 0f, false, 0, 0f, 0f, false, null, YaWeaponImpactSurfaceKind.None, false );
			return;
		}

		var equipMirroredCombatId = equip.ServerGetActiveCombatWeaponDefinitionId() ?? "";
		if ( !string.IsNullOrEmpty( equipMirroredCombatId )
		     && !string.Equals( authoritativeCombatId, equipMirroredCombatId, StringComparison.OrdinalIgnoreCase ) )
			Log.Warning(
				$"[YA] Active weapon mirror desync: hotbar row combat='{authoritativeCombatId}' equip cache='{equipMirroredCombatId}' — resolving from row" );

		var def = YaWeaponDefinitions.Get( authoritativeCombatId );

		if ( isHeavyMeleeAttack )
		{
			if ( !YaWeaponDefinitions.TreatsAsMeleeWeapon( def, authoritativeCombatId )
			     || !YaWeaponDefinitions.HasSecondaryMeleeResolved( def, authoritativeCombatId ) )
			{
				Log.Warning( "[YA] Heavy melee rejected: not a melee with secondary stats" );
				SendRpcFireOutcome( false, false, 0f, false, 0, 0f, 0f, false, null, YaWeaponImpactSurfaceKind.None, false );
				return;
			}
		}

		if ( IsWeaponBrokenInSlot( slot ) )
		{
			Log.Warning( "[YA] Fire rejected: weapon broken" );
			ClientNotifyWeaponBroken();
			SendRpcFireOutcome( false, false, 0f, false, 0, 0f, 0f, false, null, YaWeaponImpactSurfaceKind.None, false );
			return;
		}

		if ( _hostReloadInProgress || _hostShotgunPumpReloadSession )
		{
			Log.Warning( "[YA] Fire rejected: reloading" );
			SendRpcFireOutcome( false, false, 0f, false, 0, 0f, 0f, false, null, YaWeaponImpactSurfaceKind.None, false );
			return;
		}

		var now = Time.Now;
		var isMelee = YaWeaponDefinitions.TreatsAsMeleeWeapon( def, authoritativeCombatId );

		if ( attackVariant != 0 && !isMelee )
		{
			Log.Warning( "[YA] Fire rejected: melee-only attack variant while holding non-melee weapon" );
			SendRpcFireOutcome( false, false, 0f, false, 0, 0f, 0f, false, null, YaWeaponImpactSurfaceKind.None, false );
			return;
		}

		if ( isHeavyMeleeAttack )
		{
			if ( now < _nextMeleeHeavyAllowedHostTime )
			{
				Log.Warning( $"[YA] Heavy melee rejected: cooldown (next at {_nextMeleeHeavyAllowedHostTime})" );
				SendRpcFireOutcome( false, false, 0f, false, 0, 0f, 0f, false, null, YaWeaponImpactSurfaceKind.None, false );
				return;
			}
		}
		else if ( now < _nextFireAllowedHostTime )
		{
			Log.Warning( $"[YA] Fire rejected: fire rate (next at {_nextFireAllowedHostTime})" );
			SendRpcFireOutcome( false, false, 0f, false, 0, 0f, 0f, false, null, YaWeaponImpactSurfaceKind.None, false );
			return;
		}

		if ( !isMelee )
		{
			if ( slot.WeaponLoadedAmmo <= 0 )
			{
				Log.Warning( "[YA] Fire rejected: empty clip" );
				SendRpcFireOutcome( false, false, 0f, false, 0, 0f, 0f, false, null, YaWeaponImpactSurfaceKind.None, false );
				return;
			}

			var ammoBefore = slot.WeaponLoadedAmmo;

			slot.WeaponLoadedAmmo--;

			inv.ServerWriteSlot( hotbar, slot );

			var ammoAfter = slot.WeaponLoadedAmmo;

			_nextFireAllowedHostTime = now + def.FireIntervalSeconds;

			Log.Info( $"[YA] Fire accepted: ammo {ammoBefore}->{ammoAfter}" );

			if ( string.Equals( authoritativeCombatId, "m4", StringComparison.OrdinalIgnoreCase ) )
				SendWorldWeaponSound( M4FireSoundResource );
			else if ( string.Equals( authoritativeCombatId, "shotgun", StringComparison.OrdinalIgnoreCase ) )
				SendWorldWeaponSound( ShotgunFireSoundResource );

			PushWeaponHudToOwnerHost();
		}
		else
		{
			if ( isHeavyMeleeAttack )
				_nextMeleeHeavyAllowedHostTime = now + def.SecondaryAttackFireIntervalSeconds;
			else
				_nextFireAllowedHostTime = now + def.FireIntervalSeconds;
		}

		var dmgBaseForHit =
			isHeavyMeleeAttack ? def.SecondaryAttackBaseDamage : def.BaseDamage;

		var fpPresentationMelee = isHeavyMeleeAttack ? 2 : 1;

		Log.Info( $"[YA] Fire validation passed for '{GameObject.Name}' — executing hitscan" );

		var fireDir = dir;
		var clientKickPitch = 0f;
		var clientKickYaw = 0f;
		if ( !isMelee )
		{
			var ctrl = Components.Get<CharacterController>();
			var planarLen = ctrl.IsValid()
				? new Vector3( ctrl.Velocity.x, ctrl.Velocity.y, 0f ).Length
				: 0f;
			var movingRm = planarLen > 55f;
			var vitalsRm = Components.Get<YaVitalsStub>();
			var crouchedRm = vitalsRm.IsValid() && vitalsRm.ServerCrouching;

			fireDir = YaWeaponRecoilSolve.SolveAuthoritativeFireDirection(
				dir,
				eyeRot,
				def,
				ref _hostRecoilLastShotTime,
				ref _hostRecoilPatternIndex,
				ref _hostRecoilSprayOrdinal,
				now,
				aimDownSights,
				movingRm,
				crouchedRm,
				out _,
				out clientKickPitch,
				out clientKickYaw,
				out _ );

			Log.Info(
				$"[Thorns Recoil] snapshot weapon={def.Id} ads={aimDownSights} move={movingRm} crouch={crouchedRm} planarSpd={planarLen:F1}" );
			Log.Info( $"[Thorns Recoil] host kick out: pitch={clientKickPitch:F4}° yaw={clientKickYaw:F4}°" );
		}

		var range = def.MaxRange;
		var start = eyePos;
		var pelletCount = Math.Max( 1, def.PelletCount );

		if ( pelletCount <= 1 )
		{
			var fpKindSingle = isMelee ? fpPresentationMelee : 0;
			var dirN = fireDir.Normal;

			if ( !HostTryResolveHitscanDamageTarget( start, fireDir, range, out var tr, out var hitGo, out var victimPawn, out var victimHealth, out var usedAnalyticFallback, out var analyticHitPos ) )
			{
				var surfMiss = YaWeaponImpactSurfaceKind.None;
				var endMiss = HostFeedbackEndpointWorldTrace( start, dirN, range, out surfMiss );
				var feedbackMiss = !isMelee || surfMiss != YaWeaponImpactSurfaceKind.None;
				SendRpcFireOutcome(
					true,
					false,
					0f,
					false,
					fpKindSingle,
					isMelee ? 0f : clientKickPitch,
					isMelee ? 0f : clientKickYaw,
					feedbackMiss,
					feedbackMiss ? endMiss : null,
					surfMiss,
					false );
				return;
			}

			if ( !hitGo.IsValid() )
			{
				var surfMiss2 = YaWeaponImpactSurfaceKind.None;
				var endMiss2 = HostFeedbackEndpointWorldTrace( start, dirN, range, out surfMiss2 );
				var feedbackMiss2 = !isMelee || surfMiss2 != YaWeaponImpactSurfaceKind.None;
				SendRpcFireOutcome(
					true,
					false,
					0f,
					false,
					fpKindSingle,
					isMelee ? 0f : clientKickPitch,
					isMelee ? 0f : clientKickYaw,
					feedbackMiss2,
					feedbackMiss2 ? endMiss2 : null,
					surfMiss2,
					false );
				return;
			}

			var headshot = !isMelee && !usedAnalyticFallback &&
			               YaCombatAuthority.TryHeadshotFromTrace( tr, victimPawn );
			var dmg = dmgBaseForHit * (headshot ? def.HeadshotMultiplier : 1f );

			Log.Info( $"[YA] Applying damage: amount={dmg:F1}, headshot={headshot}" );

			var killingBlow = victimHealth.TakeDamage( dmg, new DamageContext
			{
				AttackerRoot = GameObject,
				Headshot = headshot,
				Kind = isMelee ? "melee" : "hitscan"
			} );

			var hitPos = usedAnalyticFallback ? analyticHitPos : tr.HitPosition;
			SendRpcFireOutcome(
				true,
				true,
				dmg,
				headshot,
				fpKindSingle,
				isMelee ? 0f : clientKickPitch,
				isMelee ? 0f : clientKickYaw,
				true,
				hitPos,
				YaWeaponImpactSurfaceKind.Player,
				killingBlow );
			return;
		}

		var totalDamageDealt = 0f;
		var anyDamage = false;
		var anyHeadshot = false;
		var anyKillPellet = false;
		Vector3? firstPelletBloodPos = null;

		for ( var p = 0; p < pelletCount; p++ )
		{
			var pelletDir = YaSharedHostHitscan.SamplePelletDirection( fireDir, def.PelletSpreadHalfAngleDegrees );
			if ( !HostTryResolveHitscanDamageTarget( start, pelletDir, range, out var trP, out var hitGoP, out var victimPawnP, out var victimHealthP, out var usedAnalyticFallbackP, out var analyticHitPosP ) )
				continue;

			if ( !hitGoP.IsValid() )
				continue;

			var headshotP = !usedAnalyticFallbackP && YaCombatAuthority.TryHeadshotFromTrace( trP, victimPawnP );
			var dmgP = def.BaseDamage * (headshotP ? def.HeadshotMultiplier : 1f );

			var killP = victimHealthP.TakeDamage( dmgP, new DamageContext
			{
				AttackerRoot = GameObject,
				Headshot = headshotP,
				Kind = "pellet"
			} );

			if ( killP )
				anyKillPellet = true;

			totalDamageDealt += dmgP;
			anyDamage = true;
			if ( headshotP )
				anyHeadshot = true;

			if ( !firstPelletBloodPos.HasValue )
				firstPelletBloodPos = usedAnalyticFallbackP ? analyticHitPosP : trP.HitPosition;
		}

		if ( anyDamage )
			Log.Info( $"[YA] Pellet volley: totalDamage={totalDamageDealt:F1}, pellets={pelletCount}" );

		var dirPellet = fireDir.Normal;
		YaWeaponImpactSurfaceKind surfPellet;
		Vector3 endPellet;
		if ( firstPelletBloodPos.HasValue )
		{
			endPellet = firstPelletBloodPos.Value;
			surfPellet = YaWeaponImpactSurfaceKind.Player;
		}
		else
		{
			endPellet = HostFeedbackEndpointWorldTrace( start, dirPellet, range, out surfPellet );
		}

		SendRpcFireOutcome(
			true,
			anyDamage,
			totalDamageDealt,
			anyHeadshot,
			0,
			clientKickPitch,
			clientKickYaw,
			true,
			endPellet,
			surfPellet,
			anyKillPellet );
	}

	[Rpc.Owner]
	void ClientNotifyWeaponBroken()
	{
		Log.Warning( "[YA] Owner notify: active weapon is broken (cannot fire until repaired — not implemented)" );
	}

	bool ValidateRpcCallerOwnsPawn() => YaPawn.ValidateHostRpcCallerOwnsPawnRoot( GameObject );

	static bool IsWeaponBrokenInSlot( YaInventorySlot slot )
	{
		return slot.HasDurability && slot.Durability <= 0f;
	}

	static bool TryResolveWeaponItemDefResilient( string itemId, out YaWeaponItemCatalog.YaItemDefinition itemDef )
	{
		if ( YaWeaponItemCatalog.TryGet( itemId, out itemDef ) )
			return itemDef.ItemType == YaItemType.Weapon;

		if ( string.Equals( itemId, "sniper", StringComparison.OrdinalIgnoreCase ) )
		{
			itemDef = new YaWeaponItemCatalog.YaItemDefinition(
				Id: "sniper",
				DisplayName: "Sniper",
				MaxStack: 1,
				ItemType: YaItemType.Weapon,
				CombatWeaponDefinitionId: "sniper",
				ViewModelAsset: YaViewModelController.SniperFirstPersonViewmodelPath,
				WorldModelAsset: YaViewModelController.SniperWorldModelPath );
			return true;
		}

		if ( string.Equals( itemId, "m9_bayonet", StringComparison.OrdinalIgnoreCase ) )
		{
			itemDef = new YaWeaponItemCatalog.YaItemDefinition(
				Id: "m9_bayonet",
				DisplayName: "M9 Bayonet",
				MaxStack: 1,
				ItemType: YaItemType.Weapon,
				CombatWeaponDefinitionId: "m9_bayonet",
				ViewModelAsset: YaViewModelController.BayonetM9FirstPersonViewmodelPath,
				WorldModelAsset: YaViewModelController.BayonetM9WorldModelPath );
			return true;
		}

		itemDef = default;
		return false;
	}

	/// <summary>
	/// One ray segment: hitboxes first, then physics-only (same as legacy single shot).
	/// </summary>
	SceneTraceResult HostTraceHitscanSegment( Vector3 segmentStart, Vector3 dir, float segmentLen, List<GameObject> ignoredRoots ) =>
		YaSharedHostHitscan.TraceHitscanSegment( GameObject, segmentStart, dir, segmentLen, ignoredRoots );

	/// <summary>
	/// Host hitscan can strike a large world collider (e.g. scene Plane) before the pawn along the same ray.
	/// Step past penetrable surfaces until we hit a damageable pawn, a placed structure (blocks), or run out of range.
	/// </summary>
	bool HostTryResolveHitscanDamageTarget(
		Vector3 start,
		Vector3 dir,
		float range,
		out SceneTraceResult tr,
		out GameObject hitGo,
		out YaPawn victimPawn,
		out YaPlayerHealth victimHealth,
		out bool usedAnalyticFallback,
		out Vector3 analyticHitPosition ) =>
		YaSharedHostHitscan.TryResolveHitscanDamageTarget(
			GameObject,
			start,
			dir,
			range,
			out tr,
			out hitGo,
			out victimPawn,
			out victimHealth,
			out usedAnalyticFallback,
			out analyticHitPosition );

	bool IsOriginPlausible( Vector3 eyeWorld )
	{
		const float maxEyeSpanFromFeet = 180f;
		if ( (eyeWorld - GameObject.WorldPosition).Length > maxEyeSpanFromFeet )
		{
			Log.Warning( "[YA] Origin sanity: eye too far from pawn root" );
			return false;
		}

		return true;
	}

	/// <summary>
	/// Pack visual kick for <see cref="RpcFireOutcome"/> — ints avoid flaky trailing-float Rpc deserialization on some builds.
	/// </summary>
	static (int pitchMilli, int yawMilli) PackKickMilliDegrees( float pitchDeg, float yawDeg ) =>
		((int)MathF.Round(pitchDeg * 1000f), (int)MathF.Round(yawDeg * 1000f));

	void SendRpcFireOutcome(
		bool ammunitionExpended,
		bool damageAppliedToTarget,
		float damageDealt,
		bool headshot,
		int fpAttackPresentationKind,
		float clientKickPitch,
		float clientKickYaw,
		bool feedbackHasEndpoint,
		Vector3? feedbackHitWorld,
		YaWeaponImpactSurfaceKind feedbackSurface,
		bool feedbackTargetKilled )
	{
		var (kmP, kmY) = PackKickMilliDegrees(
			fpAttackPresentationKind == 0 ? clientKickPitch : 0f,
			fpAttackPresentationKind == 0 ? clientKickYaw : 0f );
		var hx = 0;
		var hy = 0;
		var hz = 0;
		if ( feedbackHasEndpoint && feedbackHitWorld.HasValue )
			YaWeaponCombatFeedback.PackHitMm( feedbackHitWorld.Value, out hx, out hy, out hz );

		RpcFireOutcome(
			ammunitionExpended,
			damageAppliedToTarget,
			damageDealt,
			headshot,
			fpAttackPresentationKind,
			kmP,
			kmY,
			feedbackHasEndpoint,
			hx,
			hy,
			hz,
			(int)feedbackSurface,
			feedbackTargetKilled );
	}

	static YaWeaponImpactSurfaceKind HostClassifyFeedbackSurface( SceneTraceResult tr, GameObject hitGo )
	{
		if ( !tr.Hit || !hitGo.IsValid() )
			return YaWeaponImpactSurfaceKind.Terrain;
		return YaWeaponImpactSurfaceKind.Terrain;
	}

	SceneTraceResult HostTraceFeedbackWorldFirstHit( Vector3 rayStart, Vector3 dirN, float range )
	{
		var ray = new Ray( rayStart, dirN );
		return Scene.Trace.Ray( ray, range )
			.UseHitPosition( true )
			.UseHitboxes( true )
			.UsePhysicsWorld( true )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();
	}

	Vector3 HostFeedbackEndpointWorldTrace( Vector3 rayStart, Vector3 dirN, float range, out YaWeaponImpactSurfaceKind surfaceKind )
	{
		var tr = HostTraceFeedbackWorldFirstHit( rayStart, dirN, range );
		if ( tr.Hit && tr.GameObject.IsValid() )
		{
			surfaceKind = HostClassifyFeedbackSurface( tr, tr.GameObject );
			return tr.HitPosition;
		}

		surfaceKind = YaWeaponImpactSurfaceKind.None;
		return rayStart + dirN * range;
	}

	/// <summary>
	/// Owner-only: <paramref name="ammunitionExpended"/> is true only after the host consumed a round (THORNS §3 host authority).
	/// FP viewmodel <c>b_attack</c> must only pulse when <paramref name="ammunitionExpended"/> — never on rejected fire intents.
	/// </summary>
	[Rpc.Owner]
	void RpcFireOutcome(
		bool ammunitionExpended,
		bool damageAppliedToTarget,
		float damageDealt,
		bool headshot,
		int fpAttackPresentationKind,
		int clientKickPitchMilliDegrees,
		int clientKickYawMilliDegrees,
		bool feedbackHasEndpoint,
		int feedbackHitXMm,
		int feedbackHitYMm,
		int feedbackHitZMm,
		int feedbackSurfaceKind,
		bool feedbackTargetKilled )
	{
		var clientKickPitchDegrees = clientKickPitchMilliDegrees / 1000f;
		var clientKickYawDegrees = clientKickYawMilliDegrees / 1000f;

		if ( ammunitionExpended )
		{
			Log.Info(
				$"[YA] Fire confirmed (fpKind={fpAttackPresentationKind}, camKickPitch={clientKickPitchDegrees:F3}°, yaw={clientKickYawDegrees:F3}°, kickMilli=p{clientKickPitchMilliDegrees},y{clientKickYawMilliDegrees})" );
			PlayOwnerConfirmedGunshotSoundIfApplicable( fpAttackPresentationKind, damageAppliedToTarget );

			if ( feedbackHasEndpoint
			     && YaCombatAuthority.TryGetAuthoritativeEye( GameObject, out var traceStart, out _ ) )
			{
				var endWorld = YaWeaponCombatFeedback.UnpackHitMm( feedbackHitXMm, feedbackHitYMm, feedbackHitZMm );
				var surf = (YaWeaponImpactSurfaceKind)feedbackSurfaceKind;

				if ( fpAttackPresentationKind == 0 )
					YaWeaponCombatFeedback.SpawnGunTracerAndImpactLocal( traceStart, endWorld, surf, damageAppliedToTarget );
				else if ( fpAttackPresentationKind is 1 or 2 )
					YaWeaponCombatFeedback.SpawnImpactOnlyLocal( endWorld, surf );
			}

			if ( damageAppliedToTarget && feedbackTargetKilled && !string.IsNullOrWhiteSpace( KillConfirmSoundResource ) )
			{
				var killHandle = Sound.Play( KillConfirmSoundResource, GameObject.WorldPosition );
				if ( killHandle is { IsValid: true } killSnd )
					killSnd.Volume = 1f;
			}

			if ( damageAppliedToTarget && headshot && !string.IsNullOrWhiteSpace( HeadshotConfirmSoundResource ) )
			{
				var hsHandle = Sound.Play( HeadshotConfirmSoundResource, GameObject.WorldPosition );
				if ( hsHandle is { IsValid: true } hsSnd )
					hsSnd.Volume = 0.92f;
			}

			var fp = ResolveLocalFpAnimator();
			if ( fp.IsValid() )
			{
				if ( fpAttackPresentationKind == 1 )
					fp.OwnerNotifyMeleeAttackCommitted( heavy: false );
				else if ( fpAttackPresentationKind == 2 )
					fp.OwnerNotifyMeleeAttackCommitted( heavy: true );
				else
					fp.OwnerNotifyServerConfirmedFire();
			}

			if ( fpAttackPresentationKind == 0
			     && (clientKickPitchMilliDegrees != 0 || clientKickYawMilliDegrees != 0) )
			{
				var pawnForMove = GameObject.Components.GetInAncestorsOrSelf<YaPawn>( true );
				var pawnRootForMove = pawnForMove.IsValid() ? pawnForMove.GameObject : GameObject;
				var move = pawnRootForMove.IsValid() ? pawnRootForMove.Components.Get<YaPawnMovement>() : default;
				if ( move.IsValid() )
				{
					move.OwnerApplyMomentaryWeaponRecoil( clientKickPitchDegrees, clientKickYawDegrees );
					Log.Info( "[Thorns Recoil] client camera kick applied (visual only)" );
				}
			}

			if ( damageAppliedToTarget )
			{
				var hud = GameObject.Components.Get<YaPlayerHud>( FindMode.EnabledInSelf );
				hud?.NotifyHitmarkerLocal( fpAttackPresentationKind == 0 && headshot );
				if ( feedbackTargetKilled )
					hud?.NotifyKillConfirmedLocal();
			}
		}

		if ( ammunitionExpended && damageAppliedToTarget )
			Log.Info( $"[YA] Server-confirmed hit: damage={damageDealt:F1}, headshot={headshot}" );
		else if ( ammunitionExpended && !damageAppliedToTarget )
			Log.Info( "[YA] Fire confirmed — no damageable pawn hit (hitscan miss)" );
		else
			Log.Info( "[YA] Fire rejected (authoritative) — no round expended" );
	}

	/// <summary>Host only: updates third-person weapon mesh visibility/model (replicated to non-owners).</summary>
	public void HostApplyEquippedWorldPresentation( YaWeaponItemCatalog.YaItemDefinition def, bool treatAsHeldWeapon )
	{
		if ( !Networking.IsHost )
			return;

		var ww = FindDescendantNamed( GameObject, WorldVisualChildName );
		if ( !ww.IsValid() )
			return;

		var smr = GetOrCreateWorldSkinnedModelRenderer( ww );
		if ( !smr.IsValid() )
			return;

		if ( !treatAsHeldWeapon || def is null || string.IsNullOrEmpty( def.WorldModelAsset ) )
		{
			smr.Enabled = false;
			Log.Info( "[YA] World weapon model hidden (non-weapon selection or no mesh)" );
			return;
		}

		// Reparenting runs on all clients in <see cref="YaWeaponWorldVisual"/> so the hierarchy matches everywhere.

		smr.Model = YaWeaponResourceLoad.LoadWeaponModelOrFallback( def.WorldModelAsset, "TP world weapon", out var usedFallbackGeometry );
		// LocalScale is owned by <see cref="YaWeaponWorldVisual"/> on every peer so it matches ObserversCombatWeaponDefinitionId
		// and is not stacked with host + client applications.
		smr.Tint = usedFallbackGeometry ? new Color( 0.85f, 0.45f, 0.12f, 1f ) : Color.White;
		smr.UseAnimGraph = false;
		smr.Enabled = true;
		Log.Info( $"[YA] World weapon model updated (networked): asset={def.WorldModelAsset} fallbackGeometry={usedFallbackGeometry}" );
	}

	/// <summary>Skinned vmdl on a plain <see cref="ModelRenderer"/> shows engine ERROR; world weapons use <see cref="SkinnedModelRenderer"/>.</summary>
	public static SkinnedModelRenderer GetOrCreateWorldSkinnedModelRenderer( GameObject weaponWorld )
	{
		if ( !weaponWorld.IsValid() )
			return default;

		var s = weaponWorld.Components.Get<SkinnedModelRenderer>();
		if ( s.IsValid() )
			return s;

		foreach ( var c in weaponWorld.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelf ) )
		{
			if ( c is SkinnedModelRenderer sh )
				return sh;
			if ( c is not null && c.IsValid() )
				c.Destroy();
		}

		return weaponWorld.Components.Create<SkinnedModelRenderer>();
	}

	/// <summary>
	/// Places the third-person weapon mesh on the animated right-hand bone (world space each frame).
	/// Prefer this over <see cref="ParentWorldWeaponToCitizenRig"/> when you want the carry pose to follow grip IK instead of a fixed torso offset.
	/// Returns false if Body/skin/bone is missing — caller should fall back to <see cref="ParentWorldWeaponToCitizenRig"/>.
	/// </summary>
	public static bool TryAlignThirdPersonWeaponToCitizenRightHand(
		GameObject pawnRoot,
		GameObject weaponWorld,
		Vector3 gripOffsetInHandSpace,
		Rotation gripRotationAfterHand )
	{
		if ( !pawnRoot.IsValid() || !weaponWorld.IsValid() )
			return false;

		var body = FindDescendantNamed( pawnRoot, "Body" );
		if ( !body.IsValid() )
			return false;

		var skin = body.Components.Get<SkinnedModelRenderer>();
		if ( !skin.IsValid() )
			return false;

		foreach ( var boneName in CitizenTpWeaponRightHandBoneCandidates )
		{
			if ( !skin.TryGetBoneTransform( boneName, out var handWorld ) )
				continue;

			if ( weaponWorld.Parent != body )
				weaponWorld.SetParent( body );

			var worldRot = handWorld.Rotation * gripRotationAfterHand;
			var worldPos = handWorld.Position + handWorld.Rotation * gripOffsetInHandSpace;
			weaponWorld.WorldRotation = worldRot;
			weaponWorld.WorldPosition = worldPos;
			return true;
		}

		return false;
	}

	public static void ParentWorldWeaponToCitizenRig( GameObject pawnRoot, GameObject weaponWorld ) =>
		ParentWorldWeaponToCitizenRig(
			pawnRoot,
			weaponWorld,
			WorldMeshLocalPositionRelBody,
			Rotation.Identity,
			WorldMeshLocalPositionIfNoBody );

	/// <param name="localPositionRelBody">Local position under <c>Body</c> (inspector: <see cref="YaWeaponWorldVisual.TpWeaponManualLocalPositionRelBody"/>).</param>
	/// <param name="localRotationRelBody">Local rotation under <c>Body</c>.</param>
	/// <param name="localPositionIfNoBody">Local position when parented to pawn root if <c>Body</c> is missing.</param>
	public static void ParentWorldWeaponToCitizenRig(
		GameObject pawnRoot,
		GameObject weaponWorld,
		Vector3 localPositionRelBody,
		Rotation localRotationRelBody,
		Vector3 localPositionIfNoBody )
	{
		if ( !pawnRoot.IsValid() || !weaponWorld.IsValid() )
			return;

		// Prefer depth-first name match — replication can attach Citizen Body after spawn; not always a direct child.
		var body = FindDescendantNamed( pawnRoot, "Body" );
		if ( body.IsValid() )
		{
			// SetParent only when needed — repeated reparents can stack LocalScale. Always re-apply local pose so
			// network transform replication cannot leave the weapon stuck at root/View offsets for remote viewers.
			if ( weaponWorld.Parent != body )
				weaponWorld.SetParent( body );

			weaponWorld.LocalPosition = localPositionRelBody;
			weaponWorld.LocalRotation = localRotationRelBody;
			return;
		}

		if ( weaponWorld.Parent != pawnRoot )
			weaponWorld.SetParent( pawnRoot );

		weaponWorld.LocalPosition = localPositionIfNoBody;
		weaponWorld.LocalRotation = Rotation.Identity;
	}

	/// <summary>Alone + mimic: replicated observer combat can match the mirrored hunter; re-apply FP from owner hotbar snapshot so the viewmodel stays M9 (etc.).</summary>
	void TryRefreshFirstPersonPresentationForAloneMimic()
	{
		var roleCmp = Components.Get<YaPlayerRoleComponent>( FindMode.EnabledInSelf );
		var aloneMech = Components.Get<YaAloneMechanics>( FindMode.EnabledInSelf );
		var equip = Components.Get<YaHotbarEquipment>();
		if ( roleCmp is not { IsValid: true, Role: YaPlayerRole.Alone }
		     || aloneMech is not { IsValid: true, MimicPresentationActive: true }
		     || !equip.IsValid() )
			return;

		var trueCombat = equip.ClientMirrorEquippedCombatWeaponDefinitionId;
		if ( string.IsNullOrWhiteSpace( trueCombat ) )
			return;

		if ( string.Equals( _ownerMirrorCombatWeaponDefinitionId, trueCombat, StringComparison.OrdinalIgnoreCase ) )
			return;

		if ( !YaWeaponItemCatalog.TryGet( equip.ClientMirrorActiveItemId, out var def ) || def.ItemType != YaItemType.Weapon )
			return;

		var vm = string.IsNullOrEmpty( def.ViewModelAsset ) ? "models/dev/box.vmdl" : def.ViewModelAsset;
		var combatId = string.IsNullOrEmpty( def.CombatWeaponDefinitionId ) ? trueCombat : def.CombatWeaponDefinitionId;
		ApplyOwnerEquipmentPresentation( true, vm, combatId );
	}

	/// <summary>Owner client only — spawn/update local FP viewmodel after server equip confirmation.</summary>
	public void ApplyOwnerEquipmentPresentation( bool showWeaponFp, string viewModelAsset, string combatWeaponDefinitionId )
	{
		_ownerMirrorCombatWeaponDefinitionId = combatWeaponDefinitionId ?? "";
		_clientNextAutoFireIntentTime = 0;

		var viewChild = FindChild( GameObject, "View" );
		if ( !viewChild.IsValid() )
			return;

		var vmc = viewChild.Components.Get<YaViewModelController>();
		if ( !vmc.IsValid() )
			vmc = viewChild.Components.Create<YaViewModelController>();

		if ( !showWeaponFp || string.IsNullOrEmpty( viewModelAsset ) )
		{
			vmc.ClearViewModel();
			Log.Info( "[YA] Owner FP weapon viewmodel cleared (non-weapon or empty asset)" );
			return;
		}

		var vmPath = viewModelAsset;
		if ( string.Equals( combatWeaponDefinitionId, "m4", StringComparison.OrdinalIgnoreCase ) )
			vmPath = YaViewModelController.M4FirstPersonViewmodelPath;
		else if ( string.Equals( combatWeaponDefinitionId, "mp5", StringComparison.OrdinalIgnoreCase ) )
			vmPath = YaViewModelController.Mp5FirstPersonViewmodelPath;
		else if ( string.Equals( combatWeaponDefinitionId, "shotgun", StringComparison.OrdinalIgnoreCase ) )
			vmPath = YaViewModelController.ShotgunFirstPersonViewmodelPath;
		else if ( string.Equals( combatWeaponDefinitionId, "sniper", StringComparison.OrdinalIgnoreCase ) )
			vmPath = YaViewModelController.SniperFirstPersonViewmodelPath;
		else if ( string.Equals( combatWeaponDefinitionId, "m9_bayonet", StringComparison.OrdinalIgnoreCase ) )
			vmPath = YaViewModelController.BayonetM9FirstPersonViewmodelPath;

		vmc.SpawnViewModel( vmPath );

		Log.Info( $"[YA] Weapon VIEWMODEL updated (local-only, server-confirmed equip): asset={vmPath}, combatMirror={_ownerMirrorCombatWeaponDefinitionId}" );
	}

	protected override void OnDestroy()
	{
		var viewChild = FindChild( GameObject, "View" );
		if ( !viewChild.IsValid() )
			return;

		var vmc = viewChild.Components.Get<YaViewModelController>();
		if ( vmc.IsValid() )
			vmc.ClearViewModel();
	}

	void TickFpViewmodelSequences()
	{
		var fp = ResolveLocalFpAnimator();
		if ( !fp.IsValid() )
			return;

		var cid = ClientMirrorCombatDefinitionId;
		var def = YaWeaponDefinitions.Get( cid );
		var meleeEquipped = YaWeaponDefinitions.TreatsAsMeleeWeapon( def, cid );
		var wantAds = !meleeEquipped && (Input.Down( "Attack2" ) || Input.Down( "attack2" ));
		var reloadEdge = _clientReloading && !_fpVmLastReloadHud;
		_fpVmLastReloadHud = _clientReloading;

		var shellReloadFx = !meleeEquipped && YaWeaponDefinitions.UsesPerShellReloadCycle( def, cid );
		var reloadSecs = shellReloadFx
			? YaWeaponDefinitions.ShellReloadGameplayGateSeconds( def, cid )
			: Math.Max( 0.01f, def.ReloadTimeSeconds );
		var primaryHeld = Input.Down( "Attack1" ) || Input.Down( "attack1" );
		var tubeAmmoBeforeShellRpc = shellReloadFx ? ClientMirrorLoadedAmmo : -1;

		var vitals = Components.Get<YaVitalsStub>();
		var sprintHeldFp = meleeEquipped && vitals.IsValid() && vitals.ServerSprinting;

		fp.OwnerTickPresentation(
			wantAds,
			reloadEdge,
			reloadSecs,
			primaryHeld,
			YaWeaponDefinitions.FiringModeGraphValue( def ),
			driveMeleeKnifeExtras: meleeEquipped,
			sprintHeld: sprintHeldFp,
			shellReloadPresentation: shellReloadFx,
			shellReloadAmmoBeforeRpc: tubeAmmoBeforeShellRpc,
			shotgunPumpSessionHeld: _clientShotgunPumpHud );
	}

	static YaViewModelFpAnimator ResolveLocalFpAnimator( GameObject pawnRoot )
	{
		var view = FindChild( pawnRoot, "View" );
		if ( !view.IsValid() )
			return default;

		var wm = FindChild( view, "WeaponViewmodel" );
		if ( !wm.IsValid() )
			return default;

		return wm.Components.Get<YaViewModelFpAnimator>();
	}

	YaViewModelFpAnimator ResolveLocalFpAnimator() => ResolveLocalFpAnimator( GameObject );

	static GameObject FindChild( GameObject root, string name )
	{
		foreach ( var c in root.Children )
		{
			if ( c.Name == name )
				return c;
		}

		return default;
	}

	static GameObject FindDescendantNamed( GameObject root, string name )
	{
		foreach ( var c in root.Children )
		{
			if ( c.Name == name )
				return c;

			var nested = FindDescendantNamed( c, name );
			if ( nested.IsValid() )
				return nested;
		}

		return default;
	}

	public Transform GetPresentationMuzzleTransform()
	{
		var world = FindDescendantNamed( GameObject, WorldVisualChildName );
		if ( YaPawn.IsLocalConnectionOwner( this ) )
		{
			var view = FindChild( GameObject, "View" );
			if ( view.IsValid() )
				return view.WorldTransform;
		}

		if ( world.IsValid() )
			return world.WorldTransform;

		return GameObject.WorldTransform;
	}
}
