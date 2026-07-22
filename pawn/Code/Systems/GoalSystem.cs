namespace PawnShop;

public enum GoalMetric
{
	ItemsSold,
	ItemsBought,
	PawnsIssued,
	Redemptions,
	FakesCaught,
	ItemsCleaned,
	ItemsRepaired,
	ItemsResearched,
	SaleRevenue,
	DealsClosed,
	FloorsSwept,
	ShelvesDusted,
	PilesOrganized,
	TrashTakenOut,
	PlantsWatered,
	CounterPolished,
	ChoresDone,
}

/// <summary>A rollable daily goal.</summary>
public sealed class GoalDef
{
	public string Id { get; init; }
	public GoalMetric Metric { get; init; }
	public int Target { get; init; } = 1;
	public int Reward { get; init; } = 50;
	public string Icon { get; init; } = "flag";
	/// <summary>Text with {0} = target, {1} = reward.</summary>
	public string Template { get; init; }
	/// <summary>Earliest day this goal can roll.</summary>
	public int MinDay { get; init; } = 1;
	/// <summary>Requires the repair bench upgrade.</summary>
	public bool NeedsRepairBench { get; init; }

	public string Text => string.Format( Template, Target, GameConstants.FormatCash( Reward ) );
}

public static class GoalCatalog
{
	public static readonly List<GoalDef> All = new()
	{
		new GoalDef { Id = "sell2", Metric = GoalMetric.ItemsSold, Target = 2, Reward = 70, Icon = "sell", Template = "Sell {0} items" },
		new GoalDef { Id = "sell3", Metric = GoalMetric.ItemsSold, Target = 3, Reward = 120, Icon = "sell", Template = "Sell {0} items", MinDay = 4 },
		new GoalDef { Id = "buy2", Metric = GoalMetric.ItemsBought, Target = 2, Reward = 60, Icon = "shopping_bag", Template = "Buy {0} items from sellers" },
		new GoalDef { Id = "buy3", Metric = GoalMetric.ItemsBought, Target = 3, Reward = 100, Icon = "shopping_bag", Template = "Buy {0} items from sellers", MinDay = 5 },
		new GoalDef { Id = "pawn1", Metric = GoalMetric.PawnsIssued, Target = 1, Reward = 60, Icon = "handshake", Template = "Issue a pawn loan" },
		new GoalDef { Id = "redeem1", Metric = GoalMetric.Redemptions, Target = 1, Reward = 70, Icon = "paid", Template = "Complete a pawn redemption", MinDay = 4 },
		new GoalDef { Id = "fake1", Metric = GoalMetric.FakesCaught, Target = 1, Reward = 90, Icon = "gpp_bad", Template = "Catch a counterfeit", MinDay = 2 },
		new GoalDef { Id = "clean2", Metric = GoalMetric.ItemsCleaned, Target = 2, Reward = 45, Icon = "cleaning_services", Template = "Clean {0} items" },
		new GoalDef { Id = "repair1", Metric = GoalMetric.ItemsRepaired, Target = 1, Reward = 70, Icon = "handyman", Template = "Complete a repair", NeedsRepairBench = true },
		new GoalDef { Id = "research1", Metric = GoalMetric.ItemsResearched, Target = 1, Reward = 55, Icon = "science", Template = "Research an item", MinDay = 2 },
		new GoalDef { Id = "revenue250", Metric = GoalMetric.SaleRevenue, Target = 250, Reward = 80, Icon = "trending_up", Template = "Ring up ${0} in sales", MinDay = 2 },
		new GoalDef { Id = "revenue600", Metric = GoalMetric.SaleRevenue, Target = 600, Reward = 160, Icon = "trending_up", Template = "Ring up ${0} in sales", MinDay = 6 },
		new GoalDef { Id = "deals3", Metric = GoalMetric.DealsClosed, Target = 3, Reward = 75, Icon = "done_all", Template = "Close {0} deals of any kind" },
		new GoalDef { Id = "deals5", Metric = GoalMetric.DealsClosed, Target = 5, Reward = 140, Icon = "done_all", Template = "Close {0} deals of any kind", MinDay = 5 },

		// Shop-keeping chores (break up the customer loop).
		new GoalDef { Id = "sweep3", Metric = GoalMetric.FloorsSwept, Target = 3, Reward = 40, Icon = "cleaning_services", Template = "Sweep {0} dirty spots on the floor" },
		new GoalDef { Id = "dust2", Metric = GoalMetric.ShelvesDusted, Target = 2, Reward = 40, Icon = "auto_awesome", Template = "Dust {0} shelves or cases" },
		new GoalDef { Id = "organize2", Metric = GoalMetric.PilesOrganized, Target = 2, Reward = 45, Icon = "inventory_2", Template = "Restack {0} messy box piles" },
		new GoalDef { Id = "trash1", Metric = GoalMetric.TrashTakenOut, Target = 1, Reward = 35, Icon = "delete", Template = "Take the trash out to the alley dumpster" },
		new GoalDef { Id = "water1", Metric = GoalMetric.PlantsWatered, Target = 1, Reward = 25, Icon = "local_florist", Template = "Water the shop plant" },
		new GoalDef { Id = "polish1", Metric = GoalMetric.CounterPolished, Target = 1, Reward = 30, Icon = "countertops", Template = "Polish the service counter" },
		new GoalDef { Id = "chores4", Metric = GoalMetric.ChoresDone, Target = 4, Reward = 60, Icon = "home_repair_service", Template = "Finish {0} shop chores of any kind" },
		new GoalDef { Id = "chores6", Metric = GoalMetric.ChoresDone, Target = 6, Reward = 100, Icon = "home_repair_service", Template = "Finish {0} shop chores of any kind", MinDay = 4 },
	};

	private static Dictionary<string, GoalDef> _byId;
	public static GoalDef Get( string id )
	{
		_byId ??= All.ToDictionary( d => d.Id );
		return id is not null && _byId.TryGetValue( id, out var d ) ? d : null;
	}
}

/// <summary>
/// Rolls three daily goals each morning and pays out instantly when one completes.
/// Progress and completion live in the save so mid-day reloads keep them.
/// </summary>
public sealed class GoalSystem
{
	private readonly SaveData _save;

	public GoalSystem( SaveData save )
	{
		_save = save;
	}

	public IEnumerable<GoalDef> Today => _save.TodayGoals.Select( GoalCatalog.Get ).Where( g => g is not null );

	public int Progress( GoalDef goal ) => _save.GoalProgress.GetValueOrDefault( goal.Id );
	public bool IsComplete( GoalDef goal ) => _save.CompletedGoals.Contains( goal.Id );
	public bool AllComplete => Today.Any() && Today.All( IsComplete );

	/// <summary>Pick today's goals (idempotent per day).</summary>
	public void RollForDay( int day )
	{
		if ( _save.GoalDay == day && _save.TodayGoals.Count > 0 ) return;

		_save.GoalDay = day;
		_save.TodayGoals.Clear();
		_save.CompletedGoals.Clear();
		_save.GoalProgress.Clear();

		var pool = GoalCatalog.All.Where( g =>
			g.MinDay <= day
			&& (!g.NeedsRepairBench || _save.OwnsUpgrade( UpgradeId.RepairBench )) ).ToList();

		// Prefer at least one shop-chore goal so the day isn't only counter work.
		var chorePool = pool.Where( IsChoreGoal ).ToList();
		if ( chorePool.Count > 0 )
		{
			var chorePick = chorePool[Game.Random.Int( 0, chorePool.Count - 1 )];
			_save.TodayGoals.Add( chorePick.Id );
			pool.RemoveAll( g => g.Metric == chorePick.Metric );
		}

		while ( _save.TodayGoals.Count < 3 && pool.Count > 0 )
		{
			var pick = pool[Game.Random.Int( 0, pool.Count - 1 )];
			_save.TodayGoals.Add( pick.Id );
			pool.RemoveAll( g => g.Metric == pick.Metric );
		}
	}

	private static bool IsChoreGoal( GoalDef g ) => g.Metric is
		GoalMetric.FloorsSwept or GoalMetric.ShelvesDusted or GoalMetric.PilesOrganized
		or GoalMetric.TrashTakenOut or GoalMetric.PlantsWatered or GoalMetric.CounterPolished
		or GoalMetric.ChoresDone;

	/// <summary>Report progress on a metric. Pays instantly when a goal completes.</summary>
	public void Notify( GoalMetric metric, int amount = 1 )
	{
		var game = GameManager.Instance;
		if ( game is null || amount <= 0 ) return;

		foreach ( var goal in Today )
		{
			if ( goal.Metric != metric || IsComplete( goal ) ) continue;

			var progress = Progress( goal ) + amount;
			_save.GoalProgress[goal.Id] = progress;

			if ( progress >= goal.Target )
			{
				_save.CompletedGoals.Add( goal.Id );
				game.Economy.Earn( goal.Reward );
				game.Economy.Ledger.GoalBonuses += goal.Reward;
				Sfx.Play( Sfx.Reward, 0.7f );
				game.Toast( $"Goal complete: {goal.Text} — +{GameConstants.FormatCash( goal.Reward )}!", "flag" );

				if ( AllComplete )
				{
					game.Reputation.Add( 2f );
					game.Toast( "All daily goals done! The neighborhood takes notice. +Rep", "military_tech" );
				}
			}
		}
		UiState.Bump();
	}
}
