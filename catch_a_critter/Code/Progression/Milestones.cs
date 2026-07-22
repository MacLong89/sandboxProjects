namespace CatchACritter;

public sealed record MilestoneDef( string Title, string Detail, double CoinReward, int GemReward, Func<PlayerProgress, bool> IsDone, Func<PlayerProgress, string> ProgressLabel );

/// <summary>
/// The onboarding momentum chain. One goal at a time, each with a burst reward,
/// walking a brand-new player straight into every core system.
/// </summary>
public static class Milestones
{
	public static readonly MilestoneDef[] Chain =
	{
		new( "Catch your first critter!",
			"Walk up to a critter and swing your net (Left Click).",
			25, 0,
			p => p.Data.LifetimeCatches >= 1,
			p => $"{Math.Min( p.Data.LifetimeCatches, 1 )}/1" ),

		new( "Fill your backpack",
			"Catch 5 critters. Sneak (hold Ctrl) to get close to skittish ones!",
			60, 0,
			p => p.Data.LifetimeCatches >= 5,
			p => $"{Math.Min( p.Data.LifetimeCatches, 5 )}/5" ),

		new( "Sell your haul",
			"Bring your critters to the SELL stand in the hub and press E.",
			100, 1,
			p => p.Data.SellCount >= 1,
			p => $"{Math.Min( p.Data.SellCount, 1 )}/1" ),

		new( "Upgrade your gear",
			"Open the Shop (Tab) and buy any upgrade or a better net.",
			150, 1,
			p => p.Data.NetPower > 0 || p.Data.SpeedLevel > 0 || p.Data.BackpackLevel > 0 || p.Data.LuckLevel > 0,
			p => (p.Data.NetPower > 0 || p.Data.SpeedLevel > 0 || p.Data.BackpackLevel > 0 || p.Data.LuckLevel > 0) ? "1/1" : "0/1" ),

		new( "Start your sanctuary",
			"Open Sanctuary (Tab) and Keep a caught critter instead of selling it.",
			200, 2,
			p => p.Data.Sanctuary.Count >= 1,
			p => $"{Math.Min( p.Data.Sanctuary.Count, 1 )}/1" ),

		new( "Make a friend",
			"Set a sanctuary critter as your follower — it buffs you as it tags along!",
			250, 2,
			p => p.Data.FollowerIds.Count >= 1,
			p => $"{Math.Min( p.Data.FollowerIds.Count, 1 )}/1" ),

		new( "Cross the bridge",
			"Save up and unlock Whisperwood — rarer critters, bigger payouts.",
			800, 3,
			p => p.Data.UnlockedZones.Count >= 2,
			p => $"{Math.Min( p.Data.UnlockedZones.Count - 1, 1 )}/1" ),

		new( "Hatch an egg",
			"Breed two same-species critters at the Sanctuary and hatch the egg.",
			2_000, 5,
			p => p.Data.Codex.Values.Any( e => e.Bred > 0 ),
			p => p.Data.Codex.Values.Any( e => e.Bred > 0 ) ? "1/1" : "0/1" ),

		new( "Complete a daily quest",
			"Check the Daily board — fresh quests and gems every single day.",
			3_000, 5,
			p => p.Data.DailyQuests.Any( q => q.Claimed ),
			p => $"{p.Data.DailyQuests.Count( q => q.Claimed ).Clamp( 0, 1 )}/1" ),

		new( "Ascend!",
			"Reach the Ascend statue's price and be reborn with a permanent crown.",
			0, 20,
			p => p.Data.Crowns >= 1,
			p => $"{Math.Min( p.Data.Crowns, 1 )}/1" ),
	};

	public static MilestoneDef Current( PlayerProgress p ) =>
		p.Data.MilestoneIndex < Chain.Length ? Chain[p.Data.MilestoneIndex] : null;

	public static void Tick( PlayerProgress p )
	{
		var current = Current( p );
		if ( current is null ) return;
		if ( !current.IsDone( p ) ) return;

		p.Data.MilestoneIndex++;
		if ( current.CoinReward > 0 ) p.AddCoins( current.CoinReward );
		if ( current.GemReward > 0 ) p.Data.Gems += current.GemReward;

		var rewards = new List<string>();
		if ( current.CoinReward > 0 ) rewards.Add( $"+{Balance.Fmt( current.CoinReward )} coins" );
		if ( current.GemReward > 0 ) rewards.Add( $"+{current.GemReward} 💎" );
		var suffix = rewards.Count > 0 ? $" ({string.Join( ", ", rewards )})" : "";
		p.AddToast( $"Goal complete: {current.Title}{suffix}", "⭐" );
		Sfx.Play( "unlock" );
		p.RequestSave();
	}
}
