namespace Terraingen.Combat;

using System;
using Terraingen.GameData;

/// <summary>Harvest-tool combat profile ids for FP presentation (matches thorns <c>ThornsToolMeleeCombat</c>).</summary>
public static class ThornsFpToolCombat
{
	public const string CombatIdBareHands = "hands_melee";
	public const string CombatIdPrimitive = "tool_melee_primitive";
	public const string CombatIdStone = "tool_melee_stone";
	public const string CombatIdMetal = "tool_melee_metal";

	public const float ToolMeleeLightSwingCooldownSeconds = 0.5f;
	public const float PunchBaseDamage = 10f;

	public static bool IsPunchCombatId( string combatId )
	{
		var t = combatId?.Trim() ?? "";
		if ( string.IsNullOrWhiteSpace( t ) )
			return true;

		if ( string.Equals( t, CombatIdBareHands, StringComparison.OrdinalIgnoreCase ) )
			return true;

		return IsToolMeleeCombatId( t );
	}

	public static bool TreatsAsMeleeWeapon( string combatId )
	{
		var t = combatId?.Trim() ?? "";
		if ( string.IsNullOrWhiteSpace( t ) )
			return false;

		if ( string.Equals( t, "m9_bayonet", StringComparison.OrdinalIgnoreCase ) )
			return true;

		if ( string.Equals( t, CombatIdBareHands, StringComparison.OrdinalIgnoreCase ) )
			return true;

		return IsToolMeleeCombatId( t );
	}

	public static bool IsToolMeleeCombatId( string combatId )
	{
		var t = combatId?.Trim() ?? "";
		return string.Equals( t, CombatIdPrimitive, StringComparison.OrdinalIgnoreCase )
		       || string.Equals( t, CombatIdStone, StringComparison.OrdinalIgnoreCase )
		       || string.Equals( t, CombatIdMetal, StringComparison.OrdinalIgnoreCase );
	}

	public static string GetCombatDefinitionIdForToolItemId( string itemId )
	{
		if ( string.IsNullOrWhiteSpace( itemId ) )
			return CombatIdPrimitive;

		if ( !ThornsItemRegistry.TryGet( itemId, out var def ) || def.ItemType != ThornsItemType.Tool )
			return CombatIdPrimitive;

		if ( def.HarvestToolKind == ThornsHarvestToolKind.Primitive )
			return CombatIdPrimitive;

		var id = itemId.Trim();
		if ( id.Contains( "metal", StringComparison.OrdinalIgnoreCase )
		     || id.Contains( "iron", StringComparison.OrdinalIgnoreCase ) )
			return CombatIdMetal;

		if ( def.HarvestToolKind is ThornsHarvestToolKind.Axe or ThornsHarvestToolKind.Pickaxe )
			return CombatIdStone;

		return CombatIdPrimitive;
	}
}
