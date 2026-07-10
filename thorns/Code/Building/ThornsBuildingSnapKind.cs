namespace Sandbox;

/// <summary>Rust-style module placement profile (horizontal plane = XY, up = Z).</summary>
public enum ThornsBuildingSnapKind
{
	None,
	/// <summary>100×100 footprint slab — thickness <see cref="ThornsBuildingModule.FloorThickness"/>.</summary>
	Foundation,
	/// <summary>Upright 100-high face — thickness <see cref="ThornsBuildingModule.WallThickness"/>.</summary>
	Wall,
	Window,
	DoorFrame,
	DoorPanel,
	Ramp,
}
