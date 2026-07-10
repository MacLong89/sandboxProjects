namespace Sandbox;

/// <summary>THORNS_EVERYTHING_DOCUMENT §12 — grid snap vs free placement.</summary>
public enum ThornsPlacementKind
{
	/// <summary>10 m module grid (Rust-style foundations/walls).</summary>
	Grid,

	/// <summary>Approximate world pose (chest, bed, Base Core).</summary>
	Free
}
