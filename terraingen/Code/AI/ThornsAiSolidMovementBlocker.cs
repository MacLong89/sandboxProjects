namespace Terraingen.AI;

/// <summary>Runtime guard for AI movers that can otherwise steer through generated or player-built solids.</summary>
public static class ThornsAiSolidMovementBlocker
{
	const float DefaultSkin = 10f;
	const float MinObstacleClearanceFactor = 0.62f;

	[ConVar( "animal_ignore_resource_collision" )]
	public static bool AnimalsIgnoreResourceCollision { get; set; } = true;

	[ConVar( "animal_ignore_boulder_collision" )]
	public static bool AnimalsIgnoreBoulderCollision { get; set; } = true;

	public static bool SegmentHitsStructure(
		Scene scene,
		GameObject mover,
		Vector3 from,
		Vector3 to,
		float bodyRadius,
		float bodyHeight,
		out SceneTraceResult hit )
	{
		hit = default;

		if ( scene is null || !scene.IsValid() || !mover.IsValid() )
			return false;

		var flatDelta = ( to - from ).WithZ( 0f );
		var distance = flatDelta.Length;
		if ( distance < 1f )
			return false;

		var dir = flatDelta / distance;
		var probeHeight = Math.Clamp( bodyHeight * 0.46f, 28f, 58f );
		var start = from + Vector3.Up * probeHeight - dir * MathF.Min( bodyRadius + DefaultSkin, 36f );
		var end = to + Vector3.Up * probeHeight + dir * MathF.Min( bodyRadius + DefaultSkin, 36f );

		var tr = scene.Trace
			.Ray( start, end )
			.IgnoreGameObjectHierarchy( mover )
			.Run();

		if ( !tr.Hit || !IsPathBlockingObstacle( tr.GameObject ) )
			return false;

		hit = tr;
		return true;
	}

	/// <summary>True when a mover center is inside a building foundation / boulder hull (not merely beside a tree).</summary>
	public static bool PositionOverlapsStructure(
		Scene scene,
		GameObject mover,
		Vector3 position,
		float bodyRadius,
		float bodyHeight,
		out SceneTraceResult hit )
	{
		hit = default;

		if ( scene is null || !scene.IsValid() || !mover.IsValid() )
			return false;

		var embedDistance = MathF.Min( MathF.Max( bodyRadius * 0.28f, 6f ), 12f );
		var probeRadius = MathF.Max( bodyRadius + DefaultSkin, 28f );
		ReadOnlySpan<float> checkHeights = stackalloc float[] { 14f, 32f, 52f };

		for ( var i = 0; i < 8; i++ )
		{
			var ang = i * MathF.PI * 0.25f;
			var dir = new Vector3( MathF.Cos( ang ), MathF.Sin( ang ), 0f );

			for ( var h = 0; h < checkHeights.Length; h++ )
			{
				var start = position + Vector3.Up * checkHeights[h];
				var end = start + dir * probeRadius;
				var tr = scene.Trace
					.Ray( start, end )
					.IgnoreGameObjectHierarchy( mover )
					.Run();

				if ( !tr.Hit || tr.Distance > embedDistance || !IsEmbeddedEjectObstacle( tr.GameObject ) )
					continue;

				hit = tr;
				return true;
			}
		}

		var upStart = position + Vector3.Up * 6f;
		var upEnd = position + Vector3.Up * MathF.Max( bodyHeight + 16f, 72f );
		var upTrace = scene.Trace
			.Ray( upStart, upEnd )
			.IgnoreGameObjectHierarchy( mover )
			.Run();
		if ( upTrace.Hit
		     && upTrace.Distance <= MathF.Max( bodyHeight * 0.45f, 24f )
		     && IsEmbeddedEjectObstacle( upTrace.GameObject ) )
		{
			hit = upTrace;
			return true;
		}

		return false;
	}

	/// <summary>Reject separation / step destinations that clip into any obstacle shell.</summary>
	public static bool PositionTooCloseToPathObstacle(
		Scene scene,
		GameObject mover,
		Vector3 position,
		float bodyRadius,
		float bodyHeight )
	{
		if ( scene is null || !scene.IsValid() || !mover.IsValid() )
			return false;

		var clearance = MathF.Max( bodyRadius * MinObstacleClearanceFactor, 12f );
		var probeHeight = Math.Clamp( bodyHeight * 0.46f, 28f, 58f );
		var probeReach = MathF.Max( bodyRadius + DefaultSkin, clearance + 8f );

		for ( var i = 0; i < 8; i++ )
		{
			var ang = i * MathF.PI * 0.25f;
			var dir = new Vector3( MathF.Cos( ang ), MathF.Sin( ang ), 0f );
			var start = position + Vector3.Up * probeHeight;
			var end = start + dir * probeReach;
			var tr = scene.Trace
				.Ray( start, end )
				.IgnoreGameObjectHierarchy( mover )
				.Run();

			if ( tr.Hit && tr.Distance < clearance && IsPathBlockingObstacle( tr.GameObject ) )
				return true;
		}

		return false;
	}

	public static bool HasObstacleNear(
		Scene scene,
		GameObject mover,
		Vector3 position,
		float bodyRadius,
		float bodyHeight,
		float extraReach = 48f )
	{
		if ( scene is null || !scene.IsValid() || !mover.IsValid() )
			return false;

		var probeHeight = Math.Clamp( bodyHeight * 0.46f, 28f, 58f );
		var reach = bodyRadius + DefaultSkin + extraReach;

		for ( var i = 0; i < 8; i++ )
		{
			var ang = i * MathF.PI * 0.25f;
			var dir = new Vector3( MathF.Cos( ang ), MathF.Sin( ang ), 0f );
			var start = position + Vector3.Up * probeHeight;
			var tr = scene.Trace
				.Ray( start, start + dir * reach )
				.IgnoreGameObjectHierarchy( mover )
				.Run();

			if ( tr.Hit && IsPathBlockingObstacle( tr.GameObject ) )
				return true;
		}

		return false;
	}

	public static Vector3 FilterWishVelocity(
		Scene scene,
		GameObject mover,
		Vector3 currentPosition,
		Vector3 wishVelocity,
		float deltaSeconds,
		float bodyRadius,
		float bodyHeight )
	{
		if ( wishVelocity.WithZ( 0f ).LengthSquared <= 1f || deltaSeconds <= 0f )
			return wishVelocity;

		var from = currentPosition;
		var to = currentPosition + wishVelocity.WithZ( 0f ) * deltaSeconds;
		return SegmentHitsStructure( scene, mover, from, to, bodyRadius, bodyHeight, out _ )
			? Vector3.Zero
			: wishVelocity;
	}

	/// <summary>Like <see cref="FilterWishVelocity"/> but tries wall-tangent slides when a structure blocks forward motion.</summary>
	public static Vector3 FilterWishVelocityWithSlide(
		Scene scene,
		GameObject mover,
		Vector3 currentPosition,
		Vector3 wishVelocity,
		float deltaSeconds,
		float bodyRadius,
		float bodyHeight )
	{
		var filtered = FilterWishVelocity( scene, mover, currentPosition, wishVelocity, deltaSeconds, bodyRadius, bodyHeight );
		if ( filtered.WithZ( 0f ).LengthSquared > 1f )
			return filtered;

		var planar = wishVelocity.WithZ( 0f );
		if ( planar.LengthSquared <= 1f )
			return wishVelocity;

		var speed = planar.Length;
		var wishDir = planar / speed;
		var to = currentPosition + wishDir * ( speed * deltaSeconds );
		if ( SegmentHitsStructure( scene, mover, currentPosition, to, bodyRadius, bodyHeight, out var hit ) )
		{
			foreach ( var tangent in BuildSlideTangents( hit.Normal, wishDir ) )
			{
				var slideWish = tangent * ( speed * 0.88f );
				filtered = FilterWishVelocity( scene, mover, currentPosition, slideWish, deltaSeconds, bodyRadius, bodyHeight );
				if ( filtered.WithZ( 0f ).LengthSquared > 1f )
					return filtered;
			}
		}

		ReadOnlySpan<float> slideAngles = stackalloc float[] { 40f, -40f, 75f, -75f, 25f, -25f, 110f, -110f };
		for ( var i = 0; i < slideAngles.Length; i++ )
		{
			var slid = Rotation.FromYaw( slideAngles[i] ) * wishDir * ( speed * 0.82f );
			filtered = FilterWishVelocity( scene, mover, currentPosition, slid, deltaSeconds, bodyRadius, bodyHeight );
			if ( filtered.WithZ( 0f ).LengthSquared > 1f )
				return filtered;
		}

		return Vector3.Zero;
	}

	/// <summary>Planar step with lateral slide / shortened probes when buildings or tree trunks block the path.</summary>
	public static bool TryResolvePlanarStep(
		Scene scene,
		GameObject mover,
		Vector3 from,
		Vector3 desiredTo,
		float bodyRadius,
		float bodyHeight,
		out Vector3 resolvedTo )
	{
		resolvedTo = desiredTo;
		if ( !SegmentHitsStructure( scene, mover, from, desiredTo, bodyRadius, bodyHeight, out var hit ) )
			return true;

		var planar = ( desiredTo - from ).WithZ( 0f );
		var distance = planar.Length;
		if ( distance < 1f )
			return false;

		var dir = planar / distance;
		foreach ( var tangent in BuildSlideTangents( hit.Normal, dir ) )
		{
			var candidate = from + tangent * distance;
			if ( !SegmentHitsStructure( scene, mover, from, candidate, bodyRadius, bodyHeight, out _ ) )
			{
				resolvedTo = candidate;
				return true;
			}
		}

		ReadOnlySpan<float> slideAngles = stackalloc float[] { 40f, -40f, 75f, -75f, 25f, -25f, 110f, -110f };
		for ( var i = 0; i < slideAngles.Length; i++ )
		{
			var slidDir = ( Rotation.FromYaw( slideAngles[i] ) * dir ).WithZ( 0f ).Normal;
			var candidate = from + slidDir * distance;
			if ( !SegmentHitsStructure( scene, mover, from, candidate, bodyRadius, bodyHeight, out _ ) )
			{
				resolvedTo = candidate;
				return true;
			}
		}

		for ( var scale = 0.75f; scale >= 0.15f; scale -= 0.15f )
		{
			var candidate = from + dir * ( distance * scale );
			if ( !SegmentHitsStructure( scene, mover, from, candidate, bodyRadius, bodyHeight, out _ ) )
			{
				resolvedTo = candidate;
				return true;
			}
		}

		return false;
	}

	public static IEnumerable<Vector3> BuildSlideTangents( Vector3 hitNormal, Vector3 wishDir )
	{
		var normal = hitNormal.WithZ( 0f );
		if ( normal.LengthSquared > 0.0001f )
			normal = normal.Normal;

		var wish = wishDir.WithZ( 0f );
		if ( wish.LengthSquared > 0.0001f )
			wish = wish.Normal;

		if ( normal.LengthSquared > 0.0001f && wish.LengthSquared > 0.0001f )
		{
			var projected = ( wish - normal * Vector3.Dot( wish, normal ) ).WithZ( 0f );
			if ( projected.LengthSquared > 0.01f )
				yield return projected.Normal;

			var lateral = new Vector3( -normal.y, normal.x, 0f );
			if ( lateral.LengthSquared > 0.01f )
			{
				yield return lateral.Normal;
				yield return ( -lateral ).Normal;
			}
		}
		else
		{
			yield return new Vector3( -wish.y, wish.x, 0f ).Normal;
			yield return new Vector3( wish.y, -wish.x, 0f ).Normal;
		}
	}

	/// <summary>Offset around a blocker toward <paramref name="target"/> — alternates flank side each call.</summary>
	public static Vector3 BuildFlankGoal(
		Vector3 from,
		Vector3 target,
		ref int flankSign,
		float flankDistance = 160f,
		float forwardBias = 0.42f )
	{
		var fromFlat = from.WithZ( 0f );
		var toFlat = target.WithZ( 0f );
		var delta = toFlat - fromFlat;
		if ( delta.LengthSquared < 64f )
			return target;

		var forward = delta.Normal;
		var lateral = new Vector3( -forward.y, forward.x, 0f ) * Math.Sign( flankSign == 0 ? 1 : flankSign );
		flankSign = flankSign > 0 ? -1 : 1;
		var blended = ( lateral + forward * forwardBias ).WithZ( 0f );
		if ( blended.LengthSquared < 1e-6f )
			return target;

		return fromFlat + blended.Normal * flankDistance + Vector3.Up * ( target.z - from.z );
	}

	public static bool IsPathBlockingObstacle( GameObject go ) => IsBlockingStructure( go );

	static bool IsEmbeddedEjectObstacle( GameObject go )
	{
		for ( var node = go; node is not null && node.IsValid(); node = node.Parent )
		{
			if ( node.Tags.Has( "animal" ) || node.Tags.Has( "bandit" ) || node.Tags.Has( "player" ) )
				return false;

			if ( IsAnimalPassthroughResource( node ) )
				return false;

			if ( node.Tags.Has( "thorns_structure" ) )
				return true;

			if ( node.Tags.Has( "boulder" ) )
				return !AnimalsIgnoreBoulderCollision;
		}

		return false;
	}

	public static bool IsBlockingStructure( GameObject go )
	{
		for ( var node = go; node is not null && node.IsValid(); node = node.Parent )
		{
			if ( node.Tags.Has( "animal" ) || node.Tags.Has( "bandit" ) || node.Tags.Has( "player" ) )
				return false;

			if ( IsAnimalPassthroughResource( node ) )
				return false;

			if ( node.Tags.Has( "thorns_structure" ) )
				return true;

			if ( node.Tags.Has( "boulder" ) )
				return !AnimalsIgnoreBoulderCollision;

			// Decorative world solids such as tree trunks, ore, and small rock clutter are player
			// collision, not AI path blockers. Animals may visually brush through them rather than
			// getting trapped trying to solve dense prop mazes with local steering.
		}

		return false;
	}

	static bool IsAnimalPassthroughResource( GameObject node )
	{
		if ( !AnimalsIgnoreResourceCollision )
			return false;

		return node.Tags.Has( "tree" )
		       || node.Tags.Has( "mineral" );
	}
}
