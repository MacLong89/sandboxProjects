namespace Offshore;

/// <summary>
/// Layer 1 — full-bleed sky + separate pixel sun/moon sprites.
/// Placement is camera-relative (Right/Up/Forward) so discs stay in the live frustum.
/// </summary>
public sealed class SkyLayer : Component
{
	private const int GradWidth = 512;
	private const int GradHeight = 288;

	// Distances along camera forward (closer = smaller → draws in front).
	private const float SkyDepth = 58f;
	private const float CelestialDepth = 55f; // sun/moon behind clouds
	private const float CloudDepth = 52f;     // clouds in front of sun/moon
	// Ocean sits around ~48, still in front of sky stack.
	private const int CloudCount = 6;

	private DayNightCycle _cycle;
	private GameObject _skyGo;
	private GameObject _sunGo;
	private GameObject _sunGlowGo;
	private GameObject _moonGo;
	private GameObject _moonGlowGo;
	private GameObject _starsGo;

	private SpriteRenderer _sky;
	private SpriteRenderer _stars;
	private SpriteRenderer _sun;
	private SpriteRenderer _sunGlow;
	private SpriteRenderer _moon;
	private SpriteRenderer _moonGlow;

	private readonly GameObject[] _cloudGo = new GameObject[CloudCount];
	private readonly SpriteRenderer[] _clouds = new SpriteRenderer[CloudCount];
	private readonly float[] _cloudX = new float[CloudCount];
	private readonly float[] _cloudY = new float[CloudCount];
	private readonly float[] _cloudSpeed = new float[CloudCount];
	private readonly float[] _cloudSize = new float[CloudCount];
	private readonly float[] _cloudAlpha = new float[CloudCount];

	private Texture _gradient;
	private byte[] _pixels;
	private float _lastPaintPhase = -1f;
	private Vector2 _sunUv;
	private bool _sunOn;

	private float _plateW = 90f;
	private float _plateH = 50f;

	protected override void OnStart()
	{
		_cycle = Components.Get<DayNightCycle>()
			?? OffshoreGameController.Instance?.DayNight
			?? Components.Create<DayNightCycle>();

		_pixels = new byte[GradWidth * GradHeight * 4];
		_gradient = Texture.Create( GradWidth, GradHeight, ImageFormat.RGBA8888 )
			.WithName( "offshore_sky_gradient" )
			.WithData( _pixels )
			.Finish();

		_sky = MakeSprite( "SkyGradient", OffshoreSprites.MakeSprite( _gradient ), out _skyGo );
		_sky.Opaque = false;

		_stars = MakeSprite( "Stars", OffshoreSprites.MakeSprite( ProceduralSprites.Starfield() ), out _starsGo );
		_sunGlow = MakeSprite( "SunGlow", OffshoreSprites.MakeSprite( ProceduralSprites.SoftDisc() ), out _sunGlowGo );
		_sun = MakeSprite( "Sun", OffshoreSprites.MakeSprite( OffshoreSprites.Load( OffshoreSprites.Paths.SunPixel ) ), out _sunGo );
		_moonGlow = MakeSprite( "MoonGlow", OffshoreSprites.MakeSprite( ProceduralSprites.SoftDisc() ), out _moonGlowGo );
		_moon = MakeSprite( "Moon", OffshoreSprites.MakeSprite( OffshoreSprites.Load( OffshoreSprites.Paths.MoonPixel ) ), out _moonGo );

		SpawnClouds();

		foreach ( var r in new[] { _sun, _moon, _sunGlow, _moonGlow, _stars } )
		{
			r.AlphaCutoff = 0.01f;
			r.Opaque = false;
		}

		PaintGradient( true );
	}

	protected override void OnUpdate()
	{
		if ( _skyGo is null || !_skyGo.IsValid() || _cycle is null )
			return;

		var cam = OffshoreGameController.Instance?.Camera;
		if ( cam is null || !cam.IsValid() )
			return;

		ScreenAxes.GetViewExtents( cam, SkyDepth, out var halfW, out var halfH );
		_plateW = halfW * 2.15f;
		_plateH = halfH * 2.15f;

		_skyGo.WorldPosition = ScreenAxes.FromCamera( cam, 0f, 0f, SkyDepth );
		_sky.Size = new Vector2( _plateW, _plateH );

		_starsGo.WorldPosition = ScreenAxes.FromCamera( cam, 0f, 0f, SkyDepth - 0.4f );
		_stars.Size = new Vector2( _plateW, _plateH );
		var night = _cycle.Night01;
		_stars.Color = new Color( 1f, 1f, 1f, night * night * (0.7f + 0.3f * MathF.Sin( Time.Now * 1.7f )) );

		PlaceSun( cam );
		PlaceMoon( cam );
		UpdateClouds( cam );
		PaintGradient( false );

		var camComp = cam.Components.Get<CameraComponent>();
		if ( camComp is not null && camComp.IsValid() )
			camComp.BackgroundColor = SampleSky( _cycle, 1f, 0.5f );
	}

	private void SpawnClouds()
	{
		var paths = new[]
		{
			OffshoreSprites.Paths.CloudA,
			OffshoreSprites.Paths.CloudB,
			OffshoreSprites.Paths.CloudC
		};

		for ( var i = 0; i < CloudCount; i++ )
		{
			var path = paths[i % paths.Length];
			_clouds[i] = MakeSprite( $"Cloud_{i}", OffshoreSprites.MakeSprite( OffshoreSprites.Load( path ) ), out _cloudGo[i] );
			_clouds[i].AlphaCutoff = 0.02f;
			_clouds[i].Opaque = false;

			_cloudX[i] = Game.Random.Float( -1.2f, 1.2f );
			_cloudY[i] = Game.Random.Float( 0.12f, 0.55f ); // upper sky lanes (normalized)
			_cloudSpeed[i] = Game.Random.Float( 0.012f, 0.035f ) * (i % 2 == 0 ? 1f : -1f);
			_cloudSize[i] = Game.Random.Float( 0.22f, 0.42f );
			_cloudAlpha[i] = Game.Random.Float( 0.55f, 0.9f );
		}
	}

	private void UpdateClouds( Component cam )
	{
		ScreenAxes.GetViewExtents( cam, CloudDepth, out var halfW, out var halfH );
		var day = _cycle.Daylight01;
		var gold = _cycle.Golden01;
		var night = _cycle.Night01;

		// Tint: soft white day → warm pink at golden hour → muted at night
		var tint = Color.Lerp( new Color( 0.75f, 0.8f, 0.9f ), Color.White, day );
		tint = Color.Lerp( tint, new Color( 1f, 0.72f, 0.62f ), gold * 0.85f );
		var alphaScale = Math.Clamp( 0.25f + day * 0.75f + gold * 0.15f - night * 0.35f, 0.15f, 1f );

		for ( var i = 0; i < CloudCount; i++ )
		{
			if ( _cloudGo[i] is null || !_cloudGo[i].IsValid() )
				continue;

			_cloudX[i] += _cloudSpeed[i] * Time.Delta;
			if ( _cloudX[i] > 1.35f )
				_cloudX[i] = -1.35f;
			if ( _cloudX[i] < -1.35f )
				_cloudX[i] = 1.35f;

			var sx = _cloudX[i] * halfW;
			var sy = _cloudY[i] * halfH;
			var size = halfW * _cloudSize[i];
			// Slight bob
			sy += MathF.Sin( Time.Now * 0.35f + i * 1.7f ) * (halfH * 0.01f);

			_cloudGo[i].WorldPosition = ScreenAxes.FromCamera( cam, sx, sy, CloudDepth - i * 0.05f );
			_clouds[i].Size = new Vector2( size, size * 0.55f );
			_clouds[i].Color = new Color( tint.r, tint.g, tint.b, _cloudAlpha[i] * alphaScale );
		}
	}

	private static SpriteRenderer MakeSprite( string name, Sprite sprite, out GameObject go )
	{
		go = new GameObject( true, name );
		var r = go.Components.Create<SpriteRenderer>();
		r.Sprite = sprite;
		r.StartingAnimationName = "Default";
		r.Size = new Vector2( 4f, 4f );
		r.Billboard = SpriteRenderer.BillboardMode.Always;
		r.Lighting = false;
		r.FogStrength = 0f;
		r.Opaque = false;
		r.AlphaCutoff = 0.01f;
		r.IsSorted = true;
		r.TextureFilter = Sandbox.Rendering.FilterMode.Bilinear;
		return r;
	}

	/// <summary>
	/// Arc in camera screen space: rise from under water, peak, set back under water.
	/// </summary>
	private Vector2 CelestialScreenArc( Component cam, float t )
	{
		t = Math.Clamp( t, 0f, 1f );
		ScreenAxes.GetViewExtents( cam, CelestialDepth, out var halfW, out var halfH );

		var horizon = halfH * 0.18f;
		var dip = halfH * 0.38f;  // sink deeper under the waves before fully gone
		var peak = halfH * 0.28f;

		var lift = MathF.Sin( t * MathF.PI );
		var x = MathX.Lerp( -halfW * 0.32f, halfW * 0.32f, t );
		var y = horizon - dip + lift * (peak + dip);
		return new Vector2( x, y );
	}

	private void PlaceSun( Component cam )
	{
		var h = _cycle.HourOfDay;
		// Wider window so the disc stays active while sinking into the water.
		var visible = h is >= 5.0f and <= 19.75f;
		_sunGo.Enabled = visible;
		_sunGlowGo.Enabled = visible;
		_sunOn = visible;
		if ( !visible )
			return;

		var t = Math.Clamp( (h - 5.0f) / (19.75f - 5.0f), 0f, 1f );
		var s = CelestialScreenArc( cam, t );

		ScreenAxes.GetViewExtents( cam, CelestialDepth, out _, out var halfH );
		var horizon = halfH * 0.18f;
		_sunUv = new Vector2(
			Math.Clamp( s.x / MathF.Max( 0.01f, halfH * 2f ) + 0.5f, 0f, 1f ),
			Math.Clamp( s.y / MathF.Max( 0.01f, halfH * 2f ) + 0.5f, 0f, 1f ) );

		var golden = _cycle.Golden01;
		var size = MathF.Max( 5.5f, halfH * 0.30f ) * 0.7f;
		// Soft fade once well below the waterline (still moving deeper).
		var under = Math.Clamp( (horizon - s.y) / (halfH * 0.22f), 0f, 1f );
		var fade = 1f - under * 0.85f;

		_sunGo.WorldPosition = ScreenAxes.FromCamera( cam, s.x, s.y, CelestialDepth );
		_sunGlowGo.WorldPosition = ScreenAxes.FromCamera( cam, s.x, s.y, CelestialDepth + 0.2f );
		_sun.Color = new Color( 1f, 1f, 1f, fade );
		_sun.Size = new Vector2( size, size );
		_sunGlow.Color = Color.Lerp(
			new Color( 1f, 0.9f, 0.45f, 0.32f * fade ),
			new Color( 1f, 0.5f, 0.2f, 0.4f * fade ),
			golden );
		_sunGlow.Size = new Vector2( size * 1.6f, size * 1.6f );
	}

	private void PlaceMoon( Component cam )
	{
		var h = _cycle.HourOfDay;
		float t;
		// Longer overnight window so it can sink further before vanishing.
		if ( h >= 18.75f )
			t = (h - 18.75f) / (24f - 18.75f + 6.9f);
		else if ( h <= 6.9f )
			t = (h + (24f - 18.75f)) / (24f - 18.75f + 6.9f);
		else
			t = -1f;

		var visible = t >= 0f && _cycle.Night01 > 0.02f;
		_moonGo.Enabled = visible;
		_moonGlowGo.Enabled = visible;
		if ( !visible )
			return;

		t = Math.Clamp( t, 0f, 1f );
		var s = CelestialScreenArc( cam, t );
		var a = Math.Clamp( _cycle.Night01 * 1.2f, 0f, 1f );

		ScreenAxes.GetViewExtents( cam, CelestialDepth, out _, out var halfH );
		var horizon = halfH * 0.18f;
		var under = Math.Clamp( (horizon - s.y) / (halfH * 0.22f), 0f, 1f );
		var fade = (1f - under * 0.85f) * a;
		var size = MathF.Max( 4.5f, halfH * 0.24f ) * 0.7f;

		_moonGo.WorldPosition = ScreenAxes.FromCamera( cam, s.x, s.y, CelestialDepth );
		_moonGlowGo.WorldPosition = ScreenAxes.FromCamera( cam, s.x, s.y, CelestialDepth + 0.2f );
		_moon.Color = new Color( 1f, 1f, 1f, fade );
		_moon.Size = new Vector2( size, size );
		_moonGlow.Color = new Color( 0.55f, 0.7f, 1f, fade * 0.25f );
		_moonGlow.Size = new Vector2( size * 1.5f, size * 1.5f );
	}

	private void PaintGradient( bool force )
	{
		if ( _gradient is null || !_gradient.IsValid() || _pixels is null )
			return;

		if ( !force && MathF.Abs( _cycle.Phase01 - _lastPaintPhase ) < 0.003f )
			return;

		_lastPaintPhase = _cycle.Phase01;

		for ( var y = 0; y < GradHeight; y++ )
		{
			var height01 = 1f - y / (float)(GradHeight - 1);
			for ( var x = 0; x < GradWidth; x++ )
			{
				var u = x / (float)(GradWidth - 1);
				var col = SampleSky( _cycle, height01, u );

				if ( _sunOn )
				{
					var dx = u - _sunUv.x;
					var dy = height01 - (0.5f + (_sunUv.y - 0.5f));
					var d2 = dx * dx * 2.2f + dy * dy;
					var bloom = MathF.Exp( -d2 * 16f ) * (0.18f + _cycle.Golden01 * 0.4f);
					var warm = Color.Lerp( new Color( 1f, 0.92f, 0.7f ), new Color( 1f, 0.55f, 0.25f ), _cycle.Golden01 );
					col = Color.Lerp( col, warm, bloom );
				}

				var i = (y * GradWidth + x) * 4;
				_pixels[i] = ToByte( col.r );
				_pixels[i + 1] = ToByte( col.g );
				_pixels[i + 2] = ToByte( col.b );
				_pixels[i + 3] = 255;
			}
		}

		_gradient.Update( _pixels, 0, 0, GradWidth, GradHeight );
	}

	public static Color SampleSky( DayNightCycle cycle, float height01, float u = 0.5f )
	{
		height01 = Math.Clamp( height01, 0f, 1f );
		u = Math.Clamp( u, 0f, 1f );

		var gold = cycle.Golden01;
		var day = cycle.Daylight01;
		var night = cycle.Night01;
		var h = cycle.HourOfDay;

		var dayZenith = Color.Lerp( new Color( 0.32f, 0.58f, 0.92f ), new Color( 0.18f, 0.48f, 0.9f ), MiddayPull( h ) );
		var dayUpper = new Color( 0.42f, 0.68f, 0.95f );
		var dayMid = new Color( 0.58f, 0.78f, 0.96f );
		var dayHorizon = new Color( 0.78f, 0.88f, 0.97f );

		var goldZenith = new Color( 0.28f, 0.18f, 0.42f );
		var goldUpper = new Color( 0.55f, 0.28f, 0.48f );
		var goldMid = new Color( 0.95f, 0.48f, 0.38f );
		var goldHorizon = new Color( 1f, 0.78f, 0.42f );

		var nightZenith = new Color( 0.03f, 0.045f, 0.12f );
		var nightUpper = new Color( 0.05f, 0.07f, 0.16f );
		var nightMid = new Color( 0.07f, 0.09f, 0.2f );
		var nightHorizon = new Color( 0.1f, 0.12f, 0.24f );

		var coolZ = Color.Lerp( nightZenith, dayZenith, day );
		var coolU = Color.Lerp( nightUpper, dayUpper, day );
		var coolM = Color.Lerp( nightMid, dayMid, day );
		var coolH = Color.Lerp( nightHorizon, dayHorizon, day );

		var warmAmt = Math.Clamp( gold * 1.15f, 0f, 1f );
		var zen = Color.Lerp( coolZ, goldZenith, warmAmt * 0.9f );
		var up = Color.Lerp( coolU, goldUpper, warmAmt );
		var mid = Color.Lerp( coolM, goldMid, warmAmt );
		var hz = Color.Lerp( coolH, goldHorizon, Math.Clamp( warmAmt * 1.1f, 0f, 1f ) );

		Color col;
		if ( height01 > 0.66f )
			col = Color.Lerp( up, zen, Smooth( (height01 - 0.66f) / 0.34f ) );
		else if ( height01 > 0.32f )
			col = Color.Lerp( mid, up, Smooth( (height01 - 0.32f) / 0.34f ) );
		else
			col = Color.Lerp( hz, mid, Smooth( height01 / 0.32f ) );

		var edge = MathF.Abs( u - 0.5f ) * 2f;
		col = Color.Lerp( col, col * 0.92f, edge * 0.08f * (1f - warmAmt) );

		if ( night > 0.7f && gold < 0.2f )
			col = Color.Lerp( col, Color.Lerp( nightHorizon, nightZenith, Smooth( height01 ) ), (night - 0.7f) / 0.3f );

		return col;
	}

	private static float MiddayPull( float hour ) =>
		Math.Clamp( 1f - MathF.Abs( hour - 12.5f ) / 5.5f, 0f, 1f );

	private static float Smooth( float t )
	{
		t = Math.Clamp( t, 0f, 1f );
		return t * t * (3f - 2f * t);
	}

	private static byte ToByte( float c ) => (byte)Math.Clamp( (int)(c * 255f + 0.5f), 0, 255 );
}
