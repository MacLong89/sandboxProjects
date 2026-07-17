namespace SceneLab;

/// <summary>Unique furniture dressings for each house archetype (kit boxes only).</summary>
public static class HouseInteriorPiece
{
	private static float Floor( float wallBase ) => wallBase + HouseShell.FloorT;

	public static void DressCottage( GameObject root, float cx, float cy, float wallBase, Color accent )
	{
		var z = Floor( wallBase );
		KitBox.CollidingBox( root, "Hearth",
			new Vector3( cx - 40f, cy + 50f, z + 28f ),
			new Vector3( 36f, 48f, 56f ),
			Palette.HouseBrick );
		KitBox.Box( root, "Rug",
			new Vector3( cx + 10f, cy, z + 1f ),
			new Vector3( 90f, 70f, 2f ),
			accent );
		KitBox.CollidingBox( root, "Table",
			new Vector3( cx + 20f, cy - 10f, z + 18f ),
			new Vector3( 50f, 36f, 6f ),
			Palette.Wood );
		foreach ( var sy in new[] { -22f, 22f } )
		{
			KitBox.CollidingBox( root, "Chair",
				new Vector3( cx + 20f, cy + sy, z + 14f ),
				new Vector3( 18f, 18f, 26f ),
				Palette.WoodDark );
		}
	}

	public static void DressRanch( GameObject root, float cx, float cy, float wallBase, Color accent )
	{
		var z = Floor( wallBase );
		KitBox.CollidingBox( root, "Sofa",
			new Vector3( cx - 10f, cy + 35f, z + 16f ),
			new Vector3( 40f, 100f, 30f ),
			Palette.Cushion );
		KitBox.Box( root, "Coffee",
			new Vector3( cx + 25f, cy + 35f, z + 12f ),
			new Vector3( 28f, 50f, 4f ),
			Palette.Wood );
		KitBox.CollidingBox( root, "Island",
			new Vector3( cx + 30f, cy - 45f, z + 18f ),
			new Vector3( 70f, 36f, 34f ),
			KitBox.Solid( accent, 1.1f ) );
		KitBox.Box( root, "TV",
			new Vector3( cx - 55f, cy + 35f, z + 40f ),
			new Vector3( 8f, 64f, 40f ),
			Palette.MetalDark );
	}

	public static void DressColonial(
		GameObject root,
		float cx,
		float cy,
		float wallBase,
		float upperBase,
		float landingZ,
		float wellX,
		float wellY,
		float wellD,
		float wellW,
		Color accent )
	{
		var z = Floor( wallBase );
		KitBox.Box( root, "Runner",
			new Vector3( cx + 35f, cy, z + 1f ),
			new Vector3( 70f, 32f, 2f ),
			accent );
		KitBox.CollidingBox( root, "Sideboard",
			new Vector3( cx + 45f, cy - 70f, z + 20f ),
			new Vector3( 28f, 55f, 40f ),
			Palette.WoodDark );
		KitBox.CollidingBox( root, "EntryTable",
			new Vector3( cx + 50f, cy + 70f, z + 16f ),
			new Vector3( 32f, 22f, 28f ),
			Palette.Wood );

		var bedX = wellX + wellD * 0.5f + 55f;
		KitBox.CollidingBox( root, "Bed",
			new Vector3( bedX, cy + 35f, landingZ + 13f ),
			new Vector3( 60f, 80f, 26f ),
			new Color( 0.75f, 0.78f, 0.85f ) );
		KitBox.CollidingBox( root, "Dresser",
			new Vector3( bedX + 10f, cy - 70f, landingZ + 20f ),
			new Vector3( 28f, 50f, 40f ),
			Palette.WoodDark );

		KitBox.Box( root, "Rail",
			new Vector3( wellX, wellY + wellW * 0.45f, landingZ + 22f ),
			new Vector3( wellD * 0.85f, 4f, 32f ),
			Palette.WoodDark );
	}

	public static void DressLBungalow( GameObject root, float mainX, float mainCy, float wingX, float wingY, float wallBase, Color accent )
	{
		var z = Floor( wallBase );
		KitBox.CollidingBox( root, "Sofa",
			new Vector3( mainX + 15f, mainCy - 30f, z + 16f ),
			new Vector3( 70f, 32f, 30f ),
			Palette.Cushion );
		KitBox.Box( root, "LivingRug",
			new Vector3( mainX + 10f, mainCy, z + 1f ),
			new Vector3( 80f, 70f, 2f ),
			accent );
		KitBox.CollidingBox( root, "Bed",
			new Vector3( wingX, wingY, z + 14f ),
			new Vector3( 70f, 76f, 24f ),
			new Color( 0.85f, 0.80f, 0.72f ) );
		KitBox.CollidingBox( root, "Nightstand",
			new Vector3( wingX + 42f, wingY + 38f, z + 14f ),
			new Vector3( 22f, 22f, 26f ),
			Palette.Wood );
	}

	public static void DressCraftsman( GameObject root, float mainX, float mainCy, float wingX, float wingY, float wallBase, Color accent )
	{
		var z = Floor( wallBase );
		foreach ( var sy in new[] { -48f, 48f } )
		{
			KitBox.CollidingBox( root, "Bookcase",
				new Vector3( mainX - 50f, mainCy + sy, z + 40f ),
				new Vector3( 16f, 38f, 78f ),
				accent );
		}

		KitBox.CollidingBox( root, "Dining",
			new Vector3( mainX + 20f, mainCy, z + 18f ),
			new Vector3( 70f, 40f, 6f ),
			Palette.Wood );
		foreach ( var o in new[] { (-28f, -18f), (-28f, 18f), (28f, -18f), (28f, 18f) } )
		{
			KitBox.CollidingBox( root, "DineChair",
				new Vector3( mainX + 20f + o.Item1 * 0.35f, mainCy + o.Item2, z + 14f ),
				new Vector3( 16f, 16f, 26f ),
				Palette.WoodDark );
		}

		KitBox.CollidingBox( root, "DenSofa",
			new Vector3( wingX, wingY, z + 16f ),
			new Vector3( 48f, 68f, 30f ),
			new Color( 0.40f, 0.45f, 0.38f ) );
	}
}
