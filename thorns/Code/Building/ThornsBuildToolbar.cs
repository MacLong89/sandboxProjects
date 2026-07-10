namespace Sandbox;

/// <summary>Active tool when the build dock is open (place ghost vs modify existing).</summary>
public enum ThornsBuildToolMode
{
	Place,
	Remove,
	Upgrade
}

/// <summary>Categories shown on the build-mode dock (replaces combat hotbar while building).</summary>
public enum ThornsBuildToolbarSlotKind
{
	PlaceStructure,
	Remove,
	Upgrade
}

/// <summary>One slot on the build toolbar (maps to place/remove/upgrade).</summary>
public sealed record ThornsBuildToolbarEntry(
	int SlotIndex,
	string Label,
	string IconGlyph,
	ThornsBuildToolbarSlotKind Kind,
	string StructureDefId );

/// <summary>Fixed layout — order matches hotbar-style keys 1…7 (chest kit is crafted only; place via hotbar, not here).</summary>
public static class ThornsBuildToolbar
{
	public static readonly ThornsBuildToolbarEntry[] Entries =
	{
		new ThornsBuildToolbarEntry( 0, "Floor", "▣", ThornsBuildToolbarSlotKind.PlaceStructure, "wood_foundation" ),
		new ThornsBuildToolbarEntry( 1, "Wall", "▯", ThornsBuildToolbarSlotKind.PlaceStructure, "wood_wall" ),
		new ThornsBuildToolbarEntry( 2, "Window", "▢", ThornsBuildToolbarSlotKind.PlaceStructure, "wood_window" ),
		new ThornsBuildToolbarEntry( 3, "Door", "⌂", ThornsBuildToolbarSlotKind.PlaceStructure, "wood_doorframe" ),
		new ThornsBuildToolbarEntry( 4, "Ramp", "◿", ThornsBuildToolbarSlotKind.PlaceStructure, "wood_ramp" ),
		new ThornsBuildToolbarEntry( 5, "Remove", "✕", ThornsBuildToolbarSlotKind.Remove, "" ),
		new ThornsBuildToolbarEntry( 6, "Upgrade", "↑", ThornsBuildToolbarSlotKind.Upgrade, "" )
	};

	public static int SlotCount => Entries.Length;
}
