namespace FinalOutpost;

/// <summary>Keeps workers and defenders from walking through placed structures.</summary>
public static class BuildingCollision
{
	public static float UnitRadius => GameConstants.UnitCollisionRadius;

	private static Vector3 AgentFootprint( float agentRadius ) =>
		new( agentRadius * 2f, agentRadius * 2f, 0f );

	public static bool BlocksUnit( Vector3 worldPos, float agentRadius = -1f )
	{
		agentRadius = ResolveAgentRadius( agentRadius );
		worldPos = worldPos.WithZ( 0f );
		var agentSize = AgentFootprint( agentRadius );

		if ( BuildGrid.FootprintsOverlap( worldPos, agentSize, Vector3.Zero, BuildGrid.CommandPostCollisionFootprint ) )
			return true;

		var build = BuildManager.Instance;
		if ( build is null ) return false;

		foreach ( var b in build.Buildings )
		{
			if ( b.IsDestroyed ) continue;

			var center = BuildGrid.CellToWorld( b.CellX, b.CellY );
			if ( BuildGrid.FootprintsOverlap( worldPos, agentSize, center, b.Def.CollisionFootprint ) )
				return true;
		}

		return false;
	}

	/// <summary>XY distance from a point to the nearest edge of an axis-aligned footprint.</summary>
	public static float DistToFootprintSurface( Vector3 from, Vector3 center, Vector3 size )
	{
		from = from.WithZ( 0f );
		center = center.WithZ( 0f );
		var half = size.WithZ( 0f ) * 0.5f;
		var dx = MathF.Max( MathF.Abs( from.x - center.x ) - half.x, 0f );
		var dy = MathF.Max( MathF.Abs( from.y - center.y ) - half.y, 0f );
		return MathF.Sqrt( dx * dx + dy * dy );
	}

	/// <summary>Closest point on a footprint perimeter from <paramref name="from"/>.</summary>
	public static Vector3 ClosestPointOnFootprint( Vector3 from, Vector3 center, Vector3 size )
	{
		from = from.WithZ( 0f );
		center = center.WithZ( 0f );
		var half = size.WithZ( 0f ) * 0.5f;
		var localX = from.x - center.x;
		var localY = from.y - center.y;

		if ( MathF.Abs( localX ) <= half.x && MathF.Abs( localY ) <= half.y )
		{
			var edgeX = half.x - MathF.Abs( localX );
			var edgeY = half.y - MathF.Abs( localY );
			if ( edgeX < edgeY )
				localX = MathF.Sign( localX ) * half.x;
			else
				localY = MathF.Sign( localY ) * half.y;
		}
		else
		{
			localX = Math.Clamp( localX, -half.x, half.x );
			localY = Math.Clamp( localY, -half.y, half.y );
		}

		return new Vector3( center.x + localX, center.y + localY, 0f );
	}

	/// <summary>Stand-off point outside a footprint for melee approach.</summary>
	public static Vector3 ApproachPoint( Vector3 from, Vector3 center, Vector3 size, float agentRadius = -1f )
	{
		agentRadius = ResolveAgentRadius( agentRadius );
		var closest = ClosestPointOnFootprint( from, center, size );
		var away = (from - closest).WithZ( 0f );
		if ( away.Length < 0.001f )
		{
			away = (from - center).WithZ( 0f );
			if ( away.Length < 0.001f )
				away = Vector3.Right;
		}

		return closest + away.Normal * (agentRadius + 2f);
	}

	/// <summary>True when a world point lies inside a solid building footprint (no unit padding).</summary>
	public static bool IsInsideBuildingFootprint( Vector3 worldPos )
	{
		worldPos = worldPos.WithZ( 0f );
		if ( IsInsideFootprint( worldPos, Vector3.Zero, BuildGrid.CommandPostCollisionFootprint ) )
			return true;

		var build = BuildManager.Instance;
		if ( build is null ) return false;

		foreach ( var b in build.Buildings )
		{
			if ( b.IsDestroyed ) continue;
			var center = BuildGrid.CellToWorld( b.CellX, b.CellY );
			if ( IsInsideFootprint( worldPos, center, b.Def.CollisionFootprint ) )
				return true;
		}

		return false;
	}

	/// <summary>Can a projectile travel from origin to target without passing through a building?</summary>
	public static bool HasLineOfFire( Vector3 origin, Vector3 target, float targetMargin = 28f )
	{
		origin = origin.WithZ( 0f );
		target = target.WithZ( 0f );
		var seg = target - origin;
		var len = seg.Length;
		if ( len < 8f ) return true;

		var dir = seg / len;
		var steps = Math.Max( 2, (int)(len / 32f) );
		for ( var i = 1; i < steps; i++ )
		{
			var distAlong = len * (i / (float)steps);
			if ( len - distAlong < targetMargin )
				break;

			if ( IsInsideBuildingFootprint( origin + dir * distAlong ) )
				return false;
		}

		return true;
	}

	private static bool IsInsideFootprint( Vector3 point, Vector3 center, Vector3 size )
	{
		var half = size.WithZ( 0f ) * 0.5f;
		return MathF.Abs( point.x - center.x ) <= half.x
		       && MathF.Abs( point.y - center.y ) <= half.y;
	}

	/// <summary>Steps toward <paramref name="target"/> without entering building footprints.</summary>
	public static Vector3 ResolveStep( Vector3 from, Vector3 target, float step, float agentRadius = -1f )
	{
		agentRadius = ResolveAgentRadius( agentRadius );
		from = from.WithZ( 0f );
		target = target.WithZ( 0f );

		var delta = target - from;
		var dist = delta.Length;
		if ( dist < 0.001f ) return from;

		var move = MathF.Min( step, dist );
		var desired = from + delta / dist * move;
		var resolved = ResolveMove( from, desired, agentRadius );

		if ( (resolved - from).Length <= 0.01f && BlocksUnit( from, agentRadius ) )
			resolved = PushOut( from, agentRadius );

		return resolved;
	}

	public static Vector3 ResolveMove( Vector3 from, Vector3 desired, float agentRadius = -1f )
	{
		agentRadius = ResolveAgentRadius( agentRadius );
		from = from.WithZ( 0f );
		desired = desired.WithZ( 0f );

		if ( !BlocksUnit( desired, agentRadius ) )
			return desired;

		var delta = desired - from;
		if ( delta.Length <= 0.001f )
			return from;

		var best = from;
		var bestDist = 0f;
		var norm = delta / delta.Length;
		var slide = agentRadius * 1.35f;

		TryCandidate( from + new Vector3( delta.x, 0f, 0f ), ref best, ref bestDist );
		TryCandidate( from + new Vector3( 0f, delta.y, 0f ), ref best, ref bestDist );
		TryCandidate( from + norm * MathF.Min( delta.Length, slide ), ref best, ref bestDist );

		var perp = new Vector3( -norm.y, norm.x, 0f );
		TryCandidate( from + perp * slide, ref best, ref bestDist );
		TryCandidate( from - perp * slide, ref best, ref bestDist );
		TryCandidate( from + (norm + perp).Normal * slide, ref best, ref bestDist );
		TryCandidate( from + (norm - perp).Normal * slide, ref best, ref bestDist );

		return best;

		void TryCandidate( Vector3 candidate, ref Vector3 bestPoint, ref float bestLen )
		{
			if ( BlocksUnit( candidate, agentRadius ) ) return;
			var len = (candidate - from).Length;
			if ( len <= bestLen ) return;
			bestPoint = candidate;
			bestLen = len;
		}
	}

	public static bool TryFindClearPoint( Vector3 center, float minRadius, float maxRadius, out Vector3 point, int attempts = 12 )
	{
		center = center.WithZ( 0f );
		minRadius = MathF.Max( 0f, minRadius );
		maxRadius = MathF.Max( minRadius, maxRadius );

		for ( var i = 0; i < attempts; i++ )
		{
			var angle = Game.Random.Float( 0f, MathF.PI * 2f );
			var r = Game.Random.Float( minRadius, maxRadius );
			var candidate = center + new Vector3( MathF.Cos( angle ) * r, MathF.Sin( angle ) * r, 0f );
			if ( !BlocksUnit( candidate ) )
			{
				point = candidate;
				return true;
			}
		}

		point = center;
		if ( !BlocksUnit( center ) )
			return true;

		return PushOut( from: center, out point );
	}

	private static Vector3 PushOut( Vector3 from, float agentRadius = -1f )
	{
		if ( PushOut( from, out var point, agentRadius ) )
			return point;
		return from;
	}

	private static bool PushOut( Vector3 from, out Vector3 point, float agentRadius = -1f )
	{
		agentRadius = ResolveAgentRadius( agentRadius );
		from = from.WithZ( 0f );
		for ( var ring = 1; ring <= 4; ring++ )
		{
			var push = agentRadius * ring * 0.85f;
			for ( var i = 0; i < 8; i++ )
			{
				var angle = i / 8f * MathF.PI * 2f;
				var candidate = from + new Vector3( MathF.Cos( angle ) * push, MathF.Sin( angle ) * push, 0f );
				if ( !BlocksUnit( candidate, agentRadius ) )
				{
					point = candidate;
					return true;
				}
			}
		}

		point = from;
		return false;
	}

	private static float ResolveAgentRadius( float agentRadius ) =>
		agentRadius > 0f ? agentRadius : GameConstants.UnitCollisionRadius;
}
