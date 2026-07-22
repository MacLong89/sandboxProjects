namespace UnderPressure;

/// <summary>
/// Campaign site kit: the reusable set pieces the 25-level story is dressed with.
/// Everything is tinted box primitives, ground-contact correct, and follows the
/// ±1 depth-nudge rule so stacked faces never flicker.
/// </summary>
public static partial class Scenery
{
	private static readonly Color SteelDark = new( 0.30f, 0.32f, 0.36f );
	private static readonly Color SteelLight = new( 0.58f, 0.60f, 0.64f );
	private static readonly Color Concrete = new( 0.62f, 0.60f, 0.56f );
	private static readonly Color WarnYellow = new( 0.95f, 0.82f, 0.12f );
	private static readonly Color GlassDark = new( 0.10f, 0.12f, 0.16f );
	private static readonly Color Rust = new( 0.52f, 0.30f, 0.18f );

	private static float KitScale( DecorDef def ) =>
		Math.Clamp( def.Size.x <= 0.01f ? 1f : def.Size.x, 0.35f, 3f );

	private static Color TintOr( DecorDef def, Color fallback ) =>
		def.Color == Color.White ? fallback : def.Color;

	private static void AddBoxCollider( GameObject root, Vector3 center, Vector3 size )
	{
		var col = root.Components.Create<BoxCollider>();
		col.Center = center;
		col.Scale = size;
		col.Static = true;
	}

	// ── Industrial shells ────────────────────────────────────────────────

	/// <summary>Corrugated warehouse: ribbed walls, skylight parapet, roll-up doors on −Y.</summary>
	private static void BuildWarehouse( GameObject root, DecorDef def )
	{
		var w = def.Size.x;
		var d = def.Size.y;
		var h = def.Size.z;
		var body = TintOr( def, new Color( 0.55f, 0.57f, 0.60f ) );
		var front = -d * 0.5f;

		Box( root, "Base", new Vector3( 0, 0, 14 ), new Vector3( w + 8, d + 8, 28 ), Concrete, default, GameMaterials.Concrete );
		Box( root, "Walls", new Vector3( 0, 0, 28 + (h - 28) * 0.5f ), new Vector3( w, d, h - 28 ), body, default, GameMaterials.Metal );

		// Vertical ribs across the front face.
		var faceSlot = 0;
		float FaceY() => front - DepthLayers.NextFaceDepth( ref faceSlot );
		var ribY = FaceY();
		var ribs = Math.Max( 3, (int)(w / 110f) );
		for ( var i = 0; i < ribs; i++ )
		{
			var x = -w * 0.5f + w * (i + 0.5f) / ribs;
			Box( root, $"Rib_{i}", new Vector3( x, ribY, 28 + (h - 28) * 0.5f ), new Vector3( 10, 4, h - 40 ), body.Darken( 0.12f ), default, GameMaterials.Metal );
		}

		// Two roll-up doors + a man door.
		var doorY = FaceY();
		Box( root, "RollDoorL", new Vector3( -w * 0.24f, doorY, h * 0.34f ), new Vector3( w * 0.30f, 5, h * 0.6f ), SteelLight, default, GameMaterials.Metal );
		Box( root, "RollDoorR", new Vector3( w * 0.24f, doorY, h * 0.34f ), new Vector3( w * 0.30f, 5, h * 0.6f ), SteelLight, default, GameMaterials.Metal );
		// Slats sit 1 proud of the roll-door face (a FaceY slot would land their front
		// face exactly coplanar with the 5-thick door boxes and flicker).
		var slatY = doorY - 2f;
		for ( var s = 0; s < 4; s++ )
		{
			var z = h * 0.12f + s * h * 0.13f;
			Box( root, $"SlatL_{s}", new Vector3( -w * 0.24f, slatY, z ), new Vector3( w * 0.28f, 3, 4 ), SteelDark, default, GameMaterials.Metal );
			Box( root, $"SlatR_{s}", new Vector3( w * 0.24f, slatY, z ), new Vector3( w * 0.28f, 3, 4 ), SteelDark, default, GameMaterials.Metal );
		}
		Box( root, "ManDoor", new Vector3( 0, FaceY(), 52 ), new Vector3( 44, 5, 96 ), GlassDark, default, GameMaterials.Metal );

		// Parapet + roof units, +1 above the roof top.
		var roofZ = h + 7f;
		Box( root, "Roof", new Vector3( 0, 0, roofZ ), new Vector3( w + 14, d + 14, 14 ), body.Darken( 0.2f ), default, GameMaterials.Metal );
		var roofTop = roofZ + 7f;
		Box( root, "RoofHvac", new Vector3( -w * 0.2f, d * 0.12f, roofTop + 1f + 16f ), new Vector3( 76, 54, 32 ), SteelDark, default, GameMaterials.Metal );
		Box( root, "RoofVent", new Vector3( w * 0.26f, -d * 0.08f, roofTop + 1f + 10f ), new Vector3( 40, 40, 20 ), SteelLight, default, GameMaterials.Metal );

		AddBoxCollider( root, new Vector3( 0, 0, h * 0.5f ), new Vector3( w, d, h ) );
	}

	/// <summary>Raised colliding slab the player can stand on (dock platforms, stages).</summary>
	private static void BuildPlatform( GameObject root, DecorDef def )
	{
		var w = def.Size.x;
		var d = def.Size.y;
		var h = MathF.Max( def.Size.z, 8f );
		var color = TintOr( def, Concrete );

		Box( root, "Slab", new Vector3( 0, 0, h * 0.5f ), new Vector3( w, d, h ), color, default, GameMaterials.Concrete );
		// Warning edge stripe, +1 above the slab top face.
		Box( root, "Edge", new Vector3( 0, -d * 0.5f + 9f, h + 1f ), new Vector3( w, 18f, 2f ), WarnYellow, default, GameMaterials.Concrete );
		AddBoxCollider( root, new Vector3( 0, 0, h * 0.5f ), new Vector3( w, d, h ) );
	}

	private static void BuildContainer( GameObject root, DecorDef def, bool open )
	{
		var s = KitScale( def );
		var body = TintOr( def, new Color( 0.24f, 0.42f, 0.56f ) );
		var len = 240f * s;
		var wid = 96f * s;
		var hgt = 100f * s;

		if ( open )
		{
			// Open shell: floor, three walls, roof — doors swung out at the −Y mouth.
			Box( root, "Floor", new Vector3( 0, 0, 5 * s ), new Vector3( len, wid, 10 * s ), body.Darken( 0.35f ), default, GameMaterials.Metal );
			Box( root, "WallL", new Vector3( -len * 0.5f + 5 * s, 0, hgt * 0.5f ), new Vector3( 10 * s, wid, hgt ), body, default, GameMaterials.Metal );
			Box( root, "WallR", new Vector3( len * 0.5f - 5 * s, 0, hgt * 0.5f ), new Vector3( 10 * s, wid, hgt ), body, default, GameMaterials.Metal );
			Box( root, "WallB", new Vector3( 0, wid * 0.5f - 5 * s, hgt * 0.5f ), new Vector3( len, 10 * s, hgt ), body.Darken( 0.1f ), default, GameMaterials.Metal );
			Box( root, "RoofC", new Vector3( 0, 0, hgt - 4 * s ), new Vector3( len, wid, 8 * s ), body.Darken( 0.18f ), default, GameMaterials.Metal );
			Box( root, "DoorL", new Vector3( -len * 0.5f + 40f * s, -wid * 0.5f - 26f * s, hgt * 0.5f ), new Vector3( 8 * s, 60f * s, hgt ), body.Darken( 0.06f ), new Angles( 0, 35, 0 ), GameMaterials.Metal );
			Box( root, "DoorR", new Vector3( len * 0.5f - 40f * s, -wid * 0.5f - 26f * s, hgt * 0.5f ), new Vector3( 8 * s, 60f * s, hgt ), body.Darken( 0.06f ), new Angles( 0, -35, 0 ), GameMaterials.Metal );
			AddBoxCollider( root, new Vector3( 0, wid * 0.25f, hgt * 0.5f ), new Vector3( len, wid * 0.5f, hgt ) );
			return;
		}

		Box( root, "Body", new Vector3( 0, 0, hgt * 0.5f ), new Vector3( len, wid, hgt ), body, default, GameMaterials.Metal );
		// Corrugation ribs on the long faces, ±1 outside the body.
		var ribCount = Math.Max( 4, (int)(len / 46f) );
		for ( var i = 0; i < ribCount; i++ )
		{
			var x = -len * 0.5f + len * (i + 0.5f) / ribCount;
			Box( root, $"RibF_{i}", new Vector3( x, -wid * 0.5f - 1f, hgt * 0.5f ), new Vector3( 8 * s, 3, hgt - 14 * s ), body.Darken( 0.12f ), default, GameMaterials.Metal );
			Box( root, $"RibB_{i}", new Vector3( x, wid * 0.5f + 1f, hgt * 0.5f ), new Vector3( 8 * s, 3, hgt - 14 * s ), body.Darken( 0.12f ), default, GameMaterials.Metal );
		}
		// Door bars on the −X end.
		Box( root, "DoorFace", new Vector3( -len * 0.5f - 1f, 0, hgt * 0.5f ), new Vector3( 3, wid - 8 * s, hgt - 8 * s ), body.Darken( 0.2f ), default, GameMaterials.Metal );
		Box( root, "Lock1", new Vector3( -len * 0.5f - 2f, -wid * 0.16f, hgt * 0.5f ), new Vector3( 3, 6 * s, hgt - 20 * s ), SteelLight, default, GameMaterials.Metal );
		Box( root, "Lock2", new Vector3( -len * 0.5f - 2f, wid * 0.16f, hgt * 0.5f ), new Vector3( 3, 6 * s, hgt - 20 * s ), SteelLight, default, GameMaterials.Metal );

		AddBoxCollider( root, new Vector3( 0, 0, hgt * 0.5f ), new Vector3( len, wid, hgt ) );
	}

	// ── Vehicles ─────────────────────────────────────────────────────────

	private static void BuildLuxuryCar( GameObject root, DecorDef def )
	{
		var s = KitScale( def );
		var paint = TintOr( def, new Color( 0.08f, 0.09f, 0.11f ) );
		var glass = new Color( 0.16f, 0.20f, 0.26f );
		var tire = new Color( 0.08f, 0.08f, 0.10f );

		var len = 210f * s;
		var wid = 84f * s;

		foreach ( var sx in new[] { -0.32f, 0.32f } )
		foreach ( var sy in new[] { -1f, 1f } )
			Box( root, "Wheel", new Vector3( len * sx, sy * (wid * 0.5f - 4f * s), 15f * s ), new Vector3( 34f * s, 14f * s, 34f * s ), tire, default, GameMaterials.Metal );

		Box( root, "Body", new Vector3( 0, 0, 34f * s ), new Vector3( len, wid, 34f * s ), paint, default, GameMaterials.Metal );
		Box( root, "Cabin", new Vector3( -8f * s, 0, 60f * s ), new Vector3( len * 0.52f, wid * 0.86f, 24f * s ), paint.Darken( 0.06f ), default, GameMaterials.Metal );
		Box( root, "Windshield", new Vector3( len * 0.20f, 0, 58f * s ), new Vector3( 6f * s, wid * 0.78f, 20f * s ), glass, new Angles( 0, 0, -32 ), GameMaterials.Metal );
		Box( root, "RearGlass", new Vector3( -len * 0.32f, 0, 58f * s ), new Vector3( 6f * s, wid * 0.78f, 20f * s ), glass, new Angles( 0, 0, 30 ), GameMaterials.Metal );
		Box( root, "Grille", new Vector3( len * 0.5f + 1f, 0, 30f * s ), new Vector3( 4f * s, wid * 0.5f, 14f * s ), SteelLight, default, GameMaterials.Metal );
		foreach ( var sy in new[] { -1f, 1f } )
			Box( root, "Headlight", new Vector3( len * 0.5f + 1f, sy * wid * 0.33f, 36f * s ), new Vector3( 3f * s, 14f * s, 8f * s ), new Color( 0.95f, 0.95f, 0.85f ), default, GameMaterials.Metal );

		AddBoxCollider( root, new Vector3( 0, 0, 38f * s ), new Vector3( len, wid, 76f * s ) );
	}

	private static void BuildWreckedCar( GameObject root, DecorDef def )
	{
		var s = KitScale( def );
		var scorched = TintOr( def, new Color( 0.16f, 0.14f, 0.13f ) );
		var len = 200f * s;
		var wid = 82f * s;

		// Slumped on a flat rear tire, hood crumpled up at an angle.
		Box( root, "Body", new Vector3( 0, 0, 28f * s ), new Vector3( len, wid, 30f * s ), scorched, new Angles( 0, 0, 3 ), GameMaterials.Metal );
		Box( root, "Cabin", new Vector3( -12f * s, 0, 52f * s ), new Vector3( len * 0.48f, wid * 0.84f, 22f * s ), scorched.Lighten( 0.05f ), new Angles( 0, 0, 3 ), GameMaterials.Metal );
		Box( root, "HoodCrumple", new Vector3( len * 0.34f, 0, 46f * s ), new Vector3( len * 0.26f, wid * 0.9f, 12f * s ), Rust, new Angles( 0, 4, -18 ), GameMaterials.Metal );
		foreach ( var sx in new[] { -0.32f, 0.34f } )
		foreach ( var sy in new[] { -1f, 1f } )
			Box( root, "Wheel", new Vector3( len * sx, sy * (wid * 0.5f - 3f * s), 13f * s ), new Vector3( 32f * s, 13f * s, 32f * s ), new Color( 0.07f, 0.07f, 0.08f ), default, GameMaterials.Metal );
		// Shattered windshield lying ahead of the car.
		Box( root, "GlassSheet", new Vector3( len * 0.62f, wid * 0.1f, 2f ), new Vector3( 46f * s, 34f * s, 2f ), new Color( 0.55f, 0.66f, 0.72f ), new Angles( 0, 24, 0 ), GameMaterials.Metal );

		AddBoxCollider( root, new Vector3( 0, 0, 34f * s ), new Vector3( len, wid, 68f * s ) );
	}

	private static void BuildTrainCar( GameObject root, DecorDef def )
	{
		var s = KitScale( def );
		var body = TintOr( def, new Color( 0.46f, 0.24f, 0.18f ) );
		var len = 460f * s;
		var wid = 110f * s;
		var floorZ = 34f * s;
		var hgt = 120f * s;

		foreach ( var sx in new[] { -0.36f, 0.36f } )
		{
			Box( root, "Bogie", new Vector3( len * sx, 0, 16f * s ), new Vector3( 70f * s, wid * 0.7f, 22f * s ), SteelDark, default, GameMaterials.Metal );
			foreach ( var sy in new[] { -1f, 1f } )
			{
				Box( root, "WheelA", new Vector3( len * sx - 20f * s, sy * wid * 0.36f, 12f * s ), new Vector3( 24f * s, 8f * s, 24f * s ), GlassDark, default, GameMaterials.Metal );
				Box( root, "WheelB", new Vector3( len * sx + 20f * s, sy * wid * 0.36f, 12f * s ), new Vector3( 24f * s, 8f * s, 24f * s ), GlassDark, default, GameMaterials.Metal );
			}
		}

		Box( root, "Hull", new Vector3( 0, 0, floorZ + hgt * 0.5f ), new Vector3( len, wid, hgt ), body, default, GameMaterials.Metal );
		Box( root, "Roof", new Vector3( 0, 0, floorZ + hgt + 5f * s ), new Vector3( len - 12f * s, wid - 12f * s, 10f * s ), body.Darken( 0.18f ), default, GameMaterials.Metal );
		// Sliding door panel on each long face, ±1 out.
		foreach ( var sy in new[] { -1f, 1f } )
			Box( root, "SlideDoor", new Vector3( 0, sy * (wid * 0.5f + 1f), floorZ + hgt * 0.5f ), new Vector3( len * 0.24f, 3, hgt * 0.8f ), body.Darken( 0.1f ), default, GameMaterials.Metal );

		AddBoxCollider( root, new Vector3( 0, 0, floorZ + hgt * 0.5f ), new Vector3( len, wid, hgt + floorZ ) );
	}

	private static void BuildLocomotive( GameObject root, DecorDef def )
	{
		var s = KitScale( def );
		var body = TintOr( def, new Color( 0.16f, 0.28f, 0.40f ) );
		var len = 480f * s;
		var wid = 112f * s;
		var floorZ = 34f * s;

		foreach ( var sx in new[] { -0.34f, 0.02f, 0.34f } )
			Box( root, "Bogie", new Vector3( len * sx, 0, 16f * s ), new Vector3( 74f * s, wid * 0.72f, 24f * s ), SteelDark, default, GameMaterials.Metal );

		Box( root, "Hull", new Vector3( -len * 0.08f, 0, floorZ + 62f * s ), new Vector3( len * 0.78f, wid, 118f * s ), body, default, GameMaterials.Metal );
		Box( root, "Nose", new Vector3( len * 0.38f, 0, floorZ + 40f * s ), new Vector3( len * 0.20f, wid * 0.92f, 74f * s ), body.Darken( 0.08f ), default, GameMaterials.Metal );
		Box( root, "CabGlass", new Vector3( len * 0.27f, 0, floorZ + 106f * s ), new Vector3( 6f * s, wid * 0.7f, 26f * s ), GlassDark, new Angles( 0, 0, -18 ), GameMaterials.Metal );
		Box( root, "Stripe", new Vector3( -len * 0.08f, -wid * 0.5f - 1f, floorZ + 56f * s ), new Vector3( len * 0.78f, 3, 16f * s ), WarnYellow, default, GameMaterials.Metal );
		Box( root, "Stack", new Vector3( -len * 0.3f, 0, floorZ + 128f * s ), new Vector3( 26f * s, 26f * s, 18f * s ), SteelDark, default, GameMaterials.Metal );
		Box( root, "Headlamp", new Vector3( len * 0.48f + 1f, 0, floorZ + 58f * s ), new Vector3( 4f * s, 20f * s, 12f * s ), new Color( 1f, 0.95f, 0.7f ), default, GameMaterials.Metal );

		AddBoxCollider( root, new Vector3( 0, 0, floorZ + 60f * s ), new Vector3( len, wid, 150f * s ) );
	}

	/// <summary>Twin rails + sleepers laid flat, long axis X. Size.x = run length.</summary>
	private static void BuildRailTrack( GameObject root, DecorDef def )
	{
		var len = MathF.Max( def.Size.x, 200f );
		var gauge = 72f;

		var sleepers = Math.Max( 4, (int)(len / 70f) );
		for ( var i = 0; i < sleepers; i++ )
		{
			var x = -len * 0.5f + len * (i + 0.5f) / sleepers;
			Box( root, $"Sleeper_{i}", new Vector3( x, 0, 2f ), new Vector3( 26f, gauge + 44f, 4f ), new Color( 0.30f, 0.24f, 0.18f ), default, GameMaterials.Bark );
		}

		foreach ( var sy in new[] { -1f, 1f } )
			Box( root, "Rail", new Vector3( 0, sy * gauge * 0.5f, 5.5f ), new Vector3( len, 7f, 7f ), SteelLight, default, GameMaterials.Metal );
	}

	/// <summary>Elevated walkway: legs, deck, kick rails. Size = (length, width, deck height).</summary>
	private static void BuildCatwalk( GameObject root, DecorDef def )
	{
		var len = MathF.Max( def.Size.x, 220f );
		var wid = def.Size.y > 10f ? def.Size.y : 70f;
		var deckZ = def.Size.z > 10f ? def.Size.z : 150f;
		var steel = TintOr( def, new Color( 0.40f, 0.42f, 0.30f ) );

		var legs = Math.Max( 2, (int)(len / 180f) + 1 );
		for ( var i = 0; i < legs; i++ )
		{
			var x = -len * 0.5f + len * i / (legs - 1);
			foreach ( var sy in new[] { -1f, 1f } )
				Box( root, $"Leg_{i}", new Vector3( x, sy * (wid * 0.5f - 5f), deckZ * 0.5f ), new Vector3( 10f, 10f, deckZ ), steel.Darken( 0.15f ), default, GameMaterials.Metal );
			// Cross brace.
			Box( root, $"Brace_{i}", new Vector3( x, 0, deckZ * 0.4f ), new Vector3( 8f, wid - 10f, 8f ), steel.Darken( 0.2f ), default, GameMaterials.Metal );
		}

		Box( root, "Deck", new Vector3( 0, 0, deckZ + 4f ), new Vector3( len, wid, 8f ), steel, default, GameMaterials.Metal );
		foreach ( var sy in new[] { -1f, 1f } )
		{
			Box( root, "Kick", new Vector3( 0, sy * (wid * 0.5f + 1f), deckZ + 14f ), new Vector3( len, 3f, 12f ), steel.Darken( 0.1f ), default, GameMaterials.Metal );
			Box( root, "TopRail", new Vector3( 0, sy * (wid * 0.5f + 1f), deckZ + 46f ), new Vector3( len, 4f, 6f ), WarnYellow, default, GameMaterials.Metal );
		}
	}

	// ── Clutter / equipment ──────────────────────────────────────────────

	private static void BuildBarrelCluster( GameObject root, DecorDef def )
	{
		var s = KitScale( def );
		var paint = TintOr( def, new Color( 0.20f, 0.45f, 0.72f ) );
		var offsets = new (float x, float y, float lean)[]
		{
			(0f, 0f, 0f), (44f, 14f, 0f), (18f, -40f, 0f), (-34f, 26f, 8f),
		};

		var i = 0;
		foreach ( var (x, y, lean) in offsets )
		{
			var h = 62f * s;
			var barrel = new GameObject( root, true, $"Barrel_{i}" );
			barrel.LocalPosition = new Vector3( x * s, y * s, 0 );
			Box( barrel, "Drum", new Vector3( 0, 0, h * 0.5f ), new Vector3( 42f * s, 42f * s, h ), i % 2 == 0 ? paint : paint.Darken( 0.18f ), new Angles( 0, i * 30f, lean ), GameMaterials.Metal );
			Box( barrel, "Lid", new Vector3( 0, 0, h + 1f ), new Vector3( 38f * s, 38f * s, 3f ), SteelLight, new Angles( 0, i * 30f, lean ), GameMaterials.Metal );
			Box( barrel, "Band", new Vector3( 0, 0, h * 0.55f ), new Vector3( 44f * s, 44f * s, 5f ), SteelDark, new Angles( 0, i * 30f, lean ), GameMaterials.Metal );
			i++;
		}

		AddBoxCollider( root, new Vector3( 4f * s, 0, 32f * s ), new Vector3( 120f * s, 110f * s, 64f * s ) );
	}

	private static void BuildServerRack( GameObject root, DecorDef def )
	{
		var s = KitScale( def );
		var led = TintOr( def, new Color( 0.20f, 0.90f, 0.55f ) );
		var h = 170f * s;

		Box( root, "Cabinet", new Vector3( 0, 0, h * 0.5f ), new Vector3( 60f * s, 80f * s, h ), new Color( 0.12f, 0.13f, 0.16f ), default, GameMaterials.Metal );
		// Blade slots + LEDs on the −Y face.
		for ( var i = 0; i < 6; i++ )
		{
			var z = 18f * s + i * (h - 30f * s) / 6f;
			Box( root, $"Blade_{i}", new Vector3( 0, -41f * s, z ), new Vector3( 50f * s, 2f, 10f * s ), new Color( 0.20f, 0.22f, 0.26f ), default, GameMaterials.Metal );
			Box( root, $"Led_{i}", new Vector3( 18f * s, -42f * s, z ), new Vector3( 8f * s, 2f, 4f * s ), i % 3 == 0 ? new Color( 0.95f, 0.35f, 0.25f ) : led, default, GameMaterials.Metal );
		}

		AddBoxCollider( root, new Vector3( 0, 0, h * 0.5f ), new Vector3( 60f * s, 80f * s, h ) );
	}

	private static void BuildCrateStack( GameObject root, DecorDef def )
	{
		var s = KitScale( def );
		var wood = TintOr( def, new Color( 0.60f, 0.45f, 0.26f ) );

		Box( root, "CrateA", new Vector3( 0, 0, 26f * s ), new Vector3( 52f * s, 52f * s, 52f * s ), wood, default, GameMaterials.Wood );
		Box( root, "CrateB", new Vector3( 56f * s, 10f * s, 24f * s ), new Vector3( 48f * s, 48f * s, 48f * s ), wood.Darken( 0.1f ), new Angles( 0, 18, 0 ), GameMaterials.Wood );
		Box( root, "CrateTop", new Vector3( 20f * s, 2f * s, 53f * s + 24f * s ), new Vector3( 46f * s, 46f * s, 46f * s ), wood.Lighten( 0.08f ), new Angles( 0, -12, 0 ), GameMaterials.Wood );

		AddBoxCollider( root, new Vector3( 22f * s, 0, 50f * s ), new Vector3( 120f * s, 90f * s, 100f * s ) );
	}

	private static void BuildLampPost( GameObject root, DecorDef def )
	{
		var s = KitScale( def );
		Box( root, "Pole", new Vector3( 0, 0, 130f * s ), new Vector3( 10f * s, 10f * s, 260f * s ), SteelDark, default, GameMaterials.Metal );
		Box( root, "Arm", new Vector3( 0, -28f * s, 252f * s ), new Vector3( 8f * s, 56f * s, 8f * s ), SteelDark, default, GameMaterials.Metal );
		Box( root, "Head", new Vector3( 0, -52f * s, 246f * s ), new Vector3( 20f * s, 34f * s, 10f * s ), new Color( 1f, 0.92f, 0.62f ), default, GameMaterials.Metal );
	}

	private static void BuildFloodlight( GameObject root, DecorDef def )
	{
		var s = KitScale( def );
		Box( root, "LegA", new Vector3( -16f * s, 12f * s, 50f * s ), new Vector3( 6f * s, 6f * s, 104f * s ), SteelDark, new Angles( 8, 20, 8 ), GameMaterials.Metal );
		Box( root, "LegB", new Vector3( 16f * s, 12f * s, 50f * s ), new Vector3( 6f * s, 6f * s, 104f * s ), SteelDark, new Angles( 8, -20, -8 ), GameMaterials.Metal );
		Box( root, "LegC", new Vector3( 0, -18f * s, 50f * s ), new Vector3( 6f * s, 6f * s, 104f * s ), SteelDark, new Angles( -10, 0, 0 ), GameMaterials.Metal );
		Box( root, "Lamp", new Vector3( 0, -12f * s, 108f * s ), new Vector3( 40f * s, 14f * s, 30f * s ), WarnYellow, new Angles( -24, 0, 0 ), GameMaterials.Metal );
		Box( root, "Lens", new Vector3( 0, -20f * s, 106f * s ), new Vector3( 34f * s, 3f, 24f * s ), new Color( 1f, 0.97f, 0.82f ), new Angles( -24, 0, 0 ), GameMaterials.Metal );
	}

	// ── Architectural / museum ───────────────────────────────────────────

	private static void BuildColumn( GameObject root, DecorDef def )
	{
		var dia = def.Size.x > 5f ? def.Size.x : 56f;
		var h = def.Size.z > 5f ? def.Size.z : 300f;
		var stone = TintOr( def, new Color( 0.88f, 0.86f, 0.80f ) );

		Box( root, "Plinth", new Vector3( 0, 0, 12f ), new Vector3( dia + 26f, dia + 26f, 24f ), stone.Darken( 0.08f ), default, GameMaterials.Concrete );
		Box( root, "Shaft", new Vector3( 0, 0, 24f + (h - 48f) * 0.5f ), new Vector3( dia, dia, h - 48f ), stone, default, GameMaterials.Concrete );
		Box( root, "Cap", new Vector3( 0, 0, h - 12f ), new Vector3( dia + 22f, dia + 22f, 20f ), stone.Darken( 0.06f ), default, GameMaterials.Concrete );

		AddBoxCollider( root, new Vector3( 0, 0, h * 0.5f ), new Vector3( dia + 8f, dia + 8f, h ) );
	}

	private static void BuildStatue( GameObject root, DecorDef def )
	{
		var s = KitScale( def );
		var stone = TintOr( def, new Color( 0.90f, 0.89f, 0.84f ) );

		Box( root, "Pedestal", new Vector3( 0, 0, 30f * s ), new Vector3( 80f * s, 80f * s, 60f * s ), stone.Darken( 0.12f ), default, GameMaterials.Concrete );
		Box( root, "Legs", new Vector3( 0, 0, 60f * s + 34f * s ), new Vector3( 30f * s, 24f * s, 66f * s ), stone, new Angles( 0, 14, 0 ), GameMaterials.Concrete );
		Box( root, "Torso", new Vector3( 2f * s, 0, 60f * s + 92f * s ), new Vector3( 38f * s, 28f * s, 52f * s ), stone, new Angles( 0, 8, 4 ), GameMaterials.Concrete );
		Box( root, "ArmUp", new Vector3( 10f * s, -14f * s, 60f * s + 122f * s ), new Vector3( 12f * s, 12f * s, 46f * s ), stone, new Angles( 18, 0, -24 ), GameMaterials.Concrete );
		Box( root, "Head", new Vector3( 4f * s, 0, 60f * s + 132f * s ), new Vector3( 20f * s, 18f * s, 22f * s ), stone.Lighten( 0.04f ), default, GameMaterials.Concrete );

		AddBoxCollider( root, new Vector3( 0, 0, 70f * s ), new Vector3( 84f * s, 84f * s, 140f * s ) );
	}

	private static void BuildDisplayCase( GameObject root, DecorDef def )
	{
		var s = KitScale( def );
		Box( root, "Base", new Vector3( 0, 0, 40f * s ), new Vector3( 66f * s, 66f * s, 80f * s ), new Color( 0.24f, 0.20f, 0.16f ), default, GameMaterials.Wood );
		Box( root, "Glass", new Vector3( 0, 0, 80f * s + 26f * s ), new Vector3( 56f * s, 56f * s, 52f * s ), new Color( 0.62f, 0.78f, 0.86f, 0.5f ), default, GameMaterials.Metal );
		Box( root, "Relic", new Vector3( 0, 0, 80f * s + 16f * s ), new Vector3( 20f * s, 20f * s, 24f * s ), new Color( 0.86f, 0.72f, 0.28f ), new Angles( 0, 30, 0 ), GameMaterials.Metal );
		AddBoxCollider( root, new Vector3( 0, 0, 66f * s ), new Vector3( 66f * s, 66f * s, 132f * s ) );
	}

	// ── Plant / industrial interior ──────────────────────────────────────

	private static void BuildHookRail( GameObject root, DecorDef def )
	{
		var len = MathF.Max( def.Size.x, 200f );
		var railZ = 250f;

		foreach ( var sx in new[] { -1f, 1f } )
			Box( root, "Post", new Vector3( sx * len * 0.5f, 0, railZ * 0.5f ), new Vector3( 14f, 14f, railZ ), SteelDark, default, GameMaterials.Metal );
		Box( root, "Rail", new Vector3( 0, 0, railZ ), new Vector3( len, 10f, 10f ), SteelLight, default, GameMaterials.Metal );

		var hooks = Math.Max( 3, (int)(len / 90f) );
		for ( var i = 0; i < hooks; i++ )
		{
			var x = -len * 0.5f + len * (i + 0.5f) / hooks;
			Box( root, $"Chain_{i}", new Vector3( x, 0, railZ - 26f ), new Vector3( 4f, 4f, 52f ), SteelLight.Darken( 0.2f ), default, GameMaterials.Metal );
			Box( root, $"Hook_{i}", new Vector3( x, 0, railZ - 56f ), new Vector3( 14f, 5f, 16f ), SteelLight, new Angles( 0, 0, 30 ), GameMaterials.Metal );
			if ( i % 2 == 0 )
				Box( root, $"Carcass_{i}", new Vector3( x, 0, railZ - 96f ), new Vector3( 26f, 20f, 70f ), new Color( 0.72f, 0.42f, 0.38f ), new Angles( 0, i * 25f, 4 ), GameMaterials.Concrete );
		}
	}

	private static void BuildSteelTable( GameObject root, DecorDef def )
	{
		var s = KitScale( def );
		foreach ( var sx in new[] { -1f, 1f } )
		foreach ( var sy in new[] { -1f, 1f } )
			Box( root, "Leg", new Vector3( sx * 56f * s, sy * 28f * s, 40f * s ), new Vector3( 8f * s, 8f * s, 80f * s ), SteelDark, default, GameMaterials.Metal );
		Box( root, "Top", new Vector3( 0, 0, 82f * s ), new Vector3( 130f * s, 70f * s, 6f * s ), SteelLight, default, GameMaterials.Metal );
		AddBoxCollider( root, new Vector3( 0, 0, 44f * s ), new Vector3( 130f * s, 70f * s, 88f * s ) );
	}

	private static void BuildAntenna( GameObject root, DecorDef def )
	{
		var baseW = def.Size.x > 5f ? def.Size.x : 90f;
		var h = def.Size.z > 5f ? def.Size.z : 520f;
		var steel = TintOr( def, new Color( 0.78f, 0.22f, 0.18f ) );

		Box( root, "Foot", new Vector3( 0, 0, 10f ), new Vector3( baseW + 30f, baseW + 30f, 20f ), Concrete, default, GameMaterials.Concrete );
		// Tapering lattice: stacked frames.
		var tiers = 5;
		for ( var i = 0; i < tiers; i++ )
		{
			var t = i / (float)tiers;
			var w = MathX.Lerp( baseW, baseW * 0.24f, t );
			var z0 = 20f + h * t;
			var segH = h / tiers;
			foreach ( var sx in new[] { -1f, 1f } )
			foreach ( var sy in new[] { -1f, 1f } )
				Box( root, $"Leg_{i}", new Vector3( sx * w * 0.5f, sy * w * 0.5f, z0 + segH * 0.5f ), new Vector3( 8f, 8f, segH ), i % 2 == 0 ? steel : Color.White, new Angles( 0, 0, 0 ), GameMaterials.Metal );
			Box( root, $"Frame_{i}", new Vector3( 0, 0, z0 + segH ), new Vector3( w, w, 6f ), SteelDark, default, GameMaterials.Metal );
		}
		Box( root, "Mast", new Vector3( 0, 0, 20f + h + 40f ), new Vector3( 10f, 10f, 80f ), Color.White, default, GameMaterials.Metal );
		Box( root, "Beacon", new Vector3( 0, 0, 20f + h + 84f ), new Vector3( 14f, 14f, 14f ), new Color( 1f, 0.2f, 0.15f ), default, GameMaterials.Metal );

		AddBoxCollider( root, new Vector3( 0, 0, h * 0.5f ), new Vector3( baseW, baseW, h ) );
	}

	private static void BuildSatelliteDish( GameObject root, DecorDef def )
	{
		var s = KitScale( def );
		Box( root, "Plinth", new Vector3( 0, 0, 16f * s ), new Vector3( 60f * s, 60f * s, 32f * s ), Concrete, default, GameMaterials.Concrete );
		Box( root, "Neck", new Vector3( 0, 0, 52f * s ), new Vector3( 14f * s, 14f * s, 44f * s ), SteelDark, default, GameMaterials.Metal );
		// Dish face angled up toward −Y.
		Box( root, "Dish", new Vector3( 0, -14f * s, 92f * s ), new Vector3( 96f * s, 10f * s, 96f * s ), Color.White, new Angles( -38, 0, 0 ), GameMaterials.Metal );
		Box( root, "DishRim", new Vector3( 0, -18f * s, 92f * s ), new Vector3( 102f * s, 4f * s, 102f * s ), SteelLight, new Angles( -38, 0, 0 ), GameMaterials.Metal );
		Box( root, "Feed", new Vector3( 0, -52f * s, 106f * s ), new Vector3( 8f * s, 44f * s, 8f * s ), SteelDark, new Angles( -38, 0, 0 ), GameMaterials.Metal );
		AddBoxCollider( root, new Vector3( 0, 0, 60f * s ), new Vector3( 90f * s, 70f * s, 120f * s ) );
	}

	private static void BuildGenerator( GameObject root, DecorDef def )
	{
		var s = KitScale( def );
		var housing = TintOr( def, new Color( 0.80f, 0.66f, 0.16f ) );
		Box( root, "Skid", new Vector3( 0, 0, 6f * s ), new Vector3( 150f * s, 80f * s, 12f * s ), SteelDark, default, GameMaterials.Metal );
		Box( root, "Body", new Vector3( 0, 0, 12f * s + 38f * s ), new Vector3( 140f * s, 70f * s, 76f * s ), housing, default, GameMaterials.Metal );
		Box( root, "Vents", new Vector3( 0, -36f * s - 1f, 12f * s + 40f * s ), new Vector3( 100f * s, 3f, 40f * s ), SteelDark, default, GameMaterials.Metal );
		Box( root, "Exhaust", new Vector3( -50f * s, 0, 12f * s + 76f * s + 18f * s ), new Vector3( 12f * s, 12f * s, 36f * s ), SteelDark, default, GameMaterials.Metal );
		Box( root, "Panel", new Vector3( 52f * s, -36f * s - 1f, 12f * s + 44f * s ), new Vector3( 26f * s, 3f, 30f * s ), new Color( 0.16f, 0.5f, 0.28f ), default, GameMaterials.Metal );
		AddBoxCollider( root, new Vector3( 0, 0, 46f * s ), new Vector3( 150f * s, 80f * s, 92f * s ) );
	}

	private static void BuildPipeRun( GameObject root, DecorDef def )
	{
		var len = MathF.Max( def.Size.x, 200f );
		var pipeZ = def.Size.z > 5f ? def.Size.z : 90f;
		var pipe = TintOr( def, new Color( 0.55f, 0.58f, 0.62f ) );

		var stands = Math.Max( 2, (int)(len / 200f) + 1 );
		for ( var i = 0; i < stands; i++ )
		{
			var x = -len * 0.5f + len * i / (stands - 1);
			Box( root, $"Stand_{i}", new Vector3( x, 0, pipeZ * 0.5f ), new Vector3( 10f, 10f, pipeZ ), SteelDark, default, GameMaterials.Metal );
		}

		Box( root, "PipeA", new Vector3( 0, -12f, pipeZ + 10f ), new Vector3( len, 18f, 18f ), pipe, default, GameMaterials.Metal );
		Box( root, "PipeB", new Vector3( 0, 12f, pipeZ + 4f ), new Vector3( len, 13f, 13f ), pipe.Darken( 0.18f ), default, GameMaterials.Metal );
		// Flange rings, +1 proud of pipe A.
		var rings = Math.Max( 2, (int)(len / 240f) );
		for ( var i = 0; i < rings; i++ )
		{
			var x = -len * 0.5f + len * (i + 0.5f) / rings;
			Box( root, $"Flange_{i}", new Vector3( x, -12f, pipeZ + 10f ), new Vector3( 8f, 20f, 20f ), SteelLight, default, GameMaterials.Metal );
		}
	}

	private static void BuildValveStation( GameObject root, DecorDef def )
	{
		var s = KitScale( def );
		var wheel = TintOr( def, new Color( 0.82f, 0.18f, 0.14f ) );
		Box( root, "Stub", new Vector3( 0, 0, 30f * s ), new Vector3( 22f * s, 22f * s, 60f * s ), SteelLight.Darken( 0.1f ), default, GameMaterials.Metal );
		Box( root, "WheelH", new Vector3( 0, 0, 66f * s ), new Vector3( 52f * s, 10f * s, 10f * s ), wheel, default, GameMaterials.Metal );
		Box( root, "WheelV", new Vector3( 0, 0, 66f * s ), new Vector3( 10f * s, 52f * s, 10f * s ), wheel, default, GameMaterials.Metal );
		Box( root, "Hub", new Vector3( 0, 0, 66f * s ), new Vector3( 14f * s, 14f * s, 14f * s ), SteelDark, default, GameMaterials.Metal );
	}

	// ── Road / rail dressing ─────────────────────────────────────────────

	private static void BuildGuardRail( GameObject root, DecorDef def )
	{
		var len = MathF.Max( def.Size.x, 160f );
		var posts = Math.Max( 2, (int)(len / 130f) + 1 );
		for ( var i = 0; i < posts; i++ )
		{
			var x = -len * 0.5f + len * i / (posts - 1);
			Box( root, $"Post_{i}", new Vector3( x, 0, 22f ), new Vector3( 10f, 10f, 44f ), SteelDark, default, GameMaterials.Metal );
		}
		Box( root, "Beam", new Vector3( 0, -1f, 40f ), new Vector3( len, 6f, 22f ), SteelLight, default, GameMaterials.Metal );
	}

	private static void BuildOverpass( GameObject root, DecorDef def )
	{
		var span = MathF.Max( def.Size.x, 900f );
		var deckW = def.Size.y > 10f ? def.Size.y : 420f;
		var clearance = def.Size.z > 10f ? def.Size.z : 330f;
		var deckT = 44f;

		foreach ( var sx in new[] { -0.30f, 0.30f } )
		{
			Box( root, "Pier", new Vector3( span * sx, 0, clearance * 0.5f ), new Vector3( 70f, deckW * 0.5f, clearance ), Concrete, default, GameMaterials.Concrete );
			Box( root, "PierCap", new Vector3( span * sx, 0, clearance - 12f ), new Vector3( 100f, deckW * 0.62f, 26f ), Concrete.Darken( 0.08f ), default, GameMaterials.Concrete );
		}

		Box( root, "Deck", new Vector3( 0, 0, clearance + deckT * 0.5f + 1f ), new Vector3( span, deckW, deckT ), new Color( 0.40f, 0.40f, 0.42f ), default, GameMaterials.Concrete );
		foreach ( var sy in new[] { -1f, 1f } )
			Box( root, "Parapet", new Vector3( 0, sy * (deckW * 0.5f + 1f), clearance + deckT + 20f ), new Vector3( span, 14f, 40f ), Concrete, default, GameMaterials.Concrete );

		foreach ( var sx in new[] { -0.30f, 0.30f } )
			AddBoxCollider( root, new Vector3( span * sx, 0, clearance * 0.5f ), new Vector3( 70f, deckW * 0.5f, clearance ) );
	}

	private static void BuildConcreteBarrier( GameObject root, DecorDef def )
	{
		var len = MathF.Max( def.Size.x, 120f );
		var color = TintOr( def, Concrete );
		Box( root, "Base", new Vector3( 0, 0, 12f ), new Vector3( len, 34f, 24f ), color, default, GameMaterials.Concrete );
		Box( root, "Body", new Vector3( 0, 0, 36f ), new Vector3( len, 20f, 26f ), color.Lighten( 0.04f ), default, GameMaterials.Concrete );
		AddBoxCollider( root, new Vector3( 0, 0, 25f ), new Vector3( len, 34f, 50f ) );
	}

	private static void BuildRubble( GameObject root, DecorDef def )
	{
		var s = KitScale( def );
		var debris = TintOr( def, new Color( 0.56f, 0.52f, 0.48f ) );
		Box( root, "SlabA", new Vector3( 0, 0, 12f * s ), new Vector3( 90f * s, 70f * s, 24f * s ), debris, new Angles( 6, 24, 8 ), GameMaterials.Concrete );
		Box( root, "SlabB", new Vector3( 34f * s, 22f * s, 30f * s ), new Vector3( 64f * s, 48f * s, 20f * s ), debris.Darken( 0.1f ), new Angles( -10, -18, 14 ), GameMaterials.Concrete );
		Box( root, "SlabC", new Vector3( -30f * s, -16f * s, 34f * s ), new Vector3( 48f * s, 40f * s, 16f * s ), debris.Lighten( 0.06f ), new Angles( 14, 40, -8 ), GameMaterials.Concrete );
		Box( root, "Rebar", new Vector3( 12f * s, 8f * s, 44f * s ), new Vector3( 60f * s, 3f, 3f ), Rust, new Angles( 0, 30, 24 ), GameMaterials.Metal );
	}

	// ── Interior walls / rooms ───────────────────────────────────────────

	/// <summary>Colliding interior wall with a tile band — the workhorse for enclosed sites.</summary>
	private static void BuildTiledWall( GameObject root, DecorDef def )
	{
		var w = def.Size.x;
		var t = def.Size.y > 4f ? def.Size.y : 24f;
		var h = def.Size.z > 10f ? def.Size.z : 260f;
		var tile = TintOr( def, new Color( 0.88f, 0.88f, 0.84f ) );

		Box( root, "Wall", new Vector3( 0, 0, h * 0.5f ), new Vector3( w, t, h ), tile, default, GameMaterials.Concrete );
		// Skirting + band: 2 thick, back flush with the −Y face so they protrude exactly
		// 0..2 in front (3-thick versions reached 2.5 out, into cleanable panel stacks).
		Box( root, "Skirt", new Vector3( 0, -t * 0.5f - 1f, 14f ), new Vector3( w, 2f, 28f ), tile.Darken( 0.25f ), default, GameMaterials.Concrete );
		Box( root, "Band", new Vector3( 0, -t * 0.5f - 1f, h * 0.62f ), new Vector3( w, 2f, 22f ), tile.Darken( 0.16f ), default, GameMaterials.Concrete );

		AddBoxCollider( root, new Vector3( 0, 0, h * 0.5f ), new Vector3( w, t, h ) );
	}

	private static void BuildYachtCabin( GameObject root, DecorDef def )
	{
		var w = def.Size.x;
		var d = def.Size.y;
		var h = def.Size.z;
		var hull = TintOr( def, new Color( 0.96f, 0.96f, 0.94f ) );
		var front = -d * 0.5f;

		Box( root, "DeckHouse", new Vector3( 0, 0, h * 0.5f ), new Vector3( w, d, h ), hull, default, GameMaterials.Concrete );
		// Continuous tinted window band, +1 out on the −Y face.
		Box( root, "WindowBand", new Vector3( 0, front - 1f, h * 0.62f ), new Vector3( w * 0.86f, 3f, h * 0.3f ), GlassDark, default, GameMaterials.Metal );
		Box( root, "UpperDeck", new Vector3( 0, d * 0.08f, h + 12f ), new Vector3( w * 0.66f, d * 0.7f, 24f ), hull.Darken( 0.04f ), default, GameMaterials.Concrete );
		Box( root, "Bridge", new Vector3( 0, d * 0.1f, h + 24f + 26f ), new Vector3( w * 0.4f, d * 0.44f, 52f ), hull, default, GameMaterials.Concrete );
		Box( root, "BridgeGlass", new Vector3( 0, d * 0.1f - d * 0.22f - 1f, h + 24f + 30f ), new Vector3( w * 0.34f, 3f, 26f ), GlassDark, default, GameMaterials.Metal );
		// Radar arch + stack.
		Box( root, "ArchL", new Vector3( -w * 0.18f, d * 0.3f, h + 24f + 52f + 20f ), new Vector3( 10f, 10f, 60f ), hull, new Angles( 0, 0, 14 ), GameMaterials.Metal );
		Box( root, "ArchR", new Vector3( w * 0.18f, d * 0.3f, h + 24f + 52f + 20f ), new Vector3( 10f, 10f, 60f ), hull, new Angles( 0, 0, -14 ), GameMaterials.Metal );
		Box( root, "ArchTop", new Vector3( 0, d * 0.3f, h + 24f + 52f + 52f ), new Vector3( w * 0.44f, 12f, 10f ), hull, default, GameMaterials.Metal );
		Box( root, "Radar", new Vector3( 0, d * 0.3f, h + 24f + 52f + 62f ), new Vector3( 40f, 12f, 8f ), SteelLight, default, GameMaterials.Metal );

		AddBoxCollider( root, new Vector3( 0, 0, h * 0.5f ), new Vector3( w, d, h ) );
	}

	private static void BuildRailing( GameObject root, DecorDef def )
	{
		var len = MathF.Max( def.Size.x, 120f );
		var color = TintOr( def, new Color( 0.85f, 0.86f, 0.88f ) );
		var posts = Math.Max( 2, (int)(len / 90f) + 1 );
		for ( var i = 0; i < posts; i++ )
		{
			var x = -len * 0.5f + len * i / (posts - 1);
			Box( root, $"Post_{i}", new Vector3( x, 0, 22f ), new Vector3( 5f, 5f, 44f ), color, default, GameMaterials.Metal );
		}
		Box( root, "Top", new Vector3( 0, 0, 46f ), new Vector3( len, 7f, 7f ), color, default, GameMaterials.Metal );
		Box( root, "Mid", new Vector3( 0, 0, 26f ), new Vector3( len, 4f, 4f ), color.Darken( 0.1f ), default, GameMaterials.Metal );
	}

	private static void BuildLabBench( GameObject root, DecorDef def )
	{
		var s = KitScale( def );
		Box( root, "Cabinet", new Vector3( 0, 0, 42f * s ), new Vector3( 170f * s, 66f * s, 84f * s ), Color.White.Darken( 0.03f ), default, GameMaterials.Concrete );
		Box( root, "Top", new Vector3( 0, 0, 86f * s ), new Vector3( 178f * s, 72f * s, 6f * s ), GlassDark, default, GameMaterials.Metal );
		Box( root, "Scope", new Vector3( -46f * s, 0, 89f * s + 20f * s ), new Vector3( 16f * s, 16f * s, 40f * s ), SteelDark, new Angles( 0, 0, 8 ), GameMaterials.Metal );
		Box( root, "FlaskA", new Vector3( 20f * s, 12f * s, 89f * s + 10f * s ), new Vector3( 14f * s, 14f * s, 20f * s ), new Color( 0.25f, 0.85f, 0.95f ), default, GameMaterials.Metal );
		Box( root, "FlaskB", new Vector3( 48f * s, -8f * s, 89f * s + 8f * s ), new Vector3( 12f * s, 12f * s, 16f * s ), new Color( 0.30f, 0.95f, 0.55f ), default, GameMaterials.Metal );
		AddBoxCollider( root, new Vector3( 0, 0, 45f * s ), new Vector3( 178f * s, 72f * s, 90f * s ) );
	}

	private static void BuildStudioCamera( GameObject root, DecorDef def )
	{
		var s = KitScale( def );
		Box( root, "LegA", new Vector3( -14f * s, 10f * s, 46f * s ), new Vector3( 6f * s, 6f * s, 96f * s ), SteelDark, new Angles( 6, 16, 6 ), GameMaterials.Metal );
		Box( root, "LegB", new Vector3( 14f * s, 10f * s, 46f * s ), new Vector3( 6f * s, 6f * s, 96f * s ), SteelDark, new Angles( 6, -16, -6 ), GameMaterials.Metal );
		Box( root, "LegC", new Vector3( 0, -16f * s, 46f * s ), new Vector3( 6f * s, 6f * s, 96f * s ), SteelDark, new Angles( -8, 0, 0 ), GameMaterials.Metal );
		Box( root, "Body", new Vector3( 0, 0, 102f * s ), new Vector3( 34f * s, 52f * s, 30f * s ), GlassDark, default, GameMaterials.Metal );
		Box( root, "Lens", new Vector3( 0, -34f * s, 102f * s ), new Vector3( 16f * s, 20f * s, 16f * s ), SteelDark, default, GameMaterials.Metal );
		Box( root, "Tally", new Vector3( 0, -26f * s, 120f * s ), new Vector3( 8f * s, 4f * s, 4f * s ), new Color( 1f, 0.2f, 0.15f ), default, GameMaterials.Metal );
	}

	private static void BuildControlDesk( GameObject root, DecorDef def )
	{
		var s = KitScale( def );
		Box( root, "Desk", new Vector3( 0, 0, 40f * s ), new Vector3( 220f * s, 70f * s, 80f * s ), new Color( 0.20f, 0.21f, 0.24f ), default, GameMaterials.Metal );
		Box( root, "Top", new Vector3( 0, -8f * s, 82f * s ), new Vector3( 226f * s, 84f * s, 5f * s ), GlassDark, new Angles( -6, 0, 0 ), GameMaterials.Metal );
		for ( var i = 0; i < 3; i++ )
		{
			var x = (i - 1) * 70f * s;
			Box( root, $"Screen_{i}", new Vector3( x, 12f * s, 84f * s + 26f * s ), new Vector3( 56f * s, 5f * s, 38f * s ), i == 1 ? new Color( 0.16f, 0.42f, 0.72f ) : new Color( 0.12f, 0.30f, 0.24f ), new Angles( -8, 0, 0 ), GameMaterials.Metal );
		}
		AddBoxCollider( root, new Vector3( 0, 0, 42f * s ), new Vector3( 226f * s, 84f * s, 84f * s ) );
	}

	private static void BuildHoldingCell( GameObject root, DecorDef def )
	{
		var w = def.Size.x > 10f ? def.Size.x : 180f;
		var d = def.Size.y > 10f ? def.Size.y : 160f;
		var h = def.Size.z > 10f ? def.Size.z : 200f;
		var front = -d * 0.5f;
		var wall = new Color( 0.34f, 0.34f, 0.38f );

		Box( root, "Back", new Vector3( 0, d * 0.5f - 8f, h * 0.5f ), new Vector3( w, 16f, h ), wall, default, GameMaterials.Concrete );
		foreach ( var sx in new[] { -1f, 1f } )
			Box( root, "Side", new Vector3( sx * (w * 0.5f - 8f), 0, h * 0.5f ), new Vector3( 16f, d, h ), wall.Darken( 0.06f ), default, GameMaterials.Concrete );
		Box( root, "Top", new Vector3( 0, 0, h - 6f ), new Vector3( w, d, 12f ), wall.Darken( 0.12f ), default, GameMaterials.Concrete );
		// Bars across the front.
		var bars = Math.Max( 4, (int)(w / 26f) );
		for ( var i = 0; i < bars; i++ )
		{
			var x = -w * 0.5f + 14f + (w - 28f) * i / (bars - 1);
			Box( root, $"Bar_{i}", new Vector3( x, front, (h - 24f) * 0.5f ), new Vector3( 5f, 5f, h - 24f ), SteelDark, default, GameMaterials.Metal );
		}
		Box( root, "BarTop", new Vector3( 0, front, h - 18f ), new Vector3( w, 6f, 10f ), SteelDark, default, GameMaterials.Metal );
		// Cot inside.
		Box( root, "Cot", new Vector3( 0, d * 0.24f, 24f ), new Vector3( w * 0.6f, 44f, 10f ), new Color( 0.42f, 0.40f, 0.36f ), default, GameMaterials.Concrete );

		AddBoxCollider( root, new Vector3( 0, d * 0.5f - 8f, h * 0.5f ), new Vector3( w, 16f, h ) );
	}

	private static void BuildSteamVent( GameObject root, DecorDef def )
	{
		var s = KitScale( def );
		Box( root, "Grate", new Vector3( 0, 0, 2f ), new Vector3( 56f * s, 56f * s, 4f ), SteelDark, default, GameMaterials.Metal );
		// Rising translucent steam column.
		Box( root, "SteamA", new Vector3( 0, 0, 60f * s ), new Vector3( 34f * s, 34f * s, 110f * s ), new Color( 0.92f, 0.94f, 0.97f, 0.30f ), new Angles( 0, 15, 0 ), GameMaterials.Metal );
		Box( root, "SteamB", new Vector3( 6f * s, -4f * s, 130f * s ), new Vector3( 24f * s, 24f * s, 80f * s ), new Color( 0.92f, 0.94f, 0.97f, 0.18f ), new Angles( 0, 40, 0 ), GameMaterials.Metal );
	}

	private static void BuildMachineCore( GameObject root, DecorDef def )
	{
		var s = KitScale( def );
		var glow = TintOr( def, new Color( 0.25f, 0.85f, 1f ) );

		Box( root, "Plinth", new Vector3( 0, 0, 18f * s ), new Vector3( 260f * s, 200f * s, 36f * s ), Concrete.Darken( 0.1f ), default, GameMaterials.Concrete );
		Box( root, "Housing", new Vector3( 0, 20f * s, 36f * s + 110f * s ), new Vector3( 220f * s, 150f * s, 220f * s ), SteelDark, default, GameMaterials.Metal );
		// Glowing core window on the −Y face, +1 proud.
		Box( root, "CoreFrame", new Vector3( 0, 20f * s - 75f * s - 1f, 36f * s + 100f * s ), new Vector3( 96f * s, 4f, 96f * s ), SteelLight, default, GameMaterials.Metal );
		Box( root, "Core", new Vector3( 0, 20f * s - 75f * s - 3f, 36f * s + 100f * s ), new Vector3( 74f * s, 3f, 74f * s ), glow, default, GameMaterials.Metal );
		// Conduits into the floor.
		foreach ( var sx in new[] { -1f, 1f } )
		{
			Box( root, $"Conduit_{sx}", new Vector3( sx * 130f * s, 20f * s, 60f * s ), new Vector3( 30f * s, 30f * s, 120f * s ), SteelLight.Darken( 0.1f ), new Angles( 0, 0, sx * 12f ), GameMaterials.Metal );
			Box( root, $"Duct_{sx}", new Vector3( sx * 92f * s, 20f * s, 36f * s + 226f * s ), new Vector3( 26f * s, 26f * s, 60f * s ), SteelDark, default, GameMaterials.Metal );
		}
		Box( root, "StackTop", new Vector3( 0, 20f * s, 36f * s + 240f * s ), new Vector3( 120f * s, 90f * s, 40f * s ), SteelDark.Lighten( 0.06f ), default, GameMaterials.Metal );

		AddBoxCollider( root, new Vector3( 0, 20f * s, 36f * s + 110f * s ), new Vector3( 220f * s, 150f * s, 220f * s ) );
	}

	private static void BuildWaterChannel( GameObject root, DecorDef def )
	{
		var len = MathF.Max( def.Size.x, 300f );
		var wid = def.Size.y > 10f ? def.Size.y : 140f;
		var water = TintOr( def, new Color( 0.16f, 0.44f, 0.55f ) );

		// Raised curb edges with recessed water surface between them.
		foreach ( var sy in new[] { -1f, 1f } )
			Box( root, "Curb", new Vector3( 0, sy * (wid * 0.5f + 12f), 14f ), new Vector3( len, 24f, 28f ), Concrete, default, GameMaterials.Concrete );
		Box( root, "Water", new Vector3( 0, 0, 4f ), new Vector3( len - 8f, wid, 8f ), water, default, GameMaterials.Metal );
		// Soft glow sheet +1 above the water surface.
		Box( root, "Sheen", new Vector3( 0, 0, 9f ), new Vector3( len * 0.8f, wid * 0.7f, 1f ), water.Lighten( 0.25f ).WithAlpha( 0.45f ), default, GameMaterials.Metal );

		foreach ( var sy in new[] { -1f, 1f } )
			AddBoxCollider( root, new Vector3( 0, sy * (wid * 0.5f + 12f), 14f ), new Vector3( len, 24f, 28f ) );
	}

	private static void BuildCabin( GameObject root, DecorDef def )
	{
		var w = def.Size.x;
		var d = def.Size.y;
		var h = def.Size.z;
		var log = TintOr( def, new Color( 0.48f, 0.33f, 0.20f ) );
		var front = -d * 0.5f;

		Box( root, "Foundation", new Vector3( 0, 0, 8f ), new Vector3( w + 10f, d + 10f, 16f ), new Color( 0.5f, 0.48f, 0.46f ), default, GameMaterials.Concrete );
		// Stacked log courses.
		var courses = Math.Max( 4, (int)(h / 34f) );
		for ( var i = 0; i < courses; i++ )
		{
			var z = 16f + 17f + i * 34f;
			Box( root, $"CourseF_{i}", new Vector3( 0, front + 8f, z ), new Vector3( w, 20f, 30f ), i % 2 == 0 ? log : log.Darken( 0.08f ), default, GameMaterials.Bark );
			Box( root, $"CourseB_{i}", new Vector3( 0, d * 0.5f - 8f, z ), new Vector3( w, 20f, 30f ), i % 2 == 0 ? log : log.Darken( 0.08f ), default, GameMaterials.Bark );
			Box( root, $"CourseL_{i}", new Vector3( -w * 0.5f + 8f, 0, z ), new Vector3( 20f, d - 30f, 30f ), i % 2 == 1 ? log : log.Darken( 0.08f ), default, GameMaterials.Bark );
			Box( root, $"CourseR_{i}", new Vector3( w * 0.5f - 8f, 0, z ), new Vector3( 20f, d - 30f, 30f ), i % 2 == 1 ? log : log.Darken( 0.08f ), default, GameMaterials.Bark );
		}

		var wallTop = 16f + courses * 34f;
		var faceSlot = 0;
		float FaceY() => front - DepthLayers.NextFaceDepth( ref faceSlot );
		Box( root, "Door", new Vector3( -w * 0.18f, FaceY(), 16f + 52f ), new Vector3( 48f, 6f, 100f ), new Color( 0.30f, 0.20f, 0.12f ), default, GameMaterials.Wood );
		Box( root, "Window", new Vector3( w * 0.22f, FaceY(), 16f + 84f ), new Vector3( 60f, 5f, 48f ), new Color( 0.95f, 0.86f, 0.55f ), default, GameMaterials.Metal );
		// 6 thick so the frame's face clears the 5-thick window pane by a full unit.
		Box( root, "WinFrame", new Vector3( w * 0.22f, FaceY(), 16f + 84f ), new Vector3( 70f, 6f, 58f ), log.Darken( 0.2f ), default, GameMaterials.Wood );

		// Snowy gable roof.
		BuildGableRoof( root, w, d, wallTop );
		var hw = d * 0.5f;
		var rise = d * 0.34f;
		var slope = MathF.Sqrt( hw * hw + rise * rise );
		var angle = MathF.Atan2( rise, hw ) * RadToDeg;
		Box( root, "SnowR", new Vector3( 0, hw * 0.5f, wallTop + rise * 0.5f + 9f ), new Vector3( w + 40f, slope * 0.94f, 6f ), new Color( 0.96f, 0.97f, 1f ), new Angles( 0, 0, -angle ), GameMaterials.Concrete );
		Box( root, "SnowL", new Vector3( 0, -hw * 0.5f, wallTop + rise * 0.5f + 9f ), new Vector3( w + 40f, slope * 0.94f, 6f ), new Color( 0.96f, 0.97f, 1f ), new Angles( 0, 0, angle ), GameMaterials.Concrete );
		// Chimney with snow cap, +1 above ridge.
		Box( root, "Chimney", new Vector3( w * 0.3f, d * 0.16f, wallTop + rise + 30f ), new Vector3( 34f, 34f, 90f ), new Color( 0.44f, 0.40f, 0.38f ), default, GameMaterials.Concrete );
		Box( root, "ChimneyCap", new Vector3( w * 0.3f, d * 0.16f, wallTop + rise + 76f ), new Vector3( 40f, 40f, 8f ), new Color( 0.96f, 0.97f, 1f ), default, GameMaterials.Concrete );

		AddBoxCollider( root, new Vector3( 0, 0, wallTop * 0.5f ), new Vector3( w, d, wallTop ) );
	}

	private static void BuildSofa( GameObject root, DecorDef def )
	{
		var s = KitScale( def );
		var cloth = TintOr( def, new Color( 0.30f, 0.34f, 0.44f ) );
		Box( root, "Seat", new Vector3( 0, 0, 22f * s ), new Vector3( 150f * s, 66f * s, 30f * s ), cloth, default, GameMaterials.Concrete );
		Box( root, "Back", new Vector3( 0, 26f * s, 52f * s ), new Vector3( 150f * s, 18f * s, 46f * s ), cloth.Darken( 0.08f ), default, GameMaterials.Concrete );
		foreach ( var sx in new[] { -1f, 1f } )
			Box( root, "Arm", new Vector3( sx * 70f * s, 0, 40f * s ), new Vector3( 16f * s, 62f * s, 34f * s ), cloth.Darken( 0.05f ), default, GameMaterials.Concrete );
		AddBoxCollider( root, new Vector3( 0, 8f * s, 34f * s ), new Vector3( 154f * s, 70f * s, 68f * s ) );
	}

	private static void BuildDesk( GameObject root, DecorDef def )
	{
		var s = KitScale( def );
		var wood = TintOr( def, new Color( 0.32f, 0.22f, 0.14f ) );
		Box( root, "Top", new Vector3( 0, 0, 76f * s ), new Vector3( 180f * s, 84f * s, 8f * s ), wood, default, GameMaterials.Wood );
		foreach ( var sx in new[] { -1f, 1f } )
			Box( root, "Pedestal", new Vector3( sx * 70f * s, 0, 38f * s ), new Vector3( 36f * s, 76f * s, 72f * s ), wood.Darken( 0.1f ), default, GameMaterials.Wood );
		Box( root, "Monitor", new Vector3( -20f * s, 14f * s, 80f * s + 22f * s ), new Vector3( 44f * s, 6f * s, 32f * s ), GlassDark, new Angles( 0, 12, 0 ), GameMaterials.Metal );
		// Toppled chair beside the desk.
		Box( root, "ChairSeat", new Vector3( 110f * s, -30f * s, 16f * s ), new Vector3( 46f * s, 46f * s, 10f * s ), GlassDark, new Angles( 0, 30, 78 ), GameMaterials.Metal );
		Box( root, "ChairBack", new Vector3( 132f * s, -42f * s, 20f * s ), new Vector3( 44f * s, 8f * s, 52f * s ), GlassDark, new Angles( 0, 30, 78 ), GameMaterials.Metal );
		AddBoxCollider( root, new Vector3( 0, 0, 40f * s ), new Vector3( 180f * s, 84f * s, 80f * s ) );
	}

	private static void BuildRoofUnit( GameObject root, DecorDef def )
	{
		var s = KitScale( def );
		Box( root, "Body", new Vector3( 0, 0, 26f * s ), new Vector3( 90f * s, 64f * s, 52f * s ), SteelLight, default, GameMaterials.Metal );
		Box( root, "FanRing", new Vector3( 0, 0, 53f * s ), new Vector3( 48f * s, 48f * s, 6f * s ), SteelDark, default, GameMaterials.Metal );
		Box( root, "Vents", new Vector3( 0, -33f * s, 26f * s ), new Vector3( 70f * s, 3f, 34f * s ), SteelDark, default, GameMaterials.Metal );
		AddBoxCollider( root, new Vector3( 0, 0, 26f * s ), new Vector3( 90f * s, 64f * s, 52f * s ) );
	}

	/// <summary>Platform edge + track bed with rails, drop on the −Y side.
	/// Bed sits just above the map-field plane so it never vanishes beneath terrain.</summary>
	private static void BuildTrackTrench( GameObject root, DecorDef def )
	{
		var len = MathF.Max( def.Size.x, 400f );

		// Tactile warning strip along the platform lip.
		Box( root, "Tactile", new Vector3( 0, 26f, 2.5f ), new Vector3( len, 34f, 1f ), WarnYellow, default, GameMaterials.Concrete );
		// Dark track bed sunk a few units, retaining wall on the far side.
		Box( root, "Bed", new Vector3( 0, -90f, -3f ), new Vector3( len, 190f, 4f ), new Color( 0.14f, 0.14f, 0.16f ), default, GameMaterials.Concrete );
		Box( root, "LipWall", new Vector3( 0, 4f, 6f ), new Vector3( len, 10f, 14f ), Concrete.Darken( 0.2f ), default, GameMaterials.Concrete );
		Box( root, "FarWall", new Vector3( 0, -186f, 70f ), new Vector3( len, 16f, 140f ), new Color( 0.42f, 0.40f, 0.44f ), default, GameMaterials.Concrete );

		// Rails + sleepers on the bed.
		var sleepers = Math.Max( 6, (int)(len / 80f) );
		for ( var i = 0; i < sleepers; i++ )
		{
			var x = -len * 0.5f + len * (i + 0.5f) / sleepers;
			Box( root, $"Sleeper_{i}", new Vector3( x, -90f, 1f ), new Vector3( 24f, 110f, 4f ), new Color( 0.28f, 0.22f, 0.17f ), default, GameMaterials.Bark );
		}
		foreach ( var off in new[] { -36f, 36f } )
			Box( root, "Rail", new Vector3( 0, -90f + off, 6f ), new Vector3( len, 7f, 7f ), SteelLight, default, GameMaterials.Metal );

		// Keep the player from walking onto the tracks.
		AddBoxCollider( root, new Vector3( 0, 0, 60f ), new Vector3( len, 10f, 120f ) );
	}

	private static void BuildBlastDoor( GameObject root, DecorDef def )
	{
		var w = def.Size.x > 10f ? def.Size.x : 220f;
		var t = def.Size.y > 4f ? def.Size.y : 30f;
		var h = def.Size.z > 10f ? def.Size.z : 240f;
		var steel = TintOr( def, new Color( 0.38f, 0.40f, 0.46f ) );

		foreach ( var sx in new[] { -1f, 1f } )
			Box( root, "Jamb", new Vector3( sx * (w * 0.5f + 18f), 0, h * 0.5f ), new Vector3( 36f, t + 8f, h ), Concrete.Darken( 0.1f ), default, GameMaterials.Concrete );
		Box( root, "Lintel", new Vector3( 0, 0, h + 14f ), new Vector3( w + 72f, t + 8f, 28f ), Concrete.Darken( 0.14f ), default, GameMaterials.Concrete );
		// Two heavy leaves, one ajar.
		Box( root, "LeafL", new Vector3( -w * 0.26f, 0, (h - 16f) * 0.5f ), new Vector3( w * 0.46f, t, h - 16f ), steel, default, GameMaterials.Metal );
		Box( root, "LeafR", new Vector3( w * 0.30f, -t * 0.7f, (h - 16f) * 0.5f ), new Vector3( w * 0.46f, t, h - 16f ), steel.Darken( 0.08f ), new Angles( 0, 24, 0 ), GameMaterials.Metal );
		Box( root, "StripeL", new Vector3( -w * 0.26f, -t * 0.5f - 1f, h * 0.5f ), new Vector3( w * 0.34f, 3f, 20f ), WarnYellow, new Angles( 0, 0, 40 ), GameMaterials.Metal );

		foreach ( var sx in new[] { -1f, 1f } )
			AddBoxCollider( root, new Vector3( sx * (w * 0.5f + 18f), 0, h * 0.5f ), new Vector3( 36f, t + 8f, h ) );
	}
}
