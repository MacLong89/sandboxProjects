#nullable disable

using Sandbox.UI;

namespace Sandbox;

/// <summary>One equipped armor cell (head / chest / legs) - same interactions as legacy inventory overlay.</summary>
public sealed class ThornsUiArmorEquipSlot : Panel
{
	public int ArmorSlotIndex { get; }

	readonly Label _cap;
	readonly Panel _iconHost;
	readonly Panel _iconImg;
	readonly Label _icon;
	readonly Label _dur;

	public Action<int, MouseButtons> OnArmorPointerDown { get; set; }
	public Action<int, MouseButtons> OnArmorPointerUp { get; set; }
	public Action<int> OnArmorHoverEnter { get; set; }
	public Action<int> OnArmorHoverLeave { get; set; }

	public ThornsUiArmorEquipSlot( int armorSlotIndex, string caption )
	{
		ArmorSlotIndex = armorSlotIndex;
		AddClass( "thorns-armor-equip-slot" );
		Style.PointerEvents = PointerEvents.All;

		_cap = AddChild( new Label( caption, "thorns-armor-equip-cap" ) );
		_cap.Style.PointerEvents = PointerEvents.None;

		_iconHost = ThornsUiPanelAdd.AddChildPanel( this, "thorns-armor-equip-icon-host" );
		_iconHost.Style.FlexDirection = FlexDirection.Column;
		_iconHost.Style.AlignItems = Align.Center;
		_iconHost.Style.JustifyContent = Justify.Center;
		_iconHost.Style.PointerEvents = PointerEvents.None;
		_iconHost.Style.Width = Length.Pixels( 40 );
		_iconHost.Style.Height = Length.Pixels( 40 );
		_iconHost.Style.FlexShrink = 0;

		_iconImg = ThornsUiPanelAdd.AddChildPanel( _iconHost, "thorns-armor-equip-icon-img thorns-armor-equip-icon-img--hidden" );
		_iconImg.Style.PointerEvents = PointerEvents.None;

		_icon = _iconHost.AddChild( new Label( "-", "thorns-armor-equip-icon" ) );
		_icon.Style.PointerEvents = PointerEvents.None;

		_dur = AddChild( new Label( "", "thorns-armor-equip-dur" ) );
		_dur.Style.PointerEvents = PointerEvents.None;
	}

	protected override void OnMouseOver( MousePanelEvent e )
	{
		base.OnMouseOver( e );
		OnArmorHoverEnter?.Invoke( ArmorSlotIndex );
	}

	protected override void OnMouseOut( MousePanelEvent e )
	{
		base.OnMouseOut( e );
		OnArmorHoverLeave?.Invoke( ArmorSlotIndex );
	}

	public override bool WantsMouseInput() =>
		OnArmorPointerDown is not null || OnArmorPointerUp is not null || OnArmorHoverEnter is not null
		|| OnArmorHoverLeave is not null;

	protected override void OnMouseDown( MousePanelEvent e )
	{
		base.OnMouseDown( e );
		OnArmorPointerDown?.Invoke( ArmorSlotIndex, e.MouseButton );
		if ( e.MouseButton == MouseButtons.Left )
			SetMouseCapture( false );
	}

	protected override void OnMouseUp( MousePanelEvent e )
	{
		base.OnMouseUp( e );
		OnArmorPointerUp?.Invoke( ArmorSlotIndex, e.MouseButton );
	}

	public void ApplyMirror( ThornsArmorEquipment armor )
	{
		if ( !armor.IsValid() )
		{
			ClearVisual();
			return;
		}

		armor.GetClientMirrorEquippedPieceFull( ArmorSlotIndex, out var id, out var dur, out var roll );

		if ( string.IsNullOrWhiteSpace( id ) )
		{
			ClearVisual();
			return;
		}

		var rollNet = new ThornsInventorySlotNet { ItemId = id, Quantity = 1, ArmorRollPayload = roll ?? "" };
		ThornsUiArmorInspectFormatting.ResolveArmorRoll( rollNet, out var rarity, out _ );
		_iconHost.Style.BackgroundColor = rarity.RarityInventorySlotBackdropTint();

		if ( ThornsItemRegistry.TryGet( id, out var def )
		     && TryApplyIconTexture( ThornsItemHudIcons.ResolveLoadPath( def ) ) )
		{
			_icon.Text = "";
			_icon.SetClass( "thorns-armor-equip-icon--hidden", true );
			_iconImg.SetClass( "thorns-armor-equip-icon-img--hidden", false );
		}
		else
		{
			_iconImg.Style.BackgroundImage = null;
			_iconImg.SetClass( "thorns-armor-equip-icon-img--hidden", true );
			_icon.Text = ThornsUiInventoryFormatting.ItemGlyph( id );
			_icon.Style.FontColor = rarity.TintApprox();
			_icon.SetClass( "thorns-armor-equip-icon--hidden", false );
		}

		_dur.Text = dur <= 0f ? "" : $"{dur:F0}";
	}

	void ClearVisual()
	{
		_iconHost.Style.BackgroundColor = Color.Transparent;
		_iconImg.Style.BackgroundImage = null;
		_iconImg.SetClass( "thorns-armor-equip-icon-img--hidden", true );
		_icon.Text = "-";
		_icon.Style.FontColor = ThornsUiWeaponInspectFormatting.DefaultInventorySlotPrimaryTint;
		_icon.SetClass( "thorns-armor-equip-icon--hidden", false );
		_dur.Text = "";
	}

	bool TryApplyIconTexture( string path )
	{
		if ( !ThornsItemHudIcons.TryGetToolbarTexture( path, out var tex ) )
		{
			_iconImg.Style.BackgroundImage = null;
			return false;
		}

		_iconImg.Style.BackgroundImage = tex;
		return true;
	}

	public void SetHoverDrop( bool on ) => SetClass( "thorns-armor-equip-slot--hover", on );
	public void SetDragSource( bool on ) => SetClass( "thorns-armor-equip-slot--drag-source", on );
	public void SetSelected( bool on ) => SetClass( "thorns-armor-equip-slot--selected", on );
}
