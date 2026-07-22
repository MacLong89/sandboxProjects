namespace Offshore;

/// <summary>
/// Isolated look-dev for the fixed backdrop + scrolling world layout.
/// Open scenes/sky_lab.scene — main offshore.scene is untouched.
///
/// Camera contract (permanent):
/// - Sky + water are screen-locked backdrops (always this split: sky upper, ocean lower).
/// - Dock / boat / props scroll with the world — walk right and the dock leaves the frame.
/// - Player walks the dock or boards the boat; sprite swaps when aboard vs on foot.
/// </summary>
public sealed class SkyLabController : Component
{
	/// <summary>Full day = 10 real seconds → 1440 game minutes / 10s.</summary>
	public const float LabDaySeconds = 10f;
	public const float LabTimeScale = (24f * 60f) / LabDaySeconds; // 144

	public TimeOfDayService Clock { get; } = new();
	public WeatherService Weather { get; } = new();

	readonly List<GameObject> _owned = new();
	SkyBackdrop _sky;
	WaterBackdrop _water;
	SpriteActor _shopDock;
	CameraComponent _camera;

	// Combined bait shop + pier — centered on spawn screen (walkway on mid waterline).
	const float ShopDockHeight = 320f;
	const float ShopDockX = 0f;
	// shop_dock PNG: walkway ~38% from top → center = 0 - height*(0.5-0.38)
	const float ShopDockCenterZ = -ShopDockHeight * 0.12f;
	const float ShopDockY = 22f; // behind water so waves layer over the pilings

	public bool HudBuilt { get; private set; }
	public string StatusLine { get; private set; } = "";

	protected override void OnStart()
	{
		Clock.SetMinuteOfDay( 6f * 60f ); // start at dawn so a full cycle is obvious
		Clock.TimeScale = LabTimeScale;
		Clock.Paused = false;
		EnsureCamera();
		BuildLab();
		EnsureHud();
		Log.Info( $"[SKY LAB] Full day = {LabDaySeconds}s. Sky + water + shop/dock. A/D scrub, Space pause, 1-4 phases." );
	}

	protected override void OnUpdate()
	{
		EnsureCamera();
		EnsureHud();
		HandleInput();

		// Keep lab locked to a 10s day unless user changed speed with 5/6
		Clock.Tick( Time.Delta );
		Weather.Tick( Time.Delta, 0f, Clock.Phase );
		_sky?.Update( Time.Delta, 0f, Clock, Weather );
		_water?.Update( Time.Delta, Clock, Weather );

		if ( _camera is not null && _camera.IsValid() )
			_camera.BackgroundColor = Color.Lerp( Clock.SkyTop, Clock.SkyHorizon, 0.5f );

		var cycleLeft = Clock.Paused ? "PAUSED" : $"~{LabDaySeconds:0}s/day";
		StatusLine = $"Day {Clock.Day}  {Clock.ClockText}  ·  {Clock.Phase}  ·  sun {Clock.SunAltitude:0.00}  ·  stars {Clock.StarVisibility:0.00}  ·  {cycleLeft}  ·  {Clock.TimeScale:0}x";
	}

	protected override void OnDestroy()
	{
		foreach ( var go in _owned )
		{
			if ( go.IsValid() )
				go.Destroy();
		}
		_owned.Clear();
	}

	void HandleInput()
	{
		if ( Down( "Left" ) || Key( "A" ) )
			Clock.AddMinutes( -Time.Delta * 180f );
		if ( Down( "Right" ) || Key( "D" ) )
			Clock.AddMinutes( Time.Delta * 180f );

		if ( Pressed( "Pause" ) || KeyPressed( "SPACE" ) )
			Clock.Paused = !Clock.Paused;

		if ( KeyPressed( "1" ) ) Clock.SetMinuteOfDay( 6.2f * 60f );
		if ( KeyPressed( "2" ) ) Clock.SetMinuteOfDay( 12f * 60f );
		if ( KeyPressed( "3" ) ) Clock.SetMinuteOfDay( 18f * 60f );
		if ( KeyPressed( "4" ) ) Clock.SetMinuteOfDay( 22f * 60f );

		// Reset to 10s/day, or nudge speed
		if ( KeyPressed( "5" ) ) Clock.TimeScale = Math.Max( 20f, Clock.TimeScale * 0.5f );
		if ( KeyPressed( "6" ) ) Clock.TimeScale = Math.Min( 600f, Clock.TimeScale * 2f );
		if ( KeyPressed( "0" ) ) Clock.TimeScale = LabTimeScale;
	}

	static bool Down( string action )
	{
		try { return Input.Down( action ); }
		catch { return false; }
	}

	static bool Pressed( string action )
	{
		try { return Input.Pressed( action ); }
		catch { return false; }
	}

	static bool Key( string key ) => Input.Keyboard.Down( key );
	static bool KeyPressed( string key ) => Input.Keyboard.Pressed( key );

	void BuildLab()
	{
		foreach ( var go in _owned )
		{
			if ( go.IsValid() )
				go.Destroy();
		}
		_owned.Clear();

		// Sky first (farther Y), then water overlay, then shop/dock in front.
		_sky = new SkyBackdrop();
		_sky.Build( Scene, _owned, SkyBackdrop.LayoutMode.Lab );

		_water = new WaterBackdrop();
		_water.Build( Scene, _owned );

		_shopDock = SpawnFit( "ShopDock", "env/shop_dock", ShopDockHeight, ShopDockX, ShopDockCenterZ, ShopDockY );
	}

	SpriteActor SpawnFit( string name, string path, float height, float x, float z, float y )
	{
		var go = Scene.CreateObject();
		go.Name = name;
		go.WorldPosition = new Vector3( x, y, z );
		_owned.Add( go );
		var actor = go.AddComponent<SpriteActor>();
		actor.SetFitHeight( path, height );
		return actor;
	}

	void EnsureCamera()
	{
		if ( _camera is not null && _camera.IsValid() )
		{
			BindHudCamera();
			return;
		}

		_camera = Components.Get<CameraComponent>() ?? Scene.GetAllComponents<CameraComponent>().FirstOrDefault();
		if ( _camera is null || !_camera.IsValid() )
		{
			var camGo = Scene.CreateObject();
			camGo.Name = "SkyLabCamera";
			_camera = camGo.Components.Create<CameraComponent>();
		}

		_camera.Orthographic = true;
		_camera.OrthographicHeight = 560;
		_camera.IsMainCamera = true;
		_camera.ZNear = 1f;
		_camera.ZFar = 10000f;
		_camera.WorldPosition = new Vector3( 0, -1000, WorldPresenter.CameraZ );
		_camera.WorldRotation = new Rotation( 0, 0, 0.7071068f, 0.7071068f );
		BindHudCamera();
	}

	void BindHudCamera()
	{
		foreach ( var screen in Scene.GetAllComponents<ScreenPanel>() )
		{
			if ( screen.IsValid() && _camera.IsValid() )
				screen.TargetCamera = _camera;
		}
	}

	void EnsureHud()
	{
		if ( HudBuilt ) return;
		var existing = Scene.GetAllComponents<SkyLabHud>().FirstOrDefault();
		if ( existing is not null && existing.IsValid() )
		{
			HudBuilt = true;
			return;
		}

		var panel = Components.Get<ScreenPanel>();
		if ( panel is null || !panel.IsValid() )
		{
			panel = Components.Create<ScreenPanel>();
			panel.ZIndex = 100;
		}
		if ( _camera.IsValid() )
			panel.TargetCamera = _camera;

		Components.Create<SkyLabHud>();
		HudBuilt = true;
	}
}
