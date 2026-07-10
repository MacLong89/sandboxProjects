namespace Sandbox;

/// <summary>Shared presentation strings for mirrored inventory slots (client mirror only).</summary>
public static class ThornsUiInventoryFormatting
{
	public static string SlotPrimaryLine( ThornsInventorySlotNet net )
	{
		if ( string.IsNullOrWhiteSpace( net.ItemId ) || net.Quantity <= 0 )
			return "";
		return ThornsItemRegistry.ResolveDisplayName( net.ItemId );
	}

	public static string SlotSecondaryLine( ThornsInventorySlotNet net )
	{
		if ( string.IsNullOrWhiteSpace( net.ItemId ) || net.Quantity <= 0 )
			return "";
		var q = $"×{net.Quantity}";
		if ( net.HasDurability != 0 )
			return $"{q}  {net.Durability:F0} dur";
		return q;
	}

	/// <summary>Compact glyph for inspect hero, backpack slots, hotbar fallback (matches <c>ThornsUiItemInspectPanel</c>).</summary>
	public static string ItemGlyph( string itemId )
	{
		if ( string.IsNullOrWhiteSpace( itemId ) )
			return "—";
		return itemId.Trim().ToLowerInvariant() switch
		{
			"m4" => "🔫",
			"mp5" => "🔫",
			"shotgun" => "🔫",
			"sniper" => "🔫",
			"m9_bayonet" => "🔪",
			// Geometric glyph — colored emoji often fails to render at large inspect-hero sizes in UI font stacks.
			"pistol_ammo" => "\u25C6",
			"shotgun_ammo" => "\u25C6",
			"smg_ammo" => "\u25C6",
			"rifle_ammo" => "\u25C6",
			"sniper_ammo" => "\u25C6",
			"bandage" => "🩹",
			"kevlar_helmet" => "🪖",
			"kevlar_chest" => "🦺",
			"kevlar_pants" => "👖",
			"wood" => "🪵",
			"stone" => "🪨",
			"apple" => "🍎",
			"water" => "💧",
			"cloth" => "🧵",
			"metal" => "⚙",
			"axe" => "🪓",
			"pickaxe" => "⛏",
			"medkit_field" => "🏥",
			"morphine_pen" => "💉",
			"ration_pack" => "📦",
			"electrolyte_drink" => "🥤",
			"canned_stew" => "🥘",
			"field_rations" => "🍱",
			"storage_chest_kit" => "📦",
			"campfire_kit" => "🔥",
			"workbench_kit" => "🔧",
			"bed_kit" => "🛏",
			"chair_kit" => "🪑",
			"couch_kit" => "🛋",
			"cabinet_kit" => "🗄",
			"kitchen_fridge_kit" => "🍳",
			"fridge_kit" => "🧊",
			_ => "⬜"
		};
	}
}
