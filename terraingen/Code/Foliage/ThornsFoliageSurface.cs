namespace Terraingen.Foliage;

/// <summary>
/// Snaps foliage to the live terrain mesh and corrects pivot/embed height.
/// </summary>
public static class ThornsFoliageSurface
{
	public static bool TrySampleWorld(
		Terrain terrain,
		float worldX,
		float worldY,
		Model model,
		Vector3 scale,
		FoliageSpecies species,
		ThornsFoliageConfig config,
		out Vector3 worldPosition )
	{
		worldPosition = default;

		if ( !terrain.IsValid() || !model.IsValid )
			return false;

		var maxHeight = terrain.TerrainHeight;
		var rayStart = new Vector3( worldX, worldY, maxHeight * 2.5f );
		var ray = new Ray( rayStart, Vector3.Down );

		if ( !terrain.RayIntersects( ray, maxHeight * 5f, out var localHit ) )
			return false;

		worldPosition = terrain.GameObject.WorldTransform.PointToWorld( localHit );
		worldPosition += Vector3.Up * ComputeGroundLift( model, scale, species, config );
		return true;
	}

	static float ComputeGroundLift( Model model, Vector3 scale, FoliageSpecies species, ThornsFoliageConfig config )
	{
		var bounds = model.RenderBounds.Size.LengthSquared > 1e-12f
			? model.RenderBounds
			: model.Bounds;
		var scaledMinZ = bounds.Mins.z * scale.z;
		var scaledHeight = bounds.Size.z * scale.z;

		// Tree meshes often have pivot at center — lift so the trunk base meets terrain.
		var liftFromBounds = Math.Max( 0f, -scaledMinZ );
		var liftFromCenterPivot = scaledHeight * config.TreePivotLiftFraction;
		var lift = Math.Max( liftFromBounds, liftFromCenterPivot );

		return config.SurfaceOffsetInches + lift - config.TreeGroundEmbedInches;
	}
}
