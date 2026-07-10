namespace Terraingen.World.Environment;

[Title( "Thorns Atmosphere Controller" )]
[Category( "Environment" )]
[Icon( "cloud" )]
public sealed class ThornsAtmosphereController : Component
{
	[Property, Group( "References" )] public GradientFog GradientFog { get; set; }
	[Property, Group( "Fog" ), Range( 0f, 0.25f )] public float MaxFogAlpha { get; set; } = 0.12f;
	[Property, Group( "Fog" ), Range( 0.2f, 4f )] public float FalloffExponent { get; set; } = 1.15f;

	int _lastHash = int.MinValue;

	public void ApplyEnvironment( ThornsEnvironmentState state )
	{
		if ( GradientFog is null || !GradientFog.IsValid() )
			GradientFog = Components.Get<GradientFog>( FindMode.EverythingInSelf );

		if ( GradientFog is null || !GradientFog.IsValid() )
			return;

		var hash = HashCode.Combine( state.FogColor, state.FogDensity, state.FogStartDistance, state.FogEndDistance );
		if ( hash == _lastHash )
			return;

		_lastHash = hash;
		GradientFog.Enabled = state.FogDensity > 0.001f;
		GradientFog.Color = state.FogColor.WithAlpha( Math.Min( MaxFogAlpha, state.FogDensity ) );
		GradientFog.StartDistance = state.FogStartDistance;
		GradientFog.EndDistance = state.FogEndDistance;
		GradientFog.FalloffExponent = FalloffExponent;
		GradientFog.Height = 0f;
		GradientFog.VerticalFalloffExponent = 0f;
	}
}
