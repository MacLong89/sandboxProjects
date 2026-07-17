using Sandbox;
using Sandbox.Network;
using Terraingen;
using Terraingen.AI;
using Terraingen.Animals;
using Terraingen.Buildings;
using Terraingen.Combat.Attachments;
using Terraingen.GameData;
using Terraingen.Player;
using Terraingen.Multiplayer;

namespace Terraingen.Combat;

[Title( "Thorns — Player Weapon Combat" )]
[Category( "Thorns/Combat" )]
[Icon( "sports_martial_arts" )]
[Order( 48 )]
public sealed class ThornsPlayerWeaponCombat : Component
{
	[Sync( SyncFlags.FromHost )] public int MirrorLoadedAmmo { get; private set; }
	[Sync( SyncFlags.FromHost )] public int MirrorReserveAmmo { get; private set; }
	[Sync( SyncFlags.FromHost )] public bool MirrorReloading { get; private set; }

	double _nextHostFireTime;
	double _hostReloadCompleteTime;
	bool _hostReloadInProgress;
	int _hostReloadHotbar = -1;

	double _hostRecoilLastShotTime;
	int _hostRecoilPatternIndex;
	int _hostRecoilSprayOrdinal;

	double _clientRecoilLastShotTime;
	int _clientRecoilPatternIndex;
	int _clientRecoilSprayOrdinal;

	double _clientNextFireIntentTime;

	public static bool IsRangedWeaponEquipped( GameObject pawn )
	{
		if ( pawn is null || !pawn.IsValid() )
			return false;

		var gameplay = pawn.Components.Get<ThornsPlayerGameplay>();
		if ( !gameplay.IsValid() || !gameplay.TryGetActiveHotbarItemId( out var itemId ) )
			return false;

		if ( !ThornsItemRegistry.TryGet( itemId, out var def ) || def.ItemType != ThornsItemType.Weapon )
			return false;

		var combatId = ThornsInventoryWeaponState.ResolveCombatId( def, itemId );
		var wdef = ThornsWeaponDefinitions.Get( combatId );
		return !ThornsWeaponDefinitions.TreatsAsMeleeWeapon( wdef, combatId )
		       && wdef.ClipSize > 0
		       && TryResolveActiveFirearm( wdef, combatId );
	}

	public void RequestBowNockIfNeeded()
	{
		// AUDIT FIX: bow nock is a combat intent — same dead/UI gate as guns.
		if ( ThornsPlayerActionGate.BlocksLocalWorldActions( GameObject ) )
			return;

		if ( ShouldBlockCombatWhileBuilding() )
			return;

		if ( !TryResolveActiveAmmoWeapon( out var combatId, out var def ) )
			return;

		if ( !ThornsWeaponDefinitions.IsBowWeapon( def, combatId ) )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcRequestBowNock();
		else
			HostTryNockBowArrow();
	}

	public void RequestBowReleaseFire( Vector3 origin, Vector3 direction )
	{
		// AUDIT FIX: release still reaches HostTryFire — gate locally before predict/RPC.
		if ( ThornsPlayerActionGate.BlocksLocalWorldActions( GameObject ) )
			return;

		if ( ShouldBlockCombatWhileBuilding() )
			return;

		if ( !TryResolveActiveAmmoWeapon( out var combatId, out var def ) )
			return;

		if ( !ThornsWeaponDefinitions.IsBowWeapon( def, combatId ) )
			return;

		if ( !TryGetActiveHotbarStack( out var stack ) )
			return;

		ApplyLocalWeaponRecoilView( direction, def, aimDownSights: true, stack, combatId );
		if ( Networking.IsActive && !Networking.IsHost )
		{
			ThornsCombatTracerWorldService.SpawnPredictedLocal( Scene, GameObject, origin, direction, def.MaxRange, combatId );
			RpcRequestFire( origin, direction, aimDownSights: true );
		}
		else
			HostTryFire( origin, direction, aimDownSights: true );
	}

	ThornsPlayerGameplay _cachedGameplay;

	protected override void OnStart()
	{
		_cachedGameplay = Components.Get<ThornsPlayerGameplay>();
	}

	protected override void OnUpdate()
	{
		if ( ThornsMultiplayer.IsHostOrOffline )
			_cachedGameplay?.FlushThrottledInventorySyncIfDirty();

		if ( !IsLocallyControlled() )
			return;

		if ( IsPlacementModeActive() )
		{
			TickReloadHost();
			return;
		}

		if ( Input.Pressed( "reload" ) || Input.Pressed( "Reload" ) )
			RequestReload();

		TickReloadHost();
		TickLocalFireInput();
	}

	void TickReloadHost()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !_hostReloadInProgress )
			return;

		if ( Time.Now < _hostReloadCompleteTime )
			return;

		_hostReloadInProgress = false;
		MirrorReloading = false;
		CompleteReloadHost();
	}

	void TickLocalFireInput()
	{
		// AUDIT FIX: dead / inventory-open clients must not send fire intents (Attack1 is shared with UI).
		if ( ThornsPlayerActionGate.BlocksLocalWorldActions( GameObject ) )
			return;

		if ( !TryResolveActiveFirearm( out var combatId, out var def ) )
			return;

		if ( !PresentationAllowsFire() )
			return;

		if ( Time.Now < _clientNextFireIntentTime )
			return;

		var auto = string.Equals( def.FireMode, "auto", StringComparison.OrdinalIgnoreCase );
		var attackDown = Input.Down( "Attack1" ) || Input.Down( "attack1" );
		var attackPressed = Input.Pressed( "Attack1" ) || Input.Pressed( "attack1" );

		if ( auto )
		{
			if ( !attackDown )
				return;
		}
		else if ( !attackPressed )
		{
			return;
		}

		if ( !TryGetActiveHotbarStack( out var stack ) )
			return;

		if ( stack.WeaponLoadedAmmo <= 0 || stack.IsWeaponBroken( combatId ) )
			return;

		if ( !ThornsSceneObserver.TryResolveLocalAimRay( GameObject, out var origin, out var direction, useScreenCenter: true ) )
			return;

		var ads = Input.Down( "Attack2" ) || Input.Down( "attack2" );
		_clientNextFireIntentTime = Time.Now + def.FireIntervalSeconds;

		var fireDir = ApplyLocalWeaponRecoilView( direction, def, ads, stack, combatId );
		SpawnPredictedTracers( GameObject, origin, fireDir, def, combatId );
		if ( Networking.IsActive && !Networking.IsHost )
			RpcRequestFire( origin, direction, ads );
		else
			HostTryFire( origin, direction, ads );
	}

	static void SpawnPredictedTracers(
		GameObject pawnRoot,
		Vector3 origin,
		Vector3 fireDir,
		ThornsWeaponDefinitions.WeaponDefinition def,
		string combatId )
	{
		if ( pawnRoot is null || !pawnRoot.IsValid() )
			return;

		// Pure clients predict locally; host/offline tracers come from HostTryFire only.
		if ( !Networking.IsActive || Networking.IsHost )
			return;

		var pelletCount = Math.Max( 1, def.PelletCount );
		var spreadHalf = def.PelletSpreadHalfAngleDegrees;
		for ( var p = 0; p < pelletCount; p++ )
		{
			var pelletDir = pelletCount <= 1
				? fireDir
				: ThornsBanditCombatUtil.SamplePelletDirection( fireDir, spreadHalf );
			ThornsCombatTracerWorldService.SpawnPredictedLocal(
				pawnRoot.Scene,
				pawnRoot,
				origin,
				pelletDir,
				def.MaxRange,
				combatId );
		}
	}

	Vector3 ApplyLocalWeaponRecoilView( Vector3 aimDirection, ThornsWeaponDefinitions.WeaponDefinition def, bool aimDownSights, in ThornsItemStack stack, string combatId )
	{
		var effective = ThornsWeaponEffectiveStats.Resolve( def, combatId, stack );
		var aim = aimDirection.Normal;
		var locomotion = Components.Get<ThornsPlayerLocomotion>();
		if ( !locomotion.IsValid() )
			return aim;

		var controller = GameObject.Components.Get<PlayerController>( FindMode.EverythingInSelf );
		var eyeRot = controller.IsValid() ? controller.EyeAngles.ToRotation() : Rotation.Identity;
		var vel = controller.IsValid() ? controller.Velocity : Vector3.Zero;
		var moving = vel.WithZ( 0f ).Length > 55f;
		var crouched = ResolveCrouching();
		var lastShot = _clientRecoilLastShotTime;
		var patternIdx = _clientRecoilPatternIndex;
		var sprayOrd = _clientRecoilSprayOrdinal;

		var fireDir = ThornsWeaponRecoilSolve.SolveAuthoritativeFireDirection(
			aim,
			eyeRot,
			def,
			ref lastShot,
			ref patternIdx,
			ref sprayOrd,
			Time.Now,
			aimDownSights,
			moving,
			crouched,
			out _,
			out var kickPitch,
			out var kickYaw,
			out _,
			effective.RecoilKickMultiplier,
			ThornsAttachmentModifiers.BloomMultiplier( effective.Attachments ) );

		_clientRecoilLastShotTime = lastShot;
		_clientRecoilPatternIndex = patternIdx;
		_clientRecoilSprayOrdinal = sprayOrd;

		locomotion.ApplyWeaponRecoilKick( kickPitch, kickYaw );
		NotifyLocalFirePresentation( kickPitch, kickYaw, combatId, effective.NoiseLoudnessMultiplier );
		return fireDir.Normal.Length >= 0.95f ? fireDir.Normal : aim;
	}

	void NotifyLocalFirePresentation( float kickPitch, float kickYaw, string combatId, float noiseMul )
	{
		if ( !IsLocallyControlled() )
			return;

		var rig = ThornsPlayerFirstPersonRig.ResolvePresentationCameraObject( GameObject );
		rig?.Components.Get<ThornsViewModelController>()?.ApplyViewKick( kickPitch, kickYaw );
		GameObject.Components.Get<ThornsFpPresentation>()?.NotifyLocalConfirmedFire();

		var fireSound = ThornsGameplaySfx.FireSoundForCombatId( combatId );
		if ( !string.IsNullOrWhiteSpace( fireSound ) )
			ThornsGameplaySfx.PlayNetworkedCombatSound( GameObject, fireSound, ThornsSpatialSfxCategory.PlayerGunshot, noiseMul );
	}

	public void ResetClientCombatPresentation()
	{
		_clientRecoilLastShotTime = 0;
		_clientRecoilPatternIndex = 0;
		_clientRecoilSprayOrdinal = 0;
		_clientNextFireIntentTime = 0;

		Components.Get<ThornsPlayerLocomotion>()?.ResetWeaponRecoilState();

		var rig = ThornsPlayerFirstPersonRig.ResolvePresentationCameraObject( GameObject );
		rig?.Components.Get<ThornsViewModelController>()?.ResetViewKick();
	}

	bool PresentationAllowsFire()
	{
		if ( MirrorReloading )
			return false;

		if ( ThornsPlayerMovementDefaults.IsSprintMoving( GameObject ) )
			return false;

		var fp = Components.Get<ThornsFpPresentation>();
		return !fp.IsValid() || fp.ClientMirrorFpPresentationAllowsCombatLayers();
	}

	static bool ResolveCrouching( GameObject pawn )
	{
		var cc = pawn.Components.Get<CharacterController>( FindMode.EverythingInSelf );
		if ( cc.IsValid() && cc.Height < ThornsPlayerFirstPersonRig.DefaultBodyHeight - 8f )
			return true;

		if ( ThornsLocalPlayer.IsLocallyControlledPawn( pawn ) )
			return Input.Down( "Duck" );

		return false;
	}

	bool ResolveCrouching() => ResolveCrouching( GameObject );

	[Rpc.Host]
	void RpcRequestFire( Vector3 origin, Vector3 direction, bool aimDownSights )
	{
		if ( !ThornsNetAuthority.ValidateOwnerCaller( this ) )
			return;

		HostTryFire( origin, direction, aimDownSights );
	}

	void HostTryFire( Vector3 origin, Vector3 direction, bool aimDownSights )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || _hostReloadInProgress )
			return;

		// AUDIT FIX: host refuses fire from dead pawns even if a stale RPC arrives.
		if ( ThornsPlayerActionGate.BlocksHostWorldActions( GameObject ) )
			return;

		if ( ShouldBlockCombatWhileBuilding() )
			return;

		if ( ThornsPlayerMovementDefaults.IsSprintMoving( GameObject ) )
			return;

		if ( !TryResolveActiveAmmoWeapon( out var combatId, out var def ) )
			return;

		if ( Time.Now < _nextHostFireTime )
			return;

		if ( !ThornsCombatFireValidation.TryResolveAuthoritativeShot( GameObject, origin, direction, def.MaxRange, out origin, out var aimDir ) )
			return;

		if ( aimDir.Length < 0.95f )
			return;

		var gameplay = _cachedGameplay;
		if ( !gameplay.IsValid() )
		{
			_cachedGameplay = Components.Get<ThornsPlayerGameplay>();
			gameplay = _cachedGameplay;
		}

		if ( !gameplay.IsValid() || !gameplay.TryGetActiveHotbarIndex( out var hotbar ) )
			return;

		var stack = gameplay.GetHotbarSlot( hotbar );
		var effective = ThornsWeaponEffectiveStats.Resolve( def, combatId, stack );
		if ( stack.IsEmpty || stack.WeaponLoadedAmmo <= 0 || stack.IsWeaponBroken( combatId ) )
			return;

		_nextHostFireTime = Time.Now + def.FireIntervalSeconds;

		stack.WeaponLoadedAmmo--;
		if ( def.DurabilityLossPerShot > 0.0001f )
		{
			stack.HasDurability = true;
			stack.Durability = Math.Max( 0f, stack.Durability - def.DurabilityLossPerShot );
		}

		gameplay.SetHotbarSlot( hotbar, stack, pushInventory: false );
		PushWeaponHud( gameplay, hotbar, stack, combatId, def );

		var controller = GameObject.Components.Get<PlayerController>( FindMode.EverythingInSelf );
		var eyeRot = controller.IsValid() ? controller.EyeAngles.ToRotation() : Rotation.Identity;
		var vel = controller.IsValid() ? controller.Velocity : Vector3.Zero;
		var moving = vel.WithZ( 0f ).Length > 55f;
		var crouched = ResolveCrouching();

		var dir = ThornsWeaponRecoilSolve.SolveAuthoritativeFireDirection(
			aimDir,
			eyeRot,
			def,
			ref _hostRecoilLastShotTime,
			ref _hostRecoilPatternIndex,
			ref _hostRecoilSprayOrdinal,
			Time.Now,
			aimDownSights,
			moving,
			crouched,
			out _,
			out _,
			out _,
			out _,
			effective.RecoilKickMultiplier,
			ThornsAttachmentModifiers.BloomMultiplier( effective.Attachments ) );

		var noiseMul = effective.NoiseLoudnessMultiplier;
		if ( !IsLocallyControlled() )
		{
			var fireSound = ThornsGameplaySfx.FireSoundForCombatId( combatId );
			if ( !string.IsNullOrWhiteSpace( fireSound ) )
				ThornsGameplaySfx.PlayNetworkedCombatSound( GameObject, fireSound, ThornsSpatialSfxCategory.PlayerGunshot, noiseMul );
		}

		ThornsBanditCommunication.HostRegisterGunshot( origin, noiseMul );

		var pelletCount = Math.Max( 1, def.PelletCount );
		var spreadHalf = def.PelletSpreadHalfAngleDegrees;
		var totalDamage = 0f;
		var anyKill = false;
		ThornsItemRegistry.TryGet( stack.ItemId, out var itemDef );
		var damageMul = itemDef is not null ? ThornsItemTier.ResolveStatMultiplier( stack, itemDef ) : 1f;
		var scaledDamage = def.BaseDamage * damageMul;

		for ( var p = 0; p < pelletCount; p++ )
		{
			var pelletDir = pelletCount <= 1
				? dir
				: ThornsBanditCombatUtil.SamplePelletDirection( dir, spreadHalf );

			var hit = HostTryApplyPelletDamage(
				gameplay,
				origin,
				aimDir,
				pelletDir,
				def.MaxRange,
				combatId,
				scaledDamage,
				out var pelletDamage,
				out var pelletKill,
				out var victim,
				out var victimKind,
				out var resolveTrace );

			var impact = hit && resolveTrace.Hit
				? resolveTrace.HitPosition
				: ThornsCombatHitResolver.SampleImpactPointOnRay(
					GameObject.Scene,
					origin,
					pelletDir,
					def.MaxRange,
					GameObject,
					victim,
					resolveTrace );

			var blockDistance = hit && resolveTrace.Hit
				? ThornsCombatTraceUtil.ResolveDistance( origin, resolveTrace )
				: Vector3.DistanceBetween( origin, impact );

			var muzzle = ThornsCombatMuzzleResolve.ResolvePlayerTracerOrigin(
				GameObject,
				pelletDir,
				combatId,
				preferFirstPersonViewmodel: IsLocallyControlled() );

			ThornsCombatTracerWorldService.HostBroadcastSegment(
				GameObject.Scene,
				muzzle,
				impact,
				ThornsCombatTracerSource.Player );

			ThornsCombatHitscanDebug.LogPlayerShot(
				GameObject,
				combatId,
				origin,
				muzzle,
				aimDir,
				pelletDir,
				def.MaxRange,
				hit,
				victim,
				victimKind,
				impact,
				blockDistance,
				hit ? $"damage={pelletDamage:F1} killed={pelletKill}" : "miss" );

			if ( hit )
			{
				totalDamage += pelletDamage;
				anyKill |= pelletKill;
			}
		}

		if ( totalDamage > 0f )
			Components.Get<ThornsPlayerGameplay>()?.PushCrosshairHitFeedbackToOwner( totalDamage, anyKill );
	}

	bool HostTryApplyPelletDamage(
		ThornsPlayerGameplay gameplay,
		Vector3 origin,
		Vector3 aimDir,
		Vector3 pelletDir,
		float maxRange,
		string combatId,
		float damage,
		out float damageDealt,
		out bool killed,
		out GameObject victim,
		out ThornsCombatDamage.VictimKind victimKind,
		out SceneTraceResult resolveTrace )
	{
		damageDealt = 0f;
		killed = false;
		victim = null;
		victimKind = ThornsCombatDamage.VictimKind.Unknown;
		resolveTrace = default;

		if ( !TryResolveWeaponVictim( origin, aimDir, pelletDir, maxRange, GameObject, out victim, out victimKind, out var hitDir, out resolveTrace ) )
			return false;

		ThornsCombatDamage.DamageResult result = default;
		if ( victimKind == ThornsCombatDamage.VictimKind.Animal )
		{
			var animal = victim.Components.Get<ThornsAnimalBrain>( FindMode.EverythingInSelfAndParent );
			if ( !animal.IsValid() || ThornsAnimalCombatRules.ShouldIgnoreDamage( animal, GameObject ) )
				return false;

			result = ThornsCombatDamage.HostApplyDamage(
				GameObject,
				animal.GameObject,
				ThornsCombatDamage.BuildAttackerWeaponHit(
					GameObject,
					animal.GameObject,
					damage,
					combatId,
					ThornsCombatDamage.VictimKind.Animal,
					hitDir ) );
			if ( result.Killed && !animal.IsTamed )
				gameplay.HostNotifyWildlifeKill();

			ThornsAnimalCompanion.NotifyOwnerMarkedTarget( GameObject, animal.GameObject );
		}
		else if ( victimKind == ThornsCombatDamage.VictimKind.Npc )
		{
			var bandit = victim.Components.Get<ThornsBanditBrain>( FindMode.EverythingInSelfAndParent );
			if ( !bandit.IsValid() )
				return false;

			ThornsCitizenHitbox.TryClassifyCitizenHit( victim, origin, hitDir, maxRange, resolveTrace, out var hitWorld, out var headshot );
			result = ThornsCombatDamage.HostApplyDamage(
				GameObject,
				victim,
				ThornsCombatDamage.BuildAttackerWeaponHit(
					GameObject,
					victim,
					damage,
					combatId,
					ThornsCombatDamage.VictimKind.Npc,
					hitDir,
					headshot,
					hitWorld ) );

			ThornsAnimalCompanion.NotifyOwnerMarkedTarget( GameObject, bandit.GameObject );
		}
		else if ( victimKind == ThornsCombatDamage.VictimKind.Player )
		{
			ThornsCitizenHitbox.TryClassifyCitizenHit( victim, origin, hitDir, maxRange, resolveTrace, out var hitWorld, out var headshot );
			result = ThornsCombatDamage.HostApplyDamage( GameObject, victim,
				ThornsCombatDamage.BuildPlayerWeaponHit( GameObject, victim, damage, combatId, headshot, hitWorld, -hitDir ) );
		}
		else
		{
			return false;
		}

		if ( !result.Applied )
			return false;

		damageDealt = result.DamageDealt > 0f ? result.DamageDealt : damage;
		killed = result.Killed;
		return true;
	}

	static bool TryResolveWeaponVictim(
		Vector3 origin,
		Vector3 aimDir,
		Vector3 recoiledDir,
		float maxRange,
		GameObject attackerRoot,
		out GameObject victim,
		out ThornsCombatDamage.VictimKind victimKind,
		out Vector3 hitDir,
		out SceneTraceResult resolveTrace )
	{
		victim = null;
		victimKind = ThornsCombatDamage.VictimKind.Unknown;
		resolveTrace = default;
		hitDir = recoiledDir.Normal;
		if ( hitDir.Length < 0.95f )
			hitDir = aimDir.Normal;

		return ThornsCombatHitResolver.TryResolveVictimAlongRay(
			attackerRoot.Scene,
			origin,
			hitDir,
			maxRange,
			attackerRoot,
			out victim,
			out victimKind,
			out resolveTrace );
	}

	public void RequestReload()
	{
		if ( ThornsPlayerActionGate.BlocksLocalWorldActions( GameObject ) )
			return;

		if ( ShouldBlockCombatWhileBuilding() )
			return;

		if ( TryResolveActiveAmmoWeapon( out var combatId, out var def )
		     && ThornsWeaponDefinitions.IsBowWeapon( def, combatId ) )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcRequestReload();
		else
			HostTryStartReload();
	}

	[Rpc.Host]
	void RpcRequestReload()
	{
		if ( !ThornsNetAuthority.ValidateOwnerCaller( this ) )
			return;

		HostTryStartReload();
	}

	void HostTryStartReload()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || _hostReloadInProgress )
			return;

		if ( ThornsPlayerActionGate.BlocksHostWorldActions( GameObject ) )
			return;

		if ( ShouldBlockCombatWhileBuilding() )
			return;

		if ( !TryResolveActiveAmmoWeapon( out var combatId, out var def ) )
			return;

		if ( ThornsWeaponDefinitions.IsBowWeapon( def, combatId ) )
			return;

		var gameplay = Components.Get<ThornsPlayerGameplay>();
		if ( !gameplay.IsValid() || !gameplay.TryGetActiveHotbarIndex( out var hotbar ) )
			return;

		var stack = gameplay.GetHotbarSlot( hotbar );
		if ( stack.IsEmpty || stack.IsWeaponBroken( combatId ) )
			return;

		if ( stack.WeaponLoadedAmmo >= ThornsWeaponEffectiveStats.ResolveClipSize( def, combatId, stack ) )
			return;

		var reserve = ThornsInventoryWeaponState.CountAmmoInContainer( gameplay.Inventory, def.AmmoTypeId );
		if ( reserve <= 0 )
			return;

		_hostReloadInProgress = true;
		_hostReloadHotbar = hotbar;
		MirrorReloading = true;
		_hostReloadCompleteTime = Time.Now + ThornsWeaponDefinitions.ShellReloadGameplayGateSeconds( def, combatId );
		PushWeaponHud( gameplay, hotbar, stack, combatId, def );
		GameObject.Components.Get<ThornsFpPresentation>()?.NotifyReloadPresentation( def.ReloadTimeSeconds );
		PlayReloadSfx( combatId );
	}

	[Rpc.Host]
	void RpcRequestBowNock()
	{
		if ( !ThornsNetAuthority.ValidateOwnerCaller( this ) )
			return;

		HostTryNockBowArrow();
	}

	void HostTryNockBowArrow()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || _hostReloadInProgress )
			return;

		if ( ThornsPlayerActionGate.BlocksHostWorldActions( GameObject ) )
			return;

		if ( ShouldBlockCombatWhileBuilding() )
			return;

		if ( !TryResolveActiveAmmoWeapon( out var combatId, out var def ) )
			return;

		if ( !ThornsWeaponDefinitions.IsBowWeapon( def, combatId ) )
			return;

		var gameplay = Components.Get<ThornsPlayerGameplay>();
		if ( !gameplay.IsValid() || !gameplay.TryGetActiveHotbarIndex( out var hotbar ) )
			return;

		var stack = gameplay.GetHotbarSlot( hotbar );
		if ( stack.IsEmpty || stack.IsWeaponBroken( combatId ) )
			return;

		if ( stack.WeaponLoadedAmmo >= ThornsWeaponEffectiveStats.ResolveClipSize( def, combatId, stack ) )
			return;

		var reserve = ThornsInventoryWeaponState.CountAmmoInContainer( gameplay.Inventory, def.AmmoTypeId );
		if ( reserve <= 0 )
			return;

		stack.WeaponLoadedAmmo++;
		gameplay.HostConsumeAmmoItems( def.AmmoTypeId, 1 );
		gameplay.SetHotbarSlot( hotbar, stack, pushInventory: false );
		PushWeaponHud( gameplay, hotbar, stack, combatId, def );
	}

	void CompleteReloadHost()
	{
		if ( !TryResolveActiveAmmoWeapon( out var combatId, out var def ) )
			return;

		var gameplay = Components.Get<ThornsPlayerGameplay>();
		if ( !gameplay.IsValid() || _hostReloadHotbar < 0 )
			return;

		var stack = gameplay.GetHotbarSlot( _hostReloadHotbar );
		if ( stack.IsEmpty )
			return;

		var reserve = ThornsInventoryWeaponState.CountAmmoInContainer( gameplay.Inventory, def.AmmoTypeId );
		if ( reserve <= 0 )
			return;

		var effectiveClip = ThornsWeaponEffectiveStats.ResolveClipSize( def, combatId, stack );
		var need = effectiveClip - stack.WeaponLoadedAmmo;
		if ( need <= 0 )
			return;

		var perShell = ThornsWeaponDefinitions.UsesPerShellReloadCycle( def, combatId )
			? Math.Max( 1, def.ReloadShellCountPerRpc )
			: need;

		var take = Math.Min( reserve, Math.Min( need, perShell ) );
		stack.WeaponLoadedAmmo += take;
		gameplay.HostConsumeAmmoItems( def.AmmoTypeId, take );
		gameplay.SetHotbarSlot( _hostReloadHotbar, stack, pushInventory: false );
		PushWeaponHud( gameplay, _hostReloadHotbar, stack, combatId, def );

		if ( ThornsWeaponDefinitions.UsesPerShellReloadCycle( def, combatId )
		     && stack.WeaponLoadedAmmo < ThornsWeaponEffectiveStats.ResolveClipSize( def, combatId, stack )
		     && ThornsInventoryWeaponState.CountAmmoInContainer( gameplay.Inventory, def.AmmoTypeId ) > 0 )
		{
			_hostReloadInProgress = true;
			MirrorReloading = true;
			_hostReloadCompleteTime = Time.Now + ThornsWeaponDefinitions.ShellReloadGameplayGateSeconds( def, combatId );
			PushWeaponHud( gameplay, _hostReloadHotbar, stack, combatId, def );
			GameObject.Components.Get<ThornsFpPresentation>()?.NotifyReloadPresentation( def.ReloadTimeSeconds );
			PlayReloadSfx( combatId );
		}
		else
		{
			gameplay.FlushThrottledInventorySyncIfDirty( force: true );
		}
	}

	public void HostRefreshHudFromActiveWeapon()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		if ( !TryResolveActiveAmmoWeapon( out var combatId, out var def ) )
			return;

		var gameplay = Components.Get<ThornsPlayerGameplay>();
		if ( !gameplay.IsValid() || !gameplay.TryGetActiveHotbarIndex( out var hotbar ) )
			return;

		var stack = gameplay.GetHotbarSlot( hotbar );
		_hostReloadInProgress = false;
		MirrorReloading = false;
		PushWeaponHud( gameplay, hotbar, stack, combatId, def );
	}

	void PushWeaponHud( ThornsPlayerGameplay gameplay, int hotbar, in ThornsItemStack stack, string combatId, ThornsWeaponDefinitions.WeaponDefinition def )
	{
		var loaded = stack.WeaponLoadedAmmo;
		var reserve = ThornsInventoryWeaponState.CountAmmoInContainer( gameplay.Inventory, def.AmmoTypeId );
		var broken = stack.IsWeaponBroken( combatId );

		MirrorLoadedAmmo = loaded;
		MirrorReserveAmmo = reserve;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcSyncWeaponHud( loaded, reserve, broken, MirrorReloading );

		if ( gameplay.IsValid() )
			gameplay.PatchOwnerHotbarWeaponUi( hotbar, stack, combatId, def );
	}

	[Rpc.Owner]
	void RpcSyncWeaponHud( int loaded, int reserve, bool broken, bool reloading )
	{
		MirrorLoadedAmmo = loaded;
		MirrorReserveAmmo = reserve;
		MirrorReloading = reloading;
	}

	bool TryGetActiveHotbarStack( out ThornsItemStack stack )
	{
		stack = default;
		var gameplay = Components.Get<ThornsPlayerGameplay>();
		if ( !gameplay.IsValid() || !gameplay.TryGetActiveHotbarIndex( out var hotbar ) )
			return false;

		stack = gameplay.GetHotbarSlot( hotbar );
		return !stack.IsEmpty;
	}

	bool TryResolveActiveAmmoWeapon( out string combatId, out ThornsWeaponDefinitions.WeaponDefinition def )
	{
		combatId = "";
		def = ThornsWeaponDefinitions.Get( "dev_placeholder" );

		var gameplay = Components.Get<ThornsPlayerGameplay>();
		if ( !gameplay.IsValid() || !gameplay.TryGetActiveHotbarItemId( out var itemId ) )
			return false;

		if ( !ThornsItemRegistry.TryGet( itemId, out var idef ) || idef.ItemType != ThornsItemType.Weapon )
			return false;

		combatId = ThornsInventoryWeaponState.ResolveCombatId( idef, itemId );
		def = ThornsWeaponDefinitions.Get( combatId );
		return !ThornsWeaponDefinitions.TreatsAsMeleeWeapon( def, combatId ) && def.ClipSize > 0;
	}

	bool TryResolveActiveFirearm( out string combatId, out ThornsWeaponDefinitions.WeaponDefinition def )
	{
		if ( !TryResolveActiveAmmoWeapon( out combatId, out def ) )
			return false;

		return TryResolveActiveFirearm( def, combatId );
	}

	static bool TryResolveActiveFirearm( ThornsWeaponDefinitions.WeaponDefinition def, string combatId ) =>
		!ThornsWeaponDefinitions.IsBowWeapon( def, combatId );

	bool IsLocallyControlled() => ThornsLocalPlayer.IsLocalConnectionOwner( this );

	bool IsPlacementModeActive()
	{
		var building = Components.Get<ThornsPlayerBuildingController>();
		return building.IsValid() && building.UsesPrimaryFireForPlacement;
	}

	bool IsBuildMenuOpen()
	{
		var building = Components.Get<ThornsPlayerBuildingController>();
		return building.IsValid() && building.BuildMenuOpen;
	}

	bool IsHotbarPlaceModeActive()
	{
		var building = Components.Get<ThornsPlayerBuildingController>();
		return building.IsValid() && building.IsHotbarPlaceModeActive;
	}

	bool ShouldBlockCombatWhileBuilding() => IsBuildMenuOpen() || IsHotbarPlaceModeActive();

	void PlayReloadSfx( string combatId )
	{
		var path = ThornsGameplaySfx.ReloadSoundForCombatId( combatId );
		if ( !string.IsNullOrWhiteSpace( path ) )
		{
			ThornsGameplaySfx.PlayNetworkedCombatSound(
				GameObject,
				path,
				ThornsSpatialSfxCategory.PlayerReload );
		}
	}

}
