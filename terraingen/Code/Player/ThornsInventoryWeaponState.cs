namespace Terraingen.Player;

using System;
using Terraingen.Combat;
using Terraingen.Combat.Attachments;
using Terraingen.GameData;

/// <summary>Host helpers for weapon rows (instance id, clip fill, durability seed).</summary>
public static class ThornsInventoryWeaponState
{
	public static void EnsureWeaponRowInitialized( ref ThornsItemStack stack, string combatId )
	{
		if ( stack.IsEmpty )
			return;

		if ( !ThornsItemRegistry.TryGet( stack.ItemId, out var def ) )
			return;

		if ( ThornsItemTier.SupportsTiering( def ) && stack.ItemTier <= 0 )
			ThornsItemTier.ApplyCraftDefaults( ref stack, def );

		var wdef = ThornsWeaponDefinitions.Get( combatId );
		if ( ThornsWeaponDefinitions.TreatsAsMeleeWeapon( wdef, combatId ) )
		{
			if ( wdef.DurabilityLossPerShot > 0.0001f && wdef.MaxDurability > 0.001f )
			{
				stack.HasDurability = true;
				if ( stack.Durability <= 0.001f )
					ThornsItemTier.ApplyTierScaledDurability( ref stack, def );
			}

			return;
		}

		if ( wdef.ClipSize <= 0 )
			return;

		stack.HasDurability = wdef.MaxDurability > 0.001f;
		if ( stack.Durability <= 0.001f && stack.HasDurability )
			ThornsItemTier.ApplyTierScaledDurability( ref stack, def );

		if ( string.IsNullOrWhiteSpace( stack.WeaponInstanceId ) )
			stack.WeaponInstanceId = Guid.NewGuid().ToString( "N" );

		if ( stack.WeaponLoadedAmmo <= 0 )
			stack.WeaponLoadedAmmo = ThornsWeaponEffectiveStats.ResolveClipSize( wdef, combatId, stack );

		SanitizeAttachmentsOnStack( ref stack, combatId );
	}

	static void SanitizeAttachmentsOnStack( ref ThornsItemStack stack, string combatWeaponId )
	{
		var ids = ThornsWeaponAttachmentState.GetAttachmentItemIds( stack );
		ThornsWeaponAttachmentState.SetAttachmentItemIds( ref stack, ids, combatWeaponId );
	}

	/// <summary>World furniture, airdrops, and other generated loot containers spawn tiered gear.</summary>
	public static void PrepareWorldLootStack( ref ThornsItemStack stack, Random rng = null, float attachmentRollMul = 1f, bool premiumTable = false )
	{
		if ( stack.IsEmpty || !ThornsItemRegistry.TryGet( stack.ItemId, out var def ) )
			return;

		rng ??= Random.Shared;

		if ( ThornsItemTier.SupportsTiering( def ) )
			ThornsItemTier.ApplyLootRoll( ref stack, def, rng, premiumTable );

		if ( def.Category != ThornsItemCategory.Weapon )
			return;

		var combatId = ResolveCombatId( def, stack.ItemId );
		EnsureWeaponRowInitialized( ref stack, combatId );
		ThornsWeaponAttachmentRoll.RollOntoStack( ref stack, rng, attachmentRollMul );
		ClampLoadedAmmoToClip( ref stack, combatId );
	}

	/// <summary>Backward-compatible alias for weapon-only callers.</summary>
	public static void PrepareWorldLootWeaponStack( ref ThornsItemStack stack, Random rng = null, float attachmentRollMul = 1f )
		=> PrepareWorldLootStack( ref stack, rng, attachmentRollMul );

	public static void ClampLoadedAmmoToClip( ref ThornsItemStack stack, string combatId )
	{
		if ( stack.IsEmpty || string.IsNullOrWhiteSpace( combatId ) )
			return;

		var wdef = ThornsWeaponDefinitions.Get( combatId );
		if ( wdef.ClipSize <= 0 )
			return;

		var clip = ThornsWeaponEffectiveStats.ResolveClipSize( wdef, combatId, stack );
		if ( stack.WeaponLoadedAmmo > clip )
			stack.WeaponLoadedAmmo = clip;
	}

	public static void EnsureToolDurabilityInitialized( ref ThornsItemStack stack, ThornsItemDefinition def )
	{
		if ( stack.IsEmpty || def is null || def.ItemType != ThornsItemType.Tool )
			return;

		if ( def.ToolMaxDurability <= 0.001f )
			return;

		if ( ThornsItemTier.SupportsTiering( def ) && stack.ItemTier <= 0 )
			ThornsItemTier.ApplyCraftDefaults( ref stack, def );

		stack.HasDurability = true;
		if ( stack.Durability <= 0.001f )
			ThornsItemTier.ApplyTierScaledDurability( ref stack, def );
	}

	public static int CountAmmoInContainer( ThornsInventoryContainer inv, string ammoTypeId )
	{
		if ( inv is null || string.IsNullOrWhiteSpace( ammoTypeId ) )
			return 0;

		var total = 0;
		ForEachSlot( inv, ( _, stack ) =>
		{
			if ( stack.IsEmpty )
				return;

			if ( !string.Equals( stack.ItemId, ammoTypeId, StringComparison.OrdinalIgnoreCase ) )
				return;

			total += stack.Count;
		} );

		return total;
	}

	public static int ResolveDisplayTier( in ThornsItemStack stack, ThornsItemDefinition def )
		=> ThornsItemTier.ResolveTier( stack, def );

	public static ThornsInventorySlotDto ToSlotDto(
		ThornsContainerKind kind,
		int index,
		in ThornsItemStack stack,
		ThornsInventoryContainer inv )
	{
		var dto = new ThornsInventorySlotDto
		{
			Container = kind,
			Index = kind is ThornsContainerKind.Head or ThornsContainerKind.Chest or ThornsContainerKind.Legs ? 0 : index,
			ItemId = stack.ItemId,
			Count = stack.Count,
			HasDurability = stack.HasDurability,
			Durability = stack.Durability,
			WeaponLoadedAmmo = stack.WeaponLoadedAmmo,
			ItemTier = stack.ItemTier,
			StatRoll = stack.StatRoll
		};

		dto.WeaponAttachmentIds = ThornsWeaponAttachmentState.ToDtoList( stack );

		if ( stack.IsEmpty || !ThornsItemRegistry.TryGet( stack.ItemId, out var def ) )
			return dto;

		var tier = ResolveDisplayTier( stack, def );
		dto.ItemTier = tier;
		dto.WeaponTier = tier;
		var combatId = ResolveCombatId( def, stack.ItemId );
		var wdef = ThornsWeaponDefinitions.Get( combatId );
		var effective = ThornsWeaponEffectiveStats.Resolve( wdef, combatId, stack );
		dto.WeaponClipSize = Math.Max( 0, effective.ClipSize );
		dto.WeaponBroken = stack.IsWeaponBroken( combatId );

		if ( !ThornsWeaponDefinitions.TreatsAsMeleeWeapon( wdef, combatId ) && !string.IsNullOrWhiteSpace( wdef.AmmoTypeId ) )
			dto.AmmoReserve = CountAmmoInContainer( inv, wdef.AmmoTypeId );

		return dto;
	}

	public static ThornsItemStack CopyStackWithCount( in ThornsItemStack stack, int count ) =>
		new()
		{
			ItemId = stack.ItemId,
			Count = count,
			HasDurability = stack.HasDurability,
			Durability = stack.Durability,
			WeaponInstanceId = stack.WeaponInstanceId,
			WeaponLoadedAmmo = stack.WeaponLoadedAmmo,
			AttachmentId0 = stack.AttachmentId0,
			AttachmentId1 = stack.AttachmentId1,
			AttachmentId2 = stack.AttachmentId2,
			ItemTier = stack.ItemTier,
			StatRoll = stack.StatRoll
		};

	public static string ResolveCombatId( ThornsItemDefinition def, string itemId )
	{
		if ( def is null )
			return "";

		if ( def.ItemType == ThornsItemType.Tool )
			return ThornsFpToolCombat.GetCombatDefinitionIdForToolItemId( itemId );

		if ( def.ItemType == ThornsItemType.Weapon )
			return string.IsNullOrWhiteSpace( def.CombatWeaponDefinitionId ) ? itemId.Trim() : def.CombatWeaponDefinitionId.Trim();

		return "";
	}

	static void ForEachSlot( ThornsInventoryContainer inv, Action<ThornsContainerKind, ThornsItemStack> action )
	{
		for ( var i = 0; i < ThornsInventoryContainer.InventorySlotCount; i++ )
			action( ThornsContainerKind.Inventory, inv.GetSlot( ThornsContainerKind.Inventory, i ) );

		for ( var i = 0; i < ThornsInventoryContainer.HotbarSlotCount; i++ )
			action( ThornsContainerKind.Hotbar, inv.GetSlot( ThornsContainerKind.Hotbar, i ) );

		action( ThornsContainerKind.Head, inv.GetSlot( ThornsContainerKind.Head, 0 ) );
		action( ThornsContainerKind.Chest, inv.GetSlot( ThornsContainerKind.Chest, 0 ) );
		action( ThornsContainerKind.Legs, inv.GetSlot( ThornsContainerKind.Legs, 0 ) );
	}
}
