#nullable disable

using Terraingen.Clutter;
using Terraingen.Foliage;
using Terraingen.TerrainGen;

namespace Sandbox;

/// <summary>
/// Applies terraingen sandbox defaults onto Thorns runtime configs so procedural worlds match the
/// terraingen project's sculpt, materials, and foliage density (see <c>terraingen/Code/Terrain</c>).
/// Does not override scene-authored values when <paramref name="onlyIfUnmodified"/> is true.
/// </summary>
public static class ThornsTerraingenParity
{
	/// <summary>Matches <c>ThornsTerrainExplorer.SprintSpeedMultiplier</c> / network game manager.</summary>
	public const float PlayerSprintSpeedMultiplier = 1.75f;

	/// <summary>0 = leave engine / prefab <see cref="PlayerController.JumpSpeed"/> (terraingen explorer does not override jump).</summary>
	public const float PlayerJumpSpeed = 0f;

	public const float PlayerWalkSpeed = 320f;

	/// <summary>Stock citizen capsule radius (terraingen player template).</summary>
	public const float PlayerBodyRadius = 16f;

	public const float PlayerRunSpeed = PlayerWalkSpeed * PlayerSprintSpeedMultiplier;

	/// <summary>
	/// Sculpt + material line defaults from the terraingen project (not Thorns performance-thinned variants).
	/// </summary>
	public static void ApplyTerrainConfig( ThornsTerrainConfig config, bool onlyIfUnmodified = false )
	{
		if ( config is null )
			return;

		Set( onlyIfUnmodified, () => config.VerticalExaggeration, 3.4f, v => config.VerticalExaggeration = v );
		Set( onlyIfUnmodified, () => config.PeakExaggerationMultiplier, 1.72f, v => config.PeakExaggerationMultiplier = v );
		Set( onlyIfUnmodified, () => config.MountainExaggerationStrength, 2.5f, v => config.MountainExaggerationStrength = v );
		Set( onlyIfUnmodified, () => config.RidgeSharpeningStrength, 1.45f, v => config.RidgeSharpeningStrength = v );
		Set( onlyIfUnmodified, () => config.CliffExposureStrength, 1.5f, v => config.CliffExposureStrength = v );
		Set( onlyIfUnmodified, () => config.MicroNoiseReductionStrength, 0.8f, v => config.MicroNoiseReductionStrength = v );
		Set( onlyIfUnmodified, () => config.GrassUpperRangeFraction, 0.72f, v => config.GrassUpperRangeFraction = v );
		Set( onlyIfUnmodified, () => config.GrassMaxSlope, 0.12f, v => config.GrassMaxSlope = v );
		Set( onlyIfUnmodified, () => config.RockLowerRangeFraction, 0.5f, v => config.RockLowerRangeFraction = v );
		Set( onlyIfUnmodified, () => config.SnowUpperRangeFraction, 0.35f, v => config.SnowUpperRangeFraction = v );

		if ( string.IsNullOrWhiteSpace( config.WaterSurfaceMaterial )
		     || config.WaterSurfaceMaterial == "materials/water.vmat" )
			config.WaterSurfaceMaterial = "terrain_materials/thorns_terrain_water.vmat";
	}

	public static void ApplyFoliageDensity( ThornsFoliageConfig config )
	{
		if ( config is null )
			return;

		// Terraingen explorer defaults — Thorns gameplay presets cap further for MP perf.
		config.GlobalDensity = Math.Max( config.GlobalDensity, 1f );
		config.MaxTreeClustersPerChunk = Math.Max( config.MaxTreeClustersPerChunk, 12 );
	}

	static void Set( bool onlyIfUnmodified, Func<float> read, float terraingenValue, Action<float> write )
	{
		var current = read();
		if ( onlyIfUnmodified && MathF.Abs( current - terraingenValue ) > 0.001f )
			return;

		write( terraingenValue );
	}
}
