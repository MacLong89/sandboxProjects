namespace FinalOutpost;

/// <summary>Handles grid placement, selection, upgrades, repairs, and persistence of buildings.</summary>
public sealed class BuildManager : Component
{
	public static BuildManager Instance { get; private set; }

	public ISelectable Selected { get; private set; }
	public BuildableId? PlacementMode { get; private set; }
	public PlacedBuilding MovingBuilding { get; private set; }
	public bool MovingCore { get; private set; }

	private readonly Dictionary<(int x, int y), PlacedBuilding> _cells = new();
	private int _nextPlaceOrder;
	private GameObject _ghostGo;
	private List<(ModelRenderer Renderer, Color Base)> _ghostParts = new();
	private BuildableId? _ghostBuiltType;
	private bool _ghostIsCore;
	private int _ghostCellX = int.MinValue;
	private int _ghostCellY = int.MinValue;
	/// <summary>Placement facing in 90° steps (0–3). R advances while placing / moving.</summary>
	private int _placementYawSteps;
	private bool _placementYawManual;

	public IReadOnlyCollection<PlacedBuilding> Buildings => _cells.Values;

	public int BarracksCount => CountAlive( BuildableId.Barracks );

	public int CountAlive( BuildableId type )
	{
		var n = 0;
		foreach ( var b in _cells.Values )
			if ( !b.IsDestroyed && b.Type == type ) n++;
		return n;
	}

	public int RecruitCapacity => GameConstants.MaxRecruitCapacity( BarracksCount );

	/// <summary>Non-destroyed barracks in the order they were placed.</summary>
	public IReadOnlyList<PlacedBuilding> GetBarracksOrdered()
	{
		return _cells.Values
			.Where( b => !b.IsDestroyed && b.Type == BuildableId.Barracks )
			.OrderBy( b => b.PlaceOrder )
			.ToList();
	}

	/// <summary>
	/// Recruits 1–3 use barracks #1, 4–6 use barracks #2, etc. (0-based recruit index).
	/// </summary>
	public PlacedBuilding GetBarracksForRecruit( int recruitIndex )
	{
		var barracks = GetBarracksOrdered();
		if ( barracks.Count == 0 || recruitIndex < 0 ) return null;

		var slot = recruitIndex / GameConstants.RecruitsPerBarracks;
		if ( slot >= barracks.Count )
			slot = barracks.Count - 1;

		return barracks[slot];
	}

	public bool TryGetPlotForBuilding( PlacedBuilding building, out int plotX, out int plotY )
	{
		plotX = plotY = 0;
		if ( building is null ) return false;
		var center = BuildGrid.CellToWorld( building.CellX, building.CellY );
		return PlotGrid.WorldToPlot( center, out plotX, out plotY );
	}

	public double PlaceCost( BuildableDef def )
	{
		var cost = CombatEconomy.EscalatedCost( def.BaseCost, CountAlive( def.Id ), def.CostBump );
		var core = GameCore.Instance;
		if ( core?.IsCure == true )
			cost *= TeamBonuses.BuildCostMult( core );
		return cost;
	}

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
		ClearGhost();
	}

	public void LoadFromSave( SaveData save )
	{
		ClearAll();
		_nextPlaceOrder = 0;
		foreach ( var entry in save.Buildings )
		{
			try
			{
				var def = BuildableCatalog.Get( entry.Type );
				if ( entry.Health <= 0f )
					continue;

				var order = entry.PlaceOrder > 0 ? entry.PlaceOrder : ++_nextPlaceOrder;
				PlaceInternal( def.Id, entry.CellX, entry.CellY, entry.Level, entry.Health, order, fromSave: true, paidPlaceCost: entry.PaidPlaceCost, yawSteps: entry.YawSteps );
			}
			catch ( Exception e )
			{
				Log.Warning( $"[FinalOutpost] Skipped building {entry.Type}: {e.Message}" );
			}
		}
	}

	public void SaveTo( SaveData save )
	{
		save.Buildings = _cells.Values
			.Where( b => !b.IsDestroyed )
			.OrderBy( b => b.PlaceOrder )
			.ThenBy( b => b.CellX )
			.ThenBy( b => b.CellY )
			.Select( b => b.ToSave() )
			.ToList();
	}

	public void ClearAll()
	{
		CancelBuildInteraction();
		TileOccupancy.ClearBuildings();
		foreach ( var b in _cells.Values )
			b?.GameObject?.Destroy();
		_cells.Clear();
		_nextPlaceOrder = 0;
		Selected = null;
	}

	public void BeginPlacement( BuildableId id )
	{
		if ( !NightUnlocks.IsBuildingUnlocked( GameCore.Instance?.Save, id ) ) return;
		CancelMove();
		PlacementMode = id;
		Selected = null;
		_placementYawSteps = 0;
		_placementYawManual = false;
	}

	public void CancelPlacement()
	{
		PlacementMode = null;
		ClearGhost();
	}

	void ClearGhost()
	{
		_ghostGo?.Destroy();
		_ghostGo = null;
		_ghostParts.Clear();
		_ghostBuiltType = null;
		_ghostIsCore = false;
		_ghostCellX = int.MinValue;
		_ghostCellY = int.MinValue;
	}

	public void BeginMove( PlacedBuilding building )
	{
		var core = GameCore.Instance;
		if ( core is null || core.Phase != GamePhase.Day || building is null || building.IsDestroyed ) return;
		if ( RepairManager.Instance?.IsScheduled( building ) == true ) return;

		CancelPlacement();
		CancelMove();

		MovingBuilding = building;
		_placementYawSteps = building.YawSteps;
		_placementYawManual = true;
		building.SetHiddenForMove( true );
		Selected = null;
	}

	public void BeginMoveCore()
	{
		var core = GameCore.Instance;
		if ( core is null || core.Phase != GamePhase.Day ) return;
		if ( OutpostManager.Instance is null ) return;
		if ( RepairManager.Instance?.IsScheduledCore() == true ) return;

		CancelPlacement();
		CancelMove();

		MovingCore = true;
		OutpostManager.Instance.SetCoreHiddenForMove( true );
		Selected = null;
	}

	public void CancelMove()
	{
		if ( MovingBuilding is not null )
		{
			MovingBuilding.SetHiddenForMove( false );
			MovingBuilding = null;
		}

		if ( MovingCore )
		{
			OutpostManager.Instance?.SetCoreHiddenForMove( false );
			MovingCore = false;
		}

		ClearGhost();
	}

	public void CancelBuildInteraction()
	{
		CancelPlacement();
		CancelMove();
	}

	public void Select( ISelectable selectable )
	{
		Selected = selectable;
		CancelBuildInteraction();
	}

	public void Deselect() => Selected = null;

	/// <summary>Damaging combat pads — attack towers and barracks (not supports, labs, or civic).</summary>
	public static bool CountsTowardPlotStructureLimit( BuildableId id )
	{
		var def = BuildableCatalog.Get( id );
		return def.Role == BuildingRole.Defense || id == BuildableId.Barracks;
	}

	public static bool CountsTowardPlotSupportLimit( BuildableId id ) =>
		BuildableCatalog.Get( id ).Role == BuildingRole.Support;

	/// <summary>
	/// Attack-slot budget for a plot. Home plot gains +1 per Fortify Command level;
	/// outer plots stay at the base cap.
	/// </summary>
	public static int MaxStructuresForPlot( int plotX, int plotY )
	{
		var max = GameConstants.MaxPlotStructures;
		if ( !PlotGrid.IsHome( plotX, plotY ) )
			return max;

		var fortify = GameCore.Instance?.Upgrades?.Level( UpgradeId.FortifyCore ) ?? 0;
		return max + fortify;
	}

	public bool TryGetPlotAtCell( int cellX, int cellY, out int plotX, out int plotY )
	{
		plotX = plotY = 0;
		var center = BuildGrid.CellToWorld( cellX, cellY );
		return PlotGrid.WorldToPlot( center, out plotX, out plotY );
	}

	public int CountPlotStructures( int plotX, int plotY, PlacedBuilding exclude = null )
	{
		var n = 0;
		foreach ( var b in _cells.Values )
		{
			if ( b.IsDestroyed || !CountsTowardPlotStructureLimit( b.Type ) ) continue;
			if ( exclude is not null && ReferenceEquals( b, exclude ) ) continue;
			var center = BuildGrid.CellToWorld( b.CellX, b.CellY );
			if ( PlotGrid.WorldToPlot( center, out var px, out var py ) && px == plotX && py == plotY )
				n++;
		}
		return n;
	}

	public int CountPlotSupports( int plotX, int plotY, PlacedBuilding exclude = null )
	{
		var n = 0;
		foreach ( var b in _cells.Values )
		{
			if ( b.IsDestroyed || !CountsTowardPlotSupportLimit( b.Type ) ) continue;
			if ( exclude is not null && ReferenceEquals( b, exclude ) ) continue;
			var center = BuildGrid.CellToWorld( b.CellX, b.CellY );
			if ( PlotGrid.WorldToPlot( center, out var px, out var py ) && px == plotX && py == plotY )
				n++;
		}
		return n;
	}

	public bool PlotHasSupportType( int plotX, int plotY, BuildableId id, PlacedBuilding exclude = null )
	{
		if ( !CountsTowardPlotSupportLimit( id ) ) return false;
		foreach ( var b in _cells.Values )
		{
			if ( b.IsDestroyed || b.Type != id ) continue;
			if ( exclude is not null && ReferenceEquals( b, exclude ) ) continue;
			var center = BuildGrid.CellToWorld( b.CellX, b.CellY );
			if ( PlotGrid.WorldToPlot( center, out var px, out var py ) && px == plotX && py == plotY )
				return true;
		}
		return false;
	}

	public bool IsPlotStructureLimitReached( int cellX, int cellY, BuildableId id, PlacedBuilding exclude = null )
	{
		if ( !CountsTowardPlotStructureLimit( id ) ) return false;
		if ( !TryGetPlotAtCell( cellX, cellY, out var px, out var py ) ) return false;
		return CountPlotStructures( px, py, exclude ) >= MaxStructuresForPlot( px, py );
	}

	public bool IsPlotSupportLimitReached( int cellX, int cellY, BuildableId id, PlacedBuilding exclude = null )
	{
		if ( !CountsTowardPlotSupportLimit( id ) ) return false;
		if ( !TryGetPlotAtCell( cellX, cellY, out var px, out var py ) ) return false;
		return CountPlotSupports( px, py, exclude ) >= GameConstants.MaxPlotSupports;
	}

	public bool IsPlotSupportDuplicate( int cellX, int cellY, BuildableId id, PlacedBuilding exclude = null )
	{
		if ( !CountsTowardPlotSupportLimit( id ) ) return false;
		if ( !TryGetPlotAtCell( cellX, cellY, out var px, out var py ) ) return false;
		return PlotHasSupportType( px, py, id, exclude );
	}

	/// <summary>Null when placement is allowed under plot caps; otherwise a player-facing toast.</summary>
	public string PlotCapToast( int cellX, int cellY, BuildableId id, PlacedBuilding exclude = null )
	{
		if ( !TryGetPlotAtCell( cellX, cellY, out var px, out var py ) )
			return null;

		if ( CountsTowardPlotSupportLimit( id ) )
		{
			if ( PlotHasSupportType( px, py, id, exclude ) )
				return $"Already have a {BuildableCatalog.Get( id ).Name} on this plot.";
			if ( CountPlotSupports( px, py, exclude ) >= GameConstants.MaxPlotSupports )
				return $"Plot support full ({GameConstants.MaxPlotSupports}/{GameConstants.MaxPlotSupports}).";
			return null;
		}

		if ( CountsTowardPlotStructureLimit( id ) )
		{
			var max = MaxStructuresForPlot( px, py );
			if ( CountPlotStructures( px, py, exclude ) >= max )
			{
				var hint = PlotGrid.IsHome( px, py ) && max > GameConstants.MaxPlotStructures
					? " Fortify Command adds slots on the home plot."
					: " Claim an adjacent plot to expand.";
				return $"Plot full ({max}/{max} towers & barracks).{hint}";
			}
		}

		return null;
	}

	public bool CanPlaceAt( int cellX, int cellY, BuildableId? id = null, PlacedBuilding ignoreOccupied = null )
	{
		if ( BuildGrid.IsCoreCell( cellX, cellY ) ) return false;

		if ( !GameConstants.AllowWallMountPlacement && TileOccupancy.IsWallCell( cellX, cellY ) )
			return false;

		if ( _cells.TryGetValue( (cellX, cellY), out var occupant )
		     && (ignoreOccupied is null || !ReferenceEquals( occupant, ignoreOccupied )) )
			return false;

		var center = BuildGrid.CellToWorld( cellX, cellY );
		var onWall = TileOccupancy.IsWallCell( cellX, cellY );
		var wallMount = GameConstants.AllowWallMountPlacement
			&& onWall
			&& id is { } mountId
			&& BuildableCatalog.Get( mountId ).Role == BuildingRole.Defense;

		if ( !PlotGrid.WorldToPlot( center, out var px, out var py ) )
		{
			if ( !wallMount ) return false;
		}
		else
		{
			var plots = PlotManager.Instance;
			var plotOk = plots is not null && plots.IsBuildable( px, py );

			// Defenses may sit on perimeter wall tiles even when that cell's plot isn't cleared yet.
			if ( !plotOk && !wallMount ) return false;

			if ( plotOk && id is { } buildingId )
			{
				if ( IsPlotStructureLimitReached( cellX, cellY, buildingId, ignoreOccupied ) )
					return false;
				if ( IsPlotSupportLimitReached( cellX, cellY, buildingId, ignoreOccupied ) )
					return false;
				if ( IsPlotSupportDuplicate( cellX, cellY, buildingId, ignoreOccupied ) )
					return false;
			}
		}

		if ( id is not { } placeId )
			return true;

		var footprint = BuildableCatalog.Get( placeId ).VisualSize;
		if ( OverlapsAnyBuilding( cellX, cellY, footprint, ignoreOccupied ) )
			return false;

		var corePos = OutpostManager.Instance?.CorePosition ?? Vector3.Zero;
		if ( !MovingCore && BuildGrid.FootprintsOverlap( center, footprint, corePos, BuildGrid.CommandPostFootprint ) )
			return false;

		return true;
	}

	/// <summary>
	/// Valid southwest anchor for the command post's 2×2 — home courtyard only, clear of walls/buildings.
	/// </summary>
	public bool CanPlaceCoreAt( int anchorX, int anchorY )
	{
		for ( var dx = 0; dx <= 1; dx++ )
		for ( var dy = 0; dy <= 1; dy++ )
		{
			var cx = anchorX + dx;
			var cy = anchorY + dy;

			if ( TileOccupancy.IsWallCell( cx, cy ) )
				return false;

			if ( _cells.ContainsKey( (cx, cy) ) )
				return false;

			var cellCenter = BuildGrid.CellToWorld( cx, cy );
			if ( !PlotGrid.WorldToPlot( cellCenter, out var px, out var py ) || !PlotGrid.IsHome( px, py ) )
				return false;

			var plots = PlotManager.Instance;
			if ( plots is not null && !plots.IsBuildable( px, py ) )
				return false;
		}

		var world = BuildGrid.CoreWorldFromAnchor( anchorX, anchorY );
		foreach ( var b in _cells.Values )
		{
			if ( b.IsDestroyed ) continue;
			var otherCenter = BuildGrid.CellToWorld( b.CellX, b.CellY );
			if ( BuildGrid.FootprintsOverlap( world, BuildGrid.CommandPostFootprint, otherCenter, b.Def.VisualSize ) )
				return false;
		}

		return true;
	}

	private bool OverlapsAnyBuilding( int cellX, int cellY, Vector3 footprint, PlacedBuilding ignore = null )
	{
		var center = BuildGrid.CellToWorld( cellX, cellY );
		foreach ( var b in _cells.Values )
		{
			if ( b.IsDestroyed ) continue;
			if ( ignore is not null && ReferenceEquals( b, ignore ) ) continue;

			var otherCenter = BuildGrid.CellToWorld( b.CellX, b.CellY );
			if ( BuildGrid.FootprintsOverlap( center, footprint, otherCenter, b.Def.VisualSize ) )
				return true;
		}

		return false;
	}

	public void TickBarracksHeal( float dt )
	{
		var defenders = DefenderManager.Instance;
		if ( defenders is null || dt <= 0f ) return;

		var heal = GameConstants.BarracksHealPerSec * dt;
		if ( GameCore.Instance?.IsCure == true )
			heal *= TeamBonuses.HealRateMult( GameCore.Instance );
		foreach ( var b in GetBarracksOrdered() )
			defenders.HealAssignedToBarracks( b, heal );
	}

	/// <summary>After a successful night, fully restore recruits stationed at each barracks.</summary>
	public void BarracksHealAfterNight()
	{
		var defenders = DefenderManager.Instance;
		if ( defenders is null ) return;

		foreach ( var b in GetBarracksOrdered() )
			defenders.FullHealAssignedToBarracks( b );
	}

	public bool TryPlace( BuildableId id, int cellX, int cellY )
	{
		var core = GameCore.Instance;
		if ( core is null || core.Phase != GamePhase.Day ) return false;

		var wallMount = GameConstants.AllowWallMountPlacement
			&& TileOccupancy.IsWallCell( cellX, cellY )
			&& BuildableCatalog.Get( id ).Role == BuildingRole.Defense;

		if ( !wallMount )
		{
			var capToast = PlotCapToast( cellX, cellY, id );
			if ( capToast is not null )
			{
				core.ShowToast( capToast );
				return false;
			}
		}

		if ( !wallMount && TryGetPlotAtCell( cellX, cellY, out var plotX, out var plotY ) )
		{
			var plots = PlotManager.Instance;
			if ( plots is not null
				&& !PlotGrid.IsHome( plotX, plotY )
				&& plots.IsOwned( plotX, plotY )
				&& !plots.IsCleared( plotX, plotY ) )
			{
				core.ShowToast( "You must clear this with a forager first!" );
				return false;
			}
		}

		if ( !CanPlaceAt( cellX, cellY, id ) )
		{
			core.ShowToast( !GameConstants.AllowWallMountPlacement && TileOccupancy.IsWallCell( cellX, cellY )
				? "Wall mounts are disabled for now."
				: "Cannot place here — overlaps another building or the command post." );
			return false;
		}

		var def = BuildableCatalog.Get( id );
		if ( !NightUnlocks.IsBuildingUnlocked( core.Save, id ) ) return false;
		var cost = PlaceCost( def );
		if ( !BuildPayment.TryPay( core, cost ) )
		{
			core.ShowToast( BuildPayment.ShortfallToast( core, cost ) );
			return false;
		}

		// AUDIT FIX M1: record what was actually paid (barracks escalates).
		PlaceInternal( id, cellX, cellY, 1, def.MaxHp( 1 ), placeOrder: 0, fromSave: false, paidPlaceCost: cost, yawSteps: _placementYawSteps );
		KnowledgeGain.OnBuildingPlaced( core );
		Sfx.Play( Sfx.Purchase );
		core.SaveManagerTouch();
		CancelPlacement();
		return true;
	}

	public bool TryMove( int cellX, int cellY )
	{
		var core = GameCore.Instance;
		var building = MovingBuilding;
		if ( core is null || core.Phase != GamePhase.Day || building is null ) return false;

		if ( building.CellX == cellX && building.CellY == cellY )
		{
			CancelMove();
			Select( building );
			return true;
		}

		var wallMount = GameConstants.AllowWallMountPlacement
			&& TileOccupancy.IsWallCell( cellX, cellY )
			&& building.Def.Role == BuildingRole.Defense;

		if ( !wallMount )
		{
			var capToast = PlotCapToast( cellX, cellY, building.Type, building );
			if ( capToast is not null )
			{
				core.ShowToast( capToast );
				return false;
			}
		}

		if ( !wallMount && TryGetPlotAtCell( cellX, cellY, out var plotX, out var plotY ) )
		{
			var plots = PlotManager.Instance;
			if ( plots is not null
				&& !PlotGrid.IsHome( plotX, plotY )
				&& plots.IsOwned( plotX, plotY )
				&& !plots.IsCleared( plotX, plotY ) )
			{
				core.ShowToast( "You must clear this with a forager first!" );
				return false;
			}
		}

		if ( !CanPlaceAt( cellX, cellY, building.Type, building ) )
		{
			core.ShowToast( "Cannot move here — overlaps another building or the command post." );
			return false;
		}

		_cells.Remove( (building.CellX, building.CellY) );
		TileOccupancy.UnmarkBuilding( building );
		building.Relocate( cellX, cellY, _placementYawSteps );
		_cells[(cellX, cellY)] = building;
		TileOccupancy.MarkBuilding( building );
		building.SetHiddenForMove( false );
		MovingBuilding = null;
		ClearGhost();
		Select( building );
		core.SaveManagerTouch();
		return true;
	}

	public bool TryMoveCore( int anchorCellX, int anchorCellY )
	{
		var core = GameCore.Instance;
		var outpost = OutpostManager.Instance;
		if ( core is null || core.Phase != GamePhase.Day || !MovingCore || outpost is null )
			return false;

		if ( outpost.CoreAnchorCellX == anchorCellX && outpost.CoreAnchorCellY == anchorCellY )
		{
			CancelMove();
			Select( new CoreSelectable( outpost ) );
			return true;
		}

		if ( !CanPlaceCoreAt( anchorCellX, anchorCellY ) )
		{
			core.ShowToast( "Cannot move command post here — need a clear 2×2 on the home plot." );
			return false;
		}

		if ( !outpost.RelocateCore( anchorCellX, anchorCellY ) )
			return false;

		MovingCore = false;
		outpost.SetCoreHiddenForMove( false );
		ClearGhost();
		Select( new CoreSelectable( outpost ) );
		core.SaveManagerTouch();
		return true;
	}

	public bool TryUpgradeBuilding( PlacedBuilding building )
	{
		var core = GameCore.Instance;
		// AUDIT FIX H5: API had no Day gate (UI gated Move only).
		if ( core is null || core.Phase != GamePhase.Day || building is null || building.IsDestroyed ) return false;
		if ( building.Level >= building.Def.MaxLevel ) return false;

		var cost = building.Def.UpgradeCost( building.Level );
		if ( !BuildPayment.TryPay( core, cost ) )
		{
			core.ShowToast( BuildPayment.ShortfallToast( core, cost ) );
			return false;
		}

		building.TryUpgrade();
		Sfx.Play( Sfx.Purchase );
		core.SaveManagerTouch();
		return true;
	}

	public bool TryRepairBuilding( PlacedBuilding building ) =>
		RepairManager.Instance?.TryRepairBuilding( building ) ?? false;

	public bool TryRepairWall( WallSegment wall ) =>
		RepairManager.Instance?.TryRepairWall( wall ) ?? false;

	public bool TryRepairCore() =>
		RepairManager.Instance?.TryRepairCore() ?? false;

	public double RepairAllCost()
	{
		var rm = RepairManager.Instance;
		var total = 0.0;

		foreach ( var b in _cells.Values )
		{
			if ( b.IsDestroyed || rm?.IsScheduled( b ) == true ) continue;
			total += b.Def.RepairCost( b.MaxHealth - b.Health );
		}

		var outpost = OutpostManager.Instance;
		if ( outpost is not null )
		{
			if ( outpost.CoreHealth < outpost.CoreMaxHealth && rm?.IsScheduledCore() != true )
				total += RepairCosts.BaseScrapCost( outpost.CoreMaxHealth - outpost.CoreHealth );

			foreach ( var w in outpost.Walls )
			{
				if ( w.Health >= w.MaxHealth || rm?.IsScheduled( w ) == true ) continue;
				total += RepairCosts.BaseScrapCost( w.MaxHealth - w.Health );
			}
		}

		return RepairCosts.EffectiveScrapCost( total );
	}

	/// <summary>What Repair All will actually charge given current scrap (may be a partial spend).</summary>
	public double AffordableRepairAllCost()
	{
		var cost = RepairAllCost();
		return cost <= 0.001 ? 0 : SelectHelp.AffordableRepairSpend( cost );
	}

	public bool CanStartRepairAll()
	{
		if ( !SelectHelp.IsDay || !HasUnscheduledDamage() ) return false;
		var cost = RepairAllCost();
		return cost <= 0.001 || SelectHelp.AffordableRepairSpend( cost ) > 0;
	}

	public bool HasUnscheduledDamage()
	{
		var rm = RepairManager.Instance;
		foreach ( var b in _cells.Values )
		{
			if ( !b.IsDestroyed && b.Health < b.MaxHealth && rm?.IsScheduled( b ) != true )
				return true;
		}

		var outpost = OutpostManager.Instance;
		if ( outpost is not null )
		{
			if ( outpost.CoreHealth < outpost.CoreMaxHealth && rm?.IsScheduledCore() != true )
				return true;
			foreach ( var w in outpost.Walls )
			{
				if ( w.Health < w.MaxHealth && rm?.IsScheduled( w ) != true )
					return true;
			}
		}

		return false;
	}

	public bool TryRepairAll() =>
		RepairManager.Instance?.TryRepairAll( _cells.Values ) ?? false;

	public bool TrySellBuilding( PlacedBuilding building )
	{
		var core = GameCore.Instance;
		// AUDIT FIX H5: sell was night-capable while place/move were not.
		if ( core is null || core.Phase != GamePhase.Day || building is null ) return false;

		core.Wallet.Earn( building.SellRefund(), applyIncomeScale: false );
		if ( ReferenceEquals( Selected, building ) )
			Selected = null;

		RemoveBuilding( building );
		Sfx.Play( Sfx.Purchase );
		core.SaveManagerTouch();
		return true;
	}

	/// <summary>Any structure that reaches 0 HP is fully removed mid-round — no rubble foundation.</summary>
	public void HandleStructureDestroyed( PlacedBuilding building )
	{
		var core = GameCore.Instance;
		if ( core is null || building is null ) return;

		var name = building.Def.Name;
		var isDefense = building.IsDefense;
		var refund = isDefense ? building.SellRefund() : 0;

		RepairManager.Instance?.CancelBuilding( building );

		if ( ReferenceEquals( Selected, building ) )
			Selected = null;

		RemoveBuilding( building );

		if ( refund > 0 )
		{
			core.Wallet.Earn( refund, applyIncomeScale: false );
			core.ShowToast( $"{name} destroyed — +{GameConstants.FormatScrap( refund ).Replace( " scrap", "" )} scrap salvaged" );
		}
		else
			core.ShowToast( $"{name} destroyed" );

		Sfx.Play( Sfx.Purchase );
		core.SaveManagerTouch();
	}

	public void HandleTowerDestroyed( PlacedBuilding building ) =>
		HandleStructureDestroyed( building );

	/// <summary>Removes destroyed player structures (rubble foundations) at the start of each day.</summary>
	public void ClearDestroyedRemnants()
	{
		var toRemove = _cells.Values.Where( b => b.IsDestroyed ).ToList();
		if ( toRemove.Count == 0 ) return;

		foreach ( var building in toRemove )
		{
			RepairManager.Instance?.CancelBuilding( building );
			if ( ReferenceEquals( Selected, building ) )
				Selected = null;
			RemoveBuilding( building );
		}

		GameCore.Instance?.SaveManagerTouch();
	}

	public PlacedBuilding NearestBuilding( Vector3 pos, float maxDist )
	{
		PlacedBuilding best = null;
		var bestD = maxDist * maxDist;

		foreach ( var b in _cells.Values )
		{
			if ( b.IsDestroyed || ReferenceEquals( b, MovingBuilding ) ) continue;
			var d = (b.Center - pos).LengthSquared;
			if ( d < bestD ) { bestD = d; best = b; }
		}

		return best;
	}

	public PlacedBuilding BuildingAt( int cellX, int cellY ) =>
		_cells.TryGetValue( (cellX, cellY), out var b ) ? b : null;

	/// <summary>Re-seat wall-mounted towers after perimeter wall tiles are marked/unmarked.</summary>
	public void RefreshWallMountHeights()
	{
		foreach ( var b in _cells.Values )
		{
			if ( b is null || b.IsDestroyed || (!b.IsDefense && !b.IsSupport) ) continue;
			b.RefreshMountHeight();
		}
	}

	/// <summary>Finds the closest clickable base element (building, wall, defender, or command post) to a world point.</summary>
	public ISelectable PickSelectable( Vector3 ground )
	{
		var groundXY = new Vector2( ground.x, ground.y );
		ISelectable bestBuilding = null;
		var bestBuildingDist = float.MaxValue;
		ISelectable bestOther = null;
		var bestOtherDist = float.MaxValue;

		void ConsiderBuilding( ISelectable s )
		{
			if ( s is null || !s.IsAlive ) return;
			var pos = new Vector2( s.WorldPos.x, s.WorldPos.y );
			var d = (pos - groundXY).Length;
			if ( d <= s.SelectRadius && d < bestBuildingDist )
			{
				bestBuildingDist = d;
				bestBuilding = s;
			}
		}

		void ConsiderOther( ISelectable s )
		{
			if ( s is null || !s.IsAlive ) return;
			var pos = new Vector2( s.WorldPos.x, s.WorldPos.y );
			var d = (pos - groundXY).Length;
			if ( d <= s.SelectRadius && d < bestOtherDist )
			{
				bestOtherDist = d;
				bestOther = s;
			}
		}

		foreach ( var b in _cells.Values )
			ConsiderBuilding( b );

		// Wall-mounted towers share the wall's XY — always prefer the building under the cursor.
		if ( bestBuilding is not null )
			return bestBuilding;

		var outpost = OutpostManager.Instance;
		if ( outpost is not null )
		{
			foreach ( var w in outpost.Walls )
			{
				if ( w.IsBroken ) continue;

				// Skip walls that already have a defense sitting on them.
				if ( BuildGrid.WorldToCell( w.Center, out var wx, out var wy )
				     && _cells.TryGetValue( (wx, wy), out var mounted )
				     && mounted is { IsDestroyed: false, IsDefense: true } )
					continue;

				ConsiderOther( new WallSelectable( w ) );
			}

			ConsiderOther( new CoreSelectable( outpost ) );
		}

		var defenders = DefenderManager.Instance;
		if ( defenders is not null )
			foreach ( var u in defenders.Units )
				ConsiderOther( new DefenderSelectable( u ) );

		var workers = WorkerManager.Instance;
		if ( workers is not null )
			foreach ( var u in workers.Units )
				ConsiderOther( new WorkerSelectable( u ) );

		var plots = PlotManager.Instance;
		var core = GameCore.Instance;
		if ( plots is not null
		     && PlotGrid.WorldToPlot( ground, out var px, out var py )
		     && !PlotGrid.IsHome( px, py )
		     && (core?.IsCure == true || plots.IsOwned( px, py ) || plots.IsBuyable( px, py )) )
			ConsiderOther( new PlotSelectable( px, py ) );

		return bestOther;
	}

	protected override void OnUpdate()
	{
		var core = GameCore.Instance;
		if ( core is null ) return;

		if ( Selected is not null && !Selected.IsAlive )
			Selected = null;

		if ( core.IsUiBlocking ) return;
		if ( RecruitTakeoverController.Instance?.IsPossessing == true ) return;

		// Night: only recruit focus / area orders. Towers keep firing on their own.
		if ( core.Phase == GamePhase.Night )
		{
			HandleNightOrders();
			return;
		}

		if ( core.Phase != GamePhase.Day ) return;

		UpdateGhost();
		HandleInput();
	}

	private void HandleNightOrders()
	{
		var orders = UnitOrderController.Instance;
		if ( orders is null ) return;

		if ( Input.Pressed( "Attack2" ) || Input.Pressed( "Cancel" ) )
		{
			orders.CancelNightCommandMode();
			return;
		}

		if ( !orders.CanAcceptNightClick || !Input.Pressed( "Attack1" ) ) return;

		var cam = OutpostCamera.Instance;
		if ( cam is null || !cam.ScreenToGround( Mouse.Position, out var ground ) )
			return;

		orders.TryIssueNightRecruitOrders( ground );
	}

	private void HandleInput()
	{
		if ( (PlacementMode is not null || MovingBuilding is not null) && Input.Pressed( "Reload" ) )
		{
			_placementYawSteps = (_placementYawSteps + 1) % 4;
			_placementYawManual = true;
		}

		var cam = OutpostCamera.Instance;
		if ( cam is null || !cam.ScreenToGround( Mouse.Position, out var ground ) )
			return;

		if ( Input.Pressed( "Attack1" ) )
		{
			if ( PlacementMode is { } mode )
			{
				if ( BuildGrid.WorldToCell( ground, out var cx, out var cy ) )
					TryPlace( mode, cx, cy );
			}
			else if ( MovingBuilding is not null )
			{
				if ( BuildGrid.WorldToCell( ground, out var cx, out var cy ) )
					TryMove( cx, cy );
			}
			else if ( MovingCore )
			{
				if ( BuildGrid.WorldToCell( ground, out var cx, out var cy ) )
					TryMoveCore( cx, cy );
			}
			else
			{
				if ( Selected is not null && GameCore.Instance?.IsCure == true
				     && UnitOrderController.Instance?.TryIssueOrder( ground, Selected ) == true )
					return;

				var hit = PickSelectable( ground );
				if ( hit is not null ) Select( hit );
				else Deselect();
			}
		}

		if ( Input.Pressed( "Attack2" ) || Input.Pressed( "Cancel" ) )
		{
			if ( PlacementMode is not null )
				CancelPlacement();
			else if ( MovingBuilding is not null || MovingCore )
				CancelMove();
			else
				Deselect();
		}
	}

	private void UpdateGhost()
	{
		var moving = MovingBuilding;
		if ( PlacementMode is null && moving is null && !MovingCore )
		{
			ClearGhost();
			return;
		}

		if ( MovingCore )
		{
			UpdateCoreGhost();
			return;
		}

		var cam = OutpostCamera.Instance;
		if ( cam is null || !cam.ScreenToGround( Mouse.Position, out var ground ) )
			return;

		if ( !BuildGrid.WorldToCell( ground, out var cx, out var cy ) )
			return;

		var id = PlacementMode ?? moving.Type;
		var mount = TileOccupancy.WallMountWorldPosition( cx, cy );
		var valid = CanPlaceAt( cx, cy, id, moving );
		var afford = PlacementMode is not null
			? BuildPayment.CanAfford( GameCore.Instance, PlaceCost( BuildableCatalog.Get( id ) ) )
			: true;

		if ( !_placementYawManual && id == BuildableId.WallPiece
		     && (_ghostCellX != cx || _ghostCellY != cy || !_ghostGo.IsValid()) )
			_placementYawSteps = AutoWallYawSteps( mount );

		var needRebuild = !_ghostGo.IsValid()
			|| _ghostBuiltType != id;

		if ( needRebuild )
			RebuildGhost( id, mount );

		_ghostCellX = cx;
		_ghostCellY = cy;
		if ( _ghostGo.IsValid() )
		{
			_ghostGo.WorldPosition = mount;
			_ghostGo.WorldRotation = Rotation.FromYaw( _placementYawSteps * 90f );
		}

		var ok = valid && afford == true;
		foreach ( var (mr, baseTint) in _ghostParts )
		{
			if ( !mr.IsValid() ) continue;
			mr.Tint = ok
				? baseTint.WithAlpha( 0.55f )
				: new Color( 1f, 0.25f, 0.2f, 0.5f );
		}
	}

	void UpdateCoreGhost()
	{
		var cam = OutpostCamera.Instance;
		if ( cam is null || !cam.ScreenToGround( Mouse.Position, out var ground ) )
			return;

		if ( !BuildGrid.WorldToCell( ground, out var ax, out var ay ) )
			return;

		var world = BuildGrid.CoreWorldFromAnchor( ax, ay );
		var valid = CanPlaceCoreAt( ax, ay );

		if ( !_ghostGo.IsValid() || !_ghostIsCore )
			RebuildCoreGhost( world );

		_ghostCellX = ax;
		_ghostCellY = ay;
		if ( _ghostGo.IsValid() )
			_ghostGo.WorldPosition = world;

		foreach ( var (mr, baseTint) in _ghostParts )
		{
			if ( !mr.IsValid() ) continue;
			mr.Tint = valid
				? baseTint.WithAlpha( 0.55f )
				: new Color( 1f, 0.25f, 0.2f, 0.5f );
		}
	}

	void RebuildCoreGhost( Vector3 worldPos )
	{
		ClearGhost();
		_ghostGo = new GameObject( true, "CoreMoveGhost" );
		_ghostGo.WorldPosition = worldPos;
		_ghostParts = new List<(ModelRenderer Renderer, Color Base)>();
		CommandPostVisual.Build( _ghostGo, ( mr, tint ) => _ghostParts.Add( (mr, tint) ) );
		_ghostIsCore = true;
	}

	void RebuildGhost( BuildableId id, Vector3 worldPos )
	{
		ClearGhost();
		_ghostGo = new GameObject( true, "PlacementGhost" );
		_ghostGo.WorldPosition = worldPos;
		_ghostGo.WorldRotation = Rotation.FromYaw( _placementYawSteps * 90f );
		var built = BuildingVisual.Build( _ghostGo, id, worldPos, includeRubble: false );
		_ghostParts = built.Parts;
		_ghostBuiltType = id;
	}

	/// <summary>Default wall facing so the ghost joins the nearest perimeter side flush.</summary>
	static int AutoWallYawSteps( Vector3 worldPos )
	{
		var side = WallApproach.FromWorldPosition( worldPos, Vector3.Zero );
		// Local wall mesh runs along +X; 90° aims it along +Y for east/west sides.
		return side is WallApproachSide.East or WallApproachSide.West ? 1 : 0;
	}

	private void RemoveBuilding( PlacedBuilding building )
	{
		TileOccupancy.UnmarkBuilding( building );
		_cells.Remove( (building.CellX, building.CellY) );
		building.GameObject.Destroy();
	}

	private void PlaceInternal( BuildableId id, int cellX, int cellY, int level, float health, int placeOrder, bool fromSave, double paidPlaceCost = 0, int yawSteps = 0 )
	{
		if ( placeOrder <= 0 )
			placeOrder = ++_nextPlaceOrder;
		else if ( placeOrder > _nextPlaceOrder )
			_nextPlaceOrder = placeOrder;

		var go = new GameObject( true, $"Building_{id}_{cellX}_{cellY}" );
		var building = go.Components.Create<PlacedBuilding>();
		building.Setup( id, cellX, cellY, level, health, placeOrder, paidPlaceCost, yawSteps );
		_cells[(cellX, cellY)] = building;
		TileOccupancy.MarkBuilding( building );
	}
}
