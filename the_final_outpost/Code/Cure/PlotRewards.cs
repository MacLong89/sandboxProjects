namespace FinalOutpost;



/// <summary>Bonus payouts when Cure plots are fully cleared.</summary>

public static class PlotRewards

{

	public static void OnPlotCleared( GameCore core, int x, int y )

	{

		if ( core?.IsCure != true ) return;



		var kind = PlotFeatureGrid.KindAt( x, y );

		var key = PlotGrid.Key( x, y );



		switch ( kind )

		{

			case PlotKind.Standard:

			{

				var resource = PlotGrid.ResourceAt( x, y );

				if ( resource is ResourceKind.Wood or ResourceKind.Stone )

				{

					core.Resources.Add( resource, CureConstants.PlotClearMaterialBonus );

					core.ShowToast( $"{ResourceInfo.Name( resource )} secured — +{CureConstants.PlotClearMaterialBonus:0}" );

				}

				break;

			}



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

		}



		var boss = PlotWorldRolls.BossAt( x, y );

		if ( boss != BossKind.None )

		{

			core.Save.ClearedBossPlots ??= new List<string>();

			if ( !core.Save.ClearedBossPlots.Contains( key ) )

			{

				core.Save.ClearedBossPlots.Add( key );

				var def = PlotWorldRolls.GetBoss( boss );

				core.TriggerThreat( def.ThreatMult, def.Name );

			}

		}



		PlotBoosts.Claim( core, x, y );

		core.SaveManagerTouch();

	}

}

