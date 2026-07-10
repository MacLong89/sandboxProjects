#nullable disable

using System;
using System.Collections.Generic;

namespace Sandbox;

/// <summary>
/// Server-authoritative health on the pawn network root. Death crate strip, respawn, XP hooks (THORNS_EVERYTHING_DOCUMENT §3, §13).
/// Damage application must only occur on the host simulation.
/// </summary>
[Title( "Thorns — Health" )]
[Category( "Thorns" )]
[Icon( "favorite" )]
[Order( 50 )]
public sealed class ThornsHealth : Component, Component.INetworkSpawn
{
	[Property] public float MaxHealth { get; set; } = 100f;

	[Property] public float RespawnDelaySeconds { get; set; } = 2.5f;

	// After join/respawn: not used as wildlife/bandit spawn anchor for this many seconds (other players still can). Not combat immunity.
	[Property] public float NpcMobSpawnGraceSeconds { get; set; } = 60f;

	[Property] public bool DebugSpawnEmptyDeathCrate { get; set; }

	[Sync( SyncFlags.FromHost )] public float CurrentHealth { get; set; } = 100f;

	[Sync( SyncFlags.FromHost )] public bool IsDeadState { get; set; }

	public bool IsAlive => CurrentHealth > 0f;

	/// <summary>Host-only idempotency guard — runtime mirror is <see cref="IsDeadState"/> + health.</summary>
	bool _deathHandlingCommitted;

	/// <summary>Increments each death; stale delayed respawn completions abort (no duplicate respawn apply).</summary>
	uint _deathSequence;

	/// <summary>Host-only: <see cref="Time.Now"/> until which this pawn is excluded as a spawn anchor for wander fauna / bandits.</summary>
	double _hostNpcMobSpawnGraceUntil;

	float _damageFloaterPending;
	double _damageFloaterWindowStart = -1d;
	float _damageFloaterJitterX;
	float _damageFloaterJitterY;

	public void OnNetworkSpawn( Connection owner )
	{
		if ( !Networking.IsHost )
			return;

		if ( !Components.Get<ThornsPawn>( FindMode.EnabledInSelf ).IsValid() )
			return;

		HostRestartNpcMobSpawnGraceWindow();
	}

	/// <summary>Host: (re)start the post-spawn window where this pawn is not used as a procedural spawn anchor.</summary>
	public void HostRestartNpcMobSpawnGraceWindow()
	{
		if ( !Networking.IsHost )
			return;

		if ( !Components.Get<ThornsPawn>( FindMode.EnabledInSelf ).IsValid() )
			return;

		var sec = Math.Max( 1.0f, NpcMobSpawnGraceSeconds );
		_hostNpcMobSpawnGraceUntil = Time.Now + sec;
	}

	/// <summary>Host: true while this pawn is in the post-spawn window — used only to skip them as a procedural spawn anchor.</summary>
	public static bool HostPlayerRootHasNpcMobSpawnGrace( GameObject playerRoot )
	{
		if ( !Networking.IsHost || playerRoot is null || !playerRoot.IsValid() )
			return false;

		var hp = playerRoot.Components.Get<ThornsHealth>();
		if ( !hp.IsValid() )
			return false;

		return hp.HostIsPlayerUnderNpcMobSpawnGrace();
	}

	/// <summary>Host-only: alive player pawns while the post-spawn spawn-anchor cooldown is active.</summary>
	public bool HostIsPlayerUnderNpcMobSpawnGrace()
	{
		if ( !Networking.IsHost )
			return false;

		if ( !Components.Get<ThornsPawn>( FindMode.EnabledInSelf ).IsValid() )
			return false;

		if ( !IsAlive || IsDeadState )
			return false;

		return Time.Now < _hostNpcMobSpawnGraceUntil;
	}

	/// <summary>
	/// Host-only: pick a random living player from <paramref name="roots"/> to anchor procedural wildlife / wanderer spawns.
	/// Skips players in <see cref="HostIsPlayerUnderNpcMobSpawnGrace"/> so fresh spawns/respawns do not pull mobs off themselves.
	/// </summary>
	public static bool HostTryPickRandomNpcSpawnAnchorPlayer( IReadOnlyList<GameObject> roots, out GameObject anchor )
	{
		anchor = default;
		if ( !Networking.IsHost || roots is null || roots.Count == 0 )
			return false;

		var n = roots.Count;
		var start = Random.Shared.Next( n );
		for ( var k = 0; k < n; k++ )
		{
			var r = roots[( start + k ) % n];
			if ( r is null || !r.IsValid() )
				continue;

			var hp = r.Components.Get<ThornsHealth>();
			if ( hp.IsValid() && ( !hp.IsAlive || hp.IsDeadState ) )
				continue;

			if ( HostPlayerRootHasNpcMobSpawnGrace( r ) )
				continue;

			anchor = r;
			return true;
		}

		return false;
	}

	protected override void OnStart()
	{
		if ( Networking.IsHost )
			CurrentHealth = MaxHealth;
	}

	protected override void OnFixedUpdate()
	{
		if ( Networking.IsHost )
			HostFlushDamageFloater( force: false );

		if ( !Game.IsPlaying )
			return;

		var authoritative = !Networking.IsActive || Networking.IsHost;
		if ( !authoritative || !IsAlive )
			return;

		if ( !Components.Get<ThornsPawn>( FindMode.EnabledInSelf ).IsValid() )
			return;

		var killZ = ThornsGameManager.ResolveVoidKillPlaneWorldZ( Scene );
		if ( GameObject.WorldPosition.z < killZ )
		{
			TakeDamage( 999999f, new DamageContext
			{
				Kind = "void",
				Headshot = false,
				AttackerRoot = default
			} );
		}
	}

	/// <summary>Apply damage on the host only. Call only from server-validated combat (hitscan, melee, etc.). Returns whether this application dropped health through zero (kill).</summary>
	public bool TakeDamage( float amount, DamageContext context )
	{
		if ( !Networking.IsHost )
			return false;

		if ( amount <= 0f || !IsAlive )
			return false;

		if ( context.AttackerRoot.IsValid()
		     && ThornsWildlifeIdentity.HostShouldSuppressTameFriendlyFire( GameObject, context.AttackerRoot ) )
			return false;

		if ( context.AttackerRoot.IsValid()
		     && ThornsGuildCombat.HostShouldSuppressGuildFriendlyFire( GameObject, context.AttackerRoot ) )
			return false;

		if ( context.AttackerRoot.IsValid()
		     && ThornsSharedHostHitscan.IsDirectCombatDamageKind( context.Kind )
		     && !context.CombatLosVerified
		     && !ThornsSharedHostHitscan.CombatDamageHasClearLineOfSight( context.AttackerRoot, GameObject ) )
			return false;

		if ( context.AttackerRoot.IsValid() )
		{
			var victimBrain = GameObject.Components.GetInAncestorsOrSelf<ThornsWildlifeBrain>( true );
			if ( victimBrain.IsValid() )
				victimBrain.HostNotifyDamagedByHostile( context.AttackerRoot );

			var victimBandit = GameObject.Components.GetInAncestorsOrSelf<ThornsBanditBrain>( true );
			if ( victimBandit.IsValid() )
				victimBandit.HostNotifyDamagedByHostile( context.AttackerRoot );
		}

		ThornsMusicWorldSignals.HostNotifyPlayerDamaged( GameObject );

		var hpBefore = CurrentHealth;
		var damageToHealth = amount;
		var armorEq = Components.Get<ThornsArmorEquipment>();
		if ( armorEq.IsValid() )
		{
			damageToHealth = armorEq.HostComputeMitigatedDamage( amount, out _, out _ );
			armorEq.HostApplyArmorDurabilityStub( damageToHealth );
		}

		var victimUps = Components.Get<ThornsPlayerUpgrades>();
		damageToHealth *= ThornsPlayerUpgrades.HostGetDamageMultiplierAfterArmor( victimUps, context );

		CurrentHealth = Math.Max( 0f, CurrentHealth - damageToHealth );

		if ( damageToHealth > 0.001f && context.AttackerRoot.IsValid() && CurrentHealth > 0.001f && MaxHealth > 0.01f )
		{
			var wid = GameObject.Components.GetInAncestorsOrSelf<ThornsWildlifeIdentity>( true );
			if ( wid.IsValid() && !wid.HostIsTamed )
			{
				var max = MaxHealth;
				var beforeFrac = hpBefore / max;
				var afterFrac = CurrentHealth / max;
				var stun = ThornsWildlifeMotor.TamingStunHealthFraction;
				var eps = ThornsWildlifeTamingRules.ThresholdEpsilon;
				if ( beforeFrac > stun + eps && afterFrac <= stun + eps )
				{
					var atkRoot = ThornsTameHostIntel.HostResolveCombatChaseRoot( context.AttackerRoot );
					if ( atkRoot is not null && atkRoot.IsValid() && atkRoot.Components.Get<ThornsPawn>().IsValid() )
					{
						ThornsGameShell.HostPushTameStunBannerForPawnRoot(
							atkRoot,
							"You've just stunned an animal.",
							"Hold Use (E) on the creature to tame it!",
							4.2f );
					}
				}
			}
		}

		if ( damageToHealth > 0.001f && context.AttackerRoot.IsValid() )
		{
			var atkRoot = ThornsTameHostIntel.HostResolveCombatChaseRoot( context.AttackerRoot );

			var victimPawn = Components.Get<ThornsPawn>();
			if ( victimPawn.IsValid() )
				ThornsTameHostIntel.HostNotifyOwnerThreatened( victimPawn.GameObject, atkRoot );

			var attackerPawn = context.AttackerRoot.Components.GetInAncestorsOrSelf<ThornsPawn>( true );
			if ( attackerPawn.IsValid() )
			{
				var victimChase = ThornsTameHostIntel.HostResolveCombatChaseRoot( GameObject );
				ThornsTameHostIntel.HostNotifyOwnerDamagedTarget( attackerPawn.GameObject, victimChase );
			}
		}

		if ( damageToHealth > 0.001f )
			HostQueueDamageFloater( damageToHealth );

		RpcDamagedNotify( CurrentHealth, damageToHealth );

		var killingBlow = hpBefore > 0f && CurrentHealth <= 0f;
		if ( killingBlow )
			HostFlushDamageFloater( force: true );

		if ( CurrentHealth <= 0f )
			Die( context );

		return killingBlow;
	}

	void Die( DamageContext context )
	{
		if ( !Networking.IsHost )
			return;

		if ( _deathHandlingCommitted )
		{
			Log.Info( $"[Thorns] Death already handled — ignored: '{GameObject.Name}'" );
			return;
		}

		_deathHandlingCommitted = true;
		IsDeadState = true;

		HostTryAwardPvpKillXpToAttacker( context );
		HostTryAwardTameKillXpToAttacker( context );
		HostTryAwardCreatureKillXpToPlayerAttacker( context );
		HostTryRecordMilestoneKillOnAttacker( context );

		if ( HostIsNonPlayerDisposableCombatant( GameObject ) )
		{
			Log.Info( $"[Thorns] Death triggered: '{GameObject.Name}', last attacker={(context.AttackerRoot.IsValid() ? context.AttackerRoot.Name : "none")}" );

			var wid = GameObject.Components.GetInAncestorsOrSelf<ThornsWildlifeIdentity>( true );
			if ( wid.IsValid() )
			{
				ThornsWildlifeMountHost.HostDismountRiderFromWildlife( wid );
				ThornsWildlifeLoot.HostTrySpawnLootCrateOnWildlifeKill( GameObject.Scene, GameObject.WorldPosition, wid );
				ThornsWildlifeDeathHost.HostBeginDespawn( GameObject );
				Log.Info( $"[Thorns] Wildlife death — corpse despawn scheduled '{GameObject.Name}'" );
				return;
			}

			var bandit = GameObject.Components.GetInAncestorsOrSelf<ThornsBanditBrain>( true );
			if ( bandit.IsValid() && bandit.SpawnGuardLootCrateOnDeath )
				ThornsAirdropGuardLoot.HostTrySpawnLootCrateOnGuardKill( GameObject.Scene, GameObject.WorldPosition, bandit );

			Log.Info( $"[Thorns] Destroying deceased NPC '{GameObject.Name}' (wildlife/bandit)" );
			GameObject.Destroy();
			return;
		}

		var inv = Components.Get<ThornsInventory>();
		if ( inv.IsValid() )
			inv.HostCancelPendingConsumableUse();

		var armorEq = Components.Get<ThornsArmorEquipment>();
		var hotbar = Components.Get<ThornsHotbarEquipment>();
		var progression = Components.Get<ThornsCharacterProgression>();

		var gridClone = inv.IsValid() ? inv.HostCloneAllSlotsForDeath() : new ThornsInventorySlot[ThornsInventory.TotalSlots];
		var armorClone = armorEq.IsValid() ? armorEq.HostCloneEquippedForDeath() : new ThornsEquippedArmorPiece[3];
		if ( armorClone.Length != 3 )
		{
			var a = new ThornsEquippedArmorPiece[3];
			Array.Copy( armorClone, a, Math.Min( 3, armorClone.Length ) );
			armorClone = a;
		}

		var invNonEmpty = CountNonEmptyInventorySlots( gridClone );
		var armorNonEmpty = CountNonEmptyArmorPieces( armorClone );
		Log.Info( $"[Thorns] Inventory serialized count (non-empty slots)={invNonEmpty}" );
		Log.Info( $"[Thorns] Armor serialized count (non-empty pieces)={armorNonEmpty}" );

		var shouldSpawnCrate = invNonEmpty + armorNonEmpty > 0 || DebugSpawnEmptyDeathCrate;
		if ( shouldSpawnCrate )
		{
			var dropPos = GameObject.WorldPosition + Vector3.Up * 8f;
			var crate = ThornsDeathCrate.SpawnHost( GameObject.Scene, dropPos, gridClone, armorClone );
			if ( crate is null )
				Log.Error( "[Thorns] Death crate spawn returned null on host — strip still applied (investigate NetworkSpawn / scene)" );
			else
				Log.Info( "[Thorns] Death crate spawned (host)" );
		}
		else
		{
			Log.Info( "[Thorns] No death crate spawned (nothing to drop; debug empty off)" );
		}

		if ( inv.IsValid() )
		{
			inv.ServerClearInventory();
			Log.Info( "[Thorns] Player inventory cleared after death (host)" );
		}

		if ( armorEq.IsValid() )
			armorEq.HostStripAllEquippedForDeath();

		if ( hotbar.IsValid() )
			hotbar.HostClearEquipmentAfterDeath();

		if ( progression.IsValid() )
			progression.HostApplyDeathXpPlaceholderRule();

		RpcDeathNotify();

		_deathSequence++;
		var deathToken = _deathSequence;
		_ = HostRespawnAfterDelayAsync( deathToken );
	}

	/// <summary>Player hits often pass the weapon root as <see cref="DamageContext.AttackerRoot"/> — walk ancestors for <see cref="ThornsPawn"/>.</summary>
	static ThornsPawn HostResolveKillerPawnFromAttackerRoot( GameObject attackerRoot ) =>
		attackerRoot.IsValid()
			? attackerRoot.Components.GetInAncestorsOrSelf<ThornsPawn>( true )
			: default;

	/// <summary>THORNS_EVERYTHING_DOCUMENT §death: player killer gains PvP XP chunk (host-only, no client RPC).</summary>
	void HostTryRecordMilestoneKillOnAttacker( DamageContext context )
	{
		if ( !context.AttackerRoot.IsValid() )
			return;

		var killerPawn = HostResolveKillerPawnFromAttackerRoot( context.AttackerRoot );
		if ( !killerPawn.IsValid() )
			return;

		var milestones = killerPawn.GameObject.Components.Get<ThornsPlayerMilestones>();
		if ( milestones.IsValid() )
			milestones.HostRecordKill( GameObject );
	}

	void HostTryAwardPvpKillXpToAttacker( DamageContext context )
	{
		if ( !context.AttackerRoot.IsValid() )
			return;

		var killerPawn = HostResolveKillerPawnFromAttackerRoot( context.AttackerRoot );
		var victimPawn = GameObject.Components.Get<ThornsPawn>( FindMode.EnabledInSelf );
		if ( !killerPawn.IsValid() || !victimPawn.IsValid() )
			return;

		if ( killerPawn.GameObject == victimPawn.GameObject )
			return;

		var killerVitals = killerPawn.GameObject.Components.Get<ThornsVitals>();
		if ( !killerVitals.IsValid() )
			return;

		killerVitals.AddXp( ThornsXpBalance.PvpKillPlayerReward );
		Log.Info( $"[Thorns] PvP kill XP awarded killer='{killerPawn.GameObject.Name}' victim='{GameObject.Name}' +{ThornsXpBalance.PvpKillPlayerReward}" );
	}

	/// <summary>Tamed wildlife earns XP when it kills a non-player creature (host-only).</summary>
	void HostTryAwardTameKillXpToAttacker( DamageContext context )
	{
		if ( !context.AttackerRoot.IsValid() )
			return;

		if ( GameObject == context.AttackerRoot )
			return;

		var killerWid = context.AttackerRoot.Components.GetInAncestorsOrSelf<ThornsWildlifeIdentity>( true );
		if ( !killerWid.IsValid() || !killerWid.HostIsTamed )
			return;

		var victimPawn = Components.Get<ThornsPawn>( FindMode.EnabledInSelf );
		if ( victimPawn.IsValid() )
			return;

		var victimWid = Components.GetInAncestorsOrSelf<ThornsWildlifeIdentity>( true );
		if ( victimWid.IsValid() && victimWid.HostIsTamed
		                         && ThornsWildlifeIdentity.HostTamesShareOwner( killerWid, victimWid ) )
			return;

		var tameXp = ThornsXpBalance.TameKillCreatureReward;
		if ( victimWid.IsValid() && victimWid.IsBossWildlifeSync )
			tameXp *= ThornsXpBalance.BossWildlifeXpRewardMultiplier;

		killerWid.HostAddTameXp( tameXp );
	}

	/// <summary>Player earns combat XP for killing wildlife or bandits (host-only; not PvP).</summary>
	void HostTryAwardCreatureKillXpToPlayerAttacker( DamageContext context )
	{
		if ( !context.AttackerRoot.IsValid() )
			return;

		var killerPawn = HostResolveKillerPawnFromAttackerRoot( context.AttackerRoot );
		if ( !killerPawn.IsValid() )
			return;

		if ( Components.Get<ThornsPawn>( FindMode.EnabledInSelf ).IsValid() )
			return;

		if ( !HostIsNonPlayerDisposableCombatant( GameObject ) )
			return;

		var killerVitals = killerPawn.GameObject.Components.Get<ThornsVitals>();
		if ( !killerVitals.IsValid() )
			return;

		var wildlife = Components.GetInAncestorsOrSelf<ThornsWildlifeIdentity>( true );
		var bandit = Components.GetInAncestorsOrSelf<ThornsBanditBrain>( true );
		var reward = wildlife.IsValid()
			? ThornsXpBalance.WildlifeKillReward
			: bandit.IsValid() && bandit.AwardWildlifeKillXp
				? ThornsXpBalance.WildlifeKillReward
				: ThornsXpBalance.BanditKillReward;

		if ( wildlife.IsValid() && wildlife.IsBossWildlifeSync )
			reward *= ThornsXpBalance.BossWildlifeXpRewardMultiplier;

		killerVitals.AddXp( reward );
	}

	async Task HostRespawnAfterDelayAsync( uint deathToken )
	{
		await Task.DelayRealtimeSeconds( RespawnDelaySeconds );

		if ( !Networking.IsHost || !GameObject.IsValid() )
			return;

		if ( deathToken != _deathSequence )
		{
			Log.Info( $"[Thorns] Respawn aborted (superseded death sequence): token={deathToken}, current={_deathSequence}" );
			return;
		}

		ApplyRespawnHost();
	}

	/// <summary>Environmental / starvation — bypasses armor (THORNS_EVERYTHING_DOCUMENT §vitals).</summary>
	public void HostApplyEnvironmentalDamage( float amount, string kind )
	{
		if ( !Networking.IsHost || amount <= 0f || !IsAlive )
			return;

		var ups = Components.Get<ThornsPlayerUpgrades>();
		if ( ups.IsValid() && ups.WeatheredRank > 0 )
			amount *= Math.Max(
				0.35f,
				1f - ups.WeatheredRank * ThornsUpgradeBalance.WeatheredEnvironmentalDamageReductionPerRank );

		CurrentHealth = Math.Max( 0f, CurrentHealth - amount );
		if ( kind != "starvation_or_thirst" )
			Log.Info( $"[Thorns] Environmental damage kind={kind} amount={amount:F1} hp={CurrentHealth:F1}/{MaxHealth:F1} pawn='{GameObject.Name}'" );

		if ( amount > 0.001f )
		{
			var jx = Random.Shared.NextSingle() * 14f - 7f;
			var jy = Random.Shared.NextSingle() * 14f - 7f;
			RpcDamageFloater( amount, jx, jy );
		}

		RpcDamagedNotify( CurrentHealth, amount );

		if ( CurrentHealth <= 0f )
			Die( new DamageContext { Kind = kind } );
	}

	/// <summary>Passive regen when vitals allow (caller decides cadence). Bypasses armor.</summary>
	public void HostApplyPassiveRegen( float amount )
	{
		if ( !Networking.IsHost || amount <= 0f || !IsAlive )
			return;

		CurrentHealth = Math.Min( MaxHealth, CurrentHealth + amount );
	}

	/// <summary>Medical consumables — host-only, clamps to max, bypasses armor.</summary>
	public void HostApplyHealing( float amount, string sourceKind )
	{
		if ( !Networking.IsHost || amount <= 0f || !IsAlive )
			return;

		var before = CurrentHealth;
		CurrentHealth = Math.Min( MaxHealth, CurrentHealth + amount );
		var gained = CurrentHealth - before;
		if ( gained > 0f )
			Log.Info( $"[Thorns] Healing applied kind={sourceKind} +{gained:F1} hp → {CurrentHealth:F1}/{MaxHealth:F1}" );
	}

	/// <summary>Host-only: teleport + reset death flags (THORNS doc — manual respawn UI may call this).</summary>
	void ApplyRespawnHost()
	{
		if ( !Networking.IsHost )
			return;

		CurrentHealth = MaxHealth;
		IsDeadState = false;
		_deathHandlingCommitted = false;

		var prePos = GameObject.WorldPosition;
		Transform spawnTf;
		if ( !ThornsPlayerBedSpawn.HostTryGetRespawnTransform( GameObject.Scene, GameObject, out spawnTf ) )
			spawnTf = ThornsGameManager.ResolveSafeRespawnTransformForPawn( GameObject.Scene, GameObject, prePos );
		GameObject.WorldPosition = spawnTf.Position;
		GameObject.WorldRotation = spawnTf.Rotation;

		var vitals = Components.Get<ThornsVitals>();
		if ( vitals.IsValid() )
			vitals.HostResetAfterRespawn();

		var move = Components.Get<ThornsPawnMovement>();
		if ( move.IsValid() )
			move.HostApplyRespawnSnap();

		var inv = Components.Get<ThornsInventory>();
		if ( inv.IsValid() )
		{
			inv.ServerApplyEmptyPlayerLoadout();
			inv.HostPushInventorySnapshotToOwner();
		}

		HostRestartNpcMobSpawnGraceWindow();

		Log.Info( $"[Thorns] Respawn completed: '{GameObject.Name}' at pos={spawnTf.Position}" );
	}

	/// <summary>Debug UI only — intent RPC; host validates death state (THORNS_EVERYTHING_DOCUMENT §spawn rules stub).</summary>
	[Rpc.Host]
	public void RequestRespawnFromDebugUi()
	{
		Log.Info( "[Thorns] UI: respawn request received (host)" );

		if ( !Networking.IsHost )
			return;

		if ( !ValidateRpcCallerOwnsPawnForUi() )
		{
			Log.Warning( "[Thorns] UI respawn rejected: not owner" );
			return;
		}

		if ( !IsDeadState )
		{
			Log.Info( "[Thorns] UI respawn rejected: not dead" );
			return;
		}

		_deathSequence++;
		ApplyRespawnHost();
	}

	bool ValidateRpcCallerOwnsPawnForUi() => ThornsPawn.ValidateHostRpcCallerOwnsPawnRoot( GameObject );

	/// <summary>Wildlife and bandits use <see cref="ThornsHealth"/> without <see cref="ThornsPawn"/> — remove the entity on death instead of respawn.</summary>
	static bool HostIsNonPlayerDisposableCombatant( GameObject root )
	{
		if ( !root.IsValid() )
			return false;
		if ( root.Components.Get<ThornsPawn>( FindMode.EnabledInSelf ).IsValid() )
			return false;

		return root.Components.GetInAncestorsOrSelf<ThornsWildlifeIdentity>( true ).IsValid()
		       || root.Components.GetInAncestorsOrSelf<ThornsBanditBrain>( true ).IsValid();
	}

	static int CountNonEmptyInventorySlots( ThornsInventorySlot[] grid )
	{
		if ( grid is null )
			return 0;
		var n = 0;
		for ( var i = 0; i < grid.Length; i++ )
		{
			if ( !grid[i].IsEmpty )
				n++;
		}

		return n;
	}

	static int CountNonEmptyArmorPieces( ThornsEquippedArmorPiece[] pieces )
	{
		if ( pieces is null )
			return 0;
		var n = 0;
		for ( var i = 0; i < pieces.Length; i++ )
		{
			if ( !pieces[i].IsEmpty )
				n++;
		}

		return n;
	}

	void HostQueueDamageFloater( float damageAmount )
	{
		if ( damageAmount <= 0.001f )
			return;

		_damageFloaterPending += damageAmount;
		if ( _damageFloaterWindowStart < 0d )
		{
			_damageFloaterWindowStart = Time.Now;
			_damageFloaterJitterX = Random.Shared.NextSingle() * 14f - 7f;
			_damageFloaterJitterY = Random.Shared.NextSingle() * 14f - 7f;
		}
	}

	void HostFlushDamageFloater( bool force )
	{
		if ( _damageFloaterPending <= 0.001f )
			return;

		var coalesceSec = 1f / MathF.Max( 4f, ThornsPerformanceBudgets.DamageFloaterMaxBroadcastHz );
		if ( !force && _damageFloaterWindowStart >= 0d && Time.Now - _damageFloaterWindowStart < coalesceSec )
			return;

		RpcDamageFloater( _damageFloaterPending, _damageFloaterJitterX, _damageFloaterJitterY );
		_damageFloaterPending = 0f;
		_damageFloaterWindowStart = -1d;
	}

	[Rpc.Broadcast]
	void RpcDamageFloater( float damageAmount, float jitterX, float jitterY )
	{
		ThornsDamageFloaterWorld.Spawn( GameObject, damageAmount, jitterX, jitterY );
	}

	[Rpc.Owner]
	void RpcDamagedNotify( float healthAfter, float lastDamage )
	{
		var root = GameObject;
		var shell = root.Components.GetInAncestorsOrSelf<ThornsGameShell>( true );
		if ( shell is { IsValid: true, Enabled: true } )
		{
			shell.NotifyLocalDamageVignette( lastDamage, healthAfter );
			return;
		}

		var hud = root.Components.GetInAncestorsOrSelf<ThornsDebugHudHost>( true );
		if ( hud is { IsValid: true, Enabled: true } )
			hud.NotifyLocalDamageVignette( lastDamage, healthAfter );
	}

	[Rpc.Owner]
	void RpcDeathNotify()
	{
		Log.Warning( $"[Thorns] (local) You died." );

		var root = GameObject;
		var mm = root.Components.GetInAncestorsOrSelf<ThornsMinimapHud>( true );
		if ( mm is { IsValid: true, Enabled: true } )
			mm.NotifyMostRecentDeathForMinimap( root.WorldPosition );
	}
}

/// <summary>Extensible damage metadata for future armor DR, hit zone, weapon rolls (THORNS_EVERYTHING_DOCUMENT §3).</summary>
public readonly struct DamageContext
{
	public GameObject AttackerRoot { get; init; }
	public bool Headshot { get; init; }
	/// <summary>Rolled player-weapon crit vs eligible targets — same damage multiplier tier as <see cref="Headshot"/> (see <see cref="ThornsWeaponDefinitions.WeaponDefinition.HeadshotMultiplier"/>).</summary>
	public bool CriticalHit { get; init; }
	public string Kind { get; init; }
	/// <summary>Host hitscan already confirmed LOS — skip duplicate trace in <see cref="ThornsHealth.TakeDamage"/>.</summary>
	public bool CombatLosVerified { get; init; }
}
