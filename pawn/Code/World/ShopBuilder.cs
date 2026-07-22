namespace PawnShop;

/// <summary>
/// Builds the entire pawn shop out of tinted primitives: room, counter, displays,
/// backroom, repair bench, signage, and the street outside. Keeps display props in
/// sync with inventory.
/// </summary>
public sealed class ShopBuilder : Component
{
	public static readonly Color FloorColor = new( 0.52f, 0.38f, 0.24f );
	public static readonly Color WallColor = new( 0.82f, 0.72f, 0.55f );
	public static readonly Color TrimColor = new( 0.45f, 0.30f, 0.18f );
	public static readonly Color CounterColor = new( 0.50f, 0.32f, 0.16f );
	public static readonly Color ShelfColor = new( 0.58f, 0.40f, 0.22f );
	public static readonly Color GlassColor = new( 0.65f, 0.85f, 0.92f, 0.5f );
	public static readonly Color StreetColor = new( 0.35f, 0.35f, 0.38f );
	public static readonly Color WoodDarkColor = new( 0.32f, 0.2f, 0.11f );

	private GameObject _static;      // never rebuilt
	private GameObject _upgradeable; // rebuilt when upgrades change
	private GameObject _displayItems;
	private GameObject _counterItem;
	private TextRenderer _doorSign;

	private GameManager Game => GameManager.Instance;

	protected override void OnStart()
	{
		BuildStatic();
		RebuildUpgradeGeometry();
		RefreshDisplays();
		SetDoorSign( false );
	}

	// ==================================================================== Static shell

	private void BuildStatic()
	{
		_static = new GameObject( GameObject, true, "Static" );

		var w = ShopLayout.HalfW;
		var d = ShopLayout.HalfD;
		var h = ShopLayout.WallH;
		var t = ShopLayout.WallT;

		// Floor + ceiling.
		MeshKit.Spawn( _static, "Floor", new Vector3( 0, 0, -6 ), new Vector3( w * 2, d * 2, 12 ), FloorColor, default, collide: true );
		MeshKit.Spawn( _static, "Ceiling", new Vector3( 0, 0, h + 6 ), new Vector3( w * 2, d * 2, 12 ), new Color( 0.9f, 0.86f, 0.78f ) );

		// Ceiling beams give the box a read as "old shop".
		for ( var i = -2; i <= 2; i++ )
			MeshKit.Spawn( _static, $"Beam{i}", new Vector3( i * 150f, 0, h - 4 ), new Vector3( 14, d * 2, 8 ), TrimColor );

		// Floorboard seams: thin darker strips so the floor isn't one flat slab.
		for ( var i = -3; i <= 3; i++ )
			MeshKit.Spawn( _static, $"Board{i}", new Vector3( i * 96f, 0, 0.35f ), new Vector3( 2.5f, d * 2 - 20, 0.7f ), new Color( 0.42f, 0.30f, 0.18f ) );

		// Rug in the customer area (border + inner panel).
		MeshKit.Spawn( _static, "RugBorder", new Vector3( 0, -60, 0.6f ), new Vector3( 300, 260, 1 ), new Color( 0.38f, 0.16f, 0.14f ) );
		MeshKit.Spawn( _static, "Rug", new Vector3( 0, -60, 1.2f ), new Vector3( 266, 226, 1 ), new Color( 0.62f, 0.28f, 0.24f ) );
		MeshKit.Spawn( _static, "RugCenter", new Vector3( 0, -60, 1.8f ), new Vector3( 130, 110, 1 ), new Color( 0.72f, 0.44f, 0.28f ) );

		// Walls. Front (-y) and back (+y) both have door gaps.
		MeshKit.Spawn( _static, "WallLeft", new Vector3( -w, 0, h * 0.5f ), new Vector3( t, d * 2, h ), WallColor, default, collide: true );
		MeshKit.Spawn( _static, "WallRight", new Vector3( w, 0, h * 0.5f ), new Vector3( t, d * 2, h ), WallColor, default, collide: true );

		var doorHalf = ShopLayout.DoorWidth * 0.5f;
		var frontSeg = w - doorHalf;
		MeshKit.Spawn( _static, "WallFrontL", new Vector3( -(doorHalf + frontSeg * 0.5f), -d, h * 0.5f ), new Vector3( frontSeg, t, h ), WallColor, default, collide: true );
		MeshKit.Spawn( _static, "WallFrontR", new Vector3( doorHalf + frontSeg * 0.5f, -d, h * 0.5f ), new Vector3( frontSeg, t, h ), WallColor, default, collide: true );
		MeshKit.Spawn( _static, "DoorHeader", new Vector3( 0, -d, h - 15 ), new Vector3( ShopLayout.DoorWidth + 10, t, 30 ), TrimColor, default, collide: true );

		// Back wall with staff door gap (crate alley).
		var backDoorHalf = 40f;
		var backSeg = w - backDoorHalf;
		MeshKit.Spawn( _static, "WallBackL", new Vector3( -(backDoorHalf + backSeg * 0.5f), d, h * 0.5f ), new Vector3( backSeg, t, h ), WallColor, default, collide: true );
		MeshKit.Spawn( _static, "WallBackR", new Vector3( backDoorHalf + backSeg * 0.5f, d, h * 0.5f ), new Vector3( backSeg, t, h ), WallColor, default, collide: true );
		MeshKit.Spawn( _static, "BackDoorWallHeader", new Vector3( 0, d, h - 15 ), new Vector3( backDoorHalf * 2 + 10, t, 30 ), TrimColor, default, collide: true );

		// Door frame posts.
		MeshKit.Spawn( _static, "DoorPostL", new Vector3( -doorHalf - 4, -d, (h - 30) * 0.5f ), new Vector3( 8, t + 2, h - 30 ), TrimColor );
		MeshKit.Spawn( _static, "DoorPostR", new Vector3( doorHalf + 4, -d, (h - 30) * 0.5f ), new Vector3( 8, t + 2, h - 30 ), TrimColor );

		// Baseboards along the interior walls.
		MeshKit.Spawn( _static, "BaseBack", new Vector3( 0, d - t, 5 ), new Vector3( w * 2 - 10, 3, 10 ), TrimColor );
		MeshKit.Spawn( _static, "BaseLeft", new Vector3( -w + t, 0, 5 ), new Vector3( 3, d * 2 - 10, 10 ), TrimColor );
		MeshKit.Spawn( _static, "BaseRight", new Vector3( w - t, 0, 5 ), new Vector3( 3, d * 2 - 10, 10 ), TrimColor );

		// Windows on the front wall (glass panes with frames and sills).
		foreach ( var wx in new[] { -180f, 180f } )
		{
			var side = wx < 0 ? "L" : "R";
			MeshKit.Spawn( _static, $"Window{side}", new Vector3( wx, -d - 1, 82 ), new Vector3( 150, t - 4, 80 ), GlassColor );
			MeshKit.Spawn( _static, $"WinFrameT{side}", new Vector3( wx, -d - 2, 124 ), new Vector3( 160, t - 2, 6 ), TrimColor );
			MeshKit.Spawn( _static, $"WinFrameB{side}", new Vector3( wx, -d - 3, 40 ), new Vector3( 164, t + 4, 7 ), TrimColor );
			MeshKit.Spawn( _static, $"WinMullion{side}", new Vector3( wx, -d - 2, 82 ), new Vector3( 5, t - 2, 80 ), TrimColor );
			// Striped awning outside each window.
			for ( var a = 0; a < 5; a++ )
			{
				var stripe = a % 2 == 0 ? new Color( 0.75f, 0.28f, 0.22f ) : new Color( 0.92f, 0.88f, 0.78f );
				MeshKit.Spawn( _static, $"Awning{side}{a}", new Vector3( wx - 80 + 8 + a * 32, -d - 26, 132 ), new Vector3( 32, 46, 4 ), stripe, new Angles( -28, 0, 0 ) );
			}
		}

		// Classic three-ball pawnbroker sign over the door.
		MeshKit.Spawn( _static, "SignArm", new Vector3( 0, -d - 20, h + 4 ), new Vector3( 6, 40, 5 ), new Color( 0.2f, 0.18f, 0.16f ) );
		MeshKit.SpawnSphere( _static, "Ball1", new Vector3( -16, -d - 34, h - 8 ), 15f, new Color( 0.95f, 0.78f, 0.25f ) );
		MeshKit.SpawnSphere( _static, "Ball2", new Vector3( 16, -d - 34, h - 8 ), 15f, new Color( 0.95f, 0.78f, 0.25f ) );
		MeshKit.SpawnSphere( _static, "Ball3", new Vector3( 0, -d - 34, h - 30 ), 15f, new Color( 0.95f, 0.78f, 0.25f ) );

		// Street + sidewalk outside.
		MeshKit.Spawn( _static, "Sidewalk", new Vector3( 0, -d - 120, -5 ), new Vector3( w * 2 + 200, 240, 10 ), new Color( 0.6f, 0.58f, 0.55f ), default, collide: true );
		MeshKit.Spawn( _static, "Curb", new Vector3( 0, -d - 238, -3 ), new Vector3( w * 2 + 200, 6, 8 ), new Color( 0.5f, 0.48f, 0.46f ) );
		MeshKit.Spawn( _static, "Street", new Vector3( 0, -d - 380, -6 ), new Vector3( w * 2 + 600, 280, 10 ), StreetColor, default, collide: true );
		// Sidewalk seams.
		for ( var i = -4; i <= 4; i++ )
			MeshKit.Spawn( _static, $"Seam{i}", new Vector3( i * 90f, -d - 120, 0.2f ), new Vector3( 2, 236, 0.5f ), new Color( 0.5f, 0.48f, 0.45f ) );
		// Road centerline dashes.
		for ( var i = -4; i <= 4; i++ )
			MeshKit.Spawn( _static, $"Dash{i}", new Vector3( i * 110f, -d - 380, -0.4f ), new Vector3( 46, 7, 0.6f ), new Color( 0.85f, 0.8f, 0.55f ) );
		// Crosswalk in front of the door.
		for ( var i = 0; i < 6; i++ )
			MeshKit.Spawn( _static, $"Cross{i}", new Vector3( -50 + i * 22f, -d - 300, -0.4f ), new Vector3( 12, 90, 0.6f ), new Color( 0.8f, 0.78f, 0.72f ) );

		// The block across the street gets storefronts instead of a blank slab.
		MeshKit.Spawn( _static, "OppositeBlock", new Vector3( 0, -d - 620, 90 ), new Vector3( w * 2 + 600, 200, 180 ), new Color( 0.55f, 0.48f, 0.42f ), default, collide: true );
		for ( var i = -3; i <= 3; i++ )
		{
			var bx = i * 170f;
			var shade = new ColorHsv( 20f + ((i + 3) * 47f) % 50f, 0.25f, 0.5f + (i % 2) * 0.12f );
			MeshKit.Spawn( _static, $"OppFacade{i}", new Vector3( bx, -d - 518, 80 ), new Vector3( 150, 6, 160 ), shade );
			MeshKit.Spawn( _static, $"OppWin{i}", new Vector3( bx, -d - 514, 95 ), new Vector3( 100, 4, 50 ), new Color( 0.4f, 0.55f, 0.65f ) );
			MeshKit.Spawn( _static, $"OppDoor{i}", new Vector3( bx + 40, -d - 514, 35 ), new Vector3( 34, 4, 70 ), new Color( 0.28f, 0.2f, 0.14f ) );
			MeshKit.Spawn( _static, $"OppSign{i}", new Vector3( bx, -d - 512, 138 ), new Vector3( 110, 4, 18 ), new ColorHsv( ((i + 3) * 77f) % 360f, 0.45f, 0.5f ) );
		}

		// Street lamps on the sidewalk.
		foreach ( var lx in new[] { -260f, 260f } )
		{
			MeshKit.Spawn( _static, $"LampPost{lx}", new Vector3( lx, -d - 220, 70 ), new Vector3( 7, 7, 140 ), new Color( 0.18f, 0.2f, 0.22f ), default, collide: true );
			MeshKit.Spawn( _static, $"LampHead{lx}", new Vector3( lx, -d - 210, 142 ), new Vector3( 14, 26, 8 ), new Color( 0.18f, 0.2f, 0.22f ) );
			MeshKit.SpawnSphere( _static, $"LampBulb{lx}", new Vector3( lx, -d - 202, 137 ), 9f, new Color( 1f, 0.92f, 0.7f ) );
		}

		// Hydrant + planters.
		MeshKit.Spawn( _static, "HydrantBody", new Vector3( 90, -d - 200, 12 ), new Vector3( 12, 12, 24 ), new Color( 0.8f, 0.25f, 0.2f ) );
		MeshKit.SpawnSphere( _static, "HydrantCap", new Vector3( 90, -d - 200, 26 ), 11f, new Color( 0.7f, 0.2f, 0.16f ) );
		MeshKit.Spawn( _static, "HydrantNozzle", new Vector3( 90, -d - 192, 16 ), new Vector3( 5, 6, 5 ), new Color( 0.85f, 0.75f, 0.3f ) );
		foreach ( var px in new[] { -120f, 220f } )
		{
			MeshKit.Spawn( _static, $"Planter{px}", new Vector3( px, -d - 205, 9 ), new Vector3( 40, 26, 18 ), new Color( 0.45f, 0.35f, 0.28f ), default, collide: true );
			MeshKit.Spawn( _static, $"PlanterSoil{px}", new Vector3( px, -d - 205, 18.6f ), new Vector3( 36, 22, 2 ), new Color( 0.25f, 0.18f, 0.12f ) );
			MeshKit.SpawnSphere( _static, $"Bush{px}A", new Vector3( px - 8, -d - 205, 27 ), 18f, new Color( 0.3f, 0.5f, 0.25f ) );
			MeshKit.SpawnSphere( _static, $"Bush{px}B", new Vector3( px + 8, -d - 208, 25 ), 14f, new Color( 0.36f, 0.55f, 0.28f ) );
		}

		// A parked sedan across the street (kit-recipe proportions, ~20 parts light).
		BuildParkedCar( _static, new Vector3( -170, -d - 420, 0 ), new Color( 0.65f, 0.3f, 0.25f ) );
		BuildParkedCar( _static, new Vector3( 210, -d - 340, 0 ), new Color( 0.3f, 0.42f, 0.55f ), flip: true );

		// Neighboring buildings box the street in so nobody wanders off the map.
		MeshKit.Spawn( _static, "NeighborL", new Vector3( -w - 108, -d - 240, 80 ), new Vector3( 200, 520, 160 ), new Color( 0.62f, 0.5f, 0.4f ), default, collide: true );
		MeshKit.Spawn( _static, "NeighborR", new Vector3( w + 108, -d - 240, 80 ), new Vector3( 200, 520, 160 ), new Color( 0.5f, 0.55f, 0.45f ), default, collide: true );
		// Windows on the neighbor walls facing the street.
		for ( var i = 0; i < 3; i++ )
		{
			MeshKit.Spawn( _static, $"NbrWinL{i}", new Vector3( -w - 6, -d - 120 - i * 130f, 95 ), new Vector3( 3, 60, 45 ), new Color( 0.42f, 0.5f, 0.55f ) );
			MeshKit.Spawn( _static, $"NbrWinR{i}", new Vector3( w + 6, -d - 120 - i * 130f, 95 ), new Vector3( 3, 60, 45 ), new Color( 0.42f, 0.5f, 0.55f ) );
		}

		// Service counter (collides so customers/player stay on their sides).
		var cc = ShopLayout.CounterCenter;
		var cs = ShopLayout.CounterSize;
		MeshKit.Spawn( _static, "Counter", cc + new Vector3( 0, 0, cs.z * 0.5f ), cs, CounterColor, default, collide: true );
		MeshKit.Spawn( _static, "CounterTop", cc + new Vector3( 0, 0, cs.z + 2 ), new Vector3( cs.x + 14, cs.y + 14, 4 ), TrimColor );
		MeshKit.Spawn( _static, "CounterKick", cc + new Vector3( 0, -cs.y * 0.5f - 1, 6 ), new Vector3( cs.x - 10, 2, 12 ), new Color( 0.3f, 0.19f, 0.1f ) );

		// Cash register on the counter (player side of the item spot).
		MeshKit.Spawn( _static, "RegBody", cc + new Vector3( 95, 8, cs.z + 10 ), new Vector3( 30, 24, 16 ), new Color( 0.24f, 0.26f, 0.3f ) );
		MeshKit.Spawn( _static, "RegKeys", cc + new Vector3( 88, 2, cs.z + 18.5f ), new Vector3( 14, 10, 2 ), new Color( 0.7f, 0.72f, 0.75f ), new Angles( -18, 0, 0 ) );
		MeshKit.Spawn( _static, "RegDisplay", cc + new Vector3( 102, 8, cs.z + 22 ), new Vector3( 4, 18, 8 ), new Color( 0.15f, 0.3f, 0.2f ) );

		// Counter lamp (banker's lamp look) on the other end.
		MeshKit.Spawn( _static, "LampBase", cc + new Vector3( -100, 10, cs.z + 5.5f ), new Vector3( 10, 10, 3 ), new Color( 0.2f, 0.3f, 0.22f ) );
		MeshKit.Spawn( _static, "LampStem", cc + new Vector3( -100, 10, cs.z + 12 ), new Vector3( 2.5f, 2.5f, 12 ), new Color( 0.75f, 0.62f, 0.3f ) );
		MeshKit.Spawn( _static, "LampShade", cc + new Vector3( -100, 4, cs.z + 19 ), new Vector3( 12, 16, 6 ), new Color( 0.16f, 0.4f, 0.26f ), new Angles( -12, 0, 0 ) );
		SpawnLight( _static, cc + new Vector3( -100, 0, cs.z + 14 ), new Color( 1f, 0.9f, 0.6f ), 70f );

		// Wooden stool behind the counter.
		MeshKit.Spawn( _static, "StoolSeat", cc + new Vector3( 0, cs.y * 0.5f + 24, 26 ), new Vector3( 20, 20, 4 ), ShelfColor );
		for ( var i = 0; i < 4; i++ )
		{
			var ox = (i % 2 == 0 ? -1 : 1) * 7f;
			var oy = (i < 2 ? -1 : 1) * 7f;
			MeshKit.Spawn( _static, $"StoolLeg{i}", cc + new Vector3( ox, cs.y * 0.5f + 24 + oy, 12 ), new Vector3( 3, 3, 24 ), TrimColor );
		}

		// Radio on the counter — togglable.
		MeshKit.Spawn( _static, "RadioBody", cc + new Vector3( -60, 12, cs.z + 9 ), new Vector3( 22, 12, 14 ), new Color( 0.5f, 0.32f, 0.18f ) );
		MeshKit.Spawn( _static, "RadioGrill", cc + new Vector3( -60, 5.2f, cs.z + 9 ), new Vector3( 16, 1.6f, 9 ), new Color( 0.85f, 0.78f, 0.6f ) );
		MeshKit.SpawnSphere( _static, "RadioKnob", cc + new Vector3( -51, 5.4f, cs.z + 13 ), 3f, new Color( 0.9f, 0.85f, 0.7f ) );
		MeshKit.Spawn( _static, "RadioAntenna", cc + new Vector3( -68, 14, cs.z + 22 ), new Vector3( 1, 1, 14 ), new Color( 0.7f, 0.7f, 0.72f ), new Angles( 0, 0, 20 ) );
		AddInteractable( "RadioZone", cc + new Vector3( -60, 8, cs.z + 10 ), new Vector3( 34, 26, 30 ), InteractKind.Radio );

		// Potted plant by the door (waterable chore).
		var plant = ShopLayout.PlantSpot;
		MeshKit.Spawn( _static, "PlantPot", plant + new Vector3( 0, 0, 11 ), new Vector3( 24, 24, 22 ), new Color( 0.6f, 0.34f, 0.22f ) );
		MeshKit.SpawnSphere( _static, "PlantA", plant + new Vector3( 0, 0, 40 ), 30f, new Color( 0.28f, 0.48f, 0.24f ) );
		MeshKit.SpawnSphere( _static, "PlantB", plant + new Vector3( -8, 6, 54 ), 20f, new Color( 0.34f, 0.55f, 0.27f ) );
		MeshKit.Spawn( _static, "WateringCan", plant + new Vector3( 22, -10, 8 ), new Vector3( 14, 8, 10 ), new Color( 0.45f, 0.55f, 0.35f ), new Angles( 0, 35, 0 ) );

		// Ceiling fan over the customer area (spins during open hours for life).
		var fanGo = new GameObject( _static, true, "Fan" );
		fanGo.WorldPosition = new Vector3( 0, -60, h - 16 );
		fanGo.Components.Create<Spinner>().DegreesPerSecond = 140f;
		MeshKit.Spawn( _static, "FanRod", new Vector3( 0, -60, h - 8 ), new Vector3( 3, 3, 16 ), TrimColor );
		MeshKit.SpawnSphere( fanGo, "FanHub", new Vector3( 0, 0, 0 ), 10f, TrimColor );
		for ( var i = 0; i < 4; i++ )
		{
			var blade = MeshKit.Spawn( fanGo, $"FanBlade{i}", Vector3.Zero, new Vector3( 60, 12, 2 ), ShelfColor );
			blade.LocalRotation = Rotation.FromYaw( i * 90f );
			blade.LocalPosition = blade.LocalRotation.Forward * 36f;
		}

		BuildBackroom( _static );
		BuildShopClutter( _static );
		BuildUtilityProps( _static );

		// Wall shelves (base displays) — boards under the slot positions.
		BuildShelfBoards( _static, 0, 4 );   // left wall
		BuildGlassCases( _static, 4, 2, premium: false );
		BuildShelfBoards( _static, 6, 2 );   // right wall

		// Warm interior lights, each with a visible hanging pendant fixture.
		SpawnPendant( _static, new Vector3( -150, -60, ShopLayout.WallH ), new Color( 1f, 0.87f, 0.65f ), 320f );
		SpawnPendant( _static, new Vector3( 150, -60, ShopLayout.WallH ), new Color( 1f, 0.87f, 0.65f ), 320f );
		SpawnPendant( _static, new Vector3( 0, 150, ShopLayout.WallH ), new Color( 1f, 0.9f, 0.7f ), 300f );

		// Door sign (hangs over the door, readable from inside).
		var signGo = new GameObject( _static, true, "DoorSign" );
		signGo.WorldPosition = new Vector3( 0, -d + 14, ShopLayout.WallH - 34 );
		_doorSign = signGo.Components.Create<TextRenderer>();
		_doorSign.Text = "CLOSED";
		_doorSign.FontSize = 28;
		_doorSign.Color = new Color( 0.9f, 0.3f, 0.25f );
		_doorSign.Billboard = TextRenderer.BillboardMode.YOnly;
		_doorSign.HorizontalAlignment = TextRenderer.HAlignment.Center;

		// Shop name plate facing the sales floor (on the staff divider).
		var plateGo = new GameObject( _static, true, "NamePlate" );
		plateGo.WorldPosition = new Vector3( 0, ShopLayout.BackroomLine - 8, ShopLayout.WallH - 28 );
		var plate = plateGo.Components.Create<TextRenderer>();
		plate.Text = "BRASS BUCK PAWN";
		plate.FontSize = 34;
		plate.Scale = 0.22f;
		plate.Color = new Color( 0.95f, 0.78f, 0.25f );
		plate.Billboard = TextRenderer.BillboardMode.YOnly;
		plate.HorizontalAlignment = TextRenderer.HAlignment.Center;

		// Interactables.
		AddInteractable( "DoorZone", new Vector3( 0, -d + 30, 40 ), new Vector3( ShopLayout.DoorWidth + 60, 90, 90 ), InteractKind.Door );
		AddInteractable( "CounterZone", ShopLayout.CounterCenter + new Vector3( 0, 0, 30 ), new Vector3( ShopLayout.CounterSize.x, ShopLayout.CounterSize.y + 30, 70 ), InteractKind.Counter );
		AddInteractable( "BackDoorZone", ShopLayout.BackDoorInside + new Vector3( 0, 0, 40 ), new Vector3( 110, 100, 100 ), InteractKind.BackDoor );
		AddInteractable( "StockTableZone", ShopLayout.StockTable + new Vector3( 0, 0, 30 ), new Vector3( 100, 70, 70 ), InteractKind.StockTable );
		AddInteractable( "BaseBenchZone", ShopLayout.Workbench + new Vector3( 0, -20, 40 ), new Vector3( 130, 70, 80 ), InteractKind.Workbench );
		AddInteractable( "PawnCageZone", ShopLayout.PawnCage + new Vector3( 0, -20, 40 ), new Vector3( 120, 80, 90 ), InteractKind.PawnCage );
	}

	/// <summary>Staff-only half of the shop: racks, stock table, cage, back door, safe.</summary>
	private void BuildBackroom( GameObject parent )
	{
		var w = ShopLayout.HalfW;
		var d = ShopLayout.HalfD;
		var line = ShopLayout.BackroomLine;

		// Painted concrete floor strip so the backroom reads as a different room.
		MeshKit.Spawn( parent, "BackFloor", new Vector3( 0, (line + d) * 0.5f, 0.4f ), new Vector3( w * 2 - 8, d - line - 4, 1 ), new Color( 0.42f, 0.4f, 0.38f ) );
		for ( var i = -3; i <= 3; i++ )
			MeshKit.Spawn( parent, $"BackSeam{i}", new Vector3( i * 90f, (line + d) * 0.5f, 0.9f ), new Vector3( 2, d - line - 10, 0.4f ), new Color( 0.35f, 0.33f, 0.31f ) );

		// Half-wall divider with a walk-through gap and "STAFF" header.
		MeshKit.Spawn( parent, "BackDividerL", new Vector3( -w * 0.58f, line, 55 ), new Vector3( w * 0.68f, 8, 110 ), TrimColor, default, collide: true );
		MeshKit.Spawn( parent, "BackDividerR", new Vector3( w * 0.58f, line, 55 ), new Vector3( w * 0.68f, 8, 110 ), TrimColor, default, collide: true );
		MeshKit.Spawn( parent, "DividerCapL", new Vector3( -w * 0.58f, line, 112 ), new Vector3( w * 0.68f + 6, 12, 6 ), WoodDarkColor );
		MeshKit.Spawn( parent, "DividerCapR", new Vector3( w * 0.58f, line, 112 ), new Vector3( w * 0.68f + 6, 12, 6 ), WoodDarkColor );
		MeshKit.Spawn( parent, "StaffSignBoard", new Vector3( 0, line + 4, 118 ), new Vector3( 90, 4, 22 ), new Color( 0.18f, 0.16f, 0.14f ) );
		var staffGo = new GameObject( parent, true, "StaffSign" );
		staffGo.WorldPosition = new Vector3( 0, line - 6, 118 );
		var staffTxt = staffGo.Components.Create<TextRenderer>();
		staffTxt.Text = "STAFF ONLY — BACKROOM";
		staffTxt.FontSize = 22;
		staffTxt.Scale = 0.16f;
		staffTxt.Color = new Color( 0.95f, 0.78f, 0.35f );
		staffTxt.Billboard = TextRenderer.BillboardMode.YOnly;
		staffTxt.HorizontalAlignment = TextRenderer.HAlignment.Center;

		// Industrial steel racks along the back wall (stock props spawn into these).
		for ( var i = 0; i < 3; i++ )
		{
			var x = -240f + i * 240f;
			var y = d - 48f;
			MeshKit.Spawn( parent, $"RackFrame{i}", new Vector3( x, y, 55 ), new Vector3( 140, 36, 110 ), new Color( 0.35f, 0.36f, 0.4f ), default, collide: true );
			MeshKit.Spawn( parent, $"RackShelfLo{i}", new Vector3( x, y - 4, 36 ), new Vector3( 136, 40, 5 ), ShelfColor );
			MeshKit.Spawn( parent, $"RackShelfHi{i}", new Vector3( x, y - 4, 78 ), new Vector3( 136, 40, 5 ), ShelfColor );
			MeshKit.Spawn( parent, $"RackTop{i}", new Vector3( x, y, 112 ), new Vector3( 144, 42, 6 ), TrimColor );
			// Decorative empty boxes on the very top (stock sits on the shelves).
			MeshKit.Spawn( parent, $"TopBoxA{i}", new Vector3( x - 36, y, 126 ), new Vector3( 32, 28, 22 ), new Color( 0.68f, 0.52f, 0.32f ) );
			MeshKit.Spawn( parent, $"TopBoxB{i}", new Vector3( x + 30, y - 4, 122 ), new Vector3( 26, 24, 16 ), new Color( 0.6f, 0.45f, 0.28f ) );
		}

		// Sorting table in the middle of the backroom.
		var table = ShopLayout.StockTable;
		MeshKit.Spawn( parent, "StockTable", table + new Vector3( 0, 0, 22 ), new Vector3( 110, 56, 44 ), TrimColor, default, collide: true );
		MeshKit.Spawn( parent, "StockTableTop", table + new Vector3( 0, 0, 46 ), new Vector3( 118, 62, 4 ), WoodDarkColor );
		MeshKit.Spawn( parent, "Clipboard", table + new Vector3( -30, 8, 50 ), new Vector3( 16, 12, 1.5f ), new Color( 0.75f, 0.7f, 0.55f ), new Angles( 0, 20, 0 ) );
		MeshKit.Spawn( parent, "Mug", table + new Vector3( 34, -10, 52 ), new Vector3( 8, 8, 10 ), new Color( 0.55f, 0.22f, 0.18f ) );
		MeshKit.SpawnSphere( parent, "MugRim", table + new Vector3( 34, -10, 58 ), 7f, new Color( 0.65f, 0.28f, 0.22f ) );

		// Always-present basic workbench (upgrade adds tools / light).
		var bench = ShopLayout.Workbench;
		MeshKit.Spawn( parent, "BaseBench", bench + new Vector3( 0, 0, 22 ), new Vector3( 120, 50, 44 ), new Color( 0.38f, 0.4f, 0.44f ), default, collide: true );
		MeshKit.Spawn( parent, "BaseBenchTop", bench + new Vector3( 0, 0, 46 ), new Vector3( 128, 56, 4 ), WoodDarkColor );
		MeshKit.Spawn( parent, "Rag", bench + new Vector3( 20, -8, 49 ), new Vector3( 18, 12, 2 ), new Color( 0.55f, 0.5f, 0.35f ), new Angles( 0, 30, 0 ) );
		MeshKit.Spawn( parent, "Bottle", bench + new Vector3( -35, 10, 54 ), new Vector3( 6, 6, 14 ), new Color( 0.3f, 0.55f, 0.45f ) );

		// Heavy floor safe.
		MeshKit.Spawn( parent, "Safe", new Vector3( -w + 55, line + 55, 26 ), new Vector3( 55, 50, 52 ), new Color( 0.2f, 0.24f, 0.28f ), default, collide: true );
		MeshKit.SpawnSphere( parent, "SafeDial", new Vector3( -w + 55, line + 27, 32 ), 8f, new Color( 0.7f, 0.72f, 0.75f ) );
		MeshKit.Spawn( parent, "SafeHandle", new Vector3( -w + 68, line + 28, 26 ), new Vector3( 3, 2, 10 ), new Color( 0.75f, 0.62f, 0.3f ) );

		// Pawn lockup cage — deeper into the backroom.
		var cage = ShopLayout.PawnCage;
		MeshKit.Spawn( parent, "CageTop", cage + new Vector3( 0, 0, 100 ), new Vector3( 112, 72, 4 ), new Color( 0.4f, 0.42f, 0.46f ) );
		MeshKit.Spawn( parent, "CageBase", cage + new Vector3( 0, 0, 2 ), new Vector3( 112, 72, 4 ), new Color( 0.4f, 0.42f, 0.46f ) );
		for ( var i = 0; i < 7; i++ )
			MeshKit.Spawn( parent, $"CageBarF{i}", cage + new Vector3( -53 + i * 18f, -36, 50 ), new Vector3( 3, 3, 96 ), new Color( 0.55f, 0.58f, 0.62f ) );
		for ( var i = 0; i < 4; i++ )
			MeshKit.Spawn( parent, $"CageBarS{i}", cage + new Vector3( -54, -34 + i * 22f, 50 ), new Vector3( 3, 3, 96 ), new Color( 0.55f, 0.58f, 0.62f ) );
		MeshKit.Spawn( parent, "CageSignBoard", cage + new Vector3( 0, -38, 82 ), new Vector3( 56, 3, 14 ), new Color( 0.75f, 0.62f, 0.3f ) );
		var cageLabel = new GameObject( parent, true, "CageLabel" );
		cageLabel.WorldPosition = cage + new Vector3( 0, -42, 82 );
		var cageTxt = cageLabel.Components.Create<TextRenderer>();
		cageTxt.Text = "PAWN HOLD";
		cageTxt.FontSize = 20;
		cageTxt.Scale = 0.14f;
		cageTxt.Color = new Color( 0.15f, 0.12f, 0.08f );
		cageTxt.Billboard = TextRenderer.BillboardMode.YOnly;
		cageTxt.HorizontalAlignment = TextRenderer.HAlignment.Center;

		// Back door opening — leave the gap clear so the player can walk into the alley.
		MeshKit.Spawn( parent, "BackDoorFrameL", new Vector3( -40, d, 50 ), new Vector3( 10, ShopLayout.WallT + 4, 100 ), TrimColor );
		MeshKit.Spawn( parent, "BackDoorFrameR", new Vector3( 40, d, 50 ), new Vector3( 10, ShopLayout.WallT + 4, 100 ), TrimColor );
		MeshKit.Spawn( parent, "BackDoorHeader", new Vector3( 0, d, 110 ), new Vector3( 90, ShopLayout.WallT + 4, 20 ), TrimColor );
		// Door swung open against the right jamb (does not block the walkway).
		MeshKit.Spawn( parent, "BackDoorLeaf", new Vector3( 48, d - 28, 50 ), new Vector3( 4, 70, 96 ), new Color( 0.28f, 0.2f, 0.14f ), new Angles( 0, -75f, 0 ) );
		MeshKit.Spawn( parent, "BackDoorKnob", new Vector3( 48, d - 55, 48 ), new Vector3( 4, 4, 4 ), new Color( 0.75f, 0.62f, 0.3f ) );
		MeshKit.Spawn( parent, "AlleyPad", ShopLayout.BackDoorOutside + new Vector3( 40, 30, -4 ), new Vector3( 220, 140, 10 ), new Color( 0.3f, 0.3f, 0.32f ), default, collide: true );
		MeshKit.Spawn( parent, "AlleyFence", ShopLayout.BackDoorOutside + new Vector3( 40, 90, 40 ), new Vector3( 220, 8, 80 ), new Color( 0.35f, 0.32f, 0.28f ), default, collide: true );
		MeshKit.Spawn( parent, "SealedCrate", ShopLayout.BackDoorOutside + new Vector3( -40, 10, 18 ), new Vector3( 48, 36, 36 ), new Color( 0.62f, 0.48f, 0.28f ) );
		MeshKit.Spawn( parent, "CrateStrap", ShopLayout.BackDoorOutside + new Vector3( -40, 10, 18 ), new Vector3( 50, 4, 8 ), new Color( 0.25f, 0.22f, 0.18f ) );

		var backDoorLabel = new GameObject( parent, true, "BackDoorLabel" );
		backDoorLabel.WorldPosition = new Vector3( 0, d - 10, 118 );
		var bdTxt = backDoorLabel.Components.Create<TextRenderer>();
		bdTxt.Text = "BACK DOOR";
		bdTxt.FontSize = 20;
		bdTxt.Scale = 0.14f;
		bdTxt.Color = new Color( 0.9f, 0.85f, 0.7f );
		bdTxt.Billboard = TextRenderer.BillboardMode.YOnly;
		bdTxt.HorizontalAlignment = TextRenderer.HAlignment.Center;

		// Warm pendant over the backroom work area.
		SpawnPendant( parent, new Vector3( 0, 300, ShopLayout.WallH ), new Color( 1f, 0.88f, 0.65f ), 280f );
		SpawnPendant( parent, new Vector3( -240, 340, ShopLayout.WallH ), new Color( 1f, 0.9f, 0.7f ), 180f );
	}

	/// <summary>Posters, corkboard, wall clock, hanging tags — makes the box feel lived-in.</summary>
	private void BuildShopClutter( GameObject parent )
	{
		var w = ShopLayout.HalfW;
		var d = ShopLayout.HalfD;

		// Corkboard behind the counter with sticky notes.
		MeshKit.Spawn( parent, "Corkboard", new Vector3( -130, 118, 90 ), new Vector3( 70, 3, 50 ), new Color( 0.62f, 0.48f, 0.28f ) );
		MeshKit.Spawn( parent, "NoteA", new Vector3( -145, 116, 100 ), new Vector3( 14, 1.5f, 12 ), new Color( 0.95f, 0.9f, 0.55f ), new Angles( 0, 0, 8 ) );
		MeshKit.Spawn( parent, "NoteB", new Vector3( -120, 116, 85 ), new Vector3( 12, 1.5f, 14 ), new Color( 0.7f, 0.9f, 0.85f ), new Angles( 0, 0, -6 ) );
		MeshKit.Spawn( parent, "NoteC", new Vector3( -110, 116, 102 ), new Vector3( 10, 1.5f, 10 ), new Color( 0.95f, 0.7f, 0.7f ) );

		// Wall clock.
		MeshKit.SpawnSphere( parent, "ClockFace", new Vector3( 130, 118, 100 ), 22f, new Color( 0.92f, 0.9f, 0.82f ) );
		MeshKit.Spawn( parent, "ClockRim", new Vector3( 130, 119.5f, 100 ), new Vector3( 24, 3, 24 ), TrimColor );
		MeshKit.Spawn( parent, "ClockHandH", new Vector3( 130, 116.5f, 104 ), new Vector3( 2, 1, 8 ), Darkish() );
		MeshKit.Spawn( parent, "ClockHandM", new Vector3( 134, 116.5f, 100 ), new Vector3( 10, 1, 2 ), Darkish() );

		// Framed "certificates" and posters on the left wall.
		SpawnPoster( parent, "PosterBuy", new Vector3( -w + 3, -40, 95 ), new Vector3( 3, 50, 70 ), new Color( 0.55f, 0.22f, 0.18f ), "WE BUY GOLD" );
		SpawnPoster( parent, "PosterCash", new Vector3( -w + 3, 80, 90 ), new Vector3( 3, 44, 56 ), new Color( 0.2f, 0.35f, 0.28f ), "CASH TODAY" );
		SpawnPoster( parent, "PosterFair", new Vector3( w - 3, -80, 95 ), new Vector3( 3, 48, 64 ), new Color( 0.25f, 0.28f, 0.45f ), "FAIR DEALS" );

		// Hanging price-tag strips near the door.
		for ( var i = 0; i < 5; i++ )
		{
			var x = -90f + i * 45f;
			MeshKit.Spawn( parent, $"HangString{i}", new Vector3( x, -d + 18, 120 ), new Vector3( 1, 1, 20 ), new Color( 0.3f, 0.28f, 0.24f ) );
			MeshKit.Spawn( parent, $"HangTag{i}", new Vector3( x, -d + 18, 105 ), new Vector3( 10, 2, 14 ), new ColorHsv( (i * 40f) % 360f, 0.35f, 0.75f ) );
		}

		// Floor crates / clutter near the left wall (sales floor).
		MeshKit.Spawn( parent, "FloorCrateA", new Vector3( -280, -40, 14 ), new Vector3( 36, 30, 28 ), new Color( 0.55f, 0.42f, 0.26f ), default, collide: true );
		MeshKit.Spawn( parent, "FloorCrateB", new Vector3( -270, -10, 10 ), new Vector3( 24, 22, 20 ), new Color( 0.62f, 0.48f, 0.3f ) );
		MeshKit.Spawn( parent, "Barrel", new Vector3( 280, -200, 22 ), new Vector3( 28, 28, 44 ), new Color( 0.45f, 0.32f, 0.2f ), default, collide: true );
		MeshKit.Spawn( parent, "BarrelRim", new Vector3( 280, -200, 44 ), new Vector3( 30, 30, 3 ), Metalish() );

		// Wainscot / chair rail so walls aren't one flat color.
		MeshKit.Spawn( parent, "RailBack", new Vector3( 0, d - ShopLayout.WallT, 48 ), new Vector3( w * 2 - 10, 3, 5 ), TrimColor );
		MeshKit.Spawn( parent, "RailLeft", new Vector3( -w + ShopLayout.WallT, 0, 48 ), new Vector3( 3, d * 2 - 10, 5 ), TrimColor );
		MeshKit.Spawn( parent, "RailRight", new Vector3( w - ShopLayout.WallT, 0, 48 ), new Vector3( 3, d * 2 - 10, 5 ), TrimColor );
		MeshKit.Spawn( parent, "WainscotL", new Vector3( -w + 5, 0, 22 ), new Vector3( 4, d * 2 - 16, 44 ), new Color( 0.48f, 0.34f, 0.2f ) );
		MeshKit.Spawn( parent, "WainscotR", new Vector3( w - 5, 0, 22 ), new Vector3( 4, d * 2 - 16, 44 ), new Color( 0.48f, 0.34f, 0.2f ) );
	}

	/// <summary>Broom closet, mop sink, and alley dumpster — fixtures for shop chores.</summary>
	private void BuildUtilityProps( GameObject parent )
	{
		// Broom closet niche in the backroom.
		var broom = ShopLayout.BroomCloset;
		MeshKit.Spawn( parent, "ClosetBack", broom + new Vector3( -8, 0, 50 ), new Vector3( 8, 40, 100 ), TrimColor, default, collide: true );
		MeshKit.Spawn( parent, "ClosetShelf", broom + new Vector3( 4, 0, 70 ), new Vector3( 20, 36, 4 ), ShelfColor );
		MeshKit.Spawn( parent, "BroomHandle", broom + new Vector3( 6, -8, 55 ), new Vector3( 3, 3, 90 ), new Color( 0.45f, 0.32f, 0.18f ), new Angles( 8, 0, 0 ) );
		MeshKit.Spawn( parent, "BroomHead", broom + new Vector3( 8, -8, 8 ), new Vector3( 16, 10, 8 ), new Color( 0.55f, 0.4f, 0.22f ) );
		MeshKit.Spawn( parent, "Dustpan", broom + new Vector3( 10, 10, 6 ), new Vector3( 18, 12, 4 ), new Color( 0.35f, 0.38f, 0.42f ), new Angles( 0, -20, 0 ) );

		// Utility sink + mop bucket.
		var sink = ShopLayout.UtilitySink;
		MeshKit.Spawn( parent, "SinkCabinet", sink + new Vector3( 0, 0, 20 ), new Vector3( 50, 36, 40 ), new Color( 0.4f, 0.42f, 0.46f ), default, collide: true );
		MeshKit.Spawn( parent, "SinkBasin", sink + new Vector3( 0, 0, 42 ), new Vector3( 46, 32, 8 ), Metalish() );
		MeshKit.Spawn( parent, "Faucet", sink + new Vector3( 0, 10, 52 ), new Vector3( 4, 10, 14 ), Metalish() );
		MeshKit.Spawn( parent, "MopBucket", sink + new Vector3( 32, -20, 12 ), new Vector3( 22, 22, 24 ), new Color( 0.75f, 0.55f, 0.2f ) );
		MeshKit.Spawn( parent, "MopHandle", sink + new Vector3( 32, -20, 50 ), new Vector3( 2.5f, 2.5f, 70 ), new Color( 0.5f, 0.35f, 0.2f ) );
		MeshKit.Spawn( parent, "Soap", sink + new Vector3( -14, -8, 47 ), new Vector3( 8, 6, 4 ), new Color( 0.85f, 0.9f, 0.95f ) );

		// Alley dumpster past the back door.
		var dump = ShopLayout.Dumpster;
		MeshKit.Spawn( parent, "DumpsterBody", dump + new Vector3( 0, 0, 32 ), new Vector3( 70, 50, 64 ), new Color( 0.28f, 0.42f, 0.3f ), default, collide: true );
		MeshKit.Spawn( parent, "DumpsterLid", dump + new Vector3( 0, -8, 68 ), new Vector3( 74, 54, 6 ), new Color( 0.22f, 0.35f, 0.25f ), new Angles( -12, 0, 0 ) );
		MeshKit.Spawn( parent, "DumpsterWheelL", dump + new Vector3( -24, 22, 6 ), new Vector3( 10, 6, 12 ), Darkish() );
		MeshKit.Spawn( parent, "DumpsterWheelR", dump + new Vector3( 24, 22, 6 ), new Vector3( 10, 6, 12 ), Darkish() );
		var dumpLabel = new GameObject( parent, true, "DumpsterLabel" );
		dumpLabel.WorldPosition = dump + new Vector3( 0, -28, 48 );
		var txt = dumpLabel.Components.Create<TextRenderer>();
		txt.Text = "TRASH";
		txt.FontSize = 22;
		txt.Scale = 0.14f;
		txt.Color = new Color( 0.9f, 0.85f, 0.55f );
		txt.Billboard = TextRenderer.BillboardMode.YOnly;
		txt.HorizontalAlignment = TextRenderer.HAlignment.Center;
	}

	private static Color Darkish() => new( 0.16f, 0.15f, 0.17f );
	private static Color Metalish() => new( 0.62f, 0.64f, 0.68f );

	private void SpawnPoster( GameObject parent, string name, Vector3 pos, Vector3 size, Color color, string label )
	{
		MeshKit.Spawn( parent, name, pos, size, color );
		MeshKit.Spawn( parent, name + "Frame", pos + new Vector3( pos.x < 0 ? 1.5f : -1.5f, 0, 0 ), size + new Vector3( 2, 4, 4 ), TrimColor );
		var go = new GameObject( parent, true, name + "Text" );
		go.WorldPosition = pos + new Vector3( pos.x < 0 ? 4f : -4f, 0, 0 );
		var txt = go.Components.Create<TextRenderer>();
		txt.Text = label;
		txt.FontSize = 18;
		txt.Scale = 0.12f;
		txt.Color = new Color( 0.95f, 0.9f, 0.75f );
		txt.Billboard = TextRenderer.BillboardMode.YOnly;
		txt.HorizontalAlignment = TextRenderer.HAlignment.Center;
	}

	private void BuildShelfBoards( GameObject parent, int firstSlot, int count )
	{
		for ( var i = firstSlot; i < firstSlot + count; i++ )
		{
			var slot = ShopLayout.Slot( i );
			if ( slot is null ) continue;
			var pos = slot.Position;
			MeshKit.Spawn( parent, $"ShelfBoard{i}", pos.WithZ( pos.z - 4 ), new Vector3( 60, 40, 6 ), ShelfColor );
			MeshKit.Spawn( parent, $"ShelfBracket{i}", pos.WithZ( pos.z - 22 ), new Vector3( 8, 30, 34 ), TrimColor );
		}
	}

	private void BuildGlassCases( GameObject parent, int firstSlot, int count, bool premium )
	{
		for ( var i = firstSlot; i < firstSlot + count; i++ )
		{
			var slot = ShopLayout.Slot( i );
			if ( slot is null ) continue;
			var pos = slot.Position.WithZ( 0 );
			var baseColor = premium ? new Color( 0.25f, 0.2f, 0.35f ) : TrimColor;
			MeshKit.Spawn( parent, $"CaseBase{i}", pos.WithZ( 20 ), new Vector3( 56, 46, 40 ), baseColor, default, collide: true );
			MeshKit.Spawn( parent, $"CaseGlass{i}", pos.WithZ( 58 ), new Vector3( 52, 42, 36 ), GlassColor );
			if ( premium )
				SpawnLight( parent, pos.WithZ( 84 ), new Color( 0.9f, 0.8f, 1f ), 90f );
		}
	}

	private static void SpawnLight( GameObject parent, Vector3 pos, Color color, float radius )
	{
		var go = new GameObject( parent, true, "Light" );
		go.WorldPosition = pos;
		var light = go.Components.Create<PointLight>();
		light.LightColor = color;
		light.Radius = radius;
		light.Shadows = false;
	}

	/// <summary>A hanging pendant fixture: cord, shade cone, glowing bulb + the light itself.</summary>
	private static void SpawnPendant( GameObject parent, Vector3 ceiling, Color color, float radius )
	{
		MeshKit.Spawn( parent, "PendantCord", ceiling - new Vector3( 0, 0, 9 ), new Vector3( 1.5f, 1.5f, 18 ), new Color( 0.15f, 0.13f, 0.11f ) );
		MeshKit.Spawn( parent, "PendantShade", ceiling - new Vector3( 0, 0, 21 ), new Vector3( 26, 26, 10 ), new Color( 0.25f, 0.35f, 0.3f ) );
		MeshKit.SpawnSphere( parent, "PendantBulb", ceiling - new Vector3( 0, 0, 27 ), 9f, new Color( 1f, 0.95f, 0.8f ) );
		SpawnLight( parent, ceiling - new Vector3( 0, 0, 32 ), color, radius );
	}

	/// <summary>The classic kit sedan: rocker, hood, deck, cabin, glass, wheels.</summary>
	private static void BuildParkedCar( GameObject parent, Vector3 pos, Color paint, bool flip = false )
	{
		var car = new GameObject( parent, true, "ParkedCar" );
		car.WorldPosition = pos;
		car.WorldRotation = Rotation.FromYaw( flip ? 180f : 0f );

		var dark = Color.Lerp( paint, Color.Black, 0.4f );
		var glass = new Color( 0.55f, 0.72f, 0.8f );

		// Body: rocker + hood + trunk + cabin (length along X).
		MeshKit.Spawn( car, "Rocker", new Vector3( 0, 0, 18 ), new Vector3( 150, 58, 20 ), paint, default, collide: true );
		MeshKit.Spawn( car, "Hood", new Vector3( 48, 0, 31 ), new Vector3( 54, 54, 8 ), paint );
		MeshKit.Spawn( car, "Trunk", new Vector3( -52, 0, 31 ), new Vector3( 44, 54, 8 ), paint );
		MeshKit.Spawn( car, "Cabin", new Vector3( -4, 0, 43 ), new Vector3( 62, 50, 22 ), paint );
		MeshKit.Spawn( car, "Roof", new Vector3( -4, 0, 55 ), new Vector3( 56, 46, 4 ), dark );
		// Glass, nudged out of the cabin faces.
		MeshKit.Spawn( car, "Windshield", new Vector3( 27, 0, 44 ), new Vector3( 3, 44, 16 ), glass, new Angles( -22, 0, 0 ) );
		MeshKit.Spawn( car, "RearGlass", new Vector3( -35, 0, 44 ), new Vector3( 3, 44, 14 ), glass, new Angles( 24, 0, 0 ) );
		MeshKit.Spawn( car, "SideGlassL", new Vector3( -4, -26, 45 ), new Vector3( 52, 2, 13 ), glass );
		MeshKit.Spawn( car, "SideGlassR", new Vector3( -4, 26, 45 ), new Vector3( 52, 2, 13 ), glass );
		// Lights + bumpers.
		MeshKit.Spawn( car, "BumperF", new Vector3( 76, 0, 14 ), new Vector3( 6, 60, 10 ), new Color( 0.6f, 0.62f, 0.65f ) );
		MeshKit.Spawn( car, "BumperR", new Vector3( -76, 0, 14 ), new Vector3( 6, 60, 10 ), new Color( 0.6f, 0.62f, 0.65f ) );
		MeshKit.Spawn( car, "HeadL", new Vector3( 75.6f, -20, 24 ), new Vector3( 3, 10, 6 ), new Color( 1f, 0.95f, 0.75f ) );
		MeshKit.Spawn( car, "HeadR", new Vector3( 75.6f, 20, 24 ), new Vector3( 3, 10, 6 ), new Color( 1f, 0.95f, 0.75f ) );
		MeshKit.Spawn( car, "TailL", new Vector3( -75.6f, -20, 24 ), new Vector3( 3, 10, 6 ), new Color( 0.85f, 0.2f, 0.16f ) );
		MeshKit.Spawn( car, "TailR", new Vector3( -75.6f, 20, 24 ), new Vector3( 3, 10, 6 ), new Color( 0.85f, 0.2f, 0.16f ) );
		// Wheels: spheres squashed thin on Y, center Z = radius.
		foreach ( var (wx, wy) in new[] { (45f, -30f), (45f, 30f), (-45f, -30f), (-45f, 30f) } )
		{
			var wheel = MeshKit.SpawnSphere( car, "Wheel", new Vector3( wx, wy, 12 ), 24f, new Color( 0.12f, 0.12f, 0.13f ) );
			wheel.LocalScale = wheel.LocalScale.WithY( wheel.LocalScale.y * 0.4f );
			var hub = MeshKit.SpawnSphere( car, "Hub", new Vector3( wx, wy + (wy < 0 ? -5f : 5f), 12 ), 10f, new Color( 0.7f, 0.72f, 0.75f ) );
			hub.LocalScale = hub.LocalScale.WithY( hub.LocalScale.y * 0.3f );
		}
	}

	private void AddInteractable( string name, Vector3 pos, Vector3 size, InteractKind kind, GameObject parent = null )
	{
		var go = new GameObject( parent ?? GameObject, true, name );
		go.WorldPosition = pos;
		var i = go.Components.Create<Interactable>();
		i.Kind = kind;
		i.HalfExtents = size * 0.5f;
	}

	// ==================================================================== Upgrade-driven geometry

	public void RebuildUpgradeGeometry()
	{
		_upgradeable?.Destroy();
		_upgradeable = new GameObject( GameObject, true, "Upgradeable" );

		var save = Game?.Save;
		if ( save is null ) return;

		if ( save.OwnsUpgrade( UpgradeId.DisplayWall ) )
			BuildShelfBoards( _upgradeable, 8, 4 );

		if ( save.OwnsUpgrade( UpgradeId.DisplayFloor ) )
		{
			for ( var i = 12; i <= 15; i++ )
			{
				var slot = ShopLayout.Slot( i );
				if ( slot is null ) continue;
				MeshKit.Spawn( _upgradeable, $"Stand{i}", slot.Position.WithZ( 18 ), new Vector3( 50, 44, 36 ), ShelfColor, default, collide: true );
			}
		}

		if ( save.OwnsUpgrade( UpgradeId.PremiumCase ) )
			BuildGlassCases( _upgradeable, 16, 2, premium: true );

		if ( save.OwnsUpgrade( UpgradeId.RepairBench ) )
		{
			// Tooling bolted onto the base backroom bench.
			var bench = ShopLayout.Workbench;
			MeshKit.Spawn( _upgradeable, "BenchVise", bench + new Vector3( -20, 0, 54 ), new Vector3( 18, 16, 16 ), new Color( 0.75f, 0.3f, 0.2f ) );
			MeshKit.Spawn( _upgradeable, "BenchLamp", bench + new Vector3( 30, 10, 64 ), new Vector3( 8, 8, 30 ), new Color( 0.85f, 0.85f, 0.8f ) );
			MeshKit.Spawn( _upgradeable, "ToolBoard", bench + new Vector3( 0, 22, 80 ), new Vector3( 100, 4, 44 ), new Color( 0.3f, 0.24f, 0.16f ) );
			MeshKit.Spawn( _upgradeable, "ToolA", bench + new Vector3( -22, 20, 82 ), new Vector3( 4, 2, 20 ), new Color( 0.7f, 0.7f, 0.72f ) );
			MeshKit.Spawn( _upgradeable, "ToolB", bench + new Vector3( -6, 20, 80 ), new Vector3( 10, 2, 4 ), new Color( 0.85f, 0.35f, 0.2f ) );
			MeshKit.Spawn( _upgradeable, "ToolC", bench + new Vector3( 14, 20, 84 ), new Vector3( 3, 2, 16 ), new Color( 0.75f, 0.62f, 0.3f ) );
			SpawnLight( _upgradeable, bench + new Vector3( 0, 0, 80 ), new Color( 0.95f, 0.95f, 0.85f ), 140f );
		}

		if ( save.OwnsUpgrade( UpgradeId.ReferenceComputer ) )
		{
			var desk = ShopLayout.ResearchDesk;
			MeshKit.Spawn( _upgradeable, "Desk", desk + new Vector3( 0, 0, 20 ), new Vector3( 80, 44, 40 ), TrimColor, default, collide: true );
			MeshKit.Spawn( _upgradeable, "Monitor", desk + new Vector3( 0, 10, 56 ), new Vector3( 34, 6, 24 ), new Color( 0.15f, 0.2f, 0.3f ) );
			MeshKit.Spawn( _upgradeable, "Screen", desk + new Vector3( 0, 6.4f, 56 ), new Vector3( 28, 1.5f, 18 ), new Color( 0.25f, 0.55f, 0.45f ) );
			MeshKit.Spawn( _upgradeable, "Keyboard", desk + new Vector3( 0, -10, 42.5f ), new Vector3( 26, 10, 2 ), new Color( 0.28f, 0.3f, 0.34f ) );
			MeshKit.Spawn( _upgradeable, "DeskChair", desk + new Vector3( 0, -45, 16 ), new Vector3( 26, 26, 6 ), new Color( 0.22f, 0.22f, 0.26f ) );
			MeshKit.Spawn( _upgradeable, "ChairBack", desk + new Vector3( 0, -58, 34 ), new Vector3( 24, 4, 30 ), new Color( 0.22f, 0.22f, 0.26f ) );
			AddInteractable( "ComputerZone", desk + new Vector3( 0, -15, 45 ), new Vector3( 90, 70, 70 ), InteractKind.Computer, _upgradeable );
		}

		if ( save.OwnsUpgrade( UpgradeId.Lighting ) )
		{
			SpawnLight( _upgradeable, new Vector3( -250, -180, ShopLayout.WallH - 15 ), new Color( 1f, 0.92f, 0.75f ), 260f );
			SpawnLight( _upgradeable, new Vector3( 250, -180, ShopLayout.WallH - 15 ), new Color( 1f, 0.92f, 0.75f ), 260f );
		}

		if ( save.OwnsUpgrade( UpgradeId.AdSign ) )
		{
			// Neon sign outside above the door.
			MeshKit.Spawn( _upgradeable, "SignBoard", new Vector3( 0, -ShopLayout.HalfD - 12, ShopLayout.WallH + 26 ), new Vector3( 260, 12, 44 ), new Color( 0.12f, 0.1f, 0.16f ) );
			var neonGo = new GameObject( _upgradeable, true, "NeonText" );
			neonGo.WorldPosition = new Vector3( 0, -ShopLayout.HalfD - 22, ShopLayout.WallH + 26 );
			var neon = neonGo.Components.Create<TextRenderer>();
			neon.Text = "BRASS BUCK PAWN";
			neon.FontSize = 30;
			neon.Color = new Color( 0.3f, 1f, 0.85f );
			neon.Billboard = TextRenderer.BillboardMode.YOnly;
			neon.HorizontalAlignment = TextRenderer.HAlignment.Center;
			SpawnLight( _upgradeable, new Vector3( 0, -ShopLayout.HalfD - 40, ShopLayout.WallH + 20 ), new Color( 0.3f, 1f, 0.85f ), 160f );
		}

		if ( save.OwnsUpgrade( UpgradeId.SecurityCamera ) )
		{
			MeshKit.Spawn( _upgradeable, "Camera1", new Vector3( -ShopLayout.HalfW + 20, -ShopLayout.HalfD + 20, ShopLayout.WallH - 18 ), new Vector3( 14, 14, 10 ), new Color( 0.2f, 0.2f, 0.24f ) );
			MeshKit.Spawn( _upgradeable, "Camera2", new Vector3( ShopLayout.HalfW - 20, -ShopLayout.HalfD + 20, ShopLayout.WallH - 18 ), new Vector3( 14, 14, 10 ), new Color( 0.2f, 0.2f, 0.24f ) );
		}

		if ( save.OwnsUpgrade( UpgradeId.AlarmSystem ) )
			MeshKit.Spawn( _upgradeable, "AlarmBox", new Vector3( 60, -ShopLayout.HalfD + 12, ShopLayout.WallH - 30 ), new Vector3( 20, 8, 26 ), new Color( 0.8f, 0.2f, 0.2f ) );

		if ( save.OwnsUpgrade( UpgradeId.BetterCounter ) )
			MeshKit.Spawn( _upgradeable, "CounterUpgrade", ShopLayout.CounterCenter + new Vector3( 0, 0, ShopLayout.CounterSize.z + 5.5f ), new Vector3( ShopLayout.CounterSize.x + 20, ShopLayout.CounterSize.y + 20, 3 ), new Color( 0.35f, 0.2f, 0.1f ) );
	}

	// ==================================================================== Display + backroom stock props

	/// <summary>Rebuild front-shelf props, backroom stock, pawn cage goods, and their interact zones.</summary>
	public void RefreshDisplays()
	{
		_displayItems?.Destroy();
		_displayItems = new GameObject( GameObject, true, "DisplayItems" );

		var game = Game;
		if ( game?.Inventory is null ) return;

		// --- Front display shelves ---
		foreach ( var item in game.Inventory.OnDisplay )
		{
			if ( item.Id == game.CarriedItemId ) continue;
			var slot = ShopLayout.Slot( item.DisplaySlot );
			if ( slot is null ) continue;

			var root = new GameObject( _displayItems, true, $"Slot{slot.Index}" );
			root.WorldPosition = slot.Position;
			root.WorldRotation = Rotation.FromYaw( slot.Yaw );
			ItemProp.Build( root, item );
			AddPriceTag( root, slot.Position + Vector3.Up * 52f, item );

			AddShelfInteract( $"Disp{slot.Index}", slot.Position, InteractKind.DisplayShelf, item.Id, slot.Index );
		}

		// Empty unlocked shelves still accept a carried item.
		for ( var s = 0; s < ShopLayout.Slots.Length; s++ )
		{
			if ( !game.Inventory.SlotUnlocked( s ) || game.Inventory.SlotOccupied( s ) ) continue;
			var slot = ShopLayout.Slot( s );
			if ( slot is null ) continue;
			AddShelfInteract( $"EmptyDisp{s}", slot.Position, InteractKind.DisplayShelf, -1, s );
		}

		// --- Backroom stock on the racks ---
		var stock = game.Inventory.Backroom
			.Where( i => i.Id != game.CarriedItemId )
			.OrderBy( i => i.Id )
			.ToList();
		for ( var i = 0; i < stock.Count && i < ShopLayout.StorageSlots.Length; i++ )
		{
			var item = stock[i];
			var pos = ShopLayout.StorageSlots[i];
			var root = new GameObject( _displayItems, true, $"Stock{item.Id}" );
			root.WorldPosition = pos;
			root.WorldRotation = Rotation.FromYaw( 180f + (i % 3) * 8f );
			ItemProp.Build( root, item );
			AddShelfInteract( $"StockZone{item.Id}", pos, InteractKind.StockShelf, item.Id, -1 );
		}

		// --- Pawn cage contents ---
		var pawns = game.Inventory.Pawned.OrderBy( i => i.Id ).ToList();
		for ( var i = 0; i < pawns.Count && i < ShopLayout.PawnSlots.Length; i++ )
		{
			var item = pawns[i];
			var pos = ShopLayout.PawnSlots[i];
			var root = new GameObject( _displayItems, true, $"Pawn{item.Id}" );
			root.WorldPosition = pos;
			root.WorldRotation = Rotation.FromYaw( 200f );
			ItemProp.Build( root, item );
		}
	}

	private void AddPriceTag( GameObject parent, Vector3 worldPos, ItemInstance item )
	{
		var tagGo = new GameObject( parent, true, "PriceTag" );
		tagGo.WorldPosition = worldPos;
		var tag = tagGo.Components.Create<TextRenderer>();
		tag.Text = item.NotForSale ? "NOT FOR SALE" : GameConstants.FormatCash( item.SalePrice );
		tag.FontSize = 32;
		tag.Scale = 0.18f;
		tag.Color = item.NotForSale ? new Color( 0.85f, 0.4f, 0.35f ) : new Color( 1f, 0.95f, 0.7f );
		tag.Billboard = TextRenderer.BillboardMode.YOnly;
		tag.HorizontalAlignment = TextRenderer.HAlignment.Center;
	}

	private void AddShelfInteract( string name, Vector3 pos, InteractKind kind, int itemId, int slotIndex )
	{
		var go = new GameObject( _displayItems, true, name );
		go.WorldPosition = pos;
		var zone = go.Components.Create<Interactable>();
		zone.Kind = kind;
		zone.ItemId = itemId;
		zone.SlotIndex = slotIndex;
		zone.HalfExtents = new Vector3( 28, 28, 40 );
	}

	/// <summary>Show/remove the item being negotiated on the counter.</summary>
	public void SetCounterItem( ItemInstance item )
	{
		_counterItem?.Destroy();
		_counterItem = null;

		if ( item is null ) return;

		_counterItem = new GameObject( GameObject, true, "CounterItem" );
		_counterItem.WorldPosition = ShopLayout.CounterItemSpot;
		_counterItem.WorldRotation = Rotation.FromYaw( 180f );
		ItemProp.Build( _counterItem, item );
		Sfx.PlayAt( Sfx.ItemPlaced, ShopLayout.CounterItemSpot );
	}

	public void SetDoorSign( bool open )
	{
		if ( _doorSign.IsValid() )
		{
			_doorSign.Text = open ? "OPEN" : "CLOSED";
			_doorSign.Color = open ? new Color( 0.3f, 0.9f, 0.45f ) : new Color( 0.9f, 0.3f, 0.25f );
		}
	}
}
