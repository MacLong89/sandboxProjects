using Sandbox.Rendering;

namespace Sandbox;

/// <summary>Screen-space sun and moon discs on the main camera (works with camera-fill sky).</summary>
[Title( "Thorns — Celestial Sprites" )]
[Category( "Thorns/World" )]
public sealed class ThornsCelestialSprites : Component
{
	double _nextDrawRealtime = -1d;

	protected override void OnUpdate()
	{
		var now = Time.Now;
		if ( now < _nextDrawRealtime )
			return;

		_nextDrawRealtime = now + (1f / MathF.Max( 4f, ThornsPerformanceQualityPresets.Get( ThornsPerformanceQualityPresets.ActiveQuality ).CelestialVisualHz ));

		var celestial = ThornsCelestialSystem.Instance;
		if ( !celestial.IsValid() && !ThornsCelestialSystem.TryGet( Scene, out celestial ) )
			return;

		var camera = Components.Get<CameraComponent>();
		if ( !camera.IsValid() || !camera.Enabled || !camera.IsMainCamera )
			return;

		var hud = camera.Hud;
		var state = celestial.CurrentState;

		var showSun = celestial.UseCameraSunSprite
			&& state.SunDiscIntensity > 0.02f
			&& state.SunAltitudeRadians > celestial.SunSpriteMinAltitudeRad;
		if ( showSun )
		{
			DrawDirectionalDisc(
				hud,
				camera,
				state.SunDirection,
				celestial.EffectiveSunDiscAngularDiameter,
				state.SunDiscColor,
				state.SunDiscIntensity );
		}

		var showMoon = state.MoonDiscIntensity > 0.02f && state.MoonAltitudeRadians > 0.02f;
		if ( showMoon )
		{
			DrawDirectionalDisc(
				hud,
				camera,
				state.MoonDirection,
				celestial.MoonDiscAngularDiameter,
				state.MoonDiscColor,
				state.MoonDiscIntensity * 0.85f );
		}
	}

	static void DrawDirectionalDisc(
		HudPainter hud,
		CameraComponent camera,
		Vector3 worldDirection,
		float angularDiameter,
		Color color,
		float intensity )
	{
		if ( !TryProjectDirection( camera, worldDirection, out var center ) )
			return;

		var radiusPx = ComputeAngularRadiusPixels( camera, angularDiameter, intensity );
		var tint = color * Math.Max( intensity, 0.45f );
		tint = new Color( tint.r, tint.g, tint.b, 1f );

		// Circles only — DrawRect / square textures show visible quads on the HUD.
		DrawRadialDisc( hud, center, radiusPx, tint );
	}

	static void DrawRadialDisc( HudPainter hud, Vector2 center, float radiusPx, Color tint )
	{
		var diameter = radiusPx * 2f;
		// Tight halo — large multipliers looked like stacked UI rings on the sky.
		hud.DrawCircle( center, diameter * 1.28f, WithAlpha( tint, 0.05f ) );
		hud.DrawCircle( center, diameter * 1.02f, WithAlpha( tint, 0.12f ) );
		hud.DrawCircle( center, diameter * 0.78f, WithAlpha( tint, 0.35f ) );
		hud.DrawCircle( center, diameter * 0.48f, WithAlpha( tint, 0.88f ) );
		hud.DrawCircle( center, diameter * 0.26f, WithAlpha( Color.Lerp( tint, Color.White, 0.35f ), 1f ) );
	}

	static Color WithAlpha( Color color, float alpha ) =>
		new Color( color.r, color.g, color.b, Math.Clamp( alpha, 0f, 1f ) );

	static bool TryProjectDirection( CameraComponent camera, Vector3 worldDirection, out Vector2 screenCenter )
	{
		screenCenter = default;
		var dir = worldDirection;
		if ( dir.Length < 0.001f )
			return false;

		dir = dir.Normal;
		var rot = camera.WorldRotation;
		var forward = rot.Forward;
		var dot = Vector3.Dot( forward, dir );
		if ( dot < 0.035f )
			return false;

		var viewX = Vector3.Dot( rot.Right, dir ) / dot;
		var viewY = Vector3.Dot( rot.Up, dir ) / dot;

		var halfFovTan = MathF.Tan( camera.FieldOfView * 0.5f * MathF.PI / 180f );
		if ( halfFovTan < 0.0001f )
			return false;

		var aspect = Screen.Height > 1f ? Screen.Width / Screen.Height : 1.777f;
		var ndcX = viewX / (halfFovTan * aspect );
		var ndcY = -viewY / halfFovTan;

		if ( MathF.Abs( ndcX ) > 1.35f || MathF.Abs( ndcY ) > 1.35f )
			return false;

		screenCenter = new Vector2(
			(ndcX * 0.5f + 0.5f) * Screen.Width,
			(ndcY * 0.5f + 0.5f) * Screen.Height );
		return true;
	}

	static float ComputeAngularRadiusPixels( CameraComponent camera, float angularDiameterDeg, float intensity )
	{
		var halfFovRad = camera.FieldOfView * 0.5f * MathF.PI / 180f;
		var halfDiscRad = Math.Max( angularDiameterDeg, 6f ) * 0.5f * MathF.PI / 180f;
		var screenH = Screen.Height > 1f ? Screen.Height : 1080f;
		var tanFov = MathF.Tan( halfFovRad );
		if ( tanFov < 0.0001f )
			return 24f;

		var radius = MathF.Tan( halfDiscRad ) / tanFov * screenH * 0.5f;
		radius *= MathX.Lerp( 0.88f, 1.05f, Math.Clamp( intensity, 0f, 1.5f ) );
		return Math.Clamp( radius, 8f, screenH * 0.11f );
	}
}
