namespace Sandbox;

/// <summary>
/// Author a static POI in the Hammer/scene hierarchy. Host aggregates enabled markers into <see cref="ThornsPoiAuthority"/> — no POI baked in registry code (THORNS § POI system).
/// </summary>
[Title( "Thorns — POI Marker" )]
[Category( "Thorns/World" )]
[Icon( "place" )]
public sealed class ThornsPoiMarker : Component
{
	[Property] public string StableIdSync { get; set; } = "";

	public Guid StableId
	{
		get => SyncGuidParse( StableIdSync );
		set => StableIdSync = value == Guid.Empty ? "" : value.ToString( "D" );
	}

	[Property] public string CategoryKey { get; set; } = "general";

	/// <summary>When false (default), this marker does not contribute to minimap/network POI payloads — use for authored scene props. World buildings / dynamic drops set true.</summary>
	[Property] public bool ShowOnMinimap { get; set; }

	[Property] public string DisplayName { get; set; } = "Location";

	[Property] public Color MinimapColor { get; set; } = new( 0.35f, 0.82f, 1f, 0.92f );

	[Property] public float MinimapBlipDiameterPx { get; set; } = 9f;

	protected override void OnAwake()
	{
		if ( StableId == Guid.Empty )
			StableId = Guid.NewGuid();
	}

	static Guid SyncGuidParse( string s ) =>
		string.IsNullOrWhiteSpace( s ) ? Guid.Empty : (Guid.TryParse( s, out var g ) ? g : Guid.Empty);
}
