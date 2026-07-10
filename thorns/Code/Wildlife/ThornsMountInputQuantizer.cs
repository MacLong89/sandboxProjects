namespace Sandbox;

/// <summary>Shared client/host planar steer quantization + clamping for mount RPCs (server authority).</summary>
public static class ThornsMountInputQuantizer
{
	/// <summary>Host: reject NaNs / overshoot; keep wish on the unit disk in XY.</summary>
	public static Vector3 ClampHostPlanarSteer( Vector3 worldPlanar )
	{
		var p = worldPlanar.WithZ( 0f );
		if ( p.LengthSquared < 1e-8f )
			return Vector3.Zero;
		if ( p.LengthSquared > 1f + 1e-5f )
			p = p.Normal;
		return p;
	}

	/// <summary>Client + host: snap planar XY to a finite grid on <c>[-1,1]</c> then re-project to the unit disk.</summary>
	public static Vector3 QuantizePlanarSteer( Vector3 worldPlanar, int steps )
	{
		var p = worldPlanar.WithZ( 0f );
		if ( p.LengthSquared < 1e-8f )
			return Vector3.Zero;
		if ( p.LengthSquared > 1f + 1e-5f )
			p = p.Normal;

		var s = Math.Max( 1, steps );
		float Q( float v ) => Math.Clamp( MathF.Round( v * s ) / s, -1f, 1f );
		var q = new Vector3( Q( p.x ), Q( p.y ), 0f );
		if ( q.LengthSquared > 1f + 1e-5f )
			q = q.Normal;
		return q;
	}
}
