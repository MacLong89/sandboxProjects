namespace Fauna2;

/// <summary>Stardew-style chibi billboard critter sprites with walk animation.</summary>
public static class CritterSpriteVisual
{
	public static GameObject Build( GameObject parent, AnimalDefinition def, Color tint )
	{
		var stem = def is not null ? Defs.ResourceStem( def.ResourceName ) : "";
		var key = WorldSpriteCatalog.CritterFor( stem );
		var sprite = PixelArt.CritterSprite( key );

		var root = new GameObject( parent, true, "PixelVisual" );
		var tiles = WorldSpriteCatalog.AnimalTileSize( def );
		WorldSprites.Spawn(
			root,
			sprite,
			GameConstants.Tiles( tiles ),
			new Vector3( 0, 0, 2f ),
			$"AnimalSprite_{key}",
			layer: WorldSprites.AnimalLayer,
			dynamicDepthSort: true,
			sourcePixels: PixelArt.IsSuppliedCritter( key ) ? PixelArt.SuppliedSpriteSourcePixels : PixelArt.SpriteSourcePixels,
			movementRoot: parent,
			walkAnimator: true,
			flipFacingHorizontal: true );
		Log.Info( $"[Fauna2 Scale] Animal sprite '{key}' size={tiles:0.##} tiles for definition='{def?.ResourceName ?? "unknown"}'." );

		return root;
	}
}
