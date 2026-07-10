namespace FinalOutpost;

/// <summary>Handles grid placement, selection, upgrades, repairs, and persistence of buildings.</summary>
public sealed class BuildManager : Component
{
	public static BuildManager Instance { get; private set; }

	public ISelectable Selected { get; private set; }
	public BuildableId? PlacementMode { get; private set; }
	public PlacedBuilding MovingBuilding { get; private set; }

	private readonly Dictionary<(int x, int y), PlacedBuilding> _cells = new();
	private int _nextPlaceOrder;
	private GameObject _ghostGo;
	private ModelRenderer _ghostRenderer;
	private int _ghostCellX = -1;
	private int _ghostCellY = -1;

	public IReadOnlyCollection<PlacedBuilding> Buildings => _cells.Values;

	public int BarracksCount
	{
		get
		{
			var n = 0;
			foreach ( var b in _cells.Values )
				if ( !b.IsDestroyed && b.Type == BuildableId.Barracks ) n++;
			return n;
		}
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
		var cost = def.Id == BuildableId.Barracks
			? CombatEconomy.BarracksPlaceCost( def.BaseHp, BarracksCount )
			: def.PlaceCost;
		var core = GameCore.Instance;
		if ( core?.IsCure == true )
			cost *= TeamBonuses.BuildCostMult( core );
		return cost;
	}

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
		_ghostGo?.Destroy();
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
				PlaceInternal( def.Id, entry.CellX, entry.CellY, entry.Level, entry.Health, order, fromSave: true );
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
		EnsureGhost();
	}

	public void CancelPlacement()
	{
		PlacementMode = null;
		_ghostGo?.Destroy();
		_ghostGo = null;
		_ghostRenderer = null;
	}

	public void BeginMove( PlacedBuilding building )
	{
		var core = GameCore.Instance;
		if ( core is null || core.Phase != GamePhase.Day || building is null || building.IsDestroyed ) return;
		if ( RepairManager.Instance?.IsScheduled( building ) == true ) return;

		CancelPlacement();
		CancelMove();

		MovingBuilding = building;
		building.SetHiddenForMove( true );
		Selected = null;
		EnsureGhost();
	}

	public void CancelMove()
	{
		if ( MovingBuilding is not null )
		{
			MovingBuilding.SetHiddenForMove( false );
			MovingBuilding = null;
		}
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

	public static bool CountsTowardPlotStructureLimit( BuildableId id )
	{
		var role = BuildableCatalog.Get( id ).Role;
		return role is BuildingRole.Defense or BuildingRole.Management;
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

	public bool IsPlotStructureLimitReached( int cellX, int cellY, BuildableId id, PlacedBuilding exclude = null )
	{
		if ( !CountsTowardPlotStructureLimit( id ) ) return false;
		if ( !TryGetPlotAtCell( cellX, cellY, out var px, out var py ) ) return false;
		return CountPlotStructures( px, py, exclude ) >= GameConstants.MaxPlotStructures;
	}

	public bool CanPlaceAt( int cellX, int cellY, BuildableId? id = null, PlacedBuilding ignoreOccupied = null )
	{
		if ( BuildGrid.IsCoreCell( cellX, cellY ) ) return false;

		if ( _cells.TryGetValue( (cellX, cellY), out var occupant )
		     && (ignoreOccupied is null || !ReferenceEquals( occupant, ignoreOccupied )) )
			return false;

		var center = BuildGrid.CellToWorld( cellX, cellY );
		if ( !PlotGrid.WorldToPlot( center, out var px, out var py ) ) return false;

		var plots = PlotManager.Instance;
		if ( plots is null || !plots.IsBuildable( px, py ) ) return false;

		if ( id is { } buildingId && IsPlotStructureLimitReached( cellX, cellY, buildingId, ignoreOccupied ) )
			return false;

		return true;
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

		if ( IsPlotStructureLimitReached( cellX, cellY, id ) )
		{
			core.ShowToast( $"Plot full ({GameConstants.MaxPlotStructures}/{GameConstants.MaxPlotStructures} towers & barracks). Claim an adjacent plot to build more." );
			return false;
		}

		if ( TryGetPlotAtCell( cellX, cellY, out var plotX, out var plotY ) )
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

		if ( !CanPlaceAt( cellX, cellY, id ) ) return false;

		var def = BuildableCatalog.Get( id );
		if ( !NightUnlocks.IsBuildingUnlocked( core.Save, id ) ) return false;
		if ( !core.Wallet.TrySpend( PlaceCost( def ) ) ) return false;

		PlaceInternal( id, cellX, cellY, 1, def.MaxHp( 1 ), placeOrder: 0, fromSave: false );
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

		if ( IsPlotStructureLimitReached( cellX, cellY, building.Type, building ) )
		{
			core.ShowToast( $"Plot full ({GameConstants.MaxPlotStructures}/{GameConstants.MaxPlotStructures} towers & barracks). Claim an adjacent plot to build more." );
			return false;
		}

		if ( TryGetPlotAtCell( cellX, cellY, out var plotX, out var plotY ) )
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

		if ( !CanPlaceAt( cellX, cellY, building.Type, building ) ) return false;

		_cells.Remove( (building.CellX, building.CellY) );
		building.Relocate( cellX, cellY );
		_cells[(cellX, cellY)] = building;
		building.SetHiddenForMove( false );
		MovingBuilding = null;
		_ghostGo?.Destroy();
		_ghostGo = null;
		_ghostRenderer = null;
		Select( building );
		core.SaveManagerTouch();
		return true;
	}

	public bool TryUpgradeBuilding( PlacedBuilding building )
	{
		var core = GameCore.Instance;
		if ( core is null || building is null || building.IsDestroyed ) return false;
		if ( building.Level >= building.Def.MaxLevel ) return false;

		var cost = building.Def.UpgradeCost( building.Level );
		if ( !core.Wallet.TrySpend( cost ) ) return false;

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
			total += b.MissingRepairCost();
		}

		var outpost = OutpostManager.Instance;
		if ( outpost is not null )
		{
			if ( outpost.CoreHealth < outpost.CoreMaxHealth && rm?.IsScheduledCore() != true )
				total += (outpost.CoreMaxHealth - outpost.CoreHealth) * GameConstants.RepairCostPerHp;

			foreach ( var w in outpost.Walls )
			{
				if ( w.Health >= w.MaxHealth || rm?.IsScheduled( w ) == true ) continue;
				total += (w.MaxHealth - w.Health) * GameConstants.RepairCostPerHp;
			}
		}

		return total;
	}

	public bool TryRepairAll() =>
		RepairManager.Instance?.TryRepairAll( _cells.Values ) ?? false;

	public bool TrySellBuilding( PlacedBuilding building )
	{
		var core = GameCore.Instance;
		if ( core is null || building is null ) return false;

		core.Wallet.Earn( building.SellRefund(), applyIncomeScale: false );
		if ( ReferenceEquals( Selected, building ) )
			Selected = null;

		RemoveBuilding( building );
		Sfx.Play( Sfx.Purchase );
		core.SaveManagerTouch();
		return true;
	}

	/// <summary>Defense towers are salvaged for half their cost and fully removed when destroyed.</summary>
	public void HandleTowerDestroyed( PlacedBuilding building )
	{
		var core = GameCore.Instance;
		if ( core is null || building is null || !building.IsDefense ) return;

		var name = building.Def.Name;
		var refund = building.SellRefund();

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

	/// <summary>Finds the closest clickable base element (building, wall, defender, or command post) to a world point.</summary>
	public ISelectable PickSelectable( Vector3 ground )
	{
		var groundXY = new Vector2( ground.x, ground.y );
		ISelectable best = null;
		var bestDist = float.MaxValue;

		void Consider( ISelectable s )
		{
			if ( s is null || !s.IsAlive ) return;
			var pos = new Vector2( s.WorldPos.x, s.WorldPos.y );
			var d = (pos - groundXY).Length;
			if ( d <= s.SelectRadius && d < bestDist )
			{
				bestDist = d;
				best = s;
			}
		}

		foreach ( var b in _cells.Values )
			Consider( b );

		var outpost = OutpostManager.Instance;
		if ( outpost is not null )
		{
			foreach ( var w in outpost.Walls )
				if ( !w.IsBroken )
					Consider( new WallSelectable( w ) );

			Consider( new CoreSelectable( outpost ) );
		}

		var defenders = DefenderManager.Instance;
		if ( defenders is not null )
			foreach ( var u in defenders.Units )
				Consider( new DefenderSelectable( u ) );

		var workers = WorkerManager.Instance;
		if ( workers is not null )
			foreach ( var u in workers.Units )
				Consider( new WorkerSelectable( u ) );

		// Plots are large parcels — only the one under the cursor is a candidate (non-home).
		var plots = PlotManager.Instance;
		if ( plots is not null && PlotGrid.WorldToPlot( ground, out var px, out var py )
		     && !PlotGrid.IsHome( px, py ) && (plots.IsOwned( px, py ) || plots.IsBuyable( px, py )) )
			Consider( new PlotSelectable( px, py ) );

		return best;
	}

	protected override void OnUpdate()
	{
		var core = GameCore.Instance;
		if ( core is null ) return;

		if ( Selected is not null && !Selected.IsAlive )
			Selected = null;

		if ( core.IsUiBlocking ) return;
		if ( core.Phase != GamePhase.Day ) return;

		UpdateGhost();
		HandleInput();
	}

	private void HandleInput()
	{
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
			else
			{
				var hit = PickSelectable( ground );
				if ( hit is not null ) Select( hit );
				else Deselect();
			}
		}

		if ( Input.Pressed( "Attack2" ) || Input.Pressed( "Cancel" ) )
		{
			if ( PlacementMode is not null )
				CancelPlacement();
			else if ( MovingBuilding is not null )
				CancelMove();
			else
				Deselect();
		}
	}

	private void UpdateGhost()
	{
		var moving = MovingBuilding;
		if ( PlacementMode is null && moving is null )
		{
			_ghostGo?.Destroy();
			_ghostGo = null;
			return;
		}

		EnsureGhost();
		var cam = OutpostCamera.Instance;
		if ( cam is null || !cam.ScreenToGround( Mouse.Position, out var ground ) )
			return;

		if ( !BuildGrid.WorldToCell( ground, out var cx, out var cy ) )
			return;

		if ( cx == _ghostCellX && cy == _ghostCellY ) return;
		_ghostCellX = cx;
		_ghostCellY = cy;

		var pos = BuildGrid.CellToWorld( cx, cy );
		var id = PlacementMode ?? moving.Type;
		var def = BuildableCatalog.Get( id );
		_ghostGo.WorldPosition = pos.WithZ( pos.z + def.VisualSize.z * 0.5f );
		_ghostGo.LocalScale = MeshPrimitives.BoxScale( def.VisualSize );

		var valid = CanPlaceAt( cx, cy, id, moving );
		var afford = PlacementMode is not null
			? GameCore.Instance?.Wallet.Scrap >= PlaceCost( def )
			: true;
		_ghostRenderer.Tint = valid && afford == true
			? def.Tint.WithAlpha( 0.55f )
			: new Color( 1f, 0.2f, 0.2f, 0.45f );
	}

	private void EnsureGhost()
	{
		if ( _ghostGo.IsValid() ) return;

		_ghostGo = new GameObject( true, "PlacementGhost" );
		_ghostRenderer = _ghostGo.Components.Create<ModelRenderer>();
		_ghostRenderer.Model = MeshPrimitives.Box;
		_ghostRenderer.MaterialOverride = MeshPrimitives.Mat;
	}

	private void PlaceInternal( BuildableId id, int cellX, int cellY, int level, float health, int placeOrder, bool fromSave )
	{
		if ( placeOrder <= 0 )
			placeOrder = ++_nextPlaceOrder;
		else if ( placeOrder > _nextPlaceOrder )
			_nextPlaceOrder = placeOrder;

		var go = new GameObject( true, $"Building_{id}_{cellX}_{cellY}" );
		var building = go.Components.Create<PlacedBuilding>();
		building.Setup( id, cellX, cellY, level, health, placeOrder );
		_cells[(cellX, cellY)] = building;
	}

	private void RemoveBuilding( PlacedBuilding building )
	{
		_cells.Remove( (building.CellX, building.CellY) );
		building.GameObject.Destroy();
	}
}
