namespace DeepDive;

public enum ShopCategory
{
	All,
	Equipment,
	Tools,
	Resources
}

public sealed class ShopItemDefinition
{
	public string Id { get; init; }
	public string DisplayName { get; init; }
	public string Description { get; init; }
	public ShopCategory Category { get; init; }
	public float GoldCost { get; init; }
	public float ShellCost { get; init; }
	public string IconPath { get; init; }
	/// <summary>Hotbar tool slot to restock, or -1 for non-tool items.</summary>
	public int ToolSlot { get; init; } = -1;
	public int ChargesGranted { get; init; }
	/// <summary>If true, purchase grants shells instead of tool charges.</summary>
	public bool GrantsShells { get; init; }
	public int ShellsGranted { get; init; }
	/// <summary>Ammo refills require the base tool to be unlocked first.</summary>
	public bool RequiresUnlockedTool { get; init; }
	/// <summary>One-time unlock — cannot repurchase once owned.</summary>
	public bool UnlockOnly { get; init; }
}

public static class ShopCatalog
{
	public static IReadOnlyList<ShopItemDefinition> All { get; } =
	[
		new()
		{
			Id = "harpoon", DisplayName = "Harpoon",
			Description = "Unlock a harpoon with 6 spears.",
			Category = ShopCategory.Tools, GoldCost = 75f, ToolSlot = 1, ChargesGranted = 6,
			IconPath = "/ui/icons/tool_harpoon.png", UnlockOnly = true
		},
		new()
		{
			Id = "scanner", DisplayName = "Scanner",
			Description = "Unlock a scanner with 4 cells.",
			Category = ShopCategory.Tools, GoldCost = 60f, ToolSlot = 2, ChargesGranted = 4,
			IconPath = "/ui/icons/tool_scanner.png", UnlockOnly = true
		},
		new()
		{
			Id = "camera", DisplayName = "Camera",
			Description = "Unlock a camera with 3 film shots.",
			Category = ShopCategory.Tools, GoldCost = 55f, ToolSlot = 3, ChargesGranted = 3,
			IconPath = "/ui/icons/tool_camera.png", UnlockOnly = true
		},
		new()
		{
			Id = "oxygen_tank", DisplayName = "O2 Canister",
			Description = "Unlock O2 canisters — 2 charges.",
			Category = ShopCategory.Equipment, GoldCost = 80f, ToolSlot = 4, ChargesGranted = 2,
			IconPath = "/ui/icons/tool_oxygen.png", UnlockOnly = true
		},
		new()
		{
			Id = "drone", DisplayName = "Scout Drone",
			Description = "Unlock a scout drone with 2 batteries.",
			Category = ShopCategory.Tools, GoldCost = 90f, ToolSlot = 5, ChargesGranted = 2,
			IconPath = "/ui/icons/tool_drone.png", UnlockOnly = true
		},
		new()
		{
			Id = "bio_lure", DisplayName = "Bio Lure",
			Description = "Unlock wildlife lures — 3 charges.",
			Category = ShopCategory.Resources, GoldCost = 50f, ToolSlot = 6, ChargesGranted = 3,
			IconPath = "/ui/icons/tool_lure.png", UnlockOnly = true
		},
		new()
		{
			Id = "submersible", DisplayName = "Mini Sub",
			Description = "Unlock a docked mini-sub near the boat.",
			Category = ShopCategory.Equipment, GoldCost = 180f, ToolSlot = 7, ChargesGranted = 1,
			IconPath = "/ui/icons/tool_sub.png", UnlockOnly = true
		},
		new()
		{
			Id = "harpoon_ammo", DisplayName = "Harpoon Crate",
			Description = "+4 harpoon spears (requires harpoon).",
			Category = ShopCategory.Tools, GoldCost = 45f, ToolSlot = 1, ChargesGranted = 4,
			RequiresUnlockedTool = true,
			IconPath = "/ui/icons/tool_harpoon.png"
		},
		new()
		{
			Id = "scanner_cells", DisplayName = "Scanner Cells",
			Description = "+3 scanner charges.",
			Category = ShopCategory.Tools, GoldCost = 35f, ToolSlot = 2, ChargesGranted = 3,
			RequiresUnlockedTool = true,
			IconPath = "/ui/icons/tool_scanner.png"
		},
		new()
		{
			Id = "camera_film", DisplayName = "Camera Film",
			Description = "+3 camera shots.",
			Category = ShopCategory.Tools, GoldCost = 30f, ToolSlot = 3, ChargesGranted = 3,
			RequiresUnlockedTool = true,
			IconPath = "/ui/icons/tool_camera.png"
		},
		new()
		{
			Id = "o2_refill", DisplayName = "O2 Refill",
			Description = "+2 oxygen canisters.",
			Category = ShopCategory.Equipment, GoldCost = 40f, ToolSlot = 4, ChargesGranted = 2,
			RequiresUnlockedTool = true,
			IconPath = "/ui/icons/tool_oxygen.png"
		},
		new()
		{
			Id = "drone_cells", DisplayName = "Drone Batteries",
			Description = "+2 scout drone charges.",
			Category = ShopCategory.Tools, GoldCost = 40f, ToolSlot = 5, ChargesGranted = 2,
			RequiresUnlockedTool = true,
			IconPath = "/ui/icons/tool_drone.png"
		},
		new()
		{
			Id = "lure_pack", DisplayName = "Lure Pack",
			Description = "+3 bio lures.",
			Category = ShopCategory.Resources, GoldCost = 28f, ToolSlot = 6, ChargesGranted = 3,
			RequiresUnlockedTool = true,
			IconPath = "/ui/icons/tool_lure.png"
		},
		new()
		{
			Id = "shell_bundle", DisplayName = "Shell Bundle",
			Description = "Trade gold for upgrade shells.",
			Category = ShopCategory.Resources, GoldCost = 100f, GrantsShells = true, ShellsGranted = 25,
			IconPath = "/ui/icons/icon_shell.png"
		},
	];

	public static ShopItemDefinition Get( string id ) =>
		All.FirstOrDefault( i => i.Id == id );

	public static IEnumerable<ShopItemDefinition> Filtered( ShopCategory category )
	{
		if ( category == ShopCategory.All )
			return All;
		return All.Where( i => i.Category == category );
	}
}
