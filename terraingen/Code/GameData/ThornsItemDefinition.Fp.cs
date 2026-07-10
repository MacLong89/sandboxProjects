namespace Terraingen.GameData;

public sealed partial class ThornsItemDefinition
{
	public ThornsItemType ItemType => Category switch
	{
		ThornsItemCategory.Tool => ThornsItemType.Tool,
		ThornsItemCategory.Weapon => ThornsItemType.Weapon,
		ThornsItemCategory.Consumable => ThornsItemType.Consumable,
		ThornsItemCategory.Ammo => ThornsItemType.Ammo,
		ThornsItemCategory.Armor => ThornsItemType.Armor,
		ThornsItemCategory.Blueprint => ThornsItemType.Blueprint,
		_ => ThornsItemType.Resource
	};
}
