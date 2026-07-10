namespace Terraingen.Rendering;

using Sandbox;

/// <summary>
/// Legacy scene alias — gameplay atmosphere is driven by
/// <see cref="World.Environment.ThornsAtmosphereController"/> via <see cref="World.Environment.ThornsEnvironmentDirector"/>.
/// </summary>
[Title( "Thorns Atmosphere (legacy scene)" )]
[Category( "Thorns/Rendering" )]
[Icon( "cloud" )]
public sealed class ThornsAtmosphere : Component;
