namespace Terraingen.UI.Core;

using Terraingen.Player;

/// <summary>Shared UI sizing — menu panels use the 1080p baseline; gameplay HUD uses 2× touch targets.</summary>
public static class ThornsUiMetrics
{
	public const int IconScale = 2;

	public static int Px( int baseSize ) => baseSize * IconScale;

	public const int MenuScreenSideInset = 0;
	public const int MenuScreenEdgeInset = 0;
	public const int MenuTopbarHeight = 72;
	public const int TabIcon = 48;
	public const int BrandIcon = 42;
	public const int CloseIcon = 42;

	// Menu panels — concept inventory layout (6×5 grid, larger touch targets)
	public const int MenuInventoryColumns = 6;
	public const int MenuInventoryRows = 5;
	public const int MenuInventoryGridGap = 6;
	public const int MenuItemSlot = 72;
	public const int MenuHotbarSlot = 72;
	public const int MenuInspectIcon = 128;
	public const int MenuInspectIconWrap = 152;
	public const int MenuWeaponInspectPanelHeight = 208;
	public const int MenuInspectPanelHeight = 272;

	public static int MenuInventoryGridWidth =>
		MenuInventoryColumns * MenuItemSlot + (MenuInventoryColumns - 1) * MenuInventoryGridGap;

	public static int MenuHotbarGridWidth =>
		ThornsInventoryContainer.HotbarSlotCount * MenuHotbarSlot
		+ (ThornsInventoryContainer.HotbarSlotCount - 1) * MenuInventoryGridGap;
	public const int MenuExplorerPortraitHeight = 88;
	public const int MenuExplorerPreviewMaxHeight = 118;
	public const int MenuArmorSlot = MenuItemSlot * 2;
	public const int MenuArmorHeroMinHeight = 220;
	public const int MenuStatIcon = 18;
	public const int MenuStatSegmentHeight = 7;
	public const int MenuCraftCatIcon = 46;
	public const int MenuRecipeRowIcon = 46;
	public const int MenuRecipeDetailIcon = 64;
	public const int MenuDragGhost = 72;
	public const int MenuSkillCategoryIcon = 56;
	public const int MenuSkillNodeIcon = 96;
	public const int MenuSkillNodeLockIcon = 80;
	public const int MenuSkillNodeDiamond = 168;
	public const int MenuSkillDetailIcon = 36;
	public const int MenuSkillDetailDiamond = 64;
	public const int MenuSkillDetailDiamondWrap = 88;
	public const int MenuJournalSectionIcon = 34;
	public const int MenuJournalListIcon = 40;
	public const int MenuJournalRewardIcon = 48;
	public const int MenuJournalDetailIcon = 96;
	public const int MenuTamePortrait = 48;
	public const int MenuTameSpeciesIcon = 44;
	public const int MenuTameTraitIcon = 28;
	public const int MenuTameCommandIcon = 28;
	public const int MenuTameStatIcon = 44;

	// Gameplay HUD — 2× menu baseline (IconScale)
	public const int ItemSlot = 144;
	/// <summary>Gameplay hotbar slot size — scaled at runtime via <see cref="Terraingen.UI.ThornsHudTheme.HotbarSlotPx"/>.</summary>
	public const int HotbarSlotDesign = 116;
	public const int StatIcon = 40;
	public const int DragGhost = 128;

	/// <summary>Gameplay hotbar layout baselines — use <see cref="Terraingen.UI.ThornsHudTheme"/> scaled properties in HUD code.</summary>
	public const int HotbarRootWidthDesign = 1200;
	public const int HotbarXpWidthDesign = 1196;
	public const int HotbarMarginLeftDesign = -600;
	public const int HotbarStackHeightDesign = 236;
	/// <summary>Minimap + pinned goal keep the original compact column width (see <see cref="Terraingen.UI.ThornsHudTheme.RightHudColumnWidthPx"/>).</summary>
	public const int RightHudColumnWidth = 340;
	public const int HudPinnedGoalNotificationIcon = 56;
	public const int HudPromptNotificationIcon = 56;

	// World container overlay — compact slots so 5× inventory + 8 hotbar fit half the panel.
	public const int WorldContainerSlot = 48;
	public const int WorldContainerGridGap = 6;
	public const int WorldContainerInventoryColumns = 5;

	public static int WorldContainerInventoryGridWidth =>
		WorldContainerInventoryColumns * WorldContainerSlot
		+ (WorldContainerInventoryColumns - 1) * WorldContainerGridGap;

	public static int WorldContainerHotbarWidth =>
		ThornsInventoryContainer.HotbarSlotCount * WorldContainerSlot
		+ (ThornsInventoryContainer.HotbarSlotCount - 1) * WorldContainerGridGap;

	public static int WorldContainerStorageGridWidth( int columns ) =>
		columns * WorldContainerSlot + (columns - 1) * WorldContainerGridGap;

	/// <summary>World loot / furniture container overlay — sized for dual-pane grids without excess margins.</summary>
	public const int WorldContainerMaxWidthPx = 1200;

	/// <summary>Campfire forge overlay — matches container width with room for smelt controls.</summary>
	public const int CampfireOverlayMinHeightPx = 520;
}
