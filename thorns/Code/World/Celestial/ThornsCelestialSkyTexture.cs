namespace Sandbox;

/// <summary>
/// Bakes <see cref="ThornsCelestialState"/> into an equirectangular panorama at runtime.
/// Bypasses custom sky shaders when project shader assets fail to compile.
/// </summary>
public static class ThornsCelestialSkyTexture
{
	const int Width = 512;
	const int Height = 256;

	static Bitmap _bitmap;
	static Texture _texture;
	static int _contentKey = int.MinValue;

	public static Texture GetOrUpdate( in ThornsCelestialState state, float sunDiscAngularDiameter, bool forceTestColors )
	{
		var key = ComputeContentKey( in state, sunDiscAngularDiameter, forceTestColors );
		if ( _texture.IsValid() && key == _contentKey )
			return _texture;

		_bitmap ??= new Bitmap( Width, Height );

		var zenith = state.SkyZenith;
		var mid = state.SkyMid;
		var horizon = state.SkyHorizon;
		var glowColor = state.HorizonGlowColor;
		var bandPower = Math.Max( state.HorizonBandPower, 0.65f );
		var exposure = Math.Max( 0.02f, state.SkyExposure );
		var sunDir = state.SunDirection.Normal;
		var sunDiscDiameter = Math.Max( sunDiscAngularDiameter, 8f );
		var discPower = 5200f * MathF.Pow( 10f / sunDiscDiameter, 2f );

		for ( var y = 0; y < Height; y++ )
		{
			var lat = (0.5f - y / (Height - 1f)) * MathF.PI;
			var sinLat = MathF.Sin( lat );
			var cosLat = MathF.Cos( lat );

			for ( var x = 0; x < Width; x++ )
			{
				var lon = x / (Width - 1f) * MathF.Tau;
				var dir = new Vector3( cosLat * MathF.Cos( lon ), cosLat * MathF.Sin( lon ), sinLat );

				var up = Math.Clamp( (dir.z + 0.08f) / 1.08f, 0f, 1f );
				var horizonMix = MathF.Pow( 1f - up, bandPower );
				var skyHigh = Color.Lerp( mid, zenith, SmoothStep( 0.2f, 0.9f, up ) );
				var color = Color.Lerp( skyHigh, horizon, horizonMix );
				color = Color.Lerp( color, glowColor, horizonMix * Math.Clamp( state.HorizonGlowStrength * 0.25f, 0f, 1f ) );

				if ( state.HorizonGlowStrength > 0.001f && sunDir.Length > 0.001f )
				{
					var sunFocus = MathF.Pow( Math.Clamp( Vector3.Dot( dir, sunDir ), 0f, 1f ), 4.5f );
					color = color + glowColor * horizonMix * sunFocus * state.HorizonGlowStrength * 0.35f;
				}

				color = color * exposure;

				if ( state.SunDiscIntensity > 0.001f && sunDir.Length > 0.001f && dir.z > -0.05f )
				{
					var sunDot = Math.Clamp( Vector3.Dot( dir, sunDir ), 0f, 1f );
					var disc = MathF.Pow( sunDot, discPower ) * state.SunDiscIntensity;
					var halo = MathF.Pow( sunDot, Math.Max( discPower * 0.18f, 220f ) ) * state.SunDiscGlow;
					var viewFade = Math.Clamp( dir.z * 2.5f, 0f, 1f );
					color = color + state.SunDiscColor * (disc + halo) * viewFade;
				}

				if ( state.StarBrightness > 0.01f && dir.z > 0f )
				{
					var star = Stars( dir, state.StarRotation, state.StarBrightness );
					color = color + new Color( star, star, star );
				}

				color = ClampColor( color );

				if ( forceTestColors )
					color = Color.Lerp( color, new Color( 1f, 0f, 1f ), 0.55f );

				_bitmap.SetPixel( x, y, color );
			}
		}

		_texture = _bitmap.ToTexture();
		_contentKey = key;
		return _texture;
	}

	static float Stars( Vector3 dir, float rotation, float brightness )
	{
		var s = MathF.Sin( rotation );
		var c = MathF.Cos( rotation );
		var starRay = new Vector3( dir.x * c + dir.z * s, dir.y, -dir.x * s + dir.z * c ).Normal;

		var p = starRay * 420f;
		var cell = new Vector3( MathF.Floor( p.x ), MathF.Floor( p.y ), MathF.Floor( p.z ) );
		var v = Hash31( cell );
		v = MathF.Pow( v, 18f );
		v *= Math.Clamp( dir.z * 60f, 0f, 1f );
		return v * brightness;
	}

	static float Hash31( Vector3 p )
	{
		var f = new Vector3(
			p.x * 0.1031f - MathF.Floor( p.x * 0.1031f ),
			p.y * 0.1031f - MathF.Floor( p.y * 0.1031f ),
			p.z * 0.1031f - MathF.Floor( p.z * 0.1031f ) );
		var dot = f.x * (f.y + 33.33f) + f.y * (f.z + 33.33f) + f.z * (f.x + 33.33f);
		var t = f.x + f.y + dot;
		return t - MathF.Floor( t );
	}

	static float SmoothStep( float edge0, float edge1, float x )
	{
		var t = Math.Clamp( (x - edge0) / (edge1 - edge0), 0f, 1f );
		return t * t * (3f - 2f * t);
	}

	static Color ClampColor( Color c ) => new Color(
		Math.Clamp( c.r, 0f, 1f ),
		Math.Clamp( c.g, 0f, 1f ),
		Math.Clamp( c.b, 0f, 1f ),
		Math.Clamp( c.a, 0f, 1f ) );

	static int ComputeContentKey( in ThornsCelestialState state, float sunDiscDiameter, bool forceTestColors )
	{
		var h = HashCode.Combine(
			Quant( state.SkyZenith ),
			Quant( state.SkyMid ),
			Quant( state.SkyHorizon ),
			Quant( state.HorizonGlowColor ),
			Quant( state.SunDiscColor ),
			QuantDir( state.SunDirection ),
			(int)(state.SkyExposure * 100f),
			(int)(state.HorizonGlowStrength * 100f ) );

		return HashCode.Combine(
			h,
			(int)(state.StarBrightness * 100f),
			(int)(state.SunDiscIntensity * 100f),
			(int)(state.SunDiscGlow * 10000f),
			(int)(sunDiscDiameter * 10f),
			forceTestColors ? 1 : 0 );
	}

	static int Quant( Color c ) => ((int)(c.r * 255f) << 16) | ((int)(c.g * 255f) << 8) | (int)(c.b * 255f);

	static int QuantDir( Vector3 dir )
	{
		if ( dir.Length < 0.001f )
			return 0;

		dir = dir.Normal;
		return ((int)(dir.x * 100f) << 20) | ((int)(dir.y * 100f) << 10) | (int)(dir.z * 100f);
	}
}
