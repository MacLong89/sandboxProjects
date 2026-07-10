namespace Terraingen.UI;

using Terraingen.GameData;

/// <summary>Builds item definitions from legacy PNGs under <c>ui/icons/</c> (optional inventory art).</summary>
public static class ThornsIconDrivenCatalog
{
	public static IEnumerable<ThornsItemDefinition> BuildItems()
	{
		ThornsDefinitionRegistry.EnsureInitialized();
		ThornsIconManifest.Refresh();

		foreach ( var id in ThornsIconManifest.GetDiscoveredItemIds() )
		{
			if ( string.IsNullOrWhiteSpace( id ) || ThornsItemIdAliases.IsAttachmentIconOnlyStem( id ) )
				continue;

			var canonical = ThornsItemIdAliases.Canonicalize( id );
			if ( !string.Equals( id, canonical, StringComparison.OrdinalIgnoreCase ) )
				continue;

			if ( ThornsDefinitionRegistry.GetItem( canonical ) is not null )
				continue;

			var category = GuessCategory( id );
			yield return new ThornsItemDefinition
			{
				Id = id,
				DisplayName = TitleFromId( id ),
				Description = $"Item ({id}).",
				Category = category,
				EquipSlot = GuessEquipSlot( id, category ),
				MaxStack = GuessMaxStack( id ),
				IconPath = ThornsIconManifest.ResolveItemPath( id ),
				WeightKg = 0.5f
			};
		}
	}

	public static IEnumerable<ThornsSkillDefinition> BuildSkillsMissingFromUpgrades()
	{
		ThornsIconManifest.Refresh();
		var known = new HashSet<string>( ThornsUpgradeDefinitions.All.Select( s => s.Id ),
			StringComparer.OrdinalIgnoreCase );

		foreach ( var id in ThornsIconManifest.GetDiscoveredSkillIds() )
		{
			if ( string.IsNullOrWhiteSpace( id ) || known.Contains( id ) )
				continue;

			yield return new ThornsSkillDefinition
			{
				Id = id,
				DisplayName = TitleFromId( id ),
				Description = "Skill discovered from icon.",
				Category = ThornsSkillCategory.Persistence,
				Tier = 1,
				MaxRank = 1,
				BasePointCost = 1,
				IconPath = ThornsIconManifest.ResolveSkillPath( id ),
				RankBonuses = new List<string> { "+1 rank" }
			};
		}
	}

	static string TitleFromId( string id )
	{
		if ( string.IsNullOrWhiteSpace( id ) )
			return "Unknown";

		if ( id.EndsWith( "_kit", StringComparison.OrdinalIgnoreCase ) )
			id = id[..^4];

		var parts = id.Replace( '-', '_' ).Split( '_', StringSplitOptions.RemoveEmptyEntries );
		return string.Join( " ", parts.Select( p =>
			p.Length <= 3 ? p.ToUpperInvariant() : char.ToUpperInvariant( p[0] ) + p[1..].ToLowerInvariant() ) );
	}

	static ThornsEquipSlot GuessEquipSlot( string id, ThornsItemCategory category )
	{
		if ( category != ThornsItemCategory.Armor )
			return ThornsEquipSlot.None;

		var s = id.ToLowerInvariant();
		if ( s.Contains( "head" ) || s.Contains( "helmet" ) )
			return ThornsEquipSlot.Head;
		if ( s.Contains( "leg" ) || s.Contains( "pants" ) )
			return ThornsEquipSlot.Legs;
		if ( s.Contains( "chest" ) || s.Contains( "vest" ) || s.Contains( "torso" ) )
			return ThornsEquipSlot.Chest;

		return ThornsEquipSlot.Chest;
	}

	static ThornsItemCategory GuessCategory( string id )
	{
		var s = id.ToLowerInvariant();
		if ( s.Contains( "helmet" ) || s.Contains( "vest" ) || s.Contains( "pants" )
		     || s.Contains( "head" ) || s.Contains( "chest" ) || s.Contains( "legs" )
		     || s.Contains( "armor" ) || s.Contains( "kevlar" ) || s.Contains( "makeshift" ) )
			return ThornsItemCategory.Armor;
		if ( s is "m4" or "rifle" or "bow" || s.Contains( "gun" ) || s.Contains( "weapon" ) )
			return ThornsItemCategory.Weapon;
		if ( s.Contains( "pickaxe" ) || s.Contains( "hatchet" ) )
			return ThornsItemCategory.Tool;
		if ( s.Contains( "pick" ) || s.Contains( "axe" ) || s.Contains( "tool" ) )
			return ThornsItemCategory.Tool;
		if ( s is "food" or "water" or "bandage" or "medkit" or "apple" or "stew" or "meat"
		     || s.Contains( "water" ) || s.Contains( "ration" ) || s.Contains( "ammo" ) || s.Contains( "morphine" ) )
			return ThornsItemCategory.Consumable;
		if ( s.Contains( "foundation" ) || s.Contains( "wall" ) || s.Contains( "door" ) || s.Contains( "campfire" ) || s.Contains( "bed" ) || s.Contains( "workbench" ) )
			return ThornsItemCategory.Resource;
		if ( s.Contains( "_kit" ) )
			return ThornsItemCategory.Resource;
		return ThornsItemCategory.Resource;
	}

	static int GuessMaxStack( string id ) => GuessCategory( id ) switch
	{
		ThornsItemCategory.Weapon or ThornsItemCategory.Tool or ThornsItemCategory.Armor => 1,
		ThornsItemCategory.Consumable => 20,
		_ => 250
	};
}
