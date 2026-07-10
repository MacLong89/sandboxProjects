namespace Terraingen.Buildings;

using Terraingen.Boulders;
using Terraingen.Buildings.Settlement;
using Terraingen.Core;
using Terraingen.Foliage;
using Terraingen.Physics;
using Terraingen.Rendering;
using Terraingen.TerrainGen;
using Terraingen.World;

/// <summary>Light tree/boulder scatter around each POI gallery slot for movement testing.</summary>
public static class ThornsSettlementTestObstacleScatter
{
	const float TreeRingMin = 720f;
	const float TreeRingMax = 1500f;
	const float BoulderRingMin = 520f;
	const float BoulderRingMax = 1200f;
	const float BuildingMarginInches = 480f;

	/// <summary>Fixed row near spawn: one tree per species for trunk/collision tuning.</summary>
	const float SpeciesDebugSpacingInches = 1100f;
	const float SpeciesDebugForwardInches = 1400f;

	static readonly FoliageSpecies[] SpeciesDebugOrder =
	{
		FoliageSpecies.Pine,
		FoliageSpecies.Aspen,
		FoliageSpecies.Oak,
	};

	public static int HostScatterGalleryObstacles(
		Scene scene,
		GameObject host,
		Terrain terrain,
		int worldSeed,
		int treesPerPoi,
		int bouldersPerPoi )
	{
		if ( scene is null || !scene.IsValid() || !host.IsValid() || !terrain.IsValid() )
			return 0;

		treesPerPoi = Math.Clamp( treesPerPoi, 0, 16 );
		bouldersPerPoi = Math.Clamp( bouldersPerPoi, 0, 10 );

		var foliageConfig = new ThornsFoliageConfig { FoliageSeed = worldSeed };
		var treeModels = ThornsFoliagePlacer.LoadModels( foliageConfig, null );
		if ( !treeModels.IsValid )
		{
			Log.Warning( "[Thorns Settlement Test] Tree models unavailable — skipping obstacle scatter." );
			return 0;
		}

		var boulderConfig = new ThornsBoulderConfig { WorldSeed = worldSeed };
		var boulderModels = LoadBoulderModels( boulderConfig );
		if ( boulderModels.Count == 0 && bouldersPerPoi > 0 )
			Log.Warning( "[Thorns Settlement Test] Boulder models unavailable — skipping boulders." );

		var root = scene.CreateObject( true );
		root.Name = "Settlement Test Obstacles";
		root.Parent = host;

		var terrainCenter = ThornsWorldInterest.ResolveTerrainCenter( terrain );
		var placed = SpawnSpeciesDebugTrees( scene, root, terrain, foliageConfig, treeModels, terrainCenter, worldSeed );

		if ( treesPerPoi == 0 && bouldersPerPoi == 0 )
			return placed;

		for ( var slot = 0; slot < ThornsSettlementTestSceneBootstrap.GallerySettlementCount; slot++ )
		{
			var poiCenter = terrainCenter + ThornsSettlementTestSceneBootstrap.GetGalleryOffset( slot );
			var rng = new Random( HashCode.Combine( worldSeed, slot, 0x5E77_1E0B ) );

			for ( var t = 0; t < treesPerPoi; t++ )
			{
				if ( !TryPickScatterPoint( poiCenter, rng, TreeRingMin, TreeRingMax, out var wx, out var wy ) )
					continue;

				var species = SpeciesDebugOrder[t % SpeciesDebugOrder.Length];
				if ( TryPlaceTree( scene, root, terrain, foliageConfig, treeModels, species, wx, wy, rng ) )
					placed++;
			}

			for ( var b = 0; b < bouldersPerPoi; b++ )
			{
				if ( boulderModels.Count == 0 )
					break;

				if ( !TryPickScatterPoint( poiCenter, rng, BoulderRingMin, BoulderRingMax, out var wx, out var wy ) )
					continue;

				var model = boulderModels[rng.Next( boulderModels.Count )];
				var scale = ComputeBoulderScale( model, boulderConfig, rng );
				if ( !TrySampleBoulder( terrain, wx, wy, model, scale, boulderConfig, out var surface ) )
					continue;

				if ( TryPlaceBoulder( scene, root, terrain, model, surface, scale, rng ) )
					placed++;
			}
		}

		ThornsTreeTrunkCollision.RefreshAllInScene( scene );
		ThornsBoulderSphereCollision.RefreshAllInScene( scene );
		return placed;
	}

	static int SpawnSpeciesDebugTrees(
		Scene scene,
		GameObject root,
		Terrain terrain,
		ThornsFoliageConfig config,
		ThornsFoliagePlacer.FoliageModelSet models,
		Vector3 terrainCenter,
		int worldSeed )
	{
		var placed = 0;
		var debugRoot = scene.CreateObject( true );
		debugRoot.Name = "Settlement Test Tree Species";
		debugRoot.Parent = root;

		for ( var i = 0; i < SpeciesDebugOrder.Length; i++ )
		{
			var species = SpeciesDebugOrder[i];
			var wx = terrainCenter.x + (i - 1) * SpeciesDebugSpacingInches;
			var wy = terrainCenter.y + SpeciesDebugForwardInches;
			var rng = new Random( HashCode.Combine( worldSeed, (int)species, 0x7E33_1A0F ) );

			if ( TryPlaceTree( scene, debugRoot, terrain, config, models, species, wx, wy, rng, fixedYaw: 0f ) )
				placed++;
		}

		if ( placed > 0 )
		{
			Log.Info(
				$"[Thorns Settlement Test] Species debug trees ({placed}/{SpeciesDebugOrder.Length}): "
				+ $"Pine/Aspen/Oak row {SpeciesDebugForwardInches:F0}\" forward from terrain center, "
				+ $"{SpeciesDebugSpacingInches:F0}\" apart." );
		}

		return placed;
	}

	static List<Model> LoadBoulderModels( ThornsBoulderConfig config )
	{
		var models = new List<Model>( 3 );
		foreach ( var path in config.ModelPaths() )
		{
			if ( string.IsNullOrWhiteSpace( path ) )
				continue;

			var model = ThornsFoliageModelCache.Load( path.Trim() );
			if ( model.IsValid && !model.IsError )
				models.Add( model );
		}

		return models;
	}

	static bool TryPickScatterPoint( Vector3 poiCenter, Random rng, float minRadius, float maxRadius, out float wx, out float wy )
	{
		for ( var attempt = 0; attempt < 12; attempt++ )
		{
			var angle = rng.NextSingle() * MathF.Tau;
			var radius = MathX.Lerp( minRadius, maxRadius, MathF.Sqrt( rng.NextSingle() ) );
			wx = poiCenter.x + MathF.Cos( angle ) * radius;
			wy = poiCenter.y + MathF.Sin( angle ) * radius;

			if ( !ThornsProcBuildingFootprintRegistry.ContainsWorldPoint( wx, wy, BuildingMarginInches ) )
				return true;
		}

		wx = wy = 0f;
		return false;
	}

	static bool TrySampleTerrain( Terrain terrain, float wx, float wy, out Vector3 worldPosition )
	{
		worldPosition = default;
		var rayStart = new Vector3( wx, wy, terrain.TerrainHeight * 2.5f + terrain.GameObject.WorldPosition.z );
		var ray = new Ray( rayStart, Vector3.Down );

		if ( !terrain.RayIntersects( ray, terrain.TerrainHeight * 5f, out var localHit ) )
			return false;

		worldPosition = terrain.GameObject.WorldTransform.PointToWorld( localHit );
		return true;
	}

	static bool TryPlaceTree(
		Scene scene,
		GameObject root,
		Terrain terrain,
		ThornsFoliageConfig config,
		ThornsFoliagePlacer.FoliageModelSet models,
		FoliageSpecies species,
		float wx,
		float wy,
		Random rng,
		float? fixedYaw = null )
	{
		var model = models.Get( species );
		if ( !model.IsValid )
			return false;

		var scale = ComputeTreeScale( model, species, config, rng );
		if ( !ThornsFoliageSurface.TrySampleWorld( terrain, wx, wy, model, scale, species, config, out var worldPos ) )
			return false;

		var yaw = fixedYaw ?? (rng.NextSingle() * 360f);

		var instance = scene.CreateObject( true );
		instance.Name = $"Settlement Test Tree ({species})";
		instance.Parent = root;
		instance.LocalPosition = terrain.GameObject.WorldTransform.PointToLocal( worldPos );
		instance.LocalRotation = Rotation.FromYaw( yaw );
		instance.LocalScale = scale;

		var renderer = instance.Components.Create<ModelRenderer>();
		renderer.Model = model;
		renderer.Enabled = true;
		renderer.RenderType = ModelRenderer.ShadowRenderType.On;
		ThornsWorldShadowUtil.EnableWorldShadows( renderer );

		var lodTag = instance.Components.Create<ThornsFoliageInstance>();
		lodTag.Species = species;
		lodTag.Renderer = renderer;

		ThornsTreeTrunkCollision.Apply( instance, model, scale.x );
		lodTag.Collider = TerraingenAnchoredPhysics.FindTreeTrunkCollider( instance );
		return true;
	}

	static Vector3 ComputeTreeScale( Model model, FoliageSpecies species, ThornsFoliageConfig config, Random rng )
	{
		var targetHeight = species switch
		{
			FoliageSpecies.Oak => config.OakTargetHeightInches,
			FoliageSpecies.Aspen => config.AspenTargetHeightInches,
			_ => config.PineTargetHeightInches,
		};
		var uniform = ThornsFoliageCloudModels.ComputeUniformScale(
			model,
			targetHeight,
			config,
			rng );
		return ThornsNatureScaleVariance.Apply( new Vector3( uniform ), rng );
	}

	static bool TrySampleBoulder(
		Terrain terrain,
		float wx,
		float wy,
		Model model,
		float scale,
		ThornsBoulderConfig config,
		out Vector3 worldPosition )
	{
		worldPosition = default;
		if ( !TrySampleTerrain( terrain, wx, wy, out worldPosition ) )
			return false;

		var bounds = model.Bounds;
		var lift = Math.Max( 0f, -bounds.Mins.z * scale );
		var embed = bounds.Size.z * scale * config.GroundEmbedFraction + config.GroundSinkOffsetInches;
		worldPosition += Vector3.Up * (lift - embed);
		return true;
	}

	static float ComputeBoulderScale( Model model, ThornsBoulderConfig config, Random rng )
	{
		var meshHeight = Math.Max( model.Bounds.Size.z, 1f );
		var targetHeight = MathX.Lerp( config.MinTargetHeightInches, config.MaxTargetHeightInches, rng.NextSingle() );
		var scale = targetHeight / meshHeight;
		return Math.Max( ThornsNatureScaleVariance.Apply( scale, rng ), 0.05f );
	}

	static bool TryPlaceBoulder(
		Scene scene,
		GameObject root,
		Terrain terrain,
		Model model,
		Vector3 worldPosition,
		float scale,
		Random rng )
	{
		var yaw = rng.NextSingle() * 360f;
		if ( ThornsWorldScatterFootprintRegistry.WouldBoulderOverlap(
			     worldPosition.x,
			     worldPosition.y,
			     yaw,
			     model,
			     scale ) )
			return false;

		var obj = scene.CreateObject( true );
		obj.Name = "Settlement Test Boulder";
		obj.Parent = root;
		obj.LocalPosition = terrain.GameObject.WorldTransform.PointToLocal( worldPosition );
		obj.LocalRotation = Rotation.FromYaw( yaw );
		obj.LocalScale = new Vector3( scale );
		obj.Tags.Add( "boulder" );

		var renderer = obj.Components.Create<ModelRenderer>();
		renderer.Model = model;
		renderer.Enabled = true;
		ThornsWorldShadowUtil.EnableWorldShadows( renderer );

		TerraingenAnchoredPhysics.EnsureSolidTags( obj );
		ThornsBoulderSphereCollision.Apply( obj, model, scale );
		ThornsWorldScatterFootprintRegistry.RegisterBoulder( worldPosition, yaw, model, scale );
		return true;
	}
}
