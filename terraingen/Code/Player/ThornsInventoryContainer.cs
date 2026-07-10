namespace Terraingen.Player;

using Terraingen.GameData;

public sealed class ThornsInventoryContainer
{
	public const int InventorySlotCount = 30;
	public const int HotbarSlotCount = 8;

	readonly ThornsItemStack[] _inventory = new ThornsItemStack[InventorySlotCount];
	readonly ThornsItemStack[] _hotbar = new ThornsItemStack[HotbarSlotCount];
	readonly ThornsItemStack[] _armor = new ThornsItemStack[3];

	public ThornsItemStack GetSlot( ThornsContainerKind kind, int index )
	{
		index = ResolveSlotIndex( kind, index );
		var arr = ResolveArray( kind );
		if ( arr is null || index < 0 || index >= arr.Length )
			return ThornsItemStack.EmptyStack;

		return arr[index];
	}

	public void SetSlot( ThornsContainerKind kind, int index, ThornsItemStack stack )
	{
		index = ResolveSlotIndex( kind, index );
		var arr = ResolveArray( kind );
		if ( arr is null || index < 0 || index >= arr.Length )
			return;

		arr[index] = stack;
	}

	static int ResolveSlotIndex( ThornsContainerKind kind, int index ) => kind switch
	{
		ThornsContainerKind.Head => 0,
		ThornsContainerKind.Chest => 1,
		ThornsContainerKind.Legs => 2,
		_ => index
	};

	ThornsItemStack[] ResolveArray( ThornsContainerKind kind ) => kind switch
	{
		ThornsContainerKind.Inventory => _inventory,
		ThornsContainerKind.Hotbar => _hotbar,
		ThornsContainerKind.Head => _armor,
		ThornsContainerKind.Chest => _armor,
		ThornsContainerKind.Legs => _armor,
		_ => null
	};

	public int ArmorIndexFor( ThornsContainerKind kind ) => ResolveSlotIndex( kind, 0 );

	public ThornsInventorySnapshotDto ToSnapshot( bool craftExpanded, string craftCategory, string selectedRecipe, int activeHotbarIndex )
	{
		var dto = new ThornsInventorySnapshotDto
		{
			CraftPanelExpanded = craftExpanded,
			ActiveCraftCategoryId = ThornsCraftCatalog.NormalizeCraftCategoryId( craftCategory ),
			SelectedRecipeId = selectedRecipe,
			ActiveHotbarIndex = Math.Clamp( activeHotbarIndex, 0, HotbarSlotCount - 1 )
		};

		AppendSlots( dto, ThornsContainerKind.Inventory, _inventory );
		AppendSlots( dto, ThornsContainerKind.Hotbar, _hotbar );
		AppendSlots( dto, ThornsContainerKind.Head, [_armor[0]] );
		AppendSlots( dto, ThornsContainerKind.Chest, [_armor[1]] );
		AppendSlots( dto, ThornsContainerKind.Legs, [_armor[2]] );
		return dto;
	}

	void AppendSlots( ThornsInventorySnapshotDto dto, ThornsContainerKind kind, ThornsItemStack[] stacks )
	{
		for ( var i = 0; i < stacks.Length; i++ )
		{
			var s = stacks[i];
			if ( s.IsEmpty )
				continue;

			dto.Slots.Add( ThornsInventoryWeaponState.ToSlotDto( kind, i, s, this ) );
		}
	}

	public void ApplySnapshot( ThornsInventorySnapshotDto dto )
	{
		ClearAll();
		if ( dto?.Slots is null )
			return;

		foreach ( var slot in dto.Slots )
		{
			if ( string.IsNullOrEmpty( slot.ItemId ) || slot.Count <= 0 )
				continue;

			var kind = slot.Container;
			var index = kind is ThornsContainerKind.Head or ThornsContainerKind.Chest or ThornsContainerKind.Legs
				? ArmorIndexFor( kind )
				: slot.Index;

			var stack = ThornsItemIdAliases.CanonicalizeStack( new ThornsItemStack
			{
				ItemId = slot.ItemId,
				Count = slot.Count,
				HasDurability = slot.HasDurability,
				Durability = slot.Durability,
				WeaponLoadedAmmo = slot.WeaponLoadedAmmo,
				ItemTier = slot.ItemTier > 0 ? slot.ItemTier : slot.WeaponTier,
				StatRoll = slot.StatRoll,
				AttachmentId0 = slot.WeaponAttachmentIds?.Count > 0 ? slot.WeaponAttachmentIds[0] : "",
				AttachmentId1 = slot.WeaponAttachmentIds?.Count > 1 ? slot.WeaponAttachmentIds[1] : "",
				AttachmentId2 = slot.WeaponAttachmentIds?.Count > 2 ? slot.WeaponAttachmentIds[2] : ""
			} );

			if ( ThornsItemRegistry.TryGet( stack.ItemId, out var def ) )
			{
				var combatId = ThornsInventoryWeaponState.ResolveCombatId( def, slot.ItemId );
				ThornsInventoryWeaponState.EnsureWeaponRowInitialized( ref stack, combatId );
				ThornsInventoryWeaponState.EnsureToolDurabilityInitialized( ref stack, def );
			}

			SetSlot( kind, index, stack );
		}
	}

	public void ClearAll()
	{
		for ( var i = 0; i < _inventory.Length; i++ )
			_inventory[i] = default;
		for ( var i = 0; i < _hotbar.Length; i++ )
			_hotbar[i] = default;
		for ( var i = 0; i < _armor.Length; i++ )
			_armor[i] = default;
	}

	public bool IsEmpty()
	{
		for ( var i = 0; i < _inventory.Length; i++ )
			if ( !_inventory[i].IsEmpty )
				return false;

		for ( var i = 0; i < _hotbar.Length; i++ )
			if ( !_hotbar[i].IsEmpty )
				return false;

		for ( var i = 0; i < _armor.Length; i++ )
			if ( !_armor[i].IsEmpty )
				return false;

		return true;
	}

	public void SeedStarterItems()
	{
		ThornsStarterLoadout.Apply( this );
	}
}
