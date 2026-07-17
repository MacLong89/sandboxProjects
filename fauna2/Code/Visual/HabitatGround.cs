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
/// Current approach: one flat XY floor quad sized exactly to the interior, depth
/// anchored in HabitatGroundLayer (behind enrichment/animals). Surrounding OwnedTiles
/// stay enabled. Search "HABITAT GROUND FIX" / "LAYER / PRIORITY" for related code.
/// </summary>
public static class HabitatGround
{
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

		// Interior = area inside the fence rails (matches HabitatSizing / Small=512).
		// Exact interior size — TileCoverage oversize bled into the fence ring and
		// compounded depth-clip of enrichments near walls.
		var interior = HabitatSizing.InteriorSize( footprint );
		var draw = interior;

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

		Log.Info(
			$"[Fauna2 HabitatGround] pad biome={biome} footprint={footprint.x:0}x{footprint.y:0} " +
			$"interior={interior.x:0}x{interior.y:0} center=({habitatWorld.x:0},{habitatWorld.y:0}) " +
			$"tiles=1 flatFloor=True groundBand=True alpha={alpha:0.##} " +
			$"tex={GroundDiagnostics.TextureKey( texture )}" );
	}
}
