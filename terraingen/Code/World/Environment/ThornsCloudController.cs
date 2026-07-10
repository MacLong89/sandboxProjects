namespace Terraingen.World.Environment;

[Title( "Thorns Cloud Controller" )]
[Category( "Environment" )]
[Icon( "cloud_queue" )]
public sealed class ThornsCloudController : Component
{
	[Property, Group( "References" )] public ThornsSkyController Sky { get; set; }
	[Property, Group( "References" )] public ThornsCloudBillboardLayer Billboards { get; set; }
	[Property, Group( "Wind" )] public Vector2 WindDirection { get; set; } = new( 1f, 0.25f );
	[Property, Group( "Wind" ), Range( 0f, 2f )] public float WindSpeed { get; set; } = 1f;
	[Property, Group( "Sky Haze" ), Range( 0f, 1f ), Title( "Procedural sky haze opacity" )]
	public float SkyHazeOpacityScale { get; set; } = 0f;

	int _lastHash = int.MinValue;

	public void ApplyEnvironment( ThornsEnvironmentState state )
	{
		if ( Sky is null || !Sky.IsValid() )
			Sky = Components.Get<ThornsSkyController>( FindMode.EverythingInSelfAndParent );

		if ( Billboards is null || !Billboards.IsValid() )
			Billboards = Components.Get<ThornsCloudBillboardLayer>( FindMode.EverythingInSelfAndParent );

		if ( Billboards is not null && Billboards.IsValid() )
		{
			Billboards.WindDirection = WindDirection;
			Billboards.WindSpeed = WindSpeed;
			Billboards.ApplyEnvironment( state );
		}

		var drift = state.CloudDrift * WindSpeed;
		var hash = HashCode.Combine( state.CloudColor, state.CloudOpacity, (int)(drift * 1000f), WindDirection, SkyHazeOpacityScale );
		if ( hash == _lastHash )
			return;

		_lastHash = hash;

		var material = Sky?.MaterialInstance;
		if ( material is not null && material.IsValid() && material.Attributes is not null )
		{
			material.Attributes.Set( "CloudTint", state.CloudColor );
			material.Attributes.Set( "CloudOpacity", state.CloudOpacity * SkyHazeOpacityScale );
			material.Attributes.Set( "CloudDrift", drift );
		}
	}
}
