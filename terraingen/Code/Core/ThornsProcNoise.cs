namespace Terraingen.Core;

/// <summary>Shared procedural noise helpers for scatter systems.</summary>
public static class ThornsProcNoise
{
	public static float ValueNoise( float x, float y )
	{
		var xi = (int)MathF.Floor( x * 12.9898f );
		var yi = (int)MathF.Floor( y * 78.233f );
		var n = Math.Sin( xi * 127.1 + yi * 311.7 ) * 43758.5453;
		return (float)(n - Math.Floor( n ));
	}
}
