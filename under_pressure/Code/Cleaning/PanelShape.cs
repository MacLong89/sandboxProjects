namespace UnderPressure;

/// <summary>Silhouette of a dirty panel within its bounding rectangle.</summary>
public enum PanelShape
{
	/// <summary>Entire rectangle is cleanable.</summary>
	Full,
	/// <summary>Soft rounded rectangle.</summary>
	Rounded,
	/// <summary>Circular / spill pool.</summary>
	Circle,
	/// <summary>Wide oval stain.</summary>
	Ellipse,
	/// <summary>Driveway taper — wide at the street, narrow toward the garage.</summary>
	Driveway,
	/// <summary>Long bay with rounded ends (car wash, loading lane).</summary>
	CarBay,
	/// <summary>Irregular splatter blob.</summary>
	OilSpill,
	/// <summary>Wide low platform slab.</summary>
	Platform,
	/// <summary>Teak deck with soft corners.</summary>
	Deck,
	/// <summary>Very wide, short strip (guardrail, banner, rug).</summary>
	Banner,
	/// <summary>Tall narrow strip (container door, pillar, window stack).</summary>
	Strip,
	/// <summary>Small centered pane with margin (observation glass).</summary>
	Window,
	/// <summary>L-shaped corner pad.</summary>
	LCorner,
	/// <summary>Plus-shaped valve cluster.</summary>
	Cross,
	/// <summary>Donut ring around a dry center.</summary>
	Ring,
}

/// <summary>How grime is shaded when a layer is baked.</summary>
public enum GrimePattern
{
	Organic,
	Streaks,
	Splatter,
	Speckled,
	Rust,
}

/// <summary>Maps normalized panel UV (0..1) to active cleanable texels.</summary>
public static class PanelShapeMask
{
	public static bool IsActive( PanelShape shape, float u, float v, float aspect )
	{
		switch ( shape )
		{
			case PanelShape.Full:
				return true;
			case PanelShape.Rounded:
				return InRoundedRect( u, v, 0.07f );
			case PanelShape.Circle:
				return InEllipse( u, v, 0.46f, 0.46f );
			case PanelShape.Ellipse:
				return InEllipse( u, v, 0.48f, 0.34f );
			case PanelShape.Driveway:
			{
				// Wider at street (v≈0), narrows toward house (v≈1).
				var halfW = 0.46f - v * 0.17f;
				return MathF.Abs( u - 0.5f ) <= halfW && v >= 0.02f && v <= 0.98f;
			}
			case PanelShape.CarBay:
				return InCapsule( u, v, 0.48f, 0.38f );
			case PanelShape.OilSpill:
				return InSplatter( u, v, 0.38f, 0.42f );
			case PanelShape.Platform:
				return InRoundedRect( u, v, 0.04f ) && v >= 0.18f && v <= 0.82f;
			case PanelShape.Deck:
				return InRoundedRect( u, v, 0.06f );
			case PanelShape.Banner:
				return InRoundedRect( u, v, 0.05f ) && v >= 0.22f && v <= 0.78f;
			case PanelShape.Strip:
				return InRoundedRect( u, v, 0.05f ) && MathF.Abs( u - 0.5f ) <= 0.38f;
			case PanelShape.Window:
				return InRoundedRect( u, v, 0.08f ) && MathF.Abs( u - 0.5f ) <= 0.34f && v >= 0.2f && v <= 0.8f;
			case PanelShape.LCorner:
				return InLCorner( u, v );
			case PanelShape.Cross:
				return InCross( u, v, 0.14f );
			case PanelShape.Ring:
				return InRing( u, v, 0.22f, 0.46f );
			default:
				return true;
		}
	}

	public static float GrimeShade( GrimePattern pattern, int x, int y, int texW, int texH )
	{
		var u = (x + 0.5f) / texW;
		var v = (y + 0.5f) / texH;
		var n = Hash( x, y );

		return pattern switch
		{
			GrimePattern.Streaks => 0.62f + 0.38f * (0.5f + 0.5f * MathF.Sin( v * 48f + u * 6f + n * 0.4f )),
			GrimePattern.Splatter => 0.55f + 0.45f * SplatterNoise( u, v ),
			GrimePattern.Speckled => 0.68f + 0.32f * (0.35f + 0.65f * n ),
			GrimePattern.Rust => 0.58f + 0.42f * (0.4f + 0.6f * MathF.Sin( u * 22f ) * MathF.Cos( v * 18f )),
			_ => 0.72f + 0.28f * n,
		};
	}

	static bool InRoundedRect( float u, float v, float radius )
	{
		var rx = 0.5f - radius;
		var ry = 0.5f - radius;
		var dx = MathF.Max( MathF.Abs( u - 0.5f ) - rx, 0f );
		var dy = MathF.Max( MathF.Abs( v - 0.5f ) - ry, 0f );
		return dx * dx + dy * dy <= radius * radius;
	}

	static bool InEllipse( float u, float v, float rx, float ry )
	{
		var dx = (u - 0.5f) / rx;
		var dy = (v - 0.5f) / ry;
		return dx * dx + dy * dy <= 1f;
	}

	static bool InCapsule( float u, float v, float halfW, float halfH )
	{
		if ( InEllipse( u, v, halfW, halfH ) )
			return true;

		// Rounded ends along the long axis when the panel is wider than tall.
		var longHorizontal = halfW > halfH;
		if ( longHorizontal )
		{
			var cy = 0.5f;
			var left = 0.5f - halfW + halfH;
			var right = 0.5f + halfW - halfH;
			if ( u >= left && u <= right )
				return MathF.Abs( v - cy ) <= halfH;
			var cx = u < left ? left : right;
			var dx = u - cx;
			var dy = v - cy;
			return dx * dx + dy * dy <= halfH * halfH;
		}

		var cx2 = 0.5f;
		var bottom = 0.5f - halfH + halfW;
		var top = 0.5f + halfH - halfW;
		if ( v >= bottom && v <= top )
			return MathF.Abs( u - cx2 ) <= halfW;
		var cy2 = v < bottom ? bottom : top;
		var dx2 = u - cx2;
		var dy2 = v - cy2;
		return dx2 * dx2 + dy2 * dy2 <= halfW * halfW;
	}

	static bool InSplatter( float u, float v, float baseR, float variance )
	{
		var dx = u - 0.5f;
		var dy = v - 0.5f;
		var angle = MathF.Atan2( dy, dx );
		var r = baseR + variance * (0.35f * MathF.Sin( angle * 3f ) + 0.25f * MathF.Cos( angle * 5f )
			+ 0.15f * MathF.Sin( angle * 7f + 1.2f ));
		return dx * dx + dy * dy <= r * r;
	}

	static bool InLCorner( float u, float v )
	{
		var leg = 0.42f;
		var thick = 0.22f;
		var inHorizontal = v <= thick && u <= leg;
		var inVertical = u <= thick && v <= leg;
		return inHorizontal || inVertical;
	}

	static bool InCross( float u, float v, float arm )
	{
		var cx = MathF.Abs( u - 0.5f ) <= arm;
		var cy = MathF.Abs( v - 0.5f ) <= arm;
		return cx || cy;
	}

	static bool InRing( float u, float v, float inner, float outer )
	{
		var dx = u - 0.5f;
		var dy = v - 0.5f;
		var d2 = dx * dx + dy * dy;
		return d2 >= inner * inner && d2 <= outer * outer;
	}

	static float SplatterNoise( float u, float v )
	{
		var dx = u - 0.5f;
		var dy = v - 0.5f;
		var d = MathF.Sqrt( dx * dx + dy * dy );
		return Math.Clamp( 1.1f - d * 2.2f + 0.15f * MathF.Sin( u * 31f ) * MathF.Cos( v * 27f ), 0f, 1f );
	}

	static float Hash( int x, int y )
	{
		var n = x * 374761393 + y * 668265263;
		n = (n ^ (n >> 13)) * 1274126177;
		return (n & 0xFFFF) / 65535f;
	}
}
