namespace RunGun;

/// <summary>
/// Themed city districts that cycle as the riot pushes deeper into the metro.
/// Visual identity + copy only — spawn logic stays in TrackManager / SectionPacing.
/// </summary>
public static class DistrictTheme
{
	public static string Name( int biomeIndex ) => biomeIndex switch
	{
		0 => "DOWNTOWN",
		1 => "HARBOR",
		2 => "NEON STRIP",
		_ => "INDUSTRIAL",
	};

	public static string Tagline( int biomeIndex ) => biomeIndex switch
	{
		0 => "Corporate towers. Break the line.",
		1 => "Dockyards swarming with security.",
		2 => "Neon alleys. Loud and lethal.",
		_ => "Smokestacks and steel walls.",
	};

	public static (Color ground, Color wall, Color accent) Colors( int biomeIndex ) => biomeIndex switch
	{
		0 => (
			new Color( 0.22f, 0.24f, 0.28f ),
			new Color( 0.14f, 0.16f, 0.2f ),
			new Color( 1f, 0.35f, 0.2f ) ),
		1 => (
			new Color( 0.16f, 0.22f, 0.28f ),
			new Color( 0.1f, 0.18f, 0.24f ),
			new Color( 0.3f, 0.85f, 1f ) ),
		2 => (
			new Color( 0.2f, 0.12f, 0.28f ),
			new Color( 0.14f, 0.08f, 0.22f ),
			new Color( 1f, 0.25f, 0.75f ) ),
		_ => (
			new Color( 0.26f, 0.2f, 0.16f ),
			new Color( 0.18f, 0.14f, 0.1f ),
			new Color( 1f, 0.7f, 0.2f ) ),
	};

	public static Color CrowdTint => new( 1f, 0.55f, 0.25f );
	public static Color SecurityTint => new( 0.35f, 0.55f, 0.95f );
}
