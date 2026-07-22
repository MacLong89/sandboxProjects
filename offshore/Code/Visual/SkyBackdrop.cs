namespace Offshore;

/// <summary>
/// Screen-locked sky backdrop (day plates, sun/moon, clouds). Does not scroll with the player.
/// Pair with <see cref="WaterBackdrop"/> for the permanent upper/lower split.
/// </summary>
public sealed class SkyBackdrop
{
	public enum LayoutMode
	{
		Game,
		Lab
	}

	readonly List<Cloud> _clouds = new();
	readonly List<(SpriteActor actor, float baseX, float z)> _stars = new();

	SpriteActor _skyA;
	SpriteActor _skyB;
	SpriteActor _sun;
	SpriteActor _moon;
	string _plateA = "";
	string _plateB = "";
	LayoutMode _mode = LayoutMode.Game;

	float SkyWidth => 2800f;
	/// <summary>
	/// Lab must overshoot the ortho frustum. s&box OrthographicHeight behaves like a half-extent
	/// in practice (full visible ≈ 2×), so Lab uses ~2.2× camera ortho.
	/// </summary>
	float SkyHeight => _mode == LayoutMode.Lab ? 1600f : 320f;
	float SkyCenterZ => _mode == LayoutMode.Lab ? 0f : WorldPresenter.WaterlineZ + 160f;
	float SkyY => _mode == LayoutMode.Lab ? 90f : 85f;

	struct Cloud
	{
		public SpriteActor Day;
		public SpriteActor Sunset;
		public float WorldX;
		public float Z;
		public float Speed;
		public float Parallax;
		public float Height;
	}

	public void Build( Scene scene, List<GameObject> ownedBucket, LayoutMode mode = LayoutMode.Game )
	{
		_mode = mode;
		_clouds.Clear();
		_stars.Clear();

		_skyA = Band( scene, ownedBucket, "SkyA", "env/sky_day", SkyWidth, SkyHeight, SkyCenterZ, SkyY );
		_skyB = Band( scene, ownedBucket, "SkyB", "env/sky_sunset", SkyWidth, SkyHeight, SkyCenterZ, SkyY - 1f );
		_skyB.Tint = new Color( 1, 1, 1, 0 );

		var sunSize = _mode == LayoutMode.Lab ? 110f : 72f;
		var moonSize = _mode == LayoutMode.Lab ? 70f : 48f;
		_sun = Fit( scene, ownedBucket, "Sun", "env/sun", sunSize, 0f, 100f, SkyY - 8f );
		_moon = Fit( scene, ownedBucket, "Moon", "env/moon", moonSize, 0f, 110f, SkyY - 9f );

		// No extra star sprites — night sky plate carries the starfield.

		var specs = _mode == LayoutMode.Lab
			? new (string day, string sunset, float x, float z, float speed, float para, float h)[]
			{
				("env/cloud_a", "env/cloud_sunset_a", -520f, 220f, 18f, 0.12f, 55f),
				("env/cloud_b", "env/cloud_sunset_b", -200f, 140f, 14f, 0.18f, 62f),
				("env/cloud_c", "env/cloud_sunset_c", 120f, 80f, 20f, 0.1f, 50f),
				("env/cloud_a", "env/cloud_sunset_a", 400f, 260f, 16f, 0.2f, 58f),
				("env/cloud_b", "env/cloud_sunset_b", 680f, 110f, 12f, 0.15f, 64f),
				("env/cloud_c", "env/cloud_sunset_c", -780f, 300f, 11f, 0.08f, 48f),
				("env/cloud_a", "env/cloud_sunset_a", 900f, 60f, 15f, 0.22f, 52f),
				("env/cloud_b", "env/cloud_sunset_b", -40f, 180f, 17f, 0.14f, 56f),
			}
			: new (string day, string sunset, float x, float z, float speed, float para, float h)[]
			{
				("env/cloud_a", "env/cloud_sunset_a", -520f, 130f, 14f, 0.18f, 42f),
				("env/cloud_b", "env/cloud_sunset_b", -280f, 150f, 11f, 0.22f, 48f),
				("env/cloud_c", "env/cloud_sunset_c", -40f, 120f, 16f, 0.15f, 40f),
				("env/cloud_a", "env/cloud_sunset_a", 220f, 145f, 12f, 0.25f, 44f),
				("env/cloud_b", "env/cloud_sunset_b", 480f, 125f, 15f, 0.2f, 46f),
				("env/cloud_c", "env/cloud_sunset_c", 720f, 155f, 10f, 0.28f, 50f),
				("env/cloud_a", "env/cloud_sunset_a", -700f, 165f, 9f, 0.12f, 36f),
				("env/cloud_b", "env/cloud_sunset_b", 900f, 135f, 13f, 0.24f, 42f),
			};

		for ( var i = 0; i < specs.Length; i++ )
		{
			var s = specs[i];
			var day = Fit( scene, ownedBucket, $"CloudDay{i}", s.day, s.h, s.x, s.z, SkyY - 14f );
			var sunset = Fit( scene, ownedBucket, $"CloudSunset{i}", s.sunset, s.h, s.x, s.z, SkyY - 15f );
			sunset.Tint = new Color( 1, 1, 1, 0 );
			_clouds.Add( new Cloud
			{
				Day = day,
				Sunset = sunset,
				WorldX = s.x,
				Z = s.z,
				Speed = s.speed,
				Parallax = s.para,
				Height = s.h
			} );
		}
	}

	public void Update( float dt, float cameraX, TimeOfDayService time, WeatherService weather )
	{
		time.GetSkyPlates( out var plateA, out var plateB, out var blend );

		if ( _skyA is not null )
		{
			if ( _plateA != plateA )
			{
				_plateA = plateA;
				_skyA.Path = plateA;
			}
			_skyA.LockNativeAspect = false;
			_skyA.Size = new Vector2( SkyWidth, SkyHeight );
			_skyA.GameObject.WorldPosition = new Vector3( 0, SkyY, SkyCenterZ );
			_skyA.Tint = new Color( 1, 1, 1, 1f - blend );
		}

		if ( _skyB is not null )
		{
			if ( _plateB != plateB )
			{
				_plateB = plateB;
				_skyB.Path = plateB;
			}
			_skyB.LockNativeAspect = false;
			_skyB.Size = new Vector2( SkyWidth, SkyHeight );
			_skyB.GameObject.WorldPosition = new Vector3( 0, SkyY - 1f, SkyCenterZ );
			_skyB.Tint = new Color( 1, 1, 1, blend );
		}

		if ( _sun is not null )
		{
			var alt = time.SunAltitude;
			var sunX = (time.Normalized - 0.5f) * (_mode == LayoutMode.Lab ? 1000f : 900f);
			var sunZ = _mode == LayoutMode.Lab
				? alt * 420f
				: WorldPresenter.WaterlineZ + 40f + Math.Max( 0f, alt ) * 180f;
			_sun.GameObject.WorldPosition = new Vector3( sunX, SkyY - 8f, sunZ );
			// Keep sun readable in lab even near horizon
			var sunAlpha = alt > -0.35f ? Math.Clamp( 0.35f + alt * 1.2f, 0.2f, 1f ) : 0f;
			if ( _mode == LayoutMode.Lab && alt > -0.55f )
				sunAlpha = Math.Max( sunAlpha, 0.45f );
			var c = time.SunTint;
			_sun.Tint = new Color( c.r, c.g, c.b, sunAlpha );
		}

		if ( _moon is not null )
		{
			var malt = time.MoonAltitude;
			var moonX = -((time.Normalized - 0.5f) * (_mode == LayoutMode.Lab ? 1000f : 900f));
			var moonZ = _mode == LayoutMode.Lab
				? malt * 400f
				: WorldPresenter.WaterlineZ + 50f + Math.Max( 0f, malt ) * 160f;
			_moon.GameObject.WorldPosition = new Vector3( moonX, SkyY - 9f, moonZ );
			var moonAlpha = Math.Clamp( time.StarVisibility * (0.35f + Math.Max( 0f, malt ) ), 0f, 1f );
			if ( _mode == LayoutMode.Lab && time.StarVisibility > 0.2f )
				moonAlpha = Math.Max( moonAlpha, 0.55f );
			_moon.Tint = new Color( 0.92f, 0.94f, 1f, moonAlpha );
		}

		for ( var i = 0; i < _stars.Count; i++ )
		{
			var (star, baseX, z) = _stars[i];
			star.Tint = new Color( 1, 1, 1, 0 );
			star.GameObject.WorldPosition = new Vector3( baseX - cameraX * 0.04f, SkyY - 12f, z );
		}

		var wind = 0.6f + weather.Wind * 1.4f;

		for ( var i = 0; i < _clouds.Count; i++ )
		{
			var c = _clouds[i];
			c.WorldX -= dt * c.Speed * wind;
			if ( c.WorldX < -1100f ) c.WorldX += 2200f;
			if ( c.WorldX > 1200f ) c.WorldX -= 2200f;

			var screenX = c.WorldX - cameraX * c.Parallax;

			// Always identical & fully visible — no time tint, no sunset crossfade, no alpha fade.
			if ( c.Day is not null )
			{
				c.Day.GameObject.WorldPosition = new Vector3( screenX, SkyY - 14f, c.Z );
				c.Day.Tint = Color.White;
			}
			if ( c.Sunset is not null )
			{
				c.Sunset.GameObject.WorldPosition = new Vector3( screenX, SkyY - 15f, c.Z + 1f );
				c.Sunset.Tint = new Color( 1, 1, 1, 0 );
			}

			_clouds[i] = c;
		}
	}

	static SpriteActor Band( Scene scene, List<GameObject> bucket, string name, string path, float w, float h, float z, float y )
	{
		var go = scene.CreateObject();
		go.Name = name;
		go.WorldPosition = new Vector3( 0, y, z );
		bucket.Add( go );
		var actor = go.AddComponent<SpriteActor>();
		actor.LockNativeAspect = false;
		actor.Path = path;
		actor.Size = new Vector2( w, h );
		return actor;
	}

	static SpriteActor Fit( Scene scene, List<GameObject> bucket, string name, string path, float height, float x, float z, float y )
	{
		var go = scene.CreateObject();
		go.Name = name;
		go.WorldPosition = new Vector3( x, y, z );
		bucket.Add( go );
		var actor = go.AddComponent<SpriteActor>();
		actor.SetFitHeight( path, height );
		return actor;
	}
}
