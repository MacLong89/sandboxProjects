namespace FinalOutpost;

/// <summary>Player-paid repairs that restore HP over time — duration scales with scrap cost.</summary>
public sealed class RepairManager : Component
{
	public static RepairManager Instance { get; private set; }

	private readonly Queue<RepairJob> _queue = new();
	private RepairJob _active;

	private readonly HashSet<PlacedBuilding> _buildings = new();
	private readonly HashSet<WallSegment> _walls = new();
	private bool _coreScheduled;

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
		Clear();
	}

	public bool IsBusy => _active is not null || _queue.Count > 0;

	public float ActiveProgress => _active?.Progress ?? 0f;

	public string ActiveLabel => _active?.Label ?? "";

	/// <summary>Active + queued jobs, or pending damage when idle.</summary>
	public void FillRepairLines( List<RepairLine> lines, IEnumerable<PlacedBuilding> buildings )
	{
		lines.Clear();

		if ( _active is not null )
			lines.Add( new RepairLine( _active.Label, ProgressPct( _active.Progress ), true, false ) );

		foreach ( var job in _queue )
			lines.Add( new RepairLine( job.Label, 0, false, true ) );

		if ( lines.Count > 0 )
			return;

		var outpost = OutpostManager.Instance;
		if ( outpost is not null && outpost.CoreHealth < outpost.CoreMaxHealth )
		{
			var pct = HealthPct( outpost.CoreHealth, outpost.CoreMaxHealth );
			lines.Add( new RepairLine( "Command Post", pct, false, false ) );
		}

		if ( outpost is not null )
		{
			var wallIdx = 0;
			foreach ( var w in outpost.Walls )
			{
				if ( w.Health >= w.MaxHealth ) continue;
				wallIdx++;
				lines.Add( new RepairLine( WallLabel( wallIdx ), HealthPct( w.Health, w.MaxHealth ), false, false ) );
			}
		}

		foreach ( var b in buildings )
		{
			if ( b is null || b.IsDestroyed || b.Health >= b.MaxHealth ) continue;
			lines.Add( new RepairLine( b.Def.Name, HealthPct( b.Health, b.MaxHealth ), false, false ) );
		}
	}

	public readonly record struct RepairLine( string Label, int Percent, bool Active, bool Queued );

	private static int ProgressPct( float progress ) => (int)MathF.Round( Math.Clamp( progress, 0f, 1f ) * 100f );

	private static int HealthPct( float health, float maxHealth ) =>
		maxHealth <= 0f ? 0 : (int)MathF.Round( Math.Clamp( health / maxHealth, 0f, 1f ) * 100f );

	private static string WallLabel( int index ) => index <= 1 ? "Wall" : $"Wall #{index}";

	public bool IsScheduled( PlacedBuilding building ) =>
		building is not null && _buildings.Contains( building );

	public bool IsScheduled( WallSegment wall ) =>
		wall is not null && _walls.Contains( wall );

	public bool IsScheduledCore() => _coreScheduled;

	public float ProgressFor( PlacedBuilding building ) => ProgressForTarget( building, null, false );
	public float ProgressFor( WallSegment wall ) => ProgressForTarget( null, wall, false );
	public float ProgressForCore() => ProgressForTarget( null, null, true );

	public bool TryRepairBuilding( PlacedBuilding building )
	{
		var core = GameCore.Instance;
		if ( core is null || core.Phase != GamePhase.Day || building is null ) return false;
		if ( building.Health >= building.MaxHealth || IsScheduled( building ) ) return false;

		var fullCost = building.MissingRepairCost();
		var spend = SelectHelp.AffordableRepairSpend( fullCost );
		if ( spend <= 0 || !core.Wallet.TrySpend( spend ) ) return false;

		var missingHp = building.MaxHealth - building.Health;
		var targetHp = spend >= fullCost - 0.001
			? building.MaxHealth
			: building.Health + (float)(missingHp * (spend / fullCost));

		Enqueue( RepairJob.ForBuilding( building, spend, building.Health, targetHp ) );
		core.ShowToast( spend >= fullCost - 0.001 ? "Repairs underway…" : "Partial repair underway…" );
		Sfx.Play( Sfx.Purchase );
		core.SaveManagerTouch();
		return true;
	}

	public bool TryRepairWall( WallSegment wall )
	{
		var core = GameCore.Instance;
		if ( core is null || core.Phase != GamePhase.Day || wall is null ) return false;
		if ( wall.Health >= wall.MaxHealth || IsScheduled( wall ) ) return false;

		var missingHp = wall.MaxHealth - wall.Health;
		var fullCost = missingHp * GameConstants.RepairCostPerHp;
		var spend = SelectHelp.AffordableRepairSpend( fullCost );
		if ( spend <= 0 || !core.Wallet.TrySpend( spend ) ) return false;

		var targetHp = spend >= fullCost - 0.001
			? wall.MaxHealth
			: wall.Health + (float)(missingHp * (spend / fullCost));

		Enqueue( RepairJob.ForWall( wall, spend, wall.Health, targetHp ) );
		core.ShowToast( spend >= fullCost - 0.001 ? "Repairs underway…" : "Partial repair underway…" );
		Sfx.Play( Sfx.Purchase );
		core.SaveManagerTouch();
		return true;
	}

	public bool TryRepairCore()
	{
		var core = GameCore.Instance;
		var outpost = OutpostManager.Instance;
		if ( core is null || outpost is null || core.Phase != GamePhase.Day ) return false;
		if ( outpost.CoreHealth >= outpost.CoreMaxHealth || _coreScheduled ) return false;

		var missingHp = outpost.CoreMaxHealth - outpost.CoreHealth;
		var fullCost = missingHp * GameConstants.RepairCostPerHp;
		var spend = SelectHelp.AffordableRepairSpend( fullCost );
		if ( spend <= 0 || !core.Wallet.TrySpend( spend ) ) return false;

		var targetHp = spend >= fullCost - 0.001
			? outpost.CoreMaxHealth
			: outpost.CoreHealth + (float)(missingHp * (spend / fullCost));

		Enqueue( RepairJob.ForCore( spend, outpost.CoreHealth, targetHp ) );
		core.ShowToast( spend >= fullCost - 0.001 ? "Repairs underway…" : "Partial repair underway…" );
		Sfx.Play( Sfx.Purchase );
		core.SaveManagerTouch();
		return true;
	}

	public bool TryRepairAll( IEnumerable<PlacedBuilding> buildings ) =>
		EnqueueDamagedRepairs( buildings, requirePayment: true );

	/// <summary>Repairmen queue damaged structures for free — same timed jobs as paid repairs.</summary>
	public bool TryEnqueueFreeRepairs( IEnumerable<PlacedBuilding> buildings ) =>
		EnqueueDamagedRepairs( buildings, requirePayment: false );

	private bool EnqueueDamagedRepairs( IEnumerable<PlacedBuilding> buildings, bool requirePayment )
	{
		var core = GameCore.Instance;
		var outpost = OutpostManager.Instance;
		if ( core is null || core.Phase != GamePhase.Day ) return false;

		var pending = new List<(RepairTargetKind Kind, PlacedBuilding Building, WallSegment Wall, float Start, float Full, double Cost)>();

		if ( outpost is not null && outpost.CoreHealth < outpost.CoreMaxHealth && !_coreScheduled )
		{
			var missing = outpost.CoreMaxHealth - outpost.CoreHealth;
			pending.Add( (RepairTargetKind.Core, null, null, outpost.CoreHealth, outpost.CoreMaxHealth,
				missing * GameConstants.RepairCostPerHp) );
		}

		if ( outpost is not null )
		{
			foreach ( var w in outpost.Walls )
			{
				if ( w.Health >= w.MaxHealth || IsScheduled( w ) ) continue;
				var missing = w.MaxHealth - w.Health;
				pending.Add( (RepairTargetKind.Wall, null, w, w.Health, w.MaxHealth,
					missing * GameConstants.RepairCostPerHp) );
			}
		}

		foreach ( var b in buildings )
		{
			if ( b is null || b.IsDestroyed || b.Health >= b.MaxHealth || IsScheduled( b ) ) continue;
			pending.Add( (RepairTargetKind.Building, b, null, b.Health, b.MaxHealth, b.MissingRepairCost()) );
		}

		if ( pending.Count == 0 )
			return false;

		var budget = requirePayment ? core.Wallet.Scrap : double.PositiveInfinity;
		if ( requirePayment && budget <= 0 )
			return false;

		var queued = 0;
		var spent = 0.0;
		var anyPartial = false;

		foreach ( var item in pending )
		{
			if ( budget <= 0.001 ) break;
			if ( item.Cost <= 0 ) continue;

			double pieceSpend;
			float targetHp;
			if ( !requirePayment || budget + 0.001 >= item.Cost )
			{
				pieceSpend = item.Cost;
				targetHp = item.Full;
			}
			else
			{
				pieceSpend = budget;
				var missing = item.Full - item.Start;
				targetHp = item.Start + (float)(missing * (pieceSpend / item.Cost));
				anyPartial = true;
			}

			if ( requirePayment && !core.Wallet.TrySpend( pieceSpend ) )
				break;

			budget -= pieceSpend;
			spent += pieceSpend;

			var label = item.Kind switch
			{
				RepairTargetKind.Core => "Command Post",
				RepairTargetKind.Wall => WallLabelFor( item.Wall ),
				RepairTargetKind.Building when item.Building is not null => item.Building.Def.Name,
				_ => "Repair"
			};

			var target = item.Kind switch
			{
				RepairTargetKind.Core => RepairTarget.ForCore( item.Start, targetHp ),
				RepairTargetKind.Wall => RepairTarget.ForWall( item.Wall, item.Start, targetHp ),
				_ => RepairTarget.ForBuilding( item.Building, item.Start, targetHp )
			};

			Enqueue( new RepairJob( new List<RepairTarget> { target }, pieceSpend, label ) );
			queued++;
		}

		if ( queued == 0 )
			return false;

		if ( requirePayment )
		{
			core.ShowToast( anyPartial || spent + 0.001 < pending.Sum( p => p.Cost )
				? "Partial repairs underway…"
				: "Repairs underway…" );
			Sfx.Play( Sfx.Purchase );
		}

		core.SaveManagerTouch();
		return true;
	}

	public void Tick( float dt )
	{
		if ( dt <= 0f ) return;

		if ( _active is null && _queue.Count > 0 )
			_active = _queue.Dequeue();

		if ( _active is null ) return;

		var speed = GameCore.Instance?.IsCure == true
			? TeamBonuses.RepairSpeedMult( GameCore.Instance )
			: 1f;
		_active.Progress = MathF.Min( 1f, _active.Progress + dt * speed / _active.Duration );
		_active.Apply();

		if ( _active.Progress < 1f ) return;

		_active.Complete();
		ReleaseJob( _active );
		_active = null;
		GameCore.Instance?.SaveManagerTouch();
	}

	public void Clear()
	{
		if ( _active is not null )
			ReleaseJob( _active );

		_queue.Clear();
		_active = null;
	}

	public void CancelBuilding( PlacedBuilding building )
	{
		if ( building is null ) return;

		_buildings.Remove( building );

		if ( _active?.Contains( building, null, false ) == true )
		{
			ReleaseJob( _active );
			_active = null;
		}

		if ( _queue.Count == 0 ) return;

		var kept = new Queue<RepairJob>();
		while ( _queue.Count > 0 )
		{
			var job = _queue.Dequeue();
			if ( job.Contains( building, null, false ) )
				ReleaseJob( job );
			else
				kept.Enqueue( job );
		}

		while ( kept.Count > 0 )
			_queue.Enqueue( kept.Dequeue() );
	}

	private void Enqueue( RepairJob job )
	{
		ReserveJob( job );
		_queue.Enqueue( job );
	}

	private void ReserveJob( RepairJob job )
	{
		foreach ( var t in job.Targets )
		{
			switch ( t.Kind )
			{
				case RepairTargetKind.Building when t.Building is not null:
					_buildings.Add( t.Building );
					break;
				case RepairTargetKind.Wall when t.Wall is not null:
					_walls.Add( t.Wall );
					break;
				case RepairTargetKind.Core:
					_coreScheduled = true;
					break;
			}
		}
	}

	private void ReleaseJob( RepairJob job )
	{
		foreach ( var t in job.Targets )
		{
			switch ( t.Kind )
			{
				case RepairTargetKind.Building when t.Building is not null:
					_buildings.Remove( t.Building );
					break;
				case RepairTargetKind.Wall when t.Wall is not null:
					_walls.Remove( t.Wall );
					break;
				case RepairTargetKind.Core:
					_coreScheduled = false;
					break;
			}
		}
	}

	private float ProgressForTarget( PlacedBuilding building, WallSegment wall, bool core )
	{
		if ( _active is not null && _active.Contains( building, wall, core ) )
			return _active.Progress;

		foreach ( var job in _queue )
		{
			if ( job.Contains( building, wall, core ) )
				return 0f;
		}

		return 0f;
	}

	private enum RepairTargetKind { Core, Wall, Building }

	private sealed class RepairTarget
	{
		public RepairTargetKind Kind;
		public float StartHealth;
		public float TargetHealth;
		public PlacedBuilding Building;
		public WallSegment Wall;

		public static RepairTarget ForCore( float start, float target ) => new()
		{
			Kind = RepairTargetKind.Core,
			StartHealth = start,
			TargetHealth = target
		};

		public static RepairTarget ForWall( WallSegment wall, float start, float target ) => new()
		{
			Kind = RepairTargetKind.Wall,
			Wall = wall,
			StartHealth = start,
			TargetHealth = target
		};

		public static RepairTarget ForBuilding( PlacedBuilding building, float start, float target ) => new()
		{
			Kind = RepairTargetKind.Building,
			Building = building,
			StartHealth = start,
			TargetHealth = target
		};

		public void Apply( float t )
		{
			var hp = StartHealth + (TargetHealth - StartHealth) * t;

			switch ( Kind )
			{
				case RepairTargetKind.Core:
					OutpostManager.Instance?.SetCoreHealth( hp );
					break;
				case RepairTargetKind.Wall when Wall is not null:
					Wall.SetHealth( hp );
					break;
				case RepairTargetKind.Building when Building is not null && !Building.IsDestroyed:
					Building.SetHealth( hp );
					break;
			}
		}

		public void Complete()
		{
			// Snap to the paid target HP — not always max (supports partial repairs).
			Apply( 1f );
		}

		public bool Matches( PlacedBuilding building, WallSegment wall, bool core )
		{
			return Kind switch
			{
				RepairTargetKind.Building => building is not null && Building == building,
				RepairTargetKind.Wall => wall is not null && Wall == wall,
				RepairTargetKind.Core => core,
				_ => false
			};
		}
	}

	private sealed class RepairJob
	{
		public string Label { get; }
		public float Duration { get; }
		public float Progress { get; set; }
		public List<RepairTarget> Targets { get; }

		public RepairJob( List<RepairTarget> targets, double cost, string label )
		{
			Targets = targets;
			Label = label;
			var repairBoost = WorkerManager.Instance?.RepairSpeedContributors() ?? 0;
			Duration = GameConstants.RepairDurationForCost( cost, repairBoost );
		}

		public static RepairJob ForBuilding( PlacedBuilding building, double cost, float startHp, float targetHp ) =>
			new( new List<RepairTarget> { RepairTarget.ForBuilding( building, startHp, targetHp ) }, cost, building.Def.Name );

		public static RepairJob ForWall( WallSegment wall, double cost, float startHp, float targetHp ) =>
			new( new List<RepairTarget> { RepairTarget.ForWall( wall, startHp, targetHp ) }, cost, WallLabelFor( wall ) );

		public static RepairJob ForCore( double cost, float startHp, float targetHp ) =>
			new( new List<RepairTarget> { RepairTarget.ForCore( startHp, targetHp ) }, cost, "Command Post" );

		public void Apply()
		{
			foreach ( var t in Targets )
				t.Apply( Progress );
		}

		public void Complete()
		{
			foreach ( var t in Targets )
				t.Complete();
		}

		public bool Contains( PlacedBuilding building, WallSegment wall, bool core ) =>
			Targets.Any( t => t.Matches( building, wall, core ) );
	}

	private static string WallLabelFor( WallSegment wall )
	{
		var outpost = OutpostManager.Instance;
		if ( outpost is null || wall is null ) return "Wall";

		var idx = 0;
		foreach ( var w in outpost.Walls )
		{
			if ( w.Health >= w.MaxHealth ) continue;
			idx++;
			if ( ReferenceEquals( w, wall ) )
				return WallLabel( idx );
		}

		return "Wall";
	}
}
