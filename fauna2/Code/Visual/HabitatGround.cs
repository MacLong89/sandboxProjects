namespace Fauna2;

/// <summary>
/// Habitat biome floor — fills the enclosure interior.
///
/// IMPORTANT (bug history / HABITAT GROUND FIX 2026-07):
/// Previously stamped many 64u Always-billboards (one per build cell). Under the pitched
/// camera those painted thin horizontal bands; OwnedTile grass (512u) showed through.
///
/// OwnedTile DISABLE / punch-out was tried next — that removed entire 512u grass tiles
/// and left a large camera-clear "hole" around/between pens (dark blue void). Do NOT
/// reintroduce grass punch-out.
///
/// Current approach: one flat XY pad sized to the interior (slight underlap into the fence
/// ring), depth-anchored in HabitatGroundLayer. Surrounding OwnedTiles stay enabled.
/// Search "HABITAT GROUND FIX" / "LAYER / PRIORITY" for related code.
/// </summary>
public static class HabitatGround
{
	/// <summary>
	/// How far the floor tucks under the fence rails (each side). Enough to hide grass
	/// at the rail line without spilling outside the enclosure.
	/// </summary>
	private static float FenceUnderlap => GameConstants.TileSize * 0.4f;

	/// <summary>
	/// Attach biome floor under <paramref name="parent"/>.
	/// Ghosts pass alpha &lt; 1; placed habitats use full opacity.
	/// </summary>
	public static void Attach(
		GameObject parent,
		Vector2 footprint,
		Biome biome,
		float alpha = 1f )
	{
		if ( parent is null || footprint.x <= 0f || footprint.y <= 0f )
			return;

		footprint = HabitatSizing.EffectiveFootprint( footprint );
		var texture = WildernessBiomeMap.GroundTile( biome );
		if ( !texture.IsValid() )
		{
			Log.Warning( $"[Fauna2 HabitatGround] missing biome texture for {biome}" );
			return;
		}

		var habitatRoot = parent.Parent.IsValid() ? parent.Parent : parent;
		var habitatWorld = habitatRoot.WorldPosition.WithZ( 0f );

		var interior = HabitatSizing.InteriorSize( footprint );
		var underlap = FenceUnderlap * 2f;
		var draw = new Vector2(
			MathF.Min( footprint.x, interior.x + underlap ),
			MathF.Min( footprint.y, interior.y + underlap ) );

		var groundRoot = new GameObject( parent, true, "Ground" );
		var pad = WorldSprites.SpawnHabitatGroundTile(
			groundRoot,
			texture,
			draw,
			Vector3.Zero,
			habitatRoot,
			"HabitatGroundPad" );

		if ( alpha < 0.99f )
		{
			var renderer = WorldSprites.GetGroundSpriteRenderer( pad );
			if ( renderer.IsValid() )
				renderer.Color = renderer.Color.WithAlpha( alpha );
		}

		Fauna2Debug.Info(
			"HabitatGround",
			$"pad biome={biome} footprint={footprint.x:0}x{footprint.y:0} " +
			$"interior={interior.x:0}x{interior.y:0} draw={draw.x:0}x{draw.y:0} " +
			$"center=({habitatWorld.x:0},{habitatWorld.y:0}) " +
			$"flatFloor=True underlap={FenceUnderlap:0.#} groundBand=True alpha={alpha:0.##} " +
			$"tex={GroundDiagnostics.TextureKey( texture )}" );
	}
}
