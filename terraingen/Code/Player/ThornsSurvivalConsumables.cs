namespace Terraingen.Player;

using Terraingen.GameData;

/// <summary>Food, water, and medical items the player can consume from inventory or hotbar.</summary>
public static class ThornsSurvivalConsumables
{
	public const float ConsumeHoldSeconds = 1f;

	static readonly string[] Ids =
	{
		"apple", "field_rations", "canned_stew", "raw_meat", "food",
		"water_bottle", "clean_water", "water", "electrolytes",
		"bandage", "medkit", "morphine_pen"
	};

	public static bool IsConsumable( string itemId )
	{
		if ( string.IsNullOrWhiteSpace( itemId ) )
			return false;

		foreach ( var id in Ids )
		{
			if ( string.Equals( id, itemId, StringComparison.OrdinalIgnoreCase ) )
				return true;
		}

		return false;
	}

	public static string GetDisplayName( string itemId )
	{
		if ( ThornsItemRegistry.TryGet( itemId, out var def ) && !string.IsNullOrWhiteSpace( def.DisplayName ) )
			return def.DisplayName;

		return itemId;
	}
}
