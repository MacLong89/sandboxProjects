namespace Fauna2;

using Fauna2.UI;

public enum BuildMode
{
	None,
	Place,
	PlaceAnimal,
	PlaceCarried,
	MoveAnimal,
	Demolish,
	ExpandLand,
}

/// <summary>
/// The local player's build tool. Renders a placement ghost with instant
/// client-side validation feedback, then sends requests to the host which
/// re-validates authoritatively. Lives next to the camera; entirely local.
/// </summary>
public sealed class BuildController : Component
{
	public static BuildController Instance { get; private set; }

	public BuildMode Mode { get; private set; } = BuildMode.None;
	public PlaceableDefinition PlacingDef { get; private set; }
	public AnimalDefinition PlacingAnimal { get; private set; }
	public int PlacingCarrySlot { get; private set; } = -1;
	public AnimalComponent MovingAnimal { get; private set; }

	/// <summary>Status line shown in the HUD hint bar.</summary>
	public string HintText { get; private set; } = "";
	public bool GhostValid { get; private set; }

	private GameObject _ghost;
	private IReadOnlyList<SpriteRenderer> _expandPlotHighlight;
	private float _yaw;
	private Vector3 _cursorWorld;
	private bool _cursorOnGround;
	private TimeUntil _pathPlaceCooldown;
	private bool _attack1WasDown;
	private bool _hasLastPathCell;
	private Vector3 _lastPathCell;

	protected override void OnAwake()
	{
		Instance = this;
	}

	protected override void OnStart()
	{
		// WorldInput forwards LMB/RMB to attack1/attack2 when the cursor isn't over UI.
		// Without this, ScreenPanel eats mouse buttons and placement never fires.
		var worldInput = GameObject.GetOrAddComponent<WorldInput>();
		worldInput.LeftMouseAction = "attack1";
		worldInput.RightMouseAction = "attack2";

		var camera = Components.Get<CameraComponent>();
		foreach ( var screenPanel in Scene.GetAllComponents<ScreenPanel>() )
		{
			if ( camera.IsValid() )
				screenPanel.TargetCamera = camera;
		}

		UiWorldProjection.BindScreenPanelCamera( Scene, camera );

		Log.Info( "[Fauna2 Build] BuildController ready — WorldInput wired to attack1/attack2." );
	}
	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
		ClearGhost();
	}

	// ── Mode switching (called from UI) ─────────────────────

	public void BeginPlace( PlaceableDefinition def )
	{
		if ( PlayerState.Local?.IsZooOwner != true ) return;
		CancelMode();
		Mode = BuildMode.Place;
		PlacingDef = def;
		_yaw = 0;
		_hasLastPathCell = false;
		BuildGhostFor( def );
		Log.Info( $"[Fauna2 Build] BeginPlace '{def?.DisplayName}' id={def?.ResourceName} habitat={def?.IsHabitat} footprint={GameConstants.FormatTiles( def?.EffectiveFootprint ?? Vector2.Zero )} tiles snap={MathF.Max( GameConstants.TileSize, def?.GridSnap ?? GameConstants.TileSize ) / GameConstants.TileSize:0.##} tiles." );
	}

	public void BeginPlaceAnimal( AnimalDefinition def )
	{
		CancelMode();
		Mode = BuildMode.PlaceAnimal;
		PlacingAnimal = def;
		PlacingCarrySlot = -1;
		BuildAnimalGhost( def );
	}

	public bool BeginPlaceCarried( int carrySlot )
	{
		if ( PlayerState.Local?.IsZooOwner != true ) return false;

		var inv = PlayerInventory.Local;
		var speciesId = inv?.GetCarriedAt( carrySlot );
		var def = string.IsNullOrEmpty( speciesId ) ? null : Defs.Animal( speciesId );
		if ( def is null )
		{
			Log.Warning( $"[Fauna2 Build] BeginPlaceCarried slot={carrySlot} failed — species='{speciesId ?? ""}' inv={inv is not null}." );
			return false;
		}

		CancelMode();
		Mode = BuildMode.PlaceCarried;
		PlacingAnimal = def;
		PlacingCarrySlot = carrySlot;
		BuildAnimalGhost( def );
		Log.Info( $"[Fauna2 Build] BeginPlaceCarried slot={carrySlot} species={speciesId} ({def.DisplayName})." );
		return true;
	}

	public void BeginMoveAnimal( AnimalComponent animal )
	{
		if ( PlayerState.Local?.IsZooOwner != true ) return;
		CancelMode();
		if ( animal?.Definition is null ) return;

		Mode = BuildMode.MoveAnimal;
		MovingAnimal = animal;
		BuildAnimalGhost( animal.Definition, animal.Variant?.Tint ?? animal.Definition.BodyTint );
	}

	public void BeginDemolish()
	{
		if ( PlayerState.Local?.IsZooOwner != true ) return;
		CancelMode();
		Mode = BuildMode.Demolish;
	}

	public void BeginExpandLand()
	{
		if ( PlayerState.Local?.IsZooOwner != true ) return;
		CancelMode();
		Mode = BuildMode.ExpandLand;
	}

	public void CancelMode()
	{
		Mode = BuildMode.None;
		PlacingDef = null;
		PlacingAnimal = null;
		PlacingCarrySlot = -1;
		MovingAnimal = null;
		HintText = "";
		ClearGhost();
	}

	private void FinishPlacement()
	{
		CancelMode();

		if ( UiState.ActivePage is UiPage.Build or UiPage.Market )
			UiState.Close();
	}

	// ── Update loop ─────────────────────────────────────────

	protected override void OnUpdate()
	{
		if ( PlayerState.Local?.IsZooOwner != true )
		{
			if ( Mode != BuildMode.None )
				CancelMode();
			return;
		}

		if ( Mode == BuildMode.None ) return;

		if ( Input.Pressed( "attack2" ) || Input.Pressed( "Cancel" ) )
		{
			CancelMode();
			return;
		}

		UpdateCursor();

		switch ( Mode )
		{
			case BuildMode.Place: TickPlace(); break;
			case BuildMode.PlaceAnimal: TickPlaceAnimal(); break;
			case BuildMode.PlaceCarried: TickPlaceCarried(); break;
			case BuildMode.MoveAnimal: TickMoveAnimal(); break;
			case BuildMode.Demolish: TickDemolish(); break;
			case BuildMode.ExpandLand: TickExpandLand(); break;
		}
	}

	private void UpdateCursor()
	{
		_cursorOnGround = TryGetGroundPoint( out _cursorWorld );
	}

	/// <summary>
	/// Project the mouse onto the zoo floor (z=0). Uses the ground plane first so
	/// guest sprites, props and pick colliders never steal the build cursor.
	/// </summary>
	private bool TryGetGroundPoint( out Vector3 worldPoint )
	{
		worldPoint = default;

		var camera = Scene.Camera;
		if ( !camera.IsValid() ) return false;

		var ray = camera.ScreenPixelToRay( Mouse.Position );

		if ( MathF.Abs( ray.Forward.z ) >= 0.0001f )
		{
			var t = -ray.Position.z / ray.Forward.z;
			if ( t >= 0f )
			{
				worldPoint = ray.Project( t ).WithZ( 0 );
				return true;
			}
		}

		var trace = Scene.Trace.Ray( ray, 50_000f );
		if ( _ghost.IsValid() )
			trace = trace.IgnoreGameObjectHierarchy( _ghost );

		var result = trace.Run();
		if ( result.Hit && result.GameObject.IsValid() && result.GameObject.Tags.Has( "ground" ) )
		{
			worldPoint = result.HitPosition.WithZ( 0 );
			return true;
		}

		return false;
	}

	private bool WasLeftClickPressed()
	{
		var down = Input.Down( "attack1" );
		var pressed = down && !_attack1WasDown;
		_attack1WasDown = down;
		return pressed || Input.Pressed( "attack1" );
	}

	private bool ClickedWorld()
	{
		if ( !_cursorOnGround || !WasLeftClickPressed() ) return false;
		if ( UiState.ActivePage != UiPage.None ) return false;
		return !UiState.PointerOverUI;
	}

	private void LogBuildClick( string phase, bool ghostValid, Vector3 pos, string extra = "" )
	{
		if ( !UiState.BuildDebug ) return;

		Log.Info( $"[Fauna2 Build] {phase}: " +
			$"attack1 pressed={Input.Pressed( "attack1" )} down={Input.Down( "attack1" )} " +
			$"onGround={_cursorOnGround} overUI={UiState.PointerOverUI} blocker='{UiState.PointerBlocker}' " +
			$"page={UiState.ActivePage} ghostValid={ghostValid} pos={pos} " +
			$"buildSys={(BuildSystem.Instance is not null)} host={Networking.IsHost} " +
			$"plots={PlotSystem.Instance?.PlotCount ?? -1} money=${ZooState.Instance?.Money ?? -1} {extra}" );
	}

	// ── Place buildable ─────────────────────────────────────

	private void TickPlace()
	{
		if ( PlacingDef is null ) { CancelMode(); return; }

		var isPath = PlacingDef.IsPathTile;

		if ( Input.Pressed( "RotatePiece" ) )
			_yaw = (_yaw + PlacingDef.RotationStep) % 360f;

		var pos = BuildSnap.ResolvePlacement( _cursorWorld, PlacingDef, _yaw, PlotSystem.Instance );

		var spatialOk = BuildValidation.CanPlace( PlacingDef, pos, out var error, out var resolvedPos, _yaw );
		if ( PlacingDef.IsPathTile )
			pos = resolvedPos;
		var affordable = ZooState.Instance?.CanAfford( PlacingDef.Cost ) ?? false;
		GhostValid = spatialOk && affordable && _cursorOnGround;

		HintText = !affordable ? $"Need ${PlacingDef.Cost:n0}"
			: !spatialOk ? error
			: isPath
				? $"{PlacingDef.DisplayName} — ${PlacingDef.Cost:n0}   [Click/Drag] lay path  [Esc/RMB] done"
				: $"{PlacingDef.DisplayName} — ${PlacingDef.Cost:n0}   [Click] place  [R] rotate  [Esc/RMB] cancel";

		UpdateGhostTransform( pos, _yaw, GhostValid );

		if ( PlacingDef.IsEntrance )
			TintEntranceGhost( BuildValidation.IsNearOwnedPlotEdge( pos, _yaw ) );

		var pathBrush = isPath && Input.Down( "attack1" );
		var movedToNewPathCell = !_hasLastPathCell || !BuildSnap.SamePathTile( pos, _lastPathCell );
		var canPlaceNow = ( ClickedWorld() && GhostValid )
			|| ( pathBrush && GhostValid && _pathPlaceCooldown && movedToNewPathCell );

		if ( WasLeftClickPressed() )
			LogBuildClick( "click", GhostValid, pos, $"spatial={spatialOk} afford={affordable} err='{error}'" );

		if ( canPlaceNow && GhostValid )
		{
			var build = BuildSystem.Instance;
			if ( build is null )
			{
				Log.Warning( "[Fauna2 Build] BuildSystem.Instance is null!" );
				UiState.PushToast( "Zoo systems still loading — try again.", "hourglass_empty" );
				return;
			}

			Log.Info( $"[Fauna2 Build] Placing '{PlacingDef.DisplayName}' at {pos} yaw={_yaw}" );

			if ( Networking.IsHost )
				build.Place( PlacingDef, pos, _yaw );
			else
				build.RequestPlace( Defs.IdOf( PlacingDef ), pos, _yaw );

			if ( isPath )
			{
				_hasLastPathCell = true;
				_lastPathCell = pos;
			}

			if ( pathBrush )
				_pathPlaceCooldown = 0.12f;
			else if ( !isPath )
				FinishPlacement();
		}
		else if ( WasLeftClickPressed() && _cursorOnGround && !GhostValid && !pathBrush )
		{
			var reason = UiState.ActivePage != UiPage.None ? "Close the menu first"
				: UiState.PointerOverUI ? $"Mouse is over UI ({UiState.PointerBlocker})"
				: !affordable ? $"Need ${PlacingDef.Cost:n0}"
				: !spatialOk ? error
				: "Can't place here";
			Log.Info( $"[Fauna2 Build] Click rejected: {reason} (pos={pos} plots={PlotSystem.Instance?.PlotCount ?? -1} owned={PlotSystem.Instance?.IsWorldPointOnOwnedPlot( pos )})" );
			UiState.PushToast( reason, "block" );
			ZooSoundEffects.PlayPlacementError();
		}
		else if ( WasLeftClickPressed() && !ClickedWorld() )
		{
			LogBuildClick( "click-blocked", GhostValid, pos );
		}
	}

	// ── Place animal ────────────────────────────────────────

	private void TickMoveAnimal()
	{
		if ( MovingAnimal is null || !MovingAnimal.IsValid() ) { CancelMode(); return; }

		var def = MovingAnimal.Definition;
		if ( def is null ) { CancelMode(); return; }

		var habitat = HabitatRegistry.FindAt( _cursorWorld );
		string moveError = null;
		var hasRoom = habitat is not null && habitat.TryAccept( def, MovingAnimal, out moveError );
		var transferring = habitat is not null && habitat.HabitatId != MovingAnimal.HabitatId;

		GhostValid = habitat is not null && hasRoom && _cursorOnGround;

		HintText = habitat is null ? "Click inside a habitat to move the animal"
			: !hasRoom ? moveError ?? "Can't move here"
			: transferring
				? $"Move {MovingAnimal.AnimalName} to {habitat.Definition?.DisplayName}   [Esc/RMB] cancel"
				: $"Relocate {MovingAnimal.AnimalName} here   [Esc/RMB] cancel";

		UpdateGhostTransform( _cursorWorld, _yaw, GhostValid );

		if ( ClickedWorld() && GhostValid )
		{
			var animalId = MovingAnimal.AnimalId;
			AnimalSystem.Instance?.RequestMoveAnimal( animalId, _cursorWorld );
			FinishPlacement();
			UiState.SelectAnimal( animalId );
		}
	}

	private void TickPlaceAnimal()
	{
		if ( PlacingAnimal is null ) { CancelMode(); return; }

		var habitat = HabitatRegistry.FindAt( _cursorWorld );
		var cost = AnimalSystem.Instance?.GetPurchaseCost( PlacingAnimal ) ?? PlacingAnimal.Cost;
		var affordable = ZooState.Instance?.CanAfford( cost ) ?? false;
		string placeError = null;
		var canPlace = habitat is not null && habitat.TryAccept( PlacingAnimal, null, out placeError );

		GhostValid = canPlace && affordable && _cursorOnGround;

		HintText = habitat is null ? $"Needs a {AnimalHabitatRules.RequirementText( PlacingAnimal )} — click inside one"
			: !canPlace ? placeError ?? "Can't place here"
			: !affordable ? $"Need ${cost:n0}"
			: $"Release {PlacingAnimal.DisplayName} here — ${cost:n0}   [Esc/RMB] cancel";

		UpdateGhostTransform( _cursorWorld, _yaw, GhostValid );

		if ( ClickedWorld() && GhostValid )
		{
			AnimalSystem.Instance?.RequestBuyAnimal( Defs.IdOf( PlacingAnimal ), _cursorWorld );
			FinishPlacement();
		}
	}

	private void TickPlaceCarried()
	{
		if ( PlacingAnimal is null || PlacingCarrySlot < 0 ) { CancelMode(); return; }

		var plots = PlotSystem.Instance;
		var atCap = plots is not null && AnimalRegistry.Count >= plots.AnimalCap;
		var habitat = HabitatRegistry.FindAt( _cursorWorld );
		string placeError = null;
		var canPlace = !atCap && habitat is not null && habitat.TryAccept( PlacingAnimal, null, out placeError );

		GhostValid = canPlace && _cursorOnGround;

		HintText = atCap ? "Animal capacity reached — buy more land!"
			: habitat is null ? $"Needs a {AnimalHabitatRules.RequirementText( PlacingAnimal )} — click inside a habitat"
			: !canPlace ? placeError ?? "Can't place here"
			: $"Release {PlacingAnimal.DisplayName} here   [Esc/RMB] cancel";

		UpdateGhostTransform( _cursorWorld, _yaw, GhostValid );

		if ( ClickedWorld() && GhostValid )
		{
			CatchSystem.Instance?.RequestPlaceCarriedAtSlot( _cursorWorld, PlacingCarrySlot );
			FinishPlacement();
		}
	}

	// ── Demolish ────────────────────────────────────────────

	private void TickDemolish()
	{
		var placeable = PlaceableRegistry.Nearest( _cursorWorld, 90f );
		var habitat = placeable is null ? HabitatRegistry.FindAt( _cursorWorld ) : null;

		HintText = placeable is not null ? $"[Click] demolish {placeable.Definition?.DisplayName}"
			: habitat is not null ? "[Click] demolish habitat (must be empty)"
			: "Hover something to demolish   [Esc/RMB] exit";

		if ( ClickedWorld() && (placeable is not null || habitat is not null) )
		{
			BuildSystem.Instance?.RequestDemolish( _cursorWorld );
		}
	}

	// ── Expand land ─────────────────────────────────────────

	private void TickExpandLand()
	{
		var plots = PlotSystem.Instance;
		if ( !plots.IsValid() ) return;

		var (px, py) = PlotSystem.PlotAt( _cursorWorld );
		var buyable = plots.IsBuyable( px, py );
		var cost = plots.NextPlotCost();

		HintText = buyable
			? $"Buy this plot for ${cost:n0}?   [Click] confirm  [Esc/RMB] cancel"
			: $"Hover a plot bordering your land — next plot costs ${cost:n0}   [Esc/RMB] cancel";

		EnsureExpandGhost();
		if ( _ghost.IsValid() )
		{
			_ghost.Enabled = _cursorOnGround;
			_ghost.WorldPosition = PlotSystem.PlotCenter( px, py ) + new Vector3( 0, 0, 4f );
			TintExpandPlot( buyable );
		}

		if ( ClickedWorld() && buyable )
		{
			plots.RequestBuyPlot( px, py );
			FinishPlacement();
		}
	}

	// ── Ghost helpers ───────────────────────────────────────

	private void BuildGhostFor( PlaceableDefinition def )
	{
		ClearGhost();
		_ghost = new GameObject( true, "Build Ghost" );

		if ( def.IsHabitat )
		{
			var visuals = new GameObject( _ghost, true, "Visuals" );
			HabitatGroundOverlay.Attach( visuals, def.HabitatSize, def.HabitatBiome, alpha: 0.65f );
			HabitatFenceRenderer.Attach( visuals, Vector3.Zero, def.HabitatSize, alpha: 0.85f, collision: false );
		}
		else if ( def.IsPathTile )
		{
			var visuals = new GameObject( _ghost, true, "Visuals" );
			PathGroundOverlay.Attach( visuals, def, alpha: 0.65f, parentWorldZ: 0f );
		}
		else
		{
			PlaceableComponent.BuildVisualTree( def, _ghost, alpha: 0.65f, dynamicDepthSort: true );
		}
	}

	private void BuildAnimalGhost( AnimalDefinition def ) =>
		BuildAnimalGhost( def, def.BodyTint );

	private void BuildAnimalGhost( AnimalDefinition def, Color tint )
	{
		ClearGhost();
		_ghost = new GameObject( true, "Animal Ghost" );
		CritterSpriteVisual.Build( _ghost, def, tint );
		_ghost.LocalScale = Vector3.One * 0.65f;
	}

	private void EnsureExpandGhost()
	{
		if ( _ghost.IsValid() ) return;

		_ghost = new GameObject( true, "Expand Ghost" );
		_expandPlotHighlight = PlotHighlightOverlay.Attach(
			_ghost,
			GameConstants.PlotSize,
			new Color( 0.72f, 0.74f, 0.78f, 0.35f ),
			parentWorldZ: 4f );
	}

	private void TintExpandPlot( bool buyable )
	{
		if ( _expandPlotHighlight is null ) return;

		var tint = buyable
			? new Color( 0.45f, 0.92f, 0.55f, 0.42f )
			: new Color( 0.72f, 0.74f, 0.78f, 0.35f );

		foreach ( var tile in _expandPlotHighlight )
		{
			if ( !tile.IsValid() ) continue;
			tile.Color = tint;
		}
	}

	private void UpdateGhostTransform( Vector3 position, float yaw, bool valid )
	{
		if ( !_ghost.IsValid() ) return;
		_ghost.Enabled = _cursorOnGround;
		_ghost.WorldPosition = position.WithZ( 0 );
		_ghost.WorldRotation = Rotation.FromYaw( yaw );
		if ( PlacingDef?.IsPathTile == true )
		{
			foreach ( var child in _ghost.Children )
			{
				if ( child.Name != "Visuals" ) continue;
				PathGroundOverlay.SyncParentElevation( child, _ghost.WorldPosition.z );
			}
		}
		TintGhost( valid );
	}

	private void TintGhost( bool valid )
	{
		if ( !_ghost.IsValid() ) return;

		var color = valid
			? new Color( 1f, 1f, 1f, 0.65f )
			: new Color( 1f, 0.42f, 0.42f, 0.55f );
		ApplyGhostSpriteTint( _ghost, color );
	}

	private static void ApplyGhostSpriteTint( GameObject root, Color color )
	{
		foreach ( var renderer in root.GetComponentsInChildren<SpriteRenderer>() )
		{
			if ( !renderer.IsValid() ) continue;
			if ( renderer.GameObject.Tags.Has( "footprint_preview" ) )
				continue;
			renderer.Color = color;
		}
	}

	private void TintEntranceGhost( bool onEdge )
	{
		if ( !_ghost.IsValid() ) return;

		if ( !GhostValid )
		{
			TintGhost( false );
			return;
		}

		var color = onEdge
			? new Color( 1f, 1f, 1f, 0.65f )
			: new Color( 1f, 0.82f, 0.45f, 0.55f );
		ApplyGhostSpriteTint( _ghost, color );
	}

	private void ClearGhost()
	{
		_ghost?.Destroy();
		_ghost = null;
		_expandPlotHighlight = null;
	}
}
