namespace FinalOutpost;

public sealed class TutorialTipDef
{
	public string Id { get; init; }
	public int MinNight { get; init; } = 1;
	public int MaxNight { get; init; } = TutorialTips.MaxNight;
	public string Title { get; init; }
	public string Body { get; init; }
	public string Icon { get; init; } = "tips_and_updates";
	public int Priority { get; init; }
}

/// <summary>
/// Contextual coach marks for the first few nights — teaches build → defend → repair → barracks →
/// recruits, plus warnings when new zombie types first appear.
/// </summary>
public static class TutorialTips
{
	public const int MaxNight = 5;

	public static readonly IReadOnlyList<TutorialTipDef> All = new List<TutorialTipDef>
	{
		new()
		{
			Id = "welcome", MinNight = 1, MaxNight = 1, Priority = 100, Icon = "wb_sunny",
			Title = "Day breaks on the outpost",
			Body = "Build during the day, defend at night. Zombies rush your Command Post — if it falls, "
				+ "you lose everything and must rebuild from scratch."
		},
		new()
		{
			Id = "build_tower", MinNight = 1, MaxNight = 2, Priority = 90, Icon = "adjust",
			Title = "Place a Gun Tower",
			Body = "Select Gun Tower in the build dock and click open ground near your walls. "
				+ "Towers auto-fire at the nearest zombies — stats appear in the bottom-left preview."
		},
		new()
		{
			Id = "start_night", MinNight = 1, MaxNight = 1, Priority = 80, Icon = "bedtime",
			Title = "Start your first night",
			Body = "When you are ready, press Start Night in the build dock. "
				+ "You earn scrap for every kill and a bonus if you survive until dawn."
		},
		new()
		{
			Id = "repair", MinNight = 2, MaxNight = 3, Priority = 85, Icon = "build",
			Title = "Repair damage",
			Body = "Structures take hits every night. Click a wall or building to repair it, "
				+ "or use Repair All in the build dock before the next wave."
		},
		new()
		{
			Id = "barracks", MinNight = 2, MaxNight = 5, Priority = 78, Icon = "groups",
			Title = "Build a Barracks first",
			Body = "You need a Barracks before recruiting soldiers. Each Barracks holds up to 3 recruits, "
				+ "slowly heals them during the day, and fully restores nearby defenders at dawn. "
				+ "Recruits who fall during a night are gone for good."
		},
		new()
		{
			Id = "recruit", MinNight = 2, MaxNight = 4, Priority = 72, Icon = "military_tech",
			Title = "Train recruits",
			Body = "With a Barracks built, open Recruits in the build dock. "
				+ "Each Barracks supports up to 3 soldiers — build more barracks to expand your squad."
		},
		new()
		{
			Id = "zombie_runner", MinNight = 3, MaxNight = 3, Priority = 95, Icon = "directions_run",
			Title = "Tonight: Runners",
			Body = "Runners are fast and vault over walls. "
				+ "Layer towers behind your perimeter and keep recruits near the core."
		},
		new()
		{
			Id = "zombie_swarm", MinNight = 4, MaxNight = 4, Priority = 95, Icon = "pest_control",
			Title = "Tonight: Swarms",
			Body = "Swarms are small, quick, and arrive in numbers. "
				+ "More gun coverage helps — spread towers so nothing slips through unchecked."
		},
		new()
		{
			Id = "zombie_brute", MinNight = 5, MaxNight = 5, Priority = 95, Icon = "fitness_center",
			Title = "Tonight: Brutes",
			Body = "Brutes are slow but very tanky and smash structures hard. "
				+ "Focus fire on them and repair walls between waves."
		},
		new()
		{
			Id = "expand", MinNight = 5, MaxNight = 5, Priority = 55, Icon = "map",
			Title = "Claim nearby land",
			Body = "Each plot holds up to 6 towers and barracks. Survive tonight and you earn a "
				+ "+200 scrap plot fund — click frontier plots around your base to claim them. "
				+ "Staff foragers to clear debris so new land becomes buildable."
		},
		new()
		{
			Id = "tutorial_done", MinNight = 5, MaxNight = 5, Priority = 45, Icon = "check_circle",
			Title = "You are on your own",
			Body = "New zombie types and unlocks appear as nights progress. "
				+ "Press H any time to hide these tips on future runs."
		}
	};

	public static bool ShouldRun( SaveData save ) =>
		save is not null && !save.HideTutorialTips;

	public static TutorialTipDef PickNext( GameCore core )
	{
		if ( core?.Save is null || !ShouldRun( core.Save ) )
			return null;

		var night = core.Save.CurrentNight;
		if ( night > MaxNight )
			return null;

		if ( core.Phase != GamePhase.Day )
			return null;

		var shown = core.Save.TutorialTipsShown;
		TutorialTipDef best = null;

		foreach ( var tip in All.OrderByDescending( t => t.Priority ) )
		{
			if ( shown.Contains( tip.Id ) )
				continue;
			if ( night < tip.MinNight || night > tip.MaxNight )
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

		if ( !save.TutorialTipsShown.Contains( id ) )
			save.TutorialTipsShown.Add( id );
	}

	private static bool MeetsCondition( TutorialTipDef tip, GameCore core )
	{
		return tip.Id switch
		{
			"build_tower" => !HasDefenseTower(),
			"start_night" => HasDefenseTower(),
			"barracks" => !HasBarracks(),
			"recruit" => HasBarracks() && (core?.Save.Recruits.Count ?? 0) == 0,
			"expand" => ShouldOfferExpand( core ),
			"tutorial_done" => !ShouldOfferExpand( core ) || core.Save.TutorialTipsShown.Contains( "expand" ),
			_ => true
		};
	}

	private static bool HasDefenseTower() =>
		BuildManager.Instance?.Buildings.Any( b => b.IsDefense ) == true;

	private static bool HasBarracks() =>
		BuildManager.Instance?.Buildings.Any( b => b.Type == BuildableId.Barracks ) == true;

	private static bool ShouldOfferExpand( GameCore c ) =>
		c is not null
		&& (c.Save.OwnedPlots.Count <= 1)
		&& HasBarracks();
}
