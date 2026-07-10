namespace Terraingen.Animals;

using Terraingen.Combat;
using Terraingen.Core;
using Terraingen.Buildings;
using Terraingen.Multiplayer;
using Terraingen.Progression;
using Terraingen.TerrainGen;

/// <summary>Staggered detection, player cache, navmesh bake gate, corpse cleanup.</summary>
[Title( "Thorns Animal Manager" )]
[Category( "Thorns/Animals" )]
[Icon( "pets" )]
public sealed class ThornsAnimalManager : Component
{
	public const float CorpseFallbackLifetimeSeconds = 2.4f;
	public const float CorpseDespawnBufferSeconds = 0.25f;
	public const float CorpseMinLifetimeSeconds = 0.85f;
	public const float CorpseMaxLifetimeSeconds = 6f;
	public const int MaxDetectionsPerFrame = 32;
	public const float VisualScale = 0.25f;
	public const float BaseAgentHeight = 64f;
	public const float BaseAgentRadius = 20f;

	[Property] public int MaxWorldAnimals { get; set; } = 12;

	// Max creatures per ambient encounter
	[Property]
	public int MaxCreaturesPerAmbientEncounter { get; set; } = 5;

	[Property] public bool IgnorePlayers { get; set; }

	[Property] public bool IgnoreAnimals { get; set; }

	// Seconds between ambient spawn rolls
	[Property]
	public float AmbientSpawnIntervalSeconds { get; set; } = 60f;

	// Chance per roll (0–1)
	[Property, Range( 0f, 1f )]
	public float AmbientSpawnChance { get; set; } = 0.5f;

	[Property] public float AmbientSpawnDistanceMin { get; set; } = 1800f;
	[Property] public float AmbientSpawnDistanceMax { get; set; } = 2400f;

	public static bool NavMeshReady { get; private set; }
	public static bool NavMeshUsableForAnimals { get; private set; }
	public static ThornsAnimalManager Instance { get; private set; }

	static readonly List<ThornsAnimalBrain> Animals = new( 128 );
	static uint _nextHerdGroupId = 1;
	static readonly List<ThornsAnimalBrain> LiveAnimals = new( 128 );
	static readonly List<ThornsAnimalSpawner> Spawners = new( 8 );

	internal static IReadOnlyList<GameObject> CachedPlayerRoots => ThornsPlayerRootCache.RootsReadOnly;

	internal static IReadOnlyList<ThornsAnimalBrain> AnimalRegistry => Animals;

	public static ThornsAnimalBrain TryGetByObjectId( Guid objectId )
	{
		if ( objectId == Guid.Empty )
			return null;

		for ( var i = 0; i < Animals.Count; i++ )
		{
			var brain = Animals[i];
			if ( brain.IsValid() && brain.GameObject.Id == objectId )
				return brain;
		}

		return null;
	}

	public static GameObject TryGetPlayerByAccountKey( Scene scene, string accountKey ) =>
		ThornsPlayerRootCache.TryGetByAccountKey( scene, accountKey );

	public static GameObject TryGetPlayerByObjectId( Scene scene, Guid objectId ) =>
		ThornsPlayerRootCache.TryGetByObjectId( scene, objectId );

	TimeUntil _nextPlayerRefresh;
	TimeUntil _nextCorpseSweep;
	TimeUntil _nextAmbientSpawnRoll;
	TimeUntil _nextAnimalLodRefresh;
	bool _navBakeStarted;
	bool _navBakeInProgress;
	double _navBakeRequestedAt;
	int _navCoverageRetries;
	bool _persistedTamesRestored;
	bool _ambientSpawnArmed;
	int _animalSimCursor;
	int _detectCursor;

	[Property] public bool BakeNavMeshOnWorldReady { get; set; } = true;

	// Half-size (inches) of nav bake around map center — avoids full-terrain Recast triangulation failures.
	[Property] public float NavBakeHalfExtent { get; set; } = 12000f;

	public static void Register( ThornsAnimalBrain brain )
	{
		if ( brain is null || Animals.Contains( brain ) )
			return;

		Animals.Add( brain );
		if ( brain.IsValid() && !brain.IsDead )
			LiveAnimals.Add( brain );
	}

	public static void Unregister( ThornsAnimalBrain brain )
	{
		if ( brain is null )
			return;

		Animals.Remove( brain );
		LiveAnimals.Remove( brain );
	}

	public static void NotifyAnimalDied( ThornsAnimalBrain brain ) => LiveAnimals.Remove( brain );

	public static int CountLiveAnimals()
	{
		var count = 0;
		for ( var i = 0; i < Animals.Count; i++ )
		{
			var brain = Animals[i];
			if ( brain.IsValid() && !brain.IsDead )
				count++;
		}

		return count;
	}

	public static int CountLiveTamed()
	{
		var count = 0;
		for ( var i = 0; i < Animals.Count; i++ )
		{
			var brain = Animals[i];
			if ( brain.IsValid() && !brain.IsDead && brain.IsTamed )
				count++;
		}

		return count;
	}

	public static bool PersistedTamesRestoreAttempted { get; private set; }
	public static bool PersistedTamesRestoreHadSuccess { get; private set; }

	internal static void ResetPersistedTameRestoreGate()
	{
		PersistedTamesRestoreAttempted = false;
		PersistedTamesRestoreHadSuccess = false;
	}

	internal static void NotifyTameRestoreHadSuccess() => PersistedTamesRestoreHadSuccess = true;

	public static bool CanSpawnMore()
	{
		var max = Instance?.MaxWorldAnimals ?? 15;
		return CountLiveAnimals() < max;
	}

	public static bool ShouldIgnorePlayers( ThornsAnimalSpeciesData species )
	{
		if ( Instance?.IgnorePlayers == true )
			return true;

		if ( species?.AttackPlayers == true )
			return false;

		if ( species is not null )
			return species.IgnorePlayers;

		return true;
	}

	public static int RemainingSpawnSlots()
	{
		var max = Instance?.MaxWorldAnimals ?? 15;
		return Math.Max( 0, max - CountLiveAnimals() );
	}

	internal static void RegisterSpawner( ThornsAnimalSpawner spawner )
	{
		if ( spawner is null || Spawners.Contains( spawner ) )
			return;

		Spawners.Add( spawner );
	}

	internal static void UnregisterSpawner( ThornsAnimalSpawner spawner )
	{
		if ( spawner is null )
			return;

		Spawners.Remove( spawner );
	}

	protected override void OnStart()
	{
		Instance = this;
		PersistedTamesRestoreAttempted = false;
		ThornsAnimalSpeciesRegistry.EnsureInitialized();
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}

	public void OnWorldReady( Terrain terrain, ThornsTerrainConfig config )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		ThornsAnimalSpeciesRegistry.EnsureInitialized();
		HostRestorePersistedTamesOnce();

		if ( BakeNavMeshOnWorldReady && !_navBakeStarted )
		{
			_navBakeStarted = true;
			BeginNavMeshBake( terrain );
		}
		else
		{
			NavMeshReady = true;
			LogNavMeshCoverage();
			NotifySpawners();
		}
	}

	/// <summary>Re-bake nav after player structures restore so paths avoid new colliders.</summary>
	public void RequestStructureAwareNavRebake( Terrain terrain )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !BakeNavMeshOnWorldReady )
			return;

		if ( !terrain.IsValid() || Scene?.NavMesh is null )
			return;

		BeginNavMeshBake( terrain );
	}

	void BeginNavMeshBake( Terrain terrain )
	{
		NavMeshReady = false;
		NavMeshUsableForAnimals = false;
		if ( !terrain.IsValid() || Scene?.NavMesh is null )
		{
			Log.Warning( "[Thorns Animals] NavMesh bake skipped — terrain or navmesh missing." );
			NavMeshUsableForAnimals = false;
			NavMeshReady = true;
			NotifySpawners();
			return;
		}

		var nav = Scene.NavMesh;
		nav.IsEnabled = true;
		nav.CustomBounds = true;
		var min = terrain.GameObject.WorldPosition;
		var fullMax = min + new Vector3( terrain.TerrainSize, terrain.TerrainSize, terrain.TerrainHeight );
		var bounds = BuildNavBakeBounds( min, fullMax );
		nav.Bounds = bounds;
		if ( ThornsAnimalDebug.Verbose )
			Log.Info( $"[Thorns Animals] Nav bake bounds {bounds.Mins} -> {bounds.Maxs} (subset of terrain)." );

		nav.RequestTilesGeneration( bounds );
		_navBakeRequestedAt = Time.Now;
		_navCoverageRetries = 0;
		_navBakeInProgress = true;
	}

	void TickNavMeshBake()
	{
		if ( !_navBakeInProgress || Scene?.NavMesh is null )
			return;

		if ( Scene.NavMesh.IsGenerating )
			return;

		if ( Time.Now - _navBakeRequestedAt < 0.75f )
			return;

		LogNavMeshCoverage();
		if ( !NavMeshUsableForAnimals && _navCoverageRetries++ < 90 )
			return;

		_navBakeInProgress = false;
		NavMeshReady = true;
		if ( !NavMeshUsableForAnimals )
			Log.Warning( "[Thorns Animals] Nav bake finished without usable animal nav; fallback movement remains active." );
		NotifySpawners();
	}

	static BBox BuildNavBakeBounds( Vector3 terrainMin, Vector3 terrainMax )
	{
		var extent = Instance?.NavBakeHalfExtent ?? 12000f;
		extent = Math.Clamp( extent, 4096f, 24000f );
		var center = (terrainMin + terrainMax) * 0.5f;
		center.z = terrainMin.z;
		var half = new Vector3( extent, extent, terrainMax.z - terrainMin.z );
		var bakeMin = Vector3.Max( terrainMin, center - half );
		var bakeMax = Vector3.Min( terrainMax, center + half );
		return new BBox( bakeMin, bakeMax );
	}

	void LogNavMeshCoverage()
	{
		var terrain = ThornsTerrainCache.Resolve( Scene );
		if ( !terrain.IsValid() )
		{
			NavMeshUsableForAnimals = false;
			return;
		}

		var min = terrain.GameObject.WorldPosition;
		var center = min + new Vector3( terrain.TerrainSize * 0.5f, terrain.TerrainSize * 0.5f, 0f );
		if ( ThornsAnimalWorldUtil.TrySnapToTerrain( terrain, center, out var snappedCenter ) )
			center = snappedCenter;

		var available = ThornsAnimalWorldUtil.IsNavMeshAvailableNear( Scene, center );
		NavMeshUsableForAnimals = available;

		if ( !available )
		{
			if ( _navCoverageRetries is 0 or 30 or 60 or 90 )
			{
				Log.Warning(
					$"[Thorns Animals] Nav mesh ready but no animal nav found near terrain center {center:F0}. " +
					$"enabled={Scene?.NavMesh?.IsEnabled == true}, generating={Scene?.NavMesh?.IsGenerating == true}" );
			}
			return;
		}

		if ( ThornsAnimalDebug.Verbose || ThornsSettlementTestSceneBootstrap.IsActive )
			Log.Info( $"[Thorns Animals] Nav mesh usable near terrain center {center:F0}." );
	}

	static void NotifySpawners()
	{
		ThornsAnimalSpeciesRegistry.EnsureInitialized();
		Instance?.ArmAmbientSpawnRolls();
		ThornsTameSummonUtil.HostSummonAllOwnedTamesNearPlayers( Instance?.Scene );
	}

	void ArmAmbientSpawnRolls()
	{
		if ( _ambientSpawnArmed )
			return;

		_ambientSpawnArmed = true;
		_nextAmbientSpawnRoll = AmbientSpawnIntervalSeconds;
	}

	void HostRestorePersistedTamesOnce()
	{
		if ( _persistedTamesRestored || Scene is null )
			return;

		_persistedTamesRestored = true;
		PersistedTamesRestoreAttempted = true;
		ThornsWorldTamePersistence.HostRestoreTames( Scene );
	}

	void TickAmbientWildlifeSpawn()
	{
		if ( Instance != this || !_ambientSpawnArmed || !NavMeshReady || Scene is null )
			return;

		if ( !_nextAmbientSpawnRoll )
			return;

		_nextAmbientSpawnRoll = AmbientSpawnIntervalSeconds;
		HostRollAmbientWildlifeSpawn();
	}

	void HostRollAmbientWildlifeSpawn()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		var players = ThornsPlayerRootCache.RootsReadOnly;
		if ( players.Count == 0 )
			RefreshPlayerRoots();

		players = ThornsPlayerRootCache.RootsReadOnly;
		if ( players.Count == 0 )
			return;

		if ( Game.Random.Float( 0f, 1f ) > AmbientSpawnChance )
		{
			if ( ThornsAnimalDebug.Verbose )
				Log.Info( "[Thorns Animals] Ambient spawn roll failed (no encounter this interval)." );

			return;
		}

		if ( !CanSpawnMore() )
		{
			if ( ThornsAnimalDebug.Verbose )
				Log.Info( "[Thorns Animals] Ambient spawn roll succeeded but world animal cap is full." );

			return;
		}

		var player = players[Game.Random.Int( 0, players.Count - 1 )];
		if ( !player.IsValid() )
			return;

		if ( !ThornsAnimalSpawnUtil.TryPickAmbientAnchorNearPlayer(
			     Scene,
			     player.WorldPosition,
			     AmbientSpawnDistanceMin,
			     AmbientSpawnDistanceMax,
			     AmbientSpawnDistanceMin,
			     out var anchor ) )
		{
			if ( ThornsAnimalDebug.Verbose )
				Log.Info( "[Thorns Animals] Ambient spawn skipped — no anchor far enough from players." );

			return;
		}

		if ( ThornsNewPlayerWildlifeGrace.IsWithinGrace( player )
		     || ThornsNewPlayerWildlifeGrace.ShouldBlockSpawnNear( anchor, AmbientSpawnDistanceMax ) )
		{
			if ( ThornsAnimalDebug.Verbose )
				Log.Info( "[Thorns Animals] Ambient spawn skipped — new player wildlife grace active." );

			return;
		}

		var spawned = HostSpawnAmbientEncounter( anchor, player.WorldPosition );

		if ( spawned > 0 )
		{
			Log.Info(
				$"[Thorns Animals] Ambient encounter spawned {spawned} creature(s) near '{player.Name}' at {anchor:F0} " +
				$"(roll {AmbientSpawnChance:P0}, interval {AmbientSpawnIntervalSeconds:F0}s)." );
		}
	}

	/// <summary>One encounter: wolf/deer group, solitary panther/moose, or predator/prey skirmish (capped).</summary>
	int HostSpawnAmbientEncounter( Vector3 anchor, Vector3 referencePlayerPosition )
	{
		var encounterCap = Math.Max( 1, MaxCreaturesPerAmbientEncounter );
		var minClearance = AmbientSpawnDistanceMin;

		if ( Game.Random.Float( 0f, 1f ) < 0.4f )
			return HostSpawnPredatorPreySkirmish( anchor, encounterCap, referencePlayerPosition, minClearance );

		var groupEncounter = Game.Random.Float( 0f, 1f ) < 0.5f;
		if ( groupEncounter )
		{
			var speciesKey = Game.Random.Float( 0f, 1f ) < 0.5f ? "deer" : "wolf";
			return HostSpawnSpeciesGroup( speciesKey, anchor, encounterCap, null, minClearance );
		}

		var solitaryKey = Game.Random.Float( 0f, 1f ) < 0.5f ? "panther" : "moose";
		return HostSpawnSpeciesSolitary( solitaryKey, anchor, minClearance );
	}

	int HostSpawnPredatorPreySkirmish(
		Vector3 anchor,
		int cap,
		Vector3 referencePlayerPosition,
		float minPlayerClearance )
	{
		var preyBudget = Math.Max( 1, cap / 2 );
		var predatorBudget = Math.Max( 1, cap - preyBudget );
		var preyBrains = new List<ThornsAnimalBrain>();
		var predatorBrains = new List<ThornsAnimalBrain>();
		var preySpawned = HostSpawnSpeciesGroup( "deer", anchor, preyBudget, preyBrains, minPlayerClearance );
		var awayFromPlayer = (anchor - referencePlayerPosition).WithZ( 0f );
		if ( awayFromPlayer.LengthSquared < 64f )
			awayFromPlayer = Vector3.Random.WithZ( 0f );
		else
			awayFromPlayer = awayFromPlayer.Normal;
		var predatorAnchor = anchor + awayFromPlayer * Game.Random.Float( 160f, 360f );
		var predatorSpawned = HostSpawnSpeciesGroup( "wolf", predatorAnchor, predatorBudget, predatorBrains, minPlayerClearance );
		HostKickstartPredatorPreySkirmish( predatorBrains, preyBrains );
		var total = preySpawned + predatorSpawned;

		if ( total > 0 )
		{
			Log.Info(
				$"[Thorns Animals] Predator/prey skirmish near {anchor:F0}: " +
				$"{predatorSpawned} wolf(s), {preySpawned} deer (cap {cap})." );
		}

		return total;
	}

	static void HostKickstartPredatorPreySkirmish( List<ThornsAnimalBrain> predators, List<ThornsAnimalBrain> prey )
	{
		if ( prey.Count == 0 || predators.Count == 0 )
			return;

		for ( var p = 0; p < predators.Count; p++ )
		{
			var predator = predators[p];
			if ( !predator.IsValid() || predator.IsDead || predator.IsTamed )
				continue;

			ThornsAnimalBrain nearestPrey = null;
			var nearestDistSq = float.MaxValue;
			var predatorPos = predator.GameObject.WorldPosition.WithZ( 0f );
			for ( var i = 0; i < prey.Count; i++ )
			{
				var candidate = prey[i];
				if ( !candidate.IsValid() || candidate.IsDead || candidate.IsTamed )
					continue;

				var distSq = predatorPos.DistanceSquared( candidate.GameObject.WorldPosition.WithZ( 0f ) );
				if ( distSq >= nearestDistSq )
					continue;

				nearestDistSq = distSq;
				nearestPrey = candidate;
			}

			if ( nearestPrey.IsValid() )
				predator.HostKickstartHunt( nearestPrey.GameObject );
		}
	}

	static ushort SpeciesIdForKey( string speciesKey )
	{
		return speciesKey?.ToLowerInvariant() switch
		{
			"wolf" => ThornsAnimalSpeciesCatalog.WolfId,
			"panther" => ThornsAnimalSpeciesCatalog.PantherId,
			"deer" => ThornsAnimalSpeciesCatalog.DeerId,
			"moose" => ThornsAnimalSpeciesCatalog.MooseId,
			_ => 0,
		};
	}

	int HostSpawnSpeciesGroup(
		string speciesKey,
		Vector3 anchor,
		int maxInEncounter = int.MaxValue,
		List<ThornsAnimalBrain> spawnedBrains = null,
		float minPlayerClearanceInches = 0f )
	{
		if ( !TryResolveSpecies( speciesKey, out var species ) )
			return 0;

		if ( !species.SpawnsInGroups )
			return HostSpawnSpeciesSolitary( speciesKey, anchor, minPlayerClearanceInches );

		var cap = Math.Max( 1, maxInEncounter );
		var min = Math.Min( species.GroupSpawnCountMin, cap );
		var max = Math.Min( species.GroupSpawnCountMax, cap );
		if ( max < min )
			max = min;

		var count = Game.Random.Int( min, max );
		count = Math.Min( count, RemainingSpawnSlots() );
		if ( count <= 0 )
			return 0;

		return ThornsAnimalSpawnUtil.HostSpawnGroup(
			Scene,
			species,
			anchor,
			count,
			spawnedBrains,
			minPlayerClearanceInches );
	}

	int HostSpawnSpeciesSolitary( string speciesKey, Vector3 requested, float minPlayerClearanceInches = 0f )
	{
		if ( !TryResolveSpecies( speciesKey, out var species ) )
			return 0;

		return ThornsAnimalSpawnUtil.HostSpawnSolitary( Scene, species, requested, minPlayerClearanceInches );
	}

	bool TryResolveSpecies( string speciesKey, out ThornsAnimalSpeciesData species )
	{
		species = null;
		if ( !ThornsAnimalSpeciesRegistry.TryGet( speciesKey, out species ) )
		{
			var fallbackId = SpeciesIdForKey( speciesKey );
			if ( fallbackId == 0 || !ThornsAnimalSpeciesRegistry.TryGet( fallbackId, out species ) )
			{
				Log.Warning( $"[Thorns Animals] Unknown species '{speciesKey}' — spawn skipped." );
				return false;
			}

			Log.Warning( $"[Thorns Animals] Resolved '{speciesKey}' via id fallback ({fallbackId})." );
		}

		return true;
	}

	public static uint AllocateHerdGroupId() => _nextHerdGroupId++;

	public static void NotifyHerdFlee( ThornsAnimalBrain leader, GameObject threat )
	{
		if ( leader is null || !leader.IsValid() || leader.IsDead || !threat.IsValid() )
			return;

		if ( leader.HerdGroupId == 0 )
			return;

		var herdGroupId = leader.HerdGroupId;

		for ( var i = 0; i < Animals.Count; i++ )
		{
			var member = Animals[i];
			if ( member == leader || !member.IsValid() || member.IsDead )
				continue;

			if ( member.HerdGroupId != herdGroupId )
				continue;

			member.HostJoinHerdFlee( threat );
		}
	}

	public static void NotifyPackHunt( ThornsAnimalBrain leader, GameObject prey )
	{
		if ( leader is null || !leader.IsValid() || leader.IsDead || !prey.IsValid() )
			return;

		if ( leader.Species is null || !leader.Species.HuntsInGroups )
			return;

		var joinRadius = leader.Species.PackHuntJoinRadius;
		var joinRadiusSq = joinRadius * joinRadius;
		var leaderPos = leader.GameObject.WorldPosition;

		for ( var i = 0; i < Animals.Count; i++ )
		{
			var member = Animals[i];
			if ( member == leader || !member.IsValid() || member.IsDead )
				continue;

			if ( member.SpeciesId != leader.SpeciesId || member.Species is null || !member.Species.HuntsInGroups )
				continue;

			if ( (member.GameObject.WorldPosition - leaderPos).LengthSquared > joinRadiusSq )
				continue;

			member.HostJoinPackHunt( prey );
		}
	}

	public static void NotifyPackPostKillWander( ThornsAnimalBrain leader )
	{
		if ( leader is null || !leader.IsValid() || leader.IsDead )
			return;

		if ( leader.Species is null || !leader.Species.HuntsInGroups )
			return;

		var joinRadiusSq = leader.Species.PackHuntJoinRadius * leader.Species.PackHuntJoinRadius;
		var leaderPos = leader.GameObject.WorldPosition;

		for ( var i = 0; i < Animals.Count; i++ )
		{
			var member = Animals[i];
			if ( member == leader || !member.IsValid() || member.IsDead )
				continue;

			if ( member.SpeciesId != leader.SpeciesId )
				continue;

			if ( (member.GameObject.WorldPosition - leaderPos).LengthSquared > joinRadiusSq )
				continue;

			member.HostEnterPostKillWander();
		}
	}

	protected override void OnUpdate()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || Instance != this )
			return;

		TickNavMeshBake();

		if ( _nextPlayerRefresh )
		{
			_nextPlayerRefresh = 0.5f;
			RefreshPlayerRoots();
		}

		UpdateAnimalLodTiers();
		TickAmbientWildlifeSpawn();
		RunStaggeredDetection();
		RunStaggeredSimulation();
		RunAnimalSeparation();
		RunCorpseSweep();
	}

	void RunStaggeredSimulation()
	{
		if ( LiveAnimals.Count == 0 )
			return;

		for ( var i = 0; i < LiveAnimals.Count; i++ )
		{
			var brain = LiveAnimals[i];
			if ( !brain.IsValid() || brain.IsDead )
				continue;

			if ( brain.IsMounted || brain.HostRequiresActiveSimulation )
				brain.HostTickSimulation( Time.Delta );
		}

		ThornsNpcTickScheduler.RunRoundRobin(
			LiveAnimals,
			ref _animalSimCursor,
			ThornsNpcTickScheduler.MaxAnimalSimulationsPerFrame,
			b => b.IsMounted
			     || b.HostRequiresActiveSimulation
			     || ThornsNpcTickScheduler.ShouldSkipAnimalSimulation( b ),
			( b, d ) => b.HostTickSimulation( d ) );
	}

	void UpdateAnimalLodTiers()
	{
		if ( !_nextAnimalLodRefresh )
			return;

		_nextAnimalLodRefresh = 0.25f;

		var forceFullLod = ThornsSettlementTestSceneBootstrap.IsActive
		                   || ThornsBanditTestSceneBootstrap.IsActive;

		var players = ThornsPlayerRootCache.RootsReadOnly;
		for ( var i = 0; i < Animals.Count; i++ )
		{
			var brain = Animals[i];
			if ( !brain.IsValid() || brain.IsDead )
				continue;

			if ( forceFullLod )
			{
				brain.LodTier = ThornsNpcLodTier.Full;
				continue;
			}

			var pos = brain.GameObject.WorldPosition.WithZ( 0f );
			var minDistSq = float.MaxValue;
			for ( var p = 0; p < players.Count; p++ )
			{
				var player = players[p];
				if ( !player.IsValid() )
					continue;

				var delta = pos - player.WorldPosition.WithZ( 0f );
				var distSq = delta.LengthSquared;
				if ( distSq < minDistSq )
					minDistSq = distSq;
			}

			brain.LodTier = minDistSq < float.MaxValue
				? ThornsNpcLod.TierForDistanceSquared( minDistSq )
				: ThornsNpcLodTier.Sleeping;
		}
	}

	void RunAnimalSeparation()
	{
		var terrain = ThornsTerrainCache.Resolve( Scene );
		ThornsAnimalSeparation.RunSeparationPass( Animals, terrain );
	}

	void RefreshPlayerRoots() => ThornsPlayerRootCache.Refresh( Scene );

	void RunStaggeredDetection()
	{
		if ( LiveAnimals.Count == 0 )
			return;

		var processed = 0;
		var now = Time.Now;
		var count = LiveAnimals.Count;
		if ( count == 0 )
			return;

		_detectCursor = (( _detectCursor % count ) + count) % count;
		for ( var step = 0; step < count && processed < MaxDetectionsPerFrame; step++ )
		{
			var brain = LiveAnimals[( _detectCursor + step ) % count];
			if ( !brain.IsValid() || brain.IsDead )
				continue;
			if ( now < brain.NextDetectTime )
				continue;

			if ( !ThornsAnimalSpeciesRegistry.TryGet( brain.SpeciesId, out var species ) )
				continue;

			var interval = species.DetectionInterval * ThornsNpcLod.DetectionIntervalScale( brain.LodTier );
			if ( brain.LodTier == ThornsNpcLodTier.Sleeping )
			{
				if ( brain.HostRequiresActiveSimulation
				     || (!IgnoreAnimals && brain.HostShouldDetectWhileSleeping( LiveAnimals )) )
				{
					brain.HostDetect(
						IgnoreAnimals ? Array.Empty<ThornsAnimalBrain>() : LiveAnimals,
						ThornsPlayerRootCache.RootsReadOnly );
					brain.NextDetectTime = now + interval + brain.DetectStaggerSeconds;
					processed++;
					continue;
				}

				brain.NextDetectTime = now + interval + brain.DetectStaggerSeconds;
				processed++;
				continue;
			}

			brain.HostDetect(
				IgnoreAnimals ? Array.Empty<ThornsAnimalBrain>() : LiveAnimals,
				ThornsPlayerRootCache.RootsReadOnly );
			brain.NextDetectTime = now + interval + brain.DetectStaggerSeconds;
			processed++;
		}

		if ( processed > 0 )
			_detectCursor = ( _detectCursor + processed ) % count;
	}

	void RunCorpseSweep()
	{
		if ( !_nextCorpseSweep )
			return;

		_nextCorpseSweep = 0.1f;
		for ( var i = Animals.Count - 1; i >= 0; i-- )
		{
			var brain = Animals[i];
			if ( !brain.IsValid() )
			{
				Animals.RemoveAt( i );
				continue;
			}

			if ( !brain.HostShouldDespawnCorpse() )
				continue;

			brain.GameObject.Destroy();
			Animals.RemoveAt( i );
		}
	}

}
