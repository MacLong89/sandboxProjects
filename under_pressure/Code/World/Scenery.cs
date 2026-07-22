namespace UnderPressure;

/// <summary>
/// Kind of composite decoration. Level-1 kit plus authored job set pieces.
/// </summary>
public enum DecorKind
{
	House,
	Tree,
	Bush,
	Fence,
	Mailbox,
	/// <summary>Foothills-style cream store + pump canopy. Size = store footprint (w,d,h).</summary>
	FoothillsStation,
	/// <summary>Restaurant rear elevation (beige wall, doors, steps). Size = footprint (w,d,h).</summary>
	SteakhouseAlley,
	/// <summary>Commercial dumpster.</summary>
	Dumpster,

	// --- Campaign site kit (see SceneryKits.cs) ---
	/// <summary>Corrugated industrial shell with roll-up doors on the −Y face. Size = footprint (w,d,h).</summary>
	Warehouse,
	/// <summary>Raised colliding concrete slab (dock platform, stage). Size = (w,d,height).</summary>
	Platform,
	/// <summary>Shipping container, doors on −Y. Size.x = uniform scale. Color = body.</summary>
	Container,
	/// <summary>Container with doors swung open and a dark interior. Size.x = scale. Color = body.</summary>
	ContainerOpen,
	/// <summary>Parked luxury sedan/SUV. Size.x = scale, Color = paint. Faces +X.</summary>
	LuxuryCar,
	/// <summary>Burnt, crumpled wreck. Size.x = scale. Faces +X.</summary>
	WreckedCar,
	/// <summary>Freight boxcar. Size.x = scale. Long axis X. Color = body.</summary>
	TrainCar,
	/// <summary>Diesel locomotive. Size.x = scale. Nose faces +X. Color = body.</summary>
	Locomotive,
	/// <summary>Twin rails + sleepers, flat on the ground. Size.x = run length. Long axis X.</summary>
	RailTrack,
	/// <summary>Elevated steel walkway with legs and rails. Size = (length, width, deck height). Long axis X.</summary>
	Catwalk,
	/// <summary>Cluster of 55-gal drums. Size.x = scale. Color = drum paint.</summary>
	BarrelCluster,
	/// <summary>Tall dark server cabinet with LED strips. Size.x = scale. Accent color = Color.</summary>
	ServerRack,
	/// <summary>Stack of wooden crates. Size.x = scale.</summary>
	CrateStack,
	/// <summary>Street lamp with a warm head. Size.x = scale.</summary>
	LampPost,
	/// <summary>Tripod work floodlight, angled toward −Y. Size.x = scale.</summary>
	Floodlight,
	/// <summary>Classical column. Size = (diameter, unused, height). Color = stone.</summary>
	Column,
	/// <summary>Pedestal + abstract marble figure. Size.x = scale. Color = stone.</summary>
	Statue,
	/// <summary>Museum display case: base + glass. Size.x = scale.</summary>
	DisplayCase,
	/// <summary>Overhead meat-hook rail on posts. Size.x = run length. Long axis X.</summary>
	HookRail,
	/// <summary>Stainless prep table. Size.x = scale.</summary>
	SteelTable,
	/// <summary>Lattice broadcast tower. Size = (base width, unused, height).</summary>
	Antenna,
	/// <summary>Satellite dish on a plinth, face tilted toward −Y. Size.x = scale.</summary>
	SatelliteDish,
	/// <summary>Industrial generator block. Size.x = scale. Color = housing.</summary>
	Generator,
	/// <summary>Pipes on stands. Size = (run length, unused, pipe height). Long axis X. Color = pipe.</summary>
	PipeRun,
	/// <summary>Valve stub with a wheel. Size.x = scale. Color = wheel.</summary>
	ValveStation,
	/// <summary>Highway W-beam guardrail. Size.x = run length. Long axis X.</summary>
	GuardRail,
	/// <summary>Elevated road deck on two piers, spanning X. Size = (span, deck width, clearance).</summary>
	Overpass,
	/// <summary>Jersey barrier run. Size.x = run length. Long axis X.</summary>
	ConcreteBarrier,
	/// <summary>Collapsed masonry pile. Size.x = scale. Color = debris.</summary>
	Rubble,
	/// <summary>Colliding interior wall slab with a tile band. Size = (width, thickness, height). Faces −Y. Color = tile.</summary>
	TiledWall,
	/// <summary>Superyacht cabin block with window band + radar arch. Size = footprint (w,d,h).</summary>
	YachtCabin,
	/// <summary>Deck/roof railing run: posts + top rail. Size.x = run length. Long axis X.</summary>
	Railing,
	/// <summary>White lab bench with equipment. Size.x = scale.</summary>
	LabBench,
	/// <summary>Studio camera on a tripod, lens toward −Y. Size.x = scale.</summary>
	StudioCamera,
	/// <summary>Broadcast control desk with monitors, screens toward −Y. Size.x = scale.</summary>
	ControlDesk,
	/// <summary>Detention cell: dark box with a barred front on −Y. Size = footprint (w,d,h).</summary>
	HoldingCell,
	/// <summary>Floor grate venting a translucent steam column. Size.x = scale.</summary>
	SteamVent,
	/// <summary>Giant pump/reactor machine with a glowing core on −Y. Size.x = scale. Color = glow.</summary>
	MachineCore,
	/// <summary>Recessed water channel with raised edges. Size = (length, width). Long axis X. Color = water.</summary>
	WaterChannel,
	/// <summary>Log cabin with snowy roof. Size = footprint (w,d,h).</summary>
	Cabin,
	/// <summary>Sofa. Size.x = scale. Color = upholstery. Seat faces −Y.</summary>
	Sofa,
	/// <summary>Executive desk with a toppled chair. Size.x = scale.</summary>
	Desk,
	/// <summary>Roof HVAC unit. Size.x = scale.</summary>
	RoofUnit,
	/// <summary>Subway platform edge: tactile strip + track trench with rails, drop on −Y. Size.x = run length.</summary>
	TrackTrench,
	/// <summary>Ceiling-less door frame with heavy blast doors ajar. Size = (width, thickness, height). Faces −Y.</summary>
	BlastDoor,
}

/// <summary>
/// One placed decoration. <see cref="Size"/> is interpreted per-kind (house footprint,
/// fence run length, tree scale). <see cref="Color"/> tints the primary surface.
/// </summary>
public sealed class DecorDef
{
	public DecorKind Kind { get; init; }
	public Vector3 Position { get; init; }
	public float Yaw { get; init; }
	public Vector3 Size { get; init; } = Vector3.One;
	public Color Color { get; init; } = Color.White;
}

/// <summary>
/// Builds composite low-poly props out of the shared box primitive.
/// </summary>
public static partial class Scenery
{
	private const float RadToDeg = 57.29578f;

	private static readonly Color TreeTrunk = new( 0.62f, 0.38f, 0.14f );
	private static readonly Color TreeLeafA = new( 0.38f, 0.86f, 0.08f );
	private static readonly Color TreeLeafB = new( 0.28f, 0.78f, 0.06f );
	private static readonly Color Wood = new( 0.86f, 0.58f, 0.24f );
	private static readonly Color RoofColor = new( 0.56f, 0.28f, 0.10f );
	private static readonly Color Garage = new( 0.90f, 0.78f, 0.58f );
	private static readonly Color WindowGlass = new( 0.58f, 0.82f, 0.94f );
	private static readonly Color DoorColor = new( 0.62f, 0.34f, 0.10f );
	private static readonly Color Foundation = new( 0.68f, 0.60f, 0.48f );
	private static readonly Color FenceColor = new( 1f, 0.82f, 0.38f );

	/// <summary>Spawn one decoration under <paramref name="parent"/> at world position/yaw.</summary>
	public static void Build( GameObject parent, DecorDef def )
	{
		var root = new GameObject( parent, true, def.Kind.ToString() );
		var pos = def.Position;
		if ( pos.z < 0.01f )
			pos = pos.WithZ( DepthLayers.DecorSitOnPad );
		root.WorldPosition = pos;
		root.WorldRotation = Rotation.FromYaw( def.Yaw );

		switch ( def.Kind )
		{
			case DecorKind.House: BuildHouse( root, def ); break;
			case DecorKind.Tree: BuildTree( root, def ); break;
			case DecorKind.Bush: BuildBush( root, def ); break;
			case DecorKind.Fence: BuildFence( root, def ); break;
			case DecorKind.Mailbox: BuildMailbox( root ); break;
			case DecorKind.FoothillsStation: BuildFoothillsStation( root, def ); break;
			case DecorKind.SteakhouseAlley: BuildSteakhouseAlley( root, def ); break;
			case DecorKind.Dumpster: BuildDumpster( root, def ); break;
			case DecorKind.Warehouse: BuildWarehouse( root, def ); break;
			case DecorKind.Platform: BuildPlatform( root, def ); break;
			case DecorKind.Container: BuildContainer( root, def, open: false ); break;
			case DecorKind.ContainerOpen: BuildContainer( root, def, open: true ); break;
			case DecorKind.LuxuryCar: BuildLuxuryCar( root, def ); break;
			case DecorKind.WreckedCar: BuildWreckedCar( root, def ); break;
			case DecorKind.TrainCar: BuildTrainCar( root, def ); break;
			case DecorKind.Locomotive: BuildLocomotive( root, def ); break;
			case DecorKind.RailTrack: BuildRailTrack( root, def ); break;
			case DecorKind.Catwalk: BuildCatwalk( root, def ); break;
			case DecorKind.BarrelCluster: BuildBarrelCluster( root, def ); break;
			case DecorKind.ServerRack: BuildServerRack( root, def ); break;
			case DecorKind.CrateStack: BuildCrateStack( root, def ); break;
			case DecorKind.LampPost: BuildLampPost( root, def ); break;
			case DecorKind.Floodlight: BuildFloodlight( root, def ); break;
			case DecorKind.Column: BuildColumn( root, def ); break;
			case DecorKind.Statue: BuildStatue( root, def ); break;
			case DecorKind.DisplayCase: BuildDisplayCase( root, def ); break;
			case DecorKind.HookRail: BuildHookRail( root, def ); break;
			case DecorKind.SteelTable: BuildSteelTable( root, def ); break;
			case DecorKind.Antenna: BuildAntenna( root, def ); break;
			case DecorKind.SatelliteDish: BuildSatelliteDish( root, def ); break;
			case DecorKind.Generator: BuildGenerator( root, def ); break;
			case DecorKind.PipeRun: BuildPipeRun( root, def ); break;
			case DecorKind.ValveStation: BuildValveStation( root, def ); break;
			case DecorKind.GuardRail: BuildGuardRail( root, def ); break;
			case DecorKind.Overpass: BuildOverpass( root, def ); break;
			case DecorKind.ConcreteBarrier: BuildConcreteBarrier( root, def ); break;
			case DecorKind.Rubble: BuildRubble( root, def ); break;
			case DecorKind.TiledWall: BuildTiledWall( root, def ); break;
			case DecorKind.YachtCabin: BuildYachtCabin( root, def ); break;
			case DecorKind.Railing: BuildRailing( root, def ); break;
			case DecorKind.LabBench: BuildLabBench( root, def ); break;
			case DecorKind.StudioCamera: BuildStudioCamera( root, def ); break;
			case DecorKind.ControlDesk: BuildControlDesk( root, def ); break;
			case DecorKind.HoldingCell: BuildHoldingCell( root, def ); break;
			case DecorKind.SteamVent: BuildSteamVent( root, def ); break;
			case DecorKind.MachineCore: BuildMachineCore( root, def ); break;
			case DecorKind.WaterChannel: BuildWaterChannel( root, def ); break;
			case DecorKind.Cabin: BuildCabin( root, def ); break;
			case DecorKind.Sofa: BuildSofa( root, def ); break;
			case DecorKind.Desk: BuildDesk( root, def ); break;
			case DecorKind.RoofUnit: BuildRoofUnit( root, def ); break;
			case DecorKind.TrackTrench: BuildTrackTrench( root, def ); break;
			case DecorKind.BlastDoor: BuildBlastDoor( root, def ); break;
			default:
				Log.Warning( $"[Scenery] Unhandled DecorKind {def.Kind} at {def.Position}" );
				break;
		}
	}

	/// <summary>
	/// Shared box primitive used by every builder and by <see cref="WorldMapBuilder"/> for
	/// roads, pads, and perimeter dressing.
	/// </summary>
	public static GameObject Box( GameObject parent, string name, Vector3 localPos, Vector3 size, Color color, Angles rot = default, Material material = null )
	{
		var go = new GameObject( parent, true, name );
		go.LocalPosition = localPos;
		go.LocalRotation = rot.ToRotation();
		go.LocalScale = MeshPrimitives.BoxScale( size );
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = MeshPrimitives.Box;
		mr.MaterialOverride = material ?? MeshPrimitives.Mat;
		mr.Tint = color;
		return go;
	}

	private static void BuildHouse( GameObject root, DecorDef def )
	{
		var w = def.Size.x;
		var d = def.Size.y;
		var h = def.Size.z;
		var front = -d * 0.5f;

		Box( root, "Foundation", new Vector3( 0, 0, 6 ), new Vector3( w + 10, d + 10, 12 ), Foundation, default, GameMaterials.Concrete );
		Box( root, "Walls", new Vector3( 0, 0, h * 0.5f + 10 ), new Vector3( w, d, h ), def.Color, default, GameMaterials.Wood );

		BuildGableRoof( root, w, d, h + 10 );

		var faceSlot = 0;
		float FaceY() => front - DepthLayers.NextFaceDepth( ref faceSlot );

		Box( root, "Garage", new Vector3( -w * 0.18f, FaceY(), 55 ), new Vector3( w * 0.42f, 6, 88 ), Garage, default, GameMaterials.Metal );
		Box( root, "GarageTrim", new Vector3( -w * 0.18f, FaceY(), 104 ), new Vector3( w * 0.46f, 5, 9 ), DoorColor, default, GameMaterials.Wood );
		Box( root, "Door", new Vector3( w * 0.32f, FaceY(), 58 ), new Vector3( 44, 6, 96 ), DoorColor, default, GameMaterials.Wood );
		Box( root, "WindowFrame", new Vector3( w * 0.32f, FaceY(), 150 ), new Vector3( 70, 4, 56 ), Wood, default, GameMaterials.Wood );
		Box( root, "Window", new Vector3( w * 0.32f, FaceY(), 150 ), new Vector3( 60, 4, 46 ), WindowGlass, default, GameMaterials.Metal );

		var step1 = front - DepthLayers.NextFaceDepth( ref faceSlot );
		var step2 = front - DepthLayers.NextFaceDepth( ref faceSlot );
		Box( root, "Step1", new Vector3( w * 0.32f, step1, 8 ), new Vector3( 70, 26, 16 ), Foundation, default, GameMaterials.Concrete );
		Box( root, "Step2", new Vector3( w * 0.32f, step2, 20 ), new Vector3( 70, 20, 12 ), Foundation, default, GameMaterials.Concrete );

		var col = root.Components.Create<BoxCollider>();
		col.Center = new Vector3( 0, 0, h * 0.5f + 10 );
		col.Scale = new Vector3( w, d, h + 20 );
		col.Static = true;
	}

	private static void BuildGableRoof( GameObject root, float w, float d, float wallTop )
	{
		var hw = d * 0.5f;
		var rise = d * 0.34f;
		var slope = MathF.Sqrt( hw * hw + rise * rise );
		var angle = MathF.Atan2( rise, hw ) * RadToDeg;
		var roofX = w + 44;

		Box( root, "RoofR", new Vector3( 0, hw * 0.5f, wallTop + rise * 0.5f ), new Vector3( roofX, slope, 10 ), RoofColor, new Angles( 0, 0, -angle ), GameMaterials.Shingles );
		Box( root, "RoofL", new Vector3( 0, -hw * 0.5f, wallTop + rise * 0.5f ), new Vector3( roofX, slope, 10 ), RoofColor, new Angles( 0, 0, angle ), GameMaterials.Shingles );
	}

	private static void BuildTree( GameObject root, DecorDef def )
	{
		// Stylized box pines — reliable scale, matches low-poly look.
		var s = Math.Clamp( def.Size.x <= 0.01f ? 1f : def.Size.x, 0.5f, 2.5f );
		BuildBoxTree( root, s, def.Color );
	}

	/// <summary>Conical low-poly pine.</summary>
	private static void BuildBoxTree( GameObject root, float s, Color color )
	{
		var trunkH = 48f * s;
		var leaf = color == Color.White ? TreeLeafA : color;

		Box( root, "Trunk", new Vector3( 0, 0, trunkH * 0.5f ), new Vector3( 14 * s, 14 * s, trunkH ), TreeTrunk, default, GameMaterials.Bark );
		Box( root, "Cone1", new Vector3( 0, 0, trunkH + 28 * s ), new Vector3( 90 * s, 90 * s, 56 * s ), leaf, default, GameMaterials.Leaves );
		Box( root, "Cone2", new Vector3( 0, 0, trunkH + 66 * s ), new Vector3( 64 * s, 64 * s, 48 * s ), TreeLeafB, default, GameMaterials.Leaves );
		Box( root, "Cone3", new Vector3( 0, 0, trunkH + 98 * s ), new Vector3( 36 * s, 36 * s, 40 * s ), leaf, default, GameMaterials.Leaves );
	}

	private static void BuildBush( GameObject root, DecorDef def )
	{
		var s = Math.Clamp( def.Size.x <= 0.01f ? 1f : def.Size.x, 0.5f, 2.5f );
		var leaf = def.Color == Color.White ? new Color( 0.38f, 0.86f, 0.08f ) : def.Color;

		Box( root, "Bush1", new Vector3( 0, 0, 20 * s ), new Vector3( 52 * s, 52 * s, 40 * s ), leaf, default, GameMaterials.Leaves );
		Box( root, "Bush2", new Vector3( 22 * s, -8 * s, 15 * s ), new Vector3( 34 * s, 34 * s, 30 * s ), TreeLeafB, new Angles( 0, 30, 0 ), GameMaterials.Leaves );
		Box( root, "Bush3", new Vector3( -20 * s, 10 * s, 14 * s ), new Vector3( 30 * s, 30 * s, 26 * s ), leaf, new Angles( 0, 15, 0 ), GameMaterials.Leaves );
	}

	private static void BuildFence( GameObject root, DecorDef def )
	{
		var length = def.Size.x;
		var color = def.Color == Color.White ? FenceColor : def.Color;
		var picketH = 70f;
		var spacing = 26f;
		var count = Math.Max( 1, (int)MathF.Round( length / spacing ) );
		var step = length / count;
		var start = -length * 0.5f + step * 0.5f;

		for ( var i = 0; i < count; i++ )
		{
			var x = start + i * step;
			Box( root, "Picket", new Vector3( x, 0, picketH * 0.5f ), new Vector3( 20, 8, picketH ), color, default, GameMaterials.Wood );
		}

		Box( root, "RailLow", new Vector3( 0, 0, 24 ), new Vector3( length, 6, 10 ), color, default, GameMaterials.Wood );
		Box( root, "RailHigh", new Vector3( 0, 0, 52 ), new Vector3( length, 6, 10 ), color, default, GameMaterials.Wood );

		var posts = Math.Max( 2, (int)MathF.Round( length / 150f ) + 1 );
		for ( var i = 0; i < posts; i++ )
		{
			var x = -length * 0.5f + length * i / (posts - 1);
			Box( root, "Post", new Vector3( x, 0, 44 ), new Vector3( 16, 16, 88 ), TreeTrunk, default, GameMaterials.Bark );
		}
	}

	private static void BuildMailbox( GameObject root )
	{
		Box( root, "Post", new Vector3( 0, 0, 26 ), new Vector3( 9, 9, 52 ), Wood, default, GameMaterials.Bark );
		Box( root, "Box", new Vector3( 0, 0, 58 ), new Vector3( 20, 36, 24 ), new Color( 0.16f, 0.16f, 0.18f ), default, GameMaterials.Metal );
		Box( root, "Flag", new Vector3( 13, -20, 62 ), new Vector3( 3, 12, 16 ), new Color( 0.85f, 0.2f, 0.2f ), default, GameMaterials.Metal );
	}

	/// <summary>
	/// Foothills Gas &amp; Go: cream convenience store + red-trimmed canopy over pumps.
	/// Size = store footprint (width X, depth Y, wall height Z). Front faces −Y.
	/// Under-canopy asphalt is left open for JobDef dirty panels.
	/// </summary>
	private static void BuildFoothillsStation( GameObject root, DecorDef def )
	{
		var w = def.Size.x;
		var d = def.Size.y;
		var h = def.Size.z;
		var cream = def.Color == Color.White ? new Color( 0.93f, 0.89f, 0.80f ) : def.Color;
		var red = new Color( 0.86f, 0.18f, 0.14f );
		var dark = new Color( 0.28f, 0.30f, 0.34f );
		var pumpCream = new Color( 0.90f, 0.86f, 0.78f );
		var glass = new Color( 0.12f, 0.12f, 0.14f );
		var front = -d * 0.5f;

		// --- Store body (rear) ---
		var baseH = h * 0.22f;
		Box( root, "StoreBase", new Vector3( 0, 0, baseH * 0.5f ), new Vector3( w, d, baseH ), dark, default, GameMaterials.Concrete );
		Box( root, "StoreWalls", new Vector3( 0, 0, baseH + (h - baseH) * 0.5f ), new Vector3( w, d, h - baseH ), cream, default, GameMaterials.Concrete );
		// Roof slab, then HVAC/dish +1 above the roof top so they never share a face.
		var roofZ = h + 8f;
		var roofT = 14f;
		var roofTop = roofZ + roofT * 0.5f;
		Box( root, "StoreRoof", new Vector3( 0, 0, roofZ ), new Vector3( w + 16, d + 16, roofT ), cream, default, GameMaterials.Concrete );
		Box( root, "StoreStripe", new Vector3( 0, front - 3f, h - 18 ), new Vector3( w * 0.96f, 6, 16 ), red, default, GameMaterials.Metal );

		var faceSlot = 0;
		float FaceY() => front - DepthLayers.NextFaceDepth( ref faceSlot );
		Box( root, "StoreGlass", new Vector3( -w * 0.22f, FaceY(), h * 0.48f ), new Vector3( w * 0.42f, 5, h * 0.58f ), glass, default, GameMaterials.Metal );
		var doorY = FaceY();
		Box( root, "StoreDoor", new Vector3( w * 0.28f, doorY, h * 0.38f ), new Vector3( 52, 6, h * 0.66f ), dark, default, GameMaterials.Metal );
		// Window sits proud of the 6-thick door (a FaceY slot lands coplanar with its face).
		Box( root, "DoorWin", new Vector3( w * 0.28f, doorY - 3f, h * 0.52f ), new Vector3( 28, 4, 24 ), WindowGlass, default, GameMaterials.Metal );

		Box( root, "HVAC", new Vector3( -w * 0.18f, d * 0.1f, roofTop + 1f + 14f ), new Vector3( 70, 50, 28 ), dark, default, GameMaterials.Metal );
		Box( root, "Dish", new Vector3( w * 0.28f, -d * 0.05f, roofTop + 1f + 4f ), new Vector3( 36, 8, 8 ), dark, new Angles( 25, 0, 0 ), GameMaterials.Metal );

		// --- Pump canopy (extends forward toward spawn / −Y) ---
		var canopyW = w * 1.05f;
		var canopyD = d * 1.55f;
		var canopyCy = front - canopyD * 0.48f;
		var pillarH = h * 0.92f;
		var deckT = 22f;
		var deckZ = pillarH + deckT * 0.5f;
		var deckBottom = deckZ - deckT * 0.5f;
		var deckTop = deckZ + deckT * 0.5f;

		foreach ( var sx in new[] { -0.38f, 0.38f } )
		foreach ( var sy in new[] { -0.36f, 0.36f } )
			Box( root, "Pillar", new Vector3( canopyW * sx, canopyCy + canopyD * sy, pillarH * 0.5f ), new Vector3( 28, 28, pillarH ), dark, default, GameMaterials.Metal );

		Box( root, "Canopy", new Vector3( 0, canopyCy, deckZ ), new Vector3( canopyW, canopyD, deckT ), cream, default, GameMaterials.Concrete );
		// Red fascia rim: +1 above canopy top, nudged outward so faces aren't coplanar with the deck.
		var fasciaZ = deckTop + 1f;
		var fasciaH = 10f;
		Box( root, "FasciaF", new Vector3( 0, canopyCy - canopyD * 0.5f - 1f, fasciaZ ), new Vector3( canopyW + 2, 6, fasciaH ), red, default, GameMaterials.Metal );
		Box( root, "FasciaB", new Vector3( 0, canopyCy + canopyD * 0.5f + 1f, fasciaZ ), new Vector3( canopyW + 2, 6, fasciaH ), red, default, GameMaterials.Metal );
		Box( root, "FasciaL", new Vector3( -canopyW * 0.5f - 1f, canopyCy, fasciaZ ), new Vector3( 6, canopyD, fasciaH ), red, default, GameMaterials.Metal );
		Box( root, "FasciaR", new Vector3( canopyW * 0.5f + 1f, canopyCy, fasciaZ ), new Vector3( 6, canopyD, fasciaH ), red, default, GameMaterials.Metal );
		// Underside lights: −1 below canopy bottom.
		foreach ( var sx in new[] { -0.28f, 0.28f } )
		foreach ( var sy in new[] { -0.25f, 0.25f } )
			Box( root, "CanopyLight", new Vector3( canopyW * sx, canopyCy + canopyD * sy, deckBottom - 1f ), new Vector3( 36, 36, 4 ), Color.White, default, GameMaterials.Metal );

		// --- Pump islands (normal curb height on the pad) ---
		foreach ( var px in new[] { -canopyW * 0.22f, canopyW * 0.22f } )
		{
			Box( root, "Island", new Vector3( px, canopyCy, 7 ), new Vector3( 70, 160, 12 ), new Color( 0.72f, 0.72f, 0.70f ), default, GameMaterials.Concrete );
			Box( root, "Pump", new Vector3( px, canopyCy, 44 ), new Vector3( 36, 40, 72 ), pumpCream, default, GameMaterials.Metal );
			Box( root, "PumpTop", new Vector3( px, canopyCy, 84 ), new Vector3( 40, 44, 12 ), red, default, GameMaterials.Metal );
			Box( root, "PumpBot", new Vector3( px, canopyCy, 16 ), new Vector3( 40, 44, 10 ), red, default, GameMaterials.Metal );
			Box( root, "Screen", new Vector3( px, canopyCy - 18, 52 ), new Vector3( 28, 4, 22 ), dark, default, GameMaterials.Metal );
			Box( root, "BollardN", new Vector3( px, canopyCy + 70, 20 ), new Vector3( 12, 12, 36 ), new Color( 0.95f, 0.82f, 0.12f ), default, GameMaterials.Metal );
			Box( root, "BollardS", new Vector3( px, canopyCy - 70, 20 ), new Vector3( 12, 12, 36 ), new Color( 0.95f, 0.82f, 0.12f ), default, GameMaterials.Metal );
		}

		var col = root.Components.Create<BoxCollider>();
		col.Center = new Vector3( 0, 0, h * 0.5f );
		col.Scale = new Vector3( w, d, h );
		col.Static = true;
	}

	/// <summary>
	/// Laurent-style restaurant rear: beige wall facing −Y, foundation trim, doors, stoop, roof HVAC.
	/// Size = footprint (width X, depth Y, wall height Z). Keep rooftop props ±1 clear of the roof slab.
	/// </summary>
	private static void BuildSteakhouseAlley( GameObject root, DecorDef def )
	{
		var w = def.Size.x;
		var d = def.Size.y;
		var h = def.Size.z;
		var beige = def.Color == Color.White ? new Color( 0.90f, 0.84f, 0.72f ) : def.Color;
		var dark = new Color( 0.22f, 0.22f, 0.24f );
		var front = -d * 0.5f;

		var baseH = 28f;
		Box( root, "Foundation", new Vector3( 0, 0, baseH * 0.5f ), new Vector3( w + 8, d + 8, baseH ), dark, default, GameMaterials.Concrete );
		Box( root, "Walls", new Vector3( 0, 0, baseH + (h - baseH) * 0.5f ), new Vector3( w, d, h - baseH ), beige, default, GameMaterials.Concrete );

		var roofZ = h + 8f;
		var roofT = 16f;
		var roofTop = roofZ + roofT * 0.5f;
		Box( root, "Parapet", new Vector3( 0, 0, roofZ ), new Vector3( w + 20, d + 20, roofT ), dark, default, GameMaterials.Concrete );
		Box( root, "HVAC", new Vector3( -w * 0.15f, d * 0.05f, roofTop + 1f + 16f ), new Vector3( 90, 60, 32 ), new Color( 0.55f, 0.56f, 0.58f ), default, GameMaterials.Metal );

		var faceSlot = 0;
		float FaceY() => front - DepthLayers.NextFaceDepth( ref faceSlot );

		// Sign band (text approximated as dark metal bars). Bar sits a full unit proud of
		// the 5-thick band face — successive FaceY slots leave only a 0.5 gap and flicker.
		var bandY = FaceY();
		Box( root, "SignBand", new Vector3( w * 0.18f, bandY, h * 0.72f ), new Vector3( w * 0.42f, 5, 36 ), dark, default, GameMaterials.Metal );
		Box( root, "SignBar", new Vector3( w * 0.18f, bandY - 3f, h * 0.72f ), new Vector3( w * 0.38f, 4, 8 ), new Color( 0.12f, 0.12f, 0.12f ), default, GameMaterials.Metal );

		// Left employee door (flush).
		Box( root, "DoorL", new Vector3( -w * 0.32f, FaceY(), baseH + 52 ), new Vector3( 48, 6, 100 ), dark, default, GameMaterials.Metal );
		Box( root, "DoorLLight", new Vector3( -w * 0.32f, FaceY(), baseH + 110 ), new Vector3( 18, 8, 8 ), new Color( 0.95f, 0.92f, 0.75f ), default, GameMaterials.Metal );

		// Right door on stoop with rails.
		var doorX = w * 0.28f;
		Box( root, "Stoop", new Vector3( doorX, front - 28f, 10 ), new Vector3( 90, 54, 18 ), new Color( 0.72f, 0.72f, 0.70f ), default, GameMaterials.Concrete );
		Box( root, "Step1", new Vector3( doorX, front - 52f, 5 ), new Vector3( 88, 22, 8 ), new Color( 0.70f, 0.70f, 0.68f ), default, GameMaterials.Concrete );
		Box( root, "Step2", new Vector3( doorX, front - 68f, 2 ), new Vector3( 88, 18, 4 ), new Color( 0.68f, 0.68f, 0.66f ), default, GameMaterials.Concrete );
		Box( root, "RailL", new Vector3( doorX - 42f, front - 40f, 28 ), new Vector3( 4, 50, 36 ), dark, default, GameMaterials.Metal );
		Box( root, "RailR", new Vector3( doorX + 42f, front - 40f, 28 ), new Vector3( 4, 50, 36 ), dark, default, GameMaterials.Metal );
		Box( root, "DoorR", new Vector3( doorX, FaceY(), baseH + 58 ), new Vector3( 48, 6, 100 ), dark, default, GameMaterials.Metal );
		Box( root, "DoorRLight", new Vector3( doorX, FaceY(), baseH + 116 ), new Vector3( 18, 8, 8 ), new Color( 0.95f, 0.92f, 0.75f ), default, GameMaterials.Metal );

		// Gooseneck + electrical cabinet (alley utility).
		Box( root, "LampArm", new Vector3( -w * 0.02f, front - 20f, h * 0.78f ), new Vector3( 8, 36, 8 ), dark, default, GameMaterials.Metal );
		Box( root, "LampHead", new Vector3( -w * 0.02f, front - 40f, h * 0.74f ), new Vector3( 22, 16, 14 ), dark, default, GameMaterials.Metal );
		Box( root, "ElecBox", new Vector3( w * 0.42f, FaceY(), baseH + 54 ), new Vector3( 56, 10, 70 ), new Color( 0.48f, 0.50f, 0.52f ), default, GameMaterials.Metal );
		Box( root, "Vent", new Vector3( 0f, FaceY(), h * 0.55f ), new Vector3( 36, 6, 36 ), dark, default, GameMaterials.Metal );

		var wallCol = root.Components.Create<BoxCollider>();
		wallCol.Center = new Vector3( 0, 0, h * 0.5f );
		wallCol.Scale = new Vector3( w, d, h );
		wallCol.Static = true;
	}

	private static void BuildDumpster( GameObject root, DecorDef def )
	{
		var s = Math.Clamp( def.Size.x <= 0.01f ? 1f : def.Size.x, 0.5f, 2.5f );
		var green = def.Color == Color.White ? new Color( 0.28f, 0.62f, 0.34f ) : def.Color;
		Box( root, "Bin", new Vector3( 0, 0, 42 * s ), new Vector3( 110 * s, 70 * s, 84 * s ), green, default, GameMaterials.Metal );
		Box( root, "Lid", new Vector3( 0, -6 * s, 88 * s ), new Vector3( 114 * s, 74 * s, 10 * s ), new Color( 0.14f, 0.14f, 0.16f ), default, GameMaterials.Metal );
		Box( root, "Overflow", new Vector3( 8 * s, -18 * s, 96 * s ), new Vector3( 70 * s, 40 * s, 22 * s ), new Color( 0.86f, 0.84f, 0.76f ), default, GameMaterials.Concrete );
	}

	// --- Player van ---

	private static readonly Color[] VanBody =
	{
		new( 0.78f, 0.66f, 0.48f ),
		new( 0.84f, 0.74f, 0.54f ),
		new( 0.88f, 0.80f, 0.60f ),
		new( 0.92f, 0.84f, 0.66f ),
		new( 0.94f, 0.88f, 0.72f ),
	};

	private static Model _vanCloudModel;
	private static Model VanCloudModel =>
		_vanCloudModel is { IsValid: true, IsError: false }
			? _vanCloudModel
			: _vanCloudModel = Cloud.Model( "facepunch.van_dev" );

	/// <summary>
	/// Builds the player's van into <paramref name="root"/> and returns the parts so the
	/// caller can tear it down on a rebuild.
	/// </summary>
	public static List<GameObject> BuildVan( GameObject root, int tier )
	{
		tier = Math.Clamp( tier, 0, VanBody.Length - 1 );

		var cloud = VanCloudModel;
		if ( cloud is null || cloud.IsError )
			return BuildBoxVan( root, tier );

		var parts = new List<GameObject>();
		void Add( GameObject go ) => parts.Add( go );

		var chrome = new Color( 0.82f, 0.72f, 0.56f );
		var accent = tier >= 4 ? new Color( 1f, 0.8f, 0.25f ) : new Color( 0.95f, 0.55f, 0.15f );
		var tank = new Color( 0.4f, 0.62f, 0.86f );
		var vanScale = 1f + tier * 0.05f;

		var vanGo = new GameObject( root, true, "VanModel" );
		vanGo.LocalPosition = Vector3.Zero;
		vanGo.LocalScale = vanScale;
		var mr = vanGo.Components.Create<ModelRenderer>();
		mr.Model = cloud;
		mr.Tint = VanBody[tier];
		Add( vanGo );

		var col = vanGo.Components.Create<ModelCollider>();
		col.Model = cloud;
		col.Static = true;

		var bb = cloud.Bounds;
		var len = bb.Size.x * vanScale;
		var wid = bb.Size.y * vanScale;
		var top = bb.Maxs.z * vanScale;
		var midX = bb.Center.x * vanScale;

		if ( tier >= 2 )
		{
			var rackZ = top + 5f;
			Add( Box( root, "Rack", new Vector3( midX, 0, rackZ ), new Vector3( len * 0.66f, wid * 0.9f, 6f ), chrome, default, GameMaterials.Metal ) );
			Add( Box( root, "Tank", new Vector3( midX, 0, rackZ + 20f ), new Vector3( len * 0.42f, wid * 0.62f, 30f + tier * 4f ), tank, default, GameMaterials.Metal ) );
		}

		if ( tier >= 3 )
			Add( Box( root, "Ladder", new Vector3( midX - len * 0.12f, wid * 0.5f + 3f, top * 0.6f ), new Vector3( len * 0.4f, 4f, 10f ), chrome, default, GameMaterials.Metal ) );

		if ( tier >= 4 )
			Add( Box( root, "LightBar", new Vector3( midX + len * 0.28f, 0, top + 6f ), new Vector3( 12f, wid * 0.6f, 8f ), accent, default, GameMaterials.Metal ) );

		return parts;
	}

	private static List<GameObject> BuildBoxVan( GameObject root, int tier )
	{
		var parts = new List<GameObject>();
		tier = Math.Clamp( tier, 0, VanBody.Length - 1 );

		var body = VanBody[tier];
		var glass = new Color( 0.55f, 0.72f, 0.85f );
		var tire = new Color( 0.1f, 0.1f, 0.12f );
		var chrome = new Color( 0.82f, 0.72f, 0.56f );
		var accent = tier >= 4 ? new Color( 1f, 0.8f, 0.25f ) : new Color( 0.95f, 0.55f, 0.15f );
		var tank = new Color( 0.4f, 0.62f, 0.86f );

		var s = 1f + tier * 0.06f;
		var len = 210f * s;
		var wid = 98f;
		var hgt = 82f + tier * 8f;
		var floor = 20f;
		var bodyZ = floor + hgt * 0.5f;

		void Add( GameObject go ) => parts.Add( go );

		var wx = len * 0.30f;
		var wy = wid * 0.5f - 2f;
		foreach ( var sx in new[] { -1f, 1f } )
		foreach ( var sy in new[] { -1f, 1f } )
			Add( Box( root, "Wheel", new Vector3( sx * wx, sy * wy, floor - 2f ), new Vector3( 46f, 18f, 46f ), tire, default, GameMaterials.Metal ) );

		Add( Box( root, "Cargo", new Vector3( -len * 0.06f, 0, bodyZ ), new Vector3( len * 0.7f, wid, hgt ), body, default, GameMaterials.Metal ) );
		Add( Box( root, "Cab", new Vector3( len * 0.34f, 0, floor + hgt * 0.4f ), new Vector3( len * 0.3f, wid, hgt * 0.8f ), body, default, GameMaterials.Metal ) );
		Add( Box( root, "Hood", new Vector3( len * 0.46f, 0, floor + hgt * 0.22f ), new Vector3( len * 0.12f, wid * 0.98f, hgt * 0.4f ), body, default, GameMaterials.Metal ) );
		Add( Box( root, "Windshield", new Vector3( len * 0.42f, 0, floor + hgt * 0.62f ), new Vector3( 6f, wid * 0.86f, hgt * 0.3f ), glass, default, GameMaterials.Metal ) );
		foreach ( var sy in new[] { -1f, 1f } )
			Add( Box( root, "SideWin", new Vector3( len * 0.32f, sy * (wid * 0.5f - 1f), floor + hgt * 0.58f ), new Vector3( len * 0.16f, 4f, hgt * 0.24f ), glass, default, GameMaterials.Metal ) );
		foreach ( var sy in new[] { -1f, 1f } )
			Add( Box( root, "Light", new Vector3( len * 0.52f, sy * wid * 0.32f, floor + hgt * 0.18f ), new Vector3( 5f, 16f, 12f ), new Color( 1f, 0.95f, 0.7f ), default, GameMaterials.Metal ) );

		if ( tier >= 1 )
		{
			Add( Box( root, "BumperF", new Vector3( len * 0.52f, 0, floor - 2f ), new Vector3( 10f, wid + 6f, 16f ), chrome, default, GameMaterials.Metal ) );
			Add( Box( root, "BumperR", new Vector3( -len * 0.41f, 0, floor - 2f ), new Vector3( 10f, wid + 6f, 16f ), chrome, default, GameMaterials.Metal ) );
			foreach ( var sy in new[] { -1f, 1f } )
				Add( Box( root, "Stripe", new Vector3( -len * 0.06f, sy * (wid * 0.5f + 1f), bodyZ - hgt * 0.12f ), new Vector3( len * 0.66f, 3f, 12f ), accent, default, GameMaterials.Metal ) );
		}

		if ( tier >= 2 )
		{
			var rackZ = floor + hgt + 6f;
			Add( Box( root, "Rack", new Vector3( -len * 0.06f, 0, rackZ ), new Vector3( len * 0.68f, wid * 0.9f, 6f ), chrome, default, GameMaterials.Metal ) );
			Add( Box( root, "Tank", new Vector3( -len * 0.06f, 0, rackZ + 18f ), new Vector3( len * 0.44f, wid * 0.66f, 30f + tier * 4f ), tank, default, GameMaterials.Metal ) );
		}

		if ( tier >= 3 )
			Add( Box( root, "Ladder", new Vector3( -len * 0.2f, wid * 0.5f + 2f, bodyZ ), new Vector3( len * 0.4f, 4f, 10f ), chrome, default, GameMaterials.Metal ) );

		if ( tier >= 4 )
			Add( Box( root, "LightBar", new Vector3( len * 0.34f, 0, floor + hgt * 0.86f ), new Vector3( 12f, wid * 0.6f, 8f ), accent, default, GameMaterials.Metal ) );

		var colGo = new GameObject( root, true, "VanCollider" );
		colGo.LocalPosition = new Vector3( 0, 0, bodyZ );
		var col = colGo.Components.Create<BoxCollider>();
		col.Scale = new Vector3( len * 0.8f, wid, hgt + floor );
		col.Static = true;
		parts.Add( colGo );

		return parts;
	}
}
