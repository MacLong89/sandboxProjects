namespace Offshore;

/// <summary>
/// Kelp, seaweed, rocks, and seabed clusters rising from the bottom of the view.
/// Camera-locked so they always fringe the lower ocean band and sway gently.
/// Drawn slightly in front of the ocean plate so they stay visible through the water.
/// </summary>
public sealed class SeafloorFlora : Component
{
	// Closer than OceanDepth (48) → sits in the water foreground.
	private const float FloraDepth = 46.5f;

	private readonly List<FloraSprite> _items = new();

	private sealed class FloraSprite
	{
		public GameObject Go;
		public SpriteRenderer Sprite;
		public float NormX;      // -1 left … +1 right across the view
		public float HeightFrac; // how tall relative to halfH (reaches up from bottom)
		public float WidthFrac;
		public float Phase;
		public float Sway;
		public float BaseAlpha;
	}

	protected override void OnStart()
	{
		WorldPosition = Vector3.Zero;
		SpawnBank();
	}

	protected override void OnDestroy()
	{
		foreach ( var item in _items )
		{
			if ( item.Go is not null && item.Go.IsValid() )
				item.Go.Destroy();
		}
		_items.Clear();
	}

	protected override void OnUpdate()
	{
		var cam = OffshoreGameController.Instance?.Camera;
		if ( cam is null || !cam.IsValid() || _items.Count == 0 )
			return;

		ScreenAxes.GetViewExtents( cam, FloraDepth, out var halfW, out var halfH );
		var t = Time.Now;

		foreach ( var item in _items )
		{
			if ( item.Go is null || !item.Go.IsValid() || item.Sprite is null )
				continue;

			var sizeH = halfH * item.HeightFrac;
			var sizeW = halfW * item.WidthFrac;
			item.Sprite.Size = new Vector2( sizeW, sizeH );

			// Anchor near the bottom edge; sway a little in X/Y.
			var swayX = MathF.Sin( t * item.Sway + item.Phase ) * halfW * 0.012f;
			var swayY = MathF.Sin( t * (item.Sway * 0.85f) + item.Phase * 1.3f ) * halfH * 0.008f;
			var screenX = item.NormX * halfW * 0.96f + swayX;
			// Bottom of sprite near bottom of screen; plant grows upward.
			var screenY = -halfH + sizeH * 0.48f + swayY;

			item.Go.WorldPosition = ScreenAxes.FromCamera( cam, screenX, screenY, FloraDepth );
			item.Sprite.Color = new Color( 1f, 1f, 1f, item.BaseAlpha );
		}
	}

	private void SpawnBank()
	{
		// Tall kelp strands
		for ( var i = 0; i < 10; i++ )
		{
			Add(
				OffshoreSprites.Paths.Kelp,
				normX: Game.Random.Float( -0.95f, 0.95f ),
				heightFrac: Game.Random.Float( 0.38f, 0.72f ),
				widthFrac: Game.Random.Float( 0.06f, 0.12f ),
				alpha: Game.Random.Float( 0.72f, 0.95f ),
				sway: Game.Random.Float( 0.7f, 1.4f ) );
		}

		// Bushier seaweed clumps
		for ( var i = 0; i < 8; i++ )
		{
			Add(
				OffshoreSprites.Paths.Seaweed,
				normX: Game.Random.Float( -0.92f, 0.92f ),
				heightFrac: Game.Random.Float( 0.28f, 0.52f ),
				widthFrac: Game.Random.Float( 0.08f, 0.16f ),
				alpha: Game.Random.Float( 0.7f, 0.92f ),
				sway: Game.Random.Float( 0.55f, 1.2f ) );
		}

		// Rocky / seabed clusters along the floor
		for ( var i = 0; i < 5; i++ )
		{
			Add(
				OffshoreSprites.Paths.SeabedCluster,
				normX: Game.Random.Float( -0.9f, 0.9f ),
				heightFrac: Game.Random.Float( 0.14f, 0.26f ),
				widthFrac: Game.Random.Float( 0.14f, 0.28f ),
				alpha: Game.Random.Float( 0.85f, 1f ),
				sway: Game.Random.Float( 0.15f, 0.35f ) );
		}

		// A few rocks
		for ( var i = 0; i < 4; i++ )
		{
			Add(
				OffshoreSprites.Paths.Rock,
				normX: Game.Random.Float( -0.88f, 0.88f ),
				heightFrac: Game.Random.Float( 0.1f, 0.2f ),
				widthFrac: Game.Random.Float( 0.07f, 0.13f ),
				alpha: 1f,
				sway: 0.1f );
		}
	}

	private void Add( string path, float normX, float heightFrac, float widthFrac, float alpha, float sway )
	{
		var sprite = OffshoreSprites.Spawn(
			GameObject,
			path,
			new Vector2( 2f, 4f ),
			Vector3.Zero,
			$"Flora_{_items.Count}" );
		sprite.AlphaCutoff = 0.04f;
		sprite.Color = new Color( 1f, 1f, 1f, alpha );

		_items.Add( new FloraSprite
		{
			Go = sprite.GameObject,
			Sprite = sprite,
			NormX = normX,
			HeightFrac = heightFrac,
			WidthFrac = widthFrac,
			Phase = Game.Random.Float( 0f, MathF.PI * 2f ),
			Sway = sway,
			BaseAlpha = alpha,
		} );
	}
}
