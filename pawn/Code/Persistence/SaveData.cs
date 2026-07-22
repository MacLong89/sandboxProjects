namespace PawnShop;

/// <summary>A pawn loan contract.</summary>
public sealed class PawnContract
{
	public int ItemId { get; set; }
	public string CustomerId { get; set; }
	public string CustomerName { get; set; }
	public int Principal { get; set; }
	/// <summary>Fee fraction (e.g. 0.2 = 20%).</summary>
	public float Fee { get; set; }
	public int StartDay { get; set; }
	public int DueDay { get; set; }
	public bool Extended { get; set; }
	/// <summary>Hidden likelihood 0..1 that the customer returns to redeem.</summary>
	public float RepayLikelihood { get; set; }

	public int RedemptionAmount => Principal + (int)Math.Ceiling( Principal * Fee );
}

/// <summary>Persistent relationship with a recurring customer.</summary>
public sealed class RelationshipData
{
	public string CustomerId { get; set; }
	public string Name { get; set; }
	public float Trust { get; set; } = 0.5f;
	public int Deals { get; set; }
	public int TotalValue { get; set; }
	public int Lowballs { get; set; }
	public int FairDeals { get; set; }
	public int PawnsRedeemed { get; set; }
	public int PawnsDefaulted { get; set; }

	public string Tier => Deals switch
	{
		>= 12 => "VIP",
		>= 8 => "Loyal",
		>= 5 => "Regular",
		>= 2 => "Familiar",
		_ => "Unknown",
	};
}

/// <summary>One day's ledger for the daily summary.</summary>
public sealed class DayLedger
{
	public int Day { get; set; }
	public int Purchases { get; set; }
	public int PurchaseSpend { get; set; }
	public int Sales { get; set; }
	public int SaleRevenue { get; set; }
	public int PawnsIssued { get; set; }
	public int PawnSpend { get; set; }
	public int Redemptions { get; set; }
	public int RedemptionRevenue { get; set; }
	public int Expenses { get; set; }
	public int RepairCosts { get; set; }
	public float RepChange { get; set; }
	public string BestDeal { get; set; } = "";
	public int BestDealProfit { get; set; }
	public string WorstDeal { get; set; } = "";
	public int WorstDealProfit { get; set; }
	public int TheftLosses { get; set; }
	public int Fines { get; set; }
	public int GoalBonuses { get; set; }

	public int Profit => SaleRevenue + RedemptionRevenue + GoalBonuses - PurchaseSpend - PawnSpend - Expenses - RepairCosts - TheftLosses - Fines;
}

/// <summary>Whole-game persistent state, serialized to JSON.</summary>
public sealed class SaveData
{
	public const int CurrentVersion = 1;
	public int Version { get; set; } = CurrentVersion;

	public int Day { get; set; } = 1;
	public int Cash { get; set; } = GameConstants.StartingCash;
	public float Reputation { get; set; } = GameConstants.RepStart;
	public int Debt { get; set; }
	public int DaysInTrouble { get; set; }

	public int NextItemId { get; set; } = 1;

	public List<ItemInstance> Inventory { get; set; } = new();
	public List<PawnContract> PawnContracts { get; set; } = new();
	public List<string> Upgrades { get; set; } = new();
	public List<string> Tools { get; set; } = new() { nameof( InspectTool.Eyes ) };
	public Dictionary<string, RelationshipData> Relationships { get; set; } = new();

	public string TodaysEvent { get; set; }

	// Daily goals
	public int GoalDay { get; set; }
	public List<string> TodayGoals { get; set; } = new();
	public List<string> CompletedGoals { get; set; } = new();
	public Dictionary<string, int> GoalProgress { get; set; } = new();

	// Mystery crate
	public int CrateDay { get; set; }
	public int CrateCost { get; set; }
	public int CrateItemCount { get; set; }
	public bool CrateBought { get; set; }

	// Collector's ledger
	public List<string> FlippedDefs { get; set; } = new();
	public List<string> RewardedCategories { get; set; } = new();

	// Stats
	public int LifetimeProfit { get; set; }
	public int TotalDeals { get; set; }
	public int FakesCaught { get; set; }
	public int FakesBought { get; set; }
	public int ItemsSold { get; set; }
	public int BestFlip { get; set; }

	public int TutorialStep { get; set; }
	public bool TutorialDone { get; set; }

	public bool HasSeenIntro { get; set; }

	public bool OwnsUpgrade( UpgradeId id ) => Upgrades.Contains( id.ToString() );
	public bool OwnsTool( InspectTool id ) => Tools.Contains( id.ToString() );
}
