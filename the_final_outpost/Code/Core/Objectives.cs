namespace FinalOutpost;

public sealed class ObjectiveDef
{
	public string Id { get; init; }
	public string Title { get; init; }
	public string Icon { get; init; }
	public double Reward { get; init; }
	public Func<GameCore, bool> IsMet { get; init; }
}

/// <summary>
/// Lightweight onboarding checklist. Each goal teaches one core loop action and pays a little scrap
/// on completion — this both explains the game to new players and gives an early sense of momentum,
/// which is important for first-session retention.
/// </summary>
public static class Objectives
{
	public static readonly IReadOnlyList<ObjectiveDef> All = new List<ObjectiveDef>
	{
		new()
		{
			Id = "build_tower", Title = "Build a defense tower", Icon = "adjust", Reward = 15,
			IsMet = c => BuildManager.Instance?.Buildings.Any( b => b.IsDefense ) == true
		},
		new()
		{
			Id = "build_barracks", Title = "Build a Barracks", Icon = "groups", Reward = 20,
			IsMet = c => BuildManager.Instance?.BarracksCount > 0
		},
		new()
		{
			Id = "recruit_soldier", Title = "Recruit a soldier", Icon = "military_tech", Reward = 17.5,
			IsMet = c => (c?.Save.Recruits.Count ?? 0) > 0
		},
		new()
		{
			Id = "survive_night", Title = "Survive your first night", Icon = "bedtime", Reward = 22.5,
			IsMet = c => (c?.Save.BestNight ?? 0) >= 1 || (c?.Save.CurrentNight ?? 1) > 1
		},
		new()
		{
			Id = "hire_worker", Title = "Hire a worker", Icon = "engineering", Reward = 20,
			IsMet = c => WorkerManager.Instance?.Count > 0
		},
		new()
		{
			Id = "send_scouts", Title = "Send out a scout party", Icon = "explore", Reward = 27.5,
			IsMet = c => c?.Save.EverSentScouts == true
		},
		new()
		{
			Id = "claim_plot", Title = "Claim a land plot", Icon = "map", Reward = 20,
			IsMet = c => (c?.Save.OwnedPlots.Count ?? 0) > 1
		},
	};

	/// <summary>Marks any newly-met objectives complete, pays rewards, and returns the first new title (for a toast).</summary>
	public static string EvaluateAndReward( GameCore core )
	{
		if ( core is null ) return null;

		var done = core.Save.ObjectivesDone;
		string firstNew = null;

		foreach ( var o in All )
		{
			if ( done.Contains( o.Id ) ) continue;
			if ( o.IsMet is null || !o.IsMet( core ) ) continue;

			done.Add( o.Id );
			core.Wallet.Earn( o.Reward );
			firstNew ??= o.Title;
		}

		if ( firstNew is not null )
			core.SaveManagerTouch();

		return firstNew;
	}

	public static bool IsDone( GameCore core, string id ) => core?.Save.ObjectivesDone.Contains( id ) == true;

	public static int CompletedCount( GameCore core )
	{
		if ( core is null ) return 0;
		var n = 0;
		foreach ( var o in All )
			if ( core.Save.ObjectivesDone.Contains( o.Id ) ) n++;
		return n;
	}
}
