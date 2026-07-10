namespace Terraingen.GameData;

/// <summary>Campfire station slot indices (wood → ore → ingot output).</summary>
public static class ThornsCampfireStationSlots
{
	public const int Wood = 0;
	public const int Ore = 1;
	public const int Output = 2;
	public const int SlotCount = 3;
}

/// <summary>Workbench station slot indices (item → parts ×3 → restored output).</summary>
public static class ThornsWorkbenchStationSlots
{
	public const int Item = 0;
	public const int Part0 = 1;
	public const int Part1 = 2;
	public const int Part2 = 3;
	public const int Output = 4;
	public const int SlotCount = 5;
	public const int FirstPart = Part0;
	public const int PartCount = 3;
}

public static class ThornsStationSlotRules
{
	public static bool CanAccept( ThornsContainerKind container, int index, string itemId )
	{
		if ( string.IsNullOrWhiteSpace( itemId ) )
			return false;

		itemId = ThornsItemIdAliases.Canonicalize( itemId );

		if ( container == ThornsContainerKind.CampfireStation )
		{
			return index switch
			{
				ThornsCampfireStationSlots.Wood => string.Equals( itemId, ThornsCampfireSmelt.WoodItemId, StringComparison.OrdinalIgnoreCase ),
				ThornsCampfireStationSlots.Ore => string.Equals( itemId, ThornsCampfireSmelt.OreItemId, StringComparison.OrdinalIgnoreCase ),
				ThornsCampfireStationSlots.Output => string.Equals( itemId, ThornsCampfireSmelt.IngotItemId, StringComparison.OrdinalIgnoreCase ),
				_ => false
			};
		}

		if ( container == ThornsContainerKind.WorkbenchStation )
		{
			if ( index == ThornsWorkbenchStationSlots.Output )
				return false;

			if ( index == ThornsWorkbenchStationSlots.Item )
			{
				if ( !ThornsItemRegistry.TryGet( itemId, out var def ) )
					return false;

				var stack = new ThornsItemStack { ItemId = itemId, Count = 1 };
				return ThornsItemTier.IsWorkbenchServiceable( stack, def );
			}

			if ( index is >= ThornsWorkbenchStationSlots.FirstPart
			     and < ThornsWorkbenchStationSlots.FirstPart + ThornsWorkbenchStationSlots.PartCount )
				return IsRepairMaterial( itemId );

			return false;
		}

		return false;
	}

	public static bool IsOutputSlot( ThornsContainerKind container, int index ) =>
		container switch
		{
			ThornsContainerKind.CampfireStation => index == ThornsCampfireStationSlots.Output,
			ThornsContainerKind.WorkbenchStation => index == ThornsWorkbenchStationSlots.Output,
			_ => false
		};

	public static bool TryResolveCampfireQuickTarget( string itemId, out int slotIndex )
	{
		slotIndex = -1;
		itemId = ThornsItemIdAliases.Canonicalize( itemId );
		if ( string.Equals( itemId, ThornsCampfireSmelt.WoodItemId, StringComparison.OrdinalIgnoreCase ) )
		{
			slotIndex = ThornsCampfireStationSlots.Wood;
			return true;
		}

		if ( string.Equals( itemId, ThornsCampfireSmelt.OreItemId, StringComparison.OrdinalIgnoreCase ) )
		{
			slotIndex = ThornsCampfireStationSlots.Ore;
			return true;
		}

		if ( string.Equals( itemId, ThornsCampfireSmelt.IngotItemId, StringComparison.OrdinalIgnoreCase ) )
		{
			slotIndex = ThornsCampfireStationSlots.Output;
			return false;
		}

		return false;
	}

	public static bool TryResolveWorkbenchQuickTarget( string itemId, out int slotIndex )
	{
		slotIndex = -1;
		itemId = ThornsItemIdAliases.Canonicalize( itemId );

		if ( ThornsItemRegistry.TryGet( itemId, out var def ) )
		{
			var stack = new ThornsItemStack { ItemId = itemId, Count = 1 };
			if ( ThornsItemTier.IsWorkbenchServiceable( stack, def ) )
			{
				slotIndex = ThornsWorkbenchStationSlots.Item;
				return true;
			}
		}

		if ( IsRepairMaterial( itemId ) )
		{
			slotIndex = ThornsWorkbenchStationSlots.Part0;
			return true;
		}

		return false;
	}

	static bool IsRepairMaterial( string itemId ) =>
		itemId is "smelt_metal" or "cloth" or "leather_scrap" or "stone";

	public static bool HasMaterialsInSlots(
		ThornsItemStack[] slots,
		int firstPartIndex,
		int partCount,
		IEnumerable<ThornsRecipeIngredient> costs )
	{
		var needed = costs
			.GroupBy( c => ThornsItemIdAliases.Canonicalize( c.ItemId ), StringComparer.OrdinalIgnoreCase )
			.ToDictionary( g => g.Key, g => g.Sum( x => x.Count ), StringComparer.OrdinalIgnoreCase );

		var available = new Dictionary<string, int>( StringComparer.OrdinalIgnoreCase );
		for ( var i = 0; i < partCount; i++ )
		{
			var stack = slots[firstPartIndex + i];
			if ( stack.IsEmpty )
				continue;

			var id = ThornsItemIdAliases.Canonicalize( stack.ItemId );
			available[id] = available.GetValueOrDefault( id ) + stack.Count;
		}

		foreach ( var (itemId, count) in needed )
		{
			if ( available.GetValueOrDefault( itemId ) < count )
				return false;
		}

		return true;
	}

	public static void ConsumeMaterialsFromSlots(
		ThornsItemStack[] slots,
		int firstPartIndex,
		int partCount,
		IEnumerable<ThornsRecipeIngredient> costs )
	{
		foreach ( var cost in costs )
		{
			var remaining = cost.Count;
			var itemId = ThornsItemIdAliases.Canonicalize( cost.ItemId );

			for ( var i = 0; i < partCount && remaining > 0; i++ )
			{
				ref var stack = ref slots[firstPartIndex + i];
				if ( stack.IsEmpty )
					continue;

				if ( !string.Equals( ThornsItemIdAliases.Canonicalize( stack.ItemId ), itemId, StringComparison.OrdinalIgnoreCase ) )
					continue;

				var take = Math.Min( remaining, stack.Count );
				stack.Count -= take;
				remaining -= take;
				if ( stack.Count <= 0 )
					stack = ThornsItemStack.EmptyStack;
			}
		}
	}

	public static List<ThornsInventorySlotDto> BuildSlotDtos( ThornsContainerKind container, ThornsItemStack[] slots )
	{
		var list = new List<ThornsInventorySlotDto>( slots.Length );
		for ( var i = 0; i < slots.Length; i++ )
		{
			var stack = slots[i];
			if ( stack.IsEmpty )
				continue;

			list.Add( new ThornsInventorySlotDto
			{
				Container = container,
				Index = i,
				ItemId = stack.ItemId,
				Count = stack.Count,
				HasDurability = stack.HasDurability,
				Durability = stack.Durability,
				ItemTier = stack.ItemTier,
				WeaponTier = stack.ItemTier,
				StatRoll = stack.StatRoll,
				WeaponLoadedAmmo = stack.WeaponLoadedAmmo
			} );
		}

		return list;
	}
}
