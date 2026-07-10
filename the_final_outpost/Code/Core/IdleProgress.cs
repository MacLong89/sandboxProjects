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
/// Grants a capped amount of idle production for the time the player was away: foragers keep
/// gathering their plots and craftsmen keep converting stock into scrap. A gentle offline loop like
/// this is a strong retention driver ("come back to a pile of rewards").
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

		// Foragers gather their assigned, owned resource plots.
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

			var kind = PlotGrid.ResourceAt( w.PlotX, w.PlotY );
			var amount = GameConstants.ForagerHarvestPerSec * elapsed;
			switch ( kind )
			{
				case ResourceKind.Wood: summary.Wood += amount; break;
				case ResourceKind.Stone: summary.Stone += amount; break;
				case ResourceKind.Water: summary.Water += amount; break;
			}
		}

		summary.Wood = Math.Floor( summary.Wood );
		summary.Stone = Math.Floor( summary.Stone );
		summary.Water = Math.Floor( summary.Water );

		core.Resources.Add( ResourceKind.Wood, summary.Wood );
		core.Resources.Add( ResourceKind.Stone, summary.Stone );
		core.Resources.Add( ResourceKind.Water, summary.Water );

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

			summary.Scrap = Math.Floor( converted * GameConstants.CraftsmanScrapPerResource );
			core.Wallet.Earn( summary.Scrap );
		}

		return summary;
	}
}
