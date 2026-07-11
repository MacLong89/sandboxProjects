namespace UnderPressure;

/// <summary>
/// Kind of composite decoration to assemble from box primitives. Each kind maps to a
/// builder in <see cref="Scenery"/> that stacks tinted boxes into a recognisable low-poly
/// prop, keeping the whole world on the same "dev box" pipeline (no authored models).
/// </summary>
public enum DecorKind
{
	House,
	Tree,
	Bush,
	Fence,
	Mailbox,
	Sign,
	Cloud,
	GasCanopy,
	Building,
	CarWashBay,
	Restaurant,
	Warehouse,
	EstateGarage,
	SubwayStation,
	ContainerYard,
	Yacht,
	WaterPlant,
	Cabin,
	HighwayUnderpass,
	DataCenter,
	Penthouse,
	MeatPlant,
	PlayerShop,
	BioLab,
	Locomotive,
	Museum,
	Dam,
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
/// Builds composite low-poly props out of the shared box primitive. Everything is flat
/// tinted geometry so the scene reads like the concept art without any authored assets.
/// </summary>
public static class Scenery
{
	private const float RadToDeg = 57.29578f;

	// --- Shared palette (warm, saturated low-poly look) ---
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
	private static readonly Color SignBoard = new( 0.14f, 0.12f, 0.10f );
	private static readonly Color SignAccent = new( 1f, 0.78f, 0.22f );

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
			case DecorKind.Sign: BuildSign( root, def ); break;
			case DecorKind.Cloud: BuildCloud( root, def ); break;
			case DecorKind.GasCanopy: BuildGasCanopy( root, def ); break;
			case DecorKind.Building: BuildBuilding( root, def ); break;
			case DecorKind.CarWashBay: BuildCarWashBay( root, def ); break;
			case DecorKind.Restaurant: BuildRestaurant( root, def ); break;
			case DecorKind.Warehouse: BuildWarehouse( root, def ); break;
			case DecorKind.EstateGarage: BuildEstateGarage( root, def ); break;
			case DecorKind.SubwayStation: BuildSubwayStation( root, def ); break;
			case DecorKind.ContainerYard: BuildContainerYard( root, def ); break;
			case DecorKind.Yacht: BuildYacht( root, def ); break;
			case DecorKind.WaterPlant: BuildWaterPlant( root, def ); break;
			case DecorKind.Cabin: BuildCabin( root, def ); break;
			case DecorKind.HighwayUnderpass: BuildHighwayUnderpass( root, def ); break;
			case DecorKind.DataCenter: BuildDataCenter( root, def ); break;
			case DecorKind.Penthouse: BuildPenthouse( root, def ); break;
			case DecorKind.MeatPlant: BuildMeatPlant( root, def ); break;
			case DecorKind.PlayerShop: BuildPlayerShop( root, def ); break;
			case DecorKind.BioLab: BuildBioLab( root, def ); break;
			case DecorKind.Locomotive: BuildLocomotive( root, def ); break;
			case DecorKind.Museum: BuildMuseum( root, def ); break;
			case DecorKind.Dam: BuildDam( root, def ); break;
		}
	}

	/// <summary>
	/// Core helper: a single tinted box at a local position/size/rotation. Pass a
	/// <paramref name="material"/> to add surface detail (it is tinted by <paramref name="color"/>);
	/// omit it to use the flat dev material.
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
		// Size = footprint (width X, depth Y, wall height Z). Front face is -Y (toward player).
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

	// Authored tree models brought over from the terrain generator. They are textured
	// meshes (aspen / oak / pine) that replace the old stacked-box trees.
	private static readonly string[] TreeModels =
	{
		"models/foliage2/pine_tree.vmdl",
		"models/foliage2/aspen_tree.vmdl",
		"models/foliage2/oak_tree.vmdl",
	};

	// Height (inches) a Size.x == 1 tree stands, before the per-placement scale is applied.
	private const float TreeTargetHeightInches = 240f;

	private static void BuildTree( GameObject root, DecorDef def )
	{
		var s = def.Size.x <= 0 ? 1f : def.Size.x;

		// Pick a species + spin deterministically from the placement so every client
		// (and every rebuild) agrees on the same tree in the same spot.
		var seed = System.HashCode.Combine( (int)MathF.Round( def.Position.x ), (int)MathF.Round( def.Position.y ) );
		var model = Model.Load( TreeModels[(int)((uint)seed % (uint)TreeModels.Length)] );

		if ( model is null || model.IsError )
		{
			BuildBoxTree( root, def );
			return;
		}

		// Some foliage2 meshes are authored sub-inch (meters), so recover the intended height
		// the same way the terrain generator does before scaling to the target world height.
		var meshHeight = model.Bounds.Size.z;
		if ( meshHeight <= 32f )
			meshHeight *= 39.37f;
		meshHeight = MathF.Max( meshHeight, 1f );

		var targetHeight = TreeTargetHeightInches * s;
		var scale = targetHeight / meshHeight;

		// The mesh origin often sits at the model's center, so placing it at ground level buries
		// the lower half. Lift by the (scaled) distance from the origin down to the mesh bottom
		// so the trunk base rests on the floor. Bounds.Mins is in unscaled model space.
		var baseZ = model.Bounds.Mins.z;
		var lift = -baseZ * scale;

		var go = new GameObject( root, true, "TreeModel" );
		go.LocalPosition = new Vector3( 0, 0, lift );
		go.LocalRotation = Rotation.FromYaw( (seed % 360 + 360) % 360 );
		go.LocalScale = scale;

		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = model;

		// A slim trunk collider so the player bumps the tree instead of walking through it.
		// Everything is expressed in local (unscaled) space; start it at the mesh bottom so it
		// stays grounded now that the model is lifted.
		var col = go.Components.Create<CapsuleCollider>();
		col.Start = new Vector3( 0, 0, baseZ );
		col.End = new Vector3( 0, 0, baseZ + targetHeight * 0.6f / scale );
		col.Radius = 8f / scale;
		col.Static = true;
	}

	/// <summary>Fallback stacked-box tree used when the authored model fails to load.</summary>
	private static void BuildBoxTree( GameObject root, DecorDef def )
	{
		var s = def.Size.x <= 0 ? 1f : def.Size.x;
		var trunkH = 70f * s;
		var leaf = def.Color == Color.White ? TreeLeafA : def.Color;

		Box( root, "Trunk", new Vector3( 0, 0, trunkH * 0.5f ), new Vector3( 16 * s, 16 * s, trunkH ), TreeTrunk, default, GameMaterials.Bark );
		Box( root, "Leaf1", new Vector3( 0, 0, trunkH + 26 * s ), new Vector3( 78 * s, 78 * s, 62 * s ), leaf, default, GameMaterials.Leaves );
		Box( root, "Leaf2", new Vector3( 0, 0, trunkH + 66 * s ), new Vector3( 54 * s, 54 * s, 48 * s ), TreeLeafB, new Angles( 0, 45, 0 ), GameMaterials.Leaves );
		Box( root, "Leaf3", new Vector3( 6 * s, -4 * s, trunkH + 40 * s ), new Vector3( 40 * s, 40 * s, 38 * s ), leaf, new Angles( 0, 22, 0 ), GameMaterials.Leaves );
	}

	private static void BuildBush( GameObject root, DecorDef def )
	{
		var s = def.Size.x <= 0 ? 1f : def.Size.x;
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

	private static void BuildSign( GameObject root, DecorDef def )
	{
		var color = def.Color == Color.White ? SignBoard : def.Color;
		Box( root, "PostL", new Vector3( -42, 0, 34 ), new Vector3( 9, 9, 68 ), Wood, default, GameMaterials.Bark );
		Box( root, "PostR", new Vector3( 42, 0, 34 ), new Vector3( 9, 9, 68 ), Wood, default, GameMaterials.Bark );
		Box( root, "Board", new Vector3( 0, 0, 84 ), new Vector3( 128, 7, 58 ), color, default, GameMaterials.Wood );
		Box( root, "Accent", new Vector3( 0, -4, 70 ), new Vector3( 128, 3, 16 ), SignAccent );
	}

	private static void BuildGasCanopy( GameObject root, DecorDef def )
	{
		// Size = span (width X, depth Y, clearance height Z). Colour tints the roof band.
		var w = def.Size.x;
		var d = def.Size.y;
		var h = def.Size.z;
		var white = new Color( 0.90f, 0.82f, 0.66f );

		Box( root, "Deck", new Vector3( 0, 0, h + 30 ), new Vector3( w + 40, d + 40, 24 ), white, default, GameMaterials.Concrete );
		Box( root, "Band", new Vector3( 0, 0, h + 14 ), new Vector3( w + 46, d + 46, 16 ), def.Color, default, GameMaterials.Metal );

		var px = w * 0.5f - 30;
		var py = d * 0.5f - 30;
		foreach ( var sx in new[] { -1f, 1f } )
		foreach ( var sy in new[] { -1f, 1f } )
			Box( root, "Pillar", new Vector3( sx * px, sy * py, h * 0.5f ), new Vector3( 26, 26, h ), white, default, GameMaterials.Metal );

		// A couple of fuel pumps under the canopy.
		Box( root, "PumpL", new Vector3( -w * 0.2f, 0, 34 ), new Vector3( 34, 60, 68 ), new Color( 0.85f, 0.2f, 0.2f ), default, GameMaterials.Metal );
		Box( root, "PumpR", new Vector3( w * 0.2f, 0, 34 ), new Vector3( 34, 60, 68 ), new Color( 0.85f, 0.2f, 0.2f ), default, GameMaterials.Metal );
	}

	private static void BuildBuilding( GameObject root, DecorDef def )
	{
		var w = def.Size.x;
		var d = def.Size.y;
		var h = def.Size.z;
		var front = -d * 0.5f;

		Box( root, "Walls", new Vector3( 0, 0, h * 0.5f ), new Vector3( w, d, h ), def.Color, default, GameMaterials.Concrete );
		Box( root, "Parapet", new Vector3( 0, 0, h + 8 ), new Vector3( w + 12, d + 12, 16 ), new Color( 0.9f, 0.2f, 0.2f ), default, GameMaterials.Metal );
		var faceSlot = 0;
		float FaceY() => front - DepthLayers.NextFaceDepth( ref faceSlot );

		Box( root, "Sign", new Vector3( 0, FaceY(), h * 0.62f ), new Vector3( w * 0.5f, 6, 40 ), new Color( 0.9f, 0.2f, 0.2f ), default, GameMaterials.Metal );
		Box( root, "Glass", new Vector3( 0, FaceY(), h * 0.32f ), new Vector3( w * 0.72f, 4, h * 0.4f ), WindowGlass, default, GameMaterials.Metal );
		Box( root, "Door", new Vector3( 0, FaceY(), h * 0.26f ), new Vector3( 60, 6, h * 0.5f ), new Color( 0.35f, 0.4f, 0.46f ), default, GameMaterials.Metal );

		var col = root.Components.Create<BoxCollider>();
		col.Center = new Vector3( 0, 0, h * 0.5f );
		col.Scale = new Vector3( w, d, h );
		col.Static = true;
	}

	private static void BuildCarWashBay( GameObject root, DecorDef def )
	{
		var w = def.Size.x;
		var d = def.Size.y;
		var h = def.Size.z;
		var wall = new Color( 0.72f, 0.74f, 0.78f );
		var trim = new Color( 0.9f, 0.2f, 0.2f );
		var curtain = new Color( 0.42f, 0.78f, 0.96f, 0.55f );

		Box( root, "FloorLip", new Vector3( 0, 0, 2 ), new Vector3( w + 20, d + 20, 4 ), Foundation, default, GameMaterials.Concrete );
		Box( root, "WallL", new Vector3( -w * 0.5f - 14, 0, h * 0.45f ), new Vector3( 28, d + 30, h * 0.9f ), wall, default, GameMaterials.Metal );
		Box( root, "WallR", new Vector3( w * 0.5f + 14, 0, h * 0.45f ), new Vector3( 28, d + 30, h * 0.9f ), wall, default, GameMaterials.Metal );
		Box( root, "BackWall", new Vector3( 0, d * 0.5f + 14, h * 0.45f ), new Vector3( w + 56, 28, h * 0.9f ), wall, default, GameMaterials.Metal );
		Box( root, "Beam", new Vector3( 0, 0, h + 8 ), new Vector3( w + 40, d + 40, 18 ), trim, default, GameMaterials.Metal );
		Box( root, "CurtainL", new Vector3( -w * 0.22f, -d * 0.5f - 4, h * 0.42f ), new Vector3( w * 0.35f, 6, h * 0.75f ), curtain, default, GameMaterials.Metal );
		Box( root, "CurtainR", new Vector3( w * 0.22f, -d * 0.5f - 4, h * 0.42f ), new Vector3( w * 0.35f, 6, h * 0.75f ), curtain, default, GameMaterials.Metal );
		Box( root, "BrushRailL", new Vector3( -w * 0.38f, 0, 24 ), new Vector3( 10, d * 0.7f, 48 ), trim );
		Box( root, "BrushRailR", new Vector3( w * 0.38f, 0, 24 ), new Vector3( 10, d * 0.7f, 48 ), trim );
	}

	private static void BuildRestaurant( GameObject root, DecorDef def )
	{
		var w = def.Size.x;
		var d = def.Size.y;
		var h = def.Size.z;
		var brick = def.Color == Color.White ? new Color( 0.82f, 0.32f, 0.24f ) : def.Color;
		var front = -d * 0.5f;
		var faceSlot = 0;
		float FaceY() => front - DepthLayers.NextFaceDepth( ref faceSlot );

		Box( root, "Kitchen", new Vector3( 0, 0, h * 0.5f ), new Vector3( w, d, h ), brick, default, GameMaterials.Concrete );
		Box( root, "Roof", new Vector3( 0, 0, h + 10 ), new Vector3( w + 16, d + 16, 20 ), new Color( 0.22f, 0.22f, 0.24f ), default, GameMaterials.Metal );
		Box( root, "Exhaust", new Vector3( w * 0.25f, d * 0.15f, h + 36 ), new Vector3( 80, 50, 40 ), new Color( 0.5f, 0.5f, 0.52f ), default, GameMaterials.Metal );
		Box( root, "AC", new Vector3( -w * 0.28f, d * 0.1f, h + 28 ), new Vector3( 60, 40, 28 ), new Color( 0.62f, 0.64f, 0.66f ), default, GameMaterials.Metal );
		Box( root, "BackDoor", new Vector3( 0, FaceY(), h * 0.28f ), new Vector3( 72, 6, h * 0.52f ), new Color( 0.34f, 0.36f, 0.38f ), default, GameMaterials.Metal );
		Box( root, "Neon", new Vector3( 0, FaceY(), h * 0.78f ), new Vector3( w * 0.55f, 5, 28 ), new Color( 1f, 0.45f, 0.65f ), default, GameMaterials.Metal );
		Box( root, "Dumpster", new Vector3( -w * 0.42f, -d * 0.2f, 34 ), new Vector3( 90, 140, 68 ), new Color( 0.16f, 0.42f, 0.18f ), default, GameMaterials.Metal );
		Box( root, "GreaseTrap", new Vector3( w * 0.3f, -d * 0.15f, 8 ), new Vector3( 70, 70, 16 ), new Color( 0.48f, 0.48f, 0.50f ), default, GameMaterials.Metal );

		var col = root.Components.Create<BoxCollider>();
		col.Center = new Vector3( 0, 0, h * 0.5f );
		col.Scale = new Vector3( w, d, h );
		col.Static = true;
	}

	private static void BuildWarehouse( GameObject root, DecorDef def )
	{
		var w = def.Size.x;
		var d = def.Size.y;
		var h = def.Size.z;
		var front = -d * 0.5f;
		var faceSlot = 0;
		float FaceY() => front - DepthLayers.NextFaceDepth( ref faceSlot );
		var steel = def.Color == Color.White ? new Color( 0.58f, 0.58f, 0.60f ) : def.Color;

		Box( root, "Shell", new Vector3( 0, 0, h * 0.5f ), new Vector3( w, d, h ), steel, default, GameMaterials.Metal );
		Box( root, "DockLip", new Vector3( 0, front - 30, 10 ), new Vector3( w * 0.7f, 40, 20 ), Foundation, default, GameMaterials.Concrete );
		Box( root, "RollDoor", new Vector3( 0, FaceY(), h * 0.38f ), new Vector3( w * 0.55f, 6, h * 0.72f ), new Color( 0.42f, 0.44f, 0.46f ), default, GameMaterials.Metal );
		Box( root, "PalletA", new Vector3( -w * 0.28f, d * 0.2f, 28 ), new Vector3( 80, 80, 56 ), Wood, default, GameMaterials.Wood );
		Box( root, "PalletB", new Vector3( w * 0.25f, d * 0.28f, 28 ), new Vector3( 80, 80, 56 ), Wood, default, GameMaterials.Wood );
		Box( root, "Warning", new Vector3( 0, FaceY(), h * 0.82f ), new Vector3( 120, 5, 36 ), new Color( 1f, 0.82f, 0.08f ), default, GameMaterials.Metal );
	}

	private static void BuildEstateGarage( GameObject root, DecorDef def )
	{
		var w = def.Size.x;
		var d = def.Size.y;
		var h = def.Size.z;
		var front = -d * 0.5f;
		var faceSlot = 0;
		float FaceY() => front - DepthLayers.NextFaceDepth( ref faceSlot );
		var stone = def.Color == Color.White ? new Color( 0.68f, 0.64f, 0.58f ) : def.Color;

		Box( root, "Foundation", new Vector3( 0, 0, 8 ), new Vector3( w + 24, d + 24, 16 ), Foundation, default, GameMaterials.Concrete );
		Box( root, "Walls", new Vector3( 0, 0, h * 0.5f + 10 ), new Vector3( w, d, h ), stone, default, GameMaterials.Concrete );
		BuildGableRoof( root, w, d, h + 10 );
		Box( root, "GarageDoor", new Vector3( 0, FaceY(), h * 0.42f ), new Vector3( w * 0.72f, 6, h * 0.78f ), Garage, default, GameMaterials.Metal );
		Box( root, "PillarL", new Vector3( -w * 0.38f, FaceY(), h * 0.5f ), new Vector3( 20, 8, h * 0.9f ), stone, default, GameMaterials.Concrete );
		Box( root, "PillarR", new Vector3( w * 0.38f, FaceY(), h * 0.5f ), new Vector3( 20, 8, h * 0.9f ), stone, default, GameMaterials.Concrete );
	}

	private static void BuildSubwayStation( GameObject root, DecorDef def )
	{
		var w = def.Size.x;
		var d = def.Size.y;
		var h = def.Size.z;
		var tile = new Color( 0.88f, 0.86f, 0.82f );
		var trim = new Color( 0.22f, 0.48f, 0.82f );

		Box( root, "Canopy", new Vector3( 0, 0, h + 20 ), new Vector3( w + 40, d + 40, 24 ), trim, default, GameMaterials.Metal );
		Box( root, "WallL", new Vector3( -w * 0.5f - 18, 0, h * 0.5f ), new Vector3( 36, d + 20, h ), tile, default, GameMaterials.Concrete );
		Box( root, "WallR", new Vector3( w * 0.5f + 18, 0, h * 0.5f ), new Vector3( 36, d + 20, h ), tile, default, GameMaterials.Concrete );
		Box( root, "Bench", new Vector3( -w * 0.2f, -d * 0.25f, 22 ), new Vector3( 140, 40, 44 ), trim, default, GameMaterials.Metal );
		Box( root, "Mosaic", new Vector3( 0, d * 0.42f, h * 0.55f ), new Vector3( w * 0.6f, 6, h * 0.35f ), trim, default, GameMaterials.Metal );
		Box( root, "Track", new Vector3( 0, d * 0.35f, 3 ), new Vector3( w, 30, 6 ), new Color( 0.28f, 0.28f, 0.30f ), default, GameMaterials.Metal );
	}

	private static void BuildContainerYard( GameObject root, DecorDef def )
	{
		var s = def.Size.x;
		var rust = new Color( 0.48f, 0.30f, 0.22f );
		var blue = new Color( 0.22f, 0.42f, 0.72f );
		var olive = new Color( 0.38f, 0.44f, 0.28f );

		Box( root, "Center", new Vector3( 0, 40, s * 0.42f ), new Vector3( s, s * 0.82f, s * 0.84f ), rust, default, GameMaterials.Metal );
		Box( root, "Left", new Vector3( -s * 1.05f, -20, s * 0.38f ), new Vector3( s * 0.9f, s * 0.82f, s * 0.76f ), blue, default, GameMaterials.Metal );
		Box( root, "Right", new Vector3( s * 1.05f, -20, s * 0.38f ), new Vector3( s * 0.9f, s * 0.82f, s * 0.76f ), olive, default, GameMaterials.Metal );
		Box( root, "Door", new Vector3( 0, -s * 0.41f, s * 0.3f ), new Vector3( s * 0.7f, 6, s * 0.55f ), new Color( 0.32f, 0.34f, 0.36f ), default, GameMaterials.Metal );
	}

	private static void BuildYacht( GameObject root, DecorDef def )
	{
		var w = def.Size.x;
		var d = def.Size.y;
		var h = def.Size.z;
		var hull = new Color( 0.92f, 0.94f, 0.96f );
		var deck = def.Color == Color.White ? new Color( 0.96f, 0.70f, 0.26f ) : def.Color;
		var cabin = new Color( 0.78f, 0.82f, 0.88f );

		Box( root, "Hull", new Vector3( 0, 0, h * 0.22f ), new Vector3( w, d, h * 0.44f ), hull, default, GameMaterials.Metal );
		Box( root, "Deck", new Vector3( 0, -d * 0.05f, h * 0.48f ), new Vector3( w * 0.92f, d * 0.88f, 8 ), deck, default, GameMaterials.Wood );
		Box( root, "Cabin", new Vector3( 0, d * 0.18f, h * 0.72f ), new Vector3( w * 0.45f, d * 0.42f, h * 0.5f ), cabin, default, GameMaterials.Metal );
		Box( root, "RailL", new Vector3( -w * 0.46f, 0, h * 0.56f ), new Vector3( 6, d * 0.9f, 28 ), new Color( 0.82f, 0.72f, 0.56f ), default, GameMaterials.Metal );
		Box( root, "RailR", new Vector3( w * 0.46f, 0, h * 0.56f ), new Vector3( 6, d * 0.9f, 28 ), new Color( 0.82f, 0.72f, 0.56f ), default, GameMaterials.Metal );
		Box( root, "Mast", new Vector3( 0, d * 0.3f, h * 1.1f ), new Vector3( 12, 12, h * 0.9f ), new Color( 0.62f, 0.38f, 0.14f ), default, GameMaterials.Bark );
	}

	private static void BuildWaterPlant( GameObject root, DecorDef def )
	{
		var w = def.Size.x;
		var h = def.Size.z;
		var concrete = new Color( 0.62f, 0.62f, 0.64f );
		var pipe = new Color( 0.42f, 0.78f, 0.96f );

		Box( root, "Basin", new Vector3( 0, 40, h * 0.25f ), new Vector3( w, w * 0.7f, h * 0.5f ), concrete, default, GameMaterials.Concrete );
		Box( root, "Control", new Vector3( -w * 0.35f, -60, h * 0.55f ), new Vector3( w * 0.35f, w * 0.35f, h * 0.7f ), def.Color == Color.White ? concrete : def.Color, default, GameMaterials.Metal );
		Box( root, "Tank", new Vector3( w * 0.3f, -40, h * 0.75f ), new Vector3( w * 0.28f, w * 0.28f, h ), pipe, default, GameMaterials.Metal );
		Box( root, "PipeA", new Vector3( 0, 0, h * 0.45f ), new Vector3( w * 0.8f, 20, 20 ), pipe, default, GameMaterials.Metal );
		Box( root, "PipeB", new Vector3( w * 0.15f, 80, h * 0.65f ), new Vector3( 20, w * 0.5f, 20 ), pipe, default, GameMaterials.Metal );
	}

	private static void BuildCabin( GameObject root, DecorDef def )
	{
		var w = def.Size.x;
		var d = def.Size.y;
		var h = def.Size.z;
		var log = def.Color == Color.White ? new Color( 0.52f, 0.34f, 0.18f ) : def.Color;
		var front = -d * 0.5f;
		var faceSlot = 0;
		float FaceY() => front - DepthLayers.NextFaceDepth( ref faceSlot );

		Box( root, "Walls", new Vector3( 0, 0, h * 0.5f ), new Vector3( w, d, h ), log, default, GameMaterials.Wood );
		BuildGableRoof( root, w, d, h );
		Box( root, "Porch", new Vector3( 0, front - 40, 8 ), new Vector3( w * 0.7f, 80, 16 ), Wood, default, GameMaterials.Wood );
		Box( root, "Door", new Vector3( 0, FaceY(), h * 0.3f ), new Vector3( 52, 6, h * 0.55f ), DoorColor, default, GameMaterials.Wood );
		Box( root, "Chimney", new Vector3( w * 0.28f, d * 0.1f, h + 40 ), new Vector3( 36, 36, 50 ), new Color( 0.42f, 0.40f, 0.38f ), default, GameMaterials.Concrete );
	}

	private static void BuildHighwayUnderpass( GameObject root, DecorDef def )
	{
		var w = def.Size.x;
		var d = def.Size.y;
		var h = def.Size.z;
		var concrete = new Color( 0.56f, 0.54f, 0.52f );

		Box( root, "Deck", new Vector3( 0, 0, h + 20 ), new Vector3( w + 80, d + 80, 36 ), concrete, default, GameMaterials.Concrete );
		Box( root, "PillarL", new Vector3( -w * 0.38f, 0, h * 0.5f ), new Vector3( 60, 60, h ), concrete, default, GameMaterials.Concrete );
		Box( root, "PillarR", new Vector3( w * 0.38f, 0, h * 0.5f ), new Vector3( 60, 60, h ), concrete, default, GameMaterials.Concrete );
		Box( root, "Guardrail", new Vector3( 0, -d * 0.42f, 42 ), new Vector3( w, 10, 36 ), new Color( 0.72f, 0.72f, 0.74f ), default, GameMaterials.Metal );
		Box( root, "CrashScorch", new Vector3( 40, 30, 2 ), new Vector3( 120, 90, 3 ), new Color( 0.18f, 0.16f, 0.14f ), default, GameMaterials.Concrete );
	}

	private static void BuildDataCenter( GameObject root, DecorDef def )
	{
		var w = def.Size.x;
		var d = def.Size.y;
		var h = def.Size.z;
		var shell = new Color( 0.24f, 0.24f, 0.26f );
		var glow = new Color( 0.22f, 0.88f, 1f );

		Box( root, "Shell", new Vector3( 0, 0, h * 0.5f ), new Vector3( w, d, h ), shell, default, GameMaterials.Metal );
		for ( var i = -2; i <= 2; i++ )
			Box( root, $"Rack_{i}", new Vector3( i * 70f, d * 0.15f, h * 0.35f ), new Vector3( 40, 30, h * 0.65f ), new Color( 0.14f, 0.14f, 0.16f ), default, GameMaterials.Metal );
		Box( root, "GlassWall", new Vector3( 0, -d * 0.5f - 3, h * 0.45f ), new Vector3( w * 0.85f, 6, h * 0.75f ), WindowGlass, default, GameMaterials.Metal );
		Box( root, "LedStrip", new Vector3( 0, -d * 0.5f - 6, h * 0.82f ), new Vector3( w * 0.7f, 4, 10 ), glow, default, GameMaterials.Metal );
	}

	private static void BuildPenthouse( GameObject root, DecorDef def )
	{
		var w = def.Size.x;
		var d = def.Size.y;
		var h = def.Size.z;
		var glass = new Color( 0.58f, 0.78f, 0.92f );
		var frame = new Color( 0.42f, 0.44f, 0.48f );

		Box( root, "Tower", new Vector3( 0, 0, h * 0.55f ), new Vector3( w * 0.55f, d * 0.55f, h * 1.1f ), frame, default, GameMaterials.Concrete );
		Box( root, "Penthouse", new Vector3( 0, 0, h * 0.82f ), new Vector3( w, d, h * 0.36f ), glass, default, GameMaterials.Metal );
		Box( root, "Terrace", new Vector3( 0, -d * 0.42f, h * 0.62f ), new Vector3( w * 0.9f, 40, 8 ), new Color( 0.72f, 0.72f, 0.74f ), default, GameMaterials.Concrete );
		Box( root, "Helipad", new Vector3( w * 0.32f, d * 0.2f, h + 8 ), new Vector3( 80, 80, 4 ), new Color( 0.9f, 0.9f, 0.92f ), default, GameMaterials.Concrete );
	}

	private static void BuildMeatPlant( GameObject root, DecorDef def )
	{
		var w = def.Size.x;
		var d = def.Size.y;
		var h = def.Size.z;
		var steel = new Color( 0.78f, 0.80f, 0.82f );
		var frost = new Color( 0.86f, 0.90f, 0.94f );

		Box( root, "Plant", new Vector3( 0, 0, h * 0.5f ), new Vector3( w, d, h ), frost, default, GameMaterials.Metal );
		Box( root, "Rail", new Vector3( 0, 0, h * 0.82f ), new Vector3( w * 0.8f, 10, 10 ), steel, default, GameMaterials.Metal );
		for ( var i = -2; i <= 2; i++ )
			Box( root, $"Hook_{i}", new Vector3( i * 55f, 0, h * 0.72f ), new Vector3( 6, 6, 36 ), steel, default, GameMaterials.Metal );
		Box( root, "Drain", new Vector3( 0, -d * 0.25f, 4 ), new Vector3( 80, 80, 8 ), new Color( 0.34f, 0.36f, 0.38f ), default, GameMaterials.Metal );
	}

	private static void BuildPlayerShop( GameObject root, DecorDef def )
	{
		var w = def.Size.x;
		var d = def.Size.y;
		var h = def.Size.z;
		var cream = new Color( 0.88f, 0.80f, 0.60f );
		var accent = new Color( 0.22f, 0.62f, 0.92f );
		var front = -d * 0.5f;
		var faceSlot = 0;
		float FaceY() => front - DepthLayers.NextFaceDepth( ref faceSlot );

		Box( root, "Shop", new Vector3( 0, 0, h * 0.5f ), new Vector3( w, d, h ), cream, default, GameMaterials.Metal );
		Box( root, "GarageBay", new Vector3( -w * 0.22f, FaceY(), h * 0.35f ), new Vector3( w * 0.45f, 6, h * 0.68f ), Garage, default, GameMaterials.Metal );
		Box( root, "Sign", new Vector3( 0, FaceY(), h * 0.82f ), new Vector3( w * 0.75f, 6, 42 ), accent, default, GameMaterials.Metal );
		Box( root, "VanPad", new Vector3( w * 0.28f, -d * 0.1f, 2 ), new Vector3( 120, 160, 4 ), Foundation, default, GameMaterials.Concrete );
	}

	private static void BuildBioLab( GameObject root, DecorDef def )
	{
		var w = def.Size.x;
		var d = def.Size.y;
		var h = def.Size.z;
		var white = new Color( 0.92f, 0.94f, 0.96f );
		var hazard = new Color( 0.22f, 0.88f, 1f );

		Box( root, "Lab", new Vector3( 0, 0, h * 0.5f ), new Vector3( w, d, h ), white, default, GameMaterials.Metal );
		Box( root, "Airlock", new Vector3( 0, -d * 0.5f - 4, h * 0.35f ), new Vector3( 80, 8, h * 0.65f ), new Color( 0.72f, 0.74f, 0.76f ), default, GameMaterials.Metal );
		Box( root, "Window", new Vector3( 0, -d * 0.5f - 4, h * 0.55f ), new Vector3( w * 0.55f, 6, h * 0.35f ), WindowGlass, default, GameMaterials.Metal );
		Box( root, "HazardStripe", new Vector3( 0, -d * 0.5f - 7, 18 ), new Vector3( w * 0.7f, 4, 20 ), hazard, default, GameMaterials.Metal );
	}

	private static void BuildLocomotive( GameObject root, DecorDef def )
	{
		var len = def.Size.x;
		var h = def.Size.z;
		var body = new Color( 0.48f, 0.30f, 0.22f );
		var cab = new Color( 0.92f, 0.82f, 0.66f );

		Box( root, "Engine", new Vector3( 0, 0, h * 0.38f ), new Vector3( len, len * 0.42f, h * 0.76f ), body, default, GameMaterials.Metal );
		Box( root, "Cab", new Vector3( len * 0.28f, 0, h * 0.62f ), new Vector3( len * 0.32f, len * 0.44f, h * 0.5f ), cab, default, GameMaterials.Metal );
		Box( root, "Stack", new Vector3( -len * 0.18f, 0, h * 0.92f ), new Vector3( 36, 36, h * 0.45f ), new Color( 0.22f, 0.22f, 0.24f ), default, GameMaterials.Metal );
		Box( root, "Track", new Vector3( 0, -len * 0.35f, 3 ), new Vector3( len * 1.4f, 24, 6 ), new Color( 0.28f, 0.28f, 0.30f ), default, GameMaterials.Metal );
	}

	private static void BuildMuseum( GameObject root, DecorDef def )
	{
		var w = def.Size.x;
		var d = def.Size.y;
		var h = def.Size.z;
		var marble = new Color( 0.88f, 0.86f, 0.82f );
		var gold = new Color( 0.92f, 0.72f, 0.28f );
		var front = -d * 0.5f;
		var faceSlot = 0;
		float FaceY() => front - DepthLayers.NextFaceDepth( ref faceSlot );

		Box( root, "Hall", new Vector3( 0, 0, h * 0.5f ), new Vector3( w, d, h ), marble, default, GameMaterials.Concrete );
		Box( root, "Steps", new Vector3( 0, front - 50, 12 ), new Vector3( w * 0.8f, 100, 24 ), marble, default, GameMaterials.Concrete );
		Box( root, "ColumnsL", new Vector3( -w * 0.32f, FaceY(), h * 0.45f ), new Vector3( 28, 28, h * 0.9f ), marble, default, GameMaterials.Concrete );
		Box( root, "ColumnsR", new Vector3( w * 0.32f, FaceY(), h * 0.45f ), new Vector3( 28, 28, h * 0.9f ), marble, default, GameMaterials.Concrete );
		Box( root, "Banner", new Vector3( 0, FaceY(), h * 0.78f ), new Vector3( w * 0.5f, 6, 48 ), gold, default, GameMaterials.Metal );
	}

	private static void BuildDam( GameObject root, DecorDef def )
	{
		var w = def.Size.x;
		var h = def.Size.z;
		var concrete = new Color( 0.62f, 0.62f, 0.64f );
		var water = new Color( 0.22f, 0.62f, 0.88f );

		Box( root, "SpillwayL", new Vector3( -w * 0.28f, 60, h * 0.45f ), new Vector3( w * 0.4f, w * 0.35f, h * 0.9f ), concrete, default, GameMaterials.Concrete );
		Box( root, "SpillwayR", new Vector3( w * 0.28f, 60, h * 0.45f ), new Vector3( w * 0.4f, w * 0.35f, h * 0.9f ), concrete, default, GameMaterials.Concrete );
		Box( root, "Pool", new Vector3( 0, -w * 0.35f, 4 ), new Vector3( w * 1.1f, w * 0.55f, 8 ), water, default, GameMaterials.Metal );
		Box( root, "ValveHouse", new Vector3( 0, w * 0.15f, h * 0.3f ), new Vector3( w * 0.35f, w * 0.28f, h * 0.55f ), def.Color == Color.White ? concrete : def.Color, default, GameMaterials.Metal );
	}

	// --- Player van (tiered, built at runtime by the Van component) ---

	// The van stays cream/off-white at every tier; higher tiers read cleaner/brighter and gain extra
	// hardware (bumpers, rack, tank, ladder, light bar) rather than changing colour.
	private static readonly Color[] VanBody =
	{
		new( 0.78f, 0.66f, 0.48f ), // 0 dingy cream work van
		new( 0.84f, 0.74f, 0.54f ), // 1 clean cream
		new( 0.88f, 0.80f, 0.60f ), // 2 bright cream
		new( 0.92f, 0.84f, 0.66f ), // 3 pro cream
		new( 0.94f, 0.88f, 0.72f ), // 4 showroom cream
	};

	// The player's van is the Facepunch dev van from the asset cloud. Cloud.Model needs a
	// compile-time string literal (SB2000), so cache it behind a property.
	private static Model _vanCloudModel;
	private static Model VanCloudModel =>
		_vanCloudModel is { IsValid: true, IsError: false }
			? _vanCloudModel
			: _vanCloudModel = Cloud.Model( "facepunch.van_dev" );

	/// <summary>
	/// Builds the player's van into <paramref name="root"/> and returns the parts so the
	/// caller can tear it down on a rebuild. The van faces local +X. It uses the cloud van
	/// model; higher tiers read cleaner/bigger and gain roof hardware (rack, tank, light bar).
	/// Falls back to the stacked-box van if the cloud model is unavailable.
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

		// Higher tiers sit a touch bigger and read cleaner/brighter via tint.
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

		// Derive accessory placement from the (scaled) model bounds so hardware sits on the
		// real roof rather than guessed coordinates.
		var bb = cloud.Bounds;
		var len = bb.Size.x * vanScale;
		var wid = bb.Size.y * vanScale;
		var top = bb.Maxs.z * vanScale;
		var midX = bb.Center.x * vanScale;

		// Tier 2+: roof rack + water tank.
		if ( tier >= 2 )
		{
			var rackZ = top + 5f;
			Add( Box( root, "Rack", new Vector3( midX, 0, rackZ ), new Vector3( len * 0.66f, wid * 0.9f, 6f ), chrome, default, GameMaterials.Metal ) );
			Add( Box( root, "Tank", new Vector3( midX, 0, rackZ + 20f ), new Vector3( len * 0.42f, wid * 0.62f, 30f + tier * 4f ), tank, default, GameMaterials.Metal ) );
		}

		// Tier 3+: a side ladder.
		if ( tier >= 3 )
			Add( Box( root, "Ladder", new Vector3( midX - len * 0.12f, wid * 0.5f + 3f, top * 0.6f ), new Vector3( len * 0.4f, 4f, 10f ), chrome, default, GameMaterials.Metal ) );

		// Tier 4: roof light bar toward the cab.
		if ( tier >= 4 )
			Add( Box( root, "LightBar", new Vector3( midX + len * 0.28f, 0, top + 6f ), new Vector3( 12f, wid * 0.6f, 8f ), accent, default, GameMaterials.Metal ) );

		return parts;
	}

	/// <summary>Fallback stacked-box van used when the cloud van model is unavailable.</summary>
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

		// Wheels.
		var wx = len * 0.30f;
		var wy = wid * 0.5f - 2f;
		foreach ( var sx in new[] { -1f, 1f } )
		foreach ( var sy in new[] { -1f, 1f } )
			Add( Box( root, "Wheel", new Vector3( sx * wx, sy * wy, floor - 2f ), new Vector3( 46f, 18f, 46f ), tire, default, GameMaterials.Metal ) );

		// Cargo body + cab.
		Add( Box( root, "Cargo", new Vector3( -len * 0.06f, 0, bodyZ ), new Vector3( len * 0.7f, wid, hgt ), body, default, GameMaterials.Metal ) );
		Add( Box( root, "Cab", new Vector3( len * 0.34f, 0, floor + hgt * 0.4f ), new Vector3( len * 0.3f, wid, hgt * 0.8f ), body, default, GameMaterials.Metal ) );
		Add( Box( root, "Hood", new Vector3( len * 0.46f, 0, floor + hgt * 0.22f ), new Vector3( len * 0.12f, wid * 0.98f, hgt * 0.4f ), body, default, GameMaterials.Metal ) );

		// Glass.
		Add( Box( root, "Windshield", new Vector3( len * 0.42f, 0, floor + hgt * 0.62f ), new Vector3( 6f, wid * 0.86f, hgt * 0.3f ), glass, default, GameMaterials.Metal ) );
		foreach ( var sy in new[] { -1f, 1f } )
			Add( Box( root, "SideWin", new Vector3( len * 0.32f, sy * (wid * 0.5f - 1f), floor + hgt * 0.58f ), new Vector3( len * 0.16f, 4f, hgt * 0.24f ), glass, default, GameMaterials.Metal ) );

		// Headlights.
		foreach ( var sy in new[] { -1f, 1f } )
			Add( Box( root, "Light", new Vector3( len * 0.52f, sy * wid * 0.32f, floor + hgt * 0.18f ), new Vector3( 5f, 16f, 12f ), new Color( 1f, 0.95f, 0.7f ), default, GameMaterials.Metal ) );

		// Tier 1+: chrome bumpers and a coloured side stripe.
		if ( tier >= 1 )
		{
			Add( Box( root, "BumperF", new Vector3( len * 0.52f, 0, floor - 2f ), new Vector3( 10f, wid + 6f, 16f ), chrome, default, GameMaterials.Metal ) );
			Add( Box( root, "BumperR", new Vector3( -len * 0.41f, 0, floor - 2f ), new Vector3( 10f, wid + 6f, 16f ), chrome, default, GameMaterials.Metal ) );
			foreach ( var sy in new[] { -1f, 1f } )
				Add( Box( root, "Stripe", new Vector3( -len * 0.06f, sy * (wid * 0.5f + 1f), bodyZ - hgt * 0.12f ), new Vector3( len * 0.66f, 3f, 12f ), accent, default, GameMaterials.Metal ) );
		}

		// Tier 2+: roof rack + water tank.
		if ( tier >= 2 )
		{
			var rackZ = floor + hgt + 6f;
			Add( Box( root, "Rack", new Vector3( -len * 0.06f, 0, rackZ ), new Vector3( len * 0.68f, wid * 0.9f, 6f ), chrome, default, GameMaterials.Metal ) );
			Add( Box( root, "Tank", new Vector3( -len * 0.06f, 0, rackZ + 18f ), new Vector3( len * 0.44f, wid * 0.66f, 30f + tier * 4f ), tank, default, GameMaterials.Metal ) );
		}

		// Tier 3+: a side ladder.
		if ( tier >= 3 )
			Add( Box( root, "Ladder", new Vector3( -len * 0.2f, wid * 0.5f + 2f, bodyZ ), new Vector3( len * 0.4f, 4f, 10f ), chrome, default, GameMaterials.Metal ) );

		// Tier 4: roof light bar.
		if ( tier >= 4 )
			Add( Box( root, "LightBar", new Vector3( len * 0.34f, 0, floor + hgt * 0.86f ), new Vector3( 12f, wid * 0.6f, 8f ), accent, default, GameMaterials.Metal ) );

		// Collider on its own child so a rebuild tears it down with everything else.
		var colGo = new GameObject( root, true, "VanCollider" );
		colGo.LocalPosition = new Vector3( 0, 0, bodyZ );
		var col = colGo.Components.Create<BoxCollider>();
		col.Scale = new Vector3( len * 0.8f, wid, hgt + floor );
		col.Static = true;
		parts.Add( colGo );

		return parts;
	}

	private static void BuildCloud( GameObject root, DecorDef def )
	{
		var s = def.Size.x <= 0 ? 1f : def.Size.x;
		var white = new Color( 0.92f, 0.84f, 0.68f );
		Box( root, "Puff1", new Vector3( 0, 0, 0 ), new Vector3( 180 * s, 120 * s, 60 * s ), white );
		Box( root, "Puff2", new Vector3( 70 * s, 20 * s, 20 * s ), new Vector3( 120 * s, 90 * s, 50 * s ), white );
		Box( root, "Puff3", new Vector3( -80 * s, -10 * s, 10 * s ), new Vector3( 110 * s, 80 * s, 46 * s ), white );
	}
}
