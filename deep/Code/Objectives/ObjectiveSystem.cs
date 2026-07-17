namespace Deep;

public sealed class ObjectiveTask
{
	public string Id { get; init; }
	public string Label { get; init; }
	public bool IsComplete { get; set; }
}

/// <summary>Simple dive objectives shown in the CURRENT OBJECTIVE panel.</summary>
public sealed class ObjectiveSystem
{
	public string Title { get; private set; } = "Explore The Depths";
	public IReadOnlyList<ObjectiveTask> Tasks => _tasks;
	public float TargetDepthMeters { get; private set; }
	public bool AllComplete => _tasks.Count > 0 && _tasks.All( t => t.IsComplete );
	public float RewardGold { get; private set; } = 75f;
	public float RewardShells { get; private set; } = 8f;

	private readonly List<ObjectiveTask> _tasks = new();
	private int _questIndex;
	private bool _rewardGranted;

	public void BeginSession( PlayerProgressionData progression )
	{
		_questIndex = Math.Max( 0, progression.SuccessfulDives );
		BuildQuest( progression );
	}

	public void BeginDive( PlayerProgressionData progression )
	{
		_rewardGranted = false;
		BuildQuest( progression );
	}

	public void Tick( DeepGame game )
	{
		if ( game is null || !game.State.IsDivingActive ) return;

		var depth = game.Diver?.CurrentDepthMeters ?? 0f;
		foreach ( var task in _tasks )
		{
			if ( task.IsComplete ) continue;
			if ( task.Id == "reach_depth" && depth >= TargetDepthMeters )
			{
				task.IsComplete = true;
				game.ShowMessage( "Objective updated!", 1.2f );
			}
			else if ( task.Id == "collect_any" && (game.Run?.Haul.ItemCount ?? 0) >= 1 )
			{
				task.IsComplete = true;
			}
		}

		if ( AllComplete && !_rewardGranted )
			game.Run.ObjectiveRewardPending = true;
	}

	public void NotifyToolUsed( ToolKind kind )
	{
		if ( kind == ToolKind.Harpoon )
			Complete( "harpoon_any" );
		if ( kind == ToolKind.Camera )
			Complete( "photo_any" );
		if ( kind == ToolKind.Scanner )
			Complete( "scan_once" );
	}

	public void NotifyPhoto( string collectibleId ) => Complete( "photo_any" );

	public void NotifyDiveSuccess() => Complete( "surface" );

	/// <summary>Records objective completion into the recap; payout happens with sale.</summary>
	public bool TryGrantReward( PlayerProgressionData progression, List<DiveBonusLine> bonuses )
	{
		if ( _rewardGranted || !AllComplete || progression is null )
			return false;

		_rewardGranted = true;
		bonuses?.Add( new DiveBonusLine { Label = "Objective Complete", Amount = RewardGold } );
		return true;
	}

	public void GrantShellReward( PlayerProgressionData progression )
	{
		if ( progression is null || !AllComplete ) return;
		progression.AddShells( RewardShells );
	}

	private void Complete( string id )
	{
		foreach ( var t in _tasks )
		{
			if ( t.Id == id && !t.IsComplete )
				t.IsComplete = true;
		}
	}

	private void BuildQuest( PlayerProgressionData progression )
	{
		_tasks.Clear();
		var deepest = progression?.DeepestEverMeters ?? 0f;
		var cycle = _questIndex % 4;

		switch ( cycle )
		{
			case 0:
				Title = "Find The Sunken Relay";
				TargetDepthMeters = Math.Clamp( deepest + 40f, 80f, 220f );
				RewardGold = 90f;
				RewardShells = 10f;
				_tasks.Add( new ObjectiveTask { Id = "reach_depth", Label = $"Reach {(int)TargetDepthMeters}m depth" } );
				_tasks.Add( new ObjectiveTask { Id = "collect_any", Label = "Recover any artifact" } );
				break;
			case 1:
				Title = "Wildlife Survey";
				TargetDepthMeters = Math.Clamp( 60f + deepest * 0.2f, 50f, 180f );
				RewardGold = 80f;
				RewardShells = 8f;
				_tasks.Add( new ObjectiveTask { Id = "photo_any", Label = "Photograph a find" } );
				_tasks.Add( new ObjectiveTask { Id = "reach_depth", Label = $"Survey down to {(int)TargetDepthMeters}m" } );
				break;
			case 2:
				Title = "Hazard Sweep";
				TargetDepthMeters = Math.Clamp( 90f, 70f, 200f );
				RewardGold = 100f;
				RewardShells = 12f;
				_tasks.Add( new ObjectiveTask { Id = "harpoon_any", Label = "Harpoon a hazard" } );
				_tasks.Add( new ObjectiveTask { Id = "scan_once", Label = "Run a scanner sweep" } );
				break;
			default:
				Title = "Deep Salvage Run";
				TargetDepthMeters = Math.Clamp( deepest + 25f, 100f, 280f );
				RewardGold = 110f;
				RewardShells = 14f;
				_tasks.Add( new ObjectiveTask { Id = "reach_depth", Label = $"Push to {(int)TargetDepthMeters}m" } );
				_tasks.Add( new ObjectiveTask { Id = "surface", Label = "Return to the boat" } );
				break;
		}
	}
}
