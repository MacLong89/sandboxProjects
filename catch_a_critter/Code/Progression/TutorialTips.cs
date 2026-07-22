namespace CatchACritter;

public sealed class TutorialTipDef
{
	public string Id { get; init; }
	public string Title { get; init; }
	public string Body { get; init; }
	public string Icon { get; init; } = "tips_and_updates";
	public int Priority { get; init; }
}

/// <summary>
/// Short Critter Isle coach tips. Each tip waits on a real progress gate
/// (catch, sell, upgrade) so they never fire back-to-back on dismiss alone.
/// </summary>
public static class TutorialTips
{
	public static readonly IReadOnlyList<TutorialTipDef> All = new List<TutorialTipDef>
	{
		new()
		{
			Id = "welcome", Priority = 100, Icon = "pets",
			Title = "Welcome to Critter Isle",
			Body = "Follow the green arch to Sunny Meadow. Sneak with Ctrl, then Left Click to swing your net."
		},
		new()
		{
			Id = "sell", Priority = 90, Icon = "sell",
			Title = "Sell your catches",
			Body = "Nice catch! Bring critters to the gold SELL stand in the hub plaza for coins — or keep rare ones for your sanctuary."
		},
		new()
		{
			Id = "upgrade", Priority = 80, Icon = "upgrade",
			Title = "Upgrade and explore",
			Body = "Spend coins on better nets, unlock biomes, and breed shinies. Tab opens your menu anytime."
		},
		new()
		{
			Id = "done", Priority = 40, Icon = "check_circle",
			Title = "You're on your own",
			Body = "Press H any time to hide these tips."
		},
	};

	public static bool ShouldRun( SaveData save ) =>
		save is not null && !save.HideTutorialTips;

	public static TutorialTipDef PickNext( SaveData save )
	{
		if ( !ShouldRun( save ) )
			return null;

		var shown = save.TutorialTipsShown ??= new List<string>();
		TutorialTipDef best = null;

		foreach ( var tip in All.OrderByDescending( t => t.Priority ) )
		{
			if ( shown.Contains( tip.Id ) )
				continue;
			if ( !MeetsCondition( tip, save ) )
				continue;

			best = tip;
			break;
		}

		return best;
	}

	public static void MarkShown( SaveData save, string id )
	{
		if ( save is null || string.IsNullOrEmpty( id ) )
			return;

		save.TutorialTipsShown ??= new List<string>();
		if ( !save.TutorialTipsShown.Contains( id ) )
			save.TutorialTipsShown.Add( id );
	}

	/// <summary>
	/// Unlock gates — tip N only appears after the player did the thing tip N-1 asked for.
	/// </summary>
	static bool MeetsCondition( TutorialTipDef tip, SaveData save ) => tip.Id switch
	{
		// First tip — always available until dismissed.
		"welcome" => true,

		// After they've actually caught something.
		"sell" => save.LifetimeCatches >= 1,

		// After they've sold at the hub stand.
		"upgrade" => save.SellCount >= 1,

		// After they've spent coins on progression (net / upgrade / keep / gate).
		"done" => HasProgressedPastSell( save ),

		_ => true
	};

	static bool HasProgressedPastSell( SaveData save ) =>
		save.NetPower > 0
		|| save.SpeedLevel > 0
		|| save.BackpackLevel > 0
		|| save.LuckLevel > 0
		|| save.Sanctuary.Count > 0
		|| save.UnlockedZones.Count > 1;
}
