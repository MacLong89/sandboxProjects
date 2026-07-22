namespace FinalOutpost;

/// <summary>Keeps workers and defenders from walking through occupied grid tiles.</summary>
public static class BuildingCollision
{
	public static float UnitRadius => GameConstants.UnitCollisionRadius;

	/// <summary>Path exceptions for a single move query (zombie attacking a chosen structure).</summary>
	public readonly struct PathAllow
	{
		public readonly bool IgnorePerimeterWalls;
		public readonly bool IgnoreCore;
		public readonly bool IgnoreBuildings;
		public readonly int? PassThroughCellX;
		public readonly int? PassThroughCellY;
		/// <summary>Scale-aware zombie probe radius; 0 uses <see cref="GameConstants.ZombiePathRadius"/>.</summary>
		public readonly float PathRadiusOverride;

		public static PathAllow None => default;

		public PathAllow(
			bool ignorePerimeterWalls,
			bool ignoreCore,
			bool ignoreBuildings,
			int? passThroughCellX,
			int? passThroughCellY,
			float pathRadiusOverride )
		{
			IgnorePerimeterWalls = ignorePerimeterWalls;
			IgnoreCore = ignoreCore;
			IgnoreBuildings = ignoreBuildings;
			PassThroughCellX = passThroughCellX;
			PassThroughCellY = passThroughCellY;
			PathRadiusOverride = pathRadiusOverride;
		}

		public static PathAllow ForZombieTarget(
			bool ignorePerimeterWalls,
			PlacedBuilding building = null,
			WallSegment wall = null,
			bool towardCore = false,
			bool ignoreBuildings = false,
			float pathRadiusOverride = 0f )
		{
			int? passX = building?.CellX;
			int? passY = building?.CellY;
			if ( passX is null && wall is not null && BuildGrid.WorldToCell( wall.Center, out var wx, out var wy ) )
			{
				passX = wx;
				passY = wy;
			}

			return new(
				ignorePerimeterWalls,
				towardCore,
				ignoreBuildings,
				passX,
				passY,
				pathRadiusOverride );
		}
	}

	static float ResolveZombieAgentRadius( PathAllow allow ) =>
		allow.PathRadiusOverride > 0.01f
			? allow.PathRadiusOverride
			: GameConstants.ZombiePathRadius;

	public static bool BlocksUnit(
		Vector3 worldPos,
		float agentRadius = -1f,
		bool ignorePerimeterWalls = false,
		bool forZombieMelee = false,
		PathAllow allow = default )
	{
		agentRadius = forZombieMelee
			? ResolveZombieAgentRadius( allow )
			: ResolveAgentRadius( agentRadius );

		// Wall-mounted towers share tiles with perimeter walls. A normal pathing radius clips
		// neighboring wall cells and binary-searches into a crawl. Shrink the probe when we
		// already have a pass-through target tile so attackers can finish the last approach.
		if ( forZombieMelee
		     && allow.PassThroughCellX is int px
		     && allow.PassThroughCellY is int py
		     && BuildGrid.WorldToCell( worldPos, out var cx, out var cy )
		     && Math.Abs( cx - px ) + Math.Abs( cy - py ) <= 1 )
		{
			agentRadius = MathF.Min( agentRadius, 2f );
		}

		// Same model as walls: occupied grid cells (building cell, core cells, wall path cells).
		return TileOccupancy.IsWorldBlocked(
			worldPos,
			agentRadius,
			allow.IgnorePerimeterWalls || ignorePerimeterWalls,
			allow.PassThroughCellX,
			allow.PassThroughCellY,
			allow.IgnoreCore,
			allow.IgnoreBuildings );
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
	public static Vector3 ApproachPoint(
		Vector3 from,
		Vector3 center,
		Vector3 size,
		float agentRadius = -1f,
		float standoff = 2f )
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

		return closest + away.Normal * (agentRadius + standoff);
	}

	public static Vector3 ApproachPointForZombie(
		Vector3 from,
		Vector3 center,
		Vector3 size,
		PathAllow allow = default )
	{
		var radius = ResolveZombieAgentRadius( allow );
		from = from.WithZ( 0f );
		center = center.WithZ( 0f );

		var closest = ClosestPointOnFootprint( from, center, size );
		var away = (from - closest).WithZ( 0f );
		if ( away.Length < 0.001f )
		{
			away = (from - center).WithZ( 0f );
			if ( away.Length < 0.001f )
				away = Vector3.Right;
		}

		away = away.Normal;
		var point = closest + away * (radius + GameConstants.ZombieApproachStandoff);

		// Pass-through target cell may sit on the face.
		if ( allow.PassThroughCellX is int px
		     && allow.PassThroughCellY is int py
		     && BuildGrid.WorldToCell( point, out var cx, out var cy )
		     && cx == px && cy == py
		     && !BlocksUnit( point, ignorePerimeterWalls: allow.IgnorePerimeterWalls, forZombieMelee: true, allow: allow ) )
			return point;

		// Keep walking outward from the structure until the stand is free (grid cells ≠ mesh face).
		for ( var i = 0; i < 16; i++ )
		{
			if ( !BlocksUnit( point, ignorePerimeterWalls: allow.IgnorePerimeterWalls, forZombieMelee: true, allow: allow ) )
				return point;

			point += away * MathF.Max( 8f, radius );
		}

		return EnsureClearStand(
			point,
			from,
			radius,
			allow.IgnorePerimeterWalls,
			forZombieMelee: true,
			allow );
	}

	/// <summary>
	/// Stand point on the exterior (or interior) face of a wall-mounted building, along the
	/// perimeter outward normal so zombies don't crawl into neighboring wall tiles.
	/// </summary>
	public static Vector3 ApproachPointForWallMounted(
		Vector3 from,
		Vector3 center,
		Vector3 size,
		PathAllow allow = default )
	{
		center = center.WithZ( 0f );
		from = from.WithZ( 0f );
		var half = size.WithZ( 0f ) * 0.5f;
		var side = WallApproach.FromWorldPosition( center, Vector3.Zero );
		var outward = WallApproach.OutwardNormal( side );
		var toFrom = from - center;
		var faceDir = Vector3.Dot( toFrom, outward ) >= 0f ? outward : -outward;
		var extent = MathF.Abs( faceDir.x ) > 0.5f ? half.x : half.y;
		var radius = ResolveZombieAgentRadius( allow );
		var point = center + faceDir * (extent + radius + GameConstants.ZombieApproachStandoff);

		for ( var i = 0; i < 16; i++ )
		{
			if ( !BlocksUnit( point, ignorePerimeterWalls: allow.IgnorePerimeterWalls, forZombieMelee: true, allow: allow ) )
				return point;

			point += faceDir * MathF.Max( 8f, radius );
		}

		return EnsureClearStand(
			point,
			from,
			radius,
			allow.IgnorePerimeterWalls,
			forZombieMelee: true,
			allow );
	}

	/// <summary>
	/// True when the walk segment from→to overlaps an occupied pathing cell (excluding pass-through).
	/// </summary>
	public static bool SegmentHitsBlocker(
		Vector3 from,
		Vector3 to,
		PathAllow allow = default,
		int samples = 0 )
	{
		from = from.WithZ( 0f );
		to = to.WithZ( 0f );
		var delta = to - from;
		var len = delta.Length;
		if ( len < 4f ) return false;

		if ( samples <= 0 )
			samples = Math.Clamp( (int)(len / 24f) + 2, 6, 28 );

		for ( var i = 1; i <= samples; i++ )
		{
			var t = i / (float)samples;
			var p = from + delta * t;
			if ( BlocksUnit( p, ignorePerimeterWalls: allow.IgnorePerimeterWalls, forZombieMelee: true, allow: allow ) )
				return true;
		}

		return false;
	}

	/// <summary>
	/// Picks a clear corner waypoint around the first occupied footprint blocking from→goal.
	/// Only uses waypoints we can walk to without crossing the blocker (no far-side midpoints —
	/// those stranded agents against HQ while the goal sat on the opposite face).
	/// </summary>
	public static bool TryDetourWaypoint(
		Vector3 from,
		Vector3 goal,
		PathAllow allow,
		out Vector3 waypoint ) =>
		TryDetourWaypoint( from, goal, allow, skipPerimeterWalls: false, out waypoint );

	public static bool TryDetourWaypoint(
		Vector3 from,
		Vector3 goal,
		PathAllow allow,
		bool skipPerimeterWalls,
		out Vector3 waypoint )
	{
		from = from.WithZ( 0f );
		goal = goal.WithZ( 0f );
		waypoint = goal;

		if ( !TryFirstBlockerAlong( from, goal, allow, skipPerimeterWalls, out var blockCenter, out var blockHalf ) )
			return false;

		var radius = ResolveZombieAgentRadius( allow );
		// Stand outside the rasterized cells with walkway clearance for corner skirting.
		// Command post gets extra pad so waypoints sit past the 2×2 cell exterior corner
		// instead of dead on it (agents were grinding there when walking past HQ).
		var isCore = blockHalf.x >= BuildGrid.CommandPostFootprint.x * 0.45f
			&& blockHalf.y >= BuildGrid.CommandPostFootprint.y * 0.45f;
		var pad = radius + MathF.Max( GameConstants.U( 12f ), GameConstants.ZombieApproachStandoff * 0.5f );
		if ( isCore )
			pad += GameConstants.CellSize * 0.45f;
		var hx = blockHalf.x + pad;
		var hy = blockHalf.y + pad;
		var c = blockCenter;

		// Corners only — face centers on the far side look "straight" but require going through.
		var candidates = new[]
		{
			new Vector3( c.x - hx, c.y - hy, 0f ),
			new Vector3( c.x - hx, c.y + hy, 0f ),
			new Vector3( c.x + hx, c.y - hy, 0f ),
			new Vector3( c.x + hx, c.y + hy, 0f )
		};

		var toGoal = goal - from;
		var direct = toGoal.Length;
		if ( direct < 0.001f )
			return false;

		// When skirting HQ/buildings after a breach, ignore walls on the segment probe so we
		// don't reject every corner that clips a perimeter cell.
		var probeAllow = skipPerimeterWalls
			? new PathAllow(
				true,
				allow.IgnoreCore,
				allow.IgnoreBuildings,
				allow.PassThroughCellX,
				allow.PassThroughCellY,
				allow.PathRadiusOverride )
			: allow;

		var bestScore = float.MinValue;
		var found = false;
		foreach ( var candidate in candidates )
		{
			var cand = candidate;
			// Push core corners further radially so the stand point is clearly past the L-corner.
			if ( isCore )
			{
				var radial = (cand - c).WithZ( 0f );
				if ( radial.Length > 0.001f )
					cand = c + radial.Normal * (radial.Length + GameConstants.CellSize * 0.35f);
			}

			if ( BlocksUnit( cand, ignorePerimeterWalls: probeAllow.IgnorePerimeterWalls, forZombieMelee: true, allow: probeAllow ) )
			{
				cand = EnsureClearStand(
					cand,
					from,
					radius,
					probeAllow.IgnorePerimeterWalls,
					forZombieMelee: true,
					probeAllow );
			}

			if ( BlocksUnit( cand, ignorePerimeterWalls: probeAllow.IgnorePerimeterWalls, forZombieMelee: true, allow: probeAllow ) )
				continue;

			var leave = (cand - from).WithZ( 0f ).Length;
			var fromCoreDist = (from - c).WithZ( 0f ).Length;
			var candCoreDist = (cand - c).WithZ( 0f ).Length;
			// Reject tiny hops unless the waypoint is meaningfully further around/out from HQ.
			if ( leave < 14f && candCoreDist <= fromCoreDist + 10f )
				continue;

			if ( SegmentHitsBlocker( from, cand, probeAllow ) )
				continue;

			var via = leave + (goal - cand).WithZ( 0f ).Length;
			if ( via > direct * 3.5f )
				continue;

			var clearsRest = !SegmentHitsBlocker( cand, goal, probeAllow );
			var score = -via + (clearsRest ? 200f : 0f);
			// Prefer corners that sit further from the blocker center (clearer of the L-corner).
			if ( isCore )
				score += MathF.Min( 40f, candCoreDist - fromCoreDist );
			if ( score <= bestScore )
				continue;

			bestScore = score;
			waypoint = cand;
			found = true;
		}

		return found;
	}

	/// <summary>
	/// True when the agent has finished skirting the command-post AABB toward <paramref name="finalGoal"/>.
	/// </summary>
	public static bool HasClearedCoreSkirt( Vector3 pos, Vector3 detourGoal, Vector3 finalGoal, Vector3 corePos )
	{
		pos = pos.WithZ( 0f );
		detourGoal = detourGoal.WithZ( 0f );
		finalGoal = finalGoal.WithZ( 0f );
		corePos = corePos.WithZ( 0f );

		if ( (detourGoal - pos).Length <= 22f )
			return true;

		var half = BuildGrid.CommandPostZombieCollisionFootprint * 0.5f;
		var clearPad = GameConstants.ZombiePathRadius + GameConstants.CellSize * 0.35f;
		var outside =
			MathF.Abs( pos.x - corePos.x ) > half.x + clearPad
			|| MathF.Abs( pos.y - corePos.y ) > half.y + clearPad;
		if ( !outside )
			return false;

		// On the goal's side of HQ (past the near faces), resume direct seek only if clear.
		var toGoal = finalGoal - corePos;
		var toPos = pos - corePos;
		if ( MathF.Abs( toGoal.x ) >= MathF.Abs( toGoal.y ) )
		{
			if ( MathF.Sign( toGoal.x ) != 0 && MathF.Sign( toPos.x ) == MathF.Sign( toGoal.x )
			     && MathF.Abs( toPos.x ) > half.x + clearPad * 0.5f )
				return !SegmentHitsBlocker( pos, finalGoal, default );
		}
		else
		{
			if ( MathF.Sign( toGoal.y ) != 0 && MathF.Sign( toPos.y ) == MathF.Sign( toGoal.y )
			     && MathF.Abs( toPos.y ) > half.y + clearPad * 0.5f )
				return !SegmentHitsBlocker( pos, finalGoal, default );
		}

		return !SegmentHitsBlocker( pos, finalGoal, default );
	}

	static bool TryFirstBlockerAlong(
		Vector3 from,
		Vector3 to,
		PathAllow allow,
		bool skipPerimeterWalls,
		out Vector3 blockCenter,
		out Vector3 blockHalf )
	{
		blockCenter = default;
		blockHalf = default;
		var delta = to - from;
		var len = delta.Length;
		if ( len < 4f )
			return false;

		var samples = Math.Clamp( (int)(len / 24f) + 2, 6, 28 );
		for ( var i = 1; i <= samples; i++ )
		{
			var p = from + delta * (i / (float)samples);
			if ( !BlocksUnit( p, ignorePerimeterWalls: allow.IgnorePerimeterWalls || skipPerimeterWalls, forZombieMelee: true, allow: allow ) )
				continue;

			if ( !BuildGrid.WorldToCell( p, out var cx, out var cy ) )
				continue;

			if ( skipPerimeterWalls && TileOccupancy.IsWallPathCell( cx, cy ) && !BuildGrid.IsCoreCell( cx, cy )
			     && !TileOccupancy.IsBuildingCell( cx, cy ) )
				continue;

			if ( BuildGrid.IsCoreCell( cx, cy ) )
			{
				var core = OutpostManager.Instance?.CorePosition ?? Vector3.Zero;
				blockCenter = core.WithZ( 0f );
				var half = BuildGrid.CommandPostZombieCollisionFootprint * 0.5f;
				blockHalf = half.WithZ( 0f );
				return true;
			}

			if ( TileOccupancy.IsBuildingCell( cx, cy ) && !BuildGrid.IsCoreCell( cx, cy ) )
			{
				blockCenter = BuildGrid.CellToWorld( cx, cy ).WithZ( 0f );
				blockHalf = new Vector3( GameConstants.CellSize * 0.5f, GameConstants.CellSize * 0.5f, 0f );
				return true;
			}

			// Perimeter walls are never useful detour AABBs (keeps agents skating the ring).
			if ( TileOccupancy.IsWallPathCell( cx, cy ) )
				continue;
		}

		return false;
	}

	/// <summary>
	/// Pushes a stand point off occupied tiles so path goals never sit inside blockers.
	/// Prefer falling back to <paramref name="preferFrom"/> over returning a blocked goal.
	/// For zombies, stay near the preferred side — never jump a goal across the map.
	/// </summary>
	public static Vector3 EnsureClearStand(
		Vector3 desired,
		Vector3 preferFrom,
		float agentRadius = -1f,
		bool ignorePerimeterWalls = false,
		bool forZombieMelee = false,
		PathAllow allow = default )
	{
		agentRadius = forZombieMelee
			? ResolveZombieAgentRadius( allow )
			: ResolveAgentRadius( agentRadius );
		desired = desired.WithZ( 0f );
		preferFrom = preferFrom.WithZ( 0f );

		if ( !BlocksUnit( desired, agentRadius, ignorePerimeterWalls, forZombieMelee, allow ) )
			return desired;

		var away = (preferFrom - desired).WithZ( 0f );
		if ( away.Length < 0.001f )
			away = Vector3.Right;
		away = away.Normal;

		var step = MathF.Max( 6f, agentRadius );
		var maxPush = forZombieMelee ? GameConstants.CellSize * 2.5f : step * 12f;
		var maxI = forZombieMelee ? 20 : 12;
		for ( var i = 1; i <= maxI; i++ )
		{
			var push = step * i;
			if ( push > maxPush )
				break;

			var candidate = desired + away * push;
			if ( !BlocksUnit( candidate, agentRadius, ignorePerimeterWalls, forZombieMelee, allow ) )
				return candidate;
		}

		if ( forZombieMelee )
		{
			// Near the intended face first — never "stay put" while the desired approach is still far.
			if ( TryFindClearPoint(
				    desired,
				    agentRadius,
				    GameConstants.CellSize * 2f,
				    out var nearDesired,
				    28,
				    ignorePerimeterWalls,
				    forZombieMelee,
				    allow ) )
				return nearDesired;

			if ( TryFindClearPoint(
				    preferFrom,
				    agentRadius,
				    GameConstants.CellSize * 1.5f,
				    out var nearFrom,
				    20,
				    ignorePerimeterWalls,
				    forZombieMelee,
				    allow )
			     && (nearFrom - preferFrom).WithZ( 0f ).Length > 4f )
				return nearFrom;

			if ( !BlocksUnit( preferFrom, agentRadius, ignorePerimeterWalls, forZombieMelee, allow ) )
				return preferFrom;

			return preferFrom;
		}

		if ( TryFindClearPoint( desired, step, GameConstants.CellSize * 1.5f, out var clear, 36, ignorePerimeterWalls, forZombieMelee, allow ) )
			return clear;

		if ( TryEscape( desired, out var escape, agentRadius, ignorePerimeterWalls, forZombieMelee, allow ) )
			return escape;

		if ( !BlocksUnit( preferFrom, agentRadius, ignorePerimeterWalls, forZombieMelee, allow ) )
			return preferFrom;

		if ( TryEscape( preferFrom, out var fromEscape, agentRadius, ignorePerimeterWalls, forZombieMelee, allow ) )
			return fromEscape;

		if ( TryFindClearPoint( preferFrom, step, GameConstants.CellSize * 2f, out var nearPrefer, 28, ignorePerimeterWalls, forZombieMelee, allow ) )
			return nearPrefer;

		return preferFrom;
	}

	/// <summary>True when a world point lies on a command-post or building tile.</summary>
	public static bool IsInsideBuildingFootprint( Vector3 worldPos )
	{
		if ( !BuildGrid.WorldToCell( worldPos, out var cellX, out var cellY ) )
			return false;

		return TileOccupancy.IsBuildingCell( cellX, cellY );
	}

	/// <summary>
	/// Can a projectile travel from origin to target without passing through a solid building?
	/// Perimeter walls and scaffold wall pieces are ignored — defenses shoot through them.
	/// The shooter's own cell is skipped so turrets aren't blinded by their tile.
	/// </summary>
	/// <param name="ignoreCellX">Optional placement cell to always skip (tower base when muzzle sticks past the edge).</param>
	/// <param name="ignoreCellY">Optional placement cell Y paired with <paramref name="ignoreCellX"/>.</param>
	public static bool HasLineOfFire(
		Vector3 origin,
		Vector3 target,
		float targetMargin = 28f * GameConstants.TileScale,
		int ignoreCellX = int.MinValue,
		int ignoreCellY = int.MinValue )
	{
		origin = origin.WithZ( 0f );
		target = target.WithZ( 0f );
		var seg = target - origin;
		var len = seg.Length;
		if ( len < GameConstants.U( 8f ) ) return true;

		BuildGrid.WorldToCell( origin, out var originX, out var originY );
		var hasIgnoreCell = ignoreCellX != int.MinValue && ignoreCellY != int.MinValue;

		var dir = seg / len;
		var steps = Math.Max( 2, (int)(len / GameConstants.U( 32f )) );
		var muzzleClearance = GameConstants.U( 22f );
		for ( var i = 1; i < steps; i++ )
		{
			var distAlong = len * (i / (float)steps);
			if ( distAlong < muzzleClearance )
				continue;
			if ( len - distAlong < targetMargin )
				break;

			var sample = origin + dir * distAlong;
			if ( !BuildGrid.WorldToCell( sample, out var cellX, out var cellY ) )
				continue;

			// Don't treat the firing tile (or the shooter's placement cell) as an occluder.
			if ( cellX == originX && cellY == originY )
				continue;
			if ( hasIgnoreCell && cellX == ignoreCellX && cellY == ignoreCellY )
				continue;

			if ( TileOccupancy.BlocksLineOfFire( cellX, cellY ) )
				return false;
		}

		return true;
	}

	/// <summary>Steps toward <paramref name="target"/> without entering occupied tiles.</summary>
	public static Vector3 ResolveStep(
		Vector3 from,
		Vector3 target,
		float step,
		float agentRadius = -1f,
		bool ignorePerimeterWalls = false,
		bool forZombieMelee = false,
		PathAllow allow = default )
	{
		agentRadius = forZombieMelee
			? ResolveZombieAgentRadius( allow )
			: ResolveAgentRadius( agentRadius );
		from = from.WithZ( 0f );
		target = target.WithZ( 0f );

		if ( BlocksUnit( from, agentRadius, ignorePerimeterWalls, forZombieMelee, allow )
		     && TryEscape( from, out var escaped, agentRadius, ignorePerimeterWalls, forZombieMelee, allow ) )
		{
			var escapeDelta = (escaped - from).WithZ( 0f );
			var escapeCap = forZombieMelee
				? MathF.Max( step, agentRadius )
				: MathF.Max( step, agentRadius * 1.5f );
			var escapeStep = MathF.Min( escapeDelta.Length, escapeCap );
			if ( escapeStep > 0.01f )
				from = from + escapeDelta.Normal * escapeStep;
		}

		var delta = target - from;
		var dist = delta.Length;
		if ( dist < 0.001f ) return from;

		var move = MathF.Min( step, dist );
		var desired = from + delta / dist * move;
		return ResolveMove( from, desired, agentRadius, ignorePerimeterWalls, forZombieMelee, allow );
	}

	public static Vector3 ResolveZombieStep(
		Vector3 from,
		Vector3 target,
		float step,
		bool ignorePerimeterWalls = false,
		PathAllow allow = default ) =>
		ResolveStep( from, target, step, ResolveZombieAgentRadius( allow ), ignorePerimeterWalls, forZombieMelee: true, allow );

	/// <summary>
	/// Resolves a single short move. Slides stay within this frame's step length so agents turn/walk
	/// around obstacles instead of sideways-teleporting.
	/// </summary>
	public static Vector3 ResolveMove(
		Vector3 from,
		Vector3 desired,
		float agentRadius = -1f,
		bool ignorePerimeterWalls = false,
		bool forZombieMelee = false,
		PathAllow allow = default )
	{
		agentRadius = forZombieMelee
			? ResolveZombieAgentRadius( allow )
			: ResolveAgentRadius( agentRadius );
		from = from.WithZ( 0f );
		desired = desired.WithZ( 0f );

		if ( !BlocksUnit( desired, agentRadius, ignorePerimeterWalls, forZombieMelee, allow ) )
			return desired;

		var delta = desired - from;
		var full = delta.Length;
		if ( full <= 0.001f )
			return from;

		var prefer = delta / full;
		var best = from;
		var bestScore = -1f;

		var lo = 0f;
		var hi = full;
		for ( var i = 0; i < 6; i++ )
		{
			var mid = (lo + hi) * 0.5f;
			if ( BlocksUnit( from + prefer * mid, agentRadius, ignorePerimeterWalls, forZombieMelee, allow ) )
				hi = mid;
			else
				lo = mid;
		}

		Consider( from + prefer * lo );

		Consider( from + new Vector3( delta.x, 0f, 0f ) );
		Consider( from + new Vector3( 0f, delta.y, 0f ) );

		var perp = new Vector3( -prefer.y, prefer.x, 0f );
		var degrees = forZombieMelee
			? new[] { 15f, 30f, 45f, 60f, 80f, 100f, 125f, 150f }
			: new[] { 20f, 35f, 50f, 70f, 90f };
		foreach ( var deg in degrees )
		{
			var rad = deg * (MathF.PI / 180f);
			var cos = MathF.Cos( rad );
			var sin = MathF.Sin( rad );
			var left = (prefer * cos + perp * sin).Normal;
			var right = (prefer * cos - perp * sin).Normal;
			Consider( from + left * full );
			Consider( from + right * full );
		}

		return best;

		void Consider( Vector3 candidate )
		{
			candidate = candidate.WithZ( 0f );
			var move = candidate - from;
			var len = move.Length;
			if ( len < 0.01f ) return;

			if ( len > full + 0.05f )
			{
				candidate = from + move / len * full;
				move = candidate - from;
				len = move.Length;
			}

			if ( BlocksUnit( candidate, agentRadius, ignorePerimeterWalls, forZombieMelee, allow ) )
				return;

			var progress = Vector3.Dot( move, prefer );
			var score = progress * 2.5f + len * 0.35f;
			if ( score <= bestScore ) return;
			best = candidate;
			bestScore = score;
		}
	}

	public static bool TryFindClearPoint(
		Vector3 center,
		float minRadius,
		float maxRadius,
		out Vector3 point,
		int attempts = 12,
		bool ignorePerimeterWalls = false,
		bool forZombieMelee = false,
		PathAllow allow = default )
	{
		center = center.WithZ( 0f );
		minRadius = MathF.Max( 0f, minRadius );
		maxRadius = MathF.Max( minRadius, maxRadius );
		var agentRadius = forZombieMelee ? ResolveZombieAgentRadius( allow ) : -1f;

		for ( var i = 0; i < attempts; i++ )
		{
			var angle = Game.Random.Float( 0f, MathF.PI * 2f );
			var r = Game.Random.Float( minRadius, maxRadius );
			var candidate = center + new Vector3( MathF.Cos( angle ) * r, MathF.Sin( angle ) * r, 0f );
			if ( !BlocksUnit( candidate, agentRadius, ignorePerimeterWalls, forZombieMelee, allow ) )
			{
				point = candidate;
				return true;
			}
		}

		point = center;
		if ( !BlocksUnit( center, agentRadius, ignorePerimeterWalls, forZombieMelee, allow ) )
			return true;

		return TryEscape( center, out point, agentRadius, ignorePerimeterWalls, forZombieMelee, allow );
	}

	public static bool TryEscape(
		Vector3 from,
		out Vector3 point,
		float agentRadius = -1f,
		bool ignorePerimeterWalls = false,
		bool forZombieMelee = false,
		PathAllow allow = default )
	{
		agentRadius = forZombieMelee
			? ResolveZombieAgentRadius( allow )
			: ResolveAgentRadius( agentRadius );
		from = from.WithZ( 0f );

		if ( !BlocksUnit( from, agentRadius, ignorePerimeterWalls, forZombieMelee, allow ) )
		{
			point = from;
			return true;
		}

		var cellSize = GameConstants.CellSize;
		for ( var ring = 1; ring <= 10; ring++ )
		{
			var push = MathF.Max( agentRadius, 8f ) * ring * 0.75f;
			if ( ring >= 5 )
				push = cellSize * 0.35f * (ring - 3);

			var samples = ring <= 4 ? 8 : 12;
			for ( var i = 0; i < samples; i++ )
			{
				var angle = i / (float)samples * MathF.PI * 2f;
				var candidate = from + new Vector3( MathF.Cos( angle ) * push, MathF.Sin( angle ) * push, 0f );
				if ( !BlocksUnit( candidate, agentRadius, ignorePerimeterWalls, forZombieMelee, allow ) )
				{
					point = candidate;
					return true;
				}
			}
		}

		if ( BuildGrid.WorldToCell( from, out var cx, out var cy ) )
		{
			for ( var r = 1; r <= 3; r++ )
			{
				for ( var dx = -r; dx <= r; dx++ )
				for ( var dy = -r; dy <= r; dy++ )
				{
					if ( Math.Abs( dx ) != r && Math.Abs( dy ) != r ) continue;
					var cell = BuildGrid.CellToWorld( cx + dx, cy + dy ).WithZ( 0f );
					if ( !BlocksUnit( cell, agentRadius, ignorePerimeterWalls, forZombieMelee, allow ) )
					{
						point = cell;
						return true;
					}
				}
			}
		}

		point = from;
		return false;
	}

	private static float ResolveAgentRadius( float agentRadius ) =>
		agentRadius > 0f ? agentRadius : GameConstants.UnitCollisionRadius;
}
