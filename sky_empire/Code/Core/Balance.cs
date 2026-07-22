namespace SkyEmpire;

/// <summary>
/// Every tuning number in one place. Curves follow tycoon-genre best practice:
/// the first buttons land in seconds, floors gate mid-game pacing, and rebirth
/// keeps the sink alive forever.
/// </summary>
public static class Balance
{
	// ---- Session / world ----
	public const int MaxLobbyPlayers = 12;
	public const int PlotCount = 12;
	public const float PlotRingRadius = 2750f;
	public const float PlotRadius = 640f;
	public const float HubRadius = 520f;

	// ---- Movement ----
	public const float WalkSpeed = 260f;
	public const float JumpPower = 320f;
	public const float FallRespawnZ = -900f;

	// ---- Conveyor / orbs ----
	public const float BeltStartX = -420f;
	public const float BeltEndX = 430f;
	public const float FurnaceX = 470f;
	public const float BeltY = -110f;
	public const float BeltTopZ = 42f;
	public const float BeltBaseSpeed = 120f;
	public const float BeltSpeedPerMotor = 0.18f;   // +18% per motor, also small income mult
	public const int MaxOrbsPerPlot = 55;

	// ---- Golden orbs ----
	public const double GoldenBaseChance = 1.0 / 70.0;
	public const double GoldenValueMult = 20.0;
	public const float GoldenGemChance = 0.15f;

	// ---- Rebirth ----
	public const double RebirthBaseCost = 20_000_000;
	public const double RebirthCostGrowth = 5.0;
	public const double RebirthIncomeBonus = 0.5;    // +50% income per rebirth, forever

	// ---- Friend boost ----
	public const double FriendBoostMult = 1.25;

	// ---- Gems / boosts ----
	public const int OverdriveGemCost = 12;
	public const double OverdriveMult = 2.0;
	public const float OverdriveMinutes = 10f;
	public const int FrenzyGemCost = 8;
	public const float FrenzySeconds = 75f;
	public const float FrenzyRateMult = 3f;

	// ---- Retention ----
	public const float ChestIntervalMinutes = 8f;
	public const float ChestGemChance = 0.35f;
	public const double OfflineEarningsRate = 0.5;
	public const double OfflineMaxHours = 8.0;
	public const int DailyGemReward = 6;
	public static readonly int[] StreakGems = { 2, 3, 4, 6, 8, 10, 15 };

	/// <summary>Value → short display string (1.2k, 3.4M, ...).</summary>
	public static string Fmt( double v )
	{
		var neg = v < 0; v = Math.Abs( v );
		string s = v switch
		{
			>= 1e15 => $"{v / 1e15:0.##}Q",
			>= 1e12 => $"{v / 1e12:0.##}T",
			>= 1e9 => $"{v / 1e9:0.##}B",
			>= 1e6 => $"{v / 1e6:0.##}M",
			>= 1e4 => $"{v / 1e3:0.#}k",
			>= 10 => $"{Math.Floor( v ):0}",
			_ => $"{v:0.#}"
		};
		return neg ? "-" + s : s;
	}

	public static string FmtTime( double seconds )
	{
		if ( seconds <= 0 ) return "0s";
		var t = TimeSpan.FromSeconds( seconds );
		if ( t.TotalHours >= 1 ) return $"{(int)t.TotalHours}h {t.Minutes}m";
		if ( t.TotalMinutes >= 1 ) return $"{t.Minutes}m {t.Seconds}s";
		return $"{t.Seconds}s";
	}
}
