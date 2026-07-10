namespace Terraingen.UI.Components;

using Sandbox.UI;
using Terraingen.Combat;
using Terraingen.Combat.Attachments;
using Terraingen.GameData;
using Terraingen.Player;
using Terraingen.UI;
using Terraingen.UI.Core;

/// <summary>Inline picker for equipping compatible attachments on the weapon inspect panel.</summary>
public static class ThornsWeaponAttachmentPicker
{
	readonly record struct PickEntry( ThornsContainerKind Container, int Index, string ItemId, string DisplayName );

	static Panel _panel;

	public static bool IsOpen => _panel is not null && _panel.IsValid;

	public static void Close()
	{
		if ( _panel is null || !_panel.IsValid )
		{
			_panel = null;
			return;
		}

		_panel.Delete( true );
		_panel = null;
	}

	public static void Show(
		ThornsAttachmentInspectSlot anchor,
		ThornsContainerKind weaponContainer,
		int weaponIndex,
		int attachmentSlotIndex,
		bool allowRemove,
		Action onChanged )
	{
		if ( anchor is null || !anchor.IsValid || weaponIndex < 0 )
			return;

		Close();

		var column = anchor.Parent;
		while ( column is not null && column.IsValid && !column.HasClass( "inspect-weapon-right" ) )
			column = column.Parent;

		if ( column is null || !column.IsValid )
			column = anchor.Parent;

		if ( column is null || !column.IsValid )
			return;

		var combatId = ResolveCombatId( weaponContainer, weaponIndex );
		if ( string.IsNullOrWhiteSpace( combatId ) || !ThornsAttachmentCatalog.SupportsAttachments( combatId ) )
			return;

		_panel = ThornsUiFactory.AddPanel( column, "weapon-attachment-picker" );
		_panel.Style.FlexDirection = FlexDirection.Column;
		_panel.Style.FlexShrink = 0;
		_panel.Style.Width = Length.Percent( 100 );
		_panel.Style.MarginTop = Length.Pixels( 6 );
		_panel.Style.Overflow = OverflowMode.Hidden;

		var title = ThornsUiFactory.AddLabel( _panel, "ADD ATTACHMENT", "weapon-attachment-picker-title thorns-muted" );
		title.Style.FlexShrink = 0;
		title.Style.MarginBottom = Length.Pixels( 4 );

		var list = ThornsUiFactory.AddPanel( _panel, "weapon-attachment-picker-list" );
		list.Style.FlexDirection = FlexDirection.Column;
		list.Style.FlexShrink = 0;
		list.Style.Width = Length.Percent( 100 );
		list.Style.Overflow = OverflowMode.Hidden;

		if ( allowRemove )
		{
			AddActionRow( list, "Remove attachment", null, () =>
			{
				Equip( weaponContainer, weaponIndex, attachmentSlotIndex, unequip: true );
				Close();
				onChanged?.Invoke();
			} );
		}

		var entries = CollectCompatible( combatId );
		if ( entries.Count == 0 )
		{
			var empty = ThornsUiFactory.AddPassiveLabel( list, "No compatible attachments in inventory.",
				"weapon-attachment-picker-empty thorns-muted" );
			empty.Style.WhiteSpace = WhiteSpace.Normal;
			empty.Style.FontSize = Length.Pixels( 10 );
			empty.Style.LineHeight = Length.Pixels( 14 );
			empty.Style.PaddingTop = Length.Pixels( 2 );
			empty.Style.PaddingBottom = Length.Pixels( 2 );
		}
		else
		{
			foreach ( var entry in entries )
			{
				var captured = entry;
				AddActionRow( list, captured.DisplayName, captured.ItemId, () =>
				{
					Equip( weaponContainer, weaponIndex, attachmentSlotIndex, captured.Container, captured.Index );
					Close();
					onChanged?.Invoke();
				} );
			}
		}

		var dismiss = ThornsUiFactory.AddClickable( _panel, "weapon-attachment-picker-dismiss thorns-muted", "Cancel", Close );
		dismiss.Style.FlexShrink = 0;
		dismiss.Style.MarginTop = Length.Pixels( 4 );
		dismiss.Style.FontSize = Length.Pixels( 10 );
	}

	static void AddActionRow( Panel list, string label, string itemId, Action onPick )
	{
		var row = ThornsUiFactory.AddClickable( list, "weapon-attachment-picker-row", onPick );
		row.Style.FlexDirection = FlexDirection.Row;
		row.Style.AlignItems = Align.Center;
		row.Style.FlexShrink = 0;
		row.Style.MinHeight = Length.Pixels( 28 );
		row.Style.PaddingTop = Length.Pixels( 3 );
		row.Style.PaddingBottom = Length.Pixels( 3 );
		row.Style.Overflow = OverflowMode.Hidden;

		if ( !string.IsNullOrWhiteSpace( itemId ) )
		{
			var icon = ThornsUiFactory.AddPanel( row, "weapon-attachment-picker-icon slot-icon" );
			icon.Style.Width = Length.Pixels( 22 );
			icon.Style.Height = Length.Pixels( 22 );
			icon.Style.MinWidth = Length.Pixels( 22 );
			icon.Style.MinHeight = Length.Pixels( 22 );
			icon.Style.FlexShrink = 0;
			icon.Style.MarginRight = Length.Pixels( 6 );
			icon.Style.PointerEvents = PointerEvents.None;
			ThornsIconCache.ApplyToPanel( icon, ThornsIconManifest.ResolveItemPath( itemId ) );
		}

		var text = ThornsUiFactory.AddLabel( row, label, "weapon-attachment-picker-label" );
		text.Style.FlexGrow = 1;
		text.Style.FlexShrink = 1;
		text.Style.MinWidth = Length.Pixels( 0 );
		text.Style.FontSize = Length.Pixels( 10 );
		text.Style.Overflow = OverflowMode.Hidden;
		text.Style.TextOverflow = TextOverflow.Ellipsis;
		text.Style.WhiteSpace = WhiteSpace.NoWrap;
	}

	static List<PickEntry> CollectCompatible( string combatId )
	{
		var results = new List<PickEntry>();
		if ( !ThornsUiClientState.HasSnapshot )
			return results;

		foreach ( var slot in ThornsUiClientState.Snapshot.Inventory.Slots )
		{
			if ( slot.Container is not (ThornsContainerKind.Inventory or ThornsContainerKind.Hotbar) )
				continue;

			if ( string.IsNullOrWhiteSpace( slot.ItemId ) )
				continue;

			if ( !ThornsItemRegistry.TryGet( slot.ItemId, out var def )
			     || def.Category != ThornsItemCategory.Attachment )
				continue;

			if ( !ThornsAttachmentItemIds.TryParseItemId( slot.ItemId, out var attachment )
			     || !ThornsAttachmentCatalog.IsCompatible( combatId, attachment ) )
				continue;

			results.Add( new PickEntry( slot.Container, slot.Index, slot.ItemId, def.DisplayName ) );
		}

		return results
			.OrderBy( e => e.DisplayName, StringComparer.OrdinalIgnoreCase )
			.ToList();
	}

	static string ResolveCombatId( ThornsContainerKind weaponContainer, int weaponIndex )
	{
		if ( !ThornsUiClientState.HasSnapshot )
			return "";

		var idx = weaponContainer is ThornsContainerKind.Head or ThornsContainerKind.Chest or ThornsContainerKind.Legs
			? 0
			: weaponIndex;
		var dto = ThornsUiClientState.Snapshot.Inventory.Slots.FirstOrDefault( s =>
			s.Container == weaponContainer && s.Index == idx );
		if ( dto is null || string.IsNullOrWhiteSpace( dto.ItemId ) )
			return "";

		if ( !ThornsItemRegistry.TryGet( dto.ItemId, out var weaponDef ) )
			return "";

		return ThornsInventoryWeaponState.ResolveCombatId( weaponDef, dto.ItemId );
	}

	static void Equip(
		ThornsContainerKind weaponContainer,
		int weaponIndex,
		int attachmentSlotIndex,
		ThornsContainerKind fromContainer = default,
		int fromIndex = -1,
		bool unequip = false )
	{
		ThornsPlayerGameplay.Local?.RequestEquipAttachment( new ThornsEquipAttachmentRequest
		{
			WeaponContainer = weaponContainer,
			WeaponIndex = weaponIndex,
			AttachmentSlotIndex = attachmentSlotIndex,
			FromContainer = fromContainer,
			FromIndex = fromIndex,
			Unequip = unequip
		} );
	}
}
