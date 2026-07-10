#nullable disable

namespace Sandbox;

/// <summary>
/// Three independent FBM channels derived from <see cref="ThornsTerrainNetSpec.Seed"/> (unique permutation + scale each):
/// turf texture selection, prop affinity (trees + mushrooms), and grass-fluff affinity.
/// </summary>
public static class ThornsWorldNoise
{
	/// <summary>XOR into <see cref="ThornsPerlinNoise2D"/> seed — distinct from heightfield noise.</summary>
	public const int TerrainTurfPermSalt = unchecked((int)0x6e3742e1);

	public const int FoliagePropsPermSalt = unchecked((int)0xbd91a704);

	public const int FoliageFluffPermSalt = unchecked((int)0x44f058cc);

	/// <summary>Multiplies <see cref="ThornsTerrainNetSpec.NoiseScale"/> — must sweep many cells across the map or turf sticks in one quartile.</summary>
	public const float TurfNoiseFrequencyMul = 10.5f;

	/// <summary>Shared tree/mushroom channel — needs real spatial variance vs height FBM.</summary>
	public const float FoliagePropsFrequencyMul = 3.8f;

	/// <summary>Grass clutter anchors — finer than props.</summary>
	public const float FoliageFluffFrequencyMul = 6.25f;

	public static ThornsPerlinNoise2D CreateTerrainTurfNoise( int worldSeed ) =>
		new ThornsPerlinNoise2D( worldSeed ^ TerrainTurfPermSalt );

	public static ThornsPerlinNoise2D CreateFoliagePropsNoise( int worldSeed ) =>
		new ThornsPerlinNoise2D( worldSeed ^ FoliagePropsPermSalt );

	public static ThornsPerlinNoise2D CreateFoliageFluffNoise( int worldSeed ) =>
		new ThornsPerlinNoise2D( worldSeed ^ FoliageFluffPermSalt );

	public static float SampleTerrainTurfTexture01( ThornsPerlinNoise2D noise, float localPlaneX, float localPlaneY, in ThornsTerrainNetSpec spec )
	{
		var oct = Math.Clamp( spec.TerrainNoiseOctaves, 3, 8 );
		var nxBase = localPlaneX * spec.NoiseScale;
		var nyBase = localPlaneY * spec.NoiseScale;
		var macro = ThornsTerrainGeometry.SampleFractalNoise01(
			noise,
			nxBase * TurfNoiseFrequencyMul,
			nyBase * TurfNoiseFrequencyMul,
			oct,
			spec.TerrainNoisePersistence,
			spec.TerrainNoiseLacunarity );
		var mesoOct = Math.Clamp( oct - 2, 2, 5 );
		var mesoMul = TurfNoiseFrequencyMul * 3.85f;
		var meso = ThornsTerrainGeometry.SampleFractalNoise01(
			noise,
			nxBase * mesoMul,
			nyBase * mesoMul,
			mesoOct,
			spec.TerrainNoisePersistence * 0.92f,
			spec.TerrainNoiseLacunarity * 1.08f );
		return Math.Clamp( macro * 0.62f + meso * 0.38f, 0f, 1f );
	}

	public static float SampleFoliageProps01( ThornsPerlinNoise2D noise, float localPlaneX, float localPlaneY, in ThornsTerrainNetSpec spec ) =>
		ThornsTerrainGeometry.SampleFractalNoise01(
			noise,
			localPlaneX * spec.NoiseScale * FoliagePropsFrequencyMul,
			localPlaneY * spec.NoiseScale * FoliagePropsFrequencyMul,
			Math.Clamp( spec.TerrainNoiseOctaves - 1, 2, 7 ),
			Math.Clamp( spec.TerrainNoisePersistence + 0.04f, 0.02f, 0.98f ),
			spec.TerrainNoiseLacunarity );

	public static float SampleFoliageFluff01( ThornsPerlinNoise2D noise, float localPlaneX, float localPlaneY, in ThornsTerrainNetSpec spec ) =>
		ThornsTerrainGeometry.SampleFractalNoise01(
			noise,
			localPlaneX * spec.NoiseScale * FoliageFluffFrequencyMul,
			localPlaneY * spec.NoiseScale * FoliageFluffFrequencyMul,
			spec.TerrainNoiseOctaves,
			Math.Clamp( spec.TerrainNoisePersistence - 0.05f, 0.02f, 0.98f ),
			spec.TerrainNoiseLacunarity );
}
