namespace Terraingen.UI.Core;

using Terraingen.Combat;
using Terraingen.GameData;
using Terraingen.Player;

/// <summary>Client inspect selection — weapon attachment slots read this when resolving equip targets.</summary>
public static class ThornsInventoryInspectContext
{
	public static ThornsContainerKind? WeaponContainer { get; private set; }
	public static int WeaponIndex { get; private set; } = -1;

	public static void SyncWeaponInspect( ThornsContainerKind? container, int index )
	{
		WeaponContainer = container;
		WeaponIndex = index;
	}

	public static void ClearWeaponInspect()
	{
		WeaponContainer = null;
		WeaponIndex = -1;
	}

	public static bool TryGetWeaponSlot( out ThornsContainerKind container, out int index )
	{
		if ( WeaponContainer is null || WeaponIndex < 0 )
		{
			container = default;
			index = -1;
			return false;
		}

		container = WeaponContainer.Value;
		index = WeaponIndex;
		return true;
	}

	public static string ResolveCombatId()
	{
		if ( !TryGetWeaponSlot( out var container, out var weaponIndex ) || !ThornsUiClientState.HasSnapshot )
			return "";

		var idx = container is ThornsContainerKind.Head or ThornsContainerKind.Chest or ThornsContainerKind.Legs
			? 0
			: weaponIndex;
		var dto = ThornsUiClientState.Snapshot.Inventory.Slots.FirstOrDefault( s =>
			s.Container == container && s.Index == idx );
		if ( dto is null || string.IsNullOrWhiteSpace( dto.ItemId ) )
			return "";

		if ( !ThornsItemRegistry.TryGet( dto.ItemId, out var weaponDef ) )
			return "";

		return ThornsInventoryWeaponState.ResolveCombatId( weaponDef, dto.ItemId );
	}
}
