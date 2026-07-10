namespace Terraingen.Minerals;

using Terraingen.Core;

/// <summary>Snaps mineral scatter props to the live terrain mesh.</summary>
public static class ThornsMineralSurface
{
	public static bool TrySampleWorld(
		Terrain terrain,
		float worldX,
		float worldY,
		Model model,
		float uniformScale,
		ThornsMineralConfig config,
		out Vector3 worldPosition )
	{
		worldPosition = default;

		if ( !terrain.IsValid() || !model.IsValid )
			return false;

		var maxHeight = terrain.TerrainHeight;
		var rayStart = new Vector3( worldX, worldY, maxHeight * 2.5f + terrain.GameObject.WorldPosition.z );
		var ray = new Ray( rayStart, Vector3.Down );

		if ( terrain.RayIntersects( ray, maxHeight * 5f, out var localHit ) )
		{
			worldPosition = terrain.GameObject.WorldTransform.PointToWorld( localHit );
			worldPosition += Vector3.Up * ComputeVerticalOffset( model, uniformScale, config );
			return true;
		}

		return false;
	}

	static float ComputeVerticalOffset( Model model, float scale, ThornsMineralConfig config )
	{
		var bounds = model.Bounds;
		var scaledMinZ = bounds.Mins.z * scale;
		var scaledHeight = Math.Max( bounds.Size.z * scale, 0.01f );
		var liftFromBounds = Math.Max( 0f, -scaledMinZ );
		var bottomPivot = bounds.Mins.z >= -0.05f * Math.Max( bounds.Size.z, 0.01f );

		if ( bottomPivot )
			return config.SurfaceOffsetInches + liftFromBounds - config.GroundEmbedInches;

		var liftFromCenter = scaledHeight * config.PivotLiftFraction;
		return config.SurfaceOffsetInches + Math.Max( liftFromBounds, liftFromCenter ) - config.GroundEmbedInches;
	}

	public static float ComputeUniformScale( Model model, MineralKind kind, ThornsMineralConfig config, Random rng )
	{
		var meshHeight = Math.Max( model.Bounds.Size.z, 0.01f );
		var target = kind == MineralKind.Ore ? config.OreTargetHeightInches : config.StoneTargetHeightInches;
		var uniform = (target / meshHeight) * config.ScaleMultiplier;
		return Math.Max( ThornsNatureScaleVariance.Apply( uniform, rng ), 0.05f );
	}
}
