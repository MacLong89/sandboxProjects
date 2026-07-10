namespace Sandbox;

/// <summary>Semantic channels for mating sockets and plugs — extend for doors, tier upgrades (THORNS_EVERYTHING_DOCUMENT §12 building).</summary>
public enum ThornsSnapChannel
{
	/// <summary>Another foundation mating a full edge (Rust-style flank expansion).</summary>
	FoundationEdgeMate = 1,

	/// <summary>Upright wall/window/frame/ramp seated on perimeter of a slab.</summary>
	WallSeatOnFoundationEdge = 2,

	/// <summary>Hinged door slab into authored door-frame opening.</summary>
	DoorPanelIntoFrame = 3,

	/// <summary>Louver/window glazing into authored frame aperture (future tiers).</summary>
	WindowPanelIntoFrame = 4,

	/// <summary>Floor/ceiling slab snapped to the top face of an upright piece (upper storey).</summary>
	FloorSeatOnWallTop = 5,

	/// <summary>Ramp seated on foundation top surface — full cell XY alignment (not edge-straddling).</summary>
	RampSeatOnFoundationTop = 6,
}
