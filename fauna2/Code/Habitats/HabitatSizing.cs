namespace Fauna2;

/// <summary>Habitat footprint helpers — fence inset must fit at least one biome ground chunk.</summary>
public static class HabitatSizing
{
	/// <summary>Fence margin on each side (one build tile).</summary>
	public static float FenceMargin => GameConstants.TileSize;

	/// <summary>Interior floor area inside the fence.</summary>
	public static Vector2 InteriorSize( Vector2 footprint )
	{
		var margin = FenceMargin * 2f;
		return new Vector2(
			MathF.Max( 0f, footprint.x - margin ),
			MathF.Max( 0f, footprint.y - margin ) );
	}

	/// <summary>
	/// Expands authored 512/1024 footprints when the fence inset would be smaller than one
	/// wilderness ground chunk (512 world units).
	/// </summary>
	public static Vector2 EffectiveFootprint( Vector2 size )
	{
		var interior = InteriorSize( size );
		var minInterior = GameConstants.GroundRenderTileSize;

		if ( interior.x >= minInterior - 0.5f && interior.y >= minInterior - 0.5f )
			return size;

		return size + Vector2.One * (FenceMargin * 2f);
	}
}
