namespace FinalOutpost;

public enum WorkerRole
{
	Forager,   // harvests the resource of its assigned plot
	Craftsman, // converts stored resources into scrap
	Repairman  // slowly repairs the most-damaged base structure
}

public static class WorkerInfo
{
	public static readonly WorkerRole[] Order = { WorkerRole.Forager, WorkerRole.Craftsman, WorkerRole.Repairman };

	public static WorkerRole Parse( string s ) => Enum.TryParse<WorkerRole>( s, out var r ) ? r : WorkerRole.Forager;

	public static string Name( WorkerRole r ) => r switch
	{
		WorkerRole.Forager => "Forager",
		WorkerRole.Craftsman => "Craftsman",
		WorkerRole.Repairman => "Repairman",
		_ => "Worker"
	};

	public static string Icon( WorkerRole r ) => r switch
	{
		WorkerRole.Forager => "forest",
		WorkerRole.Craftsman => "handyman",
		WorkerRole.Repairman => "construction",
		_ => "person"
	};

	public static string Blurb( WorkerRole r ) => r switch
	{
		WorkerRole.Forager => "Harvests a claimed resource plot.",
		WorkerRole.Craftsman => "Turns stored resources into scrap.",
		WorkerRole.Repairman => "Queues and completes repairs during the day. Speeds up the repair queue.",
		_ => ""
	};

	public static Color Tint( WorkerRole r ) => r switch
	{
		WorkerRole.Forager => new Color( 0.45f, 0.7f, 0.4f ),
		WorkerRole.Craftsman => new Color( 0.82f, 0.68f, 0.4f ),
		WorkerRole.Repairman => new Color( 0.5f, 0.62f, 0.85f ),
		_ => Color.White
	};

	public static double HireCost( WorkerRole r ) => r switch
	{
		WorkerRole.Forager => GameConstants.WorkerHireCost,
		WorkerRole.Craftsman => GameConstants.WorkerHireCost * 1.4,
		WorkerRole.Repairman => GameConstants.WorkerHireCost * 1.6,
		_ => GameConstants.WorkerHireCost
	};

	public static int UnlockNight( WorkerRole r ) => NightUnlocks.WorkerUnlockNight( r );
}

/// <summary>
/// Manages hired non-combat workers. Foragers are bound to a claimed resource plot; craftsmen and
/// repairmen operate from the home base. Workers roam their work area (so the base feels alive) and
/// tick their production/repair jobs every frame.
/// </summary>
public sealed class WorkerManager : Component
{
	public static WorkerManager Instance { get; private set; }

	private readonly List<WorkerUnit> _units = new();

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
		Clear();
	}

	public IReadOnlyList<WorkerUnit> Units => _units;
	public int Count => _units.Count;

	public int CountRole( WorkerRole role )
	{
		var n = 0;
		foreach ( var u in _units ) if ( u.Role == role ) n++;
		return n;
	}

	public bool HasLivingRepairman()
	{
		foreach ( var u in _units )
			if ( u.Role == WorkerRole.Repairman && u.Go.IsValid() )
				return true;
		return false;
	}

	/// <summary>At day start, repairmen queue free timed repairs for anything damaged.</summary>
	public void TryAutoRepairOnDayStart()
	{
		if ( !HasLivingRepairman() ) return;

		var build = BuildManager.Instance;
		if ( build is null || build.RepairAllCost() <= 0 ) return;

		RepairManager.Instance?.TryEnqueueFreeRepairs( build.Buildings );
	}

	public int CountForagersOn( int px, int py )
	{
		var n = 0;
		foreach ( var u in _units )
			if ( u.Role == WorkerRole.Forager && u.PlotX == px && u.PlotY == py ) n++;
		return n;
	}

	public bool TryHireForager( int px, int py )
	{
		var plots = PlotManager.Instance;
		if ( plots is null || !plots.IsOwned( px, py ) ) return false;
		if ( PlotGrid.ResourceAt( px, py ) == ResourceKind.None ) return false;
		return HireInternal( WorkerRole.Forager, px, py );
	}

	public bool TryHire( WorkerRole role ) => HireInternal( role, int.MinValue, int.MinValue );

	private bool HireInternal( WorkerRole role, int px, int py )
	{
		var core = GameCore.Instance;
		if ( core is null || core.Phase != GamePhase.Day ) return false;
		if ( Count >= GameConstants.MaxWorkers ) return false;
		if ( !NightUnlocks.IsWorkerUnlocked( core.Save, role ) ) return false;
		if ( !core.Wallet.TrySpend( WorkerInfo.HireCost( role ) ) ) return false;

		core.Save.Workers.Add( new SavedWorker { Role = role.ToString(), PlotX = px, PlotY = py } );
		RespawnAll();
		if ( role == WorkerRole.Repairman )
			TryAutoRepairOnDayStart();
		Sfx.Play( Sfx.Purchase );
		core.SaveManagerTouch();
		return true;
	}

	public bool Dismiss( WorkerUnit unit )
	{
		var core = GameCore.Instance;
		if ( core is null || unit is null ) return false;

		var idx = _units.IndexOf( unit );
		if ( idx < 0 || idx >= core.Save.Workers.Count ) return false;

		core.Save.Workers.RemoveAt( idx );
		core.Wallet.Earn( WorkerInfo.HireCost( unit.Role ) * GameConstants.SellRefundFraction, applyIncomeScale: false );

		if ( BuildManager.Instance?.Selected is WorkerSelectable sel && sel.Wraps( unit ) )
			BuildManager.Instance.Deselect();

		RespawnAll();
		core.SaveManagerTouch();
		return true;
	}

	public bool RecallForager( int px, int py )
	{
		foreach ( var u in _units )
			if ( u.Role == WorkerRole.Forager && u.PlotX == px && u.PlotY == py )
				return Dismiss( u );
		return false;
	}

	public void RebuildFromSave() => RespawnAll();

	protected override void OnUpdate()
	{
		var core = GameCore.Instance;
		if ( core is null ) return;
		if ( core.Phase != GamePhase.Day && core.Phase != GamePhase.Night ) return;

		var dt = Time.Delta;
		foreach ( var u in _units )
			u.Tick( dt, core );
	}

	private void RespawnAll()
	{
		Clear();
		var save = GameCore.Instance?.Save;
		if ( save is null ) return;

		// Keep list order aligned 1:1 with save.Workers for stable dismiss-by-index.
		foreach ( var sw in save.Workers )
			SpawnUnit( sw );
	}

	private void SpawnUnit( SavedWorker sw )
	{
		var role = WorkerInfo.Parse( sw.Role );
		var home = ResolveHome( role, sw );

		var go = new GameObject( true, $"Worker_{role}" );
		go.WorldPosition = home.WithZ( OutpostTerrain.SampleHeight( home.x, home.y ) );

		var character = go.Components.Create<CharacterModel>();
		character.Setup( WorkerInfo.Tint( role ), null, Sandbox.Citizen.CitizenAnimationHelper.HoldTypes.None );

		var unit = new WorkerUnit
		{
			Go = go,
			Character = character,
			Role = role,
			PlotX = sw.PlotX,
			PlotY = sw.PlotY
		};
		character.Tick( Vector3.Zero, Rotation.Identity );
		_units.Add( unit );
	}

	private static Vector3 ResolveHome( WorkerRole role, SavedWorker sw )
	{
		if ( role == WorkerRole.Forager && sw.HasPlot && PlotGrid.InGrid( sw.PlotX, sw.PlotY ) )
			return PlotGrid.CenterWorld( sw.PlotX, sw.PlotY );

		// Base-bound workers loiter just outside the command post.
		return new Vector3( 0f, GameConstants.ArenaHalf * 0.45f, 0f );
	}

	private void Clear()
	{
		foreach ( var u in _units ) u.Go?.Destroy();
		_units.Clear();
	}

	/// <summary>One hired worker in the world.</summary>
	public sealed class WorkerUnit
	{
		public GameObject Go;
		public CharacterModel Character;
		public WorkerRole Role;
		public int PlotX = int.MinValue;
		public int PlotY = int.MinValue;

		private Rotation _aim = Rotation.Identity;
		private Vector3 _waypoint;
		private bool _hasWaypoint;
		private float _repathTimer;
		private double _harvestAccum;
		private double _scrapAccum;

		public Vector3 WorldPos => Go.IsValid() ? Go.WorldPosition : Vector3.Zero;

		public bool HasPlot => PlotX != int.MinValue && PlotY != int.MinValue;

		public ResourceKind PlotResource => HasPlot ? PlotGrid.ResourceAt( PlotX, PlotY ) : ResourceKind.None;

		public bool IsWorking
		{
			get
			{
				if ( Role == WorkerRole.Forager )
					return HasPlot && PlotManager.Instance?.IsOwned( PlotX, PlotY ) == true
						&& PlotResource != ResourceKind.None
						&& PlotManager.Instance?.IsCleared( PlotX, PlotY ) != true;
				return true;
			}
		}

		public void Tick( float dt, GameCore core )
		{
			if ( !Go.IsValid() ) return;

			// Movement: forager roams its plot; others roam near the base.
			var (center, radius) = RoamArea();
			Wander( dt, center, radius, GameConstants.WorkerMoveSpeed );

			// Job.
			switch ( Role )
			{
				case WorkerRole.Forager: DoForage( dt, core ); break;
				case WorkerRole.Craftsman: DoCraft( dt, core ); break;
				case WorkerRole.Repairman:
					if ( core.Phase == GamePhase.Day )
						DoRepair( dt );
					break;
			}
		}

		private (Vector3 center, float radius) RoamArea()
		{
			if ( Role == WorkerRole.Forager && HasPlot )
				return (PlotGrid.CenterWorld( PlotX, PlotY ), GameConstants.PlotSize * 0.32f);

			return (Vector3.Zero, GameConstants.ArenaHalf * 0.7f);
		}

		private void DoForage( float dt, GameCore core )
		{
			if ( !IsWorking ) return;

			var rate = GameConstants.ForagerHarvestPerSec * dt;
			if ( core.IsCure )
			{
				rate *= TeamBonuses.ForagerYieldMult( core );
				var sickness = core.Save.ColonySickness / CureConstants.MaxSickness;
				rate *= MathF.Max( 0.4f, 1f - sickness * CureConstants.SicknessWorkerPenalty * 100f );
			}
			_harvestAccum += rate;
			if ( _harvestAccum >= 1.0 )
			{
				var whole = Math.Floor( _harvestAccum );
				_harvestAccum -= whole;
				core.Resources.Add( PlotResource, whole );
			}
		}

		private void DoCraft( float dt, GameCore core )
		{
			var want = GameConstants.CraftsmanConvertPerSec * dt;
			var (kind, taken) = core.Resources.DrainRichest( want );
			if ( kind == ResourceKind.None || taken <= 0 ) return;

			_scrapAccum += taken * GameConstants.CraftsmanScrapPerResource;
			if ( _scrapAccum >= 1.0 )
			{
				var whole = Math.Floor( _scrapAccum );
				_scrapAccum -= whole;
				core.Wallet.Earn( whole );
			}
		}

		private void DoRepair( float dt )
		{
			_ = dt;
			var build = BuildManager.Instance;
			if ( build is null || build.RepairAllCost() <= 0 ) return;

			RepairManager.Instance?.TryEnqueueFreeRepairs( build.Buildings );
		}

		private void Wander( float dt, Vector3 center, float radius, float speed )
		{
			var pos = Go.WorldPosition;
			_repathTimer -= dt;

			if ( !_hasWaypoint || _repathTimer <= 0f
			     || (_waypoint - pos).WithZ( 0f ).Length <= GameConstants.DefenderHomeDeadzone )
				PickWaypoint( center, radius );

			MoveToward( _waypoint, dt, speed );
		}

		private void PickWaypoint( Vector3 center, float radius )
		{
			var angle = Game.Random.Float( 0f, MathF.PI * 2f );
			var r = Game.Random.Float( radius * 0.15f, radius );
			_waypoint = new Vector3( center.x + MathF.Cos( angle ) * r, center.y + MathF.Sin( angle ) * r, 0f );
			_hasWaypoint = true;
			_repathTimer = Game.Random.Float( 2.5f, 5.5f );
		}

		private void MoveToward( Vector3 targetXY, float dt, float speed )
		{
			var pos = Go.WorldPosition;
			var to = (targetXY - pos).WithZ( 0f );
			var dist = to.Length;

			if ( dist < 1f )
			{
				Character?.Tick( Vector3.Zero, _aim );
				return;
			}

			var dir = to / dist;
			_aim = Rotation.LookAt( dir );

			var step = MathF.Min( dist, speed * dt );
			var next = pos + dir * step;
			next.z = OutpostTerrain.SampleHeight( next.x, next.y );
			Go.WorldPosition = next;
			Go.WorldRotation = _aim;

			Character?.Tick( dir * speed, _aim );
		}
	}
}
