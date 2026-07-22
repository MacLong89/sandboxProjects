namespace CatchACritter;

/// <summary>Low-poly kit helpers built from dev primitives (no invented asset paths).</summary>
public static class Kit
{
	public const string BoxModel = "models/dev/box.vmdl";
	public const string SphereModel = "models/dev/sphere.vmdl";
	public const string PlaneModel = "models/dev/plane.vmdl";
	public const string DefaultMaterial = "materials/default.vmat";

	/// <summary>Axis-aligned tinted box. Scale is full size in units; pivot lifted so it sits on localPos.z.</summary>
	public static GameObject Box( GameObject parent, string name, Vector3 localPos, Vector3 size, Color tint, bool collider = false, bool trigger = false )
	{
		var go = new GameObject( true, name );
		go.SetParent( parent );
		go.LocalPosition = localPos + Vector3.Up * (size.z * 0.5f);
		go.LocalScale = size / 50f;

		var renderer = go.Components.Create<ModelRenderer>();
		renderer.Model = Model.Load( BoxModel );
		renderer.MaterialOverride = Material.Load( DefaultMaterial );
		renderer.Tint = tint;

		if ( collider )
		{
			var box = go.Components.Create<BoxCollider>();
			box.Scale = new Vector3( 50f, 50f, 50f );
			box.IsTrigger = trigger;
			box.Static = true;
		}

		return go;
	}

	/// <summary>Centered tinted box (pivot at center, not foot). For angled/stacked parts.</summary>
	public static GameObject BoxCentered( GameObject parent, string name, Vector3 localPos, Vector3 size, Color tint, Rotation? rot = null )
	{
		var go = new GameObject( true, name );
		go.SetParent( parent );
		go.LocalPosition = localPos;
		if ( rot.HasValue ) go.LocalRotation = rot.Value;
		go.LocalScale = size / 50f;

		var renderer = go.Components.Create<ModelRenderer>();
		renderer.Model = Model.Load( BoxModel );
		renderer.MaterialOverride = Material.Load( DefaultMaterial );
		renderer.Tint = tint;
		return go;
	}

	/// <summary>Tinted sphere, pivot at center.</summary>
	public static GameObject Sphere( GameObject parent, string name, Vector3 localPos, Vector3 size, Color tint )
	{
		var go = new GameObject( true, name );
		go.SetParent( parent );
		go.LocalPosition = localPos;
		go.LocalScale = size / 50f;

		var renderer = go.Components.Create<ModelRenderer>();
		renderer.Model = Model.Load( SphereModel );
		renderer.MaterialOverride = Material.Load( DefaultMaterial );
		renderer.Tint = tint;
		return go;
	}

	/// <summary>Invisible collider wall.</summary>
	public static GameObject Wall( GameObject parent, string name, Vector3 localPos, Vector3 size, float yawDeg = 0f )
	{
		var go = new GameObject( true, name );
		go.SetParent( parent );
		go.LocalPosition = localPos + Vector3.Up * (size.z * 0.5f);
		go.LocalRotation = Rotation.FromYaw( yawDeg );

		var box = go.Components.Create<BoxCollider>();
		box.Scale = size;
		box.Static = true;
		return go;
	}

	// TextRenderer.Scale is a small multiplier (~0.1-0.5), not world units.
	public static GameObject Label( GameObject parent, string name, Vector3 localPos, string text, Color color, float scale = 0.25f, int fontSize = 42 )
	{
		var go = new GameObject( true, name );
		go.SetParent( parent );
		go.LocalPosition = localPos;

		var tp = go.Components.Create<TextRenderer>();
		tp.Text = text;
		tp.Color = color;
		tp.Scale = scale;
		tp.FontSize = fontSize;
		tp.HorizontalAlignment = TextRenderer.HAlignment.Center;
		tp.VerticalAlignment = TextRenderer.VAlignment.Center;
		tp.Billboard = TextRenderer.BillboardMode.YOnly;
		return go;
	}

	// ---- Composite props ----

	public static void Tree( GameObject parent, Vector3 pos, Color trunk, Color canopy, float scale, Random rng )
	{
		var s = scale * (0.85f + (float)rng.NextDouble() * 0.4f);
		var root = new GameObject( true, "Tree" );
		root.SetParent( parent );
		root.LocalPosition = pos;
		root.LocalRotation = Rotation.FromYaw( (float)rng.NextDouble() * 360f );

		Box( root, "Trunk", Vector3.Zero, new Vector3( 14f * s, 14f * s, 70f * s ), trunk );
		Sphere( root, "Canopy", new Vector3( 0, 0, 92f * s ), new Vector3( 95f, 95f, 78f ) * s, canopy );
		Sphere( root, "Canopy2", new Vector3( 22f * s, 12f * s, 62f * s ), new Vector3( 60f, 60f, 50f ) * s, canopy.Darken( 0.08f ) );
	}

	public static void PineTree( GameObject parent, Vector3 pos, Color trunk, Color canopy, float scale, Random rng )
	{
		var s = scale * (0.85f + (float)rng.NextDouble() * 0.4f);
		var root = new GameObject( true, "Pine" );
		root.SetParent( parent );
		root.LocalPosition = pos;

		Box( root, "Trunk", Vector3.Zero, new Vector3( 12f * s, 12f * s, 55f * s ), trunk );
		Box( root, "Tier1", new Vector3( 0, 0, 45f * s ), new Vector3( 85f, 85f, 34f ) * s, canopy );
		Box( root, "Tier2", new Vector3( 0, 0, 78f * s ), new Vector3( 62f, 62f, 30f ) * s, canopy.Lighten( 0.05f ) );
		Box( root, "Tier3", new Vector3( 0, 0, 107f * s ), new Vector3( 38f, 38f, 26f ) * s, canopy.Lighten( 0.1f ) );
	}

	public static void Rock( GameObject parent, Vector3 pos, Color color, float scale, Random rng )
	{
		var s = scale * (0.7f + (float)rng.NextDouble() * 0.6f);
		var root = new GameObject( true, "Rock" );
		root.SetParent( parent );
		root.LocalPosition = pos;
		root.LocalRotation = Rotation.FromYaw( (float)rng.NextDouble() * 360f );

		Sphere( root, "R1", new Vector3( 0, 0, 18f * s ), new Vector3( 56f, 48f, 40f ) * s, color );
		Sphere( root, "R2", new Vector3( 20f * s, 14f * s, 10f * s ), new Vector3( 32f, 30f, 24f ) * s, color.Darken( 0.07f ) );
	}

	public static void Crystal( GameObject parent, Vector3 pos, Color color, float scale, Random rng )
	{
		var s = scale * (0.8f + (float)rng.NextDouble() * 0.5f);
		var root = new GameObject( true, "Crystal" );
		root.SetParent( parent );
		root.LocalPosition = pos;
		root.LocalRotation = Rotation.FromYaw( (float)rng.NextDouble() * 360f );

		BoxCentered( root, "C1", new Vector3( 0, 0, 34f * s ), new Vector3( 22f, 22f, 78f ) * s, color, Rotation.From( 8f, 15f, 6f ) );
		BoxCentered( root, "C2", new Vector3( 16f * s, 10f * s, 20f * s ), new Vector3( 14f, 14f, 46f ) * s, color.Lighten( 0.15f ), Rotation.From( -10f, 40f, -7f ) );
	}

	public static void Flower( GameObject parent, Vector3 pos, Color petal, Random rng )
	{
		var root = new GameObject( true, "Flower" );
		root.SetParent( parent );
		root.LocalPosition = pos;
		Box( root, "Stem", Vector3.Zero, new Vector3( 3f, 3f, 16f ), new Color( 0.35f, 0.55f, 0.3f ) );
		Sphere( root, "Bloom", new Vector3( 0, 0, 20f ), new Vector3( 12f, 12f, 9f ), petal );
	}
}
