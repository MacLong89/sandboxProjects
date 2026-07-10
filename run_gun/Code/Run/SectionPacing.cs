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

	public static (Color ground, Color wall) BiomeColors( int index ) => index switch
	{
		0 => (new Color( 0.32f, 0.34f, 0.38f ), new Color( 0.22f, 0.24f, 0.29f )),
		1 => (new Color( 0.28f, 0.36f, 0.3f ), new Color( 0.16f, 0.28f, 0.2f )),
		2 => (new Color( 0.34f, 0.3f, 0.42f ), new Color( 0.22f, 0.18f, 0.34f )),
		_ => (new Color( 0.38f, 0.3f, 0.28f ), new Color( 0.28f, 0.2f, 0.18f )),
	};
}
