namespace Terraingen.World;

using Terraingen.GameData;
using Terraingen.Player;

/// <summary>Shared swap/merge logic for player inventory and world loot containers.</summary>
public static class ThornsContainerItemMoves
{
	public delegate ThornsItemStack SlotReader( int index );
	public delegate void SlotWriter( int index, ThornsItemStack stack );

	public static bool TrySwapOrMerge(
		SlotReader readA,
		SlotWriter writeA,
		int indexA,
		SlotReader readB,
		SlotWriter writeB,
		int indexB,
		ThornsEquipSlot requiredEquipSlot = ThornsEquipSlot.None )
	{
		var from = ThornsItemIdAliases.CanonicalizeStack( readA( indexA ) );
		if ( from.IsEmpty )
			return false;

		var to = ThornsItemIdAliases.CanonicalizeStack( readB( indexB ) );
		var def = ThornsDefinitionRegistry.GetItem( from.ItemId );
		if ( def is null )
			return false;

		if ( requiredEquipSlot != ThornsEquipSlot.None && def.EquipSlot != requiredEquipSlot )
			return false;

		if ( !to.IsEmpty && string.Equals( to.ItemId, from.ItemId, StringComparison.OrdinalIgnoreCase ) )
		{
			var max = def.MaxStack;
			var space = max - to.Count;
			if ( space <= 0 )
				return false;

			var move = Math.Min( space, from.Count );
			writeB( indexB, CopyStackWithCount( to, to.Count + move ) );
			var left = from.Count - move;
			writeA( indexA, left > 0 ? CopyStackWithCount( from, left ) : ThornsItemStack.EmptyStack );
			return true;
		}

		if ( !to.IsEmpty )
		{
			writeA( indexA, to );
			writeB( indexB, from );
			return true;
		}

		writeB( indexB, from );
		writeA( indexA, ThornsItemStack.EmptyStack );
		return true;
	}

	public static bool TrySplitHalf(
		SlotReader readFrom,
		SlotWriter writeFrom,
		int fromIndex,
		SlotReader readTo,
		SlotWriter writeTo,
		int toIndex )
	{
		var from = readFrom( fromIndex );
		if ( from.IsEmpty )
			return false;

		var half = from.Count / 2;
		if ( half <= 0 )
			return false;

		var to = readTo( toIndex );
		if ( !to.IsEmpty )
			return false;

		writeTo( toIndex, CopyStackWithCount( from, half ) );
		writeFrom( fromIndex, CopyStackWithCount( from, from.Count - half ) );
		return true;
	}

	static ThornsItemStack CopyStackWithCount( ThornsItemStack stack, int count ) =>
		ThornsInventoryWeaponState.CopyStackWithCount( stack, count );

	public static ThornsEquipSlot MapEquipSlot( ThornsContainerKind kind ) => kind switch
	{
		ThornsContainerKind.Head => ThornsEquipSlot.Head,
		ThornsContainerKind.Chest => ThornsEquipSlot.Chest,
		ThornsContainerKind.Legs => ThornsEquipSlot.Legs,
		_ => ThornsEquipSlot.None
	};

	public static int NormalizePlayerIndex( ThornsInventoryContainer inventory, ThornsContainerKind kind, int index )
	{
		if ( kind is ThornsContainerKind.Head or ThornsContainerKind.Chest or ThornsContainerKind.Legs )
			return inventory.ArmorIndexFor( kind );

		return index;
	}
}
