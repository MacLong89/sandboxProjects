namespace RunGun;

/// <summary>
/// Central difficulty ramp. Everything eases in over the first stretch of a run so new
/// players get a gentle start, then scales toward full intensity with distance. Tune the
/// curve here rather than sprinkling distance checks across the spawn code.
/// </summary>
public static class Difficulty
{
	/// <summary>0 at the very start of a run, 1 once fully ramped in.</summary>
	public static float Ramp( float meters ) =>
		Math.Clamp( meters / GameConstants.DifficultyRampMeters, 0f, 1f );

	/// <summary>Which formation-complexity tier is unlocked at this distance (0 = easiest).</summary>
	public static int FormationTier( float meters )
	{
		if ( meters < GameConstants.Tier1Meters ) return 0;
		if ( meters < GameConstants.Tier2Meters ) return 1;
		if ( meters < GameConstants.Tier3Meters ) return 2;
		return 3;
	}

	/// <summary>Contact damage scales up with distance so early mistakes are forgiving.</summary>
	public static float ContactDamage( float meters ) =>
		MathX.Lerp( GameConstants.EnemyContactDamageStart, GameConstants.EnemyContactDamage, Ramp( meters ) );

	/// <summary>
	/// Extra bodies piled onto swarms/hordes as the run deepens. Zero at the start (clean opening),
	/// climbing to a big wave far out so the crowd always has more to mow down.
	/// </summary>
	public static int PackSizeBonus( float meters ) =>
		(int)MathF.Floor( Ramp( meters ) * 3f + Math.Clamp( (meters - 500f) / 450f, 0f, 4f ) );

	/// <summary>How many runners a leaking enemy costs — one early, a small pile late.</summary>
	public static int ContactSquadCost( float meters ) =>
		Math.Max( 1, (int)MathF.Round( MathX.Lerp( GameConstants.SquadContactCostStart, GameConstants.SquadContactCostMax, Ramp( meters ) ) ) );

	/// <summary>
	/// Enemies advance slower early, giving time to shoot them down, then close faster than
	/// baseline at long range so leakers punish a stalled build.
	/// </summary>
	public static float AdvanceSpeedMult( float meters )
	{
		var basic = MathX.Lerp( GameConstants.EnemyAdvanceSpeedStartMult, 1f, Ramp( meters ) );
		var extra = Math.Clamp( (meters - 600f) / 1400f, 0f, 0.45f );
		return basic + extra;
	}

	/// <summary>Chance an enemy pack spawns at a given gate row — sparse early, dense later.</summary>
	public static float PackSpawnChance( float meters ) =>
		MathX.Lerp( GameConstants.PackSpawnChanceStart, 1f, Ramp( meters ) );

	/// <summary>Chance a hazard row spawns. Zero until the opening stretch is over.</summary>
	public static float HazardChance( float meters )
	{
		if ( meters < GameConstants.HazardStartMeters ) return 0f;
		var t = Math.Clamp(
			(meters - GameConstants.HazardStartMeters) / (GameConstants.HazardRampMeters - GameConstants.HazardStartMeters),
			0f, 1f );
		return MathX.Lerp( GameConstants.HazardChanceStart, GameConstants.HazardChanceMax, t );
	}

	/// <summary>Hazard hit scales up so late mistakes are lethal.</summary>
	public static float HazardDamage( float meters ) =>
		GameConstants.HazardDamage * MathX.Lerp( 0.7f, 1.6f, Ramp( meters ) );

	/// <summary>Safe gap in a hazard row — wide early, tightening toward the minimum.</summary>
	public static float HazardGap( float meters ) =>
		MathX.Lerp( GameConstants.HazardMaxGap, GameConstants.HazardMinGap, Ramp( meters ) );
}
