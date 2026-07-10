#nullable disable

using System.Buffers;
using Terraingen;
using Terraingen.Clutter;
using Terraingen.Foliage;
using Terraingen.Rendering;
using Terraingen.TerrainGen;

namespace Sandbox;

/// <summary>
/// Hosts heightmap-driven Colorado terrain (terraingen) inside Thorns: Sandbox.Terrain visuals,
/// shared height sampling for world-gen, and terraingen foliage/clutter instead of decor fluff.
/// </summary>
public static class ThornsTerraingenTerrainRuntime
{
	public const string TerrainChildName = "TerraingenTerrain";
	const string WaterChildName = "TerraingenWater";

	static ThornsTerrainConfig _terrainConfig;
	static ThornsFoliageConfig _foliageConfig;
	static ThornsClutterConfig _clutterConfig;
	static HeightmapField _field;
	static int _fieldSeed = int.MinValue;
	/// <summary>Bump when terraingen height repair / sculpt / cliff shading changes so cached fields regenerate.</summary>
	const int FieldCacheRevision = 10;
	static int _fieldCacheRevision;
	static GameObject _waterSheet;

	public static void BindConfigs(
		ThornsTerrainConfig terrainConfig,
		ThornsFoliageConfig foliageConfig,
		ThornsClutterConfig clutterConfig )
	{
		_terrainConfig = terrainConfig ?? new ThornsTerrainConfig();
		_foliageConfig = foliageConfig ?? new ThornsFoliageConfig();
		_clutterConfig = clutterConfig ?? new ThornsClutterConfig();
		ThornsTerraingenParity.ApplyTerrainConfig( _terrainConfig, onlyIfUnmodified: true );
		ThornsTerraingenParity.ApplyFoliageDensity( _foliageConfig );
	}

	public static ThornsTerrainConfig ActiveTerrainConfig => _terrainConfig ?? new ThornsTerrainConfig();

	public static float ComputeTerrainWorldSize( ThornsTerrainConfig config )
	{
		config ??= ActiveTerrainConfig;
		var resolution = ThornsTerrainGenerator.RoundDownToPowerOfTwo( config.TerrainWorldResolution );
		return resolution * config.WorldScaleInches * config.HorizontalScale;
	}

	public static void ApplyToNetSpec( ThornsTerrainNetSpec spec, ThornsTerrainConfig config, int worldSeed )
	{
		config ??= ActiveTerrainConfig;
		var worldSize = ComputeTerrainWorldSize( config );
		var res = ThornsTerrainGenerator.RoundDownToPowerOfTwo( config.TerrainResolution );

		spec.UseTerraingenWorld = true;
		spec.UseTerraingenFoliage = true;
		spec.Seed = worldSeed;
		spec.TerraingenWorldSeed = worldSeed;
		spec.WorldWidth = worldSize;
		spec.WorldDepth = worldSize;
		spec.HeightmapResolutionX = res;
		spec.HeightmapResolutionZ = res;
		spec.HeightMultiplier = config.MaxTerrainHeightInches;
		spec.WaterLevelWorldZ = config.SeaLevelNormalized * config.MaxTerrainHeightInches;
		spec.CenterOnWorldOrigin = config.CenterAtWorldOrigin;
		spec.EnableCoastalEdgeFalloff = false;
		spec.EnableSmoothing = false;
		spec.SmoothingPasses = 0;
		// Sea visuals come from <see cref="ThornsWaterSheet"/> (live material tiling). Baked mesh water here doubled the layer and tiled UVs twice.
		spec.EnableSeaLevelWaterSheet = false;
		spec.WaterMaterialPath = string.IsNullOrWhiteSpace( config.WaterSurfaceMaterial )
			? "materials/water.vmat"
			: config.WaterSurfaceMaterial.Trim();
		spec.WaterSurfaceUvRepeat = config.WaterTextureTileRepeat;
		spec.DecorGenerateFoliageFluff = false;
		spec.DecorGrass ??= ThornsTerrainDecorGrassNet.EngineDefaults();
		spec.DecorGrass.ScatterGrassFoliage = false;
	}

	public static HeightmapField GetOrGenerateField( int worldSeed )
	{
		var config = ActiveTerrainConfig;
		if ( _field is not null && _fieldSeed == worldSeed && _fieldCacheRevision == FieldCacheRevision )
			return _field;

		config.WorldSeed = worldSeed;
		_fieldSeed = worldSeed;
		_fieldCacheRevision = FieldCacheRevision;
		_field = ThornsTerrainGenerator.GenerateHeightField( config );
		return _field;
	}

	public static void FillHeightmapBase( in ThornsTerrainNetSpec spec, float[] heightsOut, HeightmapField field )
	{
		var config = ActiveTerrainConfig;
		var rx = Math.Max( 2, spec.HeightmapResolutionX );
		var rz = Math.Max( 2, spec.HeightmapResolutionZ );
		ThornsTerrainGeometry.GetExtents( spec, out var worldW, out var worldD );
		var cellX = worldW / (rx - 1f);
		var cellY = worldD / (rz - 1f);
		var halfW = spec.CenterOnWorldOrigin ? worldW * 0.5f : 0f;
		var halfD = spec.CenterOnWorldOrigin ? worldD * 0.5f : 0f;
		var maxZ = config.MaxTerrainHeightInches;

		for ( var z = 0; z < rz; z++ )
		{
			var planeY = z * cellY - halfD;
			var rowBase = z * rx;
			for ( var x = 0; x < rx; x++ )
			{
				var planeX = x * cellX - halfW;
				var u = (planeX + halfW) / worldW;
				var v = (planeY + halfD) / worldD;
				heightsOut[rowBase + x] = field.SampleBilinear( u, v ) * maxZ;
			}
		}
	}

	public static void TryBindConfigsFromScene( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return;

		foreach ( var ts in scene.GetAllComponents<ThornsTerrainSystem>() )
		{
			if ( !ts.IsValid() || !ts.Enabled )
				continue;

			BindConfigs(
				ts.TerraingenConfig ?? new ThornsTerrainConfig(),
				ts.TerraingenFoliageConfig ?? new ThornsFoliageConfig(),
				ts.TerraingenClutterConfig ?? new ThornsClutterConfig() );
			return;
		}
	}

	public static void RebuildChunkVisuals( GameObject chunkRoot, Scene scene, in ThornsTerrainNetSpec spec, long contentHash = 0 )
	{
		if ( !chunkRoot.IsValid() || scene is null || !scene.IsValid() )
			return;

		TryBindConfigsFromScene( scene );

		if ( !Terraingen.ThornsMultiplayer.ShouldGenerateWorld(
			     ActiveTerrainConfig.HostAuthoritative,
			     ActiveTerrainConfig.ClientsGenerateDeterministic ) )
			return;

		try
		{
			ThornsLoadingScreenHero.Show( "Sculpting Thorns Terrain…" );
			var field = GetOrGenerateField( spec.TerraingenWorldSeed != 0 ? spec.TerraingenWorldSeed : spec.Seed );
			ApplySandboxTerrain( chunkRoot, field, scene, in spec, contentHash );
			// Foliage/clutter streaming is heavy — run after collision mesh is live so load hitches split across frames.
			var foliageRoot = chunkRoot;
			var foliageField = field;
			var foliageScene = scene;
			ThornsDeferredHostSpawnQueue.EnsureOn( chunkRoot, workBudgetPerFrame: 1 )
				.EnqueueOrRunNow( () => BeginFoliageAndClutter( foliageRoot, foliageField, foliageScene ) );
		}
		catch ( Exception e )
		{
			Log.Error( $"[Thorns Terraingen] Rebuild failed: {e.Message}" );
		}
		finally
		{
			ThornsWorldBootGate.NotifyTerrainRebuildFinished( scene );
			if ( !ThornsWorldBootGate.BlocksLocalOwnerPresentation )
				ThornsLoadingScreenHero.Clear();
		}
	}

	static void ApplySandboxTerrain( GameObject chunkRoot, HeightmapField field, Scene scene, in ThornsTerrainNetSpec spec, long contentHash = 0 )
	{
		var config = ActiveTerrainConfig;
		var terrainGo = chunkRoot.Children.FirstOrDefault( c => c.IsValid() && c.Name == TerrainChildName );
		if ( !terrainGo.IsValid() )
		{
			terrainGo = new GameObject( true, TerrainChildName );
			terrainGo.SetParent( chunkRoot );
		}

		ThornsAnchoredWorldPhysics.EnsureWorldSolidTags( terrainGo );

		var terrain = terrainGo.Components.Get<Terrain>( FindMode.EverythingInSelf )
		              ?? terrainGo.Components.Create<Terrain>();
		var storage = terrain.Storage ?? new TerrainStorage();
		var resolution = ThornsTerrainGenerator.RoundDownToPowerOfTwo( config.TerrainResolution );
		var worldResolution = Math.Max( resolution, ThornsTerrainGenerator.RoundDownToPowerOfTwo( config.TerrainWorldResolution ) );

		storage.SetResolution( resolution );
		storage.TerrainSize = worldResolution * config.WorldScaleInches * config.HorizontalScale;
		storage.TerrainHeight = config.MaxTerrainHeightInches;
		TerrainMaterialLibrary.PopulateMaterials( storage, config );
		storage.HeightMap = BuildTerrainHeightmapForSpec( in spec, resolution, config.MaxTerrainHeightInches, contentHash );
		TerrainMaterialPainter.InitializeDefaultControlMap( storage );
		if ( storage.Materials.Count > 0 )
			TerrainMaterialPainter.PaintControlMap( storage, field, config );

		terrain.Storage = storage;
		terrain.Create();
		ThornsTerrainCliffShader.Apply( terrain );
		terrain.UpdateMaterialsBuffer();
		terrain.SyncGPUTexture();

		ThornsTerraingenTerrainQueries.InvalidateTerrainCache( scene );

		TerrainPlacement.ApplyOriginOffset( terrainGo, storage.TerrainSize, config.CenterAtWorldOrigin );

		var padCount = spec.ProcBuildingTerrainPads?.Count ?? 0;
		var sculptCount = 0;
		if ( padCount > 0 && spec.ProcBuildingTerrainPads is { } padList )
		{
			for ( var i = 0; i < padList.Count; i++ )
			{
				if ( padList[i].SculptHeightmap )
					sculptCount++;
			}

			Log.Info(
				$"[Thorns Terraingen] Heightmap baked with {padCount} proc-building pad(s) ({sculptCount} sculpt collision, {padCount - sculptCount} snap-only)." );
		}

		_waterSheet = ThornsWaterSheet.Sync(
			scene,
			terrainGo,
			_waterSheet,
			config,
			storage.TerrainSize,
			storage.TerrainHeight,
			terrain,
			field );

		TryDrawTerrainRepairDebug( scene, chunkRoot, in spec );
	}

	static void TryDrawTerrainRepairDebug( Scene scene, GameObject chunkRoot, in ThornsTerrainNetSpec spec ) =>
		ThornsTerrainRepairDebug.TryDrawIfEnabled( scene, chunkRoot, in spec );

	/// <summary>
	/// Uses <see cref="ThornsTerrainGeometry.FillHeightmap"/> so roads, settlements, and <see cref="ThornsTerrainNetSpec.ProcBuildingTerrainPads"/>
	/// flatten walk collision under proc buildings (raw <see cref="HeightmapField"/> alone leaves hills clipping through interiors).
	/// </summary>
	static ushort[] BuildTerrainHeightmapForSpec(
		in ThornsTerrainNetSpec spec,
		int resolution,
		float terrainHeightInches,
		long contentHash = 0 )
	{
		var rx = Math.Max( 2, spec.HeightmapResolutionX );
		var rz = Math.Max( 2, spec.HeightmapResolutionZ );
		var specCount = rx * rz;
		var storageCount = resolution * resolution;

		if ( specCount == storageCount )
		{
			var heights = ArrayPool<float>.Shared.Rent( specCount );
			try
			{
				ThornsTerrainGeometry.FillHeightmap( in spec, heights, contentHash );
				return FloatHeightsToTerrainStorage( heights, specCount, terrainHeightInches );
			}
			finally
			{
				ArrayPool<float>.Shared.Return( heights );
			}
		}

		// Resolution mismatch fallback — still better than ignoring pads entirely.
		var fallback = ArrayPool<float>.Shared.Rent( specCount );
		try
		{
			ThornsTerrainGeometry.FillHeightmap( in spec, fallback, contentHash );
			return ResampleFloatHeightsToTerrainStorage( fallback, rx, rz, resolution, terrainHeightInches );
		}
		finally
		{
			ArrayPool<float>.Shared.Return( fallback );
		}
	}

	static ushort[] FloatHeightsToTerrainStorage( float[] heights, int count, float terrainHeightInches )
	{
		var maxZ = MathF.Max( 1f, terrainHeightInches );
		var data = new ushort[count];
		for ( var i = 0; i < count; i++ )
			data[i] = (ushort)Math.Clamp( (int)(heights[i] / maxZ * 65535f), 0, 65535 );

		return data;
	}

	static ushort[] ResampleFloatHeightsToTerrainStorage(
		float[] src,
		int srcRx,
		int srcRz,
		int dstResolution,
		float terrainHeightInches )
	{
		var dst = new ushort[dstResolution * dstResolution];
		var maxZ = MathF.Max( 1f, terrainHeightInches );
		for ( var z = 0; z < dstResolution; z++ )
		{
			var v = dstResolution <= 1 ? 0f : z / (float)(dstResolution - 1);
			var sz = Math.Clamp( (int)MathF.Round( v * (srcRz - 1) ), 0, srcRz - 1 );
			for ( var x = 0; x < dstResolution; x++ )
			{
				var u = dstResolution <= 1 ? 0f : x / (float)(dstResolution - 1);
				var sx = Math.Clamp( (int)MathF.Round( u * (srcRx - 1) ), 0, srcRx - 1 );
				var h = src[sz * srcRx + sx];
				dst[z * dstResolution + x] = (ushort)Math.Clamp( (int)(h / maxZ * 65535f), 0, 65535 );
			}
		}

		return dst;
	}

	static void BeginFoliageAndClutter( GameObject chunkRoot, HeightmapField field, Scene scene )
	{
		if ( !scene.IsValid() || !chunkRoot.IsValid() )
			return;

		if ( !Terraingen.ThornsMultiplayer.ShouldGenerateWorld(
			     ActiveTerrainConfig.HostAuthoritative,
			     ActiveTerrainConfig.ClientsGenerateDeterministic ) )
			return;

		var terrainGo = chunkRoot.Children.FirstOrDefault( c => c.IsValid() && c.Name == TerrainChildName );
		if ( !terrainGo.IsValid() )
			return;

		var terrain = terrainGo.Components.Get<Terrain>( FindMode.EverythingInSelf );
		if ( !terrain.IsValid() )
			return;

		var config = ActiveTerrainConfig;
		var systemGo = chunkRoot;
		ThornsTerrainSystem terrainSystem = default;
		foreach ( var ts in scene.GetAllComponents<ThornsTerrainSystem>() )
		{
			if ( !ts.IsValid() || !ts.Enabled )
				continue;

			terrainSystem = ts;
			systemGo = ts.GameObject;
			break;
		}

		var visibilityTier = ThornsVisibilityTier.Balanced;
		if ( terrainSystem.IsValid() )
			visibilityTier = terrainSystem.VisibilityTier;

		ThornsVisibilityPresets.ApplyToLocalPawnCamera( scene, visibilityTier );

		var foliageConfig = _foliageConfig ?? new ThornsFoliageConfig();
		ThornsVisibilityPresets.ApplyFoliage( foliageConfig, visibilityTier );

		var useFoliage2WorldTrees = false;
		ThornsTerrainNetSpec netSpec = null;
		var chunk = chunkRoot.Components.Get<ThornsTerrainChunk>( FindMode.EverythingInSelf );
		if ( chunk.IsValid() && chunk.TryGetResolvedNetSpec( out netSpec ) )
			useFoliage2WorldTrees = netSpec.UseTerraingenFoliage;

		var hostPopulatesHarvestTrees = !useFoliage2WorldTrees
		                                || !Terraingen.ThornsMultiplayer.IsNetworked
		                                || Terraingen.ThornsMultiplayer.IsHostOrOffline;

		if ( useFoliage2WorldTrees || hostPopulatesHarvestTrees )
			foliageConfig.SpawnAsHarvestableWoodTrees = true;

		var sharedSampler = new ThornsFoliageBiomeSampler( field, terrain, config, foliageConfig );

		var foliage = systemGo.Components.Get<ThornsFoliageFoundation>( FindMode.EverythingInSelfAndDescendants );
		if ( hostPopulatesHarvestTrees )
		{
			foliage ??= systemGo.Components.Create<ThornsFoliageFoundation>();
			foliage.Config = foliageConfig;
			foliage.BeginPopulate( terrain, field, config, sharedSampler );
		}
		else
			foliage?.Clear();

		// Retired mixed grass/rock streamer. Clear hotloaded instances before the client grass renderer takes ownership.
		systemGo.Components.Get<ThornsClutterFoundation>( FindMode.EverythingInSelfAndDescendants )?.Clear();

		var grass = systemGo.Components.Get<ClientGrassRenderer>( FindMode.EverythingInSelfAndDescendants )
		            ?? systemGo.Components.Create<ClientGrassRenderer>();
		var grassConfig = _clutterConfig ?? new ThornsClutterConfig();
		ThornsVisibilityPresets.ApplyClutter( grassConfig, visibilityTier );

		if ( grassConfig.Enabled && netSpec is not null )
			grass.BeginStreaming( terrain, field, config, netSpec, chunkRoot, grassConfig, sharedSampler );
	}

	public static void ClearUnderChunk( GameObject chunkRoot )
	{
		if ( !chunkRoot.IsValid() )
			return;

		foreach ( var ch in chunkRoot.Children )
		{
			if ( !ch.IsValid() )
				continue;
			if ( ch.Name is TerrainChildName or WaterChildName )
				ch.Destroy();
		}

		if ( _waterSheet.IsValid() )
		{
			_waterSheet.Destroy();
			_waterSheet = default;
		}
	}
}
