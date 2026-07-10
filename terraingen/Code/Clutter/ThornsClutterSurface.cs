namespace Terraingen.Clutter;

using Terraingen.Core;
using Terraingen.Foliage;

/// <summary>
/// Snaps clutter to the live terrain mesh and corrects pivot/embed height per model.
/// </summary>
public static class ThornsClutterSurface
{
	public static bool TryRaycastTerrainSurface( Terrain terrain, float worldX, float worldY, out Vector3 worldPosition )
	{
		worldPosition = default;
		if ( !terrain.IsValid() )
			return false;

		var originZ = terrain.GameObject.WorldPosition.z;
		var maxHeight = terrain.TerrainHeight;
		var rayStart = new Vector3( worldX, worldY, originZ + maxHeight * 2.5f );
		var ray = new Ray( rayStart, Vector3.Down );

		if ( !terrain.RayIntersects( ray, maxHeight * 5f, out var localHit ) )
			return false;

		worldPosition = terrain.GameObject.WorldTransform.PointToWorld( localHit );
		return true;
	}

	public static Vector3 SampleTerrainNormal(
		Terrain terrain,
		ThornsFoliageBiomeSampler sampler,
		float worldX,
		float worldY,
		float sampleDeltaInches = 24f )
	{
		var d = Math.Max( sampleDeltaInches, 8f );
		if ( TryRaycastTerrainSurface( terrain, worldX + d, worldY, out var pXp )
		     && TryRaycastTerrainSurface( terrain, worldX - d, worldY, out var pXm )
		     && TryRaycastTerrainSurface( terrain, worldX, worldY + d, out var pYp )
		     && TryRaycastTerrainSurface( terrain, worldX, worldY - d, out var pYm ) )
		{
			var dx = pXp.z - pXm.z;
			var dy = pYp.z - pYm.z;
			var normal = new Vector3( -dx, -dy, d * 2f );
			if ( normal.LengthSquared > 1e-8f )
				return normal.Normal;
		}

		if ( sampler is not null )
			return SampleTerrainNormalFromHeightfield( sampler, worldX, worldY, sampleDeltaInches );

		return Vector3.Up;
	}

	/// <summary>Mesh-accurate normal for grass (small cross-raycast on terrain).</summary>
	public static Vector3 SampleTerrainNormalOnMesh(
		Terrain terrain,
		float worldX,
		float worldY,
		float sampleDeltaInches = 16f )
	{
		if ( !terrain.IsValid() )
			return Vector3.Up;

		var d = Math.Max( sampleDeltaInches, 8f );
		if ( TryRaycastTerrainSurface( terrain, worldX + d, worldY, out var pXp )
		     && TryRaycastTerrainSurface( terrain, worldX - d, worldY, out var pXm )
		     && TryRaycastTerrainSurface( terrain, worldX, worldY + d, out var pYp )
		     && TryRaycastTerrainSurface( terrain, worldX, worldY - d, out var pYm ) )
		{
			var dx = pXp.z - pXm.z;
			var dy = pYp.z - pYm.z;
			var normal = new Vector3( -dx, -dy, d * 2f );
			if ( normal.LengthSquared > 1e-8f )
				return normal.Normal;
		}

		return Vector3.Up;
	}

	/// <summary>Heightfield normal — matches sculpted terrain and avoids per-blade raycasts.</summary>
	public static Vector3 SampleTerrainNormalFromHeightfield(
		ThornsFoliageBiomeSampler sampler,
		float worldX,
		float worldY,
		float sampleDeltaInches = 24f )
	{
		if ( sampler is null )
			return Vector3.Up;

		var d = Math.Max( sampleDeltaInches, 8f );
		var fieldDx = sampler.SampleWorldHeight( worldX + d, worldY ) - sampler.SampleWorldHeight( worldX - d, worldY );
		var fieldDy = sampler.SampleWorldHeight( worldX, worldY + d ) - sampler.SampleWorldHeight( worldX, worldY - d );
		var fieldNormal = new Vector3( -fieldDx, -fieldDy, d * 2f );
		return fieldNormal.LengthSquared > 1e-8f ? fieldNormal.Normal : Vector3.Up;
	}

	/// <summary>Grass placement — raycast to terrain mesh, offset along surface normal, heightfield fallback.</summary>
	public static bool TrySampleWorldForGrass(
		Terrain terrain,
		ThornsFoliageBiomeSampler sampler,
		float worldX,
		float worldY,
		Model model,
		float uniformScale,
		ThornsClutterConfig config,
		out Vector3 worldPosition,
		out Vector3 surfaceNormal )
	{
		worldPosition = default;
		surfaceNormal = Vector3.Up;

		if ( !terrain.IsValid() || !model.IsValid )
			return false;

		var verticalOffset = ComputeVerticalOffset( model, uniformScale, isGrass: true, config );

		if ( TryRaycastTerrainSurface( terrain, worldX, worldY, out var hit ) )
		{
			surfaceNormal = sampler is not null
				? SampleTerrainNormalFromHeightfield( sampler, worldX, worldY )
				: Vector3.Up;
			if ( surfaceNormal.z < 0.94f )
				surfaceNormal = SampleTerrainNormalOnMesh( terrain, worldX, worldY );

			worldPosition = hit + surfaceNormal * verticalOffset;
			return true;
		}

		if ( sampler is null )
			return false;

		var fieldHeight = sampler.SampleWorldHeight( worldX, worldY );
		if ( fieldHeight <= 0f )
			return false;

		surfaceNormal = SampleTerrainNormalFromHeightfield( sampler, worldX, worldY );
		worldPosition = new Vector3( worldX, worldY, terrain.GameObject.WorldPosition.z + fieldHeight )
			+ surfaceNormal * verticalOffset;
		return true;
	}

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

		if ( TryRaycastTerrainSurface( terrain, worldX, worldY, out var hit ) )
		{
			var up = isGrass
				? SampleTerrainNormalOnMesh( terrain, worldX, worldY )
				: Vector3.Up;
			worldPosition = hit + up * verticalOffset;
			return true;
		}

		if ( sampler is null )
			return false;

		var fieldHeight = sampler.SampleWorldHeight( worldX, worldY );
		if ( fieldHeight <= 0f )
			return false;

		var fallbackNormal = isGrass
			? SampleTerrainNormalFromHeightfield( sampler, worldX, worldY )
			: Vector3.Up;
		worldPosition = new Vector3( worldX, worldY, terrain.GameObject.WorldPosition.z + fieldHeight )
			+ fallbackNormal * verticalOffset;
		return true;
	}

	static float ComputeVerticalOffset( Model model, float scale, bool isGrass, ThornsClutterConfig config )
	{
		var bounds = model.Bounds;
		var scaledMinZ = bounds.Mins.z * scale;
		// Raise origin so the lowest bound point sits on the terrain contact point.
		var originToBottom = Math.Max( 0f, -scaledMinZ );

		if ( isGrass )
			return originToBottom + config.GrassSurfaceOffset - config.GrassGroundEmbedInches;

		var scaledHeight = Math.Max( bounds.Size.z * scale, 0.01f );
		var bottomPivot = bounds.Mins.z >= -0.05f * scaledHeight;
		if ( bottomPivot )
			return config.RockSurfaceOffset + originToBottom - config.RockGroundEmbedInches;

		var rockLiftFromCenter = scaledHeight * config.RockPivotLiftFraction;
		var rockLift = Math.Max( originToBottom, rockLiftFromCenter );
		return config.RockSurfaceOffset + rockLift - config.RockGroundEmbedInches;
	}

	public static float ComputeUniformScale( Model model, bool isGrass, ThornsClutterConfig config, Random rng )
	{
		var bounds = model.Bounds;
		var meshHeight = Math.Max( bounds.Size.z, 0.01f );
		var target = isGrass ? config.GrassTargetHeightInches : config.RockTargetHeightInches;
		var multiplier = isGrass ? config.GrassScaleMultiplier : config.RockScaleMultiplier;
		var uniform = (target / meshHeight) * multiplier;
		uniform *= ThornsNatureScaleVariance.Sample( rng );
		uniform = Math.Max( uniform, 0.05f );
		if ( isGrass )
			uniform = Math.Min( uniform, Math.Max( 0.05f, config.GrassMaxUniformScale ) );

		return uniform * Math.Max( 0.05f, config.GlobalScaleMultiplier );
	}
}
