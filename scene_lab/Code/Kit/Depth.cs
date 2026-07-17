namespace SceneLab;

/// <summary>
/// Mild offsets so coplanar meshes do not z-fight.
/// Prefer ±1 unit bumps — never invent large floating bands.
/// </summary>
public static class Depth
{
	public const float Step = 1f;
	public const float Sit = 0.05f;

	/// <summary>Yard grass top sits this far above embankment top (mild anti z-fight only).</summary>
	public const float YardAboveBank = Step;

	/// <summary>Driveway apron above road deck (still clears embankment when lifted with bank).</summary>
	public const float DrivewayAboveRoad = 2f;

	public static float CenterLift( float height ) => height * 0.5f + Sit;
}
