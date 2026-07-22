namespace PawnShop;

/// <summary>Central tuning values for the whole game.</summary>
public static class GameConstants
{
	public const string SaveFile = "pawnshop/save.json";
	public const float AutosaveInterval = 45f;

	// --- Day / clock ---
	/// <summary>Real seconds of open-hours per in-game day. Configurable via convar pawn_daylength.</summary>
	public static float DayLengthSeconds = 600f;
	public const float OpenClockStart = 9 * 60f;    // 9:00
	public const float OpenClockEnd = 18 * 60f;     // 18:00
	public static float ClockMinutesPerSecond => (OpenClockEnd - OpenClockStart) / Math.Max( 60f, DayLengthSeconds );

	// --- Economy ---
	public const int StartingCash = 1200;
	public const int BaseRent = 60;
	public const int BaseUtilities = 25;
	public const int InsuranceCost = 15;
	public const int EmergencyLoanAmount = 800;
	public const float EmergencyLoanDailyInterest = 0.08f;
	public const int BankruptcyDebtLimit = 2500;
	public const int BankruptcyGraceDays = 3;

	// --- Pawn rules ---
	public const int PawnTermDays = 3;
	public const float PawnFeeMin = 0.10f;
	public const float PawnFeeMax = 0.40f;
	public const float PawnFeeDefault = 0.20f;
	public const int PawnExtensionDays = 2;

	// --- Customers ---
	public const int MaxQueue = 3;
	public const int MaxBrowsers = 3;
	public const float BaseSpawnInterval = 32f;
	public const float QueuePatienceSeconds = 75f;

	// --- Player ---
	public const float EyeHeight = 64f;
	public const float WalkSpeed = 190f;
	public const float RunMultiplier = 1.5f;
	public const float FieldOfView = 78f;
	public const float InteractRange = 180f;

	// --- Reputation ---
	public const float RepMin = 0f;
	public const float RepMax = 100f;
	public const float RepStart = 40f;

	public static string FormatCash( int v ) => v < 0 ? $"-${Math.Abs( v ):N0}" : $"${v:N0}";
	public static string FormatCash( double v ) => FormatCash( (int)Math.Round( v ) );

	public static string FormatClock( float minutes )
	{
		var h = (int)(minutes / 60f);
		var m = (int)(minutes % 60f);
		var suffix = h >= 12 ? "PM" : "AM";
		var h12 = h % 12; if ( h12 == 0 ) h12 = 12;
		return $"{h12}:{m:00} {suffix}";
	}
}
