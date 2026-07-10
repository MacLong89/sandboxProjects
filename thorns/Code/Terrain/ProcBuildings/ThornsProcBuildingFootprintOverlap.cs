namespace Sandbox;

/// <summary>2D oriented-box overlap tests for procedural building placement (chunk-local XY).</summary>
public static class ThornsProcBuildingFootprintOverlap
{
	public static bool ObbsOverlap(
		float ax,
		float ay,
		float aHalfW,
		float aHalfD,
		float aYawRad,
		float bx,
		float by,
		float bHalfW,
		float bHalfD,
		float bYawRad,
		float gapWorld )
	{
		gapWorld = MathF.Max( 0f, gapWorld );
		var expandA = gapWorld * 0.5f;
		var expandB = gapWorld * 0.5f;
		aHalfW += expandA;
		aHalfD += expandA;
		bHalfW += expandB;
		bHalfD += expandB;

		Span<Vector2> cornersA = stackalloc Vector2[4];
		Span<Vector2> cornersB = stackalloc Vector2[4];
		FillObbCorners( ax, ay, aHalfW, aHalfD, aYawRad, cornersA );
		FillObbCorners( bx, by, bHalfW, bHalfD, bYawRad, cornersB );

		Span<Vector2> axes = stackalloc Vector2[4];
		axes[0] = AxisFromYaw( aYawRad );
		axes[1] = Perpendicular( axes[0] );
		axes[2] = AxisFromYaw( bYawRad );
		axes[3] = Perpendicular( axes[2] );

		for ( var i = 0; i < 4; i++ )
		{
			var axis = axes[i];
			if ( axis.LengthSquared < 0.0001f )
				continue;

			axis = axis.Normal;
			Project( cornersA, axis, out var minA, out var maxA );
			Project( cornersB, axis, out var minB, out var maxB );
			if ( maxA < minB || maxB < minA )
				return false;
		}

		return true;
	}

	public static void GetTypeMaxHalfExtents( ThornsProcBuildingType type, out float halfW, out float halfD )
	{
		var def = ThornsProcBuildingIdentityRegistry.Get( type );
		var cell = ThornsBuildingModule.Cell;
		halfW = def.WidthMax * cell * 0.5f;
		halfD = def.DepthMax * cell * 0.5f;
	}

	/// <summary>Whether the type's largest registry footprint fits inside a lot OBB (with slack).</summary>
	public static bool TypeFitsLot( ThornsProcBuildingType type, float lotHalfW, float lotHalfD, float slack = 1.05f )
	{
		GetTypeMaxHalfExtents( type, out var halfW, out var halfD );
		return halfW <= lotHalfW * slack && halfD <= lotHalfD * slack;
	}

	static void FillObbCorners( float cx, float cy, float halfW, float halfD, float yawRad, Span<Vector2> corners )
	{
		var c = MathF.Cos( yawRad );
		var s = MathF.Sin( yawRad );
		var ax = new Vector2( c, s ) * halfW;
		var ay = new Vector2( -s, c ) * halfD;
		var center = new Vector2( cx, cy );
		corners[0] = center - ax - ay;
		corners[1] = center + ax - ay;
		corners[2] = center + ax + ay;
		corners[3] = center - ax + ay;
	}

	static Vector2 AxisFromYaw( float yawRad ) => new( MathF.Cos( yawRad ), MathF.Sin( yawRad ) );

	static Vector2 Perpendicular( Vector2 v ) => new( -v.y, v.x );

	static void Project( ReadOnlySpan<Vector2> corners, Vector2 axis, out float min, out float max )
	{
		min = max = Vector2.Dot( corners[0], axis );
		for ( var i = 1; i < corners.Length; i++ )
		{
			var p = Vector2.Dot( corners[i], axis );
			min = MathF.Min( min, p );
			max = MathF.Max( max, p );
		}
	}
}
