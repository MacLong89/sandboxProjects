namespace Sandbox;

/// <summary>Inherited plane math aligned to dev proxies — no XY grid rounding; uses authoritative structure transforms.</summary>
public static class ThornsSnapPoseUtility
{
	static float Cs => ThornsBuildingModule.Cell;
	static float Ft => ThornsBuildingModule.FloorThickness;
	static float Wh => ThornsBuildingModule.WallHeight;
	static float T => ThornsBuildingModule.WallThickness;
	static float Hh => Cs * 0.5f;

	/// <summary>World-space horizontal mid-edge point at slab storey height — edge index [0=N,+Y outward … 3=W].</summary>
	public static Vector3 FoundationEdgeMidpointHorizontal( Vector3 slabCenterWorld, Rotation hostRotWorld, int edge0123 )
	{
		var localFlat = EdgeLocalMidpointXY( edge0123 );
		var planar = hostRotWorld * new Vector3( localFlat.x, localFlat.y, 0f );
		return slabCenterWorld + planar;
	}

	/// <summary>World Z of the top face of a horizontal foundation slab (uses actual centre, not storey-band quantization).</summary>
	public static float FoundationTopElevationWorldZ( Vector3 slabCenterWorld ) =>
		slabCenterWorld.z + Ft * 0.5f;

	public static float WallMidpointCenterZWorld( Vector3 slabCenterWorld ) =>
		FoundationTopElevationWorldZ( slabCenterWorld ) + Wh * 0.5f;

	/// <summary>Ramp pivot Z when snapped flush to foundation top (full cell footprint), plus a small lift so the mesh clears the slab visually.</summary>
	public static float RampSeatPivotZWorld( Vector3 slabCenterWorld )
	{
		var top = slabCenterWorld.z + Ft * 0.5f;
		return top + Wh * 0.22f + 20f;
	}

	/// <summary>Relative yaw appended in <see cref="WallRotationAroundUpFromEdge"/>.</summary>
	public static float EdgeWallLocalYawDegrees( int foundationEdge0123 ) =>
		foundationEdge0123 switch
		{
			0 => 90f,
			1 => 0f,
			2 => 270f,
			_ => 180f
		};

	public static Rotation WallRotationAroundUpFromEdge( Rotation hostBasis, int foundationEdge0123 ) =>
		hostBasis * Rotation.FromYaw( EdgeWallLocalYawDegrees( foundationEdge0123 ) );

	/// <summary>Neighbour slab centre displaced one full footprint along planar horizontal edge.</summary>
	public static Vector3 NeighbourFoundationCenterWorldFromEdge( Vector3 slabCenterWorld, Rotation hostBasis, int exitingEdge0123 )
	{
		var dir = HorizontalOutwardDirection( hostBasis, exitingEdge0123 );
		return slabCenterWorld + dir * Cs;
	}

	static Vector2 EdgeLocalMidpointXY( int edge0123 )
	{
		switch ( edge0123 )
		{
			case 0:
				return new Vector2( 0f, Hh - T * 0.5f );
			case 1:
				return new Vector2( Hh - T * 0.5f, 0f );
			case 2:
				return new Vector2( 0f, -Hh + T * 0.5f );
			default:
				return new Vector2( -Hh + T * 0.5f, 0f );
		}
	}

	/// <summary>
	/// Finds the slab <c>F</c>; below whose edge this wall sits, then lifts a ceiling tile:<br/>
	/// XY = <paramref name="F"/>, Z = wall top + slab half-thickness.<br/>
	/// Uses scene foundations (exact) rather than quaternion inversion (rotation multiplication order differs by engine).
	/// </summary>
	public static bool TryCeilingFoundationCentreFromWall(
		ThornsPlacedStructure wall,
		out Vector3 slabCentreWorld,
		out Rotation slabPlanRotation )
	{
		slabCentreWorld = default;
		slabPlanRotation = default;

		if ( wall.StructureDefId is not ("wood_wall" or "wood_window" or "wood_doorframe") )
			return false;

		var scene = wall.GameObject.Scene;
		if ( !scene.IsValid() )
			return false;

		var wallPos = wall.GameObject.WorldPosition;

		float bestScore = float.MaxValue;
		Vector3 bestFxyz = default;
		Rotation bestRf = Rotation.Identity;

		foreach ( var ps in scene.GetAllComponents<ThornsPlacedStructure>() )
		{
			if ( !ps.IsValid() )
				continue;
			if ( ps.StructureDefId != "wood_foundation" )
				continue;

			var fpos = ps.GameObject.WorldPosition;
			var fr = ps.GameObject.WorldRotation;

			for ( var e = 0; e < 4; e++ )
			{
				var eh = FoundationEdgeMidpointHorizontal( fpos, fr, e );

				var wzMid = WallMidpointCenterZWorld( fpos );
				var predictedWallMidpoint = new Vector3( eh.x, eh.y, wzMid );
				var d = (predictedWallMidpoint - wallPos).Length;

				if ( !( d < bestScore ) )
					continue;

				bestScore = d;
				bestFxyz = fpos;
				bestRf = fr;
			}
		}

		if ( bestScore > 44f )
			return false;

		var zSlab = wallPos.z + Wh * 0.5f + Ft * 0.5f;

		slabCentreWorld = new Vector3( bestFxyz.x, bestFxyz.y, zSlab );
		slabPlanRotation = bestRf;

		return true;
	}

	static Vector3 HorizontalOutwardDirection( Rotation hostBasis, int edge0123 )
	{
		var local = edge0123 switch
		{
			0 => new Vector3( 0f, 1f, 0f ),
			1 => new Vector3( 1f, 0f, 0f ),
			2 => new Vector3( 0f, -1f, 0f ),
			_ => new Vector3( -1f, 0f, 0f )
		};

		var outward = hostBasis * local;
		var flat = new Vector3( outward.x, outward.y, 0f );
		return flat.Normal;
	}
}
