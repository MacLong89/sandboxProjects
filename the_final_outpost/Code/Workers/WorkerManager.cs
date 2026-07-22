namespace FinalOutpost;

public enum WorkerRole
{
	Forager,   // clears plots; Survival scrap / Cure materials
	Craftsman, // legacy: converts stored materials into scrap
	Repairman, // slowly repairs the most-damaged base structure (Survival)
	Farmer,    // steady food income (Cure)
	Scholar,   // knowledge for tech tree + lab boost (Cure)
	Operator,  // supplies and repair queue speed (Cure)
	Medic,     // heals injured recruits (Cure)
	Merchant   // steady scrap income (Cure)
}

public static class WorkerInfo
{
	public static readonly WorkerRole[] SurvivalRoles = { WorkerRole.Repairman };
	public static readonly WorkerRole[] CureRoles = { WorkerRole.Farmer, WorkerRole.Scholar, WorkerRole.Operator, WorkerRole.Medic, WorkerRole.Merchant };
	public static readonly WorkerRole[] Order = { WorkerRole.Forager, WorkerRole.Craftsman, WorkerRole.Repairman, WorkerRole.Farmer, WorkerRole.Scholar, WorkerRole.Operator, WorkerRole.Medic, WorkerRole.Merchant };

	public static WorkerRole[] RolesForMode( bool isCure ) => isCure ? CureRoles : SurvivalRoles;

	public static WorkerRole Parse( string s ) => Enum.TryParse<WorkerRole>( s, out var r ) ? r : WorkerRole.Forager;

	public static string Name( WorkerRole r ) => r switch
	{
		WorkerRole.Forager => "Forager",
		WorkerRole.Craftsman => "Craftsman",
		WorkerRole.Repairman => "Repairman",
		WorkerRole.Farmer => "Farmer",
		WorkerRole.Scholar => "Scholar",
		WorkerRole.Operator => "Operator",
		WorkerRole.Medic => "Medic",
		WorkerRole.Merchant => "Merchant",
		_ => "Worker"
	};

	public static string Icon( WorkerRole r ) => r switch
	{
		WorkerRole.Forager => "forest",
		WorkerRole.Craftsman => "handyman",
		WorkerRole.Repairman => "construction",
		WorkerRole.Farmer => "agriculture",
		WorkerRole.Scholar => "menu_book",
		WorkerRole.Operator => "precision_manufacturing",
		WorkerRole.Medic => "medical_services",
		WorkerRole.Merchant => "storefront",
		_ => "person"
	};

	public static string Blurb( WorkerRole r ) => r switch
	{
		WorkerRole.Forager => GameCore.Instance?.IsCure == true
			? "Harvests a claimed resource plot."
			: $"Clears land and earns +{GameConstants.ForagerScrapPerSec:0.#} scrap/s.",
		WorkerRole.Craftsman => "Turns stored resources into scrap.",
		WorkerRole.Repairman => "Queues and completes repairs during the day. Speeds up the repair queue.",
		WorkerRole.Farmer => $"+{CureConstants.FarmerFoodPerSec:0.0} food/s.",
		WorkerRole.Scholar => $"+{CureConstants.ScholarKnowledgePerSec:0.0} knowledge/s · boosts lab output.",
		WorkerRole.Operator => $"+{CureConstants.OperatorSuppliesPerSec:0.0} supplies/s · speeds repairs.",
		WorkerRole.Medic => $"Heals injured recruits ({CureConstants.MedicRecruitHealPerSec:0.0} HP/s nearby).",
		WorkerRole.Merchant => $"+{CureConstants.MerchantScrapPerSec:0.0} scrap/s.",
		_ => ""
	};

	public static Color Tint( WorkerRole r ) => r switch
	{
		WorkerRole.Forager => new Color( 0.45f, 0.7f, 0.4f ),
		WorkerRole.Craftsman => new Color( 0.82f, 0.68f, 0.4f ),
		WorkerRole.Repairman => new Color( 0.5f, 0.62f, 0.85f ),
		WorkerRole.Farmer => new Color( 0.42f, 0.78f, 0.38f ),
		WorkerRole.Scholar => new Color( 0.55f, 0.72f, 0.95f ),
		WorkerRole.Operator => new Color( 0.72f, 0.58f, 0.42f ),
		WorkerRole.Medic => new Color( 0.92f, 0.95f, 0.98f ),
		WorkerRole.Merchant => new Color( 0.85f, 0.62f, 0.32f ),
		_ => Color.White
	};

	public static double HireCost( WorkerRole r ) => r switch
	{
		WorkerRole.Forager => GameConstants.WorkerHireCost,
		WorkerRole.Craftsman => GameConstants.WorkerHireCost * 1.4,
		WorkerRole.Repairman => GameConstants.WorkerHireCost * 1.6,
		WorkerRole.Farmer => GameConstants.WorkerHireCost * 1.1,
		WorkerRole.Scholar => GameConstants.WorkerHireCost * 1.35,
		WorkerRole.Operator => GameConstants.WorkerHireCost * 1.5,
		WorkerRole.Medic => GameConstants.WorkerHireCost * 1.55,
		WorkerRole.Merchant => GameConstants.WorkerHireCost * 1.45,
		_ => GameConstants.WorkerHireCost
	};

	public static int UnlockNight( WorkerRole r ) => NightUnlocks.WorkerUnlockNight( r );

	public static bool IsRepairSpecialist( WorkerRole role ) =>
		role is WorkerRole.Repairman or WorkerRole.Operator;
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

	public int RepairSpeedContributors()
	{
		var factories = 0;
		foreach ( var b in BuildManager.Instance?.Buildings ?? Array.Empty<PlacedBuilding>() )
			if ( !b.IsDestroyed && b.Type == BuildableId.Factory ) factories++;

		var n = factories / 2;
		foreach ( var u in _units )
			if ( WorkerInfo.IsRepairSpecialist( u.Role ) ) n++;
		return n;
	}

	public bool HasLivingRepairman()
	{
		foreach ( var u in _units )
			if ( WorkerInfo.IsRepairSpecialist( u.Role ) && u.Go.IsValid() )
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
		if ( PlotGrid.HarvestResourceAt( px, py ) == ResourceKind.None ) return false;
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
		if ( WorkerInfo.IsRepairSpecialist( role ) )
			TryAutoRepairOnDayStart();
		KnowledgeGain.OnWorkerHired( core );
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

		DestructionFx.Burst( unit.WorldPos, 0.33f );

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

	public void SyncForagerPlot( WorkerUnit unit )
	{
		var core = GameCore.Instance;
		if ( core?.Save is null || unit?.Role != WorkerRole.Forager ) return;

		var idx = _units.IndexOf( unit );
		if ( idx < 0 || idx >= core.Save.Workers.Count ) return;

		core.Save.Workers[idx].PlotX = unit.PlotX;
		core.Save.Workers[idx].PlotY = unit.PlotY;
		core.SaveManagerTouch();
	}

	public void RebuildFromSave() => RespawnAll();

	protected override void OnUpdate()
	{
		var core = GameCore.Instance;
		if ( core is null ) return;
		if ( core.IsUiBlocking ) return;
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
		if ( BuildingCollision.BlocksUnit( go.WorldPosition ) )
		{
			var (center, radius) = role == WorkerRole.Forager && sw.HasPlot && PlotGrid.InGrid( sw.PlotX, sw.PlotY )
				? (PlotGrid.CenterWorld( sw.PlotX, sw.PlotY ), GameConstants.PlotSize * 0.32f)
				: (Vector3.Zero, GameConstants.ArenaHalf * 0.45f);
			if ( BuildingCollision.TryFindClearPoint( center, 0f, radius, out var clear )
			     || BuildingCollision.TryEscape( center, out clear ) )
				go.WorldPosition = clear.WithZ( OutpostTerrain.SampleHeight( clear.x, clear.y ) );
		}

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
		var hq = OutpostManager.Instance?.CorePosition ?? Vector3.Zero;
		return WallApproach.ClampInsideCourtyard(
			hq + new Vector3( 0f, GameConstants.CellSize * 3f, 0f ),
			WallApproach.RingCenter );
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

		private UnitLocomotion.WanderState _wander;
		private UnitLocomotion.SteerState _steer;
		private float _moveStuckTimer;
		private Rotation _aim = Rotation.Identity;
		private double _harvestAccum;
		private double _scrapAccum;
		private UnitOrderKind _orderKind = UnitOrderKind.None;
		private Vector3 _orderTarget;
		private bool _manualMove;

		public Vector3 WorldPos => Go.IsValid() ? Go.WorldPosition : Vector3.Zero;

		public void SetMoveOrder( Vector3 ground )
		{
			_orderKind = UnitOrderKind.Move;
			_orderTarget = ground.WithZ( 0f );
			_manualMove = true;
			_wander.HasWaypoint = false;
		}

		public void SetGatherOrder( int plotX, int plotY )
		{
			PlotX = plotX;
			PlotY = plotY;
			_orderKind = UnitOrderKind.Gather;
			_manualMove = false;
			_wander.HasWaypoint = false;
			WorkerManager.Instance?.SyncForagerPlot( this );
		}

		public void ClearOrder()
		{
			_orderKind = UnitOrderKind.None;
			_manualMove = false;
		}

		public bool HasPlot => PlotX != int.MinValue && PlotY != int.MinValue;

		public ResourceKind PlotResource => HasPlot ? PlotGrid.HarvestResourceAt( PlotX, PlotY ) : ResourceKind.None;

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

			var speed = GameConstants.WorkerMoveSpeed;

			if ( _orderKind == UnitOrderKind.Move && _manualMove )
			{
				UnitLocomotion.MoveHumanoid( Go, _orderTarget, dt, speed, ref _aim, Character, ref _moveStuckTimer, ref _steer );
				if ( (_orderTarget - Go.WorldPosition).WithZ( 0f ).Length <= UnitLocomotion.ArrivalDistance )
				{
					_orderKind = UnitOrderKind.None;
					_manualMove = false;
				}
			}
			else
			{
				var (center, radius) = RoamArea();
				UnitLocomotion.TickWander( ref _wander, Go, center, radius * 0.15f, radius, dt, speed, ref _aim, Character );
			}

			// Job.
			switch ( Role )
			{
				case WorkerRole.Forager: DoForage( dt, core ); break;
				case WorkerRole.Craftsman: DoCraft( dt, core ); break;
				case WorkerRole.Repairman:
					if ( core.Phase == GamePhase.Day )
						DoRepair( dt );
					break;
				case WorkerRole.Farmer: DoFarm( dt, core ); break;
				case WorkerRole.Scholar: DoStudy( dt, core ); break;
				case WorkerRole.Operator:
					DoOperate( dt, core );
					if ( core.Phase == GamePhase.Day )
						DoRepair( dt );
					break;
				case WorkerRole.Medic: DoMedic( dt, core ); break;
				case WorkerRole.Merchant: DoTrade( dt, core ); break;
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

			if ( !core.IsCure )
			{
				_scrapAccum += GameConstants.ForagerScrapPerSec * dt;
				if ( _scrapAccum >= 1.0 )
				{
					var whole = Math.Floor( _scrapAccum );
					_scrapAccum -= whole;
					core.Wallet.Earn( whole );
				}
				return;
			}

			var rate = GameConstants.ForagerHarvestPerSec * dt;
			rate *= TeamBonuses.ForagerYieldMult( core );
			rate *= PlotBoosts.ForagerMult( core.Save );
			var sickness = core.Save.ColonySickness / CureConstants.MaxSickness;
			rate *= MathF.Max( 0.4f, 1f - sickness * CureConstants.SicknessWorkerPenalty * 100f );
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
			// Survival no longer hires craftsmen; keep conversion for leftover stock on old saves.
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

		private void DoFarm( float dt, GameCore core )
		{
			if ( !core.IsCure ) return;
			if ( !TechTreeCatalog.IsUnlocked( core.Save, "agriculture" ) ) return;
			core.Resources.Add( ResourceKind.Food, CureConstants.FarmerFoodPerSec * dt );
		}

		private void DoStudy( float dt, GameCore core )
		{
			if ( !core.IsCure ) return;
			var mult = TechTreeCatalog.IsUnlocked( core.Save, "synthesis" ) ? 1.25f : 1f;
			core.Resources.Add( ResourceKind.Knowledge, CureConstants.ScholarKnowledgePerSec * dt * mult );
		}

		private void DoOperate( float dt, GameCore core )
		{
			if ( !core.IsCure ) return;
			core.Resources.Add( ResourceKind.Supplies, CureConstants.OperatorSuppliesPerSec * dt );
		}

		private void DoMedic( float dt, GameCore core )
		{
			if ( !core.IsCure ) return;
			var heal = CureConstants.MedicRecruitHealPerSec * dt;
			if ( heal <= 0f ) return;
			DefenderManager.Instance?.HealInRadius( Go.WorldPosition, CureConstants.MedicHealRadius, heal );
		}

		private void DoTrade( float dt, GameCore core )
		{
			if ( !core.IsCure ) return;
			_scrapAccum += CureConstants.MerchantScrapPerSec * dt;
			if ( _scrapAccum >= 0.5 )
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
	}
}
