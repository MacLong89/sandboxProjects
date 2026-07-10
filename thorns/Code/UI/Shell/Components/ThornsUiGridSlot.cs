#nullable disable

using System;
using Sandbox.UI;

namespace Sandbox;

/// <summary>Inventory / toolbar cell — large icon (texture or glyph), optional stack badge, optional durability strip.</summary>
public class ThornsUiGridSlot : Panel
{
	public int SlotIndex { get; }
	readonly bool _toolbar;

	public Action<int> OnPressed { get; set; }
	public Action<int, MouseButtons> OnInventoryPointerDown { get; set; }
	public Action<int, MouseButtons> OnInventoryPointerUp { get; set; }
	public Action<int> OnHoverEnter { get; set; }
	public Action<int> OnHoverLeave { get; set; }

	Label _bind;
	Panel _iconStack;
	Panel _iconHost;
	Panel _iconFg;
	Label _iconAbbr;
	Label _qty;
	Panel _durTrack;
	Panel _durFill;

	public ThornsUiGridSlot( int slotIndex, bool toolbar = false )
	{
		_toolbar = toolbar;
		SlotIndex = slotIndex;
		AddClass( "thorns-grid-slot" );
		if ( toolbar )
			AddClass( "thorns-grid-slot--toolbar" );

		Style.PointerEvents = PointerEvents.All;
		Style.FlexDirection = FlexDirection.Column;
		Style.JustifyContent = Justify.FlexStart;
		Style.AlignItems = Align.Stretch;

		if ( toolbar )
		{
			_bind = AddChild( new Label( "", "thorns-grid-slot-bind" ) );
			_bind.Style.PointerEvents = PointerEvents.None;
		}

		_iconStack = ThornsUiPanelAdd.AddChildPanel( this, "thorns-grid-slot-icon-stack" );
		_iconStack.Style.FlexDirection = FlexDirection.Column;
		_iconStack.Style.FlexGrow = 1;
		_iconStack.Style.FlexShrink = 1;
		_iconStack.Style.MinHeight = 0;
		_iconStack.Style.Width = Length.Fraction( 1f );
		_iconStack.Style.Position = PositionMode.Relative;

		_iconHost = ThornsUiPanelAdd.AddChildPanel( _iconStack, "thorns-grid-slot-icon-host" );
		_iconHost.Style.FlexGrow = 1;
		_iconHost.Style.PointerEvents = PointerEvents.None;

		_iconFg = _iconHost.AddChild( new Panel() );
		_iconFg.AddClass( "thorns-grid-slot-icon-img" );
		_iconFg.Style.PointerEvents = PointerEvents.None;

		_iconAbbr = _iconHost.AddChild( new Label( "", "thorns-grid-slot-icon-abbr thorns-grid-slot-icon-abbr--hidden" ) );
		_iconAbbr.Style.PointerEvents = PointerEvents.None;

		_qty = _iconStack.AddChild( new Label( "", "thorns-grid-slot-qty" ) );
		_qty.Style.PointerEvents = PointerEvents.None;

		_durTrack = ThornsUiPanelAdd.AddChildPanel( this, "thorns-grid-slot-dur thorns-grid-slot-dur--hidden" );
		_durTrack.Style.FlexShrink = 0;
		_durTrack.Style.PointerEvents = PointerEvents.None;
		_durFill = ThornsUiPanelAdd.AddChildPanel( _durTrack, "thorns-grid-slot-dur-fill" );
		_durFill.Style.PointerEvents = PointerEvents.None;
	}

	protected override void OnMouseOver( MousePanelEvent e )
	{
		base.OnMouseOver( e );
		OnHoverEnter?.Invoke( SlotIndex );
	}

	protected override void OnMouseOut( MousePanelEvent e )
	{
		base.OnMouseOut( e );
		OnHoverLeave?.Invoke( SlotIndex );
	}

	public override bool WantsMouseInput() =>
		OnInventoryPointerDown is not null || OnInventoryPointerUp is not null || OnPressed is not null
		|| OnHoverEnter is not null || OnHoverLeave is not null;

	protected override void OnMouseDown( MousePanelEvent e )
	{
		base.OnMouseDown( e );
		if ( OnInventoryPointerDown is not null )
		{
			OnInventoryPointerDown( SlotIndex, e.MouseButton );
			// Default UI capture pins hover to the pressed cell; shell DnD needs :hover on other slots while LMB is held.
			if ( e.MouseButton == MouseButtons.Left )
				SetMouseCapture( false );
			return;
		}

		if ( e.MouseButton == MouseButtons.Left && OnPressed is not null )
			OnPressed( SlotIndex );
	}

	protected override void OnMouseUp( MousePanelEvent e )
	{
		base.OnMouseUp( e );
		OnInventoryPointerUp?.Invoke( SlotIndex, e.MouseButton );
	}

	static readonly Color ToolbarAbbrevDefaultTint = new( 236f / 255f, 240f / 255f, 248f / 255f, 0.95f );

	/// <summary>Storage-row placeholder hook — clears any prior item visual (captions unused for mirror-driven chest UI).</summary>
	public void SetContent( string main, string sub, Color? primaryLineTint = null )
	{
		if ( _toolbar )
			return;

		ClearItemVisual();
	}

	/// <summary>Backpack, chest, overlay slots — same visuals as toolbar (large icon + badge + durability).</summary>
	public void SetMirrorSlotVisual( in ThornsInventorySlotNet net, Color? weaponRowTint = null ) =>
		ApplySlotFromMirror( in net, bindDigit: 0, weaponRowTint );

	/// <summary>Hotbar slots: bind digit, texture or glyph, stack badge, durability strip.</summary>
	public void SetToolbarFromMirror( ThornsInventorySlotNet net, int bindDigit ) =>
		ApplySlotFromMirror( in net, bindDigit, weaponRowTint: null );

	void ApplySlotFromMirror( in ThornsInventorySlotNet net, int bindDigit, Color? weaponRowTint )
	{
		if ( _toolbar && _bind is not null )
			_bind.Text = bindDigit > 0 ? $"{bindDigit}" : "";

		if ( string.IsNullOrWhiteSpace( net.ItemId ) || net.Quantity <= 0 )
		{
			ClearItemVisual();
			return;
		}

		ThornsItemRegistry.TryGet( net.ItemId, out var def );

		var usedTex = TryApplyIconTexture( ThornsItemHudIcons.ResolveLoadPath( def ) );
		if ( usedTex )
		{
			if ( _iconAbbr is not null )
			{
				_iconAbbr.Text = "";
				_iconAbbr.SetClass( "thorns-grid-slot-icon-abbr--hidden", true );
				_iconAbbr.SetClass( "thorns-grid-slot-icon-abbr--glyph", false );
			}

			_iconFg.SetClass( "thorns-grid-slot-icon-img--hidden", false );
		}
		else
		{
			_iconFg.Style.BackgroundImage = null;
			_iconFg.SetClass( "thorns-grid-slot-icon-img--hidden", true );
			if ( _iconAbbr is not null )
			{
				_iconAbbr.Text = ThornsUiInventoryFormatting.ItemGlyph( net.ItemId );
				_iconAbbr.SetClass( "thorns-grid-slot-icon-abbr--hidden", false );
				_iconAbbr.SetClass( "thorns-grid-slot-icon-abbr--glyph", true );
				_iconAbbr.Style.FontColor = ResolveAbbrevTint( in net, def, weaponRowTint );
			}
		}

		if ( def is { ItemType: ThornsItemType.Weapon } )
		{
			ThornsUiWeaponInspectFormatting.ResolveWeaponRoll( net, out var rarity, out _, out _ );
			_iconHost.Style.BackgroundColor = rarity.RarityInventorySlotBackdropTint();
		}
		else if ( def is { ItemType: ThornsItemType.Armor } )
		{
			ThornsUiArmorInspectFormatting.ResolveArmorRoll( net, out var rarity, out _ );
			_iconHost.Style.BackgroundColor = rarity.RarityInventorySlotBackdropTint();
		}
		else if ( usedTex )
			_iconHost.Style.BackgroundColor = Color.Transparent;
		else
			_iconHost.Style.BackgroundColor = ThornsUiItemSlotVisuals.FallbackBackdrop( def?.ItemType );

		_qty.Text = ThornsItemHudIcons.ToolbarStackBadgeText( def, net.Quantity );
		ApplyDurabilityBar( in net, def );
	}

	Color ResolveAbbrevTint( in ThornsInventorySlotNet net, ThornsItemRegistry.ThornsItemDefinition def, Color? weaponRowTint )
	{
		if ( _toolbar )
		{
			if ( def?.ItemType == ThornsItemType.Weapon )
				return ThornsUiWeaponInspectFormatting.ResolveAbbrevToolbarTint( net );
			if ( def?.ItemType == ThornsItemType.Armor )
				return ThornsUiArmorInspectFormatting.ResolveAbbrevToolbarTint( net );
			return ToolbarAbbrevDefaultTint;
		}

		return weaponRowTint ?? ThornsUiWeaponInspectFormatting.DefaultInventorySlotPrimaryTint;
	}

	void ApplyDurabilityBar( in ThornsInventorySlotNet net, ThornsItemRegistry.ThornsItemDefinition def )
	{
		var fill01 = ResolveDurabilityFill01( in net, def );
		if ( fill01 < 0f || _durTrack is null || _durFill is null )
		{
			_durTrack?.SetClass( "thorns-grid-slot-dur--hidden", true );
			return;
		}

		_durTrack.SetClass( "thorns-grid-slot-dur--hidden", false );
		_durFill.Style.Width = Length.Fraction( fill01 );
		// Solid color — UI stylesheet gradients often fail to paint on narrow strips; tier read at a glance.
		_durFill.Style.BackgroundColor = DurabilityStripFillColor( fill01 );
	}

	static Color DurabilityStripFillColor( float fill01 )
	{
		if ( fill01 >= 0.55f )
			return new Color( 0.35f, 0.92f, 0.78f, 1f );
		if ( fill01 >= 0.30f )
			return new Color( 1f, 0.85f, 0.38f, 1f );
		if ( fill01 >= 0.12f )
			return new Color( 1f, 0.58f, 0.28f, 1f );
		return new Color( 1f, 0.38f, 0.38f, 1f );
	}

	static float ResolveDurabilityFill01( in ThornsInventorySlotNet net, ThornsItemRegistry.ThornsItemDefinition def )
	{
		if ( net.HasDurability == 0 || def is null )
			return -1f;

		if ( def.ItemType == ThornsItemType.Weapon && !string.IsNullOrWhiteSpace( def.CombatWeaponDefinitionId ) )
		{
			var w = ThornsWeaponDefinitions.Get( def.CombatWeaponDefinitionId );
			if ( w.MaxDurability <= 0.001f )
				return -1f;
			return Math.Clamp( net.Durability / w.MaxDurability, 0f, 1f );
		}

		if ( def.ItemType == ThornsItemType.Armor && def.ArmorMaxDurability > 0.001f )
			return Math.Clamp( net.Durability / def.ArmorMaxDurability, 0f, 1f );

		if ( def.ItemType == ThornsItemType.Tool && def.ToolMaxDurability > 0.001f )
			return Math.Clamp( net.Durability / def.ToolMaxDurability, 0f, 1f );

		return -1f;
	}

	void ClearItemVisual()
	{
		_iconFg.Style.BackgroundImage = null;
		_iconFg.SetClass( "thorns-grid-slot-icon-img--hidden", false );
		if ( _iconAbbr is not null )
		{
			_iconAbbr.Text = "";
			_iconAbbr.SetClass( "thorns-grid-slot-icon-abbr--hidden", true );
			_iconAbbr.SetClass( "thorns-grid-slot-icon-abbr--glyph", false );
		}

		_iconHost.Style.BackgroundColor = ThornsUiItemSlotVisuals.EmptySlotBackdrop;
		_qty.Text = "";
		_durTrack?.SetClass( "thorns-grid-slot-dur--hidden", true );
		if ( _durFill is not null )
		{
			_durFill.Style.Width = Length.Fraction( 0f );
			_durFill.Style.BackgroundColor = Color.Transparent;
		}
	}

	bool TryApplyIconTexture( string path )
	{
		if ( !ThornsItemHudIcons.TryGetToolbarTexture( path, out var tex ) )
		{
			_iconFg.Style.BackgroundImage = null;
			return false;
		}

		_iconFg.Style.BackgroundImage = tex;
		return true;
	}

	public void SetHighlighted( bool on ) => SetClass( "thorns-grid-slot--hover", on );
	public void SetSelected( bool on ) => SetClass( "thorns-grid-slot--selected", on );
	public void SetDragSource( bool on ) => SetClass( "thorns-grid-slot--drag-source", on );
}
