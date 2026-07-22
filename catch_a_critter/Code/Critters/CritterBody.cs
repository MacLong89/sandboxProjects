namespace CatchACritter;

/// <summary>
/// Builds a critter body. Species with a rigged animal model (scratch-v6
/// library shared from scene_lab) get a real skinned mesh; everyone else gets
/// the procedural chibi body from dev primitives. Deterministic — every client
/// builds the same body from synced species data.
/// </summary>
public static class CritterBody
{
	public static readonly Color ShinyGold = new( 1f, 0.85f, 0.35f );

	const float UnitsPerMeter = 39.37f;

	public static GameObject Build( GameObject parent, SpeciesDef def, bool shiny, float size )
	{
		var skin = SpeciesCatalog.SkinFor( def.Id );
		if ( skin is not null )
		{
			var modeled = BuildModeled( parent, def, skin, shiny, size );
			if ( modeled.IsValid() ) return modeled;
		}

		var s = def.Size * size;
		var primary = shiny ? Color.Lerp( def.Primary, ShinyGold, 0.6f ) : def.Primary;
		var secondary = shiny ? Color.Lerp( def.Secondary, Color.White, 0.35f ) : def.Secondary;

		var root = new GameObject( true, "Body" );
		root.SetParent( parent );
		root.LocalPosition = Vector3.Zero;

		float headZ;
		switch ( def.Body )
		{
			case BodyShape.Tall:
				Kit.Sphere( root, "Torso", new Vector3( 0, 0, 34f * s ), new Vector3( 34f, 30f, 52f ) * s, primary );
				Kit.Sphere( root, "Belly", new Vector3( 6f * s, 0, 30f * s ), new Vector3( 24f, 22f, 34f ) * s, secondary );
				headZ = 68f * s;
				break;
			case BodyShape.Long:
				Kit.Sphere( root, "Torso", new Vector3( -6f * s, 0, 22f * s ), new Vector3( 56f, 28f, 30f ) * s, primary );
				Kit.Sphere( root, "Belly", new Vector3( -4f * s, 0, 16f * s ), new Vector3( 40f, 20f, 20f ) * s, secondary );
				headZ = 40f * s;
				break;
			case BodyShape.Chunky:
				Kit.Sphere( root, "Torso", new Vector3( 0, 0, 24f * s ), new Vector3( 48f, 44f, 40f ) * s, primary );
				Kit.Sphere( root, "Belly", new Vector3( 8f * s, 0, 20f * s ), new Vector3( 34f, 32f, 28f ) * s, secondary );
				headZ = 52f * s;
				break;
			default: // Round
				Kit.Sphere( root, "Torso", new Vector3( 0, 0, 24f * s ), new Vector3( 40f, 36f, 38f ) * s, primary );
				Kit.Sphere( root, "Belly", new Vector3( 7f * s, 0, 20f * s ), new Vector3( 28f, 26f, 26f ) * s, secondary );
				headZ = 50f * s;
				break;
		}

		// Head faces +X (forward).
		var headX = def.Body == BodyShape.Long ? 18f * s : 6f * s;
		Kit.Sphere( root, "Head", new Vector3( headX, 0, headZ ), new Vector3( 30f, 28f, 27f ) * s, primary );

		// Eyes
		var eye = new Color( 0.1f, 0.09f, 0.12f );
		Kit.Sphere( root, "EyeL", new Vector3( headX + 11f * s, -7f * s, headZ + 3f * s ), new Vector3( 5f, 5f, 6f ) * s, eye );
		Kit.Sphere( root, "EyeR", new Vector3( headX + 11f * s, 7f * s, headZ + 3f * s ), new Vector3( 5f, 5f, 6f ) * s, eye );
		Kit.Sphere( root, "Snout", new Vector3( headX + 13f * s, 0, headZ - 4f * s ), new Vector3( 8f, 7f, 6f ) * s, secondary );

		BuildEars( root, def, primary, secondary, headX, headZ, s );
		BuildTail( root, def, primary, secondary, s );

		// Stubby feet
		var footColor = primary.Darken( 0.12f );
		Kit.Sphere( root, "FootL", new Vector3( 6f * s, -10f * s, 5f * s ), new Vector3( 12f, 10f, 9f ) * s, footColor );
		Kit.Sphere( root, "FootR", new Vector3( 6f * s, 10f * s, 5f * s ), new Vector3( 12f, 10f, 9f ) * s, footColor );

		return root;
	}

	/// <summary>
	/// Spawns a skinned animal model normalized to the species' chibi height.
	/// Returns null (caller falls back to primitives) if the model is missing.
	/// </summary>
	static GameObject BuildModeled( GameObject parent, SpeciesDef def, SpeciesCatalog.ModelSkin skin, bool shiny, float size )
	{
		var model = Model.Load( skin.ModelPath );
		if ( model is null || model.IsError )
		{
			Log.Warning( $"Critter model missing: {skin.ModelPath} ({def.Id}) — using kit body" );
			return null;
		}

		var root = new GameObject( true, "Body" );
		root.SetParent( parent );
		root.LocalPosition = Vector3.Zero;
		root.LocalRotation = Rotation.Identity;

		// Keep gameplay proportions consistent with the procedural bodies
		// (~78 units tall at Size 1) no matter how big the real animal is.
		var targetHeight = 78f * def.Size * size;
		root.LocalScale = Vector3.One * (targetHeight / (skin.HeightMeters * UnitsPerMeter));

		var renderer = root.Components.Create<SkinnedModelRenderer>();
		renderer.Model = model;
		renderer.UseAnimGraph = false;
		renderer.Sequence.Name = $"{skin.SeqPrefix}_idle";
		renderer.Sequence.Looping = true;

		// Pull the baked texture toward the species palette so recolors of the
		// same animal (frost fox vs sun fox) and shinies read at a glance.
		var tint = Color.Lerp( Color.White, def.Primary, 0.4f );
		if ( shiny ) tint = Color.Lerp( tint, ShinyGold, 0.55f );
		renderer.Tint = tint;

		return root;
	}

	static void BuildEars( GameObject root, SpeciesDef def, Color primary, Color secondary, float headX, float headZ, float s )
	{
		switch ( def.Ears )
		{
			case EarStyle.Pointy:
				Kit.BoxCentered( root, "EarL", new Vector3( headX - 2f * s, -10f * s, headZ + 16f * s ), new Vector3( 6f, 8f, 18f ) * s, primary, Rotation.From( 0, 0, -12f ) );
				Kit.BoxCentered( root, "EarR", new Vector3( headX - 2f * s, 10f * s, headZ + 16f * s ), new Vector3( 6f, 8f, 18f ) * s, primary, Rotation.From( 0, 0, 12f ) );
				break;
			case EarStyle.Round:
				Kit.Sphere( root, "EarL", new Vector3( headX - 2f * s, -12f * s, headZ + 13f * s ), new Vector3( 13f, 6f, 13f ) * s, primary );
				Kit.Sphere( root, "EarR", new Vector3( headX - 2f * s, 12f * s, headZ + 13f * s ), new Vector3( 13f, 6f, 13f ) * s, primary );
				break;
			case EarStyle.Long:
				Kit.BoxCentered( root, "EarL", new Vector3( headX - 4f * s, -8f * s, headZ + 24f * s ), new Vector3( 7f, 6f, 34f ) * s, primary, Rotation.From( -6f, 0, -6f ) );
				Kit.BoxCentered( root, "EarR", new Vector3( headX - 4f * s, 8f * s, headZ + 24f * s ), new Vector3( 7f, 6f, 34f ) * s, primary, Rotation.From( -6f, 0, 6f ) );
				Kit.BoxCentered( root, "EarInnerL", new Vector3( headX - 3f * s, -8f * s, headZ + 24f * s ), new Vector3( 8f, 3f, 24f ) * s, secondary, Rotation.From( -6f, 0, -6f ) );
				Kit.BoxCentered( root, "EarInnerR", new Vector3( headX - 3f * s, 8f * s, headZ + 24f * s ), new Vector3( 8f, 3f, 24f ) * s, secondary, Rotation.From( -6f, 0, 6f ) );
				break;
			case EarStyle.Horn:
				Kit.BoxCentered( root, "Horn", new Vector3( headX + 2f * s, 0, headZ + 18f * s ), new Vector3( 7f, 7f, 22f ) * s, secondary, Rotation.From( 12f, 0, 0 ) );
				Kit.BoxCentered( root, "HornL", new Vector3( headX - 4f * s, -9f * s, headZ + 13f * s ), new Vector3( 5f, 5f, 14f ) * s, secondary, Rotation.From( 0, 0, -25f ) );
				Kit.BoxCentered( root, "HornR", new Vector3( headX - 4f * s, 9f * s, headZ + 13f * s ), new Vector3( 5f, 5f, 14f ) * s, secondary, Rotation.From( 0, 0, 25f ) );
				break;
			case EarStyle.Antenna:
				Kit.BoxCentered( root, "AntL", new Vector3( headX, -6f * s, headZ + 18f * s ), new Vector3( 2.5f, 2.5f, 20f ) * s, primary.Darken( 0.15f ), Rotation.From( 0, 0, -10f ) );
				Kit.BoxCentered( root, "AntR", new Vector3( headX, 6f * s, headZ + 18f * s ), new Vector3( 2.5f, 2.5f, 20f ) * s, primary.Darken( 0.15f ), Rotation.From( 0, 0, 10f ) );
				Kit.Sphere( root, "AntTipL", new Vector3( headX - 2f * s, -8f * s, headZ + 28f * s ), new Vector3( 7f, 7f, 7f ) * s, secondary );
				Kit.Sphere( root, "AntTipR", new Vector3( headX - 2f * s, 8f * s, headZ + 28f * s ), new Vector3( 7f, 7f, 7f ) * s, secondary );
				break;
		}
	}

	static void BuildTail( GameObject root, SpeciesDef def, Color primary, Color secondary, float s )
	{
		switch ( def.Tail )
		{
			case TailStyle.Nub:
				Kit.Sphere( root, "Tail", new Vector3( -20f * s, 0, 22f * s ), new Vector3( 12f, 12f, 12f ) * s, secondary );
				break;
			case TailStyle.Bushy:
				Kit.Sphere( root, "Tail", new Vector3( -26f * s, 0, 30f * s ), new Vector3( 26f, 14f, 16f ) * s, primary );
				Kit.Sphere( root, "TailTip", new Vector3( -36f * s, 0, 36f * s ), new Vector3( 12f, 10f, 11f ) * s, secondary );
				break;
			case TailStyle.Long:
				Kit.BoxCentered( root, "Tail", new Vector3( -26f * s, 0, 26f * s ), new Vector3( 30f, 5f, 5f ) * s, primary, Rotation.From( -20f, 0, 0 ) );
				Kit.Sphere( root, "TailTip", new Vector3( -38f * s, 0, 34f * s ), new Vector3( 9f, 9f, 9f ) * s, secondary );
				break;
			case TailStyle.Curl:
				Kit.Sphere( root, "Tail1", new Vector3( -22f * s, 0, 26f * s ), new Vector3( 14f, 10f, 14f ) * s, primary );
				Kit.Sphere( root, "Tail2", new Vector3( -28f * s, 0, 36f * s ), new Vector3( 10f, 8f, 10f ) * s, secondary );
				break;
			case TailStyle.Fin:
				Kit.BoxCentered( root, "Tail", new Vector3( -24f * s, 0, 24f * s ), new Vector3( 8f, 4f, 22f ) * s, secondary, Rotation.From( -15f, 0, 0 ) );
				break;
		}
	}
}
