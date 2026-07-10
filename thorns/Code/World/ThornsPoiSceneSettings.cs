namespace Sandbox;

/// <summary>
/// Scene-authored tuning for POI-derived map bounds — no POI coordinates here (THORNS static POI markers are separate components).
/// Place on the same object as <see cref="ThornsGameManager"/> (or anywhere in scene); host reads once when building replica data.
/// </summary>
[Title( "Thorns — POI / Minimap Bounds" )]
[Category( "Thorns/World" )]
[Icon( "map" )]
public sealed class ThornsPoiSceneSettings : Component
{
	/// <summary>When true (and <see cref="UseTerrainWorldBounds"/> is off), minimap zooms to POI bbox — buildings look scattered. Prefer terrain bounds.</summary>
	[Property] public bool DeriveHorizontalBoundsFromPois { get; set; }

	/// <summary>Minimap and terrain overview use full <see cref="ThornsTerrainSystem"/> playable width/depth.</summary>
	[Property] public bool UseTerrainWorldBounds { get; set; } = true;

	[Property] public float BoundsPaddingWorld { get; set; } = 650f;

	[Property] public Vector2 ManualHorizontalMin { get; set; } = new( -9000f, -9000f );

	[Property] public Vector2 ManualHorizontalMax { get; set; } = new( 9000f, 9000f );

	[Property] public Vector2 EmptyMapFallbackHalfExtent { get; set; } = new( 9000f, 9000f );
}
