namespace Terraingen.NpcGuild;

using Terraingen;
using Terraingen.Buildings;
using Terraingen.GameData;
using Terraingen.TerrainGen;
using Terraingen.World;

/// <summary>Spawns a 5-building military outpost cluster with a claimable core.</summary>
public static class ThornsNpcGuildOutpostBuilder
{
	public const int BuildingsPerOutpost = 5;
	public const float ClusterRadiusInches = 520f;
	public const float MinBuildingSpacingInches = 360f;

	static readonly ThornsProcBuildingType[] MilitaryTypes =
	{
		ThornsProcBuildingType.MilitaryComplex,
		ThornsProcBuildingType.RadioOutpost
	};

	public sealed class SpawnResult
	{
		public GameObject OutpostRoot { get; init; }
		public GameObject CoreObject { get; init; }
		public ThornsNpcGuildCore Core { get; init; }
		public List<int> FurnitureIds { get; init; } = new();
	}

	public static SpawnResult HostSpawnOutpost(
		Scene scene,
		GameObject parent,
		Terrain terrain,
		ThornsTerrainConfig terrainConfig,
		Vector3 center,
		Rotation clusterRotation,
		int outpostSeed,
		string guildId,
		string outpostId,
		bool isHeadquarters,
		int buildingIndexOffset )
	{
		if ( scene is null || !scene.IsValid() || terrain is null || !terrain.IsValid() )
			return null;

		var lootService = EnsureLootService( parent );
		var spawner = new ThornsProcBuildingShellSpawner( scene );
		var placementRng = new Random( HashCode.Combine( outpostSeed, 0x51A07057 ) );
		var buildingRng = new Random( HashCode.Combine( outpostSeed, 0xB01D1E7 ) );

		var outpostRoot = scene.CreateObject( true );
		outpostRoot.Name = isHeadquarters
			? $"NPC Guild HQ ({guildId})"
			: $"NPC Guild Outpost {outpostId} ({guildId})";
		outpostRoot.Parent = parent;
		outpostRoot.WorldPosition = center;

		var furnitureIds = new List<int>( BuildingsPerOutpost * 4 );
		var resolvedCenter = center;
		var resolvedRotation = clusterRotation;
		GameObject coreBuildingRoot = null;

		for ( var i = 0; i < BuildingsPerOutpost; i++ )
		{
			if ( !TryPickBuildingLot(
				     terrain,
				     terrainConfig,
				     resolvedCenter,
				     resolvedRotation,
				     i,
				     outpostSeed,
				     out var lotPos,
				     out var lotRot,
				     out var foundationDepth ) )
				continue;

			if ( i == 0 )
			{
				resolvedCenter = new Vector3( lotPos.x, lotPos.y, center.z );
				outpostRoot.WorldPosition = resolvedCenter;
			}

			var buildingType = MilitaryTypes[(buildingIndexOffset + i) % MilitaryTypes.Length];
			var variantIndex = ThornsProcBuildingTypePicker.PickVariantIndex( buildingRng, buildingType, buildingIndexOffset + i );

			var spawnResult = spawner.Spawn(
				new ThornsProcBuildingShellSpawner.Request(
					lotPos,
					lotRot,
					outpostRoot,
					buildingType,
					variantIndex,
					1,
					buildingIndexOffset + i,
					foundationDepth,
					$"NPC Outpost Building {buildingIndexOffset + i:00} ({buildingType})",
					RegisterFootprint: true ),
				lootService,
				placementRng );

			coreBuildingRoot ??= spawnResult.Root;
		}

		CollectFurnitureIds( outpostRoot, furnitureIds );

		var coreObject = HostSpawnCore(
			scene,
			outpostRoot,
			coreBuildingRoot,
			resolvedCenter,
			guildId,
			outpostId,
			isHeadquarters );
		var core = coreObject.Components.Get<ThornsNpcGuildCore>();

		return new SpawnResult
		{
			OutpostRoot = outpostRoot,
			CoreObject = coreObject,
			Core = core,
			FurnitureIds = furnitureIds
		};
	}

	static GameObject HostSpawnCore(
		Scene scene,
		GameObject outpostRoot,
		GameObject buildingRoot,
		Vector3 fallbackCenter,
		string guildId,
		string outpostId,
		bool isHeadquarters )
	{
		const int centerCell = 1;
		const int story = 0;
		var structureId = ThornsPlaceableModels.ResearchStructureId;

		var core = scene.CreateObject( true );
		core.Name = isHeadquarters ? "NPC Guild HQ Core" : "NPC Guild Outpost Core";
		core.Tags.Add( "npc_guild_core" );
		core.Tags.Add( "solid" );

		if ( buildingRoot is not null && buildingRoot.IsValid() )
		{
			core.Parent = buildingRoot;

			var ctx = new ThornsProcBuildingInteriorPose.SpawnContext(
				buildingRoot,
				ThornsProcBuildingInterior.GridCells,
				ThornsProcBuildingInterior.GridCells,
				story,
				centerCell,
				centerCell );

			ThornsPlaceableFurniturePresentation.Apply( core, structureId, story, centerCell, centerCell );
			ThornsProcBuildingInteriorPose.SeatOnBuilding(
				core,
				buildingRoot,
				in ctx,
				structureId,
				buildingRoot.WorldRotation,
				buildingRoot.WorldPosition );
		}
		else
		{
			core.Parent = outpostRoot;
			core.WorldPosition = fallbackCenter;
			ThornsPlaceableFurniturePresentation.Apply( core, structureId );
		}

		var coreComp = core.Components.Create<ThornsNpcGuildCore>();
		coreComp.GuildId = guildId ?? "";
		coreComp.OutpostId = outpostId ?? "";
		coreComp.IsHeadquarters = isHeadquarters;

		return core;
	}

	static ThornsBuildingLootWorldService EnsureLootService( GameObject parent )
	{
		if ( parent is null || !parent.IsValid() )
			return ThornsBuildingLootWorldService.Instance;

		return parent.Components.Get<ThornsBuildingLootWorldService>()
		       ?? parent.Components.Create<ThornsBuildingLootWorldService>();
	}

	static void CollectFurnitureIds( GameObject outpostRoot, List<int> ids )
	{
		WalkFurnitureHierarchy( outpostRoot, ids );
	}

	static void WalkFurnitureHierarchy( GameObject node, List<int> ids )
	{
		if ( node is null || !node.IsValid() )
			return;

		var marker = node.Components.Get<ThornsLootableFurniture>();
		if ( marker is not null && marker.IsValid() && marker.FurnitureId > 0 && !ids.Contains( marker.FurnitureId ) )
			ids.Add( marker.FurnitureId );

		foreach ( var child in node.Children )
			WalkFurnitureHierarchy( child, ids );
	}

	static bool TryPickBuildingLot(
		Terrain terrain,
		ThornsTerrainConfig terrainConfig,
		Vector3 clusterCenter,
		Rotation clusterRotation,
		int buildingIndex,
		int outpostSeed,
		out Vector3 lotPos,
		out Rotation lotRot,
		out float foundationDepth )
	{
		lotPos = default;
		lotRot = clusterRotation;
		foundationDepth = 0f;

		for ( var attempt = 0; attempt < 12; attempt++ )
		{
			if ( !TryComputeClusterProbe( clusterCenter, clusterRotation, buildingIndex, outpostSeed, attempt, out var probe, out lotRot ) )
				continue;

			if ( ThornsProcBuildingTerrainUtil.TryResolveLotBase(
				     terrain,
				     terrainConfig,
				     probe,
				     lotRot,
				     ThornsProcBuildingTerrainUtil.DefaultMaxFootprintReliefInches,
				     out var baseZ,
				     out foundationDepth,
				     out _,
				     out _ ) )
			{
				lotPos = new Vector3( probe.x, probe.y, baseZ );
				if ( WouldBuildingOverlapWorld( lotPos, lotRot ) )
					continue;

				return true;
			}
		}

		return false;
	}

	public static bool TryFindExpansionCenter(
		Terrain terrain,
		ThornsTerrainConfig terrainConfig,
		Vector3 hqCenter,
		int outpostSeed,
		IReadOnlyList<Vector3> existingCenters,
		out Vector3 center,
		out Rotation rotation )
	{
		center = default;
		rotation = Rotation.Identity;
		if ( terrain is null || !terrain.IsValid() || terrainConfig is null )
			return false;

		var rng = new Random( HashCode.Combine( outpostSeed, 0xE0A040D ) );
		const float minDist = ThornsNpcGuildWorldService.ExpansionMinDistanceInches;
		const float maxDist = ThornsNpcGuildWorldService.ExpansionMaxDistanceInches;

		var bestRelief = float.MaxValue;
		var found = false;

		for ( var attempt = 0; attempt < 64; attempt++ )
		{
			var angle = rng.NextSingle() * MathF.PI * 2f;
			var dist = minDist + rng.NextSingle() * (maxDist - minDist );
			var offset = new Vector3( MathF.Cos( angle ) * dist, MathF.Sin( angle ) * dist, 0f );
			var candidate = hqCenter + offset;

			if ( !ThornsTerrainSurface.TrySnapToTerrain( terrain, candidate, out var ground ) )
				continue;

			if ( !ThornsProcBuildingTerrainUtil.IsWithinLowlandElevation( terrain, terrainConfig, ground.z ) )
				continue;

			if ( IsTooNearExisting( ground, existingCenters, MinBuildingSpacingInches * 2.5f ) )
				continue;

			var yaw = rng.NextSingle() * 360f;
			var candidateRotation = Rotation.FromYaw( yaw );
			if ( WouldClusterOverlapWorldBuildings( ground, candidateRotation, outpostSeed ) )
				continue;

			if ( !ThornsProcBuildingTerrainUtil.TryResolveLowlandLotBase(
				     terrain,
				     terrainConfig,
				     ground,
				     candidateRotation,
				     ThornsProcBuildingTerrainUtil.NpcGuildCenterMaxReliefInches,
				     out var baseZ,
				     out var relief,
				     out _ ) )
				continue;

			if ( relief >= bestRelief )
				continue;

			bestRelief = relief;
			center = new Vector3( ground.x, ground.y, baseZ );
			rotation = candidateRotation;
			found = true;
		}

		return found;
	}

	public static bool TryFindHeadquartersCenter(
		Terrain terrain,
		ThornsTerrainConfig terrainConfig,
		int worldSeed,
		int guildSeedSalt,
		IReadOnlyList<Vector3> avoidCenters,
		float minSeparationInches,
		out Vector3 center,
		out Rotation rotation )
	{
		center = default;
		rotation = Rotation.Identity;
		if ( terrain is null || !terrain.IsValid() || terrainConfig is null )
			return false;

		var terrainSize = terrain.TerrainSize;
		var margin = 2400f;
		var min = terrain.GameObject.WorldPosition.x + margin;
		var max = terrain.GameObject.WorldPosition.x + terrainSize - margin;
		var minY = terrain.GameObject.WorldPosition.y + margin;
		var maxY = terrain.GameObject.WorldPosition.y + terrainSize - margin;
		var rng = new Random( unchecked( (int)HashCode.Combine( worldSeed, guildSeedSalt ) ) );

		var bestRelief = float.MaxValue;
		var found = false;

		for ( var attempt = 0; attempt < 96; attempt++ )
		{
			var p = new Vector3(
				min + rng.NextSingle() * (max - min),
				minY + rng.NextSingle() * (max - minY ),
				0f );

			if ( !ThornsTerrainSurface.TrySnapToTerrain( terrain, p, out var ground ) )
				continue;

			if ( !ThornsProcBuildingTerrainUtil.IsWithinLowlandElevation( terrain, terrainConfig, ground.z ) )
				continue;

			if ( IsTooNearExisting( ground, avoidCenters, minSeparationInches ) )
				continue;

			var yaw = rng.NextSingle() * 360f;
			var candidateRotation = Rotation.FromYaw( yaw );
			if ( WouldClusterOverlapWorldBuildings( ground, candidateRotation, guildSeedSalt ) )
				continue;

			if ( !ThornsProcBuildingTerrainUtil.TryResolveLowlandLotBase(
				     terrain,
				     terrainConfig,
				     ground,
				     candidateRotation,
				     ThornsProcBuildingTerrainUtil.NpcGuildCenterMaxReliefInches,
				     out var baseZ,
				     out var relief,
				     out _ ) )
				continue;

			if ( relief >= bestRelief )
				continue;

			bestRelief = relief;
			center = new Vector3( ground.x, ground.y, baseZ );
			rotation = candidateRotation;
			found = true;
		}

		return found;
	}

	public static bool TryFindHeadquartersCenter(
		Terrain terrain,
		ThornsTerrainConfig terrainConfig,
		int worldSeed,
		out Vector3 center,
		out Rotation rotation )
		=> TryFindHeadquartersCenter(
			terrain,
			terrainConfig,
			worldSeed,
			ThornsNpcGuildCatalog.IronWolvesHqSeedSalt,
			Array.Empty<Vector3>(),
			0f,
			out center,
			out rotation );

	static bool IsTooNearExisting( Vector3 point, IReadOnlyList<Vector3> existingCenters, float minDistance )
	{
		var minSqr = minDistance * minDistance;
		foreach ( var other in existingCenters )
		{
			var d = point - other;
			if ( new Vector2( d.x, d.y ).LengthSquared < minSqr )
				return true;
		}

		return false;
	}

	static bool WouldBuildingOverlapWorld( Vector3 position, Rotation rotation ) =>
		ThornsWorldScatterFootprintRegistry.WouldBuildingFootprintOverlapRegistered(
			position,
			rotation,
			ThornsProcBuildingInterior.GridCells,
			ThornsProcBuildingInterior.GridCells );

	static bool WouldClusterOverlapWorldBuildings( Vector3 clusterCenter, Rotation clusterRotation, int seedSalt )
	{
		for ( var buildingIndex = 0; buildingIndex < BuildingsPerOutpost; buildingIndex++ )
		{
			if ( !TryComputeClusterProbe( clusterCenter, clusterRotation, buildingIndex, seedSalt, 0, out var probe, out var lotRot ) )
				continue;

			if ( WouldBuildingOverlapWorld( probe, lotRot ) )
				return true;
		}

		return false;
	}

	static bool TryComputeClusterProbe(
		Vector3 clusterCenter,
		Rotation clusterRotation,
		int buildingIndex,
		int outpostSeed,
		int attempt,
		out Vector3 probe,
		out Rotation lotRot )
	{
		probe = default;
		lotRot = clusterRotation;

		var angle = buildingIndex * (360f / BuildingsPerOutpost) + (outpostSeed % 37);
		var radius = ClusterRadiusInches + (buildingIndex % 2) * 80f;
		var offset = clusterRotation * new Vector3(
			MathF.Cos( angle * MathF.PI / 180f ) * radius,
			MathF.Sin( angle * MathF.PI / 180f ) * radius,
			0f );
		probe = clusterCenter + offset + clusterRotation * new Vector3( attempt * 42f, attempt * -31f, 0f );
		var yaw = (outpostSeed + buildingIndex * 17 + attempt * 29) % 360;
		lotRot = Rotation.FromYaw( yaw );
		return true;
	}
}
