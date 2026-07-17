namespace Offshore;

/// <summary>Permanent player progression. Expanded by save/load.</summary>
public sealed class PlayerProgressionData
{
	public float Money { get; set; }
	public float LifetimeMoneyEarned { get; set; }
	public float LifetimeMoneySpent { get; set; }
	public float HighestSale { get; set; }
	public float HighestFishValue { get; set; }
	public int PlayerLevel { get; set; } = 1;
	public float Experience { get; set; }
	public string CurrentLocationId { get; set; } = "old_dock";
	public string EquippedBoatId { get; set; } = "";
	public string SelectedBaitId { get; set; } = "worm";
	public HashSet<string> OwnedBaitIds { get; set; } = new( StringComparer.OrdinalIgnoreCase ) { "worm" };
	public int TotalCasts { get; set; }
	public int SuccessfulCatches { get; set; }
	public int EscapedFish { get; set; }
	public int CoolerCapacity { get; set; } = 6;
	public int ContractsCompleted { get; set; }
	public int TournamentsPlayed { get; set; }
	public int TournamentsWon { get; set; }
	public int HubLevel { get; set; }
	public int CatchesSinceTimeAdvance { get; set; }
	public TimeOfDay TimeOfDay { get; set; } = TimeOfDay.Day;
	public WeatherType Weather { get; set; } = WeatherType.Clear;
	public bool TournamentActive { get; set; }
	public string TournamentName { get; set; } = "";
	public float TournamentScore { get; set; }
	public float TournamentBestWeight { get; set; }

	public List<CatchRecord> Cooler { get; set; } = new();
	public HashSet<string> DiscoveredFishIds { get; set; } = new( StringComparer.OrdinalIgnoreCase );
	public HashSet<string> UnlockedLocationIds { get; set; } = new( StringComparer.OrdinalIgnoreCase ) { "old_dock" };
	public HashSet<string> OwnedBoatIds { get; set; } = new( StringComparer.OrdinalIgnoreCase );
	public HashSet<string> CompletedCollectionIds { get; set; } = new( StringComparer.OrdinalIgnoreCase );
	public HashSet<string> CompletedContractIds { get; set; } = new( StringComparer.OrdinalIgnoreCase );
	public HashSet<string> ActiveContractIds { get; set; } = new( StringComparer.OrdinalIgnoreCase );
	public Dictionary<string, int> UpgradeLevels { get; set; } = new( StringComparer.OrdinalIgnoreCase );
	public Dictionary<string, int> ContractProgress { get; set; } = new( StringComparer.OrdinalIgnoreCase );
	public Dictionary<string, JournalEntry> Journal { get; set; } = new( StringComparer.OrdinalIgnoreCase );

	public float CoolerUsed
	{
		get
		{
			var used = 0f;
			foreach ( var catchRecord in Cooler )
			{
				var def = FishCatalog.Get( catchRecord.FishId );
				if ( def is not null )
					used += MathF.Max( 0.1f, def.CapacityCost );
				else
					used += MathF.Max( 0.1f, catchRecord.Weight > 0f ? catchRecord.Weight : 1f );
			}
			return used;
		}
	}

	public float CoolerEstimatedValue
	{
		get
		{
			var total = 0f;
			foreach ( var catchRecord in Cooler )
				total += catchRecord.FinalValue;
			return total;
		}
	}

	public bool IsCoolerFull => CoolerUsed >= CoolerCapacity - 0.001f;
}
