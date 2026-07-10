#nullable disable

namespace Sandbox;

/// <summary>2D gradient (Perlin-style) noise for terrain height sampling — deterministic from seed, no heap allocs in <see cref="Sample"/>.</summary>
public sealed class ThornsPerlinNoise2D
{
	const int PermutationSize = 256;

	readonly int[] _perm;

	public ThornsPerlinNoise2D( int seed )
	{
		_perm = new int[PermutationSize * 2];
		var rng = new Random( seed );
		var order = new int[PermutationSize];
		for ( var i = 0; i < PermutationSize; i++ )
			order[i] = i;

		for ( var i = PermutationSize - 1; i > 0; i-- )
		{
			var j = rng.Next( i + 1 );
			(order[i], order[j]) = (order[j], order[i]);
		}

		for ( var i = 0; i < PermutationSize; i++ )
		{
			_perm[i] = order[i];
			_perm[i + PermutationSize] = order[i];
		}
	}

	static float Fade( float t ) => t * t * t * (t * (t * 6f - 15f) + 10f);

	static float Lerp( float t, float a, float b ) => a + t * (b - a);

	static float Grad( int hash, float x, float y )
	{
		var h = hash & 7;
		var u = h < 4 ? x : y;
		var v = h < 4 ? y : x;
		return ((h & 1) != 0 ? -u : u) + ((h & 2) != 0 ? -2f * v : 2f * v);
	}

	/// <summary>Sample in continuous noise space; typically pass world xz scaled by frequency.</summary>
	public float Sample( float x, float y )
	{
		var xf = MathF.Floor( x );
		var yf = MathF.Floor( y );
		var x0 = (int)xf & 255;
		var y0 = (int)yf & 255;
		var xp = (x0 + 1) & 255;
		var yp = (y0 + 1) & 255;
		var X = x - xf;
		var Y = y - yf;
		var u = Fade( X );
		var v = Fade( Y );

		var h00 = _perm[_perm[x0] + y0];
		var h10 = _perm[_perm[xp] + y0];
		var h01 = _perm[_perm[x0] + yp];
		var h11 = _perm[_perm[xp] + yp];

		var a = Lerp( u, Grad( h00, X, Y ), Grad( h10, X - 1f, Y ) );
		var b = Lerp( u, Grad( h01, X, Y - 1f ), Grad( h11, X - 1f, Y - 1f ) );
		return Lerp( v, a, b );
	}
}
