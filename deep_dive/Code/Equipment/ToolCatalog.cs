namespace DeepDive;

public enum ToolKind
{
	Fins,
	Harpoon,
	Scanner,
	Camera,
	OxygenTank,
	Drone,
	Lure,
	Submersible
}

public sealed class ToolDefinition
{
	public ToolKind Kind { get; init; }
	public string Id { get; init; }
	public string DisplayName { get; init; }
	public string Description { get; init; }
	public string IconPath { get; init; }
	public int DefaultCharges { get; init; }
	public bool InfiniteCharges { get; init; }
	public float CooldownSeconds { get; init; } = 0.35f;
	public bool ShowQuantity => !InfiniteCharges;
}

public static class ToolCatalog
{
	public static IReadOnlyList<ToolDefinition> All { get; } =
	[
		new()
		{
			Kind = ToolKind.Fins, Id = "fins", DisplayName = "Dive Fins",
			Description = "Equip for a swim speed boost.",
			IconPath = "/ui/icons/tool_fins.png", DefaultCharges = 0, InfiniteCharges = true, CooldownSeconds = 0f
		},
		new()
		{
			Kind = ToolKind.Harpoon, Id = "harpoon", DisplayName = "Harpoon",
			Description = "Get close and click creatures to spear them.",
			IconPath = "/ui/icons/tool_harpoon.png", DefaultCharges = 0, CooldownSeconds = 0.55f
		},
		new()
		{
			Kind = ToolKind.Scanner, Id = "scanner", DisplayName = "Scanner",
			Description = "Reveal loot and threats nearby.",
			IconPath = "/ui/icons/tool_scanner.png", DefaultCharges = 0, CooldownSeconds = 1.2f
		},
		new()
		{
			Kind = ToolKind.Camera, Id = "camera", DisplayName = "Camera",
			Description = "Photograph nearby finds for bonus pay.",
			IconPath = "/ui/icons/tool_camera.png", DefaultCharges = 0, CooldownSeconds = 1.0f
		},
		new()
		{
			Kind = ToolKind.OxygenTank, Id = "oxygen_tank", DisplayName = "O2 Canister",
			Description = "Restore oxygen mid-dive.",
			IconPath = "/ui/icons/tool_oxygen.png", DefaultCharges = 0, CooldownSeconds = 0.8f
		},
		new()
		{
			Kind = ToolKind.Drone, Id = "drone", DisplayName = "Scout Drone",
			Description = "Pulse the minimap with nearby contacts.",
			IconPath = "/ui/icons/tool_drone.png", DefaultCharges = 0, CooldownSeconds = 1.5f
		},
		new()
		{
			Kind = ToolKind.Lure, Id = "lure", DisplayName = "Bio Lure",
			Description = "Distract wildlife for a short time.",
			IconPath = "/ui/icons/tool_lure.png", DefaultCharges = 0, CooldownSeconds = 1.0f
		},
		new()
		{
			Kind = ToolKind.Submersible, Id = "submersible", DisplayName = "Mini Sub",
			Description = "Unlock a docked mini-sub — board it near the boat (E).",
			IconPath = "/ui/icons/tool_sub.png", DefaultCharges = 0, InfiniteCharges = false, CooldownSeconds = 0.35f
		},
	];

	public const int SlotCount = 8;

	public static ToolDefinition Get( ToolKind kind ) =>
		All.FirstOrDefault( t => t.Kind == kind );

	public static ToolDefinition GetBySlot( int slot ) =>
		slot >= 0 && slot < All.Count ? All[slot] : null;
}
