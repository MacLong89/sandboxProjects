namespace Terraingen.UI.Components;

using Sandbox.UI;
using Terraingen.Combat;
using Terraingen.Combat.Attachments;
using Terraingen.GameData;
using Terraingen.Player;
using Terraingen.UI;
using Terraingen.UI.Core;

/// <summary>Weapon inspect attachment slot — accepts attachment drags and supports drag-off unequip.</summary>
public sealed class ThornsAttachmentInspectSlot : Panel
{
	static readonly List<ThornsAttachmentInspectSlot> AllSlots = new();
	static ThornsAttachmentInspectSlot _dropTarget;
	static ThornsAttachmentInspectSlot _hoverSlot;

	readonly int _slotIndex;
	readonly Action _onChanged;
	Panel _icon;
	Label _emptyLabel;

	public ThornsContainerKind WeaponContainer { get; private set; }
	public int WeaponIndex { get; private set; } = -1;

	public ThornsAttachmentInspectSlot( Panel parent, int slotIndex, Action onChanged )
	{
		_slotIndex = slotIndex;
		_onChanged = onChanged;
		Parent = parent;
		AddClass( "weapon-attachment-slot" );

		Style.Width = Length.Pixels( ThornsUiMetrics.MenuItemSlot );
		Style.Height = Length.Pixels( ThornsUiMetrics.MenuItemSlot );
		Style.MinWidth = Length.Pixels( ThornsUiMetrics.MenuItemSlot );
		Style.MinHeight = Length.Pixels( ThornsUiMetrics.MenuItemSlot );
		Style.FlexShrink = 0;
		Style.Position = PositionMode.Relative;
		Style.Display = DisplayMode.Flex;
		Style.JustifyContent = Justify.Center;
		Style.AlignItems = Align.Center;
		Style.Overflow = OverflowMode.Hidden;
		ThornsHudTheme.ApplyCharcoalSlotChrome( this );

		_icon = ThornsUiFactory.AddPanel( this, "icon slot-icon weapon-attachment-slot-icon" );
		_icon.Style.Width = Length.Percent( 88 );
		_icon.Style.Height = Length.Percent( 88 );
		_icon.Style.PointerEvents = PointerEvents.None;
		_icon.Style.Display = DisplayMode.None;

		_emptyLabel = ThornsUiFactory.AddLabel( this, "+", "weapon-attachment-slot-empty" );
		_emptyLabel.Style.PointerEvents = PointerEvents.None;
		_emptyLabel.Style.FontSize = Length.Pixels( 18 );
		_emptyLabel.Style.FontWeight = 700;
		_emptyLabel.Style.FontColor = new Color( 0.75f, 0.72f, 0.66f, 0.85f );

		AllSlots.Add( this );
		AddEventListener( "oncontextmenu", OnAttachmentContextMenu );
	}

	public override void OnDeleted()
	{
		AllSlots.Remove( this );
		if ( _dropTarget == this )
			_dropTarget = null;
		base.OnDeleted();
	}

	public override bool WantsMouseInput() => true;

	public void BindWeapon( ThornsContainerKind container, int index )
	{
		WeaponContainer = container;
		WeaponIndex = index;
		Refresh();
	}

	public void BindFromInspectContext()
	{
		if ( !ThornsInventoryInspectContext.TryGetWeaponSlot( out var container, out var index ) )
		{
			ClearWeapon();
			return;
		}

		BindWeapon( container, index );
	}

	public void ClearWeapon()
	{
		WeaponContainer = default;
		WeaponIndex = -1;
		ClearVisual();
	}

	public void Refresh()
	{
		var itemId = ResolveAttachmentItemId();
		if ( string.IsNullOrWhiteSpace( itemId ) )
		{
			ClearVisual();
			return;
		}

		_emptyLabel.Style.Display = DisplayMode.None;
		_icon.Style.Display = DisplayMode.Flex;
		ThornsIconCache.ApplyToPanel( _icon, ThornsIconManifest.ResolveItemPath( itemId ) );
	}

	void ClearVisual()
	{
		_icon.Style.BackgroundImage = null;
		_icon.Style.Display = DisplayMode.None;
		_emptyLabel.Style.Display = DisplayMode.Flex;
		ThornsHudTheme.ApplyCharcoalSlotChrome( this );
	}

	string ResolveAttachmentItemId()
	{
		if ( !ThornsUiClientState.HasSnapshot )
			return "";

		if ( !TryResolveEquipWeapon( out var container, out var weaponIndex ) )
			return "";

		var idx = container is ThornsContainerKind.Head or ThornsContainerKind.Chest or ThornsContainerKind.Legs ? 0 : weaponIndex;
		var dto = ThornsUiClientState.Snapshot.Inventory.Slots.FirstOrDefault( s =>
			s.Container == container && s.Index == idx );
		if ( dto?.WeaponAttachmentIds is null || dto.WeaponAttachmentIds.Count <= _slotIndex )
			return "";

		var itemId = dto.WeaponAttachmentIds[_slotIndex];
		return string.IsNullOrWhiteSpace( itemId ) ? "" : itemId;
	}

	protected override void OnMouseOver( MousePanelEvent e )
	{
		base.OnMouseOver( e );
		if ( WeaponIndex < 0 )
			BindFromInspectContext();

		if ( WeaponIndex < 0 )
			return;

		_hoverSlot = this;
		RefreshDropTarget();
	}

	protected override void OnMouseOut( MousePanelEvent e )
	{
		base.OnMouseOut( e );
		if ( _hoverSlot == this )
		{
			_hoverSlot = null;
			SetClass( "drop-hover", false );
		}

		if ( _dropTarget == this )
			RefreshDropTarget();
	}

	protected override void OnMouseDown( MousePanelEvent e )
	{
		base.OnMouseDown( e );
		if ( e.MouseButton != MouseButtons.Left )
			return;

		EnsureWeaponBinding();
		if ( !TryResolveEquipWeapon( out var weaponContainer, out var weaponIndex ) )
			return;

		e.StopPropagation();
		ThornsWeaponAttachmentPicker.Close();

		var itemId = ResolveAttachmentItemId();
		if ( string.IsNullOrWhiteSpace( itemId ) )
			return;

		AddClass( "drag-source" );
		ThornsDragState.BeginWeaponAttachment( weaponContainer, weaponIndex, _slotIndex, itemId );
	}

	protected override void OnMouseUp( MousePanelEvent e )
	{
		base.OnMouseUp( e );
		if ( e.MouseButton != MouseButtons.Left )
			return;

		RemoveClass( "drag-source" );
		SetClass( "drop-hover", false );

		if ( !ThornsDragState.IsDragging )
			return;

		EnsureWeaponBinding();
		RefreshDropTarget();

		ThornsAttachmentDragDebug.Write(
			$"mouseup slot={_slotIndex} weapon={WeaponContainer}[{WeaponIndex}] drag={ThornsDragState.ItemId} moved={ThornsDragState.PointerMoved} weaponAttachDrag={ThornsDragState.IsWeaponAttachmentDrag}" );

		if ( TryCompleteDropIfHovered() )
		{
			ThornsAttachmentDragDebug.Write( $"mouseup slot={_slotIndex} drop completed" );
			e.StopPropagation();
			return;
		}

		if ( ThornsDragState.IsWeaponAttachmentDrag && ThornsDragState.PointerMoved )
		{
			e.StopPropagation();
			if ( _dropTarget is not null && _dropTarget.IsValid )
				_dropTarget.TryCompleteDrop();
			else
				TryUnequipDraggedAttachment();
			return;
		}

		if ( !ThornsDragState.IsWeaponAttachmentDrag && ThornsDragState.PointerMoved )
		{
			TryCompleteDropOnMousePosition();
			if ( !ThornsDragState.IsDragging )
			{
				ThornsAttachmentDragDebug.Write( $"mouseup slot={_slotIndex} drop via mouse position" );
				e.StopPropagation();
			}
		}
	}

	bool TryCompleteDropIfHovered()
	{
		if ( !ThornsDragState.IsDragging )
			return false;

		EnsureWeaponBinding();
		if ( WeaponIndex < 0 )
		{
			ThornsAttachmentDragDebug.LogReject(
				$"slot[{_slotIndex}].TryCompleteDropIfHovered",
				$"weapon unbound after bind (inspect={ThornsInventoryInspectContext.WeaponContainer}?[{ThornsInventoryInspectContext.WeaponIndex}])" );
			return false;
		}

		if ( _dropTarget != this && !IsHoveredDropTarget() )
			return false;

		if ( !AcceptsCurrentDrag( out _ ) )
		{
			ThornsAttachmentDragDebug.LogReject( $"slot[{_slotIndex}].TryCompleteDropIfHovered", "AcceptsCurrentDrag=false" );
			return false;
		}

		TryCompleteDrop();
		return !ThornsDragState.IsDragging;
	}

	void EnsureWeaponBinding()
	{
		if ( WeaponIndex >= 0 )
			return;

		BindFromInspectContext();
	}

	bool IsHoveredDropTarget() =>
		_hoverSlot == this || ContainsMouse();

	bool ContainsMouse() => IsValid && IsInside( Mouse.Position );

	void OnAttachmentContextMenu( PanelEvent e )
	{
		_ = e;
		EnsureWeaponBinding();
		if ( !TryResolveEquipWeapon( out var weaponContainer, out var weaponIndex ) )
			return;

		var itemId = ResolveAttachmentItemId();
		if ( string.IsNullOrWhiteSpace( itemId ) )
			return;

		ThornsWeaponAttachmentPicker.Close();
		ThornsPlayerGameplay.Local?.RequestEquipAttachment( new ThornsEquipAttachmentRequest
		{
			WeaponContainer = weaponContainer,
			WeaponIndex = weaponIndex,
			AttachmentSlotIndex = _slotIndex,
			Unequip = true
		} );
		_onChanged?.Invoke();
	}

	public static void RefreshDropTarget()
	{
		foreach ( var slot in AllSlots )
		{
			if ( slot is null || !slot.IsValid )
				continue;

			slot.SetClass( "drop-hover", false );
		}

		if ( !ThornsDragState.IsDragging )
		{
			_dropTarget = null;
			return;
		}

		ThornsAttachmentInspectSlot underMouse = null;
		ThornsAttachmentInspectSlot best = null;

		foreach ( var slot in AllSlots )
		{
			if ( slot is null || !slot.IsValid )
				continue;

			slot.EnsureWeaponBinding();
			if ( slot.WeaponIndex < 0 )
				continue;

			if ( !slot.ContainsMouse() )
				continue;

			underMouse = slot;
			if ( slot.AcceptsCurrentDrag( out _ ) )
				best = slot;
		}

		if ( underMouse is not null )
			_hoverSlot = underMouse;

		_dropTarget = best;
		if ( best is not null && best.IsValid )
			best.SetClass( "drop-hover", true );

		if ( ThornsAttachmentDragDebug.Enabled && underMouse is not null )
		{
			var acceptReason = underMouse.AcceptsCurrentDrag( out var reject );
			ThornsAttachmentDragDebug.Write(
				$"refresh hover slot={underMouse._slotIndex} underMouse={underMouse.WeaponContainer}[{underMouse.WeaponIndex}] accepts={acceptReason} ({reject}) dropTarget={( best is not null ? best._slotIndex.ToString() : "none" )}" );
		}
	}

	public static bool TryCompleteHoveredDrop()
	{
		if ( !ThornsDragState.IsDragging )
			return false;

		RefreshDropTarget();

		if ( _dropTarget is not null && _dropTarget.IsValid && _dropTarget.AcceptsCurrentDrag( out _ ) )
		{
			_dropTarget.TryCompleteDrop();
			_dropTarget = null;
			return !ThornsDragState.IsDragging;
		}

		if ( _hoverSlot is not null && _hoverSlot.IsValid && _hoverSlot.TryCompleteDropIfHovered() )
			return true;

		foreach ( var slot in AllSlots )
		{
			if ( slot is null || !slot.IsValid || !slot.TryCompleteDropIfHovered() )
				continue;

			return true;
		}

		if ( TryCompleteDropOnMousePosition() )
			return true;

		ThornsAttachmentDragDebug.LogReject( "TryCompleteHoveredDrop", "no attachment slot accepted drop" );

		if ( ThornsDragState.IsWeaponAttachmentDrag && ThornsDragState.PointerMoved )
		{
			ThornsPlayerGameplay.Local?.RequestEquipAttachment( new ThornsEquipAttachmentRequest
			{
				WeaponContainer = ThornsDragState.WeaponContainer,
				WeaponIndex = ThornsDragState.WeaponIndex,
				AttachmentSlotIndex = ThornsDragState.WeaponAttachmentSlotIndex,
				Unequip = true
			} );
			ThornsDragState.Clear();
			return true;
		}

		return false;
	}

	static bool TryCompleteDropOnMousePosition()
	{
		if ( !ThornsDragState.IsDragging || ThornsDragState.IsWeaponAttachmentDrag )
			return false;

		foreach ( var slot in AllSlots )
		{
			if ( slot is null || !slot.IsValid )
				continue;

			slot.EnsureWeaponBinding();
			if ( slot.WeaponIndex < 0 || !slot.ContainsMouse() || !slot.AcceptsCurrentDrag( out _ ) )
				continue;

			slot.TryCompleteDrop();
			return !ThornsDragState.IsDragging;
		}

		return false;
	}

	bool AcceptsCurrentDrag() => AcceptsCurrentDrag( out _ );

	bool AcceptsCurrentDrag( out string rejectReason )
	{
		rejectReason = "";
		if ( !ThornsDragState.IsDragging )
		{
			rejectReason = "not dragging";
			return false;
		}

		EnsureWeaponBinding();
		if ( WeaponIndex < 0 )
		{
			rejectReason = $"weapon unbound (inspect={ThornsInventoryInspectContext.WeaponContainer}?[{ThornsInventoryInspectContext.WeaponIndex}])";
			return false;
		}

		if ( ThornsDragState.IsWeaponAttachmentDrag )
			return true;

		var itemId = ThornsItemIdAliases.Canonicalize( ThornsDragState.ItemId );
		if ( !ThornsItemRegistry.TryGet( itemId, out var def ) || def.Category != ThornsItemCategory.Attachment )
		{
			rejectReason = $"not attachment item '{ThornsDragState.ItemId}' -> '{itemId}'";
			return false;
		}

		var combatId = ResolveWeaponCombatId();
		if ( string.IsNullOrWhiteSpace( combatId ) )
		{
			rejectReason = $"no combat id for {WeaponContainer}[{WeaponIndex}]";
			return false;
		}

		if ( !ThornsAttachmentItemIds.TryParseItemId( itemId, out var attachment ) )
		{
			rejectReason = $"TryParseItemId failed for '{itemId}'";
			return false;
		}

		if ( !ThornsAttachmentCatalog.IsCompatible( combatId, attachment ) )
		{
			rejectReason = $"{attachment} incompatible with {combatId}";
			return false;
		}

		return true;
	}

	string ResolveWeaponCombatId()
	{
		if ( WeaponIndex >= 0 && ThornsUiClientState.HasSnapshot )
		{
			var idx = WeaponContainer is ThornsContainerKind.Head or ThornsContainerKind.Chest or ThornsContainerKind.Legs ? 0 : WeaponIndex;
			var dto = ThornsUiClientState.Snapshot.Inventory.Slots.FirstOrDefault( s =>
				s.Container == WeaponContainer && s.Index == idx );
			if ( dto is not null && !string.IsNullOrWhiteSpace( dto.ItemId )
			     && ThornsItemRegistry.TryGet( dto.ItemId, out var weaponDef ) )
			{
				return ThornsInventoryWeaponState.ResolveCombatId( weaponDef, dto.ItemId );
			}
		}

		return ThornsInventoryInspectContext.ResolveCombatId();
	}

	bool TryResolveEquipWeapon( out ThornsContainerKind weaponContainer, out int weaponIndex )
	{
		if ( WeaponIndex >= 0 )
		{
			weaponContainer = WeaponContainer;
			weaponIndex = WeaponIndex;
			return true;
		}

		if ( ThornsInventoryInspectContext.TryGetWeaponSlot( out weaponContainer, out weaponIndex ) )
			return true;

		weaponContainer = default;
		weaponIndex = -1;
		return false;
	}

	void TryCompleteDrop()
	{
		if ( !ThornsDragState.IsDragging )
			return;

		EnsureWeaponBinding();
		if ( !TryResolveEquipWeapon( out var weaponContainer, out var weaponIndex ) )
		{
			ThornsAttachmentDragDebug.LogReject( $"slot[{_slotIndex}].TryCompleteDrop", "TryResolveEquipWeapon failed" );
			return;
		}

		if ( ThornsDragState.IsWeaponAttachmentDrag )
		{
			if ( ThornsDragState.WeaponContainer == weaponContainer
			     && ThornsDragState.WeaponIndex == weaponIndex
			     && ThornsDragState.WeaponAttachmentSlotIndex == _slotIndex )
			{
				ThornsDragState.Clear();
				return;
			}

			ThornsAttachmentDragDebug.Write(
				$"move attachment slot {ThornsDragState.WeaponAttachmentSlotIndex} -> {_slotIndex} on {weaponContainer}[{weaponIndex}]" );
			ThornsPlayerGameplay.Local?.RequestEquipAttachment( new ThornsEquipAttachmentRequest
			{
				WeaponContainer = weaponContainer,
				WeaponIndex = weaponIndex,
				AttachmentSlotIndex = _slotIndex,
				Unequip = true,
				FromContainer = ThornsDragState.WeaponContainer,
				FromIndex = ThornsDragState.WeaponIndex
			} );
		}
		else
		{
			ThornsAttachmentDragDebug.Write(
				$"equip '{ThornsDragState.ItemId}' -> slot {_slotIndex} on {weaponContainer}[{weaponIndex}] from {ThornsDragState.FromContainer}[{ThornsDragState.FromIndex}]" );
			ThornsPlayerGameplay.Local?.RequestEquipAttachment( new ThornsEquipAttachmentRequest
			{
				WeaponContainer = weaponContainer,
				WeaponIndex = weaponIndex,
				AttachmentSlotIndex = _slotIndex,
				FromContainer = ThornsDragState.FromContainer,
				FromIndex = ThornsDragState.FromIndex
			} );
		}

		ThornsDragState.MarkDropHandled();
		ThornsDragState.Clear();
		_onChanged?.Invoke();
	}

	static void TryUnequipDraggedAttachment()
	{
		if ( !ThornsDragState.IsWeaponAttachmentDrag )
			return;

		ThornsPlayerGameplay.Local?.RequestEquipAttachment( new ThornsEquipAttachmentRequest
		{
			WeaponContainer = ThornsDragState.WeaponContainer,
			WeaponIndex = ThornsDragState.WeaponIndex,
			AttachmentSlotIndex = ThornsDragState.WeaponAttachmentSlotIndex,
			Unequip = true
		} );
		ThornsDragState.Clear();
	}
}
