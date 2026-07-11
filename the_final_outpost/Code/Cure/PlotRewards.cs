namespace FinalOutpost;

/// <summary>Bonus payouts when special Cure plots are fully cleared.</summary>
public static class PlotRewards
{
	public static void OnPlotCleared( GameCore core, int x, int y )
	{
		if ( core?.IsCure != true ) return;

		var kind = PlotFeatureGrid.KindAt( x, y );
		if ( kind == PlotKind.Standard ) return;

		var key = PlotGrid.Key( x, y );

		switch ( kind )
		{
			case PlotKind.FoodCache:
				core.Resources.Add( ResourceKind.Food, 80 );
				core.ShowToast( "Food cache secured — +80 Food" );
				break;

			case PlotKind.SupplyDepot:
				core.Resources.Add( ResourceKind.Supplies, 60 );
				core.Wallet.Earn( 40 );
				core.ShowToast( "Supply depot looted — +60 Supplies, +40 Scrap" );
				break;

			case PlotKind.TechRuins:
				core.Resources.Add( ResourceKind.Knowledge, 50 );
				core.Resources.Add( ResourceKind.Specimens, 20 );
				core.ShowToast( "Tech ruins excavated — +50 Knowledge, +20 Specimens" );
				break;

			case PlotKind.BossLair:
				core.Save.ClearedBossPlots ??= new List<string>();
				if ( !core.Save.ClearedBossPlots.Contains( key ) )
				{
					core.Save.ClearedBossPlots.Add( key );
					core.ShowToast( "Threat nest disturbed — boss wave incoming!" );
					core.TriggerThreat();
				}
				break;
		}

		core.SaveManagerTouch();
	}
}
