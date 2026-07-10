namespace Sandbox;

/// <summary>How the main menu should override <see cref="ThornsTerrainSystem"/> for the next hosted session.</summary>
public enum ThornsHostWorldGenMode
{
	/// <summary>Keep scene defaults (and any seed read from disk before load for "continue world").</summary>
	None = 0,

	/// <summary>Roll a new integer seed when the host spawns terrain (blank field in host modal).</summary>
	RandomFresh,

	/// <summary>Use the given integer (<see cref="ThornsHostWorldGenerationIntent.FixedSeed"/>).</summary>
	FixedSeed
}

/// <summary>Menu → <see cref="ThornsTerrainSystem"/> one-shot world generation override.</summary>
public readonly struct ThornsHostWorldGenerationIntent
{
	public ThornsHostWorldGenerationIntent( ThornsHostWorldGenMode mode, int fixedSeed = 0 )
	{
		Mode = mode;
		FixedSeed = fixedSeed;
	}

	public ThornsHostWorldGenMode Mode { get; }

	public int FixedSeed { get; }

	public static ThornsHostWorldGenerationIntent RandomFresh => new( ThornsHostWorldGenMode.RandomFresh );

	public static ThornsHostWorldGenerationIntent Fixed( int seed ) => new( ThornsHostWorldGenMode.FixedSeed, seed );
}

/// <summary>
/// Applies <see cref="ThornsHostLocalSaveLobbyOptions.WorldGenIntent"/> on the listening host before the terrain chunk builds.
/// Cleared whenever the host-modal request is cancelled (join / story mode).
/// </summary>
public static class ThornsHostWorldGenHandoff
{
	static bool _armed;
	static ThornsHostWorldGenMode _mode;
	static int _fixedSeed;

	public static void ArmForNextGameplayTerrain( ThornsHostWorldGenerationIntent intent )
	{
		if ( intent.Mode == ThornsHostWorldGenMode.None )
		{
			_armed = false;
			return;
		}

		_armed = true;
		_mode = intent.Mode;
		_fixedSeed = intent.FixedSeed;
	}

	public static void Clear()
	{
		_armed = false;
		_mode = ThornsHostWorldGenMode.None;
		_fixedSeed = 0;
	}

	public static void ApplyOneShotIfArmed( ThornsTerrainSystem terrain )
	{
		if ( !_armed || terrain is null || !terrain.IsValid() )
			return;

		_armed = false;

		switch ( _mode )
		{
			case ThornsHostWorldGenMode.RandomFresh:
				// Randomizes building/resource layout only; terraingen heightmap crop stays on TerrainSeed / config WorldSeed.
				terrain.RandomizeSeedOnHost = true;
				break;
			case ThornsHostWorldGenMode.FixedSeed:
				terrain.TerrainSeed = _fixedSeed;
				terrain.RandomizeSeedOnHost = false;
				break;
			default:
				Clear();
				break;
		}
	}
}
