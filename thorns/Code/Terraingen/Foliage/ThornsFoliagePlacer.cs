namespace Terraingen.Foliage;

using Sandbox;
using Terraingen.TerrainGen;

public static class ThornsFoliagePlacer
{
	enum ClusterScale { Normal, Dense, Mass, River, Hero }

	static bool _loggedFirstInstance;

	public static FoliageModelSet LoadModels( ThornsFoliageConfig config, ThornsFoliageDebugStats stats )
	{
		var pine = ThornsFoliageModelCache.Load( config.PineModel );
		var aspen = ThornsFoliageModelCache.Load( config.AspenModel );
		var oak = ThornsFoliageModelCache.Load( config.OakModel );

		if ( config.VerboseDebug )
		{
			Log.Info( $"[Thorns Foliage] Model pine valid={pine.IsValid} path={config.PineModel} bounds={pine.Bounds.Size}" );
			Log.Info( $"[Thorns Foliage] Model aspen valid={aspen.IsValid} path={config.AspenModel} bounds={aspen.Bounds.Size}" );
			Log.Info( $"[Thorns Foliage] Model oak valid={oak.IsValid} path={config.OakModel} bounds={oak.Bounds.Size}" );
		}

		if ( !pine.IsValid && !aspen.IsValid && !oak.IsValid )
		{
			Log.Error( "[Thorns Foliage] Failed to load any foliage2 tree models (pine/aspen/oak)." );
			stats.LastError = "model load failed";
			return default;
		}

		if ( !pine.IsValid || !aspen.IsValid || !oak.IsValid )
		{
			Log.Warning(
				$"[Thorns Foliage] Partial tree set — pine={pine.IsValid} aspen={aspen.IsValid} oak={oak.IsValid} (missing species fall back to another)." );
		}

		_loggedFirstInstance = false;
		return new FoliageModelSet( pine, aspen, oak, config.PineModel, config.AspenModel, config.OakModel );
	}

	public static void SpawnDebugCenterRing(
		Scene scene,
		GameObject foliageRoot,
		Terrain terrain,
		ThornsFoliageBiomeSampler sampler,
		ThornsFoliageConfig config,
		FoliageModelSet models,
		ThornsFoliageDebugStats stats )
	{
		if ( !config.DebugForceCenterRing )
			return;

		Log.Info( $"[Thorns Foliage] Spawning debug center ring ({config.DebugCenterRingCount} pines at world origin)…" );

		var rng = new Random( HashCode.Combine( config.FoliageSeed, 0xDEAD ) );
		var ringRadius = 900f;
		var placed = 0;

		for ( int i = 0; i < config.DebugCenterRingCount; i++ )
		{
			var angle = (i / (float)config.DebugCenterRingCount) * MathF.PI * 2f;
			var wx = MathF.Cos( angle ) * ringRadius;
			var wy = MathF.Sin( angle ) * ringRadius;
			if ( sampler.IsAboveSeaLevel( wx, wy ) )
				placed += SpawnCluster( scene, foliageRoot, terrain, sampler, config, models, rng, wx, wy, FoliageSpecies.Pine, stats, bypassBiome: true );
		}

		if ( sampler.IsAboveSeaLevel( 0f, 0f ) )
			placed += SpawnCluster( scene, foliageRoot, terrain, sampler, config, models, rng, 0f, 0f, FoliageSpecies.Pine, stats, bypassBiome: true );
		Log.Info( $"[Thorns Foliage] Debug center ring placed {placed} instances." );
	}

	public static void SpawnGuaranteedTreesAtOrigin(
		Scene scene,
		GameObject foliageRoot,
		Terrain terrain,
		ThornsFoliageBiomeSampler sampler,
		ThornsFoliageConfig config,
		FoliageModelSet models,
		ThornsFoliageDebugStats stats )
	{
		if ( !config.SpawnGuaranteedTreesAtOrigin )
			return;

		var rng = new Random( HashCode.Combine( config.FoliageSeed, 0xB00B ) );
		var count = 10;
		var radius = 1200f;
		var placed = 0;

		for ( int i = 0; i < count; i++ )
		{
			var angle = (i / (float)count) * MathF.PI * 2f;
			var wx = MathF.Cos( angle ) * radius;
			var wy = MathF.Sin( angle ) * radius;
			if ( sampler.IsAboveSeaLevel( wx, wy ) )
				placed += SpawnCluster( scene, foliageRoot, terrain, sampler, config, models, rng, wx, wy, FoliageSpecies.Pine, stats, bypassBiome: true );
		}

		Log.Info( $"[Thorns Foliage] Guaranteed origin trees placed {placed} instances (above sea only)." );
	}

	public static List<Vector2Int> BuildChunkGrid( Terrain terrain, ThornsFoliageConfig config )
	{
		var size = terrain.TerrainSize;
		var countX = Math.Max( 1, (int)Math.Ceiling( size / config.ChunkSizeInches ) );
		var countY = Math.Max( 1, (int)Math.Ceiling( size / config.ChunkSizeInches ) );
		var cells = new List<Vector2Int>();

		for ( int y = 0; y < countY; y++ )
		{
			for ( int x = 0; x < countX; x++ )
			{
				var cell = new Vector2Int( x, y );
				if ( config.LimitSpawnToCenterRadius )
				{
					var center = ChunkCenter( terrain, config, cell );
					var dist = new Vector2( center.x, center.y ).Length;
					if ( dist > config.SpawnRadiusFromCenterInches )
						continue;
				}

				cells.Add( cell );
			}
		}

		if ( config.VerboseDebug )
			Log.Info( $"[Thorns Foliage] Chunk cells to populate: {cells.Count} (limitCenter={config.LimitSpawnToCenterRadius}, radius={config.SpawnRadiusFromCenterInches:F0})" );

		return cells;
	}

	public static ThornsFoliageChunkData PopulateChunk(
		Scene scene,
		GameObject foliageRoot,
		Terrain terrain,
		Vector2Int cell,
		ThornsFoliageBiomeSampler sampler,
		FoliageModelSet models,
		ThornsFoliageConfig config,
		ThornsFoliageDebugStats stats,
		ThornsFoliageSpawnBudget budget = null )
	{
		var chunkRoot = scene.CreateObject( true );
		chunkRoot.Name = $"Foliage {cell.x}_{cell.y}";
		chunkRoot.Parent = foliageRoot;

		var center = ChunkCenter( terrain, config, cell );
		var count = FillChunk( scene, chunkRoot, terrain, cell, center, sampler, models, config, stats, budget );

		return new ThornsFoliageChunkData
		{
			Cell = cell,
			Center = center,
			Root = chunkRoot,
			InstanceCount = count,
		};
	}

	static int FillChunk(
		Scene scene,
		GameObject chunkRoot,
		Terrain terrain,
		Vector2Int cell,
		Vector3 chunkCenter,
		ThornsFoliageBiomeSampler sampler,
		FoliageModelSet models,
		ThornsFoliageConfig config,
		ThornsFoliageDebugStats stats,
		ThornsFoliageSpawnBudget budget )
	{
		if ( budget is not null && !budget.CanSpawn )
			return 0;

		var rng = new Random( HashCode.Combine( config.FoliageSeed, cell.x, cell.y, 0x51ed ) );
		var ecology = sampler.SampleChunkEcology( chunkCenter, config.ChunkSizeInches );
		var terrainOrigin = terrain.GameObject.WorldPosition;

		var treeSuit = MathF.Max( ecology.TreeSuitability, ecology.ForestMass * 0.55f ) * ecology.TreeDensityScale;
		var treeBudget = ComputeClusterBudget(
			config.MaxTreeClustersPerChunk,
			treeSuit * (1f - ecology.Opening * 0.55f),
			config.GlobalDensity );
		if ( config.VerboseDebug && stats.ChunksProcessed < 2 )
		{
			var centerSample = sampler.Sample( chunkCenter.x, chunkCenter.y );
			Log.Info( $"[Thorns Foliage] Chunk {cell} @ {chunkCenter}: forestMass={ecology.ForestMass:F2} opening={ecology.Opening:F2} river={ecology.RiverCorridor:F2} tree={treeBudget} canTrees={centerSample.CanPlaceTrees}" );
		}

		var count = 0;
		count += SpawnForestMasses( scene, chunkRoot, terrain, cell, terrainOrigin, chunkCenter, sampler, models, config, rng, ecology, stats, budget );
		if ( budget is not null && !budget.CanSpawn )
			return count;
		count += SpawnRiverCorridorLine( scene, chunkRoot, terrain, cell, terrainOrigin, chunkCenter, sampler, models, config, rng, ecology, stats, budget );
		if ( budget is not null && !budget.CanSpawn )
			return count;
		count += ScatterClusters( scene, chunkRoot, terrain, cell, terrainOrigin, sampler, models, config, rng, treeBudget, ecology, stats, budget );
		if ( budget is not null && !budget.CanSpawn )
			return count;
		count += TrySpawnHeroTree( scene, chunkRoot, terrain, chunkCenter, sampler, models, config, rng, ecology, stats, budget );
		return count;
	}

	static int ComputeClusterBudget( int maxPerChunk, float suitability, float globalDensity )
	{
		if ( suitability < 0.08f || globalDensity <= 0.01f )
			return 0;

		var raw = maxPerChunk * suitability * globalDensity;
		var count = (int)MathF.Round( raw );
		if ( count < 1 && suitability >= 0.1f )
			count = 1;

		return Math.Min( count, maxPerChunk );
	}

	static int SpawnForestMasses(
		Scene scene,
		GameObject chunkRoot,
		Terrain terrain,
		Vector2Int cell,
		Vector3 terrainOrigin,
		Vector3 chunkCenter,
		ThornsFoliageBiomeSampler sampler,
		FoliageModelSet models,
		ThornsFoliageConfig config,
		Random rng,
		ThornsFoliageChunkEcology ecology,
		ThornsFoliageDebugStats stats,
		ThornsFoliageSpawnBudget budget )
	{
		if ( ecology.ForestMass < config.ForestMassThreshold * 0.65f || ecology.Opening > 0.85f )
			return 0;

		var massCount = ecology.ForestMass > 0.58f ? config.MaxForestMassesPerChunk : 1;
		var placed = 0;

		for ( int m = 0; m < massCount; m++ )
		{
			if ( budget is not null && !budget.CanSpawn )
				break;
			var cx = chunkCenter.x + (rng.NextSingle() - 0.5f) * config.ChunkSizeInches * 0.55f;
			var cy = chunkCenter.y + (rng.NextSingle() - 0.5f) * config.ChunkSizeInches * 0.55f;
			var biome = sampler.Sample( cx, cy );
			if ( biome.Slope >= sampler.MaxTreeSlope || biome.Opening > 0.85f )
				continue;

			var species = biome.PineWeight >= biome.AspenWeight && biome.PineWeight >= biome.OakWeight
				? FoliageSpecies.Pine
				: PickTreeSpecies( biome, rng );

			placed += SpawnCluster( scene, chunkRoot, terrain, sampler, config, models, rng, cx, cy, species, stats, ClusterScale.Mass, budget: budget );
		}

		return placed;
	}

	static int SpawnRiverCorridorLine(
		Scene scene,
		GameObject chunkRoot,
		Terrain terrain,
		Vector2Int cell,
		Vector3 terrainOrigin,
		Vector3 chunkCenter,
		ThornsFoliageBiomeSampler sampler,
		FoliageModelSet models,
		ThornsFoliageConfig config,
		Random rng,
		ThornsFoliageChunkEcology ecology,
		ThornsFoliageDebugStats stats,
		ThornsFoliageSpawnBudget budget )
	{
		if ( ecology.RiverCorridor < 0.28f || ecology.FlowDirection.Length < 0.01f )
			return 0;

		var flow = ecology.FlowDirection;
		var perp = new Vector2( -flow.y, flow.x );
		var lineClusters = Math.Min( config.MaxRiverLineClustersPerChunk, (int)MathF.Ceiling( ecology.RiverCorridor * 5f ) );
		var placed = 0;
		var halfSpan = config.ChunkSizeInches * 0.42f;

		for ( int i = 0; i < lineClusters; i++ )
		{
			if ( budget is not null && !budget.CanSpawn )
				break;

			var along = (rng.NextSingle() - 0.5f) * halfSpan * 2f;
			var lateral = (rng.NextSingle() - 0.5f) * config.RiverLineSpacingInches * 0.35f;
			var cx = chunkCenter.x + flow.x * along + perp.x * lateral;
			var cy = chunkCenter.y + flow.y * along + perp.y * lateral;

			var biome = sampler.Sample( cx, cy );
			if ( !biome.CanPlaceTrees )
				continue;

			if ( biome.RiverCorridor < 0.2f )
				continue;

			if ( biome.Slope > sampler.MaxTreeSlope || biome.Opening > 0.85f )
				continue;

			var species = biome.AspenWeight > biome.PineWeight ? FoliageSpecies.Aspen : PickTreeSpecies( biome, rng );

			placed += SpawnCluster( scene, chunkRoot, terrain, sampler, config, models, rng, cx, cy, species, stats, ClusterScale.River, budget: budget );
		}

		return placed;
	}

	static int TrySpawnHeroTree(
		Scene scene,
		GameObject chunkRoot,
		Terrain terrain,
		Vector3 chunkCenter,
		ThornsFoliageBiomeSampler sampler,
		FoliageModelSet models,
		ThornsFoliageConfig config,
		Random rng,
		ThornsFoliageChunkEcology ecology,
		ThornsFoliageDebugStats stats,
		ThornsFoliageSpawnBudget budget )
	{
		if ( budget is not null && !budget.CanSpawn )
			return 0;

		if ( ecology.HeroTreeChance < config.HeroTreeChance * 0.12f )
			return 0;

		if ( rng.NextSingle() > ecology.HeroTreeChance * config.HeroTreeChance )
			return 0;

		var cx = chunkCenter.x + (rng.NextSingle() - 0.5f) * config.ChunkSizeInches * 0.4f;
		var cy = chunkCenter.y + (rng.NextSingle() - 0.5f) * config.ChunkSizeInches * 0.4f;
		var biome = sampler.Sample( cx, cy );
		if ( biome.Slope > sampler.MaxTreeSlope || biome.Opening > 0.7f )
			return 0;

		var species = biome.Alpine > 0.45f
			? FoliageSpecies.Pine
			: (biome.OakWeight > biome.AspenWeight ? FoliageSpecies.Oak : FoliageSpecies.Aspen);

		return SpawnCluster( scene, chunkRoot, terrain, sampler, config, models, rng, cx, cy, species, stats, ClusterScale.Hero, budget: budget );
	}

	static int ScatterClusters(
		Scene scene,
		GameObject chunkRoot,
		Terrain terrain,
		Vector2Int cell,
		Vector3 terrainOrigin,
		ThornsFoliageBiomeSampler sampler,
		FoliageModelSet models,
		ThornsFoliageConfig config,
		Random rng,
		int clusterCount,
		ThornsFoliageChunkEcology ecology,
		ThornsFoliageDebugStats stats,
		ThornsFoliageSpawnBudget budget )
	{
		if ( clusterCount <= 0 )
			return 0;

		var clustersPlaced = 0;
		var placed = 0;
		var attempts = Math.Max( clusterCount * 5, 1 );

		for ( int i = 0; i < attempts && clustersPlaced < clusterCount; i++ )
		{
			if ( budget is not null && !budget.CanSpawn )
				break;

			stats.ClustersAttempted++;

			var cx = terrainOrigin.x + (cell.x + rng.NextSingle()) * config.ChunkSizeInches;
			var cy = terrainOrigin.y + (cell.y + rng.NextSingle()) * config.ChunkSizeInches;

			cx += (rng.NextSingle() - 0.5f) * config.ClusterSpacingInches * 0.35f;
			cy += (rng.NextSingle() - 0.5f) * config.ClusterSpacingInches * 0.35f;

			var biome = sampler.Sample( cx, cy );
			if ( biome.Opening > 0.9f )
			{
				stats.BiomeRejected++;
				continue;
			}

			if ( biome.Slope > sampler.MaxTreeSlope )
			{
				stats.BiomeRejected++;
				continue;
			}

			var species = PickTreeSpecies( biome, rng );
			var treeWeight = species switch
			{
				FoliageSpecies.Pine => biome.PineWeight,
				FoliageSpecies.Aspen => biome.AspenWeight,
				FoliageSpecies.Oak => biome.OakWeight,
				_ => 0f,
			};
			if ( treeWeight < 0.04f )
			{
				stats.WeightRejected++;
				continue;
			}

			if ( config.RequireTreeFootprintFlatness
				&& !ThornsFoliageFlatness.IsTreeFootprintFlat( terrain, sampler, cx, cy, config, out _ ) )
			{
				stats.SlopeFlatRejected++;
				continue;
			}

			var scale = biome.ForestMass > 0.5f ? ClusterScale.Dense : ClusterScale.Normal;
			var spawned = SpawnCluster( scene, chunkRoot, terrain, sampler, config, models, rng, cx, cy, species, stats, scale, budget: budget );
			if ( spawned > 0 )
			{
				clustersPlaced++;
				stats.ClustersPlaced++;
				placed += spawned;
			}
		}

		return placed;
	}

	static int SpawnCluster(
		Scene scene,
		GameObject chunkRoot,
		Terrain terrain,
		ThornsFoliageBiomeSampler sampler,
		ThornsFoliageConfig config,
		FoliageModelSet models,
		Random rng,
		float centerX,
		float centerY,
		FoliageSpecies species,
		ThornsFoliageDebugStats stats,
		ClusterScale scale = ClusterScale.Normal,
		bool bypassBiome = false,
		ThornsFoliageSpawnBudget budget = null )
	{
		if ( budget is not null && !budget.CanSpawn )
			return 0;

		var model = models.Get( species );
		if ( !model.IsValid )
			return 0;

		int countMin;
		int countMax;
		float radius;
		float scaleMul = 1f;

		if ( scale == ClusterScale.Mass )
		{
			if ( species == FoliageSpecies.Pine )
				(countMin, countMax, radius) = (10, 18, 3200f);
			else
				(countMin, countMax, radius) = (6, 14, 2200f);
		}
		else if ( scale == ClusterScale.River )
		{
			if ( species == FoliageSpecies.Aspen )
				(countMin, countMax, radius) = (2, 6, 900f);
			else
				(countMin, countMax, radius) = (2, 5, 820f);
		}
		else if ( scale == ClusterScale.Hero )
		{
			(countMin, countMax, radius) = (1, 1, 0f);
			scaleMul = 1.14f;
		}
		else if ( scale == ClusterScale.Dense )
		{
			if ( species == FoliageSpecies.Pine )
				(countMin, countMax, radius) = (6, 14, 1400f);
			else
				(countMin, countMax, radius) = (3, 8, 900f);
		}
		else if ( species == FoliageSpecies.Oak )
			(countMin, countMax, radius) = (1, 3, 520f);
		else if ( species == FoliageSpecies.Aspen )
			(countMin, countMax, radius) = (2, 5, 680f);
		else
			(countMin, countMax, radius) = (3, 9, 1100f);

		var instanceCount = bypassBiome ? 1 : rng.Next( countMin, countMax + 1 );
		var spawned = 0;
		var uniformScale = ComputeUniformScale( model, species, config, rng ) * scaleMul;
		stats.LastModelBoundsSize = model.Bounds.Size;

		for ( int i = 0; i < instanceCount; i++ )
		{
			if ( budget is not null && !budget.CanSpawn )
				break;

			var angle = rng.NextSingle() * MathF.PI * 2f;
			var dist = bypassBiome ? 0f : rng.NextSingle() * radius;
			var wx = centerX + MathF.Cos( angle ) * dist;
			var wy = centerY + MathF.Sin( angle ) * dist;

			if ( sampler is not null && !sampler.IsAboveSeaLevel( wx, wy ) )
			{
				stats.BiomeRejected++;
				continue;
			}

			if ( !bypassBiome && sampler is not null )
			{
				var biome = sampler.Sample( wx, wy );
				var relaxTree = scale == ClusterScale.Mass || scale == ClusterScale.Dense;

				if ( biome.Opening > (relaxTree ? 0.93f : 0.9f) )
				{
					stats.BiomeRejected++;
					continue;
				}

				if ( biome.Slope > sampler.MaxTreeSlope )
				{
					stats.BiomeRejected++;
					continue;
				}

				if ( config.RequireTreeFootprintFlatness
					&& !relaxTree
					&& !ThornsFoliageFlatness.IsTreeFootprintFlat( terrain, sampler, wx, wy, config, out _ ) )
				{
					stats.SlopeFlatRejected++;
					continue;
				}
			}

			if ( !ThornsFoliageSurface.TrySampleWorld( terrain, wx, wy, model, uniformScale, species, config, out var worldPos ) )
			{
				stats.RayMisses++;
				if ( config.VerboseDebug && stats.RayMisses <= 5 )
					Log.Warning( $"[Thorns Foliage] Ray miss at ({wx:F0},{wy:F0}) — terrain may not cover this XY." );
				continue;
			}

			var yaw = rng.NextSingle() * 360f;
			CreateFoliageInstance( scene, chunkRoot, terrain, model, models.GetModelPath( species ), worldPos, yaw, uniformScale, config,
				bypassBiome ? $"DEBUG_{species}" : species.ToString() );
			budget?.TryConsume();
			spawned++;

			stats.TreesSpawned++;

			stats.LastSpawnPosition = worldPos;
			stats.LastSpawnSpecies = species.ToString();
			stats.LastSpawnScale = uniformScale.x;

			if ( !_loggedFirstInstance )
			{
				_loggedFirstInstance = true;
				var estHeightIn = EstimateWorldHeightInches( model, uniformScale.x, species, config );
				Log.Info( $"[Thorns Foliage] First instance '{species}' at {worldPos}, scale {uniformScale}, estHeight≈{estHeightIn:F0} in (enabled, parent={chunkRoot.Name})" );
			}
		}

		return spawned;
	}

	static Vector3 ComputeUniformScale( Model model, FoliageSpecies species, ThornsFoliageConfig config, Random rng )
	{
		var bounds = model.Bounds;
		var size = bounds.Size;
		var maxAxis = Math.Max( size.x, Math.Max( size.y, size.z ) );

		float uniform;

		// pine/aspen/oak meshes are ~1 unit tall (meters in source data)
		var meshMeters = Math.Max( maxAxis, 0.01f );
		var targetMeters = GetTreeTargetHeightInches( species, config ) / config.InchesPerMeter;
		uniform = (targetMeters / meshMeters) * config.ScaleMultiplier * config.TreeSizeMultiplier;
		uniform = Math.Max( uniform, config.MinTreeRenderScale );

		uniform *= MathX.Lerp( 0.92f, 1.08f, rng.NextSingle() );
		return new Vector3( uniform );
	}

	static void CreateFoliageInstance(
		Scene scene,
		GameObject parent,
		Terrain terrain,
		Model model,
		string modelAssetPath,
		Vector3 worldPos,
		float yawDegrees,
		Vector3 scale,
		ThornsFoliageConfig config,
		string objectName )
	{
		_ = terrain;
		var instance = scene.CreateObject( true );
		instance.Name = config.SpawnAsHarvestableWoodTrees ? "ThornsTreeNode" : objectName;
		instance.Parent = parent;
		instance.WorldPosition = worldPos;
		instance.WorldRotation = Rotation.FromYaw( yawDegrees );
		instance.LocalScale = scale;

		var renderer = instance.Components.Get<ModelRenderer>() ?? instance.Components.Create<ModelRenderer>();
		renderer.Model = model;
		renderer.Enabled = true;
		renderer.RenderType = ModelRenderer.ShadowRenderType.On;
		ThornsModelMaterialUvScale.ApplyForScaledModel( renderer, instance, model, modelAssetPath );

		var lodTag = instance.Components.Get<ThornsFoliageInstance>() ?? instance.Components.Create<ThornsFoliageInstance>();
		lodTag.LodState = 0;
		lodTag.Renderer = renderer;

		if ( config.SpawnAsHarvestableWoodTrees && ( !Networking.IsActive || Networking.IsHost ) )
			ThornsResourceNode.AttachWoodHarvestGameplay( instance, model, scale.x );
		else if ( !Networking.IsActive || Networking.IsHost )
			ThornsFoliagePlacer.AttachDecorativeTreeTrunkCollision( instance, model, scale.x );
	}

	/// <summary>Visual-only foliage still gets a trunk hull so every placed tree blocks movement consistently.</summary>
	public static void AttachDecorativeTreeTrunkCollision( GameObject instance, Model model, float uniformScale )
	{
		if ( !instance.IsValid() || !model.IsValid() )
			return;

		ThornsAnchoredWorldPhysics.EnsureAnchoredWoodTrunkBoxPhysics( instance, model, uniformScale );
		ThornsCollisionTags.EnsureWoodTreeTrunkSolidCollision( instance );
	}

	static float EstimateWorldHeightInches( Model model, float uniform, FoliageSpecies species, ThornsFoliageConfig config )
	{
		var maxAxis = Math.Max( model.Bounds.Size.x, Math.Max( model.Bounds.Size.y, model.Bounds.Size.z ) );
		return maxAxis * config.InchesPerMeter * uniform;
	}

	static float GetTreeTargetHeightInches( FoliageSpecies species, ThornsFoliageConfig config ) => species switch
	{
		FoliageSpecies.Oak => config.OakTargetHeightInches,
		FoliageSpecies.Aspen => config.AspenTargetHeightInches,
		_ => config.PineTargetHeightInches,
	};

	static FoliageSpecies PickTreeSpecies( FoliageBiomeSample biome, Random rng )
	{
		var pine = biome.PineWeight;
		var aspen = biome.AspenWeight;
		var oak = biome.OakWeight * 0.75f;
		var sum = pine + aspen + oak;
		if ( sum < 0.001f )
			return FoliageSpecies.Pine;

		var pick = rng.NextSingle() * sum;
		if ( pick < pine )
			return FoliageSpecies.Pine;
		pick -= pine;
		if ( pick < aspen )
			return FoliageSpecies.Aspen;
		return FoliageSpecies.Oak;
	}

	static Vector3 ChunkCenter( Terrain terrain, ThornsFoliageConfig config, Vector2Int cell )
	{
		var origin = terrain.GameObject.WorldPosition;
		var x = origin.x + (cell.x + 0.5f) * config.ChunkSizeInches;
		var y = origin.y + (cell.y + 0.5f) * config.ChunkSizeInches;
		return new Vector3( x, y, 0f );
	}

	public readonly struct FoliageModelSet
	{
		readonly Model _pine;
		readonly Model _aspen;
		readonly Model _oak;
		readonly string _pinePath;
		readonly string _aspenPath;
		readonly string _oakPath;

		public FoliageModelSet( Model pine, Model aspen, Model oak, string pinePath, string aspenPath, string oakPath )
		{
			_pine = pine;
			_aspen = aspen;
			_oak = oak;
			_pinePath = pinePath ?? "";
			_aspenPath = aspenPath ?? "";
			_oakPath = oakPath ?? "";
		}

		public bool IsValid => _pine.IsValid || _aspen.IsValid || _oak.IsValid;

		public string GetModelPath( FoliageSpecies species ) => species switch
		{
			FoliageSpecies.Pine => _pinePath,
			FoliageSpecies.Aspen => _aspenPath,
			FoliageSpecies.Oak => _oakPath,
			_ => _pinePath,
		};

		public Model Get( FoliageSpecies species )
		{
			var pick = species switch
			{
				FoliageSpecies.Pine => _pine,
				FoliageSpecies.Aspen => _aspen,
				FoliageSpecies.Oak => _oak,
				_ => default,
			};

			if ( pick.IsValid )
				return pick;

			if ( _pine.IsValid )
				return _pine;
			if ( _aspen.IsValid )
				return _aspen;
			return _oak;
		}
	}
}
