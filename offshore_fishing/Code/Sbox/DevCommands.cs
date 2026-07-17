using OffshoreFishing.Core;

namespace Sandbox;

/// <summary>Console helpers for balance verification inside s&box.</summary>
public static class DevCommands
{
	[ConCmd( "offshore_balance" )]
	public static void RunBalance( float minutes = 300 )
	{
		var report = BalanceSimulator.Run( minutes, seed: 42 );
		Log.Info( $"[Offshore Balance] minutes={report.SimulatedMinutes:F1} gold={report.FinalGold} catches={report.TotalCatches}" );
		Log.Info( $"[Offshore Balance] zones={report.ZonesUnlocked} fish={report.FishDiscovered} trench={report.ReachedTrench}" );
		Log.Info( $"[Offshore Balance] oceanic={report.BoughtOceanic} crew2={report.HiredSecondCrew}" );
		Log.Info( $"[Offshore Balance] upgradeBy3={report.FirstUpgradeByMinute3} offshoreBy10={report.FirstOffshoreByMinute10}" );
		foreach ( var m in report.Milestones.Take( 40 ) )
			Log.Info( $"  milestone: {m}" );
	}

	[ConCmd( "offshore_newgame" )]
	public static void NewGame()
	{
		var ctrl = FishingGameController.Instance;
		if ( ctrl == null )
		{
			Log.Warning( "No FishingGameController" );
			return;
		}
		ctrl.UiNewGame();
		Log.Info( "Started new game" );
	}

	[ConCmd( "offshore_givegold" )]
	public static void GiveGold( int amount = 1000 )
	{
		var ctrl = FishingGameController.Instance;
		if ( ctrl?.Session == null ) return;
		ctrl.Session.State.Gold += amount;
		Log.Info( $"Gold now {ctrl.Session.State.Gold}" );
	}
}
