namespace Terraingen.UI.Components;

using Sandbox;
using Sandbox.UI;
using Terraingen.Combat;
using Terraingen.Combat.Attachments;
using Terraingen.GameData;
using Terraingen.Player;
using Terraingen.UI;
using Terraingen.UI.Core;

/// <summary>Single inventory/hotbar/armor slot with drag-drop and inspect selection.</summary>
public sealed class ThornsItemSlot : Panel
{
	static readonly List<ThornsItemSlot> AllSlots = new();
	static ThornsItemSlot _dropTarget;
	static ThornsItemSlot _hoverSlot;

	public ThornsContainerKind Container { get; }
	public int Index { get; }

	Panel _icon;
	Label _countLabel;
	Label _ammoLabel;
	Panel _durTrack;
	Panel _durFill;
	string _lastIconPath = "";
	readonly bool _isHotbar;
	readonly bool _menuLayout;
	readonly bool _worldContainerLayout;
	Action _onChanged;
	Action<ThornsContainerKind, int, string> _onSelected;

	public ThornsItemSlot(
		Panel parent,
		ThornsContainerKind container,
		int index,
		Action onChanged,
		bool isHotbar = false,
		Action<ThornsContainerKind, int, string> onSelected = null,
		bool menuLayout = false,
		bool worldContainerLayout = false )
	{
		Container = container;
		Index = index;
		_isHotbar = isHotbar;
		_menuLayout = menuLayout;
		_worldContainerLayout = worldContainerLayout;
		_onChanged = onChanged;
		_onSelected = onSelected;

		Parent = parent;
		var slotClass = isHotbar ? "hotbar-slot" : "thorns-item-slot";
		if ( worldContainerLayout )
			slotClass += " world-container-slot";
		else if ( menuLayout )
			slotClass += isHotbar ? " menu-hotbar-slot" : " menu-inventory-slot";
		if ( container is ThornsContainerKind.Head or ThornsContainerKind.Chest or ThornsContainerKind.Legs )
			slotClass += " armor-slot";
		AddClass( slotClass );

		var hudHotbar = isHotbar && !menuLayout && !worldContainerLayout;
		var size = worldContainerLayout
			? ThornsUiMetrics.WorldContainerSlot
			: menuLayout
				? (isHotbar ? ThornsUiMetrics.MenuHotbarSlot : ThornsUiMetrics.MenuItemSlot)
				: (isHotbar ? ThornsHudTheme.HotbarSlotPx : ThornsUiMetrics.ItemSlot);
		Style.Width = Length.Pixels( size );
		Style.Height = Length.Pixels( size );
		Style.MinWidth = Length.Pixels( size );
		Style.MinHeight = Length.Pixels( size );
		Style.FlexShrink = 0;
		Style.FlexGrow = 0;
		Style.Position = PositionMode.Relative;
		Style.Overflow = OverflowMode.Hidden;
		Style.Display = DisplayMode.Flex;
		Style.FlexDirection = FlexDirection.Column;
		Style.JustifyContent = Justify.Center;
		Style.AlignItems = Align.Center;
		Style.PointerEvents = PointerEvents.All;

		if ( menuLayout || worldContainerLayout )
		{
			AddClass( "concept-slot" );
			if ( ThornsUiSkin.Active == ThornsUiSkinKind.Classic )
				ThornsHudTheme.ApplyCharcoalSlotChrome( this );
		}
		else if ( !isHotbar )
		{
			Style.BackgroundColor = ThornsTheme.PanelBg.WithAlpha( 1f );
			Style.BorderColor = ThornsTheme.Border;
			Style.BorderWidth = Length.Pixels( 1 );
		}

		_icon = ThornsUiFactory.AddPanel( this, "icon slot-icon" );
		_icon.Style.Width = Length.Percent( 88 );
		_icon.Style.Height = Length.Percent( 88 );
		_icon.Style.FlexShrink = 0;
		_icon.Style.PointerEvents = PointerEvents.None;

		_countLabel = ThornsUiFactory.AddLabel( this, "", "count" );
		_countLabel.Style.Position = PositionMode.Absolute;
		_countLabel.Style.Right = Length.Pixels( hudHotbar ? ThornsHudTheme.HotbarScaledSlotInsetPx( 4 ) : 4 );
		_countLabel.Style.Bottom = Length.Pixels( hudHotbar ? ThornsHudTheme.HotbarScaledSlotInsetPx( 2 ) : 2 );
		_countLabel.Style.FontSize = Length.Pixels( hudHotbar ? ThornsHudTheme.HotbarScaledSlotInsetPx( 11 ) : 11 );
		_countLabel.Style.FontWeight = 700;
		_countLabel.Style.FontColor = Color.White;
		_countLabel.Style.TextAlign = TextAlign.Right;
		_countLabel.Style.PointerEvents = PointerEvents.None;

		_ammoLabel = ThornsUiFactory.AddLabel( this, "", "slot-ammo-badge" );
		_ammoLabel.Style.Position = PositionMode.Absolute;
		_ammoLabel.Style.Right = Length.Pixels( hudHotbar ? ThornsHudTheme.HotbarScaledSlotInsetPx( 3 ) : 3 );
		_ammoLabel.Style.Bottom = Length.Pixels( hudHotbar ? ThornsHudTheme.HotbarScaledSlotInsetPx( 7 ) : 7 );
		_ammoLabel.Style.FontSize = Length.Pixels( hudHotbar ? ThornsHudTheme.HotbarScaledSlotInsetPx( 9 ) : 9 );
		_ammoLabel.Style.FontWeight = 700;
		_ammoLabel.Style.TextAlign = TextAlign.Right;
		_ammoLabel.Style.FontColor = new Color( 0.92f, 0.96f, 1f, 0.95f );
		_ammoLabel.Style.PointerEvents = PointerEvents.None;
		ThornsUiLayer.ApplyLocalOrder( _ammoLabel, 2 );

		_durTrack = ThornsUiFactory.AddPanel( this, "slot-dur-track" );
		_durTrack.Style.Position = PositionMode.Absolute;
		_durTrack.Style.Left = Length.Pixels( hudHotbar ? ThornsHudTheme.HotbarScaledSlotInsetPx( 3 ) : 3 );
		_durTrack.Style.Right = Length.Pixels( hudHotbar ? ThornsHudTheme.HotbarScaledSlotInsetPx( 3 ) : 3 );
		_durTrack.Style.Bottom = Length.Pixels( hudHotbar ? ThornsHudTheme.HotbarScaledSlotInsetPx( 3 ) : 3 );
		_durTrack.Style.Height = Length.Pixels( hudHotbar ? ThornsHudTheme.HotbarScaledSlotInsetPx( 4 ) : 4 );
		_durTrack.Style.Overflow = OverflowMode.Hidden;
		_durTrack.Style.BackgroundColor = new Color( 0f, 0f, 0f, 0.55f );
		_durTrack.Style.PointerEvents = PointerEvents.None;
		ThornsUiLayer.ApplyLocalOrder( _durTrack, 1 );
		_durFill = ThornsUiFactory.AddPanel( _durTrack, "slot-dur-fill" );
		_durFill.Style.Height = Length.Percent( 100 );
		_durFill.Style.PointerEvents = PointerEvents.None;

		if ( isHotbar )
		{
			ThornsHudTheme.ApplyHotbarSlotBase( this );
			var key = ThornsUiFactory.AddLabel( this, $"{index + 1}", "hotbar-slot-key" );
			key.Style.Position = PositionMode.Absolute;
			key.Style.Left = Length.Pixels( hudHotbar ? ThornsHudTheme.HotbarSlotKeyLeftPx : 4 );
			key.Style.Top = Length.Pixels( hudHotbar ? ThornsHudTheme.HotbarSlotKeyTopPx : 2 );
			key.Style.FontSize = Length.Pixels( hudHotbar ? ThornsHudTheme.HotbarSlotKeyFontPx : 10 );
			key.Style.PointerEvents = PointerEvents.None;
		}

		AddEventListener( "ondblclick", OnDoubleClick );
		AddEventListener( "oncontextmenu", OnContextMenu );

		AllSlots.Add( this );
	}

	public override void OnDeleted()
	{
		AllSlots.Remove( this );
		if ( _dropTarget == this )
			_dropTarget = null;
		if ( _hoverSlot == this )
			_hoverSlot = null;

		base.OnDeleted();
	}

	public override bool WantsMouseInput() => true;

	protected override void OnMouseOver( MousePanelEvent e )
	{
		base.OnMouseOver( e );
		if ( ThornsMenuHost.IsOpen )
			_hoverSlot = this;
	}

	protected override void OnMouseOut( MousePanelEvent e )
	{
		base.OnMouseOut( e );
		if ( _hoverSlot == this )
		{
			_hoverSlot = null;
			SetClass( "drop-hover", false );
			SetClass( "weapon-attach-drop-hover", false );
		}
	}

	public ThornsItemStack PeekStack()
	{
		var slot = FindStack();
		return slot is null || slot.Value.IsEmpty ? ThornsItemStack.EmptyStack : slot.Value;
	}

	public void SetSelected( bool selected )
	{
		SetClass( "selected", selected );

		if ( _isHotbar && !_menuLayout )
			ThornsHudTheme.ApplyHotbarSlotSelected( this, selected );
		else if ( OwnsCharcoalSlotChrome )
			ThornsHudTheme.ApplyCharcoalSlotChrome( this, selected );
	}

	public void Refresh()
	{
		var dto = FindSlotDto();
		if ( dto is null || string.IsNullOrEmpty( dto.ItemId ) || dto.Count <= 0 )
		{
			ClearVisual();
			return;
		}

		var def = ThornsDefinitionRegistry.GetItem( dto.ItemId );
		var iconPath = string.IsNullOrWhiteSpace( dto.ItemId )
			? def?.IconPath
			: ThornsIconManifest.ResolveItemPath( dto.ItemId );
		var resolvedPath = iconPath ?? "";
		if ( !string.Equals( _lastIconPath, resolvedPath, StringComparison.OrdinalIgnoreCase ) )
		{
			ThornsIconCache.ApplyToPanel( _icon, resolvedPath );
			_lastIconPath = resolvedPath;
		}
		_icon.AddClass( "slot-icon" );

		ApplyTierChrome( dto, def );
		ApplyAmmoBadge( dto, def );
		ApplyDurabilityBar( dto, def );

		_countLabel.Text = dto.Count > 1 ? dto.Count.ToString() : "";
		_countLabel.Style.FontColor = Color.White;
	}

	bool OwnsHudHotbarChrome => _isHotbar && !_menuLayout;

	bool OwnsCharcoalSlotChrome =>
		OwnsHudHotbarChrome || ( _menuLayout && ThornsUiSkin.Active == ThornsUiSkinKind.Classic );

	void ClearVisual()
	{
		_lastIconPath = "";
		_icon.Style.BackgroundImage = null;
		_icon.RemoveClass( "slot-icon" );
		_icon.Style.BackgroundColor = Color.Transparent;
		if ( !OwnsCharcoalSlotChrome && !OwnsHudHotbarChrome )
			Style.BorderColor = ThornsTheme.Border;
		else if ( OwnsHudHotbarChrome )
			ThornsHudTheme.ApplyHudWoodSlot( this, HasClass( "selected" ) );
		else
			ThornsHudTheme.ApplyCharcoalSlotChrome( this, HasClass( "selected" ) );
		_countLabel.Text = "";
		_ammoLabel.Text = "";
		_ammoLabel.SetClass( "hidden", true );
		_durTrack.SetClass( "hidden", true );
		_durTrack.Style.Height = Length.Pixels( 0 );
		_durFill.Style.Width = Length.Fraction( 0f );
	}

	void ApplyTierChrome( ThornsInventorySlotDto dto, ThornsItemDefinition def )
	{
		var tier = dto.ItemTier > 0 ? dto.ItemTier : dto.WeaponTier;
		if ( tier > 0 && def is not null && ThornsItemTier.SupportsTiering( def ) )
		{
			_icon.Style.BackgroundColor = ThornsWeaponTierVisuals.SlotBackdropTint( tier );
			if ( !OwnsCharcoalSlotChrome )
				Style.BorderColor = ThornsWeaponTierVisuals.TitleTint( tier ).WithAlpha( 0.85f );
			return;
		}

		_icon.Style.BackgroundColor = Color.Transparent;
		if ( !OwnsCharcoalSlotChrome && !OwnsHudHotbarChrome )
			Style.BorderColor = ThornsTheme.Border;
		else if ( OwnsHudHotbarChrome )
			ThornsHudTheme.ApplyHudWoodSlot( this, HasClass( "selected" ) );
		else
			ThornsHudTheme.ApplyCharcoalSlotChrome( this, HasClass( "selected" ) );
	}

	void ApplyAmmoBadge( ThornsInventorySlotDto dto, ThornsItemDefinition def )
	{
		if ( def?.Category != ThornsItemCategory.Weapon || dto.WeaponClipSize <= 0 )
		{
			_ammoLabel.Text = "";
			_ammoLabel.SetClass( "hidden", true );
			return;
		}

		_ammoLabel.SetClass( "hidden", false );
		_ammoLabel.Text = $"{dto.WeaponLoadedAmmo}/{dto.WeaponClipSize}";
		_ammoLabel.Style.FontColor = dto.WeaponBroken
			? new Color( 1f, 0.35f, 0.35f, 1f )
			: new Color( 0.92f, 0.96f, 1f, 0.95f );
	}

	void ApplyDurabilityBar( ThornsInventorySlotDto dto, ThornsItemDefinition def )
	{
		var fill01 = ResolveDurabilityFill01( dto, def );
		if ( fill01 < 0f )
		{
			_durTrack.SetClass( "hidden", true );
			_durTrack.Style.Height = Length.Pixels( 0 );
			return;
		}

		_durTrack.SetClass( "hidden", false );
		_durTrack.Style.Height = Length.Pixels( 4 );
		_durFill.Style.Width = Length.Fraction( fill01 );
		_durFill.Style.BackgroundColor = ThornsWeaponTierVisuals.DurabilityStripColor( fill01 );
	}

	static float ResolveDurabilityFill01( ThornsInventorySlotDto dto, ThornsItemDefinition def )
	{
		if ( !dto.HasDurability || def is null )
			return -1f;

		var stack = new ThornsItemStack
		{
			ItemId = dto.ItemId,
			ItemTier = dto.ItemTier > 0 ? dto.ItemTier : dto.WeaponTier,
			StatRoll = dto.StatRoll
		};
		var statMul = ThornsItemTier.ResolveStatMultiplier( stack, def );

		if ( def.Category == ThornsItemCategory.Weapon )
		{
			var combatId = ThornsInventoryWeaponState.ResolveCombatId( def, dto.ItemId );
			var max = ThornsWeaponDefinitions.Get( combatId ).MaxDurability * statMul;
			if ( max <= 0.001f )
				return -1f;
			return Math.Clamp( dto.Durability / max, 0f, 1f );
		}

		if ( def.Category == ThornsItemCategory.Tool && def.ToolMaxDurability > 0.001f )
		{
			var max = def.ToolMaxDurability * statMul;
			return Math.Clamp( dto.Durability / max, 0f, 1f );
		}

		return -1f;
	}

	ThornsInventorySlotDto FindSlotDto()
	{
		if ( !ThornsUiClientState.HasSnapshot )
			return null;

		if ( Container == ThornsContainerKind.WorldLoot )
		{
			var external = ThornsUiClientState.Snapshot.ExternalContainer;
			if ( external?.IsOpen != true )
				return null;

			return external.Slots.FirstOrDefault( s => s.Container == Container && s.Index == Index );
		}

		if ( Container == ThornsContainerKind.CampfireStation )
		{
			var campfire = ThornsUiClientState.Snapshot.Campfire;
			if ( campfire?.IsOpen != true )
				return null;

			return campfire.StationSlots?.FirstOrDefault( s => s.Index == Index );
		}

		if ( Container == ThornsContainerKind.WorkbenchStation )
		{
			var workbench = ThornsUiClientState.Snapshot.Workbench;
			if ( workbench?.IsOpen != true )
				return null;

			return workbench.StationSlots?.FirstOrDefault( s => s.Index == Index );
		}

		var idx = Container is ThornsContainerKind.Head or ThornsContainerKind.Chest or ThornsContainerKind.Legs ? 0 : Index;
		var slots = ThornsUiClientState.Snapshot.Inventory?.Slots;
		if ( slots is null )
			return null;

		return slots.FirstOrDefault( s =>
			s.Container == Container && s.Index == idx );
	}

	ThornsItemStack? FindStack()
	{
		var dto = FindSlotDto();
		if ( dto is null )
			return null;

		return new ThornsItemStack
		{
			ItemId = dto.ItemId,
			Count = dto.Count,
			HasDurability = dto.HasDurability,
			Durability = dto.Durability,
			WeaponLoadedAmmo = dto.WeaponLoadedAmmo
		};
	}

	void SelectSlot( ThornsItemStack? stack )
	{
		if ( stack is null || stack.Value.IsEmpty )
			_onSelected?.Invoke( Container, Index, "" );
		else
			_onSelected?.Invoke( Container, Index, stack.Value.ItemId );
	}

	public static void RefreshDropTarget()
	{
		foreach ( var slot in AllSlots )
		{
			slot?.SetClass( "drop-hover", false );
			slot?.SetClass( "weapon-attach-drop-hover", false );
		}

		if ( !ThornsDragState.IsDragging )
		{
			_dropTarget = null;
			return;
		}

		ThornsItemSlot best = null;

		if ( _hoverSlot is not null && _hoverSlot.IsValid && _hoverSlot.IsVisible && _hoverSlot.AcceptsCurrentDrag() )
			best = _hoverSlot;
		else
		{
			foreach ( var slot in AllSlots )
			{
				if ( slot is null || !slot.IsValid || !slot.IsVisible || !slot.AcceptsCurrentDrag() )
					continue;

				if ( !slot.IsInside( Mouse.Position ) )
					continue;

				best = slot;
			}
		}

		_dropTarget = best;
		if ( best is not null && best.IsValid )
		{
			best.SetClass( "drop-hover", true );
			best.SetClass( "weapon-attach-drop-hover", best.CanAcceptAttachmentEquip() );
		}
	}

	public static void ClearDropTarget()
	{
		foreach ( var slot in AllSlots )
		{
			slot?.SetClass( "drop-hover", false );
			slot?.SetClass( "weapon-attach-drop-hover", false );
		}

		_dropTarget = null;
	}

	bool AcceptsCurrentDrag()
	{
		if ( !ThornsDragState.IsDragging || IsSameAsDragSource() )
			return false;

		if ( ThornsDragState.IsWeaponAttachmentDrag )
			return false;

		if ( !IsAttachmentDrag() )
		{
			if ( Container is ThornsContainerKind.CampfireStation or ThornsContainerKind.WorkbenchStation )
			{
				if ( ThornsStationSlotRules.IsOutputSlot( Container, Index ) )
					return false;

				return ThornsStationSlotRules.CanAccept( Container, Index, ThornsDragState.ItemId );
			}

			return true;
		}

		var stack = FindStack();
		if ( stack is null || stack.Value.IsEmpty )
			return true;

		if ( !ThornsItemRegistry.TryGet( stack.Value.ItemId, out var def )
		     || def.Category != ThornsItemCategory.Weapon )
			return true;

		return CanAcceptAttachmentEquip();
	}

	bool IsAttachmentDrag()
	{
		if ( string.IsNullOrWhiteSpace( ThornsDragState.ItemId ) )
			return false;

		var itemId = ThornsItemIdAliases.Canonicalize( ThornsDragState.ItemId );
		return ThornsItemRegistry.TryGet( itemId, out var def )
		       && def.Category == ThornsItemCategory.Attachment;
	}

	bool CanAcceptAttachmentEquip()
	{
		if ( !IsAttachmentDrag() || ThornsDragState.IsWeaponAttachmentDrag )
			return false;

		if ( Container is ThornsContainerKind.Head or ThornsContainerKind.Chest or ThornsContainerKind.Legs
		     or ThornsContainerKind.WorldLoot or ThornsContainerKind.CampfireStation or ThornsContainerKind.WorkbenchStation )
			return false;

		var stack = FindStack();
		if ( stack is null || stack.Value.IsEmpty )
			return false;

		if ( !ThornsItemRegistry.TryGet( stack.Value.ItemId, out var weaponDef )
		     || weaponDef.Category != ThornsItemCategory.Weapon )
			return false;

		var combatId = ThornsInventoryWeaponState.ResolveCombatId( weaponDef, stack.Value.ItemId );
		if ( !ThornsAttachmentCatalog.SupportsAttachments( combatId ) )
			return false;

		return ThornsAttachmentItemIds.TryParseItemId( ThornsItemIdAliases.Canonicalize( ThornsDragState.ItemId ), out var attachment )
		       && ThornsAttachmentCatalog.IsCompatible( combatId, attachment );
	}

	public static bool TryCompleteHoveredDrop()
	{
		if ( !ThornsDragState.IsDragging )
			return false;

		RefreshDropTarget();

		if ( _dropTarget is not null && _dropTarget.IsValid )
		{
			_dropTarget.TryCompleteDragDrop();
			_dropTarget = null;
			return true;
		}

		return false;
	}

	static bool IsLeftMouse( MousePanelEvent e ) =>
		e.MouseButton == MouseButtons.Left || string.Equals( e.Button, "mouseleft", StringComparison.OrdinalIgnoreCase );

	static bool IsShiftHeld() =>
		Input.Keyboard.Down( "Shift" ) || Input.Down( "Run" );

	void TryQuickTransferShiftClick()
	{
		var stack = FindStack();
		if ( stack is null || stack.Value.IsEmpty )
			return;

		var containerKey = ThornsUiClientState.Snapshot.ExternalContainer?.ContainerKey ?? "";
		ThornsPlayerGameplay.Local?.RequestMoveItem( new ThornsMoveItemRequest
		{
			FromContainer = Container,
			FromIndex = Index,
			Mode = ThornsMoveItemMode.QuickTransfer,
			ShiftHeld = true,
			WorldContainerKey = containerKey
		} );
		_onChanged?.Invoke();
	}

	void FinalizeSelect( ThornsItemStack? stack )
	{
		ThornsUiSfx.PlayButtonClick();

		if ( _isHotbar && !ThornsMenuHost.IsOpen )
			ThornsPlayerGameplay.Local?.RequestHotbarSlot( Index );

		SelectSlot( stack );
	}

	protected override void OnMouseDown( MousePanelEvent e )
	{
		base.OnMouseDown( e );

		if ( !IsLeftMouse( e ) )
			return;

		var stack = FindStack();

		if ( IsShiftHeld() )
		{
			if ( stack is not null && !stack.Value.IsEmpty )
			{
				e.StopPropagation();
				TryQuickTransferShiftClick();
			}
			else
			{
				FinalizeSelect( stack );
			}

			return;
		}

		if ( stack is null || stack.Value.IsEmpty )
		{
			e.StopPropagation();
			return;
		}

		e.StopPropagation();
		AddClass( "drag-source" );
		SetMouseCapture( false );
		ThornsDragState.Begin( Container, Index, stack.Value.ItemId, stack.Value.Count );
	}

	protected override void OnMouseUp( MousePanelEvent e )
	{
		base.OnMouseUp( e );

		if ( !IsLeftMouse( e ) )
			return;

		RemoveClass( "drag-source" );

		var stack = FindStack();

		if ( IsShiftHeld() && stack is not null && !stack.Value.IsEmpty )
		{
			e.StopPropagation();
			ThornsDragState.Clear();
			TryQuickTransferShiftClick();
			return;
		}

		if ( !ThornsDragState.IsDragging )
		{
			FinalizeSelect( stack );
			return;
		}

		e.StopPropagation();

		var moved = ThornsDragState.PointerMoved;
		var suppressSelect = ThornsDragState.ConsumeClickSuppression();

		if ( moved )
		{
			ThornsAttachmentInspectSlot.RefreshDropTarget();
			if ( ThornsAttachmentInspectSlot.TryCompleteHoveredDrop() )
			{
				suppressSelect = true;
				return;
			}

			RefreshDropTarget();
			if ( _dropTarget is not null && _dropTarget.IsValid )
				_dropTarget.TryCompleteDragDrop();
			else
				TryCompleteDragDrop();

			suppressSelect = true;
		}

		ThornsDragState.Clear();

		if ( !moved && !suppressSelect )
			FinalizeSelect( stack );
	}

	bool IsSameAsDragSource()
	{
		if ( ThornsDragState.FromContainer != Container )
			return false;

		if ( Container is ThornsContainerKind.Head or ThornsContainerKind.Chest or ThornsContainerKind.Legs )
			return true;

		return ThornsDragState.FromIndex == Index;
	}

	void TryCompleteDragDrop()
	{
		if ( !ThornsDragState.IsDragging )
			return;

		if ( IsSameAsDragSource() )
		{
			ThornsDragState.Clear();
			return;
		}

		if ( CanAcceptAttachmentEquip() )
		{
			ThornsAttachmentDragDebug.Write(
				$"equip via weapon slot {Container}[{Index}] item={ThornsDragState.ItemId} from={ThornsDragState.FromContainer}[{ThornsDragState.FromIndex}]" );
			ThornsPlayerGameplay.Local?.RequestEquipAttachment( new ThornsEquipAttachmentRequest
			{
				WeaponContainer = Container,
				WeaponIndex = Index,
				AttachmentSlotIndex = -1,
				FromContainer = ThornsDragState.FromContainer,
				FromIndex = ThornsDragState.FromIndex
			} );

			ThornsDragState.MarkDropHandled();
			ThornsDragState.Clear();
			_onChanged?.Invoke();
			return;
		}

		var mode = IsShiftHeld() ? ThornsMoveItemMode.QuickTransfer : ThornsMoveItemMode.Move;
		var containerKey = ThornsUiClientState.Snapshot.ExternalContainer?.ContainerKey ?? "";
		ThornsPlayerGameplay.Local?.RequestMoveItem( new ThornsMoveItemRequest
		{
			FromContainer = ThornsDragState.FromContainer,
			FromIndex = ThornsDragState.FromIndex,
			ToContainer = Container,
			ToIndex = Index,
			Mode = mode,
			ShiftHeld = IsShiftHeld(),
			WorldContainerKey = containerKey
		} );

		ThornsDragState.MarkDropHandled();
		ThornsDragState.Clear();
		_onChanged?.Invoke();
	}

	void OnDoubleClick( PanelEvent e )
	{
		_ = e;
		var containerKey = ThornsUiClientState.Snapshot.ExternalContainer?.ContainerKey ?? "";
		ThornsPlayerGameplay.Local?.RequestMoveItem( new ThornsMoveItemRequest
		{
			FromContainer = Container,
			FromIndex = Index,
			Mode = ThornsMoveItemMode.DoubleClickTransfer,
			WorldContainerKey = containerKey
		} );
	}

	void OnContextMenu( PanelEvent e )
	{
		_ = e;
		var stack = FindStack();
		if ( stack is null || stack.Value.IsEmpty )
			return;

		if ( ThornsSurvivalConsumables.IsConsumable( stack.Value.ItemId ) )
		{
			ThornsPlayerGameplay.Local?.RequestConsumeFromSlot( Container, Index );
			return;
		}

		var containerKey = ThornsUiClientState.Snapshot.ExternalContainer?.ContainerKey ?? "";
		ThornsPlayerGameplay.Local?.RequestMoveItem( new ThornsMoveItemRequest
		{
			FromContainer = Container,
			FromIndex = Index,
			Mode = ThornsMoveItemMode.SplitHalf,
			WorldContainerKey = containerKey
		} );
	}
}
