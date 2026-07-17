namespace DeepDive;

/// <summary>
/// Atmosphere behind the dive: sky above the waterline, a depth-tinted water column below.
/// Underwater darkening is driven by <see cref="DepthZoneVisuals"/> (skybox mute + this fill).
/// </summary>
public sealed class OceanBackdrop : Component
{
	public static OceanBackdrop Instance { get; private set; }

	private GameObject _skyUpper;
	private GameObject _skyNear;
	private GameObject _horizon;
	private GameObject _waterColumn;
	private SpriteRenderer _horizonRenderer;
	private SpriteRenderer _waterRenderer;

	protected override void OnAwake()
	{
		Instance = this;
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}

	protected override void OnStart()
	{
		var balance = DeepDiveGame.Instance?.Balance ?? BalanceConfig.Defaults;
		var y = balance.OceanBackdropY;
		var w = balance.OceanBackdropWidth * 1.5f;
		var surface = balance.SurfaceZ;
		// Tall enough to cover the full dive column under the waterline.
		var waterHeight = balance.MaxOceanDepthMeters + 40f;

		_skyUpper = SpawnPlate( "SkyUpper", w, 52f, new Color( 0.55f, 0.8f, 1f ),
			new Vector3( 0f, y - 2f, surface + 34f ), out _ );

		_skyNear = SpawnPlate( "SkyNearHorizon", w, 20f, new Color( 0.7f, 0.9f, 0.98f ),
			new Vector3( 0f, y - 1.5f, surface + 11f ), out _ );

		_horizon = SpawnPlate( "HorizonBand", w, 4.5f, new Color( 0.78f, 0.94f, 1f ),
			new Vector3( 0f, y - 1f, surface + 0.8f ), out _horizonRenderer );

		// Sits entirely BELOW the surface so sky plates stay visible at the boat.
		_waterColumn = SpawnPlate( "WaterColumn", w * 1.35f, waterHeight, GameConstants.WaterSunlit,
			new Vector3( 0f, y - 4f, surface - waterHeight * 0.5f ), out _waterRenderer );
	}

	private GameObject SpawnPlate( string name, float width, float height, Color tint, Vector3 pos, out SpriteRenderer renderer )
	{
		var root = new GameObject( GameObject, true, name );
		root.WorldPosition = pos;

		var go = new GameObject( root, true, "Sprite" );
		renderer = go.AddComponent<SpriteRenderer>();
		renderer.TextureFilter = Sandbox.Rendering.FilterMode.Bilinear;
		renderer.Lighting = false;
		renderer.FogStrength = 0f;
		renderer.Billboard = SpriteRenderer.BillboardMode.Always;
		renderer.IsSorted = false;
		renderer.Opaque = true;
		renderer.Sprite = DeepDivePixelArt.MakeSprite( Texture.White );
		renderer.StartingAnimationName = DeepDivePixelArt.DefaultAnimation;
		renderer.Size = new Vector2( width, height );
		renderer.Color = tint;
		go.LocalPosition = Vector3.Zero;
		return root;
	}

	protected override void OnUpdate()
	{
		var cam = DeepDiveGame.Instance?.DiveCamera;
		var balance = DeepDiveGame.Instance?.Balance ?? BalanceConfig.Defaults;
		if ( cam is null || !cam.IsValid() )
			return;

		var cx = cam.WorldPosition.x;
		var y = balance.OceanBackdropY;
		var surface = balance.SurfaceZ;
		var waterHeight = balance.MaxOceanDepthMeters + 40f;

		if ( _skyUpper.IsValid() )
			_skyUpper.WorldPosition = new Vector3( cx, y - 2f, surface + 34f );
		if ( _skyNear.IsValid() )
			_skyNear.WorldPosition = new Vector3( cx, y - 1.5f, surface + 11f );
		if ( _horizon.IsValid() )
			_horizon.WorldPosition = new Vector3( cx, y - 1f, surface + 0.8f );
		if ( _waterColumn.IsValid() )
			_waterColumn.WorldPosition = new Vector3( cx, y - 4f, surface - waterHeight * 0.5f );
	}

	/// <summary>DepthZoneVisuals hook — water column + horizon pick up the dive tint.</summary>
	public void ApplyDepthLook( Color waterTint, float brightness, float depthMeters = 0f )
	{
		_ = brightness;

		if ( _waterRenderer.IsValid() )
			_waterRenderer.Color = waterTint;

		if ( !_horizonRenderer.IsValid() )
			return;

		var nearSurface = MathX.Clamp( 1f - depthMeters / 12f, 0f, 1f );
		_horizonRenderer.Color = Color.Lerp( waterTint, new Color( 0.78f, 0.94f, 1f ), nearSurface * 0.85f );
	}
}
