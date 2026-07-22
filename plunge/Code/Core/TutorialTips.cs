namespace Plunge;

public sealed class TutorialTipDef
{
	public string Id { get; init; }
	public string Title { get; init; }
	public string Body { get; init; }
	public string Icon { get; init; } = "tips_and_updates";
	public int Priority { get; init; }
}

/// <summary>Short workshop coach tips — goal-gated, one at a time.</summary>
public static class TutorialTips
{
	public static readonly IReadOnlyList<TutorialTipDef> All = new List<TutorialTipDef>
	{
		new()
		{
			Id = "welcome", Priority = 100, Icon = "scuba_diving",
			Title = "Welcome to Plunge",
			Body = "Gear up in the workshop, then dive. WASD swims, mouse aims your tools, and the surface ends every run."
		},
		new()
		{
			Id = "collect", Priority = 90, Icon = "inventory_2",
			Title = "Collect and haul",
			Body = "Grab fish, artifacts, and salvage on the slope. Cargo fills fast — return topside to bank credits and log discoveries."
		},
		new()
		{
			Id = "shop", Priority = 80, Icon = "upgrade",
			Title = "Shop and upgrades",
			Body = "Spend credits on diver gear and submarine upgrades. Better O₂, hull, and storage let you push deeper each dive."
		},
		new()
		{
			Id = "done", Priority = 40, Icon = "check_circle",
			Title = "Ready to dive",
			Body = "Hit Dive Now when you're set. Press H any time to hide these tips."
		},
	};

	public static bool ShouldRun( PlungeSaveData save ) =>
		save is not null && !save.HideTutorialTips;

	public static TutorialTipDef PickNext( PlungeSaveData save )
	{
		if ( !ShouldRun( save ) )
			return null;

		save.TutorialTipsShown ??= new List<string>();
		foreach ( var tip in All.OrderByDescending( t => t.Priority ) )
		{
			if ( save.TutorialTipsShown.Contains( tip.Id ) )
				continue;
			if ( !MeetsCondition( tip, save ) )
				continue;

			return tip;
		}

		return null;
	}

	public static void MarkShown( PlungeSaveData save, string id )
	{
		if ( save is null || string.IsNullOrEmpty( id ) )
			return;

		save.TutorialTipsShown ??= new List<string>();
		if ( !save.TutorialTipsShown.Contains( id ) )
			save.TutorialTipsShown.Add( id );
	}

	static bool MeetsCondition( TutorialTipDef tip, PlungeSaveData save ) => tip.Id switch
	{
		"welcome" => true,
		"collect" => save.TotalDives >= 1,
		"shop" => save.TotalCredits > 0,
		"done" => save.HasShopPurchase,
		_ => true
	};
}
