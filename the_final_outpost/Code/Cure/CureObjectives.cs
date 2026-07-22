namespace FinalOutpost;

public sealed class CureObjectiveDef
{
	public string Id { get; init; }
	public string Title { get; init; }
	public string Icon { get; init; }
	public int RequiredTier { get; init; }
	public Func<GameCore, bool> IsMet { get; init; }
}

public static class CureObjectives
{
	public static readonly IReadOnlyList<CureObjectiveDef> All = new List<CureObjectiveDef>
	{
		new()
		{
			Id = "claim_plot", Title = "Claim an adjacent plot", Icon = "map", RequiredTier = 1,
			IsMet = c => (c?.Save.OwnedPlots.Count ?? 0) > 1
		},
		new()
		{
			Id = "clear_plot", Title = "Clear a plot for building", Icon = "landscape", RequiredTier = 2,
			IsMet = c => (c?.Save.ClearedPlots.Count ?? 0) > 0
		},
		new()
		{
			Id = "send_expedition", Title = "Complete a scout expedition", Icon = "explore", RequiredTier = 2,
			IsMet = c => c?.Save.EverSentScouts == true
		},
		new()
		{
			Id = "build_lab", Title = "Build a Research Lab", Icon = "science", RequiredTier = 1,
			IsMet = c => BuildManager.Instance?.Buildings.Any( b => !b.IsDestroyed && b.Type == BuildableId.Lab ) == true
		},
		new()
		{
			Id = "own_three_plots", Title = "Own 3 plots", Icon = "flag", RequiredTier = 3,
			IsMet = c => (c?.Save.OwnedPlots.Count ?? 0) >= 3
		},
		new()
		{
			Id = "survive_winter", Title = "Survive a winter threat", Icon = "ac_unit", RequiredTier = 3,
			IsMet = c => c?.Save.EverSurvivedWinterThreat == true
		},
		new()
		{
			Id = "survive_threats_25", Title = "Survive 25 threats", Icon = "shield", RequiredTier = 4,
			IsMet = c => (c?.Save.TotalThreatsSurvived ?? 0) >= 25
		},
		new()
		{
			Id = "own_five_plots", Title = "Own 5 plots", Icon = "flag", RequiredTier = 4,
			IsMet = c => (c?.Save.OwnedPlots.Count ?? 0) >= 5
		}
	};

	public static bool IsDone( GameCore core, string id ) =>
		core?.Save?.CureObjectivesDone?.Contains( id ) == true;

	public static string EvaluateAndReward( GameCore core )
	{
		if ( core is null || !core.IsCure ) return null;

		var done = core.Save.CureObjectivesDone;
		string firstNew = null;

		foreach ( var o in All )
		{
			if ( done.Contains( o.Id ) ) continue;
			if ( o.IsMet is null || !o.IsMet( core ) ) continue;

			done.Add( o.Id );
			firstNew ??= o.Title;
		}

		if ( firstNew is not null )
			core.SaveManagerTouch();

		return firstNew;
	}

	public static bool ObjectivesMetForTier( SaveData save, int tier )
	{
		foreach ( var o in All.Where( x => x.RequiredTier == tier ) )
		{
			if ( !save.CureObjectivesDone.Contains( o.Id ) )
				return false;
		}
		return true;
	}

	public static int DoneCount( SaveData save ) =>
		All.Count( o => save.CureObjectivesDone.Contains( o.Id ) );
}
