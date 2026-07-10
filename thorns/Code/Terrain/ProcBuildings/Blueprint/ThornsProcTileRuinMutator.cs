namespace Sandbox;

/// <summary>Randomly damages a compiled footprint while preserving at least one entrance.</summary>
public static class ThornsProcTileRuinMutator
{
	public static void Apply( bool[] occ, bool[] opening, int w, int d, int stories, Random rnd )
	{
		var stride = w * d;
		for ( var s = 0; s < stories; s++ )
		for ( var pass = 0; pass < 3; pass++ )
		{
			for ( var x = 0; x < w; x++ )
			for ( var y = 0; y < d; y++ )
			{
				var i = s * stride + y * w + x;
				if ( opening[i] )
					continue;

				if ( !occ[i] )
					continue;

				if ( rnd.NextDouble() > 0.14 )
					continue;

				occ[i] = false;
			}
		}

		// Re-open shaft cells above any surviving ramp anchors.
		for ( var s = 0; s < stories - 1; s++ )
		for ( var x = 0; x < w; x++ )
		for ( var y = 0; y < d; y++ )
		{
			var i = s * stride + y * w + x;
			if ( !occ[i] )
				continue;

			// Heuristic: keep openings above cells that still have floor below a gap above.
			if ( s + 1 < stories && !occ[s * stride + stride + y * w + x] )
				opening[( s + 1 ) * stride + y * w + x] = true;
		}
	}
}
