namespace RunGun;

public sealed class TutorialTipDef
{
	public string Id { get; init; }
	public string Title { get; init; }
	public string Body { get; init; }
	public string Icon { get; init; } = "tips_and_updates";
	public int Priority { get; init; }
}

/// <summary>Short riot-run basics — goal-gated during the first tutorial run.</summary>
public static class TutorialTips
{
	public const float RedTipDistanceMeters = 60f;

	public static readonly IReadOnlyList<TutorialTipDef> All = new List<TutorialTipDef>
	{
		new()
		{
			Id = "move", Priority = 100, Icon = "swap_horiz",
			Title = "Move and dodge",
			Body = "A / D strafe between lanes. Dodge RED walls — they always kill crew."
		},
		new()
		{
			Id = "green", Priority = 90, Icon = "groups",
			Title = "Green gates grow your mob",
			Body = "Take the BIGGER green gate to grow your riot squad."
		},
		new()
		{
			Id = "red", Priority = 85, Icon = "dangerous",
			Title = "Red gates kill",
			Body = "RED gates always kill crew. One bad gate can end the run."
		},
		new()
		{
			Id = "done", Priority = 40, Icon = "check_circle",
			Title = "Riot on",
			Body = "Space for Riot Surge when the meter fills. Press H any time to hide tips."
		},
	};

	public static bool ShouldRun( SaveData save ) =>
		save is not null && !save.HideTutorialTips && !save.HasCompletedTutorialRun;

	public static TutorialTipDef PickNext( GameCore core )
	{
		var save = core?.Save;
		if ( !ShouldRun( save ) )
			return null;

		var shown = save.TutorialTipsShown ??= new List<string>();

		foreach ( var tip in All.OrderByDescending( t => t.Priority ) )
		{
			if ( shown.Contains( tip.Id ) )
				continue;
			if ( !MeetsCondition( tip, core ) )
				continue;

			return tip;
		}

		return null;
	}

	public static void MarkShown( SaveData save, string id )
	{
		if ( save is null || string.IsNullOrEmpty( id ) )
			return;

		save.TutorialTipsShown ??= new List<string>();
		if ( !save.TutorialTipsShown.Contains( id ) )
			save.TutorialTipsShown.Add( id );
	}

	static bool MeetsCondition( TutorialTipDef tip, GameCore core )
	{
		var save = core.Save;
		var run = core.Run;
		return tip.Id switch
		{
			"move" => true,
			"green" => save.TutorialGreenGatePassed,
			"red" => save.TutorialRedSurvived || run.DistanceMeters >= RedTipDistanceMeters,
			"done" => run.GatesCrossed >= GameConstants.TutorialGateRows,
			_ => true
		};
	}
}
