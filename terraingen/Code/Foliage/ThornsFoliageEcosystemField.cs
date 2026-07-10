namespace Terraingen.Foliage;

using Terraingen.TerrainGen;

/// <summary>
/// Low-frequency ecosystem masks: connected forest masses, scenic openings, treeline.
/// </summary>
public sealed class ThornsFoliageEcosystemField
{
	public int Width { get; }
	public int Height { get; }

	readonly float[] _forestMass;
	readonly float[] _opening;

	public ThornsFoliageEcosystemField( int width, int height, float[] forestMass, float[] opening )
	{
		Width = width;
		Height = height;
		_forestMass = forestMass;
		_opening = opening;
	}

	public static ThornsFoliageEcosystemField Build(
		HeightmapField field,
		ThornsTerrainConfig terrainConfig,
		ThornsFoliageConfig config,
		int seed )
	{
		var w = field.Width;
		var h = field.Height;
		var raw = new float[w * h];

		for ( int y = 0; y < h; y++ )
		{
			for ( int x = 0; x < w; x++ )
			{
				var height = field.Get( x, y );
				var slope = SampleSlopeAt( field, x, y );
				raw[field.Index( x, y )] = ComputeRawForestPotential( height, slope, terrainConfig, config );
			}
		}

		var blurred = BoxBlur( raw, w, h, config.ForestMassBlurRadius, config.ForestMassBlurPasses );
		var opening = new float[w * h];

		for ( int y = 0; y < h; y++ )
		{
			for ( int x = 0; x < w; x++ )
			{
				var idx = field.Index( x, y );
				var height = field.Get( x, y );
				var treeline = ComputeTreeline( height, config );
				var meadow = ComputeOpeningNoise( x, y, w, h, seed, config );
				var valley = (1f - Smooth( height, terrainConfig.SeaLevelNormalized + 0.02f, 0.38f )).Clamp( 0f, 1f );

				opening[idx] = meadow * (0.45f + valley * 0.55f);
				blurred[idx] *= treeline;
				blurred[idx] *= 1f - opening[idx] * config.OpeningForestReduction;
			}
		}

		return new ThornsFoliageEcosystemField( w, h, blurred, opening );
	}

	public float SampleForestMass( float u, float v ) => SampleBilinear( _forestMass, Width, Height, u, v );

	public float SampleOpening( float u, float v ) => SampleBilinear( _opening, Width, Height, u, v );

	static float ComputeRawForestPotential( float height, float slope, ThornsTerrainConfig terrain, ThornsFoliageConfig config )
	{
		if ( height <= terrain.SeaLevelNormalized + 0.01f )
			return 0f;

		var alpine = Smooth( height, 0.55f, 0.75f );
		var lowland = 1f - alpine;
		var moisture = (1f - Smooth( height, 0.08f, 0.42f )).Clamp( 0f, 1f );
		var valley = (1f - slope * 3.5f).Clamp( 0f, 1f );
		var flat = 1f - Smooth( slope, 0.02f, config.MaxSlopeForTrees * 1.2f );

		return valley * moisture * lowland * flat * (1f - alpine * 0.85f);
	}

	static float ComputeTreeline( float height, ThornsFoliageConfig config )
	{
		return 1f - Smooth( height, config.TreelineStartNormalized, config.TreelineEndNormalized );
	}

	static float ComputeOpeningNoise( int x, int y, int width, int height, int seed, ThornsFoliageConfig config )
	{
		var u = x / (float)Math.Max( 1, width - 1 );
		var v = y / (float)Math.Max( 1, height - 1 );
		var scale = config.OpeningNoiseScale;
		var n1 = MathF.Sin( (u * 127.1f + v * 311.7f + seed) * scale ) * 43758.5453f;
		var n2 = MathF.Sin( (u * 269.5f + v * 183.3f + seed * 1.7f) * scale * 0.5f ) * 43758.5453f;
		var n = (n1 - MathF.Floor( n1 )) * 0.6f + (n2 - MathF.Floor( n2 )) * 0.4f;
		return Smooth( n, config.OpeningNoiseLow, config.OpeningNoiseHigh );
	}

	static float SampleSlopeAt( HeightmapField field, int x, int y )
	{
		var dx = field.Get( x + 1, y ) - field.Get( x - 1, y );
		var dy = field.Get( x, y + 1 ) - field.Get( x, y - 1 );
		return MathF.Sqrt( dx * dx + dy * dy );
	}

	static float[] CopyFloatArray( float[] source )
	{
		var copy = new float[source.Length];
		Array.Copy( source, copy, source.Length );
		return copy;
	}

	static float[] BoxBlur( float[] source, int width, int height, int radius, int passes )
	{
		if ( radius <= 0 || passes <= 0 )
			return CopyFloatArray( source );

		var work = CopyFloatArray( source );
		var output = new float[source.Length];

		for ( int pass = 0; pass < passes; pass++ )
		{
			for ( int y = 0; y < height; y++ )
			{
				for ( int x = 0; x < width; x++ )
				{
					var sum = 0f;
					var count = 0;
					for ( int oy = -radius; oy <= radius; oy++ )
					{
						for ( int ox = -radius; ox <= radius; ox++ )
						{
							var sx = (x + ox).Clamp( 0, width - 1 );
							var sy = (y + oy).Clamp( 0, height - 1 );
							sum += work[sx + sy * width];
							count++;
						}
					}

					output[x + y * width] = sum / count;
				}
			}

			(work, output) = (output, work);
		}

		return work;
	}

	static float SampleBilinear( float[] data, int width, int height, float u, float v )
	{
		u = u.Clamp( 0f, 1f );
		v = v.Clamp( 0f, 1f );
		var fx = u * (width - 1);
		var fy = v * (height - 1);
		var x0 = (int)Math.Floor( fx );
		var y0 = (int)Math.Floor( fy );
		var x1 = Math.Min( x0 + 1, width - 1 );
		var y1 = Math.Min( y0 + 1, height - 1 );
		var tx = fx - x0;
		var ty = fy - y0;

		var i00 = data[x0 + y0 * width];
		var i10 = data[x1 + y0 * width];
		var i01 = data[x0 + y1 * width];
		var i11 = data[x1 + y1 * width];
		var sx0 = i00 + (i10 - i00) * tx;
		var sx1 = i01 + (i11 - i01) * tx;
		return sx0 + (sx1 - sx0) * ty;
	}

	static float Smooth( float value, float edge0, float edge1 )
	{
		if ( edge1 <= edge0 )
			return value >= edge0 ? 1f : 0f;
		var t = ((value - edge0) / (edge1 - edge0)).Clamp( 0f, 1f );
		return t * t * (3f - 2f * t);
	}
}
