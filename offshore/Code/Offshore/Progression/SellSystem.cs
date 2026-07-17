namespace Offshore;

public sealed class SaleSummary
{
	public int FishSold { get; set; }
	public float TotalEarned { get; set; }
	public string BestFishName { get; set; } = "";
	public float BestFishValue { get; set; }
	public int NewSpeciesCount { get; set; }
	public int NewRecords { get; set; }
	public float MoneyAfter { get; set; }
	public string NextHint { get; set; } = "";
}

public static class SellSystem
{
	private static bool _saleLatched;

	public static SaleSummary LastSummary { get; private set; }

	public static bool TrySellAll( OffshoreGameController game )
	{
		if ( _saleLatched || game is null )
			return false;

		var cooler = game.Progression.Cooler;
		if ( cooler.Count == 0 )
		{
			game.SetStatus( "Cooler is empty" );
			return false;
		}

		_saleLatched = true;
		var summary = new SaleSummary { FishSold = cooler.Count };
		float total = 0f;
		CatchRecord best = null;
		var newSpecies = 0;
		var newRecords = 0;

		foreach ( var c in cooler )
		{
			total += c.FinalValue;
			if ( best is null || c.FinalValue > best.FinalValue )
				best = c;
			if ( c.IsNewDiscovery )
				newSpecies++;
			if ( c.IsPersonalRecord )
				newRecords++;
		}

		game.Progression.Money += total;
		game.Progression.LifetimeMoneyEarned += total;
		if ( total > game.Progression.HighestSale )
			game.Progression.HighestSale = total;
		if ( best is not null && best.FinalValue > game.Progression.HighestFishValue )
			game.Progression.HighestFishValue = best.FinalValue;

		cooler.Clear();
		AwardXp( game.Progression, 8f + summary.FishSold * 3f + newSpecies * 10f );
		ContractSystem.NotifySale( game.Progression, total );

		summary.TotalEarned = total;
		summary.BestFishName = best?.DisplayName ?? "";
		summary.BestFishValue = best?.FinalValue ?? 0f;
		summary.NewSpeciesCount = newSpecies;
		summary.NewRecords = newRecords;
		summary.MoneyAfter = game.Progression.Money;
		summary.NextHint = NextUnlockHint( game.Progression );
		LastSummary = summary;

		BoatSystem.CheckAutoUnlocks( game );
		LocationManager.CheckAutoUnlocks( game );
		OffshoreSaveSystem.Save( game.Progression );

		_saleLatched = false;
		game.SetStatus( $"Sold {summary.FishSold} fish for ${total:N0}" );
		return true;
	}

	public static void ClearLatch() => _saleLatched = false;

	private static void AwardXp( PlayerProgressionData p, float xp )
	{
		p.Experience += xp;
		while ( p.Experience >= XpToNext( p.PlayerLevel ) )
		{
			p.Experience -= XpToNext( p.PlayerLevel );
			p.PlayerLevel++;
		}
	}

	private static float XpToNext( int level ) => 40f + level * 25f;

	private static string NextUnlockHint( PlayerProgressionData p )
	{
		if ( !p.UnlockedLocationIds.Contains( "quiet_bay" ) )
			return $"Quiet Bay unlocks at ${LocationCatalog.Get( "quiet_bay" )?.UnlockCost ?? 150:0}";
		if ( !p.OwnedBoatIds.Contains( "rowboat" ) )
			return "Buy the Rowboat to travel farther";
		if ( !p.UnlockedLocationIds.Contains( "coastal" ) )
			return $"Coastal Waters at ${LocationCatalog.Get( "coastal" )?.UnlockCost ?? 400:0}";
		return "Keep upgrading and exploring";
	}
}
