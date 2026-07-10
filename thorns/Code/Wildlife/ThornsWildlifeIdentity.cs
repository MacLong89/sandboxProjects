using System;
using System.Collections.Generic;

namespace Sandbox;

/// <summary>Species + tame owner / commands (synced for UI + follow behavior).</summary>
[Title( "Thorns — Wildlife identity" )]
[Category( "Thorns/Wildlife" )]
[Icon( "pets" )]
public sealed class ThornsWildlifeIdentity : Component, Component.INetworkSpawn
{
	public static readonly Dictionary<Guid, ThornsWildlifeIdentity> ActiveByHost = new();

	[Property]
	[Sync( SyncFlags.FromHost )]
	public ThornsWildlifeSpeciesKind Species { get; set; } = ThornsWildlifeSpeciesKind.Deer;

	/// <summary>World boss fauna — 10× HP, larger mesh/capsule, minimap POI; 10× kill XP, extra loot; tameable like normal wildlife (longer tame range).</summary>
	[Sync( SyncFlags.FromHost )] public bool IsBossWildlifeSync { get; set; }

	[Sync( SyncFlags.FromHost )] public string WildlifeIdSync { get; set; } = "";

	public Guid WildlifeId => SyncGuidParse( WildlifeIdSync );

	[Sync( SyncFlags.FromHost )] public string TameOwnerConnectionIdSync { get; set; } = "";

	// Persistent tame owner — remapped to fresh Connection.Id on join.
	[Sync( SyncFlags.FromHost )] public string TameOwnerAccountKeySync { get; set; } = "";

	public Guid TameOwnerConnectionId => SyncGuidParse( TameOwnerConnectionIdSync );

	public static bool HostCallerOwnsTame( Guid callerConnectionId, ThornsWildlifeIdentity wid )
	{
		if ( wid is null || !wid.IsValid() )
			return false;

		if ( wid.TameOwnerConnectionId == callerConnectionId )
			return true;

		var conn = Connection.Find( callerConnectionId );
		if ( conn is null )
			return false;

		var key = ThornsPersistenceIdentity.GetStableAccountKey( conn );
		return !string.IsNullOrEmpty( wid.TameOwnerAccountKeySync ) && wid.TameOwnerAccountKeySync == key;
	}

	public static bool HostTamesShareOwner( ThornsWildlifeIdentity a, ThornsWildlifeIdentity b )
	{
		if ( a is null || !a.IsValid() || b is null || !b.IsValid() )
			return false;

		if ( !a.HostIsTamed || !b.HostIsTamed )
			return false;

		if ( a.TameOwnerConnectionId != Guid.Empty && b.TameOwnerConnectionId != Guid.Empty
		     && a.TameOwnerConnectionId == b.TameOwnerConnectionId )
			return true;

		return !string.IsNullOrEmpty( a.TameOwnerAccountKeySync )
		       && a.TameOwnerAccountKeySync == b.TameOwnerAccountKeySync;
	}

	/// <summary>Host combat: pawn ownership vs tame sync (connection id + stable account key).</summary>
	public static bool HostPawnOwnsTame( ThornsPawn pawn, ThornsWildlifeIdentity wid )
	{
		if ( pawn is null || !pawn.IsValid() || wid is null || !wid.IsValid() )
			return false;

		var cid = pawn.OwnerConnection?.Id ?? pawn.GameObject.Network.OwnerId;
		return HostCallerOwnsTame( cid, wid );
	}

	/// <summary>Owner pawn or another tame owned by the same player — never a valid combat target for a bonded pet.</summary>
	public static bool HostIsOwnerOrOwnedAlly( ThornsWildlifeIdentity tame, GameObject candidate )
	{
		if ( tame is null || !tame.IsValid() || candidate is null || !candidate.IsValid() )
			return false;

		var pawn = candidate.Components.GetInAncestorsOrSelf<ThornsPawn>( true );
		if ( pawn.IsValid() && HostPawnOwnsTame( pawn, tame ) )
			return true;

		var ally = candidate.Components.GetInAncestorsOrSelf<ThornsWildlifeIdentity>( true );
		return ally.IsValid() && ally.HostIsTamed && HostTamesShareOwner( tame, ally );
	}

	public static bool TryFindByWildlifeId( Scene scene, Guid wildlifeId, out ThornsWildlifeIdentity wid )
	{
		wid = default;
		if ( wildlifeId == Guid.Empty )
			return false;

		if ( ActiveByHost.TryGetValue( wildlifeId, out wid ) && wid.IsValid() )
			return true;

		if ( scene is null || !scene.IsValid() )
			return false;

		foreach ( var cand in scene.GetAllComponents<ThornsWildlifeIdentity>() )
		{
			if ( !cand.IsValid() || cand.WildlifeId != wildlifeId )
				continue;

			wid = cand;
			return true;
		}

		return false;
	}

	/// <summary>
	/// Host-only: suppress damage between owners and their tames, and between tames that share an owner.
	/// Does not apply when <paramref name="attackerRoot"/> is invalid (environment / void).
	/// </summary>
	public static bool HostShouldSuppressTameFriendlyFire( GameObject victimRoot, GameObject attackerRoot )
	{
		if ( !victimRoot.IsValid() || !attackerRoot.IsValid() )
			return false;

		var victimWid = victimRoot.Components.GetInAncestorsOrSelf<ThornsWildlifeIdentity>( true );
		var atkWid = attackerRoot.Components.GetInAncestorsOrSelf<ThornsWildlifeIdentity>( true );

		var victimPawn = victimRoot.Components.GetInAncestorsOrSelf<ThornsPawn>( true );
		var atkPawn = attackerRoot.Components.GetInAncestorsOrSelf<ThornsPawn>( true );

		if ( victimWid.IsValid() && victimWid.HostIsTamed && atkWid.IsValid() && atkWid.HostIsTamed
		     && HostTamesShareOwner( atkWid, victimWid ) )
			return true;

		if ( victimWid.IsValid() && victimWid.HostIsTamed && atkPawn.IsValid() && HostPawnOwnsTame( atkPawn, victimWid ) )
			return true;

		if ( atkWid.IsValid() && atkWid.HostIsTamed && victimPawn.IsValid() && HostPawnOwnsTame( victimPawn, atkWid ) )
			return true;

		return false;
	}

	// When tamed: pursue owner root until within stop band.
	[Sync( SyncFlags.FromHost )] public bool TameFollowOwnerSync { get; set; } = true;

	/// <summary>When set, this connection id's pawn is parented to the tame for riding (host authoritative).</summary>
	[Sync( SyncFlags.FromHost )] public string TameRiderConnectionIdSync { get; set; } = "";

	public Guid TameRiderConnectionId => SyncGuidParse( TameRiderConnectionIdSync );

	/// <summary>Host-only: planar move wish from the rider (world XY, z ignored). Updated from throttled mount steer RPCs.</summary>
	public Vector3 HostMountSteerPlanar { get; set; }

	/// <summary>Host <see cref="Time.Now"/> when the last mount steer RPC was accepted — used to decay steer when packets stall.</summary>
	public double HostLastMountSteerReceiveTime { get; set; }

	/// <summary>Host <see cref="Time.Now"/> when this creature was last bonded via tame — blocks instant mount-from-E-release.</summary>
	public double HostBondedAtRealtime { get; set; }

	// Empty ⇒ UI falls back to species display name.
	[Sync( SyncFlags.FromHost )] public string TameDisplayNameSync { get; set; } = "";

	/// <summary>Same cumulative curve as players (<see cref="ThornsVitals.CumulativeXpToEnterLevel"/>); host-authoritative.</summary>
	[Sync( SyncFlags.FromHost )] public int TameTotalXp { get; set; }

	/// <summary>Granted when <see cref="TameTotalXp"/> crosses a level threshold; spend via tame UI upgrades.</summary>
	[Sync( SyncFlags.FromHost )] public int TameUnspentUpgradePoints { get; set; }

	/// <summary>Hp (0–7), dmg (8–15), spd (16–23) upgrade step counts — one sync field instead of three.</summary>
	[Sync( SyncFlags.FromHost )] public int TameUpgradeStepsPacked { get; set; }

	/// <summary>Three 10-bit affinities (0–1023 ≈ 0–1.0) — one sync field instead of three floats.</summary>
	[Sync( SyncFlags.FromHost )] public int TameAffinityPacked { get; set; }

	public int TameHpUpgradeSteps
	{
		get => TameUpgradeStepsPacked & 0xFF;
		set => TameUpgradeStepsPacked = ( TameUpgradeStepsPacked & ~0xFF ) | ( value & 0xFF );
	}

	public int TameDmgUpgradeSteps
	{
		get => ( TameUpgradeStepsPacked >> 8 ) & 0xFF;
		set => TameUpgradeStepsPacked = ( TameUpgradeStepsPacked & ~( 0xFF << 8 ) ) | ( ( value & 0xFF ) << 8 );
	}

	public int TameSpdUpgradeSteps
	{
		get => ( TameUpgradeStepsPacked >> 16 ) & 0xFF;
		set => TameUpgradeStepsPacked = ( TameUpgradeStepsPacked & ~( 0xFF << 16 ) ) | ( ( value & 0xFF ) << 16 );
	}

	/// <summary>Rolled once at tame — matches weapon/armor <see cref="ThornsLootRarity"/> tiers.</summary>
	[Sync( SyncFlags.FromHost )] public byte TameQualityTierSync { get; set; }

	const float AffinityQuant = 1f / 1023f;

	static int QuantizeAffinity( float value ) =>
		(int)Math.Clamp( MathF.Round( value / AffinityQuant ), 0, 1023 );

	public float TameAffinityHpSync
	{
		get => ( TameAffinityPacked & 0x3FF ) * AffinityQuant;
		set => TameAffinityPacked = ( TameAffinityPacked & ~0x3FF ) | QuantizeAffinity( value );
	}

	public float TameAffinityDmgSync
	{
		get => ( ( TameAffinityPacked >> 10 ) & 0x3FF ) * AffinityQuant;
		set => TameAffinityPacked = ( TameAffinityPacked & ~( 0x3FF << 10 ) ) | ( QuantizeAffinity( value ) << 10 );
	}

	public float TameAffinitySpdSync
	{
		get => ( ( TameAffinityPacked >> 20 ) & 0x3FF ) * AffinityQuant;
		set => TameAffinityPacked = ( TameAffinityPacked & ~( 0x3FF << 20 ) ) | ( QuantizeAffinity( value ) << 20 );
	}

	/// <summary>Only meaningful when <see cref="TameQualityTier"/> is Legendary.</summary>
	[Sync( SyncFlags.FromHost )] public byte TameLegendaryAbilitySync { get; set; }

	public ThornsLootRarity TameQualityTier =>
		(ThornsLootRarity)Math.Clamp( TameQualityTierSync, (byte)0, (byte)4 );

	public ThornsTameLegendaryAbility TameLegendaryAbility =>
		(ThornsTameLegendaryAbility)Math.Clamp( TameLegendaryAbilitySync, (byte)0, (byte)5 );

	bool _tameRegistryRegistered;
	Guid _tameRegistryOwnerConn;
	string _tameRegistryAccountKey = "";
	Guid _tameRegistryRiderConn;

	public ThornsWildlifeSpeciesDefinition Definition => ThornsWildlifeDefinitions.Get( Species );

	public bool HostIsTamed =>
		TameOwnerConnectionId != Guid.Empty || !string.IsNullOrEmpty( TameOwnerAccountKeySync );

	/// <summary>True when <see cref="ThornsHealth"/> on this root is missing, not alive, or in death state.</summary>
	public bool HostIsDead
	{
		get
		{
			var hp = Components.Get<ThornsHealth>();
			return !hp.IsValid() || !hp.IsAlive || hp.IsDeadState;
		}
	}

	public string EffectiveDisplayName =>
		string.IsNullOrWhiteSpace( TameDisplayNameSync ) ? Definition.DisplayName : TameDisplayNameSync.Trim();

	/// <summary>Uses the same level ladder as player characters (computed from synced <see cref="TameTotalXp"/>).</summary>
	public int ComputeTameLevel() => ThornsVitals.ComputeLevelFromTotalXp( TameTotalXp );

	public static float HealthMultiplierForLevel( int level ) =>
		1f + Math.Max( 0, level - 1 ) * 0.06f;

	public static float DamageMultiplierForLevel( int level ) =>
		1f + Math.Max( 0, level - 1 ) * 0.05f;

	public static float SpeedMultiplierForLevel( int level ) =>
		1f + Math.Max( 0, level - 1 ) * 0.03f;

	public const float UpgradeHpBonusPerStep = 0.07f;

	public const float UpgradeDmgBonusPerStep = 0.06f;

	public const float UpgradeSpdBonusPerStep = 0.05f;

	/// <summary>Level scaling × chosen Health upgrades × bloodline × legendary gift.</summary>
	public float GetEffectiveHealthMultiplier()
	{
		var lv = ComputeTameLevel();
		var core = HealthMultiplierForLevel( lv ) * ( 1f + Math.Max( 0, TameHpUpgradeSteps ) * UpgradeHpBonusPerStep );
		var aff = 1f + TameAffinityHpSync;
		return core * aff * ThornsTameLegendaryAbilityDefs.HealthMul( TameLegendaryAbility );
	}

	/// <summary>Level scaling × damage upgrades × bloodline × legendary gift.</summary>
	public float GetEffectiveDamageMultiplier() =>
		DamageMultiplierForLevel( ComputeTameLevel() )
		* ( 1f + Math.Max( 0, TameDmgUpgradeSteps ) * UpgradeDmgBonusPerStep )
		* ( 1f + TameAffinityDmgSync )
		* ThornsTameLegendaryAbilityDefs.DamageMul( TameLegendaryAbility );

	/// <summary>Level scaling × speed upgrades × bloodline × legendary gift.</summary>
	public float GetEffectiveSpeedMultiplier() =>
		SpeedMultiplierForLevel( ComputeTameLevel() )
		* ( 1f + Math.Max( 0, TameSpdUpgradeSteps ) * UpgradeSpdBonusPerStep )
		* ( 1f + TameAffinitySpdSync )
		* ThornsTameLegendaryAbilityDefs.SpeedMul( TameLegendaryAbility );

	/// <summary>Host-only: combat XP and leveling — reapplies max HP from species baseline × tame level.</summary>
	public void HostAddTameXp( int amount )
	{
		if ( !Networking.IsHost || amount <= 0 || !HostIsTamed )
			return;

		var levelBefore = ThornsVitals.ComputeLevelFromTotalXp( TameTotalXp );
		TameTotalXp += amount;
		var levelAfter = ThornsVitals.ComputeLevelFromTotalXp( TameTotalXp );
		if ( levelAfter > levelBefore )
			TameUnspentUpgradePoints += levelAfter - levelBefore;

		HostRefreshTameDerivedStatsFromXp();
		ThornsWorldPersistence.HostRefreshTamedWildlifeRuntimeCacheThrottled();
	}

	/// <summary>Host-only: recomputes max health from species baseline and tame level, preserving current HP fraction.</summary>
	public void HostRefreshTameDerivedStatsFromXp()
	{
		if ( !Networking.IsHost || !HostIsTamed )
			return;

		var hp = Components.Get<ThornsHealth>();
		if ( !hp.IsValid() )
			return;

		var def = Definition;
		var hpMul = GetEffectiveHealthMultiplier();

		var oldMax = hp.MaxHealth > 0.001f ? hp.MaxHealth : def.MaxHealth;
		var ratio = oldMax > 0.001f ? Math.Clamp( hp.CurrentHealth / oldMax, 0f, 1f ) : 1f;

		var newMax = def.MaxHealth * hpMul;
		hp.MaxHealth = newMax;
		hp.CurrentHealth = Math.Clamp( newMax * ratio, 1f, newMax );
	}

	static Guid SyncGuidParse( string s ) =>
		string.IsNullOrWhiteSpace( s ) ? Guid.Empty : (Guid.TryParse( s, out var g ) ? g : Guid.Empty);

	/// <summary>Call after <see cref="Species"/> is set (factory spawn).</summary>
	public void HostApplyDefinitionNow()
	{
		if ( !Networking.IsHost )
			return;

		var hp = Components.Get<ThornsHealth>();
		if ( !hp.IsValid() )
			return;

		if ( HostIsTamed )
			HostRefreshTameDerivedStatsFromXp();
		else
		{
			hp.MaxHealth = Definition.MaxHealth;
			hp.CurrentHealth = Definition.MaxHealth;
		}
	}

	public void OnNetworkSpawn( Connection owner )
	{
		if ( !Networking.IsActive )
			return;

		ThornsWildlifeSpawn.EnsureLocalCreatureVisual( GameObject, Species );
		HostRegisterInActiveLookupIfReady();
	}

	protected override void OnStart()
	{
		if ( Networking.IsActive )
			GameObject.NetworkMode = NetworkMode.Object;

		// Proxies often miss runtime-assigned vmdl until locally re-bound (same class of issue as loot crate materials).
		ThornsWildlifeSpawn.EnsureLocalCreatureVisual( GameObject, Species );

		if ( Networking.IsHost && string.IsNullOrWhiteSpace( WildlifeIdSync ) )
			WildlifeIdSync = Guid.NewGuid().ToString( "D" );

		HostRegisterInActiveLookupIfReady();
		RefreshTameRegistryMembership();
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsActive || !HostIsTamed )
			return;

		RefreshTameRegistryMembership();
	}

	void HostRegisterInActiveLookupIfReady()
	{
		var id = WildlifeId;
		if ( id == Guid.Empty )
			return;

		ActiveByHost[id] = this;
	}

	protected override void OnDestroy()
	{
		if ( Networking.IsHost && !string.IsNullOrEmpty( TameRiderConnectionIdSync ) )
			ThornsWildlifeMountHost.HostDismountRiderFromWildlife( this );

		var id = WildlifeId;
		if ( id != Guid.Empty && ActiveByHost.TryGetValue( id, out var existing ) && existing == this )
			ActiveByHost.Remove( id );

		if ( _tameRegistryRegistered )
		{
			ThornsWildlifeTameRegistry.Unregister( this );
			_tameRegistryRegistered = false;
		}
	}

	void RefreshTameRegistryMembership()
	{
		if ( !HostIsTamed )
		{
			if ( _tameRegistryRegistered )
			{
				ThornsWildlifeTameRegistry.Unregister( this );
				_tameRegistryRegistered = false;
			}

			return;
		}

		var conn = TameOwnerConnectionId;
		var acct = TameOwnerAccountKeySync ?? "";
		var rider = TameRiderConnectionId;
		var ownerChanged = !_tameRegistryRegistered
		                   || conn != _tameRegistryOwnerConn
		                   || acct != _tameRegistryAccountKey;
		var riderChanged = rider != _tameRegistryRiderConn;

		if ( ownerChanged )
		{
			if ( _tameRegistryRegistered )
				ThornsWildlifeTameRegistry.Unregister( this );

			ThornsWildlifeTameRegistry.Register( this );
			_tameRegistryRegistered = true;
			_tameRegistryOwnerConn = conn;
			_tameRegistryAccountKey = acct;
			_tameRegistryRiderConn = rider;
			return;
		}

		if ( riderChanged )
		{
			ThornsWildlifeTameRegistry.RefreshRiderIndex( this );
			_tameRegistryRiderConn = rider;
		}
	}

	internal void HostRefreshTameRegistryMembership() => RefreshTameRegistryMembership();
}
