namespace Terraingen.TerrainGen;

using Terraingen.Rendering;

/// <summary>
/// Flat water surface at sea level — separate from terrain splat painting.
/// </summary>
public static class ThornsWaterSheet
{
	const string WaterObjectName = "Thorns Water";

	public static GameObject Sync(
		Scene scene,
		GameObject terrainRoot,
		GameObject existing,
		ThornsTerrainConfig config,
		float terrainWorldSize,
		float terrainMaxHeight,
		Terrain terrain,
		HeightmapField field )
	{
		if ( !config.CreateWaterSheet )
		{
			if ( existing.IsValid() )
				existing.Destroy();
			return default;
		}

		var water = existing;
		if ( !water.IsValid() )
		{
			water = scene.CreateObject( true );
			water.Name = WaterObjectName;
			water.Parent = terrainRoot;
		}

		var seaZ = config.SeaLevelNormalized * terrainMaxHeight;
		water.LocalPosition = new Vector3( terrainWorldSize * 0.5f, terrainWorldSize * 0.5f, seaZ );
		water.LocalRotation = Rotation.Identity;

		var margin = terrainWorldSize * Math.Max( 0f, config.WaterPlaneMarginFraction );
		var planeWorldSize = terrainWorldSize + margin * 2f;
		var planeScale = planeWorldSize / Math.Max( 1f, config.WaterPlaneBaseSizeInches );
		water.LocalScale = new Vector3( planeScale, planeScale, 1f );

		var renderer = water.Components.Get<ModelRenderer>( FindMode.EverythingInSelf ) ?? water.Components.Create<ModelRenderer>();
		renderer.Model = Model.Load( config.WaterPlaneModel );
		renderer.RenderType = ModelRenderer.ShadowRenderType.Off;

		var surface = water.Components.Get<ThornsWaterSurface>( FindMode.EverythingInSelf ) ?? water.Components.Create<ThornsWaterSurface>();
		surface.Initialize( terrain, field, config );

		return water;
	}
}
