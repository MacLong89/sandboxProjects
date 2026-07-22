namespace FinalOutpost;

public enum WallApproachSide { North, South, East, West }

/// <summary>Perimeter wall side helpers for zombie breach behavior.</summary>
public static class WallApproach
{
	/// <summary>
	/// Center of the home wall ring. Walls stay at world origin even if the command post moves.
	/// Pass this into outside/clamp/breach helpers — not <see cref="OutpostManager.CorePosition"/>.
	/// </summary>
	public static Vector3 RingCenter => Vector3.Zero;

	/// <summary>Outer face of the perimeter ring (plot / spawn boundary).</summary>
	static float OuterHalf => GameConstants.ArenaHalf;
	/// <summary>Cell-center line where starter + placeable walls sit.</summary>
	static float Half => GameConstants.WallRingCenter;
	static float SideEpsilon => GameConstants.WallThickness * 0.75f;

	public static WallApproachSide FromWorldPosition( Vector3 pos, Vector3 corePos )
	{
		// Ring is fixed at origin; ignore HQ offset so approach sides stay correct after relocating the post.
		_ = corePos;
		var d = pos - RingCenter;
		if ( MathF.Abs( d.y ) >= MathF.Abs( d.x ) )
			return d.y >= 0f ? WallApproachSide.North : WallApproachSide.South;
		return d.x >= 0f ? WallApproachSide.East : WallApproachSide.West;
	}

	public static Vector3 OutwardNormal( WallApproachSide side ) => side switch
	{
		WallApproachSide.North => new Vector3( 0f, 1f, 0f ),
		WallApproachSide.South => new Vector3( 0f, -1f, 0f ),
		WallApproachSide.East => new Vector3( 1f, 0f, 0f ),
		_ => new Vector3( -1f, 0f, 0f )
	};

	public static Vector3 SideAnchor( WallApproachSide side ) => side switch
	{
		WallApproachSide.North => new Vector3( 0f, Half, 0f ),
		WallApproachSide.South => new Vector3( 0f, -Half, 0f ),
		WallApproachSide.East => new Vector3( Half, 0f, 0f ),
		_ => new Vector3( -Half, 0f, 0f )
	};

	public static bool IsOnSide( WallSegment wall, WallApproachSide side )
	{
		if ( wall is null ) return false;
		var c = wall.Center;
		return side switch
		{
			WallApproachSide.North => MathF.Abs( c.y - Half ) <= SideEpsilon,
			WallApproachSide.South => MathF.Abs( c.y + Half ) <= SideEpsilon,
			WallApproachSide.East => MathF.Abs( c.x - Half ) <= SideEpsilon,
			_ => MathF.Abs( c.x + Half ) <= SideEpsilon
		};
	}

	/// <summary>True when this side already has a gap (broken segment or player-removed piece).</summary>
	public static bool SideHasBreach( IReadOnlyList<WallSegment> walls, WallApproachSide side )
	{
		var onSide = 0;
		var intact = 0;

		foreach ( var w in walls )
		{
			if ( !IsOnSide( w, side ) ) continue;
			onSide++;
			if ( !w.IsBroken ) intact++;
		}

		if ( onSide < GameConstants.SegmentsPerSide )
			return true;

		return intact < onSide;
	}

	/// <summary>The single perimeter segment all zombies on this side break before entering.</summary>
	public static WallSegment GetBreachWall( IReadOnlyList<WallSegment> walls, WallApproachSide side )
	{
		var anchor = SideAnchor( side );
		WallSegment best = null;
		var bestDist = float.MaxValue;

		foreach ( var w in walls )
		{
			if ( !IsOnSide( w, side ) || w.IsBroken ) continue;
			var d = (w.Center - anchor).WithZ( 0f ).LengthSquared;
			if ( d >= bestDist ) continue;
			bestDist = d;
			best = w;
		}

		return best;
	}

	/// <summary>
	/// World point just inside the perimeter through the gap on this side.
	/// Prefer the nearest broken segment to <paramref name="near"/> so zombies aim for the
	/// actual hole instead of always the first broken wall in list order.
	/// </summary>
	public static Vector3 InwardWaypoint( IReadOnlyList<WallSegment> walls, WallApproachSide side, Vector3 corePos, Vector3 near = default )
	{
		Vector3 breachPoint = SideAnchor( side );
		var bestDist = float.MaxValue;
		var found = false;

		foreach ( var w in walls )
		{
			if ( !IsOnSide( w, side ) || !w.IsBroken ) continue;
			var d = near == default
				? 0f
				: (w.Center - near).WithZ( 0f ).LengthSquared;
			if ( found && d >= bestDist ) continue;
			bestDist = d;
			breachPoint = w.Center;
			found = true;
			if ( near == default ) break;
		}

		var inward = (corePos - breachPoint).WithZ( 0f );
		if ( inward.LengthSquared < 1f )
			inward = -SideAnchor( side );
		inward = inward.Normal;

		return breachPoint + inward * (GameConstants.CellSize * 2f);
	}

	/// <summary>
	/// Landing spot just inside the ring for a wall vault — keeps lateral position near <paramref name="from"/>.
	/// </summary>
	public static Vector3 VaultLanding( Vector3 from, Vector3 corePos, WallApproachSide side )
	{
		var inward = -OutwardNormal( side );
		var inset = GameConstants.CellSize * GameConstants.ZombieWallJumpLandInset;
		var land = SideAnchor( side ) + inward * inset;

		// Preserve approach lateral so runners don't funnel to the side midpoint.
		switch ( side )
		{
			case WallApproachSide.North:
			case WallApproachSide.South:
				land.x = Math.Clamp( from.x, -OuterHalf + GameConstants.CellSize, OuterHalf - GameConstants.CellSize );
				break;
			default:
				land.y = Math.Clamp( from.y, -OuterHalf + GameConstants.CellSize, OuterHalf - GameConstants.CellSize );
				break;
		}

		return ClampInsideCourtyard( land, RingCenter );
	}

	/// <summary>Outside takeoff just before the perimeter face for a vault.</summary>
	public static Vector3 VaultTakeoff( Vector3 from, WallApproachSide side )
	{
		var outward = OutwardNormal( side );
		var face = SideAnchor( side ) + outward * (GameConstants.WallThickness * 0.5f + GameConstants.ZombiePathRadius + 4f );
		switch ( side )
		{
			case WallApproachSide.North:
			case WallApproachSide.South:
				face.x = Math.Clamp( from.x, -OuterHalf + GameConstants.CellSize, OuterHalf - GameConstants.CellSize );
				break;
			default:
				face.y = Math.Clamp( from.y, -OuterHalf + GameConstants.CellSize, OuterHalf - GameConstants.CellSize );
				break;
		}

		return face.WithZ( 0f );
	}

	public static bool IsOutsideWalls( Vector3 pos, Vector3 corePos )
	{
		_ = corePos;
		var p = (pos - RingCenter).WithZ( 0f );
		// Square ring — outside once past the outer face (ArenaHalf) of the perimeter cells.
		var outer = OuterHalf;
		return MathF.Abs( p.x ) >= outer || MathF.Abs( p.y ) >= outer;
	}

	/// <summary>
	/// True only while outside or still in the thin wall-thickness band.
	/// Once past that into the courtyard, normal pathing + HQ detours take over
	/// (wide "inner = ArenaHalf - CellSize" kept agents skating the interior wall face).
	/// </summary>
	public static bool NeedsBreachEntry( Vector3 pos, Vector3 corePos )
	{
		if ( IsOutsideWalls( pos, corePos ) )
			return true;

		var p = (pos - RingCenter).WithZ( 0f );
		var ring = Half;
		var band = GameConstants.WallPathDepth + GameConstants.ZombiePathRadius + GameConstants.U( 10f );
		var ax = MathF.Abs( p.x );
		var ay = MathF.Abs( p.y );
		return ax >= ring - band || ay >= ring - band;
	}

	/// <summary>
	/// Max |coord| where a zombie probe no longer overlaps the outer wall ring cell
	/// (wall cell spans [ArenaHalf-CellSize, ArenaHalf)).
	/// </summary>
	public static float CourtyardClearHalf( float agentRadius = -1f )
	{
		if ( agentRadius < 0.01f )
			agentRadius = GameConstants.ZombiePathRadius;
		return GameConstants.ArenaHalf - GameConstants.CellSize - agentRadius - GameConstants.U( 4f );
	}

	/// <summary>True while still close enough that path radius overlaps a perimeter wall cell.</summary>
	public static bool OverlapsPerimeterPath( Vector3 pos, Vector3 corePos, float agentRadius = -1f )
	{
		_ = corePos;
		var clear = CourtyardClearHalf( agentRadius );
		var p = (pos - RingCenter).WithZ( 0f );
		return MathF.Abs( p.x ) > clear || MathF.Abs( p.y ) > clear;
	}

	/// <summary>Pull a point into open courtyard so goals never sit on the interior wall face.</summary>
	public static Vector3 ClampInsideCourtyard( Vector3 pos, Vector3 corePos, float agentRadius = -1f )
	{
		_ = corePos;
		var clear = CourtyardClearHalf( agentRadius );
		var p = (pos - RingCenter).WithZ( 0f );
		p.x = Math.Clamp( p.x, -clear, clear );
		p.y = Math.Clamp( p.y, -clear, clear );
		return (RingCenter + p).WithZ( 0f );
	}

	/// <summary>True once the agent has crossed the inward breach waypoint toward the core.</summary>
	public static bool PastBreachWaypoint( Vector3 pos, WallApproachSide side, Vector3 waypoint )
	{
		var inward = -OutwardNormal( side );
		return Vector3.Dot( (pos - waypoint).WithZ( 0f ), inward ) > GameConstants.CellSize * 0.25f;
	}
}
