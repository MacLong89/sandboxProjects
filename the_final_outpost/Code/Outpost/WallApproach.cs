namespace FinalOutpost;

public enum WallApproachSide { North, South, East, West }

/// <summary>Perimeter wall side helpers for zombie breach behavior.</summary>
public static class WallApproach
{
	static float Half => GameConstants.ArenaHalf;
	static float SideEpsilon => GameConstants.WallThickness * 0.75f;

	public static WallApproachSide FromWorldPosition( Vector3 pos, Vector3 corePos )
	{
		var d = pos - corePos;
		if ( MathF.Abs( d.y ) >= MathF.Abs( d.x ) )
			return d.y >= 0f ? WallApproachSide.North : WallApproachSide.South;
		return d.x >= 0f ? WallApproachSide.East : WallApproachSide.West;
	}

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

	/// <summary>World point just inside the perimeter through the gap on this side.</summary>
	public static Vector3 InwardWaypoint( IReadOnlyList<WallSegment> walls, WallApproachSide side, Vector3 corePos )
	{
		Vector3 breachPoint = SideAnchor( side );

		foreach ( var w in walls )
		{
			if ( !IsOnSide( w, side ) || !w.IsBroken ) continue;
			breachPoint = w.Center;
			break;
		}

		var inward = (corePos - breachPoint).WithZ( 0f );
		if ( inward.LengthSquared < 1f )
			inward = -SideAnchor( side );
		inward = inward.Normal;

		return breachPoint + inward * 140f;
	}

	public static bool IsOutsideWalls( Vector3 pos, Vector3 corePos ) =>
		(pos - corePos).WithZ( 0f ).Length > GameConstants.ArenaHalf * 0.78f;
}
