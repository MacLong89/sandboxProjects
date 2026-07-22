namespace NoFly;

public sealed class RoundSettings
{
	public float RoundDurationSeconds { get; set; } = 480f;
	public float PreparationSeconds { get; set; } = 30f;
	public float RoleRevealSeconds { get; set; } = 6f;
	public float LobbyCountdownSeconds { get; set; } = 10f;
	public float BoardingStartsAtSeconds { get; set; } = 120f;
	public float FinalCallSeconds { get; set; } = 45f;
	public float ResultsSeconds { get; set; } = 18f;
	public float BoardingHoldSeconds { get; set; } = 2.5f;
	public float FalseSearchDelaySeconds { get; set; } = 5f;
	public float ReportCooldownSeconds { get; set; } = 20f;
	public int MinMultiplayerPlayers { get; set; } = 4;
	public int IdealPlayersMin { get; set; } = 6;
	public int IdealPlayersMax { get; set; } = 10;
	public int TargetNpcCount { get; set; } = 14;
	public int MaxNpcCount { get; set; } = 20;
	public bool AllowSinglePlayer { get; set; } = true;
	public DiscrepancyDifficulty DefaultForgeryDifficulty { get; set; } = DiscrepancyDifficulty.Easy;
	public bool DebugToolsEnabled { get; set; } = true;
}

public static class RoleColors
{
	public static Color Passenger => new( 0.25f, 0.65f, 0.95f );
	public static Color Tsa => new( 0.95f, 0.72f, 0.15f );
	public static Color Smuggler => new( 0.92f, 0.28f, 0.42f );
	public static Color Undercover => new( 0.55f, 0.35f, 0.85f );
	public static Color Document => new( 0.35f, 0.75f, 0.55f );
	public static Color Scanner => new( 0.3f, 0.85f, 0.75f );
	public static Color Security => new( 0.95f, 0.45f, 0.2f );

	public static Color ForRole( RoleType role ) => role switch
	{
		RoleType.Smuggler => Smuggler,
		RoleType.UndercoverAgent => Undercover,
		RoleType.DocumentAgent => Document,
		RoleType.ScannerAgent => Scanner,
		RoleType.SecurityOfficer => Security,
		RoleType.RegularPassenger => Passenger,
		_ => Color.White
	};
}

/// <summary>
/// Cohesive modern-airport kit language: cool neutrals, navy accents, warm wood desks.
/// Zone floors differ only slightly so the path is readable without looking like a toy set.
/// </summary>
public static class AirportPalette
{
	public static Color Wall => new( 0.94f, 0.95f, 0.96f );
	public static Color WallTrim => new( 0.78f, 0.82f, 0.88f );
	public static Color Exterior => new( 0.72f, 0.76f, 0.82f );
	public static Color Ceiling => new( 0.88f, 0.90f, 0.93f );
	public static Color Column => new( 0.86f, 0.88f, 0.91f );
	public static Color Baseboard => new( 0.55f, 0.58f, 0.64f );

	public static Color FloorEntrance => new( 0.90f, 0.89f, 0.86f );
	public static Color FloorDocs => new( 0.86f, 0.89f, 0.93f );
	public static Color FloorScanner => new( 0.86f, 0.91f, 0.89f );
	public static Color FloorSecurity => new( 0.90f, 0.87f, 0.84f );
	public static Color FloorTerminal => new( 0.89f, 0.89f, 0.91f );
	public static Color FloorGate => new( 0.87f, 0.86f, 0.90f );
	public static Color FloorPath => new( 0.55f, 0.68f, 0.78f );
	public static Color Carpet => new( 0.42f, 0.48f, 0.58f );

	public static Color Desk => new( 0.42f, 0.36f, 0.30f );
	public static Color DeskTop => new( 0.72f, 0.68f, 0.62f );
	public static Color Metal => new( 0.45f, 0.48f, 0.52f );
	public static Color MetalDark => new( 0.28f, 0.30f, 0.34f );
	public static Color Screen => new( 0.12f, 0.18f, 0.28f );
	public static Color Seat => new( 0.32f, 0.42f, 0.55f );
	public static Color SeatCushion => new( 0.38f, 0.48f, 0.62f );

	public static Color Navy => new( 0.18f, 0.32f, 0.52f );
	public static Color NavyLite => new( 0.35f, 0.52f, 0.72f );
	public static Color Amber => new( 0.85f, 0.62f, 0.28f );
	public static Color Success => new( 0.35f, 0.62f, 0.48f );
	public static Color Warn => new( 0.78f, 0.42f, 0.28f );
	public static Color Restricted => new( 0.62f, 0.22f, 0.22f );
	public static Color Glass => new( 0.55f, 0.72f, 0.88f, 0.45f );
	public static Color GlassDeep => new( 0.35f, 0.50f, 0.65f, 0.55f );

	public static Color Grass => new( 0.32f, 0.42f, 0.30f );
	public static Color Tarmac => new( 0.24f, 0.25f, 0.27f );
	public static Color TarmacLine => new( 0.88f, 0.86f, 0.55f );
	public static Color PlaneBody => new( 0.92f, 0.93f, 0.95f );
	public static Color PlaneAccent => new( 0.22f, 0.38f, 0.58f );

	public static Color AccentBlue => Navy;
	public static Color AccentOrange => Amber;
	public static Color AccentGreen => Success;
	public static Color AccentPink => new( 0.72f, 0.42f, 0.55f );
}
