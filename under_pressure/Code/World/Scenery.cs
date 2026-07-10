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
