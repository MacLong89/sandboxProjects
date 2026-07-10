namespace Terraingen.World.Environment;

[Title( "Thorns Celestial Controller" )]
[Category( "Environment" )]
[Icon( "wb_sunny" )]
public sealed class ThornsCelestialController : Component
{
	[Property, Group( "References" )] public DirectionalLight SunLight { get; set; }
	[Property, Group( "References" )] public GameObject SunVisual { get; set; }
	[Property, Group( "References" )] public GameObject MoonVisual { get; set; }
	[Property, Group( "Moon" )] public bool EnableMoon { get; set; } = true;
	[Property, Group( "Visuals" ), Range( 1000f, 250000f )] public float VisualDistance { get; set; } = 80000f;
	[Property, Group( "Visuals" ), Range( 100f, 10000f )] public float SunDiscSize { get; set; } = 2200f;

	public void ApplyEnvironment( ThornsEnvironmentState state )
	{
		if ( SunLight is null || !SunLight.IsValid() )
			SunLight = Components.Get<DirectionalLight>( FindMode.EverythingInSelf );

		if ( SunLight is not null && SunLight.IsValid() )
			SunLight.GameObject.WorldRotation = state.SunLightRotation;

		PlaceVisual( SunVisual, state.SunDirection, state.SunFactor > 0.02f );
		PlaceVisual( MoonVisual, state.MoonDirection, EnableMoon && state.NightFactor > 0.08f );
	}

	void PlaceVisual( GameObject visual, Vector3 direction, bool visible )
	{
		if ( visual is null || !visual.IsValid() )
			return;

		visual.Enabled = visible;
		if ( !visible )
			return;

		var camera = Scene is not null && Scene.IsValid() ? Scene.Camera : default;
		var origin = camera.IsValid() ? camera.GameObject.WorldPosition : Vector3.Zero;
		visual.WorldPosition = origin + direction.Normal * VisualDistance;
		visual.WorldRotation = Rotation.LookAt( -direction.Normal, Vector3.Up );
		visual.WorldScale = new Vector3( SunDiscSize );
	}
}
