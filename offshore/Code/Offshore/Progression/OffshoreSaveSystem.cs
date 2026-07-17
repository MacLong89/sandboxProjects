namespace Offshore;

public sealed class OffshoreSaveData
{
	public int Version { get; set; } = 1;
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
	public int TotalCasts { get; set; }
	public int SuccessfulCatches { get; set; }
	public int EscapedFish { get; set; }
	public int CoolerCapacity { get; set; } = 6;
	public int ContractsCompleted { get; set; }
	public int TournamentsPlayed { get; set; }
	public int TournamentsWon { get; set; }
	public int HubLevel { get; set; }
	public TimeOfDay TimeOfDay { get; set; } = TimeOfDay.Day;
	public WeatherType Weather { get; set; } = WeatherType.Clear;
	public List<string> DiscoveredFishIds { get; set; } = new();
	public List<string> UnlockedLocationIds { get; set; } = new();
	public List<string> OwnedBoatIds { get; set; } = new();
	public List<string> OwnedBaitIds { get; set; } = new();
	public List<string> CompletedCollectionIds { get; set; } = new();
	public List<string> CompletedContractIds { get; set; } = new();
	public List<string> ActiveContractIds { get; set; } = new();
	public Dictionary<string, int> UpgradeLevels { get; set; } = new();
	public Dictionary<string, int> ContractProgress { get; set; } = new();
	public Dictionary<string, JournalEntry> Journal { get; set; } = new();
	public List<CatchRecord> Cooler { get; set; } = new();
}

public static class OffshoreSaveSystem
{
	public const string Path = "offshore_save.json";
	public const int CurrentVersion = 1;

	public static void Save( PlayerProgressionData p )
	{
		if ( p is null )
			return;

		try
		{
			var data = ToData( p );
			FileSystem.Data.WriteJson( Path, data );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[Offshore] Save failed: {e.Message}" );
		}
	}

	public static bool TryLoad( PlayerProgressionData into )
	{
		if ( into is null )
			return false;

		try
		{
			if ( !FileSystem.Data.FileExists( Path ) )
				return false;

			var data = FileSystem.Data.ReadJson<OffshoreSaveData>( Path );
			if ( data is null )
				return false;

			Apply( into, data );
			Sanitize( into );
			return true;
		}
		catch ( Exception e )
		{
			Log.Warning( $"[Offshore] Load failed: {e.Message}" );
			return false;
		}
	}

	private static OffshoreSaveData ToData( PlayerProgressionData p ) => new()
	{
		Version = CurrentVersion,
		Money = p.Money,
		LifetimeMoneyEarned = p.LifetimeMoneyEarned,
		LifetimeMoneySpent = p.LifetimeMoneySpent,
		HighestSale = p.HighestSale,
		HighestFishValue = p.HighestFishValue,
		PlayerLevel = p.PlayerLevel,
		Experience = p.Experience,
		CurrentLocationId = p.CurrentLocationId,
		EquippedBoatId = p.EquippedBoatId,
		SelectedBaitId = p.SelectedBaitId,
		TotalCasts = p.TotalCasts,
		SuccessfulCatches = p.SuccessfulCatches,
		EscapedFish = p.EscapedFish,
		CoolerCapacity = p.CoolerCapacity,
		ContractsCompleted = p.ContractsCompleted,
		TournamentsPlayed = p.TournamentsPlayed,
		TournamentsWon = p.TournamentsWon,
		HubLevel = p.HubLevel,
		TimeOfDay = p.TimeOfDay,
		Weather = p.Weather,
		DiscoveredFishIds = p.DiscoveredFishIds.ToList(),
		UnlockedLocationIds = p.UnlockedLocationIds.ToList(),
		OwnedBoatIds = p.OwnedBoatIds?.ToList() ?? new List<string>(),
		OwnedBaitIds = p.OwnedBaitIds?.ToList() ?? new List<string> { "worm" },
		CompletedCollectionIds = p.CompletedCollectionIds?.ToList() ?? new List<string>(),
		CompletedContractIds = p.CompletedContractIds.ToList(),
		ActiveContractIds = p.ActiveContractIds.ToList(),
		UpgradeLevels = new Dictionary<string, int>( p.UpgradeLevels ),
		ContractProgress = new Dictionary<string, int>( p.ContractProgress ),
		Journal = new Dictionary<string, JournalEntry>( p.Journal ),
		Cooler = p.Cooler.ToList()
	};

	private static void Apply( PlayerProgressionData p, OffshoreSaveData d )
	{
		p.Money = MathF.Max( 0f, d.Money );
		p.LifetimeMoneyEarned = MathF.Max( 0f, d.LifetimeMoneyEarned );
		p.LifetimeMoneySpent = MathF.Max( 0f, d.LifetimeMoneySpent );
		p.HighestSale = MathF.Max( 0f, d.HighestSale );
		p.HighestFishValue = MathF.Max( 0f, d.HighestFishValue );
		p.PlayerLevel = Math.Max( 1, d.PlayerLevel );
		p.Experience = MathF.Max( 0f, d.Experience );
		p.CurrentLocationId = string.IsNullOrWhiteSpace( d.CurrentLocationId ) ? "old_dock" : d.CurrentLocationId;
		p.EquippedBoatId = d.EquippedBoatId ?? "";
		p.SelectedBaitId = string.IsNullOrWhiteSpace( d.SelectedBaitId ) ? "worm" : d.SelectedBaitId;
		p.TotalCasts = Math.Max( 0, d.TotalCasts );
		p.SuccessfulCatches = Math.Max( 0, d.SuccessfulCatches );
		p.EscapedFish = Math.Max( 0, d.EscapedFish );
		p.CoolerCapacity = Math.Max( 1, d.CoolerCapacity );
		p.ContractsCompleted = Math.Max( 0, d.ContractsCompleted );
		p.TournamentsPlayed = Math.Max( 0, d.TournamentsPlayed );
		p.TournamentsWon = Math.Max( 0, d.TournamentsWon );
		p.HubLevel = Math.Max( 0, d.HubLevel );
		p.TimeOfDay = d.TimeOfDay;
		p.Weather = d.Weather;
		p.DiscoveredFishIds = new HashSet<string>( d.DiscoveredFishIds ?? [], StringComparer.OrdinalIgnoreCase );
		p.UnlockedLocationIds = new HashSet<string>( d.UnlockedLocationIds ?? ["old_dock"], StringComparer.OrdinalIgnoreCase );
		p.OwnedBoatIds = new HashSet<string>( d.OwnedBoatIds ?? new List<string>(), StringComparer.OrdinalIgnoreCase );
		p.OwnedBaitIds = new HashSet<string>( d.OwnedBaitIds ?? new List<string> { "worm" }, StringComparer.OrdinalIgnoreCase );
		p.CompletedCollectionIds = new HashSet<string>( d.CompletedCollectionIds ?? new List<string>(), StringComparer.OrdinalIgnoreCase );
		p.CompletedContractIds = new HashSet<string>( d.CompletedContractIds ?? [], StringComparer.OrdinalIgnoreCase );
		p.ActiveContractIds = new HashSet<string>( d.ActiveContractIds ?? [], StringComparer.OrdinalIgnoreCase );
		p.UpgradeLevels = new Dictionary<string, int>( d.UpgradeLevels ?? new(), StringComparer.OrdinalIgnoreCase );
		p.ContractProgress = new Dictionary<string, int>( d.ContractProgress ?? new(), StringComparer.OrdinalIgnoreCase );
		p.Journal = new Dictionary<string, JournalEntry>( d.Journal ?? new(), StringComparer.OrdinalIgnoreCase );
		p.Cooler = d.Cooler ?? new();
	}

	private static void Sanitize( PlayerProgressionData p )
	{
		p.UnlockedLocationIds.Add( "old_dock" );
		if ( LocationCatalog.Get( p.CurrentLocationId ) is null )
			p.CurrentLocationId = "old_dock";

		// Drop invalid fish ids from cooler.
		p.Cooler.RemoveAll( c => c is null || FishCatalog.Get( c.FishId ) is null );

		foreach ( var key in p.UpgradeLevels.Keys.ToList() )
		{
			var def = UpgradeCatalog.Get( key );
			if ( def is null )
			{
				p.UpgradeLevels.Remove( key );
				continue;
			}
			p.UpgradeLevels[key] = Math.Clamp( p.UpgradeLevels[key], 0, def.MaxLevel );
		}

		ContractSystem.EnsureSlots( p );
		BaitSystem.EnsureDefaults( p );
	}
}
