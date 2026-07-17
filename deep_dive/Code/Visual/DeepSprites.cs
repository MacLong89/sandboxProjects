namespace DeepDive;

/// <summary>Spawns side-view SpriteRenderer visuals for DEEP (fauna2-style, slim).</summary>
public static class DeepDiveSprites
{
	public static SpriteRenderer Spawn(
		GameObject parent,
		Sprite sprite,
		float worldHeight,
		Vector3 localPosition = default,
		string name = "Sprite",
		string startingAnimation = null,
		Texture scaleTexture = null )
	{
		var go = new GameObject( parent, true, name );
		go.LocalPosition = localPosition;

		var renderer = go.AddComponent<SpriteRenderer>();
		Configure( renderer );
		renderer.Sprite = sprite;

		var start = startingAnimation
			?? (sprite?.Animations?.Any( a => a.Name == DeepDivePixelArt.IdleAnimation ) == true
				? DeepDivePixelArt.IdleAnimation
				: DeepDivePixelArt.DefaultAnimation);
		renderer.StartingAnimationName = start;

		var tex = scaleTexture
			?? sprite?.Animations?.FirstOrDefault()?.Frames?.FirstOrDefault()?.Texture;
		DeepDivePixelArt.ApplyWorldScale( renderer, worldHeight, tex );

		return renderer;
	}

	public static SpriteRenderer SpawnTexture(
		GameObject parent,
		Texture texture,
		float worldHeight,
		Vector3 localPosition = default,
		string name = "Sprite" )
	{
		return Spawn( parent, DeepDivePixelArt.MakeSprite( texture ), worldHeight, localPosition, name, scaleTexture: texture );
	}

	public static SpriteRenderer SpawnDiver( GameObject parent )
	{
		return Spawn(
			parent,
			DeepDivePixelArt.DiverSprite(),
			GameConstants.DiverSpriteWorldHeight,
			// Pull slightly toward the camera (+Y) so the diver stays in front of scenery.
			new Vector3( 0f, 0.6f, 0.2f ),
			"DiverSprite",
			DeepDivePixelArt.IdleAnimation,
			DeepDivePixelArt.DiverIdle() );
	}

	public static SpriteRenderer SpawnBoat( GameObject parent )
	{
		var go = new GameObject( parent, true, "BoatSprite" );
		go.LocalPosition = new Vector3( 0f, 0.8f, 1.2f );

		var renderer = go.AddComponent<SpriteRenderer>();
		Configure( renderer );
		var tex = DeepDivePixelArt.Boat();
		renderer.Sprite = DeepDivePixelArt.MakeSprite( tex );
		renderer.StartingAnimationName = DeepDivePixelArt.DefaultAnimation;
		DeepDivePixelArt.ApplyWorldWidth( renderer, GameConstants.BoatSpriteWorldWidth, tex );
		return renderer;
	}

	private static void Configure( SpriteRenderer renderer )
	{
		renderer.TextureFilter = Sandbox.Rendering.FilterMode.Point;
		renderer.Lighting = false;
		renderer.FogStrength = 0f;
		renderer.Billboard = SpriteRenderer.BillboardMode.Always;
		renderer.IsSorted = true;
		// Keep opaque so character/boat sort correctly in front of the water plates.
		// Transparent mattes still punch out via AlphaCutoff.
		renderer.Opaque = true;
		renderer.AlphaCutoff = 0.08f;
	}
}
