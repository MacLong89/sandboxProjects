namespace FinalOutpost;

/// <summary>Summary of resources/scrap earned by your crew while the player was away.</summary>
public struct OfflineSummary
{
	public double Seconds;
	public double Scrap;
	public double Wood;
	public double Stone;
	public double Water;
	public bool Any => Scrap > 0 || Wood > 0 || Stone > 0 || Water > 0;
}

/// <summary>
/// Grants a capped amount of idle production for the time the player was away.
/// Survival: foragers earn scrap. Cure: foragers gather materials; leftover craftsmen convert stock.
/// </summary>
public static class IdleProgress
{
	public static OfflineSummary Apply( GameCore core )
	{
		var summary = new OfflineSummary();
		var save = core?.Save;
		if ( save is null || save.LastPlayedUnix <= 0 )
			return summary;

		var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		var elapsed = Math.Clamp( now - save.LastPlayedUnix, 0, (long)GameConstants.OfflineCapSeconds );
		if ( elapsed < GameConstants.OfflineMinSeconds )
			return summary;

		summary.Seconds = elapsed;

		var owned = new HashSet<string>( save.OwnedPlots );
		var craftsmen = 0;

		foreach ( var w in save.Workers )
		{
			var role = WorkerInfo.Parse( w.Role );
			if ( role == WorkerRole.Craftsman )
			{
				craftsmen++;
				continue;
			}

			if ( role != WorkerRole.Forager || !w.HasPlot ) continue;
			if ( !owned.Contains( PlotGrid.Key( w.PlotX, w.PlotY ) ) ) continue;

			if ( !core.IsCure )
			{
				summary.Scrap += GameConstants.ForagerScrapPerSec * elapsed;
				continue;
			}

			var kind = PlotGrid.ResourceAt( w.PlotX, w.PlotY );
			var amount = GameConstants.ForagerHarvestPerSec * elapsed;
			switch ( kind )
			{
				case ResourceKind.Wood: summary.Wood += amount; break;
				case ResourceKind.Stone: summary.Stone += amount; break;
				case ResourceKind.Water: summary.Water += amount; break;
			}
		}

		if ( core.IsCure )
		{
			summary.Wood = Math.Floor( summary.Wood );
			summary.Stone = Math.Floor( summary.Stone );
			summary.Water = Math.Floor( summary.Water );

			core.Resources.Add( ResourceKind.Wood, summary.Wood );
			core.Resources.Add( ResourceKind.Stone, summary.Stone );
			core.Resources.Add( ResourceKind.Water, summary.Water );
		}
		else
		{
			summary.Scrap = Math.Floor( summary.Scrap );
		}

		// Craftsmen convert stockpile (including what foragers just added) into scrap.
		if ( craftsmen > 0 )
		{
			double capacity = craftsmen * (double)GameConstants.CraftsmanConvertPerSec * elapsed;
			var converted = 0.0;
			var guard = 0;
			while ( capacity > 0.5 && guard++ < 64 )
			{
				var (kind, taken) = core.Resources.DrainRichest( capacity );
				if ( kind == ResourceKind.None || taken <= 0 ) break;
				converted += taken;
				capacity -= taken;
			}

			var crafted = Math.Floor( converted * GameConstants.CraftsmanScrapPerResource );
			summary.Scrap += crafted;
		}

		if ( summary.Scrap > 0 )
			core.Wallet.Earn( summary.Scrap );

		return summary;
	}

	/// <summary>Survival migration — fold leftover wood/stone/water into scrap and clear the stockpile.</summary>
	public static double ConvertMaterialsToScrap( GameCore core )
	{
		if ( core?.Resources is null || core.IsCure )
			return 0;

		var materials = 0.0;
		foreach ( var kind in new[] { ResourceKind.Wood, ResourceKind.Stone, ResourceKind.Water } )
		{
			var qty = core.Resources.Get( kind );
			if ( qty <= 0 ) continue;
			materials += qty;
			core.Resources.TrySpend( kind, qty );
		}

		var scrap = Math.Floor( materials * GameConstants.CraftsmanScrapPerResource );
		if ( scrap > 0 )
			core.Wallet.Earn( scrap );
		return scrap;
	}
}
