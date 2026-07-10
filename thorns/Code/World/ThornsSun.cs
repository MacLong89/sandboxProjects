namespace Sandbox;

/// <summary>
/// Scene compatibility alias — same implementation as <see cref="ThornsCelestialSystem"/>.
/// Do not add alongside <see cref="ThornsCelestialSystem"/> on one object.
/// </summary>
[Title( "Thorns — Celestial System (legacy type name)" )]
[Obsolete( "Use ThornsCelestialSystem on new prefabs." )]
public sealed class ThornsSun : ThornsCelestialSystem;
