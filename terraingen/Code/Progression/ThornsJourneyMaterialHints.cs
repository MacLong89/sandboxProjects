namespace Terraingen.GameData;

/// <summary>Surfaces missing ingredients for pinned journey goals on the HUD.</summary>
public static class ThornsJourneyMaterialHints
{
	public static string GetHint( string goalId )
	{
		if ( string.IsNullOrWhiteSpace( goalId ) )
			return "";

		return goalId.ToLowerInvariant() switch
		{
			"goal_craft_bed_kit" => "Need: 50 wood, 10 cloth (gather from plants)",
			"goal_craft_workbench_kit" => "Need: 60 wood, 25 stone, 5 metal ore (mine with pickaxe)",
			"goal_craft_pick" => "Need: 15 wood, 20 stone",
			"goal_craft_hatchet" => "Need: 20 wood, 15 stone",
			"goal_craft_campfire_kit" => "Need: 40 wood, 10 stone",
			"goal_craft_storage_kit" => "Need: 45 wood, 10 stone",
			"goal_cloth_fiber" => "Gather cloth from plants and salvage",
			"goal_metal_ore" => "Mine metal ore nodes with a pickaxe",
			"goal_visit_town" => "Follow the compass marker to the nearest town",
			"goal_acquire_weapon" => "Craft a stone hatchet at hand (20 wood, 15 stone)",
			"goal_bare_hands_gather" => "Equip nothing and LMB trees or stone nodes to punch-gather",
			"goal_explore_controls" => "Press I, J, K, M, and B to open menus — or Tab for the full menu",
			"goal_radio_shop" => "Find a radio table in town and press E",
			"goal_discover_guild_outpost" => "Follow map markers — rival guild banners mark outposts",
			_ => ""
		};
	}
}
