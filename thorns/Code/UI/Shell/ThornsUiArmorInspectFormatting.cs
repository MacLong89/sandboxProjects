namespace Sandbox;

/// <summary>Inspect + HUD tinting for armor instances (definition + <see cref="ThornsGearRoll"/> DR payload).</summary>
public static class ThornsUiArmorInspectFormatting
{
	public static void ResolveArmorRoll( ThornsInventorySlotNet net, out ThornsLootRarity rarity, out float drContributionMul )
	{
		if ( ThornsGearRoll.TryParseArmor( net.ArmorRollPayload ?? "", out rarity, out drContributionMul ) )
			return;
		rarity = ThornsLootRarity.Common;
		drContributionMul = 1f;
	}

	public static bool TryGetArmorInventoryTitleTint( ThornsInventorySlotNet net, out Color tint )
	{
		tint = default;
		if ( string.IsNullOrWhiteSpace( net.ItemId ) || net.Quantity <= 0 )
			return false;
		if ( !ThornsItemRegistry.TryGet( net.ItemId, out var def ) || def.ItemType != ThornsItemType.Armor )
			return false;
		ResolveArmorRoll( net, out var r, out _ );
		tint = r.TintApprox();
		return true;
	}

	public static Color ResolveAbbrevToolbarTint( ThornsInventorySlotNet net )
	{
		ResolveArmorRoll( net, out var r, out _ );
		return r.TintApprox();
	}
}
