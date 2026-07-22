namespace NoFly;

/// <summary>
/// Builds a stylized but intentional airport from composed kit props.
/// </summary>
public sealed class AirportBuilder : Component
{
	readonly Dictionary<string, Vector3> _spawns = new();
	readonly Dictionary<string, Vector3> _zones = new();

	const float RoomHeight = 200f;
	const float Door = 140f;
	const float WideDoor = 300f;

	public GameObject Root => GameObject;

	public void Build()
	{
		foreach ( var child in GameObject.Children.ToArray() )
			child.Destroy();
		_spawns.Clear();
		_zones.Clear();

		BuildExteriorGround();
		BuildGround();
		BuildRooms();
		BuildEntrance();
		BuildDocumentArea();
		BuildScannerArea();
		BuildSecurityArea();
		BuildTerminal();
		BuildGate();
		BuildChaseRoutes();
		BuildLighting();

		Components.GetOrCreate<DocumentStation>();
		Components.GetOrCreate<ScannerStation>();
		Components.GetOrCreate<BoardingZone>();
	}

	void BuildExteriorGround()
	{
		// Grass sits just under walkable outdoor slabs (same top height as interior floors).
		Kit.Box( GameObject, "EarthPad", new Vector3( 2200, 0, -10 ), new Vector3( 7200, 4200, 10 ), AirportPalette.Grass );

		// Drop-off plaza outside the main entrance (walkable, meets lobby floor).
		Kit.Floor( GameObject, "Plaza", new Vector3( -650, 0, 0 ), new Vector2( 560, 420 ), AirportPalette.FloorEntrance );
		Kit.Floor( GameObject, "PlazaRoad", new Vector3( -1000, 0, 0 ), new Vector2( 280, 520 ), AirportPalette.Tarmac );

		// South outdoor walk — terminal → apron (alternate route you can stroll).
		Kit.Floor( GameObject, "TermExitPad", new Vector3( 2800, -420, 0 ), new Vector2( 220, 120 ), AirportPalette.Exterior );
		Kit.Floor( GameObject, "SouthWalk", new Vector3( 2200, -520, 0 ), new Vector2( 3600, 140 ), AirportPalette.Exterior );
		Kit.Floor( GameObject, "SouthWalkEast", new Vector3( 4200, -520, 0 ), new Vector2( 900, 140 ), AirportPalette.Tarmac );
		Kit.Floor( GameObject, "ApronRamp", new Vector3( 4600, -200, 0 ), new Vector2( 200, 400 ), AirportPalette.Tarmac );

		// Apron / tarmac at gate end — same level as floors so you can walk the planes.
		Kit.Floor( GameObject, "Tarmac", new Vector3( 4800, 0, 0 ), new Vector2( 2200, 1800 ), AirportPalette.Tarmac );
		Kit.Decal( GameObject, "ApronLine", new Vector3( 4800, 0, 0 ), new Vector2( 1400, 6 ), AirportPalette.TarmacLine );
		Kit.Decal( GameObject, "ApronLine2", new Vector3( 4800, 220, 0 ), new Vector2( 1400, 4 ), AirportPalette.TarmacLine );
		Kit.Decal( GameObject, "ApronLine3", new Vector3( 4800, -220, 0 ), new Vector2( 1400, 4 ), AirportPalette.TarmacLine );

		// Side yard north of security — another outdoor loop.
		Kit.Floor( GameObject, "NorthYard", new Vector3( 1700, 1100, 0 ), new Vector2( 900, 280 ), AirportPalette.Grass );
	}

	void BuildGround()
	{
		// Slight gaps between zone floors so shared edges don't z-fight.
		Kit.Floor( GameObject, "Floor_Entrance", new Vector3( 0, 0, 0 ), new Vector2( 796, 496 ), AirportPalette.FloorEntrance );
		Kit.Floor( GameObject, "Floor_Docs", new Vector3( 900, 0, 0 ), new Vector2( 696, 496 ), AirportPalette.FloorDocs );
		Kit.Floor( GameObject, "Floor_Scanner", new Vector3( 1700, 0, 0 ), new Vector2( 696, 496 ), AirportPalette.FloorScanner );
		Kit.Floor( GameObject, "Floor_Terminal", new Vector3( 2600, 0, 0 ), new Vector2( 1096, 696 ), AirportPalette.FloorTerminal );
		// Bridge the old void between terminal (ends ~3150) and concourse (starts ~3350).
		Kit.Floor( GameObject, "Floor_Link", new Vector3( 3250, 0, 0 ), new Vector2( 220, 320 ), AirportPalette.FloorGate );
		Kit.Floor( GameObject, "Floor_Gate", new Vector3( 3600, 0, 0 ), new Vector2( 496, 1396 ), AirportPalette.FloorGate );
		// Bridge concourse → gate fingers.
		Kit.Floor( GameObject, "Floor_GateLink", new Vector3( 3920, 0, 0 ), new Vector2( 160, 1200 ), AirportPalette.FloorGate );
		Kit.Floor( GameObject, "Floor_Security", new Vector3( 1700, 700, 0 ), new Vector2( 496, 396 ), AirportPalette.FloorSecurity );
		BuildGuidePath();
	}

	void BuildRooms()
	{
		Kit.Room( GameObject, "Room_Entrance", new Vector3( 0, 0, 0 ), new Vector2( 800, 500 ), RoomHeight,
			AirportPalette.Wall, AirportPalette.Ceiling, openEast: Door, openWest: WideDoor );
		Kit.Room( GameObject, "Room_Docs", new Vector3( 900, 0, 0 ), new Vector2( 700, 500 ), RoomHeight,
			AirportPalette.Wall, AirportPalette.Ceiling, openEast: Door, openWest: Door );
		Kit.Room( GameObject, "Room_Scanner", new Vector3( 1700, 0, 0 ), new Vector2( 700, 500 ), RoomHeight,
			AirportPalette.Wall, AirportPalette.Ceiling, openEast: Door, openWest: Door, openNorth: Door );
		Kit.Room( GameObject, "Room_Security", new Vector3( 1700, 700, 0 ), new Vector2( 500, 400 ), RoomHeight,
			AirportPalette.Wall, AirportPalette.Ceiling, openSouth: Door );
		// Wide east opening into the gates link; south door lets you step outside onto the walk.
		Kit.Room( GameObject, "Room_Terminal", new Vector3( 2600, 0, 0 ), new Vector2( 1100, 700 ), RoomHeight,
			AirportPalette.Wall, AirportPalette.Ceiling, openEast: WideDoor, openWest: Door, openSouth: Door + 40f );
		// Nearly full-width east opening so A1 / B2 / C3 are all reachable.
		Kit.Room( GameObject, "Room_Concourse", new Vector3( 3600, 0, 0 ), new Vector2( 500, 1400 ), RoomHeight,
			AirportPalette.Wall, AirportPalette.Ceiling, openWest: WideDoor, openEast: 1100f );

		// Curtain wall windows — inset inside the south wall so glass ≠ wall plane.
		for ( var i = 0; i < 5; i++ )
		{
			Kit.Box( GameObject, $"TermGlass{i}", new Vector3( 2400 + i * 100, -338, 50 ),
				new Vector3( 86f, 6f, 110f ), AirportPalette.Glass, collider: false );
			Kit.Box( GameObject, $"TermMullion{i}", new Vector3( 2355 + i * 100, -334, 50 ),
				new Vector3( 8f, 8f, 120f ), AirportPalette.Metal, collider: false );
		}

		// Front facade — pillars either side of the entrance (center stays open).
		Kit.Box( GameObject, "FacadeL", new Vector3( -430, 200, 0 ), new Vector3( 24f, 80f, RoomHeight ), AirportPalette.Exterior );
		Kit.Box( GameObject, "FacadeR", new Vector3( -430, -200, 0 ), new Vector3( 24f, 80f, RoomHeight ), AirportPalette.Exterior );
		Kit.Box( GameObject, "FacadeHeader", new Vector3( -430, 0, 150 ), new Vector3( 28f, 280f, 70f ), AirportPalette.Navy );
		Kit.Box( GameObject, "FacadeCanopy", new Vector3( -400, 0, 112 ), new Vector3( 80f, 320f, 12f ), AirportPalette.MetalDark, collider: false );
	}

	void BuildGuidePath()
	{
		// Soft carpet runner — Decal sits above FloorTop (no coplanar flicker).
		for ( var x = -60f; x <= 3900f; x += 120f )
			Kit.Decal( GameObject, $"Path_{x:0}", new Vector3( x, 0, 0 ), new Vector2( 100f, 36f ), AirportPalette.FloorPath );
		Kit.Decal( GameObject, "PathFork", new Vector3( 3600, 0, 0 ), new Vector2( 36f, 900f ), AirportPalette.FloorPath );
	}

	void BuildEntrance()
	{
		Kit.Arch( GameObject, "WelcomeArch", new Vector3( -220, 0, 0 ), width: 260f, height: 150f, depth: 28f, AirportPalette.Navy );
		Kit.Label( GameObject, "WelcomeSign", new Vector3( -220, 0, 165 ), "NO FLY", Color.White, 28f );
		Kit.Label( GameObject, "WelcomeSub", new Vector3( -220, 0, 130 ), "INTERNATIONAL", new Color( 0.75f, 0.82f, 0.92f ), 10f );

		Kit.Counter( GameObject, "CheckIn", new Vector3( 80, 170, 0 ), new Vector3( 200f, 48f, 56f ) );
		Kit.SignPanel( GameObject, "Departures", new Vector3( 60, -190, 50 ), new Vector2( 180f, 100f ), "DEPARTURES", AirportPalette.NavyLite );

		Kit.Pillar( GameObject, "LobbyPillarL", new Vector3( -60, 150, 0 ), RoomHeight - 12f );
		Kit.Pillar( GameObject, "LobbyPillarR", new Vector3( -60, -150, 0 ), RoomHeight - 12f );
		Kit.Pillar( GameObject, "LobbyPillarL2", new Vector3( 200, 150, 0 ), RoomHeight - 12f );
		Kit.Pillar( GameObject, "LobbyPillarR2", new Vector3( 200, -150, 0 ), RoomHeight - 12f );

		Kit.Bench( GameObject, "WaitBenchA", new Vector3( 40, 90, 0 ), 110f );
		Kit.Bench( GameObject, "WaitBenchB", new Vector3( 40, -90, 0 ), 110f );
		Kit.Label( GameObject, "WaitSign", new Vector3( 40, 0, 90 ), "WAITING", new Color( 0.7f, 0.75f, 0.82f ), 12f );

		_spawns["lobby"] = new Vector3( -100, 0, 8 );
		_spawns["entrance"] = new Vector3( 50, 0, 8 );
		_spawns["wait_0"] = new Vector3( 40, 40, 8 );
		_spawns["wait_1"] = new Vector3( 80, -60, 8 );
		_spawns["wait_2"] = new Vector3( -20, -80, 8 );
		_spawns["wait_3"] = new Vector3( 120, 90, 8 );
		_spawns["wait_4"] = new Vector3( 10, 120, 8 );
		_spawns["wait_smuggler"] = new Vector3( 60, -20, 8 );
		_zones["departure_board"] = new Vector3( 60, -190, 8 );
		TagZone( "departure_board", _zones["departure_board"], 80f );
	}

	void BuildDocumentArea()
	{
		for ( var i = 0; i < 5; i++ )
			Kit.Stanchion( GameObject, $"DocStanchion{i}", new Vector3( 720, -100 + i * 40, 0 ), AirportPalette.NavyLite );

		var z = Kit.FloorTop + Kit.Skin;
		Kit.Box( GameObject, "DocBoothShell", new Vector3( 1060, 0, z ), new Vector3( 130f, 150f, 100f ), AirportPalette.Wall );
		Kit.Box( GameObject, "DocBoothGlass", new Vector3( 1005 - 6f, 0, z + 40f ), new Vector3( 6f, 120f, 70f ), AirportPalette.Glass, collider: false );
		Kit.Counter( GameObject, "DocDesk", new Vector3( 1000, 0, 0 ), new Vector3( 70f, 110f, 48f ) );
		Kit.SignPanel( GameObject, "DocSign", new Vector3( 1060, 0, 115 ), new Vector2( 140f, 36f ), "DOCUMENTS", AirportPalette.Navy );

		_spawns["docs"] = new Vector3( 1080, 80, 8 );
		_spawns["docs_approach"] = new Vector3( 880, 120, 8 );
		for ( var i = 0; i < 6; i++ )
			_spawns[$"doc_queue_{i}"] = new Vector3( 750 - i * 55, 0, 8 );

		var booth = Kit.InteractPad( GameObject, "DocInteract", new Vector3( 960, 0, 0 ), new Vector2( 70f, 70f ), AirportPalette.Success );
		booth.Tags.Add( "interact_document" );
		var docMarker = booth.Components.Create<InteractableMarker>();
		docMarker.Kind = InteractionKind.PresentDocument;
		docMarker.Prompt = "Join document queue";

		MakeStaffStation( "station_docs", _spawns["docs"], "DOCUMENTS DESK", AirportPalette.Navy );
	}

	void BuildScannerArea()
	{
		for ( var i = 0; i < 5; i++ )
			Kit.Stanchion( GameObject, $"ScanStanchion{i}", new Vector3( 1470, -100 + i * 40, 0 ), AirportPalette.Success );

		var z = Kit.FloorTop + Kit.Skin;
		Kit.Box( GameObject, "Conveyor", new Vector3( 1650, -80, z ), new Vector3( 240f, 36f, 28f ), AirportPalette.Metal );
		Kit.Box( GameObject, "ConveyorBelt", new Vector3( 1650, -80, z + 28f + Kit.Skin ), new Vector3( 236f, 28f, 4f ), AirportPalette.MetalDark, collider: false );
		Kit.Box( GameObject, "ScannerTunnel", new Vector3( 1760, -80, z ), new Vector3( 90f, 70f, 90f ), AirportPalette.MetalDark );
		// Window inset on the tunnel face.
		Kit.Box( GameObject, "ScannerWindow", new Vector3( 1760, -80 + 38f, z + 45f ), new Vector3( 70f, 6f, 40f ), AirportPalette.GlassDeep, collider: false );
		Kit.Box( GameObject, "WalkArch", new Vector3( 1760, 55, z ), new Vector3( 28f, 70f, 110f ), AirportPalette.Metal );
		Kit.Counter( GameObject, "ScannerDesk", new Vector3( 1800, -160, 0 ), new Vector3( 90f, 55f, 50f ) );
		Kit.SignPanel( GameObject, "ScanSign", new Vector3( 1760, 0, 115 ), new Vector2( 140f, 36f ), "BAG SCAN", AirportPalette.Success );
		Kit.Counter( GameObject, "SecondaryTable", new Vector3( 1900, 130, 0 ), new Vector3( 90f, 50f, 44f ) );

		_spawns["scanner"] = new Vector3( 1800, -180, 8 );
		_spawns["scanner_approach"] = new Vector3( 1580, -140, 8 );
		for ( var i = 0; i < 6; i++ )
			_spawns[$"scan_queue_{i}"] = new Vector3( 1500 - i * 55, 40, 8 );

		var bagDrop = Kit.InteractPad( GameObject, "BagDrop", new Vector3( 1550, -80, 0 ), new Vector2( 70f, 50f ), AirportPalette.Amber );
		bagDrop.Tags.Add( "interact_bag" );
		var bagMarker = bagDrop.Components.Create<InteractableMarker>();
		bagMarker.Kind = InteractionKind.PlaceBag;
		bagMarker.Prompt = "Join bag scan queue";

		var passengerPass = Kit.InteractPad( GameObject, "PassengerPass", new Vector3( 1820, 55, 0 ), new Vector2( 50f, 50f ), AirportPalette.Success );
		passengerPass.Tags.Add( "interact_pass" );
		passengerPass.Components.Create<InteractableMarker>().Kind = InteractionKind.JoinQueue;

		MakeStaffStation( "station_scanner", _spawns["scanner"], "BAG SCAN DESK", AirportPalette.Success );
	}

	void BuildSecurityArea()
	{
		Kit.Counter( GameObject, "SecurityDesk", new Vector3( 1700, 640, 0 ), new Vector3( 150f, 55f, 52f ) );
		var z = Kit.FloorTop + Kit.Skin;
		Kit.Box( GameObject, "HoldingShell", new Vector3( 1900, 780, z ), new Vector3( 150f, 130f, 110f ), new Color( 0.55f, 0.40f, 0.40f ) );
		Kit.Box( GameObject, "HoldingBars", new Vector3( 1830 - 4f, 780, z + 40f ), new Vector3( 6f, 110f, 80f ), AirportPalette.Metal, collider: false );
		Kit.SignPanel( GameObject, "HoldingSign", new Vector3( 1900, 780, 120 ), new Vector2( 120f, 28f ), "HOLDING", AirportPalette.Restricted );
		Kit.Box( GameObject, "EquipmentRack", new Vector3( 1600, 820, z ), new Vector3( 70f, 36f, 70f ), AirportPalette.MetalDark );
		Kit.Box( GameObject, "RestrictedDoor", new Vector3( 2100, 500, z ), new Vector3( 28f, 70f, 110f ), AirportPalette.Restricted );
		Kit.Label( GameObject, "RestrictedSign", new Vector3( 2100, 500, 125 ), "STAFF", Color.White, 12f );

		_spawns["security"] = new Vector3( 1700, 620, 8 );
		_spawns["security_approach"] = new Vector3( 1550, 520, 8 );
		_spawns["holding"] = new Vector3( 1900, 780, 8 );
		_zones["restricted"] = new Vector3( 2200, 450, 8 );

		var restricted = Kit.InteractPad( GameObject, "RestrictedTrigger", new Vector3( 2200, 450, 0 ), new Vector2( 120f, 180f ), AirportPalette.Restricted );
		restricted.Tags.Add( "restricted" );
		restricted.Components.Create<RestrictedZone>();

		MakeStaffStation( "station_security", _spawns["security"], "SECURITY DESK", AirportPalette.Amber );
	}

	void BuildTerminal()
	{
		Kit.Kiosk( GameObject, "Food", new Vector3( 2400, 220, 0 ), AirportPalette.Amber, "SNACKS" );
		Kit.Kiosk( GameObject, "Gift", new Vector3( 2400, -220, 0 ), new Color( 0.62f, 0.38f, 0.52f ), "GIFTS" );

		for ( var i = 0; i < 4; i++ )
			Kit.Bench( GameObject, $"Seat{i}", new Vector3( 2680 + i * 75, 180, 0 ), 60f );

		var z = Kit.FloorTop + Kit.Skin;
		Kit.Box( GameObject, "RestroomBay", new Vector3( 2550, -280, z ), new Vector3( 180f, 55f, 90f ), AirportPalette.Wall );
		Kit.SignPanel( GameObject, "RestSign", new Vector3( 2550, -280, 100 ), new Vector2( 120f, 28f ), "RESTROOMS", AirportPalette.WallTrim );
		Kit.Label( GameObject, "Landmark", new Vector3( 2950, -300, 130 ), "SKY VIEW", AirportPalette.NavyLite, 14f );

		// Beams hang below the ceiling slab (ceiling bottoms at RoomHeight + Skin).
		for ( var i = 0; i < 5; i++ )
			Kit.Box( GameObject, $"TermBeam{i}", new Vector3( 2200 + i * 180, 0, RoomHeight - 18f ),
				new Vector3( 14f, 640f, 10f ), AirportPalette.WallTrim, collider: false );

		_zones["shop_food"] = new Vector3( 2400, 220, 8 );
		_zones["shop_gift"] = new Vector3( 2400, -220, 8 );
		_zones["seating"] = new Vector3( 2800, 180, 8 );
		_zones["restroom"] = new Vector3( 2550, -280, 8 );
		_zones["landmark"] = new Vector3( 2950, -280, 8 );

		MakeShop( "shop_food", _zones["shop_food"], "Buy Snack/Drink" );
		MakeShop( "shop_gift", _zones["shop_gift"], "Browse Gifts" );
		MakeSit( _zones["seating"] );
		TagZone( "landmark", _zones["landmark"], 100f );
		TagZone( "restroom", _zones["restroom"], 80f );
	}

	void BuildGate()
	{
		// Open arch — was a solid wall that sealed the gates.
		Kit.Arch( GameObject, "ConcourseArch", new Vector3( 3280, 0, 0 ), width: 320f, height: 150f, depth: 24f, AirportPalette.Navy );
		Kit.Label( GameObject, "ConcourseSign", new Vector3( 3280, 0, 165 ), "GATES", Color.White, 22f );
		// Desk off to the side so the aisle stays clear.
		Kit.Counter( GameObject, "ConcourseInfo", new Vector3( 3480, 180, 0 ), new Vector3( 90f, 50f, 48f ) );

		_spawns["gate"] = new Vector3( 3450, 0, 8 );
		_zones["gate"] = new Vector3( 3500, 0, 8 );
		TagZone( "gate", _zones["gate"], 140f );

		BuildGateFinger( "A1", 520f );
		BuildGateFinger( "B2", 0f );
		BuildGateFinger( "C3", -520f );
		_spawns["board"] = _spawns["board_B2"];
	}

	void BuildGateFinger( string gateId, float y )
	{
		// Start east of the concourse (concourse ends ~3850) so walls don't seal the hall.
		var baseX = 4000f;
		var hallLen = 750f;
		var hallW = 160f;
		var hallCenter = new Vector3( baseX + 350f, y, 0 );

		Kit.Room( GameObject, $"Room_Gate_{gateId}", hallCenter, new Vector2( hallLen, hallW ), 160f,
			AirportPalette.Wall, AirportPalette.Ceiling, openWest: 140f, openEast: 100f );
		Kit.Floor( GameObject, $"GateFloor_{gateId}", hallCenter, new Vector2( hallLen - 4f, hallW - 4f ), AirportPalette.Carpet );

		for ( var i = 0; i < 6; i++ )
			Kit.Decal( GameObject, $"GatePath_{gateId}_{i}", new Vector3( baseX + 80 + i * 110, y, 0 ), new Vector2( 80f, 28f ), AirportPalette.FloorPath );

		Kit.Counter( GameObject, $"GateDesk_{gateId}", new Vector3( baseX + 220, y + 40, 0 ), new Vector3( 90f, 50f, 48f ) );
		Kit.SignPanel( GameObject, $"GateSign_{gateId}", new Vector3( baseX + 200, y, 110 ), new Vector2( 100f, 32f ), $"GATE {gateId}", AirportPalette.Navy );
		Kit.Bench( GameObject, $"GateSeats_{gateId}", new Vector3( baseX + 100, y + 45, 0 ), 90f );

		_spawns[$"gate_{gateId}"] = new Vector3( baseX + 180, y, 8 );
		_zones[$"gate_{gateId}"] = new Vector3( baseX + 200, y, 8 );
		TagZone( $"gate_{gateId}", _zones[$"gate_{gateId}"], 100f );

		Kit.Box( GameObject, $"JetBridge_{gateId}", new Vector3( baseX + 580, y, 20 ), new Vector3( 250f, 64f, 80f ), AirportPalette.Metal );
		Kit.Ceiling( GameObject, $"BridgeCeil_{gateId}", new Vector3( baseX + 580, y, 0 ), new Vector2( 270f, 80f ), 104f, AirportPalette.MetalDark );

		var boardPos = new Vector3( baseX + 780, y, 0 );
		// Doorframe only — leave the center open so you can board / walk onto the apron.
		Kit.Arch( GameObject, $"BoardingDoor_{gateId}", boardPos + new Vector3( -36, 0, 0 ), width: 100f, height: 120f, depth: 18f, AirportPalette.Navy );
		Kit.Label( GameObject, $"BoardSign_{gateId}", boardPos + Vector3.Up * 130f, $"BOARD {gateId}", Color.White, 12f );
		_spawns[$"board_{gateId}"] = boardPos + Vector3.Up * 8f;

		var board = Kit.InteractPad( GameObject, $"BoardTrigger_{gateId}", boardPos, new Vector2( 90f, 100f ), AirportPalette.Success );
		board.Tags.Add( "boarding" );
		board.Tags.Add( $"gate_{gateId}" );
		var boardMarker = board.Components.Create<InteractableMarker>();
		boardMarker.Kind = InteractionKind.BoardFlight;
		boardMarker.ZoneTag = gateId;
		boardMarker.Prompt = $"Board Gate {gateId}";

		Kit.Plane( GameObject, $"Plane_{gateId}", new Vector3( baseX + 980, y, 28 ), gateId switch
		{
			"A1" => "SKYnoodle 101",
			"B2" => "CLOUDHOP 220",
			_ => "WAFFLE 88"
		} );
	}

	void BuildChaseRoutes()
	{
		// Open cross-links between gate fingers (floors only — old solid walls blocked travel).
		Kit.Floor( GameObject, "CrossHallNorth", new Vector3( 4300, 260, 0 ), new Vector2( 120, 360 ), AirportPalette.FloorGate );
		Kit.Floor( GameObject, "CrossHallSouth", new Vector3( 4300, -260, 0 ), new Vector2( 120, 360 ), AirportPalette.FloorGate );
		Kit.Floor( GameObject, "StaffHall", new Vector3( 2300, 450, 0 ), new Vector2( 380, 70 ), new Color( 0.78f, 0.72f, 0.72f ) );
		_spawns["alt_route"] = new Vector3( 2800, -480, 8 );
		_spawns["staff_hall"] = new Vector3( 2300, 450, 8 );
		_spawns["gate_cross"] = new Vector3( 4300, 0, 8 );
		_spawns["outside_plaza"] = new Vector3( -700, 0, 8 );
		_spawns["outside_apron"] = new Vector3( 4800, 0, 8 );
	}

	public Vector3 GetBoardSpawn( string gateId )
	{
		if ( !string.IsNullOrEmpty( gateId ) && _spawns.TryGetValue( $"board_{gateId}", out var board ) )
			return board;
		return GetSpawn( "board" );
	}

	public Vector3 GetGateApproach( string gateId )
	{
		if ( !string.IsNullOrEmpty( gateId ) && _spawns.TryGetValue( $"gate_{gateId}", out var gate ) )
			return gate;
		return GetSpawn( "gate" );
	}

	void BuildLighting()
	{
		var light = new GameObject( true, "AirportSun" );
		light.SetParent( GameObject );
		light.WorldPosition = new Vector3( 2000, 0, 900 );
		light.WorldRotation = Rotation.From( new Angles( 78, 15, 0 ) );
		var dir = light.Components.Create<DirectionalLight>();
		dir.LightColor = new Color( 0.92f, 0.90f, 0.86f );
		dir.SkyColor = new Color( 0.40f, 0.48f, 0.58f );

		var ambient = light.Components.Create<AmbientLight>();
		ambient.Color = new Color( 0.58f, 0.60f, 0.64f );
	}

	void MakeShop( string tag, Vector3 pos, string label )
	{
		var go = Kit.InteractPad( GameObject, $"Interact_{tag}", pos.WithZ( 0 ), new Vector2( 110f, 90f ), AirportPalette.Amber );
		go.Tags.Add( tag );
		var marker = go.Components.Create<InteractableMarker>();
		marker.Kind = InteractionKind.UseShop;
		marker.ZoneTag = tag;
		marker.Prompt = label;
	}

	void MakeStaffStation( string zoneTag, Vector3 pos, string label, Color accent )
	{
		var pad = Kit.InteractPad( GameObject, $"StaffPad_{zoneTag}", pos.WithZ( 0 ), new Vector2( 110f, 90f ), accent );
		pad.Tags.Add( zoneTag );
		var marker = pad.Components.Create<InteractableMarker>();
		marker.Kind = InteractionKind.ManStation;
		marker.ZoneTag = zoneTag;
		marker.Prompt = $"Man {label}";
		Kit.Label( GameObject, $"StaffLabel_{zoneTag}", pos + Vector3.Up * 70f, "PRESS E", Color.White, 11f );
		Kit.Label( GameObject, $"StaffLabel2_{zoneTag}", pos + Vector3.Up * 52f, label, accent, 9f );
	}

	void MakeSit( Vector3 pos )
	{
		var go = Kit.InteractPad( GameObject, "SitSpot", pos.WithZ( 0 ), new Vector2( 60f, 50f ), AirportPalette.NavyLite );
		go.Tags.Add( "seating" );
		var marker = go.Components.Create<InteractableMarker>();
		marker.Kind = InteractionKind.Sit;
		marker.ZoneTag = "seating";
		marker.Prompt = "Sit";
	}

	void TagZone( string tag, Vector3 pos, float radius )
	{
		var go = new GameObject( true, $"Zone_{tag}" );
		go.SetParent( GameObject );
		go.WorldPosition = pos;
		var zone = go.Components.Create<ObjectiveZone>();
		zone.ZoneTag = tag;
		zone.Radius = radius;
	}

	public Vector3 GetSpawn( string key ) => _spawns.TryGetValue( key, out var p ) ? p : WorldPosition + Vector3.Up * 8f;
	public Vector3 GetZone( string key ) => _zones.TryGetValue( key, out var p ) ? p : GetSpawn( "entrance" );
	public IReadOnlyDictionary<string, Vector3> Spawns => _spawns;
}
