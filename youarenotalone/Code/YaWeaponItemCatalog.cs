namespace Sandbox;

public enum YaItemType
{
	Weapon,
	Ammo
}

/// <summary>Minimal item defs for M4 / shotgun / bayonet + basic ammo (ported from Thorns item↔weapon wiring).</summary>
public static class YaWeaponItemCatalog
{
	public sealed record YaItemDefinition(
		string Id,
		string DisplayName,
		int MaxStack,
		YaItemType ItemType,
		string CombatWeaponDefinitionId = "",
		string ViewModelAsset = "",
		string WorldModelAsset = "",
		string AmmoTypeId = "" );

	public static bool IsUsableConsumable( YaItemDefinition _ ) => false;

	public static bool TryGet( string itemId, out YaItemDefinition def )
	{
		def = default;
		if ( string.IsNullOrWhiteSpace( itemId ) )
			return false;
		return _byId.TryGetValue( itemId.Trim(), out def );
	}

	static readonly System.Collections.Generic.Dictionary<string, YaItemDefinition> _byId =
		new( System.StringComparer.OrdinalIgnoreCase )
		{
			["m4"] = new YaItemDefinition(
				"m4",
				"M4",
				1,
				YaItemType.Weapon,
				CombatWeaponDefinitionId: "m4",
				ViewModelAsset: YaViewModelController.M4FirstPersonViewmodelPath,
				WorldModelAsset: YaViewModelController.M4WorldModelPath ),
			["shotgun"] = new YaItemDefinition(
				"shotgun",
				"Shotgun",
				1,
				YaItemType.Weapon,
				CombatWeaponDefinitionId: "shotgun",
				ViewModelAsset: YaViewModelController.ShotgunFirstPersonViewmodelPath,
				WorldModelAsset: YaViewModelController.ShotgunWorldModelPath ),
			["m9_bayonet"] = new YaItemDefinition(
				"m9_bayonet",
				"M9 Bayonet",
				1,
				YaItemType.Weapon,
				CombatWeaponDefinitionId: "m9_bayonet",
				ViewModelAsset: YaViewModelController.BayonetM9FirstPersonViewmodelPath,
				WorldModelAsset: YaViewModelController.BayonetM9WorldModelPath ),
			["ammo_basic"] = new YaItemDefinition(
				"ammo_basic",
				"Basic Ammo",
				999,
				YaItemType.Ammo,
				AmmoTypeId: "ammo_basic" )
		};
}
