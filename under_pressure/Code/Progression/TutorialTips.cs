namespace UnderPressure;

public sealed class TutorialTipDef
{
	public string Id { get; init; }
	public string Title { get; init; }
	public string Body { get; init; }
	public string Icon { get; init; } = "tips_and_updates";
	public int Priority { get; init; }
	/// <summary>Tip is eligible while save.JobIndex is at or below this (inclusive).</summary>
	public int MaxJobIndex { get; init; } = 2;
}

/// <summary>Short coach tips for the first few jobs — goal-gated, one at a time.</summary>
public static class TutorialTips
{
	public const int MaxJob = 2;

	public static readonly IReadOnlyList<TutorialTipDef> All = new List<TutorialTipDef>
	{
		new()
		{
			Id = "welcome", Priority = 100, Icon = "local_shipping", MaxJobIndex = 0,
			Title = "Pressure washing pays",
			Body = "Aim at grime and hold Left Click to blast it clean. Finish the job, then right-click your van to leave."
		},
		new()
		{
			Id = "van_shop", Priority = 90, Icon = "storefront", MaxJobIndex = 1,
			Title = "Your van is the shop",
			Body = "Right-click the van between jobs to buy tools and upgrades. Equip the right tool for each pest type."
		},
		new()
		{
			Id = "pests", Priority = 85, Icon = "pest_control", MaxJobIndex = 2,
			Title = "Pests show up",
			Body = "The left feed lists pests on site and which tool kills them. Switch tools at the van if you brought the wrong one."
		},
		new()
		{
			Id = "done", Priority = 40, Icon = "check_circle", MaxJobIndex = 2,
			Title = "You're on your own",
			Body = "Keep jobs spotless for bonuses. Press H any time to hide these tips."
		},
	};

	public static bool ShouldRun( SaveData save ) =>
		save is not null && !save.HideTutorialTips && save.JobIndex <= MaxJob;

	public static TutorialTipDef PickNext( GameCore core )
	{
		var save = core?.Save;
		if ( !ShouldRun( save ) )
			return null;

		var shown = save.TutorialTipsShown ??= new List<string>();
		TutorialTipDef best = null;

		foreach ( var tip in All.OrderByDescending( t => t.Priority ) )
		{
			if ( shown.Contains( tip.Id ) )
				continue;
			if ( save.JobIndex > tip.MaxJobIndex )
				continue;
			if ( !MeetsCondition( tip, core ) )
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

	/// <summary>Unlock gates — tip N only appears after the player did what tip N-1 asked for.</summary>
	static bool MeetsCondition( TutorialTipDef tip, GameCore core )
	{
		var save = core.Save;
		return tip.Id switch
		{
			"welcome" => true,
			"van_shop" => save.HasCleanedSomeDirt || save.HasOpenedVanOrShop,
			"pests" => save.PestsKilled >= 1
				|| (save.JobIndex >= 1 && EnemyManager.Instance?.JobHasEnemies == true),
			"done" => save.JobIndex >= 1 || save.HasOpenedVanOrShop,
			_ => true
		};
	}
}
