namespace Terraingen.Clutter;

using Terraingen.Foliage;

/// <summary>
/// Snaps clutter to the live terrain mesh and corrects pivot/embed height per model.
/// </summary>
public static class ThornsClutterSurface
{
	public static bool TrySampleWorld(
		Terrain terrain,
		ThornsFoliageBiomeSampler sampler,
		float worldX,
		float worldY,
		Model model,
		float uniformScale,
		bool isGrass,
		ThornsClutterConfig config,
		out Vector3 worldPosition )
	{
		worldPosition = default;

		if ( !terrain.IsValid() || !model.IsValid )
			return false;

		var verticalOffset = ComputeVerticalOffset( model, uniformScale, isGrass, config );

		var maxHeight = terrain.TerrainHeight;
		var rayStart = new Vector3( worldX, worldY, maxHeight * 2.5f + terrain.GameObject.WorldPosition.z );
		var ray = new Ray( rayStart, Vector3.Down );

		if ( terrain.RayIntersects( ray, maxHeight * 5f, out var localHit ) )
		{
			worldPosition = terrain.GameObject.WorldTransform.PointToWorld( localHit );
			worldPosition += Vector3.Up * verticalOffset;
			return true;
		}

		if ( sampler is null )
			return false;

		var fieldHeight = sampler.SampleWorldHeight( worldX, worldY );
		if ( fieldHeight <= 0f )
			return false;

		worldPosition = new Vector3( worldX, worldY, terrain.GameObject.WorldPosition.z + fieldHeight + verticalOffset );
		return true;
	}

	static float ComputeVerticalOffset( Model model, float scale, bool isGrass, ThornsClutterConfig config )
	{
		var bounds = model.Bounds;
		var scaledMinZ = bounds.Mins.z * scale;
		var scaledHeight = Math.Max( bounds.Size.z * scale, 0.01f );
		var liftFromBounds = Math.Max( 0f, -scaledMinZ );
		var bottomPivot = bounds.Mins.z >= -0.05f * Math.Max( bounds.Size.z, 0.01f );

		if ( isGrass )
		{
			if ( bottomPivot )
				return config.GrassSurfaceOffset + liftFromBounds - config.GrassGroundEmbedInches;

			var liftFromCenterPivot = scaledHeight * config.GrassPivotLiftFraction;
			var lift = Math.Max( liftFromBounds, liftFromCenterPivot );
			return config.GrassSurfaceOffset + lift - config.GrassGroundEmbedInches;
		}

		if ( bottomPivot )
			return config.RockSurfaceOffset + liftFromBounds - config.RockGroundEmbedInches;

		var rockLiftFromCenter = scaledHeight * config.RockPivotLiftFraction;
		var rockLift = Math.Max( liftFromBounds, rockLiftFromCenter );
		return config.RockSurfaceOffset + rockLift - config.RockGroundEmbedInches;
	}

	public static float ComputeUniformScale( Model model, bool isGrass, ThornsClutterConfig config, Random rng )
	{
		var bounds = model.Bounds;
		var meshHeight = Math.Max( bounds.Size.z, 0.01f );
		var target = isGrass ? config.GrassTargetHeightInches : config.RockTargetHeightInches;
		var multiplier = isGrass ? config.GrassScaleMultiplier : config.RockScaleMultiplier;
		var uniform = (target / meshHeight) * multiplier;
		uniform *= MathX.Lerp( 0.85f, 1.15f, rng.NextSingle() );
		uniform = Math.Max( uniform, 0.05f );
		if ( isGrass )
			uniform = Math.Min( uniform, Math.Max( 0.05f, config.GrassMaxUniformScale ) );

		return uniform;
	}
}
