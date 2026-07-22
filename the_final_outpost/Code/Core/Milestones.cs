namespace FinalOutpost;

public sealed class MilestoneDef
{
	public string Id { get; init; }
	public string Title { get; init; }
	public string Icon { get; init; }
	public double Reward { get; init; }
	public Func<SaveData, long> Progress { get; init; }
	public long Goal { get; init; }

	public bool IsMet( SaveData s ) => Progress( s ) >= Goal;
	public float Fraction( SaveData s ) => Goal <= 0 ? 1f : Math.Clamp( (float)Progress( s ) / Goal, 0f, 1f );
}

/// <summary>
/// Long-horizon milestone track built on lifetime stats. Unlike the onboarding objectives (one-time,
/// early game), milestones keep giving players goals to chase across many sessions and pay out scrap
/// bounties — steady long-term motivation that pairs with the prestige loop.
/// </summary>
public static class Milestones
{
	public static readonly IReadOnlyList<MilestoneDef> All = new List<MilestoneDef>
	{
		new() { Id = "kills_50",    Title = "First Blood",   Icon = "skull",         Reward = 30,   Progress = s => s.TotalKills, Goal = 50 },
		new() { Id = "kills_500",   Title = "Exterminator",  Icon = "skull",         Reward = 100,  Progress = s => s.TotalKills, Goal = 500 },
		new() { Id = "kills_2500",  Title = "Horde Breaker",  Icon = "skull",        Reward = 300,  Progress = s => s.TotalKills, Goal = 2500 },
		new() { Id = "night_5",     Title = "Survivor",      Icon = "bedtime",       Reward = 60,   Progress = s => s.BestNight, Goal = 5 },
		new() { Id = "night_10",    Title = "Hardened",      Icon = "bedtime",       Reward = 130,  Progress = s => s.BestNight, Goal = 10 },
		new() { Id = "night_20",    Title = "Unbreakable",   Icon = "bedtime",       Reward = 350,  Progress = s => s.BestNight, Goal = 20 },
		new() { Id = "scrap_5k",    Title = "Scavenger",     Icon = "recycling",     Reward = 75,   Progress = s => (long)s.LifetimeEarned, Goal = 5000 },
		new() { Id = "scrap_50k",   Title = "Hoarder",       Icon = "recycling",     Reward = 400,  Progress = s => (long)s.LifetimeEarned, Goal = 50000 },
		new() { Id = "catalog_all", Title = "Field Guide",   Icon = "menu_book",     Reward = 150,  Progress = s => ZombieBestiary.DiscoveredCount( s ), Goal = ZombieCatalog.All.Count },
	};

	public static bool IsDone( SaveData s, string id ) => s?.MilestonesDone.Contains( id ) == true;

	public static int CompletedCount( SaveData s )
	{
		if ( s is null ) return 0;
		var n = 0;
		foreach ( var m in All )
			if ( s.MilestonesDone.Contains( m.Id ) ) n++;
		return n;
	}

	/// <summary>Grants any newly-completed milestone rewards; returns the first new title (for a toast).</summary>
	public static string EvaluateAndReward( GameCore core )
	{
		if ( core is null ) return null;
		var save = core.Save;
		string firstNew = null;

		foreach ( var m in All )
		{
			if ( save.MilestonesDone.Contains( m.Id ) ) continue;
			if ( !m.IsMet( save ) ) continue;

			save.MilestonesDone.Add( m.Id );
			core.Wallet.Earn( m.Reward );
			firstNew ??= m.Title;
		}

		if ( firstNew is not null )
			core.SaveManagerTouch();

		return firstNew;
	}
}
