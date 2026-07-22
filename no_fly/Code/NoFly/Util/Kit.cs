namespace NoFly;

/// <summary>
/// Primitive kit + composed airport props. All boxes are ground-lifted (center pivot).
/// localPos.z is the bottom of the box.
/// </summary>
public static class Kit
{
	public const string BoxModel = "models/dev/box.vmdl";
	public const string SphereModel = "models/dev/sphere.vmdl";
	public const string CitizenModel = "models/citizen/citizen.vmdl";
	public const string DefaultMaterial = "materials/default.vmat";

	/// <summary>Top of a standard floor slab (bottom at 0, thickness FloorThick).</summary>
	public const float FloorThick = 4f;
	public const float FloorTop = FloorThick;
	/// <summary>Clearance used so overlays never share a plane with the surface below.</summary>
	public const float Skin = 0.75f;

	public static GameObject Box( GameObject parent, string name, Vector3 localPos, Vector3 scale, Color tint, bool collider = true, bool trigger = false )
	{
		var go = new GameObject( true, name );
		go.SetParent( parent );
		go.LocalPosition = localPos + Vector3.Up * (scale.z * 0.5f);
		go.LocalScale = scale / 50f;

		var renderer = go.Components.Create<ModelRenderer>();
		renderer.Model = Model.Load( BoxModel );
		renderer.MaterialOverride = Material.Load( DefaultMaterial );
		renderer.Tint = tint;

		if ( collider )
		{
			var box = go.Components.Create<BoxCollider>();
			box.Scale = new Vector3( 50f, 50f, 50f );
			box.IsTrigger = trigger;
		}

		return go;
	}

	public static GameObject Floor( GameObject parent, string name, Vector3 center, Vector2 size, Color tint )
		=> Box( parent, name, new Vector3( center.x, center.y, 0f ), new Vector3( size.x, size.y, FloorThick ), tint );

	/// <summary>Thin decorative slab sitting clearly above the floor top (no z-fight).</summary>
	public static GameObject Decal( GameObject parent, string name, Vector3 center, Vector2 size, Color tint, float thick = 1.5f )
		=> Box( parent, name, new Vector3( center.x, center.y, FloorTop + Skin ), new Vector3( size.x, size.y, thick ), tint, collider: false );

	public static GameObject Ceiling( GameObject parent, string name, Vector3 center, Vector2 size, float height, Color tint )
		// Sit slightly above wall tops so ceiling underside ≠ wall top plane.
		=> Box( parent, name, new Vector3( center.x, center.y, height + Skin ), new Vector3( size.x, size.y, 6f ), tint );

	public static GameObject Wall( GameObject parent, string name, Vector3 center, Vector3 size, Color tint )
		=> Box( parent, name, center, size, tint );

	/// <summary>Walk-through arch: two pillars + lintel (center stays open).</summary>
	public static void Arch( GameObject parent, string name, Vector3 pos, float width, float height, float depth, Color tint )
	{
		var pillar = 28f;
		var open = MathF.Max( width - pillar * 2f, 40f );
		var z = FloorTop + Skin;
		Box( parent, $"{name}_L", pos + new Vector3( 0f, open * 0.5f + pillar * 0.5f, z ), new Vector3( depth, pillar, height ), tint );
		Box( parent, $"{name}_R", pos + new Vector3( 0f, -(open * 0.5f + pillar * 0.5f), z ), new Vector3( depth, pillar, height ), tint );
		Box( parent, $"{name}_Top", pos + new Vector3( 0f, 0f, z + height - 18f ), new Vector3( depth + 4f, width, 18f ), tint );
	}

	public static void Room(
		GameObject parent,
		string name,
		Vector3 center,
		Vector2 size,
		float wallHeight,
		Color wallTint,
		Color ceilingTint,
		float openEast = 0f,
		float openWest = 0f,
		float openNorth = 0f,
		float openSouth = 0f )
	{
		var hx = size.x * 0.5f;
		var hy = size.y * 0.5f;
		var thick = 16f;
		var board = 7f;

		// East/West walls cover corners; North/South stop between them — no shared faces.
		void WallEW( string suffix, float xWall, float y, float sy )
		{
			if ( sy < 8f ) return;
			Wall( parent, $"{name}_{suffix}", new Vector3( center.x + xWall, center.y + y, 0f ), new Vector3( thick, sy, wallHeight ), wallTint );
			// Interior baseboard — inset so it never shares the wall's outer face.
			var inward = xWall < 0f ? 1f : -1f;
			Box( parent, $"{name}_{suffix}_Base",
				new Vector3( center.x + xWall + inward * (thick * 0.5f + board * 0.5f), center.y + y, FloorTop + Skin ),
				new Vector3( board, MathF.Max( sy - 4f, 4f ), 8f ), AirportPalette.Baseboard, collider: false );
		}

		void WallNS( string suffix, float yWall, float x, float sx )
		{
			if ( sx < 8f ) return;
			Wall( parent, $"{name}_{suffix}", new Vector3( center.x + x, center.y + yWall, 0f ), new Vector3( sx, thick, wallHeight ), wallTint );
			var inward = yWall < 0f ? 1f : -1f;
			Box( parent, $"{name}_{suffix}_Base",
				new Vector3( center.x + x, center.y + yWall + inward * (thick * 0.5f + board * 0.5f), FloorTop + Skin ),
				new Vector3( MathF.Max( sx - 4f, 4f ), board, 8f ), AirportPalette.Baseboard, collider: false );
		}

		void SplitY( string side, float xWall, float open )
		{
			var span = size.y + thick * 2f; // covers NS corners
			if ( open > 0f )
			{
				var gap = open * 0.5f;
				var remain = span * 0.5f - gap;
				WallEW( $"{side}_N", xWall, (gap + span * 0.5f) * 0.5f, remain );
				WallEW( $"{side}_S", xWall, -(gap + span * 0.5f) * 0.5f, remain );
				// Header inset into the doorway (not coplanar with wall face)
				var inward = xWall < 0f ? 1f : -1f;
				Box( parent, $"{name}_{side}_Header",
					new Vector3( center.x + xWall + inward * 3f, center.y, wallHeight - 28f ),
					new Vector3( thick + 2f, open + 12f, 20f ), AirportPalette.WallTrim, collider: false );
			}
			else WallEW( side, xWall, 0f, span );
		}

		void SplitX( string side, float yWall, float open )
		{
			var span = size.x; // between EW walls — no corner overlap
			if ( open > 0f )
			{
				var gap = open * 0.5f;
				var remain = span * 0.5f - gap;
				WallNS( $"{side}_E", yWall, (gap + span * 0.5f) * 0.5f, remain );
				WallNS( $"{side}_W", yWall, -(gap + span * 0.5f) * 0.5f, remain );
				var inward = yWall < 0f ? 1f : -1f;
				Box( parent, $"{name}_{side}_Header",
					new Vector3( center.x, center.y + yWall + inward * 3f, wallHeight - 28f ),
					new Vector3( open + 12f, thick + 2f, 20f ), AirportPalette.WallTrim, collider: false );
			}
			else WallNS( side, yWall, 0f, span );
		}

		SplitY( "W", -hx, openWest );
		SplitY( "E", hx, openEast );
		SplitX( "S", -hy, openSouth );
		SplitX( "N", hy, openNorth );

		Ceiling( parent, $"{name}_Ceiling", center, size + new Vector2( thick, thick ), wallHeight, ceilingTint );
	}

	public static GameObject Label( GameObject parent, string name, Vector3 localPos, string text, Color color, float letterHeight = 20f )
	{
		var go = new GameObject( true, name );
		go.SetParent( parent );
		go.LocalPosition = localPos;

		var tp = go.Components.Create<TextRenderer>();
		tp.Text = text;
		tp.Color = color;
		tp.FontSize = 64;
		tp.Scale = letterHeight / 64f;
		tp.HorizontalAlignment = TextRenderer.HAlignment.Center;
		tp.VerticalAlignment = TextRenderer.VAlignment.Center;
		tp.Billboard = TextRenderer.BillboardMode.YOnly;
		return go;
	}

	// ---- Composed props ----------------------------------------------------

	public static void Pillar( GameObject parent, string name, Vector3 pos, float height )
	{
		var footH = 10f;
		var baseZ = FloorTop + Skin;
		Box( parent, $"{name}_Foot", pos.WithZ( baseZ ), new Vector3( 34f, 34f, footH ), AirportPalette.Baseboard, collider: false );
		Box( parent, $"{name}_Shaft", pos.WithZ( baseZ + footH ), new Vector3( 26f, 26f, height - footH ), AirportPalette.Column );
		Box( parent, $"{name}_Cap", pos.WithZ( baseZ + height - 12f ), new Vector3( 34f, 34f, 12f ), AirportPalette.WallTrim, collider: false );
	}

	public static void Counter( GameObject parent, string name, Vector3 pos, Vector3 size )
	{
		var z = FloorTop + Skin;
		var bodyH = size.z * 0.78f;
		Box( parent, $"{name}_Body", pos.WithZ( z ), new Vector3( size.x, size.y, bodyH ), AirportPalette.Desk );
		Box( parent, $"{name}_Top", pos.WithZ( z + bodyH ), new Vector3( size.x + 6f, size.y + 6f, size.z * 0.22f ), AirportPalette.DeskTop );
	}

	public static void SignPanel( GameObject parent, string name, Vector3 pos, Vector2 size, string text, Color accent )
	{
		Box( parent, $"{name}_Back", pos, new Vector3( size.x, 10f, size.y ), AirportPalette.Screen );
		// Accent bar offset forward so it doesn't share the screen's front plane.
		Box( parent, $"{name}_Bar", pos + new Vector3( 0f, 8f, size.y * 0.5f - 6f ), new Vector3( size.x + 4f, 6f, 10f ), accent, collider: false );
		Label( parent, $"{name}_Text", pos + new Vector3( 0f, 10f, 8f ), text, Color.White, MathF.Min( 18f, size.y * 0.28f ) );
	}

	public static void Stanchion( GameObject parent, string name, Vector3 pos, Color rope )
	{
		var z = FloorTop + Skin;
		Box( parent, $"{name}_Base", pos.WithZ( z ), new Vector3( 22f, 22f, 6f ), AirportPalette.MetalDark, collider: false );
		Box( parent, $"{name}_Post", pos.WithZ( z + 6f ), new Vector3( 10f, 10f, 42f ), AirportPalette.Metal );
		Box( parent, $"{name}_Rope", pos + new Vector3( 0f, 18f, z + 36f ), new Vector3( 6f, 36f, 4f ), rope, collider: false );
	}

	public static void Bench( GameObject parent, string name, Vector3 pos, float width )
	{
		var z = FloorTop + Skin;
		Box( parent, $"{name}_LegL", pos + new Vector3( -width * 0.4f, 0f, z ), new Vector3( 8f, 8f, 22f ), AirportPalette.Metal, collider: false );
		Box( parent, $"{name}_LegR", pos + new Vector3( width * 0.4f, 0f, z ), new Vector3( 8f, 8f, 22f ), AirportPalette.Metal, collider: false );
		Box( parent, $"{name}_Seat", pos + new Vector3( 0f, 0f, z + 22f ), new Vector3( width, 40f, 10f ), AirportPalette.SeatCushion );
		Box( parent, $"{name}_Back", pos + new Vector3( 0f, -16f, z + 32f ), new Vector3( width, 8f, 28f ), AirportPalette.Seat );
	}

	public static void Kiosk( GameObject parent, string name, Vector3 pos, Color accent, string title )
	{
		var z = FloorTop + Skin;
		Box( parent, $"{name}_Body", pos.WithZ( z ), new Vector3( 120f, 80f, 70f ), AirportPalette.Wall );
		Box( parent, $"{name}_Accent", pos.WithZ( z + 70f + Skin ), new Vector3( 124f, 84f, 14f ), accent, collider: false );
		Box( parent, $"{name}_Counter", pos + new Vector3( 0f, 44f, z + 40f ), new Vector3( 110f, 16f, 8f ), AirportPalette.DeskTop );
		Label( parent, $"{name}_Title", pos + Vector3.Up * (z + 95f), title, Color.White, 14f );
	}

	/// <summary>Nearly invisible floor pad used as an interact trigger — sits above the floor slab.</summary>
	public static GameObject InteractPad( GameObject parent, string name, Vector3 pos, Vector2 size, Color hint )
	{
		var tint = hint.WithAlpha( 0.35f );
		return Box( parent, name, pos.WithZ( FloorTop + Skin ), new Vector3( size.x, size.y, 2f ), tint, true, true );
	}

	public static void Plane( GameObject parent, string name, Vector3 pos, string label )
	{
		Box( parent, $"{name}_Fuselage", pos, new Vector3( 260f, 48f, 48f ), AirportPalette.PlaneBody );
		Box( parent, $"{name}_Nose", pos + new Vector3( 140f, 0f, 0f ), new Vector3( 50f, 36f, 36f ), AirportPalette.PlaneBody );
		Box( parent, $"{name}_Wing", pos + new Vector3( -10f, 0f, 4f ), new Vector3( 90f, 200f, 8f ), AirportPalette.PlaneBody );
		Box( parent, $"{name}_Tail", pos + new Vector3( -120f, 0f, 40f ), new Vector3( 20f, 10f, 70f ), AirportPalette.PlaneAccent );
		Box( parent, $"{name}_Stab", pos + new Vector3( -120f, 0f, 20f ), new Vector3( 24f, 70f, 6f ), AirportPalette.PlaneAccent );
		// Slightly proud of the fuselage so the stripe doesn't z-fight.
		Box( parent, $"{name}_Stripe", pos + new Vector3( 20f, 0f, 10f ), new Vector3( 200f, 52f, 4f ), AirportPalette.PlaneAccent, collider: false );
		Label( parent, $"{name}_Label", pos + Vector3.Up * 70f, label, Color.White, 12f );
	}

	public static Color RandomOutfit()
	{
		var colors = new[]
		{
			new Color( 0.55f, 0.62f, 0.72f ),
			new Color( 0.72f, 0.55f, 0.48f ),
			new Color( 0.45f, 0.58f, 0.52f ),
			new Color( 0.62f, 0.58f, 0.45f ),
			new Color( 0.50f, 0.50f, 0.58f ),
			new Color( 0.68f, 0.48f, 0.42f ),
		};
		return Random.Shared.FromArray( colors );
	}
}
