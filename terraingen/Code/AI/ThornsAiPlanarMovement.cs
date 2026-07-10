namespace Terraingen.AI;

using Terraingen.Animals;
using Terraingen.TerrainGen;

/// <summary>Shared planar movement guards for wildlife and humanoid AI.</summary>
public static class ThornsAiPlanarMovement
{
	public static bool IsUnderSeaLevel( Scene scene, Vector3 worldPos )
	{
		var terrain = ThornsTerrainCache.Resolve( scene );
		var config = ThornsAnimalWorldUtil.ResolveTerrainConfig( scene );
		return terrain.IsValid() && config is not null
		       && ThornsAnimalWorldUtil.IsUnderSeaLevel( scene, terrain, config, worldPos );
	}

	public static Vector3 FilterWishVelocityAwayFromWater(
		Scene scene,
		Vector3 currentPosition,
		Vector3 wishVelocity )
	{
		if ( wishVelocity.WithZ( 0f ).LengthSquared <= 1f )
			return wishVelocity;

		var terrain = ThornsTerrainCache.Resolve( scene );
		var config = ThornsAnimalWorldUtil.ResolveTerrainConfig( scene );
		if ( !terrain.IsValid() || config is null )
			return wishVelocity;

		if ( ThornsAnimalWorldUtil.IsUnderSeaLevel( scene, terrain, config, currentPosition ) )
			return Vector3.Zero;

		var probe = currentPosition + wishVelocity.WithZ( 0f ).Normal * 56f;
		if ( ThornsAnimalWorldUtil.TrySnapToTerrain( terrain, probe, out var snapped )
		     && ThornsAnimalWorldUtil.IsUnderSeaLevel( scene, terrain, config, snapped ) )
			return Vector3.Zero;

		return wishVelocity;
	}

	/// <summary>Planar step that slides around solids and refuses underwater destinations.</summary>
	public static bool TryResolvePlanarStepDry(
		Scene scene,
		GameObject mover,
		Vector3 from,
		Vector3 desiredTo,
		float bodyRadius,
		float bodyHeight,
		out Vector3 resolvedTo )
	{
		resolvedTo = desiredTo;
		if ( scene is null || !scene.IsValid() || !mover.IsValid() )
			return false;

		var terrain = ThornsTerrainCache.Resolve( scene );
		var config = ThornsAnimalWorldUtil.ResolveTerrainConfig( scene );

		if ( TryResolveCandidate( scene, mover, from, desiredTo, bodyRadius, bodyHeight, terrain, config, out resolvedTo ) )
			return true;

		var planar = ( desiredTo - from ).WithZ( 0f );
		var distance = planar.Length;
		if ( distance < 1f )
			return false;

		var dir = planar / distance;
		ReadOnlySpan<float> slideAngles = stackalloc float[] { 55f, -55f, 110f, -110f, 30f, -30f, 150f, -150f };
		for ( var i = 0; i < slideAngles.Length; i++ )
		{
			var slidDir = ( Rotation.FromYaw( slideAngles[i] ) * dir ).WithZ( 0f ).Normal;
			var candidate = from + slidDir * distance;
			if ( TryResolveCandidate( scene, mover, from, candidate, bodyRadius, bodyHeight, terrain, config, out resolvedTo ) )
				return true;
		}

		for ( var scale = 0.7f; scale >= 0.2f; scale -= 0.15f )
		{
			var candidate = from + dir * ( distance * scale );
			if ( TryResolveCandidate( scene, mover, from, candidate, bodyRadius, bodyHeight, terrain, config, out resolvedTo ) )
				return true;
		}

		return false;
	}

	static bool TryResolveCandidate(
		Scene scene,
		GameObject mover,
		Vector3 from,
		Vector3 candidate,
		float bodyRadius,
		float bodyHeight,
		Terrain terrain,
		ThornsTerrainConfig config,
		out Vector3 resolvedTo )
	{
		resolvedTo = candidate;
		if ( !ThornsAiSolidMovementBlocker.TryResolvePlanarStep(
			     scene, mover, from, candidate, bodyRadius, bodyHeight, out resolvedTo ) )
			return false;

		if ( ThornsAnimalWorldUtil.IsBlockedByBuildingFootprint( resolvedTo, bodyRadius ) )
			return false;

		if ( terrain.IsValid() && config is not null
		     && ThornsAnimalWorldUtil.TrySnapToTerrain( terrain, resolvedTo, out var snapped ) )
			resolvedTo = snapped;

		if ( !terrain.IsValid() || config is null )
			return true;

		return !ThornsAnimalWorldUtil.IsUnderSeaLevel( scene, terrain, config, resolvedTo );
	}

	public static bool IsPositionBlockedForAnimal(
		Scene scene,
		GameObject mover,
		Vector3 position,
		float bodyRadius,
		float bodyHeight )
	{
		if ( ThornsAnimalWorldUtil.IsBlockedByBuildingFootprint( position, bodyRadius ) )
			return true;

		return IsEmbeddedInObstacle( scene, mover, position, bodyRadius, bodyHeight );
	}

	public static bool IsEmbeddedInObstacle(
		Scene scene,
		GameObject mover,
		Vector3 position,
		float bodyRadius,
		float bodyHeight )
	{
		return ThornsAiSolidMovementBlocker.PositionOverlapsStructure(
			scene,
			mover,
			position,
			bodyRadius,
			bodyHeight,
			out _ );
	}

	public static bool IsDestinationTooCloseToObstacle(
		Scene scene,
		GameObject mover,
		Vector3 position,
		float bodyRadius,
		float bodyHeight )
	{
		if ( ThornsAnimalWorldUtil.IsBlockedByBuildingFootprint( position, bodyRadius ) )
			return true;

		return ThornsAiSolidMovementBlocker.PositionTooCloseToPathObstacle(
			scene,
			mover,
			position,
			bodyRadius,
			bodyHeight );
	}

	/// <summary>Push an animal out of proc building foundations / other embedded solids.</summary>
	public static bool TryEjectFromBlockedPosition(
		Scene scene,
		GameObject mover,
		Vector3 position,
		float bodyRadius,
		float bodyHeight,
		out Vector3 cleared )
	{
		cleared = position;
		if ( scene is null || !scene.IsValid() || !mover.IsValid() )
			return false;

		if ( !IsEmbeddedInObstacle( scene, mover, position, bodyRadius, bodyHeight ) )
			return false;

		var terrain = ThornsTerrainCache.Resolve( scene );
		var config = ThornsAnimalWorldUtil.ResolveTerrainConfig( scene );

		for ( var ring = 1; ring <= 12; ring++ )
		{
			var dist = bodyRadius + 48f + ring * 36f;
			for ( var i = 0; i < 8; i++ )
			{
				var ang = i * MathF.PI * 0.25f;
				var offset = new Vector3( MathF.Cos( ang ), MathF.Sin( ang ), 0f ) * dist;
				var candidate = position + offset;
				if ( terrain.IsValid() && ThornsAnimalWorldUtil.TrySnapToTerrain( terrain, candidate, out var snapped ) )
					candidate = snapped;

				if ( terrain.IsValid() && config is not null
				     && ThornsAnimalWorldUtil.IsUnderSeaLevel( scene, terrain, config, candidate ) )
					continue;

				if ( IsEmbeddedInObstacle( scene, mover, candidate, bodyRadius, bodyHeight ) )
					continue;

				if ( !ThornsAiSolidMovementBlocker.TryResolvePlanarStep(
					     scene, mover, position, candidate, bodyRadius, bodyHeight, out var resolved ) )
					continue;

				if ( terrain.IsValid() && ThornsAnimalWorldUtil.TrySnapToTerrain( terrain, resolved, out var finalSnap ) )
					resolved = finalSnap;

				cleared = resolved;
				return true;
			}
		}

		return false;
	}

	/// <summary>Snap a goal onto dry land and ensure a short step toward it is legal.</summary>
	public static bool TrySanitizeMoveGoal(
		Scene scene,
		GameObject mover,
		Vector3 from,
		Vector3 goal,
		float bodyRadius,
		float bodyHeight,
		out Vector3 sanitized )
	{
		sanitized = goal;
		if ( scene is null || !scene.IsValid() || !mover.IsValid() )
			return false;

		var terrain = ThornsTerrainCache.Resolve( scene );
		var config = ThornsAnimalWorldUtil.ResolveTerrainConfig( scene );

		if ( terrain.IsValid() && config is not null )
		{
			if ( ThornsAnimalWorldUtil.IsUnderSeaLevel( scene, terrain, config, goal )
			     && !ThornsAnimalWorldUtil.TryPickDryLandPoint( scene, goal, 420f, out sanitized, maxAttempts: 12 ) )
				return false;

			if ( ThornsAnimalWorldUtil.TrySnapToTerrain( terrain, sanitized, out var snapped ) )
				sanitized = snapped;

			if ( ThornsAnimalWorldUtil.IsUnderSeaLevel( scene, terrain, config, sanitized ) )
				return false;
		}

		if ( ThornsAnimalWorldUtil.TryGetDryNavPoint( scene, sanitized, out var nav ) )
			sanitized = nav;

		return TryResolvePlanarStepDry( scene, mover, from, sanitized, bodyRadius, bodyHeight, out sanitized )
		       || from.WithZ( 0f ).Distance( sanitized.WithZ( 0f ) ) <= bodyRadius + 24f;
	}
}
