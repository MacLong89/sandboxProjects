namespace Terraingen.Minerals;

using Terraingen.Buildings;
using Terraingen.Core;
using Terraingen.Foliage;
using Terraingen.Physics;
using Terraingen.TerrainGen;
using Terraingen.World;

public static class ThornsMineralPlacer
{
	static bool _loggedFirst;

	static readonly string[] ScatterModelFallbacks =
	{
		ThornsMineralConfig.DefaultScatterModel,
		"models/clutter/rock1.vmdl",
		"models/clutter/rock2.vmdl",
	};

	public static Model LoadModel( ThornsMineralConfig config, ThornsMineralDebugStats stats )
	{
		config.NormalizeScatterModel();

		var paths = new List<string>();
		if ( !string.IsNullOrWhiteSpace( config.ScatterModel ) )
			paths.Add( config.ScatterModel.Trim() );

		foreach ( var fallback in ScatterModelFallbacks )
		{
			if ( !paths.Contains( fallback, StringComparer.OrdinalIgnoreCase ) )
				paths.Add( fallback );
		}

		foreach ( var path in paths )
		{
			var model = ThornsFoliageModelCache.Load( path );
			if ( model.IsValid && !model.IsError )
			{
				if ( !path.Equals( ThornsMineralConfig.DefaultScatterModel, StringComparison.OrdinalIgnoreCase ) )
					Log.Warning( $"[Thorns Minerals] Using fallback model '{path}' ({ThornsMineralConfig.DefaultScatterModel} unavailable)." );

				config.ScatterModel = path;
				stats.LoadedModelPath = path;
				stats.LastError = "";
				Log.Info( $"[Thorns Minerals] Scatter model resolved: '{path}' bounds={model.Bounds.Size}" );

				_loggedFirst = false;
				return model;
			}

			if ( config.VerboseDebug )
				Log.Warning( $"[Thorns Minerals] Model not available: '{path}'" );
		}

		stats.LastError = "model load failed";
		stats.LoadedModelPath = "";
		Log.Error( $"[Thorns Minerals] Failed to load scatter model (tried {paths.Count} path(s); expected {ThornsMineralConfig.DefaultScatterModel})." );
		return default;
	}

	public static List<Vector2Int> BuildChunkGrid( Terrain terrain, ThornsMineralConfig config )
		=> ThornsChunkGrid.BuildFullGrid( terrain.TerrainSize, config.ChunkSizeInches );

	public static ThornsMineralChunkData PopulateChunk(
		Scene scene,
		GameObject mineralRoot,
		Terrain terrain,
		Vector2Int cell,
		ThornsFoliageBiomeSampler sampler,
		Model model,
		ThornsMineralConfig config,
		ThornsMineralDebugStats stats )
	{
		var chunkRoot = scene.CreateObject( true );
		chunkRoot.Name = $"Minerals {cell.x}_{cell.y}";
		chunkRoot.Parent = mineralRoot;

		var center = ChunkCenter( terrain, config, cell );
		ThornsMineralChunkInstances instances = null;
		if ( config.UseInstancedMinerals )
			instances = new ThornsMineralChunkInstances { Cell = cell, Center = center };

		int stone;
		int ore;
		MineralPlacerContext.ActiveBuffer = instances;
		try
		{
			(stone, ore) = FillChunk( scene, chunkRoot, terrain, cell, center, sampler, model, config, stats );
		}
		finally
		{
			MineralPlacerContext.ActiveBuffer = null;
		}

		return new ThornsMineralChunkData
		{
			Cell = cell,
			Center = center,
			Root = chunkRoot,
			InstanceCount = stone + ore,
			StoneCount = stone,
			OreCount = ore,
			Instances = instances,
		};
	}

	static (int Stone, int Ore) FillChunk(
		Scene scene,
		GameObject chunkRoot,
		Terrain terrain,
		Vector2Int cell,
		Vector3 chunkCenter,
		ThornsFoliageBiomeSampler sampler,
		Model model,
		ThornsMineralConfig config,
		ThornsMineralDebugStats stats )
	{
		var rng = new Random( HashCode.Combine( config.WorldSeed, cell.x, cell.y, 0x4D1E ) );
		var ecology = sampler.SampleChunkEcology( chunkCenter, config.ChunkSizeInches );
		var centerSample = sampler.Sample( chunkCenter.x, chunkCenter.y );

		var rockySuit = ComputeRockySuitability( sampler, chunkCenter, centerSample, ecology );
		var oreSuit = ComputeOreSuitability( sampler, chunkCenter, centerSample, rockySuit );

		var stoneBudget = ComputeScatterBudget( config.MaxStonePerChunk, rockySuit, config.GlobalStoneDensity, config.StoneScatterMultiplier );
		var oreBudget = ComputeScatterBudget( config.MaxOrePerChunk, oreSuit, config.GlobalOreDensity, config.OreScatterMultiplier );

		if ( config.VerboseDebug && stats.ChunksProcessed < 2 )
		{
			Log.Info(
				$"[Thorns Minerals] Chunk {cell} rocky={rockySuit:F2} stone={stoneBudget} ore={oreBudget} cliff={centerSample.Cliff:F2} slope={centerSample.Slope:F2}" );
		}

		var stoneResult = ScatterKind( scene, chunkRoot, terrain, cell, sampler, model, config, stats, rng, MineralKind.Stone, stoneBudget );
		var oreResult = oreBudget > 0
			? ScatterKind( scene, chunkRoot, terrain, cell, sampler, model, config, stats, rng, MineralKind.Ore, oreBudget )
			: (Stone: 0, Ore: 0);
		return (stoneResult.Stone + oreResult.Stone, stoneResult.Ore + oreResult.Ore);
	}

	static float ComputeRockySuitability(
		ThornsFoliageBiomeSampler sampler,
		Vector3 chunkCenter,
		FoliageBiomeSample center,
		ThornsFoliageChunkEcology ecology )
	{
		var rocky = MathF.Max( center.Slope * 2.4f, center.Cliff );
		var opening = 1f - center.Opening * 0.55f;
		var alpine = 1f - center.Alpine * 0.25f;
		var mass = ecology.ForestMass * 0.15f;
		var suit = (rocky + mass).Clamp( 0.12f, 1.35f ) * opening * alpine;

		if ( sampler.TryGetDominantTerrainMaterial( chunkCenter.x, chunkCenter.y, out var mat ) )
		{
			if ( mat == TerrainMaterialPainter.MaterialRock )
				suit += 0.65f;
			else if ( mat == TerrainMaterialPainter.MaterialDirt )
				suit += 0.35f;
		}

		return suit.Clamp( 0.12f, 1.5f );
	}

	static float ComputeOreSuitability(
		ThornsFoliageBiomeSampler sampler,
		Vector3 chunkCenter,
		FoliageBiomeSample center,
		float rockySuitability )
	{
		var oreAffinity = MathF.Max( center.Alpine, center.Cliff * 0.65f );
		if ( sampler.TryGetDominantTerrainMaterial( chunkCenter.x, chunkCenter.y, out var mat ) )
		{
			if ( mat == TerrainMaterialPainter.MaterialRock )
				oreAffinity = MathF.Max( oreAffinity, 0.5f );
			else if ( mat == TerrainMaterialPainter.MaterialSnow && center.Alpine >= 0.03f )
				oreAffinity = MathF.Max( oreAffinity, 0.32f );
		}

		var opening = 1f - center.Opening * 0.35f;
		return (rockySuitability * oreAffinity * opening).Clamp( 0f, 1.5f );
	}

	static int ComputeScatterBudget( int maxPerChunk, float suitability, float globalDensity, float scatterMultiplier )
	{
		if ( maxPerChunk <= 0 || suitability < 0.08f || globalDensity <= 0.01f )
			return 0;

		var raw = maxPerChunk * suitability * globalDensity * scatterMultiplier;
		var count = (int)MathF.Round( raw );
		if ( count < 1 && suitability >= 0.12f )
			count = 1;

		return Math.Min( count, maxPerChunk );
	}

	static (int Stone, int Ore) ScatterKind(
		Scene scene,
		GameObject chunkRoot,
		Terrain terrain,
		Vector2Int cell,
		ThornsFoliageBiomeSampler sampler,
		Model model,
		ThornsMineralConfig config,
		ThornsMineralDebugStats stats,
		Random rng,
		MineralKind kind,
		int target )
	{
		if ( target <= 0 )
			return (0, 0);

		var terrainOrigin = terrain.GameObject.WorldPosition;
		var stonePlaced = 0;
		var orePlaced = 0;
		var attempts = Math.Max( target * 12, 18 );

		for ( var i = 0; i < attempts && stonePlaced + orePlaced < target; i++ )
		{
			var wx = terrainOrigin.x + (cell.x + rng.NextSingle()) * config.ChunkSizeInches;
			var wy = terrainOrigin.y + (cell.y + rng.NextSingle()) * config.ChunkSizeInches;

			if ( !AcceptPlacement( sampler, wx, wy, kind, config, stats, rng ) )
			{
				continue;
			}

			var resolvedKind = ResolveScatterKind( kind, config, rng );
			var scale = ThornsMineralSurface.ComputeUniformScale( model, resolvedKind, config, rng );
			var yaw = rng.NextSingle() * 360f;
			var hullScale = resolvedKind == MineralKind.Ore ? 0.68f : 0.74f;
			if ( ThornsWorldScatterFootprintRegistry.WouldMineralOverlap( wx, wy, yaw, model, scale, hullScale ) )
			{
				stats.BiomeRejected++;
				continue;
			}

			if ( !ThornsMineralSurface.TrySampleWorld( terrain, wx, wy, model, scale, config, out var worldPos ) )
			{
				stats.RayMisses++;
				continue;
			}

			var tilt = 8f * (rng.NextSingle() - 0.5f );
			CreateInstance( scene, chunkRoot, terrain, model, worldPos, yaw, tilt, scale, resolvedKind, config );

			if ( resolvedKind == MineralKind.Ore )
			{
				orePlaced++;
				stats.OreSpawned++;
			}
			else
			{
				stonePlaced++;
				stats.StoneSpawned++;
			}

			if ( !_loggedFirst )
			{
				_loggedFirst = true;
				Log.Info( $"[Thorns Minerals] First {resolvedKind} at {worldPos}, scale={scale:F2}, tint={(resolvedKind == MineralKind.Ore ? config.OreTint : config.StoneTint)}" );
			}
		}

		return (stonePlaced, orePlaced);
	}

	static MineralKind ResolveScatterKind( MineralKind requested, ThornsMineralConfig config, Random rng )
	{
		if ( requested != MineralKind.Stone || config.StoneToOreFraction <= 0f )
			return requested;

		return rng.NextSingle() < config.StoneToOreFraction ? MineralKind.Ore : MineralKind.Stone;
	}

	static bool AcceptPlacement(
		ThornsFoliageBiomeSampler sampler,
		float wx,
		float wy,
		MineralKind kind,
		ThornsMineralConfig config,
		ThornsMineralDebugStats stats,
		Random rng,
		bool relaxed = false )
	{
		var sample = sampler.Sample( wx, wy );

		if ( sample.Height <= 0.01f )
		{
			stats.BiomeRejected++;
			return false;
		}

		if ( sample.Height < config.MinHeightNormalized || sample.Height > config.MaxHeightNormalized )
		{
			stats.BiomeRejected++;
			return false;
		}

		if ( !relaxed && sample.Opening > 0.92f )
		{
			stats.BiomeRejected++;
			return false;
		}

		var maxSlope = kind == MineralKind.Ore ? config.MaxSlopeForOre : config.MaxSlopeForStone;
		if ( sample.Slope > maxSlope + (relaxed ? 0.12f : 0f) )
		{
			stats.BiomeRejected++;
			return false;
		}

		if ( !sampler.TryGetDominantTerrainMaterial( wx, wy, out var terrainMat ) )
			terrainMat = TerrainMaterialPainter.MaterialGrass;

		if ( !sampler.IsAboveSeaLevel( wx, wy ) )
		{
			stats.MaterialRejected++;
			return false;
		}

		if ( ThornsProcBuildingFootprintRegistry.ContainsWorldPoint( wx, wy ) )
		{
			stats.BiomeRejected++;
			return false;
		}

		if ( kind == MineralKind.Stone )
		{
			if ( terrainMat == TerrainMaterialPainter.MaterialSnow )
			{
				stats.MaterialRejected++;
				return false;
			}

			if ( !relaxed
			     && terrainMat == TerrainMaterialPainter.MaterialGrass
			     && sample.Slope < config.MinGrassSlopeForStone
			     && rng.NextSingle() > config.MeadowStoneChance )
			{
				stats.MaterialRejected++;
				return false;
			}
		}
		else
		{
			if ( terrainMat is not (TerrainMaterialPainter.MaterialRock or TerrainMaterialPainter.MaterialSnow or TerrainMaterialPainter.MaterialDirt) )
			{
				stats.MaterialRejected++;
				return false;
			}

			if ( terrainMat == TerrainMaterialPainter.MaterialDirt
			     && (sample.Slope < config.MinGrassSlopeForStone || rng.NextSingle() > 0.42f) )
			{
				stats.MaterialRejected++;
				return false;
			}

			if ( terrainMat == TerrainMaterialPainter.MaterialSnow && sample.Alpine < config.MinAlpineForOre )
			{
				stats.BiomeRejected++;
				return false;
			}
		}

		var rocky = MathF.Max( sample.Slope * 3.2f, sample.Cliff );
		var meadow = terrainMat == TerrainMaterialPainter.MaterialGrass
			? (1f - sample.ForestMass) * (1f - sample.Alpine) * config.MeadowStoneReduction
			: 0f;
		var density = rocky * (1f - meadow * 0.5f );
		if ( terrainMat == TerrainMaterialPainter.MaterialRock )
			density += 0.85f;
		else if ( terrainMat == TerrainMaterialPainter.MaterialDirt )
			density += 0.45f;

		if ( kind == MineralKind.Ore )
			density *= sample.Alpine * 1.15f + sample.Cliff * 0.55f + 0.25f;

		density = density.Clamp( 0f, 1.75f );
		var patch = ThornsProcNoise.ValueNoise( sample.Height * 41.3f, sample.Moisture * 17.7f );
		var threshold = (density * MathX.Lerp( 0.35f, 1f, patch )).Clamp( 0f, 1f );
		if ( !relaxed && rng.NextSingle() > threshold )
		{
			stats.BiomeRejected++;
			return false;
		}

		return true;
	}

	/// <summary>Guaranteed readable nodes around the local player (pocket spawn).</summary>
	public static (int Stone, int Ore) ScatterPlayerPocket(
		Scene scene,
		GameObject mineralRoot,
		Terrain terrain,
		ThornsFoliageBiomeSampler sampler,
		Model model,
		ThornsMineralConfig config,
		ThornsMineralDebugStats stats,
		Vector3 worldCenter )
	{
		if ( !terrain.IsValid() || !model.IsValid || config.PlayerPocketStoneCount <= 0 && config.PlayerPocketOreCount <= 0 )
			return (0, 0);

		var pocketRoot = scene.CreateObject( true );
		pocketRoot.Name = "Minerals Player Pocket";
		pocketRoot.Parent = mineralRoot;

		var rng = new Random( HashCode.Combine( config.WorldSeed, (int)worldCenter.x, (int)worldCenter.y, 0x50C4E ) );
		var radius = config.PlayerPocketRadiusMeters * config.InchesPerMeter;
		var stoneResult = ScatterKindInRadius( scene, pocketRoot, terrain, sampler, model, config, stats, rng, MineralKind.Stone, config.PlayerPocketStoneCount, worldCenter, radius, relaxed: true );
		var oreResult = config.PlayerPocketOreCount > 0
			? ScatterKindInRadius( scene, pocketRoot, terrain, sampler, model, config, stats, rng, MineralKind.Ore, config.PlayerPocketOreCount, worldCenter, radius * 0.85f, relaxed: true )
			: (Stone: 0, Ore: 0);
		return (stoneResult.Stone + oreResult.Stone, stoneResult.Ore + oreResult.Ore);
	}

	static (int Stone, int Ore) ScatterKindInRadius(
		Scene scene,
		GameObject parent,
		Terrain terrain,
		ThornsFoliageBiomeSampler sampler,
		Model model,
		ThornsMineralConfig config,
		ThornsMineralDebugStats stats,
		Random rng,
		MineralKind kind,
		int target,
		Vector3 center,
		float radiusInches,
		bool relaxed )
	{
		if ( target <= 0 )
			return (0, 0);

		var stonePlaced = 0;
		var orePlaced = 0;
		var attempts = Math.Max( target * 24, 48 );

		for ( var i = 0; i < attempts && stonePlaced + orePlaced < target; i++ )
		{
			var angle = rng.NextSingle() * MathF.Tau;
			var dist = MathF.Sqrt( rng.NextSingle() ) * radiusInches;
			var wx = center.x + MathF.Cos( angle ) * dist;
			var wy = center.y + MathF.Sin( angle ) * dist;

			if ( !AcceptPlacement( sampler, wx, wy, kind, config, stats, rng, relaxed ) )
				continue;

			var resolvedKind = ResolveScatterKind( kind, config, rng );
			var scale = ThornsMineralSurface.ComputeUniformScale( model, resolvedKind, config, rng );
			var yaw = rng.NextSingle() * 360f;
			var hullScale = resolvedKind == MineralKind.Ore ? 0.68f : 0.74f;
			if ( !relaxed && ThornsWorldScatterFootprintRegistry.WouldMineralOverlap( wx, wy, yaw, model, scale, hullScale ) )
				continue;

			if ( !ThornsMineralSurface.TrySampleWorld( terrain, wx, wy, model, scale, config, out var worldPos ) )
			{
				stats.RayMisses++;
				continue;
			}

			var tilt = 10f * (rng.NextSingle() - 0.5f );
			CreateInstance( scene, parent, terrain, model, worldPos, yaw, tilt, scale, resolvedKind, config );

			if ( resolvedKind == MineralKind.Ore )
			{
				orePlaced++;
				stats.OreSpawned++;
			}
			else
			{
				stonePlaced++;
				stats.StoneSpawned++;
			}
		}

		return (stonePlaced, orePlaced);
	}

	public static Vector3 GetChunkCenter( Terrain terrain, ThornsMineralConfig config, Vector2Int cell ) =>
		ChunkCenter( terrain, config, cell );

	static Vector3 ChunkCenter( Terrain terrain, ThornsMineralConfig config, Vector2Int cell )
		=> ThornsChunkGrid.CellCenter( terrain.GameObject.WorldPosition, config.ChunkSizeInches, cell );

	static void CreateInstance(
		Scene scene,
		GameObject parent,
		Terrain terrain,
		Model model,
		Vector3 worldPos,
		float yawDegrees,
		float tiltDegrees,
		float scale,
		MineralKind kind,
		ThornsMineralConfig config )
	{
		var instance = scene.CreateObject( true );
		instance.Name = kind == MineralKind.Ore ? "Scatter Ore" : "Scatter Stone";
		instance.Parent = parent;

		var terrainTransform = terrain.GameObject.WorldTransform;
		instance.LocalPosition = terrainTransform.PointToLocal( worldPos );
		instance.LocalRotation = Rotation.FromYaw( yawDegrees ) * new Angles( tiltDegrees, 0f, 0f ).ToRotation();
		instance.LocalScale = new Vector3( scale );

		var renderer = instance.Components.Create<ModelRenderer>();
		renderer.Model = model;
		renderer.MaterialOverride = ThornsMineralTintMaterials.Get( model, kind, config );
		renderer.Tint = Color.White;
		renderer.Enabled = true;
		renderer.RenderType = ModelRenderer.ShadowRenderType.Off;

		var hullScale = kind == MineralKind.Ore ? 0.68f : 0.74f;
		TerraingenAnchoredPhysics.EnsureVisualMeshBox( instance, model, hullScale );

		var worldRotation = instance.WorldRotation;
		var worldScaleVec = instance.WorldScale;
		var world = MineralPlacerContext.ActiveWorld ?? ThornsMineralWorldService.Instance;
		var nodeId = world?.RegisterNode( instance, worldPos, worldRotation, worldScaleVec, kind ) ?? -1;

		var buffer = MineralPlacerContext.ActiveBuffer;
		if ( buffer is not null && config.UseInstancedMinerals && nodeId >= 0 )
		{
			var xf = new Transform( worldPos, worldRotation, new Vector3( scale ) );
			if ( kind == MineralKind.Ore )
			{
				buffer.Ore.Add( xf );
				buffer.OreNodeIds.Add( nodeId );
			}
			else
			{
				buffer.Stone.Add( xf );
				buffer.StoneNodeIds.Add( nodeId );
			}

			renderer.Enabled = false;
			ThornsWorldScatterFootprintRegistry.RegisterMineral( worldPos, yawDegrees, model, scale, hullScale );
			return;
		}

		ThornsWorldScatterFootprintRegistry.RegisterMineral( worldPos, yawDegrees, model, scale, hullScale );
	}
}
