namespace Terraingen.Combat.Attachments;

using Terraingen.GameData;

/// <summary>Read/write attachment item ids on weapon stacks (max 3 slots).</summary>
public static class ThornsWeaponAttachmentState
{
	public static IReadOnlyList<string> GetAttachmentItemIds( in ThornsItemStack stack )
	{
		var list = new List<string>( ThornsAttachmentCatalog.MaxSlotsPerWeapon );
		if ( !string.IsNullOrWhiteSpace( stack.AttachmentId0 ) )
			list.Add( stack.AttachmentId0 );
		if ( !string.IsNullOrWhiteSpace( stack.AttachmentId1 ) )
			list.Add( stack.AttachmentId1 );
		if ( !string.IsNullOrWhiteSpace( stack.AttachmentId2 ) )
			list.Add( stack.AttachmentId2 );
		return list;
	}

	public static IReadOnlyList<ThornsAttachmentId> GetAttachments( in ThornsItemStack stack ) =>
		ThornsAttachmentCatalog.ParseAttachmentItemIds( GetAttachmentItemIds( stack ) );

	public static void SetAttachmentItemIds( ref ThornsItemStack stack, IEnumerable<string> itemIds, string combatWeaponId )
	{
		var parsed = ThornsAttachmentCatalog.ParseAttachmentItemIds( itemIds );
		var sanitized = ThornsAttachmentCatalog.SanitizeForWeapon( combatWeaponId, parsed );
		var canonical = sanitized.Select( ThornsAttachmentItemIds.ToItemId ).Where( id => !string.IsNullOrEmpty( id ) ).ToList();

		stack.AttachmentId0 = canonical.Count > 0 ? canonical[0] : "";
		stack.AttachmentId1 = canonical.Count > 1 ? canonical[1] : "";
		stack.AttachmentId2 = canonical.Count > 2 ? canonical[2] : "";
	}

	public static void CopyAttachments( in ThornsItemStack source, ref ThornsItemStack dest )
	{
		dest.AttachmentId0 = source.AttachmentId0;
		dest.AttachmentId1 = source.AttachmentId1;
		dest.AttachmentId2 = source.AttachmentId2;
	}

	public static List<string> ToDtoList( in ThornsItemStack stack ) =>
	[
		stack.AttachmentId0 ?? "",
		stack.AttachmentId1 ?? "",
		stack.AttachmentId2 ?? ""
	];

	public static int CountEquipped( in ThornsItemStack stack )
	{
		var count = 0;
		if ( !string.IsNullOrWhiteSpace( stack.AttachmentId0 ) )
			count++;
		if ( !string.IsNullOrWhiteSpace( stack.AttachmentId1 ) )
			count++;
		if ( !string.IsNullOrWhiteSpace( stack.AttachmentId2 ) )
			count++;
		return count;
	}

	public static bool HasRoomFor( in ThornsItemStack stack ) =>
		CountEquipped( stack ) < ThornsAttachmentCatalog.MaxSlotsPerWeapon;

	public static string GetAttachmentItemIdAtSlot( in ThornsItemStack stack, int slotIndex ) => slotIndex switch
	{
		0 => stack.AttachmentId0 ?? "",
		1 => stack.AttachmentId1 ?? "",
		2 => stack.AttachmentId2 ?? "",
		_ => ""
	};

	public static void SetAttachmentItemIdAtSlot( ref ThornsItemStack stack, int slotIndex, string itemId, string combatWeaponId )
	{
		if ( slotIndex < 0 || slotIndex >= ThornsAttachmentCatalog.MaxSlotsPerWeapon )
			return;

		itemId = itemId?.Trim() ?? "";
		if ( !string.IsNullOrWhiteSpace( itemId ) )
		{
			if ( !ThornsAttachmentItemIds.TryParseItemId( itemId, out var attachment )
			     || !ThornsAttachmentCatalog.IsCompatible( combatWeaponId, attachment ) )
				return;

			if ( ThornsAttachmentCatalog.IsSight( attachment ) )
				ClearCategoryFromOtherSlots( ref stack, slotIndex, ThornsAttachmentCatalog.IsSight );

			if ( ThornsAttachmentCatalog.IsForegrip( attachment ) )
				ClearCategoryFromOtherSlots( ref stack, slotIndex, ThornsAttachmentCatalog.IsForegrip );
		}

		SetSlotRaw( ref stack, slotIndex, itemId );
	}

	static void ClearCategoryFromOtherSlots(
		ref ThornsItemStack stack,
		int keepSlotIndex,
		Func<ThornsAttachmentId, bool> category )
	{
		for ( var i = 0; i < ThornsAttachmentCatalog.MaxSlotsPerWeapon; i++ )
		{
			if ( i == keepSlotIndex )
				continue;

			var existing = GetAttachmentItemIdAtSlot( stack, i );
			if ( ThornsAttachmentItemIds.TryParseItemId( existing, out var parsed ) && category( parsed ) )
				SetSlotRaw( ref stack, i, "" );
		}
	}

	static void SetSlotRaw( ref ThornsItemStack stack, int slotIndex, string itemId )
	{
		switch ( slotIndex )
		{
			case 0: stack.AttachmentId0 = itemId; break;
			case 1: stack.AttachmentId1 = itemId; break;
			case 2: stack.AttachmentId2 = itemId; break;
		}
	}

	public static string ClearAttachmentAtSlot( ref ThornsItemStack stack, int slotIndex, string combatWeaponId )
	{
		_ = combatWeaponId;
		var removed = GetAttachmentItemIdAtSlot( stack, slotIndex );
		if ( string.IsNullOrWhiteSpace( removed ) )
			return "";

		SetSlotRaw( ref stack, slotIndex, "" );
		return removed;
	}
}
