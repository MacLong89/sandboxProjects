namespace Deep;

/// <summary>
/// Bottom-of-ocean scenery: reefs, ruins, cave mouths — planted on the seafloor.
/// Mid-water stays open for free swimming (Dave the Diver style).
/// </summary>
public sealed class ZoneBackdrops : Component
{
	protected override void OnStart()
	{
		var bed = SeabedTerrain.Instance;
		if ( bed is null )
			return;

		var balance = DeepGame.Instance?.Balance ?? BalanceConfig.Defaults;
		var y = balance.BackdropY + 2.4f;

		// Left reef garden — dense life on the hills.
		Plant( DeepPixelArt.CoralCluster(), "Coral_L1", bed, -40f, 7.2f, y );
		Plant( DeepPixelArt.CoralCluster(), "Coral_L2", bed, -34f, 6.4f, y + 0.2f );
		Plant( DeepPixelArt.CoralCluster(), "Coral_L3", bed, -28f, 5.8f, y );
		Plant( DeepPixelArt.Seaweed(), "Kelp_L1", bed, -38f, 10f, y + 0.3f );
		Plant( DeepPixelArt.Seaweed(), "Kelp_L2", bed, -31f, 9.2f, y + 0.15f );
		Plant( DeepPixelArt.Seaweed(), "Kelp_L3", bed, -24f, 8.4f, y + 0.25f );
		Plant( DeepPixelArt.Rocks(), "Rocks_L1", bed, -42f, 11f, y - 0.4f );
		Plant( DeepPixelArt.Rocks(), "Rocks_L2", bed, -20f, 9.8f, y - 0.2f );

		// Center ruins + cave mouth under the boat column.
		Plant( DeepPixelArt.Ruins(), "Ruins_C1", bed, -8f, 13.5f, y );
		Plant( DeepPixelArt.Ruins(), "Ruins_C2", bed, 3f, 12.2f, y + 0.15f );
		Plant( DeepPixelArt.CaveOverhang(), "Cave_C", bed, -1f, 15.5f, y - 0.3f );
		Plant( DeepPixelArt.CoralCluster(), "Coral_C1", bed, 7f, 6.6f, y );
		Plant( DeepPixelArt.CoralCluster(), "Coral_C2", bed, -12f, 5.9f, y + 0.1f );
		Plant( DeepPixelArt.Seaweed(), "Kelp_C1", bed, -4f, 8f, y + 0.2f );
		Plant( DeepPixelArt.Seaweed(), "Kelp_C2", bed, 10f, 8.8f, y + 0.25f );

		// Right rocky trench edge.
		Plant( DeepPixelArt.CaveOverhang(), "Cave_R", bed, 30f, 14.5f, y - 0.25f );
		Plant( DeepPixelArt.Rocks(), "Rocks_R1", bed, 22f, 10.5f, y - 0.15f );
		Plant( DeepPixelArt.Rocks(), "Rocks_R2", bed, 36f, 12f, y - 0.35f );
		Plant( DeepPixelArt.Rocks(), "Rocks_R3", bed, 42f, 9.5f, y - 0.1f );
		Plant( DeepPixelArt.Seaweed(), "Kelp_R1", bed, 18f, 8.5f, y + 0.2f );
		Plant( DeepPixelArt.Seaweed(), "Kelp_R2", bed, 26f, 9.1f, y + 0.15f );
		Plant( DeepPixelArt.CoralCluster(), "Coral_R1", bed, 14f, 6.9f, y );
		Plant( DeepPixelArt.CoralCluster(), "Coral_R2", bed, 33f, 6.2f, y + 0.1f );

		// Scattered mid-span ground dressings so empty sand never reads as bare.
		Plant( DeepPixelArt.Seaweed(), "Kelp_M1", bed, -16f, 7.2f, y + 0.2f );
		Plant( DeepPixelArt.CoralCluster(), "Coral_M1", bed, 0f, 5.5f, y );
		Plant( DeepPixelArt.Rocks(), "Rocks_M1", bed, 16f, 8.8f, y - 0.2f );
	}

	private void Plant( Texture texture, string name, SeabedTerrain bed, float x, float worldHeight, float yLayer )
	{
		if ( texture is null || !texture.IsValid() || texture == Texture.White )
		{
			Log.Warning( $"[DEEP] Missing scenery texture for '{name}'." );
			return;
		}

		var root = new GameObject( GameObject, true, name );
		root.WorldPosition = bed.GroundSprite( x, worldHeight, yLayer );
		DeepSprites.SpawnTexture( root, texture, worldHeight, name: "Sprite" );
	}
}
