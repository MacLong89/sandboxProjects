namespace OffshoreFishing.Core;

/// <summary>Headless pacing simulator for the five-hour economy.</summary>
public static class BalanceSimulator
{
	public sealed class Report
	{
		public double SimulatedMinutes { get; set; }
		public int FinalGold { get; set; }
		public int TotalCatches { get; set; }
		public int ZonesUnlocked { get; set; }
		public int FishDiscovered { get; set; }
		public bool ReachedTrench { get; set; }
		public bool BoughtOceanic { get; set; }
		public bool HiredSecondCrew { get; set; }
		public List<string> Milestones { get; set; } = new();
		public bool FirstUpgradeByMinute3 { get; set; }
		public bool FirstOffshoreByMinute10 { get; set; }
	}

	public static Report Run( double targetMinutes = 300, long seed = 42 )
	{
		var content = ContentCatalog.Create();
		var session = new GameSession( content, GameSession.CreateNewState( seed ) );
		var state = session.State;
		var report = new Report();
		var minute = 0.0;
		var catchCooldown = 0f;

		// Scripted optimistic player: dock cast, sell, buy, sail, fish, upgrade.
		const double step = 0.1;
		while ( minute < targetMinutes )
		{
			session.Advance( step );
			minute = state.PlayedSeconds / 60.0;
			catchCooldown -= (float)step;

			if ( state.Mode == GameMode.CatchReveal )
				session.CloseCatchReveal();

			// Keep fishing whenever a cast/fight is active (dock tutorial or at sea).
			var fishingActive = state.Mode is GameMode.Fishing or GameMode.CatchReveal
				|| state.Fishing.Phase is not FishingPhase.Idle;

			if ( !state.OnBoat && !state.TutorialFirstCatchDone )
				AutoCatch( session, ref catchCooldown );
			else if ( fishingActive && state.Mode != GameMode.Shop )
				AutoCatch( session, ref catchCooldown );

			if ( state.Hold.Count > 0 && !state.OnBoat && state.Mode != GameMode.Fishing
				&& (state.Hold.Count >= 2 || state.Gold < 50 || state.Hold.Sum( f => f.Worth ) >= 20) )
			{
				session.OpenShop();
				session.SellAll();
			}

			if ( state.Gold >= 40 && !state.OwnedItemIds.Contains( "spool_braided" ) )
			{
				session.OpenShop();
				session.BuyItem( "spool_braided" );
				if ( minute <= 3 ) report.FirstUpgradeByMinute3 = true;
				report.Milestones.Add( $"spool@{minute:F1}m" );
			}

			if ( state.CountItem( "bait_worms" ) < 5 && state.Gold >= 15 )
			{
				session.OpenShop();
				session.BuyItem( "bait_worms" );
			}

			TryBuyLadder( session, state, report, minute );

			if ( state.TutorialFirstSaleDone && !state.OnBoat && state.Mode != GameMode.Fishing
				&& state.Fishing.Phase == FishingPhase.Idle && state.CountItem( "bait_worms" ) > 0 )
			{
				session.CloseShop();
				session.BoardBoat();
			}

			if ( state.OnBoat && state.Mode != GameMode.CatchReveal )
			{
				var boat = content.GetBoat( state.OwnedBoatId );
				var target = Math.Min( boat.MaxRangeM * 0.85f, GetTargetDistance( state ) );
				var canSteer = state.Fishing.Phase is FishingPhase.Idle or FishingPhase.Failed or FishingPhase.Waiting;

				if ( state.Hold.Count >= boat.StorageSlots - 1 )
				{
					session.Travel( -1f, (float)step );
					if ( state.BoatDistanceM < 10 )
					{
						session.Disembark();
						session.OpenShop();
						session.SellAll();
					}
				}
				else if ( canSteer && state.BoatDistanceM < target - 10 )
				{
					session.Travel( 1f, (float)step );
				}
				else if ( canSteer && state.BoatDistanceM > target + 40 )
				{
					session.Travel( -1f, (float)step );
				}
				else
				{
					AutoCatch( session, ref catchCooldown );
				}
			}

			if ( state.UnlockedZoneIds.Contains( "kelp" ) && minute <= 10 )
				report.FirstOffshoreByMinute10 = true;
		}

		report.SimulatedMinutes = minute;
		report.FinalGold = state.Gold;
		report.TotalCatches = state.TotalCatches;
		report.ZonesUnlocked = state.UnlockedZoneIds.Count;
		report.FishDiscovered = state.FishLog.Count;
		report.ReachedTrench = state.UnlockedZoneIds.Contains( "trench" );
		report.BoughtOceanic = state.OwnedBoatId == "boat_oceanic";
		report.HiredSecondCrew = state.OwnedHiredBoatIds.Contains( "crew_second" );
		return report;
	}

	private static float GetTargetDistance( GameState state )
	{
		if ( state.OwnedBoatId == "boat_oceanic" ) return 3500;
		if ( state.OwnedBoatId == "boat_explorer" ) return 1600;
		if ( state.OwnedBoatId == "boat_fisher" ) return 600;
		// Push skiff to kelp edge early for unlock pacing.
		return 200;
	}

	private static void TryBuyLadder( GameSession session, GameState state, Report report, double minute )
	{
		string[] items =
		{
			"hook_better", "bait_minnows", "rod_fiberglass", "spool_deep", "hook_barbed",
			"rod_carbon", "bait_squid", "spool_spectra", "rod_titanium", "hook_circle",
			"bait_premium", "rod_abyss", "spool_void", "hook_abyss"
		};
		foreach ( var id in items )
		{
			if ( state.OwnedItemIds.Contains( id ) ) continue;
			if ( session.BuyItem( id ) )
				report.Milestones.Add( $"{id}@{minute:F1}m" );
		}

		string[] boats = { "boat_fisher", "boat_explorer", "boat_oceanic" };
		foreach ( var id in boats )
		{
			if ( state.OwnedBoatId == id || state.OwnedItemIds.Contains( id ) ) continue;
			if ( session.BuyBoat( id ) )
				report.Milestones.Add( $"{id}@{minute:F1}m" );
		}

		foreach ( var id in new[] { "crew_second", "crew_third" } )
		{
			if ( state.OwnedHiredBoatIds.Contains( id ) ) continue;
			if ( session.HireBoat( id ) )
				report.Milestones.Add( $"{id}@{minute:F1}m" );
		}
	}

	private static void AutoCatch( GameSession session, ref float cooldown )
	{
		var f = session.State.Fishing;
		if ( cooldown > 0 ) return;

		if ( f.Phase == FishingPhase.Idle || f.Phase == FishingPhase.Failed )
		{
			session.BeginCastAim();
			session.SetAim( -0.7f );
			for ( var i = 0; i < 20; i++ ) session.ChargeCast( 0.05f );
			session.ReleaseCast();
		}
		else if ( f.Phase == FishingPhase.BiteWindow )
		{
			session.TryHook();
		}
		else if ( f.Phase == FishingPhase.Fighting )
		{
			// Keep tension near safe zone center.
			var target = f.SafeZoneCenter;
			var band = f.SafeZoneWidth * 0.35f;
			if ( f.LineTension < target - band ) session.SetReelHeld( true );
			else if ( f.LineTension > target + band ) session.SetReelHeld( false );
			else session.SetReelHeld( f.LineTension <= target );
		}
		else if ( f.Phase == FishingPhase.Landing || session.State.Mode == GameMode.CatchReveal )
		{
			session.CloseCatchReveal();
			cooldown = 2.5f; // human-like pause between casts
		}
	}
}
