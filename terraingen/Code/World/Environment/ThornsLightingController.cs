namespace Terraingen.World.Environment;

using Terraingen;

[Title( "Thorns Lighting Controller" )]
[Category( "Environment" )]
[Icon( "light_mode" )]
public sealed class ThornsLightingController : Component
{
	[Property, Group( "References" )] public DirectionalLight SunLight { get; set; }
	[Property, Group( "Camera" )] public bool DriveCameraBackground { get; set; } = true;

	const float ShadowDisableIntensity = 0.035f;
	const float ShadowEnableIntensity = 0.075f;

	int _lastHash = int.MinValue;
	bool _shadowsEnabled = true;

	public void ApplyEnvironment( ThornsEnvironmentState state )
	{
		if ( SunLight is null || !SunLight.IsValid() )
			SunLight = Components.Get<DirectionalLight>( FindMode.EverythingInSelf );

		var hash = HashCode.Combine(
			state.SunColor,
			state.SunIntensity,
			state.AmbientColor,
			state.AmbientIntensity,
			state.HorizonColor );

		if ( hash == _lastHash )
			return;

		_lastHash = hash;

		if ( SunLight is not null && SunLight.IsValid() )
		{
			SunLight.Enabled = true;
			SunLight.LightColor = Scale( state.SunColor, state.SunIntensity );
			SunLight.SkyColor = Scale( state.AmbientColor, state.AmbientIntensity );
			_shadowsEnabled = _shadowsEnabled
				? state.SunIntensity > ShadowDisableIntensity
				: state.SunIntensity > ShadowEnableIntensity;
			SunLight.Shadows = _shadowsEnabled;
		}

		if ( DriveCameraBackground )
			ApplyCameraBackground( state );
	}

	void ApplyCameraBackground( ThornsEnvironmentState state )
	{
		if ( Scene is null || !Scene.IsValid() )
			return;

		var bg = ThornsSkyController.CameraBackgroundColor( state );
		if ( ThornsSceneObserver.TryGetMainCamera( Scene, out var main ) && main.IsValid() )
		{
			main.BackgroundColor = bg;
			return;
		}

		if ( Scene.Camera is not null && Scene.Camera.IsValid() )
			Scene.Camera.BackgroundColor = bg;
	}

	static Color Scale( Color color, float intensity ) => new( color.r * intensity, color.g * intensity, color.b * intensity, color.a );
}
