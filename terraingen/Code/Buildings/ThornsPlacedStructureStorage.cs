namespace Terraingen.Buildings;

using Terraingen.GameData;
using Terraingen.Multiplayer;
using Terraingen.Player;

/// <summary>Authoritative storage inventory for placed chests (host-only mutation).</summary>
[Title( "Thorns Structure Storage" )]
[Category( "Terrain/Buildings" )]
public sealed class ThornsPlacedStructureStorage : Component
{
	public const int SlotCount = 24;

	readonly ThornsItemStack[] _slots = new ThornsItemStack[SlotCount];

	public static ThornsPlacedStructureStorage EnsureOn( ThornsPlacedBuildStructure structure )
	{
		if ( structure is null || !structure.IsValid() )
			return null;

		var storage = structure.Components.Get<ThornsPlacedStructureStorage>();
		if ( storage.IsValid() )
			return storage;

		return structure.Components.Create<ThornsPlacedStructureStorage>();
	}

	public static bool IsStorageStructure( string structureId ) =>
		string.Equals( structureId, "storage_chest", StringComparison.OrdinalIgnoreCase );

	public static bool IsPlayerPortableStructure( string structureId )
	{
		if ( string.IsNullOrWhiteSpace( structureId ) )
			return false;

		return ThornsPlayerBuildingDefinitions.TryGet( structureId, out var def )
		       && def.PlacementKind == ThornsPlayerBuildPlacementKind.Free;
	}

	public static int StorageSlotCount( string structureId ) =>
		IsStorageStructure( structureId ) ? SlotCount : 0;

	public ThornsPersistentStructureStorageDto CaptureDto()
	{
		var dto = new ThornsPersistentStructureStorageDto { Slots = new List<ThornsPersistentItemStackDto>() };
		for ( var i = 0; i < SlotCount; i++ )
		{
			var stack = _slots[i];
			if ( stack.IsEmpty )
				continue;

			dto.Slots.Add( new ThornsPersistentItemStackDto
			{
				SlotIndex = i,
				ItemId = stack.ItemId,
				Count = stack.Count,
				ItemTier = stack.ItemTier,
				StatRoll = stack.StatRoll,
				HasDurability = stack.HasDurability,
				Durability = stack.Durability
			} );
		}

		return dto;
	}

	public void ApplyDto( ThornsPersistentStructureStorageDto dto )
	{
		for ( var i = 0; i < SlotCount; i++ )
			_slots[i] = ThornsItemStack.EmptyStack;

		if ( dto?.Slots is null )
			return;

		foreach ( var entry in dto.Slots )
		{
			if ( entry is null || entry.SlotIndex < 0 || entry.SlotIndex >= SlotCount )
				continue;

			if ( string.IsNullOrWhiteSpace( entry.ItemId ) || entry.Count <= 0 )
				continue;

			_slots[entry.SlotIndex] = new ThornsItemStack
			{
				ItemId = entry.ItemId.Trim(),
				Count = entry.Count,
				ItemTier = entry.ItemTier,
				StatRoll = entry.StatRoll,
				HasDurability = entry.HasDurability,
				Durability = entry.Durability
			};
		}
	}

	public ThornsItemStack GetSlot( int index )
	{
		if ( index < 0 || index >= SlotCount )
			return ThornsItemStack.EmptyStack;

		return _slots[index];
	}

	public void SetSlot( int index, ThornsItemStack stack )
	{
		if ( index < 0 || index >= SlotCount )
			return;

		_slots[index] = stack;
		ThornsWorldPersistence.RequestSave();
	}
}
