namespace Terraingen.Combat;

using Sandbox;
using Sandbox.Network;
using Terraingen;
using Terraingen.AI;
using Terraingen.Animals;
using Terraingen.Buildings;
using Terraingen.Foliage;
using Terraingen.GameData;
using Terraingen.Minerals;
using Terraingen.Player;
using Terraingen.Multiplayer;

/// <summary>Minimal owner-input, host-authoritative animal hitscan for the terrain prototype.</summary>
[Title( "Thorns Player Animal Hitscan" )]
[Category( "Player" )]
public sealed class ThornsPlayerAnimalHitscan : Component
{
	[Property] public float Damage { get; set; } = 10f;
	[Property] public float MaxRange { get; set; } = 12000f;
	[Property] public float FireIntervalSeconds { get; set; } = 0.18f;

	[ConVar( "animal_hitscan_debug" )]
	public static bool Debug { get; set; }

	double _nextHostFireTime;
	double _nextOwnerFirePresentationTime;

	protected override void OnUpdate()
	{
		if ( !IsLocallyControlled() )
			return;

		// AUDIT FIX: melee/tools shared Attack1 with inventory UI and must not fire while dead.
		if ( ThornsPlayerActionGate.BlocksLocalWorldActions( GameObject ) )
			return;

		if ( ThornsPlayerWeaponCombat.IsRangedWeaponEquipped( GameObject )
		     || ThornsPlayerBowCombat.IsBowEquipped( GameObject ) )
			return;

		if ( ThornsPlayerBuildingController.Local?.UsesPrimaryFireForPlacement == true )
			return;

		if ( !(Input.Pressed( "Attack1" ) || Input.Pressed( "attack1" )) )
			return;

		if ( !TryResolveFireRay( out var origin, out var direction ) )
		{
			if ( Debug )
				Log.Info( "[Thorns Hitscan] Attack1 ignored — could not resolve aim ray." );

			return;
		}

		var activeItemId = ResolveActiveItemId();
		var combatId = ResolveActiveCombatId( activeItemId );
		var wdef = ThornsWeaponDefinitions.Get( combatId );
		var fireInterval = Math.Max( 0.05f, wdef?.FireIntervalSeconds ?? FireIntervalSeconds );

		if ( !ShouldSkipPrimaryFirePresentation( origin, direction )
		     && Time.Now >= _nextOwnerFirePresentationTime
		     && ShouldPlayPrimaryFirePresentation( origin, direction, combatId ) )
		{
			_nextOwnerFirePresentationTime = Time.Now + fireInterval;
			ThornsViewModelController.TryPlayOwnerAttackPresentation( GameObject, activeItemId, combatId );
		}

		if ( Networking.IsActive && !Networking.IsHost )
			RpcRequestFire( origin, direction );
		else
			HostTryFire( origin, direction );
	}

	bool ShouldSkipPrimaryFirePresentation( Vector3 origin, Vector3 direction )
	{
		if ( ThornsAxeTools.PlayerHasAxeEquipped( GameObject )
		     && ThornsTreeHitUtil.TryPickTreeAlongRay( Scene, origin, direction, ThornsGatheringRange.Inches, GameObject, out _ ) )
			return true;

		return false;
	}

	bool ShouldPlayPrimaryFirePresentation( Vector3 origin, Vector3 direction, string combatId )
	{
		if ( ThornsPickaxeTools.PlayerHasPickaxeEquipped( GameObject )
		     || ThornsAxeTools.PlayerHasAxeEquipped( GameObject ) )
			return true;

		return ThornsFpToolCombat.TreatsAsMeleeWeapon( combatId );
	}

	bool IsLocallyControlled()
	{
		return ThornsLocalPlayer.IsLocalConnectionOwner( this );
	}

	bool TryResolveFireRay( out Vector3 origin, out Vector3 direction )
	{
		if ( ThornsSceneObserver.TryResolveLocalAimRay( GameObject, out origin, out direction, useScreenCenter: true ) )
			return true;

		var controller = GameObject.Components.Get<PlayerController>( FindMode.EverythingInSelf );
		if ( !controller.IsValid() )
			return false;

		origin = GameObject.WorldPosition + Vector3.Up * 64f;
		direction = controller.EyeAngles.ToRotation().Forward.Normal;
		return direction.Length >= 0.95f;
	}

	[Rpc.Host]
	void RpcRequestFire( Vector3 origin, Vector3 direction )
	{
		if ( !ThornsNetAuthority.ValidateOwnerCaller( this ) )
			return;

		HostTryFire( origin, direction );
	}

	void HostTryFire( Vector3 origin, Vector3 direction )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || Time.Now < _nextHostFireTime )
			return;

		// AUDIT FIX: host-side dead check (inventory already used HostIsDead; combat did not).
		if ( ThornsPlayerActionGate.BlocksHostWorldActions( GameObject ) )
			return;

		var activeItemId = ResolveActiveItemId();
		var combatId = ResolveActiveCombatId( activeItemId );
		var wdef = ThornsWeaponDefinitions.Get( combatId );
		var effectiveRange = ResolveHostHitscanMaxRange( combatId, wdef );

		if ( !ThornsCombatFireValidation.TryResolveAuthoritativeShot( GameObject, origin, direction, effectiveRange, out origin, out direction ) )
			return;

		if ( ThornsPickaxeTools.PlayerHasPickaxeEquipped( GameObject ) )
		{
			if ( TryMineNode( origin, direction, activeItemId ) )
			{
				_nextHostFireTime = Time.Now + Math.Max( 0.05f, wdef?.FireIntervalSeconds ?? FireIntervalSeconds );
				return;
			}
		}

		_nextHostFireTime = Time.Now + Math.Max( 0.05f, wdef?.FireIntervalSeconds ?? FireIntervalSeconds );

		if ( ThornsFpToolCombat.IsPunchCombatId( combatId ) && TrySalvageWithFists( origin, direction ) )
			return;

		if ( !ThornsCombatHitResolver.TryResolveVictimAlongRay( Scene, origin, direction, effectiveRange, GameObject, out var victim, out var victimKind, out var resolveTrace ) )
		{
			if ( ShouldSuppressMeleeMissForGatheringTarget( origin, direction ) )
				return;

			if ( Debug )
			{
				var end = origin + direction.Normal * Math.Min( 512f, effectiveRange );
				Log.Info( $"[Thorns Hitscan] Miss from {origin:F0} -> {end:F0} (live={ThornsAnimalManager.CountLiveAnimals()})." );
			}

			if ( ShouldPlayMeleeMissSfx( combatId ) )
				ThornsGameplaySfx.PlayMeleeMiss( GameObject );

			return;
		}

		var damage = Math.Max( 0f, wdef.BaseDamage );
		if ( ThornsFpToolCombat.IsPunchCombatId( combatId ) )
			damage = ThornsFpToolCombat.PunchBaseDamage;
		else
		{
			var gameplay = Components.Get<ThornsPlayerGameplay>();
			if ( gameplay.IsValid() && gameplay.TryGetActiveHotbarIndex( out var hotbar ) )
			{
				var stack = gameplay.GetHotbarSlot( hotbar );
				if ( !stack.IsEmpty && ThornsItemRegistry.TryGet( stack.ItemId, out var def ) )
					damage *= ThornsItemTier.ResolveStatMultiplier( stack, def );
			}
		}

		if ( damage <= 0f )
			return;

		ThornsCombatDamage.DamageResult result;
		if ( victimKind == ThornsCombatDamage.VictimKind.Animal )
		{
			var animal = victim.Components.Get<ThornsAnimalBrain>( FindMode.EverythingInSelfAndParent );
			if ( !animal.IsValid() || ThornsAnimalCombatRules.ShouldIgnoreDamage( animal, GameObject ) )
			{
				if ( Debug )
					Log.Info( $"[Thorns Hitscan] Ignored friendly tame {animal.GameObject.Name}." );

				return;
			}

			result = ThornsCombatDamage.HostApplyDamage(
				GameObject,
				animal.GameObject,
				ThornsCombatDamage.BuildAttackerWeaponHit(
					GameObject,
					animal.GameObject,
					damage,
					combatId,
					ThornsCombatDamage.VictimKind.Animal,
					direction ) );
			if ( result.Killed && !animal.IsTamed )
				Components.Get<ThornsPlayerGameplay>()?.HostNotifyWildlifeKill();

			ThornsAnimalCompanion.NotifyOwnerMarkedTarget( GameObject, animal.GameObject );
			if ( Debug )
				Log.Info( $"[Thorns Hitscan] Hit {animal.GameObject.Name} for {damage:F1} (killed={result.Killed})." );
		}
		else if ( victimKind == ThornsCombatDamage.VictimKind.Npc )
		{
			var bandit = victim.Components.Get<ThornsBanditBrain>( FindMode.EverythingInSelfAndParent );
			if ( !bandit.IsValid() )
				return;

			ThornsCitizenHitbox.TryClassifyCitizenHit( victim, origin, direction, MaxRange, resolveTrace, out var hitWorld, out var headshot );
			result = ThornsCombatDamage.HostApplyDamage(
				GameObject,
				victim,
				ThornsCombatDamage.BuildAttackerWeaponHit(
					GameObject,
					victim,
					damage,
					combatId,
					ThornsCombatDamage.VictimKind.Npc,
					direction,
					headshot,
					hitWorld,
					"melee" ) );

			ThornsAnimalCompanion.NotifyOwnerMarkedTarget( GameObject, bandit.GameObject );

			if ( Debug )
				Log.Info( $"[Thorns Hitscan] Hit bandit {victim.Name} for {damage:F1} (killed={result.Killed})." );
		}
		else if ( victimKind == ThornsCombatDamage.VictimKind.Player )
		{
			ThornsCitizenHitbox.TryClassifyCitizenHit( victim, origin, direction, MaxRange, resolveTrace, out var hitWorld, out var headshot );
			result = ThornsCombatDamage.HostApplyDamage( GameObject, victim,
				ThornsCombatDamage.BuildPlayerWeaponHit( GameObject, victim, damage, combatId, headshot, hitWorld, -direction.Normal ) );
			if ( Debug )
				Log.Info( $"[Thorns Hitscan] Hit player {victim.Name} for {damage:F1} (killed={result.Killed})." );
		}
		else
		{
			return;
		}

		var killed = result.Killed;

		Components.Get<ThornsPlayerGameplay>()?.PushCrosshairHitFeedbackToOwner( damage, killed );
		if ( ThornsPickaxeTools.PlayerHasPickaxeEquipped( GameObject ) || ThornsAxeTools.PlayerHasAxeEquipped( GameObject ) )
			PlayOwnerToolStrikeSfx( activeItemId );
		else
			NotifyOwnerMeleeContactSfx( combatId );
	}

	void NotifyOwnerMeleeContactSfx( string combatId )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !ShouldPlayMeleeContactSfx( combatId ) )
			return;

		ThornsGameplaySfx.PlayMeleeContact( GameObject );
	}

	static bool ShouldPlayMeleeMissSfx( string combatId )
	{
		var id = combatId?.Trim() ?? "";
		if ( !string.IsNullOrWhiteSpace( ThornsGameplaySfx.FireSoundForCombatId( id ) ) )
			return false;

		return ThornsFpToolCombat.TreatsAsMeleeWeapon( id ) || ThornsFpToolCombat.IsPunchCombatId( id );
	}

	static bool ShouldPlayMeleeContactSfx( string combatId )
	{
		var id = combatId?.Trim() ?? "";
		if ( string.Equals( id, "m9_bayonet", StringComparison.OrdinalIgnoreCase ) )
			return false;

		return ThornsFpToolCombat.IsPunchCombatId( id );
	}

	bool ShouldSuppressMeleeMissForGatheringTarget( Vector3 origin, Vector3 direction )
	{
		if ( ThornsAxeTools.PlayerHasAxeEquipped( GameObject )
		     && ThornsTreeHitUtil.TryPickTreeAlongRay( Scene, origin, direction, ThornsGatheringRange.Inches, GameObject, out _ ) )
			return true;

		if ( ThornsPickaxeTools.PlayerHasPickaxeEquipped( GameObject )
		     && ThornsMineralHitUtil.TryPickNodeAlongRay( Scene, origin, direction, ThornsGatheringRange.Inches, GameObject, out _ ) )
			return true;

		if ( ThornsGatherSalvage.TryResolveTarget( GameObject, origin, direction, out var kind, out _ )
		     && kind != ThornsGatherSalvage.SalvageTargetKind.None )
			return true;

		return false;
	}

	bool TrySalvageWithFists( Vector3 origin, Vector3 direction )
	{
		if ( !ThornsGatherSalvage.HostTrySalvage( GameObject, origin, direction, out var kind ) )
		{
			if ( ThornsGatherSalvage.Debug )
				Log.Info( "[Thorns Salvage] Fist swing: HostTrySalvage returned false." );

			return false;
		}

		Components.Get<ThornsPlayerGameplay>()?.PushCrosshairHitFeedbackToOwner();
		PlayOwnerSalvageStrikeSfx( kind );

		var sparkColor = kind switch
		{
			ThornsGatherSalvage.SalvageTargetKind.Tree => new Color( 0.86f, 0.58f, 0.18f ),
			ThornsGatherSalvage.SalvageTargetKind.Stone => new Color( 0.65f, 0.7f, 0.78f ),
			_ => Color.White
		};

		ThornsImpactSparkFx.Spawn( Scene, origin + direction * 48f, sparkColor );

		if ( Debug )
			Log.Info( $"[Thorns Hitscan] Fist salvage {kind}." );
		else if ( ThornsGatherSalvage.Debug )
			Log.Info( $"[Thorns Salvage] Fist strike applied: {kind}." );

		NotifyBareHandsGatherGoal( kind );

		return true;
	}

	void NotifyBareHandsGatherGoal( ThornsGatherSalvage.SalvageTargetKind kind )
	{
		var taskId = kind switch
		{
			ThornsGatherSalvage.SalvageTargetKind.Tree => "punch_wood",
			ThornsGatherSalvage.SalvageTargetKind.Stone => "punch_stone",
			_ => null
		};

		if ( string.IsNullOrWhiteSpace( taskId ) )
			return;

		Components.Get<ThornsPlayerGameplay>()?.HostCompleteJournalTask( "goal_bare_hands_gather", taskId );
	}

	bool TryMineNode( Vector3 origin, Vector3 direction, string activeItemId )
	{
		if ( !ThornsPickaxeTools.PlayerHasPickaxeEquipped( GameObject ) )
			return false;

		if ( !ThornsMineralHitUtil.TryPickNodeAlongRay( Scene, origin, direction, ThornsGatheringRange.Inches, GameObject, out var nodeId ) )
			return false;

		var service = ThornsMineralWorldService.ResolveInstance();
		if ( service is null || !service.IsValid() )
			return false;

		if ( !service.HostTryMine( GameObject, nodeId, origin, direction ) )
			return false;

		if ( Debug )
			Log.Info( $"[Thorns Hitscan] Mined node #{nodeId}." );

		Components.Get<ThornsPlayerGameplay>()?.PushCrosshairHitFeedbackToOwner();
		PlayOwnerToolStrikeSfx( activeItemId );
		ThornsImpactSparkFx.Spawn( Scene, origin + direction * 48f, new Color( 0.65f, 0.7f, 0.78f ) );

		return true;
	}

	string ResolveActiveItemId()
	{
		var gameplay = Components.Get<ThornsPlayerGameplay>();
		if ( gameplay.IsValid() && gameplay.TryGetActiveHotbarItemId( out var itemId ) && !string.IsNullOrWhiteSpace( itemId ) )
			return itemId.Trim();

		return ThornsAxeTools.TryGetEquippedItemId( GameObject, out itemId ) ? itemId.Trim() : "";
	}

	static string ResolveActiveCombatId( string activeItemId )
	{
		if ( string.IsNullOrWhiteSpace( activeItemId ) )
			return ThornsFpToolCombat.CombatIdBareHands;

		if ( !ThornsItemRegistry.TryGet( activeItemId.Trim(), out var def ) )
			return ThornsFpToolCombat.CombatIdPrimitive;

		if ( def.ItemType == ThornsItemType.Tool )
			return ThornsFpToolCombat.GetCombatDefinitionIdForToolItemId( activeItemId );

		if ( def.ItemType == ThornsItemType.Weapon )
			return string.IsNullOrWhiteSpace( def.CombatWeaponDefinitionId ) ? activeItemId.Trim() : def.CombatWeaponDefinitionId.Trim();

		return ThornsFpToolCombat.CombatIdBareHands;
	}

	static float ResolveHostHitscanMaxRange( string combatId, ThornsWeaponDefinitions.WeaponDefinition wdef )
	{
		if ( wdef is not null && wdef.MaxRange > 0f && wdef.MaxRange < 512f )
			return wdef.MaxRange;

		if ( ThornsFpToolCombat.TreatsAsMeleeWeapon( combatId ) || ThornsFpToolCombat.IsPunchCombatId( combatId ) )
			return ThornsGatheringRange.Inches + 64f;

		return Math.Min( 12000f, wdef?.MaxRange ?? 512f );
	}

	void PlayOwnerSalvageStrikeSfx( ThornsGatherSalvage.SalvageTargetKind kind )
	{
		if ( kind == ThornsGatherSalvage.SalvageTargetKind.None )
			return;

		// Gather contact audio must run on the owning client (host sim is silent for local FP).
		if ( ThornsLocalPlayer.IsLocallyControlledPawn( GameObject ) )
		{
			ThornsGameplaySfx.PlaySalvageStrikeSfx( GameObject, kind );
			return;
		}

		if ( Networking.IsActive )
			RpcOwnerPlaySalvageStrikeSfx( (int)kind );
	}

	void PlayOwnerToolStrikeSfx( string activeItemId )
	{
		if ( string.IsNullOrWhiteSpace( activeItemId ) )
			return;

		if ( ThornsLocalPlayer.IsLocallyControlledPawn( GameObject ) )
		{
			ThornsGameplaySfx.PlayToolStrikeForActiveItem( GameObject, activeItemId );
			return;
		}

		if ( Networking.IsActive )
			RpcOwnerPlayToolStrikeSfx( activeItemId );
	}

	[Rpc.Owner]
	void RpcOwnerPlaySalvageStrikeSfx( int kindInt )
	{
		if ( Rpc.Caller is not null && !ThornsNetAuthority.ValidateOwnerCaller( this ) )
			return;

		var kind = (ThornsGatherSalvage.SalvageTargetKind)kindInt;
		if ( kind == ThornsGatherSalvage.SalvageTargetKind.None )
			return;

		ThornsGameplaySfx.PlaySalvageStrikeSfx( GameObject, kind );
	}

	[Rpc.Owner]
	void RpcOwnerPlayToolStrikeSfx( string activeItemId )
	{
		if ( Rpc.Caller is not null && !ThornsNetAuthority.ValidateOwnerCaller( this ) )
			return;

		ThornsGameplaySfx.PlayToolStrikeForActiveItem( GameObject, activeItemId );
	}
}
