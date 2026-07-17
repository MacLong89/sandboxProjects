namespace Offshore;

/// <summary>
/// Layer 2 — ocean on the lower portion of the screen with a wavy moving surface.
/// Wave texture is CPU-painted at a capped rate (not every frame) to stay cheap.
/// </summary>
public sealed class OceanLayer : Component
{
	private const int TexW = 256;
	private const int TexH = 128;
	private const float OceanDepth = 48f; // along camera forward; in front of sun/moon (52)
	private const float ScreenCoverage = 0.578f; // +5% from 0.55
	private const float PaintInterval = 1f / 12f; // ~12 Hz is plenty for soft waves

	private DayNightCycle _cycle;
	private GameObject _waterGo;
	private SpriteRenderer _water;
	private Texture _texture;
	private byte[] _pixels;
	private float _scroll;
	private float _time;
	private float _paintAccum;

	private float _viewHalfW = 40f;
	private float _viewHalfH = 22f;

	protected override void OnStart()
	{
		_cycle = Components.Get<DayNightCycle>()
			?? OffshoreGameController.Instance?.DayNight
			?? Components.Create<DayNightCycle>();

		_pixels = new byte[TexW * TexH * 4];
		_texture = Texture.Create( TexW, TexH, ImageFormat.RGBA8888 )
			.WithName( "offshore_ocean_waves" )
			.WithData( _pixels )
			.Finish();

		_waterGo = new GameObject( true, "OceanWater" );
		_water = _waterGo.Components.Create<SpriteRenderer>();
		_water.Sprite = OffshoreSprites.MakeSprite( _texture );
		_water.StartingAnimationName = "Default";
		_water.Billboard = SpriteRenderer.BillboardMode.Always;
		_water.Lighting = false;
		_water.FogStrength = 0f;
		_water.Opaque = false;
		_water.AlphaCutoff = 0.04f;
		_water.IsSorted = true;
		_water.TextureFilter = Sandbox.Rendering.FilterMode.Bilinear;

		PaintWaves();
	}

	protected override void OnUpdate()
	{
		if ( _waterGo is null || !_waterGo.IsValid() )
			return;

		var cam = OffshoreGameController.Instance?.Camera;
		if ( cam is null || !cam.IsValid() )
			return;

		_cycle ??= OffshoreGameController.Instance?.DayNight;
		_time += Time.Delta;
		_scroll += Time.Delta * 0.09f;

		UpdateViewSize( cam );
		PlacePlate( cam );

		_paintAccum += Time.Delta;
		if ( _paintAccum >= PaintInterval )
		{
			_paintAccum %= PaintInterval;
			PaintWaves();
		}
	}

	private void UpdateViewSize( Component cam )
	{
		ScreenAxes.GetViewExtents( cam, OceanDepth, out _viewHalfW, out _viewHalfH );
	}

	private void PlacePlate( Component cam )
	{
		// Extra height so wavy peaks have room above the mean waterline.
		var plateH = _viewHalfH * 2f * ScreenCoverage * 1.06f;
		var plateW = _viewHalfW * 2f * 1.08f;
		var screenY = -_viewHalfH + plateH * 0.5f;

		_waterGo.WorldPosition = ScreenAxes.FromCamera( cam, 0f, screenY, OceanDepth );
		_water.Size = new Vector2( plateW, plateH );
		_water.Color = Color.White;
	}

	private void PaintWaves()
	{
		if ( _texture is null || !_texture.IsValid() || _pixels is null )
			return;

		var day = _cycle?.Daylight01 ?? 1f;
		var night = _cycle?.Night01 ?? 0f;
		var gold = _cycle?.Golden01 ?? 0f;

		var deep = Color.Lerp( new Color( 0.04f, 0.1f, 0.22f ), new Color( 0.05f, 0.22f, 0.38f ), day );
		var mid = Color.Lerp( new Color( 0.07f, 0.16f, 0.3f ), new Color( 0.12f, 0.42f, 0.55f ), day );
		var shallow = Color.Lerp( new Color( 0.1f, 0.2f, 0.34f ), new Color( 0.28f, 0.62f, 0.68f ), day );
		var highlight = Color.Lerp( new Color( 0.35f, 0.45f, 0.55f ), new Color( 0.75f, 0.92f, 0.95f ), day );
		var foamCol = Color.Lerp( new Color( 0.55f, 0.65f, 0.75f ), new Color( 0.92f, 0.97f, 1f ), day );

		if ( gold > 0.05f )
		{
			var warmDeep = new Color( 0.22f, 0.12f, 0.2f );
			var warmShallow = new Color( 0.85f, 0.45f, 0.28f );
			deep = Color.Lerp( deep, warmDeep, gold * 0.55f );
			mid = Color.Lerp( mid, warmShallow * 0.55f, gold * 0.45f );
			shallow = Color.Lerp( shallow, warmShallow, gold * 0.5f );
			highlight = Color.Lerp( highlight, new Color( 1f, 0.75f, 0.4f ), gold * 0.65f );
			foamCol = Color.Lerp( foamCol, new Color( 1f, 0.85f, 0.55f ), gold * 0.5f );
		}

		if ( night > 0.4f )
		{
			deep = Color.Lerp( deep, new Color( 0.02f, 0.04f, 0.1f ), night * 0.7f );
			mid = Color.Lerp( mid, new Color( 0.04f, 0.08f, 0.16f ), night * 0.65f );
			shallow = Color.Lerp( shallow, new Color( 0.06f, 0.12f, 0.22f ), night * 0.55f );
			highlight = Color.Lerp( highlight, new Color( 0.25f, 0.35f, 0.5f ), night * 0.5f );
			foamCol = Color.Lerp( foamCol, new Color( 0.4f, 0.5f, 0.65f ), night * 0.45f );
		}

		var invWm1 = 1f / (TexW - 1);
		var invHm1 = 1f / (TexH - 1);
		var scroll = _scroll;
		var time = _time;

		for ( var y = 0; y < TexH; y++ )
		{
			var v = y * invHm1;
			var row = y * TexW;

			for ( var x = 0; x < TexW; x++ )
			{
				var u = x * invWm1;
				var i = (row + x) * 4;

				// Moving surface silhouette — gentle amplitude
				var s1 = MathF.Sin( (u * 5.5f + scroll * 1.5f) * MathF.PI * 2f + time * 0.7f );
				var s2 = MathF.Sin( (u * 11f - scroll * 2.2f) * MathF.PI * 2f + time * 1.1f );
				var surface = 0.10f + s1 * 0.018f + s2 * 0.012f;

				if ( v < surface - 0.008f )
				{
					_pixels[i] = 0;
					_pixels[i + 1] = 0;
					_pixels[i + 2] = 0;
					_pixels[i + 3] = 0;
					continue;
				}

				var edgeDist = v - surface;
				var edgeAlpha = Math.Clamp( edgeDist / 0.015f, 0f, 1f );

				var nearSurface = Math.Clamp( 1f - edgeDist / 0.4f, 0f, 1f );
				var w1 = MathF.Sin( (u * 14f + scroll * 2.2f) * MathF.PI * 2f + time * 0.85f );
				var w2 = MathF.Sin( (u * 26f - scroll * 3.1f) * MathF.PI * 2f + time * 1.25f ) * 0.55f;
				var wave = (w1 + w2) * (0.12f + nearSurface * 0.28f);

				var crest = Math.Clamp( wave * 0.5f + 0.5f, 0f, 1f );
				crest *= crest;

				var depthFade = Smooth( Math.Clamp( (v - surface) / MathF.Max( 0.001f, 1f - surface ), 0f, 1f ) );
				var col = Color.Lerp( shallow, deep, depthFade );
				col = Color.Lerp( col, mid, 0.28f + wave * 0.08f );

				var crestBand = Math.Clamp( 1f - edgeDist / 0.16f, 0f, 1f );
				col = Color.Lerp( col, highlight, crest * crestBand * 0.28f );
				col = Color.Lerp( col, foamCol, crest * crestBand * crestBand * 0.22f );

				var lip = Math.Clamp( 1f - MathF.Abs( edgeDist ) / 0.02f, 0f, 1f );
				if ( lip > 0f )
					col = Color.Lerp( col, foamCol, lip * (0.4f + crest * 0.15f) );

				_pixels[i] = ToByte( col.r );
				_pixels[i + 1] = ToByte( col.g );
				_pixels[i + 2] = ToByte( col.b );
				_pixels[i + 3] = (byte)(edgeAlpha * 255f);
			}
		}

		_texture.Update( _pixels, 0, 0, TexW, TexH );
	}

	private static float Smooth( float t )
	{
		t = Math.Clamp( t, 0f, 1f );
		return t * t * (3f - 2f * t);
	}

	private static byte ToByte( float c ) => (byte)Math.Clamp( (int)(c * 255f + 0.5f), 0, 255 );
}
