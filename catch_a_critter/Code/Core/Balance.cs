namespace CatchACritter;

/// <summary>
/// Every tuning number in one place. The curves follow simulator-genre best
/// practice: early upgrades land within seconds, later ones gate on zones and
/// prestige so the sink never runs dry.
/// </summary>
public static class Balance
{
	// ---- Session / world ----
	public const int MaxLobbyPlayers = 24;
	public const float WorldTile = 50f;

	// ---- Movement ----
	public const float BaseWalkSpeed = 210f;
	public const float SneakSpeed = 95f;
	public const float SpeedPerLevel = 18f;
	public const int SpeedMaxLevel = 25;

	// ---- Catching ----
	public const float BaseCatchRadius = 85f;
	public const float BaseSwingCooldown = 0.85f;
	public const float SwingAnimSeconds = 0.28f;

	// ---- Backpack ----
	public const int BaseBackpack = 8;
	public const int BackpackPerLevel = 4;
	public const int BackpackMaxLevel = 30;

	// ---- Luck ----
	public const int LuckMaxLevel = 20;
	public const float ShinyBaseChance = 1f / 40f;
	public const float ShinyLuckBonusPerLevel = 0.0035f;

	// ---- Critter economy ----
	/// <summary>Coin multiplier per rarity tier (Common..Mythic).</summary>
	public static readonly double[] RarityValue = { 1, 3.2, 11, 42, 190, 900 };
	public const double ShinyValueMult = 12.0;
	public const float GemChanceRare = 0.18f;
	public const float GemChanceEpicPlus = 0.5f;

	// ---- Sanctuary (passive income) ----
	public const int BaseSanctuarySlots = 4;
	public const double SanctuaryIncomeShare = 0.045; // critter value per second /1000 * this
	public const double OfflineEarningsRate = 0.5;    // sanctuary income efficiency while away
	public const double OfflineMaxHours = 10.0;

	// ---- Breeding ----
	public const float EggMinutesBase = 8f;
	public const float EggMinutesPerRarity = 7f;
	public const float BredShinyBonus = 2.5f;         // multiplier on shiny odds
	public const float BredStatGainMin = 0.06f;
	public const float BredStatGainMax = 0.16f;
	public const int GemInstantHatchCost = 12;

	// ---- Prestige ----
	public const double AscendBaseCost = 750_000;
	public const double AscendCostGrowth = 3.1;
	public const double AscendSellBonus = 0.35;        // +35% sell per crown
	public const int TalentPointsPerAscend = 3;

	// ---- Dailies / streak ----
	public const int DailyQuestCount = 3;
	public const int DailyGemReward = 6;
	public static readonly int[] StreakGems = { 2, 3, 4, 6, 8, 10, 15 };

	// ---- Upgrade cost curves ----
	public static double SpeedCost( int level ) => 45 * Math.Pow( 1.62, level );
	public static double BackpackCost( int level ) => 60 * Math.Pow( 1.55, level );
	public static double LuckCost( int level ) => 250 * Math.Pow( 2.05, level );

	/// <summary>Coins → short display string (1.2k, 3.4M, ...).</summary>
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
