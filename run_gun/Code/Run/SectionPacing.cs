namespace RunGun;

public enum RunSection
{
	Buildup,
	Swarm,
	Breather,
}

/// <summary>Cycles encounter intensity so runs have rhythm instead of flat scaling.</summary>
public sealed class SectionPacing
{
	public RunSection Current { get; private set; } = RunSection.Buildup;
	public int CycleIndex { get; private set; }
	public float SectionStartMeters { get; private set; }

	public float EnemyCountMult => Current switch
	{
		RunSection.Swarm => 1.45f,
		RunSection.Breather => 0.55f,
		_ => 1f,
	};

	public float GateBonusMult => Current switch
	{
		RunSection.Breather => 1.35f,
		RunSection.Swarm => 0.85f,
		_ => 1f,
	};

	public int BiomeIndex => CycleIndex % GameConstants.BiomeCount;

	public void Update( float meters )
	{
		var cyclePos = meters % GameConstants.SectionCycleMeters;
		var section = cyclePos switch
		{
			< 80f => RunSection.Buildup,
			< 160f => RunSection.Swarm,
			_ => RunSection.Breather,
		};

		if ( section != Current || (int)(meters / GameConstants.SectionCycleMeters) != CycleIndex )
		{
			Current = section;
			CycleIndex = (int)(meters / GameConstants.SectionCycleMeters);
			SectionStartMeters = meters;
		}
	}

	public static (Color ground, Color wall) BiomeColors( int index )
	{
		var (ground, wall, _) = DistrictTheme.Colors( index );
		return (ground, wall);
	}
}
