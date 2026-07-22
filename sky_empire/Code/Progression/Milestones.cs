namespace SkyEmpire;

public sealed record MilestoneDef( string Title, string Detail, double CashReward, int GemReward, Func<PlayerProgress, bool> IsDone, Func<PlayerProgress, string> ProgressLabel );

/// <summary>
/// The onboarding momentum chain. One goal at a time, each with a burst reward,
/// walking a brand-new player straight through every system in order.
/// </summary>
public static class Milestones
{
	public static readonly MilestoneDef[] Chain =
	{
		new( "Place your first dropper!",
			"Walk onto the glowing FREE pad on your island.",
			15, 0,
			p => p.Data.Purchased.Count >= 1,
			p => $"{Math.Min( p.Data.Purchased.Count, 1 )}/1" ),

		new( "Collect 20 orbs",
			"Orbs roll down your belt into the furnace — cash for every one.",
			40, 0,
			p => p.Data.OrbsCollected >= 20,
			p => $"{Math.Min( p.Data.OrbsCollected, 20 )}/20" ),

		new( "Buy 4 upgrades",
			"Step on buy pads when they glow green. Droppers make orbs, arches multiply them.",
			120, 1,
			p => p.Data.Purchased.Count >= 4,
			p => $"{Math.Min( p.Data.Purchased.Count, 4 )}/4" ),

		new( "Build an Upgrader Arch",
			"Arches multiply every orb that rolls under them. They stack!",
			200, 1,
			p => p.Data.Purchased.Any( id => PurchaseCatalog.Get( id )?.Kind == PurchaseKind.Upgrader ),
			p => p.Data.Purchased.Any( id => PurchaseCatalog.Get( id )?.Kind == PurchaseKind.Upgrader ) ? "1/1" : "0/1" ),

		new( "Catch a golden orb",
			"1-in-70 orbs come out golden: ×20 value. Charms improve the odds.",
			350, 2,
			p => p.Data.GoldenOrbs >= 1,
			p => $"{Math.Min( p.Data.GoldenOrbs, 1 )}/1" ),

		new( "Claim a Sky Chest",
			"A free chest charges up every 8 minutes you play. Grab it from the HUD!",
			500, 2,
			p => p.Data.ChestsClaimed >= 1,
			p => $"{Math.Min( p.Data.ChestsClaimed, 1 )}/1" ),

		new( "Unlock Floor 2",
			"Buy the STORM DECK pad to raise your tower and unlock bigger machines.",
			1_500, 3,
			p => p.Data.Purchased.Contains( "f2" ),
			p => p.Data.Purchased.Contains( "f2" ) ? "1/1" : "0/1" ),

		new( "Complete a daily quest",
			"Open the menu (Tab) → Daily. Fresh quests and gems every day.",
			4_000, 4,
			p => p.Data.DailyQuests.Any( q => q.Claimed ),
			p => $"{p.Data.DailyQuests.Count( q => q.Claimed ).Clamp( 0, 1 )}/1" ),

		new( "Reach 500k cash",
			"Floor 3 machines print money. Let it snowball — even while you're offline.",
			25_000, 5,
			p => p.Data.LifetimeCash >= 500_000,
			p => $"{Balance.Fmt( Math.Min( p.Data.LifetimeCash, 500_000 ) )}/500k" ),

		new( "REBIRTH!",
			"Open the menu (Tab) → Rebirth. Reset the island for +50% income, forever.",
			0, 25,
			p => p.Data.Rebirths >= 1,
			p => $"{Math.Min( p.Data.Rebirths, 1 )}/1" ),
	};

	public static MilestoneDef Current( PlayerProgress p ) =>
		p.Data.MilestoneIndex < Chain.Length ? Chain[p.Data.MilestoneIndex] : null;

	public static void Tick( PlayerProgress p )
	{
		var current = Current( p );
		if ( current is null ) return;
		if ( !current.IsDone( p ) ) return;

		var completedIndex = p.Data.MilestoneIndex;
		p.Data.MilestoneIndex++;
		if ( current.CashReward > 0 )
		{
			p.Data.Cash += current.CashReward;
			p.Data.LifetimeCash += current.CashReward;
		}
		if ( current.GemReward > 0 ) p.Data.Gems += current.GemReward;

		var reward = current.CashReward > 0 ? $" +{Balance.Fmt( current.CashReward )} cash" : "";
		var gems = current.GemReward > 0 ? $" +{current.GemReward} 💎" : "";
		p.AddToast( $"⭐ {current.Title} —{reward}{gems}" );
		Sfx.Play( "quest" );
		TycoonGame.Instance?.AdvanceTutorialTipAfterMilestone( completedIndex );
		p.RequestSave();
	}
}
