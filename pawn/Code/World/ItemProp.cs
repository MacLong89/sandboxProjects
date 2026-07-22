namespace PawnShop;

/// <summary>
/// Builds a chunky low-poly prop for an item instance out of primitives.
/// Each category has a real silhouette (3-8 parts); the item id seeds small
/// per-instance variation so two of the same item don't look cloned.
/// </summary>
public static class ItemProp
{
	private static readonly Color Dark = new( 0.16f, 0.15f, 0.17f );
	private static readonly Color Metal = new( 0.62f, 0.64f, 0.68f );
	private static readonly Color WoodDark = new( 0.32f, 0.2f, 0.11f );

	/// <summary>Spawn the prop under a root object. Root's position = base of the item.</summary>
	public static GameObject Build( GameObject parent, ItemInstance item, string name = "ItemProp" )
	{
		var root = new GameObject( parent, true, name );
		var def = item.Def;

		var tint = def?.Tint ?? Color.Magenta;    // magenta = missing definition, on purpose
		if ( item.Dirtiness > 0.05f )
			tint = Color.Lerp( tint, new Color( 0.28f, 0.24f, 0.18f ), item.Dirtiness * 0.55f );

		// Deterministic per-instance wobble: slight yaw + scale so shelves look hand-stocked.
		var seed = item.Id * 2654435761u;
		float Vary( float lo, float hi, int salt ) =>
			lo + (hi - lo) * (((seed >> (salt * 5)) & 1023u) / 1023f);

		root.LocalRotation = Rotation.FromYaw( Vary( -14f, 14f, 1 ) );
		var s = Vary( 0.92f, 1.1f, 2 );
		root.LocalScale = new Vector3( s, s, s );

		// Soft display plinth so every item sits on something instead of floating.
		MeshKit.Spawn( root, "Plinth", new Vector3( 0, 0, 1.2f ), new Vector3( Vary( 16f, 22f, 3 ), Vary( 14f, 20f, 4 ), 2.4f ), new Color( 0.22f, 0.18f, 0.14f ) );
		MeshKit.Spawn( root, "PlinthPad", new Vector3( 0, 0, 2.6f ), new Vector3( Vary( 12f, 16f, 5 ), Vary( 10f, 14f, 6 ), 1f ), new Color( 0.32f, 0.16f, 0.14f ) );

		switch ( def?.Category )
		{
			case ItemCategory.Jewelry:
				// Velvet display bust with the piece on top.
				MeshKit.Spawn( root, "Bust", new Vector3( 0, 0, 5 ), new Vector3( 18, 18, 10 ), new Color( 0.22f, 0.1f, 0.16f ) );
				MeshKit.Spawn( root, "BustTop", new Vector3( 0, 0, 11 ), new Vector3( 14, 14, 3 ), new Color( 0.3f, 0.14f, 0.22f ) );
				MeshKit.Spawn( root, "Band", new Vector3( 0, 0, 15 ), new Vector3( 10, 10, 4 ), tint );
				MeshKit.SpawnSphere( root, "Gem", new Vector3( 0, 0, 20 ), 8f, Color.Lerp( tint, Color.White, 0.35f ) );
				MeshKit.SpawnSphere( root, "GemL", new Vector3( -5, 0, 16 ), 4f, tint );
				MeshKit.SpawnSphere( root, "GemR", new Vector3( 5, 0, 16 ), 4f, tint );
				break;

			case ItemCategory.Watches:
				// Watch on an upright display stand: strap loop + round face + crown.
				MeshKit.Spawn( root, "Stand", new Vector3( 0, 0, 3 ), new Vector3( 14, 12, 6 ), Dark );
				MeshKit.Spawn( root, "StrapUp", new Vector3( 0, 0, 15 ), new Vector3( 5, 8, 20 ), Color.Lerp( tint, Dark, 0.55f ) );
				MeshKit.SpawnSphere( root, "Face", new Vector3( 0, -4, 18 ), 13f, tint );
				MeshKit.SpawnSphere( root, "Dial", new Vector3( 0, -6.4f, 18 ), 8f, new Color( 0.93f, 0.92f, 0.86f ) );
				MeshKit.Spawn( root, "Crown", new Vector3( 7, -4, 18 ), new Vector3( 3, 3, 3 ), Metal );
				break;

			case ItemCategory.Electronics:
				// Open-lid laptop silhouette (reads as "device" for the whole category).
				MeshKit.Spawn( root, "Base", new Vector3( 2, 0, 2.5f ), new Vector3( 22, 26, 4 ), tint );
				MeshKit.Spawn( root, "Keys", new Vector3( 2, 0, 5f ), new Vector3( 17, 20, 1.2f ), Color.Lerp( tint, Dark, 0.4f ) );
				MeshKit.Spawn( root, "Lid", new Vector3( -8, 0, 12 ), new Vector3( 3, 26, 20 ), tint, new Angles( -14, 0, 0 ) );
				MeshKit.Spawn( root, "Screen", new Vector3( -6.4f, 0, 12.4f ), new Vector3( 1.6f, 21, 15 ), new Color( 0.1f, 0.16f, 0.24f ), new Angles( -14, 0, 0 ) );
				break;

			case ItemCategory.Instruments:
				// Upright guitar: lower bout, upper bout, neck, headstock, strings.
				MeshKit.Spawn( root, "BoutLow", new Vector3( 0, 0, 9 ), new Vector3( 10, 24, 18 ), tint );
				MeshKit.Spawn( root, "BoutHigh", new Vector3( 0, 0, 21 ), new Vector3( 9, 18, 10 ), tint );
				MeshKit.Spawn( root, "SoundHole", new Vector3( -5.2f, 0, 14 ), new Vector3( 1.5f, 8, 8 ), Dark );
				MeshKit.Spawn( root, "Neck", new Vector3( 0, 0, 34 ), new Vector3( 4, 4.5f, 20 ), WoodDark );
				MeshKit.Spawn( root, "Head", new Vector3( 0, 0, 46 ), new Vector3( 5, 7, 6 ), Color.Lerp( tint, Dark, 0.3f ) );
				MeshKit.Spawn( root, "Strings", new Vector3( -2.6f, 0, 24 ), new Vector3( 0.6f, 2.4f, 34 ), new Color( 0.85f, 0.83f, 0.72f ) );
				break;

			case ItemCategory.Tools:
				// Power drill: body, grip, chuck, trigger.
				MeshKit.Spawn( root, "Body", new Vector3( 0, 2, 16 ), new Vector3( 10, 22, 10 ), tint );
				MeshKit.Spawn( root, "Chuck", new Vector3( 0, -12, 16 ), new Vector3( 6, 8, 6 ), Metal );
				MeshKit.Spawn( root, "Bit", new Vector3( 0, -18, 16 ), new Vector3( 2, 6, 2 ), Dark );
				MeshKit.Spawn( root, "Grip", new Vector3( 0, 8, 6 ), new Vector3( 8, 7, 14 ), Color.Lerp( tint, Dark, 0.5f ) );
				MeshKit.Spawn( root, "Battery", new Vector3( 0, 8, 0.5f ), new Vector3( 10, 10, 5 ), Dark );
				break;

			case ItemCategory.Sports:
				// Ball on a little ring stand, with a seam stripe.
				MeshKit.Spawn( root, "Ring", new Vector3( 0, 0, 2.5f ), new Vector3( 14, 14, 5 ), Dark );
				MeshKit.SpawnSphere( root, "Ball", new Vector3( 0, 0, 15 ), 21f, tint );
				MeshKit.Spawn( root, "Seam", new Vector3( 0, 0, 15 ), new Vector3( 22, 1.6f, 1.6f ), Color.Lerp( tint, Color.White, 0.5f ) );
				break;

			case ItemCategory.Collectibles:
				// Collector box with window and a little figure inside.
				MeshKit.Spawn( root, "Box", new Vector3( 0, 0, 13 ), new Vector3( 16, 12, 26 ), tint );
				MeshKit.Spawn( root, "Window", new Vector3( 0, -6.4f, 14 ), new Vector3( 11, 1.4f, 16 ), new Color( 0.75f, 0.88f, 0.95f, 0.8f ) );
				MeshKit.Spawn( root, "FigBody", new Vector3( 0, -4.5f, 11 ), new Vector3( 5, 3, 8 ), Color.Lerp( tint, Color.White, 0.45f ) );
				MeshKit.SpawnSphere( root, "FigHead", new Vector3( 0, -4.5f, 17 ), 4.5f, new Color( 0.95f, 0.8f, 0.66f ) );
				MeshKit.Spawn( root, "TopFlap", new Vector3( 0, 0, 27 ), new Vector3( 17, 13, 2 ), Color.Lerp( tint, Dark, 0.35f ) );
				break;

			case ItemCategory.Art:
				// Canvas on a leaning easel.
				MeshKit.Spawn( root, "LegL", new Vector3( 3, -10, 14 ), new Vector3( 2, 2.5f, 30 ), WoodDark, new Angles( 8, 0, -8 ) );
				MeshKit.Spawn( root, "LegR", new Vector3( 3, 10, 14 ), new Vector3( 2, 2.5f, 30 ), WoodDark, new Angles( 8, 0, 8 ) );
				MeshKit.Spawn( root, "LegBack", new Vector3( -6, 0, 14 ), new Vector3( 2, 2.5f, 28 ), WoodDark, new Angles( -14, 0, 0 ) );
				MeshKit.Spawn( root, "Frame", new Vector3( 2, 0, 20 ), new Vector3( 3, 24, 28 ), new Color( 0.62f, 0.48f, 0.22f ), new Angles( 8, 0, 0 ) );
				MeshKit.Spawn( root, "Canvas", new Vector3( 3.6f, 0, 20 ), new Vector3( 1.6f, 19, 23 ), tint, new Angles( 8, 0, 0 ) );
				MeshKit.Spawn( root, "Paint", new Vector3( 4.6f, -3, 22 ), new Vector3( 0.8f, 8, 6 ), Color.Lerp( tint, Color.White, 0.4f ), new Angles( 8, 0, 0 ) );
				break;

			case ItemCategory.Antiques:
				// Mantel clock: body, arched top, face, feet.
				MeshKit.Spawn( root, "FootL", new Vector3( 0, -8, 1.5f ), new Vector3( 6, 4, 3 ), WoodDark );
				MeshKit.Spawn( root, "FootR", new Vector3( 0, 8, 1.5f ), new Vector3( 6, 4, 3 ), WoodDark );
				MeshKit.Spawn( root, "Body", new Vector3( 0, 0, 14 ), new Vector3( 12, 20, 22 ), tint );
				MeshKit.Spawn( root, "Arch", new Vector3( 0, 0, 27 ), new Vector3( 10, 14, 5 ), Color.Lerp( tint, Dark, 0.25f ) );
				MeshKit.SpawnSphere( root, "Face", new Vector3( -6, 0, 16 ), 12f, new Color( 0.94f, 0.9f, 0.8f ) );
				MeshKit.Spawn( root, "Hand", new Vector3( -7.4f, 0, 17 ), new Vector3( 0.8f, 1.2f, 4.5f ), Dark );
				MeshKit.SpawnSphere( root, "Finial", new Vector3( 0, 0, 31 ), 4f, new Color( 0.85f, 0.7f, 0.3f ) );
				break;

			case ItemCategory.Gaming:
				// Console + controller in front.
				MeshKit.Spawn( root, "Console", new Vector3( -2, 0, 4 ), new Vector3( 20, 26, 8 ), tint );
				MeshKit.Spawn( root, "Vent", new Vector3( -2, 0, 8.4f ), new Vector3( 14, 18, 1 ), Color.Lerp( tint, Dark, 0.5f ) );
				MeshKit.Spawn( root, "PowerLight", new Vector3( 6, -10, 6 ), new Vector3( 3, 1.5f, 1.5f ), new Color( 0.2f, 0.9f, 0.5f ) );
				MeshKit.Spawn( root, "Pad", new Vector3( 10, 0, 2.5f ), new Vector3( 8, 12, 5 ), Dark );
				MeshKit.SpawnSphere( root, "StickL", new Vector3( 10, -3, 5.6f ), 3f, Metal );
				MeshKit.SpawnSphere( root, "StickR", new Vector3( 10, 3, 5.6f ), 3f, Metal );
				break;

			case ItemCategory.Cameras:
				// Camera body, big lens, flash block, strap lugs.
				MeshKit.Spawn( root, "Body", new Vector3( 0, 0, 10 ), new Vector3( 12, 22, 14 ), tint );
				MeshKit.Spawn( root, "Grip", new Vector3( 0, 9, 10 ), new Vector3( 13, 5, 14 ), Color.Lerp( tint, Dark, 0.4f ) );
				MeshKit.SpawnSphere( root, "Lens", new Vector3( -8, -2, 10 ), 11f, Dark );
				MeshKit.SpawnSphere( root, "Glass", new Vector3( -12, -2, 10 ), 6f, new Color( 0.35f, 0.5f, 0.7f ) );
				MeshKit.Spawn( root, "Flash", new Vector3( 0, -2, 18.4f ), new Vector3( 8, 7, 3 ), Color.Lerp( tint, Dark, 0.3f ) );
				break;

			case ItemCategory.Appliances:
				// Boxy appliance with a door, handle, and dial.
				MeshKit.Spawn( root, "Body", new Vector3( 0, 0, 13 ), new Vector3( 20, 22, 26 ), tint );
				MeshKit.Spawn( root, "Door", new Vector3( -10.6f, 0, 11 ), new Vector3( 1.4f, 17, 17 ), Color.Lerp( tint, Dark, 0.3f ) );
				MeshKit.Spawn( root, "Handle", new Vector3( -11.8f, -6, 11 ), new Vector3( 1.2f, 2, 12 ), Metal );
				MeshKit.SpawnSphere( root, "Dial", new Vector3( -10.8f, 6, 22 ), 3.5f, Metal );
				MeshKit.Spawn( root, "Vents", new Vector3( 0, 0, 26.4f ), new Vector3( 14, 16, 1 ), Color.Lerp( tint, Dark, 0.5f ) );
				break;

			case ItemCategory.Memorabilia:
				// Framed piece on a table stand with a brass plaque.
				MeshKit.Spawn( root, "Stand", new Vector3( 4, 0, 6 ), new Vector3( 2, 10, 12 ), Dark, new Angles( -18, 0, 0 ) );
				MeshKit.Spawn( root, "Frame", new Vector3( 0, 0, 14 ), new Vector3( 3, 28, 22 ), new Color( 0.25f, 0.22f, 0.2f ), new Angles( 8, 0, 0 ) );
				MeshKit.Spawn( root, "Content", new Vector3( -1.4f, 0, 14 ), new Vector3( 1.6f, 23, 17 ), tint, new Angles( 8, 0, 0 ) );
				MeshKit.Spawn( root, "Plaque", new Vector3( -2.2f, 0, 6.5f ), new Vector3( 1, 10, 3 ), new Color( 0.85f, 0.7f, 0.3f ), new Angles( 8, 0, 0 ) );
				break;

			default:
				// Unknown/missing category: obvious magenta cube (never silent-fail).
				MeshKit.Spawn( root, "Missing", new Vector3( 0, 0, 10 ), new Vector3( 20, 20, 20 ), Color.Magenta );
				break;
		}

		// Rarity glint for Rare+ items.
		if ( item.Rarity >= Rarity.Rare )
			MeshKit.SpawnSphere( root, "Glint", new Vector3( 0, 0, 44 ), 5f, item.Rarity.RarityColor() );

		return root;
	}
}
