namespace Terraingen.UI.Hud;

using Sandbox.UI;
using Terraingen.Economy;
using Terraingen.GameData;
using Terraingen.Player;
using Terraingen.UI;
using Terraingen.UI.Core;

/// <summary>Field supply radio shop — rotating buy offers and per-slot sell.</summary>
public sealed class ThornsRadioShopHud
{
	readonly Panel _backdrop;
	readonly Panel _root;
	readonly Panel _offersScroll;
	readonly Panel _sellScroll;
	readonly Label _title;
	readonly Label _metalLabel;
	readonly Label _rotationLabel;
	readonly Label _hint;

	Guid _stationId;
	double _nextRefreshRealtime;

	public bool IsOpen => _backdrop.IsValid() && _backdrop.Style.Display == DisplayMode.Flex;

	public Panel Backdrop => _backdrop;

	Action<UiRevisionChannel, int> _onRevision;

	public ThornsRadioShopHud( Panel parent )
	{
		(_backdrop, _root) = ThornsMenuChrome.CreateOverlayShell( parent, "radio-shop-hud", 920 );
		_backdrop.AddClass( "radio-shop-backdrop" );
		_backdrop.Style.Display = DisplayMode.None;
		ThornsUiLayer.ApplyModalSurface( _backdrop, ThornsUiPriority.NpcDialog );

		ThornsTheme.CreateStationOverlayHeader(
			_root,
			out _title,
			"FIELD SUPPLY — RADIO",
			() => ThornsPlayerGameplay.Local?.RequestCloseRadioShop(),
			"radio-shop-title" );

		_metalLabel = ThornsUiFactory.AddLabel( _root, "Metal ingots: —", "radio-shop-meta thorns-station-meta" );
		_rotationLabel = ThornsUiFactory.AddPassiveLabel( _root, "", "radio-shop-meta thorns-station-meta" );
		_hint = ThornsUiFactory.AddPassiveLabel(
			_root,
			"Pay with metal ingots · Sell ×1 per click · Stock rerolls every 5 minutes",
			"radio-shop-hint thorns-muted thorns-station-hint" );
		_hint.Style.MarginBottom = Length.Pixels( 10 );

		var body = ThornsUiFactory.AddPanel( _root, "radio-shop-body thorns-station-body" );
		body.Style.FlexDirection = FlexDirection.Row;
		body.Style.FlexGrow = 1;
		body.Style.FlexShrink = 1;
		body.Style.MinHeight = Length.Pixels( 0 );

		var buyCol = ThornsTheme.CreateStationColumn( body, "radio-shop-column" );
		ThornsTheme.CreateSectionHeader( buyCol, "BUY" );
		_offersScroll = ThornsUiFactory.AddPanel( buyCol, "radio-shop-scroll" );
		_offersScroll.Style.FlexGrow = 1;
		_offersScroll.Style.FlexShrink = 1;
		_offersScroll.Style.MinHeight = Length.Pixels( 0 );
		_offersScroll.Style.Overflow = OverflowMode.Scroll;

		ThornsTheme.CreateWoodColumnDivider( body );

		var sellCol = ThornsTheme.CreateStationColumn( body, "radio-shop-column" );
		ThornsTheme.CreateSectionHeader( sellCol, "SELL" );
		_sellScroll = ThornsUiFactory.AddPanel( sellCol, "radio-shop-scroll" );
		_sellScroll.Style.FlexGrow = 1;
		_sellScroll.Style.FlexShrink = 1;
		_sellScroll.Style.MinHeight = Length.Pixels( 0 );
		_sellScroll.Style.Overflow = OverflowMode.Scroll;

		_backdrop.AddEventListener( "onmouseup", e =>
		{
			if ( e.Target == _backdrop )
				ThornsPlayerGameplay.Local?.RequestCloseRadioShop();
		} );

		_onRevision = OnRevision;
		UiRevisionBus.MenuRevisionChanged += _onRevision;
		Refresh();
	}

	public void Dispose() => UiRevisionBus.MenuRevisionChanged -= _onRevision;

	void OnRevision( UiRevisionChannel channel, int _ )
	{
		if ( channel is UiRevisionChannel.RadioShop or UiRevisionChannel.Inventory )
			Refresh();
	}

	public void Refresh()
	{
		if ( !_backdrop.IsValid() )
			return;

		var shop = ThornsUiClientState.Snapshot.RadioShop;
		var open = shop?.IsOpen == true && !string.IsNullOrWhiteSpace( shop.StationId );
		_backdrop.Style.Display = open ? DisplayMode.Flex : DisplayMode.None;

		if ( !open )
		{
			_stationId = Guid.Empty;
			ClearScroll( _offersScroll );
			ClearScroll( _sellScroll );
			return;
		}

		_stationId = Guid.TryParse( shop.StationId, out var g ) ? g : Guid.Empty;

		var currency = ThornsRadioShopCatalog.CurrencyItemId;
		var metal = CountCurrencyInSnapshot( currency );
		_metalLabel.Text = $"Metal ingots in inventory: {metal}";

		var period = ThornsRadioShopRotation.RotationPeriodSeconds;
		var next = (ThornsRadioShopRotation.CurrentEpochIndexHost() + 1) * period;
		var remain = Math.Max( 0.0, next - Time.Now );
		var mm = (int)( remain / 60 );
		var ss = (int)( remain % 60 );
		_rotationLabel.Text = $"Stock refresh in {mm:00}:{ss:00}";

		if ( Input.Down( "Attack1" ) || Input.Down( "attack1" ) )
			return;

		if ( Time.Now < _nextRefreshRealtime && _offersScroll.Children.Count() > 0 )
			return;

		_nextRefreshRealtime = Time.Now + 0.35;
		RebuildOffers( shop );
		RebuildSellList();
	}

	static void ClearScroll( Panel scroll )
	{
		if ( !scroll.IsValid() )
			return;

		scroll.DeleteChildren( true );
	}

	void RebuildOffers( ThornsRadioShopSnapshotDto shop )
	{
		ClearScroll( _offersScroll );
		var offers = shop.Offers;
		if ( offers is null || offers.Count == 0 )
		{
			ThornsUiFactory.AddPassiveLabel( _offersScroll, "No offers available.", "radio-shop-empty thorns-muted" );
			return;
		}

		for ( var i = 0; i < offers.Count; i++ )
		{
			var slot = i;
			var offer = offers[i];
			var def = ThornsItemRegistry.TryGet( offer.ItemId, out var d ) ? d : null;
			var titleTxt = def?.DisplayName ?? offer.ItemId;
			var cap = Math.Max( 1, offer.MaxBuy );

			var row = ThornsUiFactory.AddPanel( _offersScroll, "radio-shop-offer-row thorns-interact" );
			row.Style.FlexDirection = FlexDirection.Row;
			row.Style.JustifyContent = Justify.SpaceBetween;
			row.Style.AlignItems = Align.Center;
			row.Style.MinHeight = Length.Pixels( 48 );
			row.Style.MarginBottom = Length.Pixels( 4 );

			ThornsUiFactory.AddPassiveLabel(
				row,
				$"{titleTxt} — {offer.BuyPrice} ingots each (max {cap})",
				"radio-shop-row-label" );

			var btnRow = ThornsUiFactory.AddPanel( row, "radio-shop-btn-row" );
			btnRow.Style.FlexDirection = FlexDirection.Row;
			ThornsUiFactory.AddClickable( btnRow, "radio-shop-btn thorns-btn-primary", "×1",
				() => TryBuy( slot, 1, cap ) );
			ThornsUiFactory.AddClickable( btnRow, "radio-shop-btn thorns-btn-primary", "×5",
				() => TryBuy( slot, 5, cap ) );
		}
	}

	void TryBuy( int slot, int qty, int cap )
	{
		if ( _stationId == Guid.Empty )
			return;

		ThornsPlayerGameplay.Local?.RequestRadioBuy( _stationId, slot, Math.Clamp( qty, 1, cap ) );
	}

	void RebuildSellList()
	{
		ClearScroll( _sellScroll );
		var inv = ThornsUiClientState.Snapshot.Inventory?.Slots;
		if ( inv is null || inv.Count == 0 )
		{
			ThornsUiFactory.AddPassiveLabel( _sellScroll, "Inventory empty.", "radio-shop-empty thorns-muted" );
			return;
		}

		foreach ( var slot in inv )
		{
			if ( slot.Container is not (ThornsContainerKind.Inventory or ThornsContainerKind.Hotbar) )
				continue;

			var kind = slot.Container;
			var index = slot.Index;
			var hasItem = !string.IsNullOrEmpty( slot.ItemId ) && slot.Count > 0;
			var def = hasItem && ThornsItemRegistry.TryGet( slot.ItemId, out var d ) ? d : null;
			var nm = def?.DisplayName ?? slot.ItemId;

			var row = ThornsUiFactory.AddPanel( _sellScroll, "radio-shop-sell-row thorns-interact" );
			row.Style.FlexDirection = FlexDirection.Row;
			row.Style.JustifyContent = Justify.SpaceBetween;
			row.Style.AlignItems = Align.Center;
			row.Style.MinHeight = Length.Pixels( 44 );
			row.Style.MarginBottom = Length.Pixels( 4 );

			string label;
			if ( !hasItem )
				label = $"{kind} {index}: empty";
			else if ( ThornsRadioShopCatalog.IsCurrencyTradeBlockedFromRadioShop( slot.ItemId ) )
				label = $"{nm} ×{slot.Count} — not sold here";
			else
			{
				var mirror = new ThornsInventorySlotMirrorDto
				{
					ItemId = slot.ItemId,
					Count = slot.Count,
					HasDurability = slot.HasDurability,
					Durability = slot.Durability
				};
				var quote = ThornsRadioShopCatalog.ClientEstimateSellDisplay( mirror );
				label = $"{nm} ×{slot.Count} → ~{quote} ingots / unit";
			}

			ThornsUiFactory.AddPassiveLabel( row, label, "radio-shop-row-label" );

			if ( hasItem && !ThornsRadioShopCatalog.IsCurrencyTradeBlockedFromRadioShop( slot.ItemId ) )
			{
				ThornsUiFactory.AddClickable( row, "radio-shop-btn thorns-btn-primary", "Sell ×1",
					() =>
					{
						if ( _stationId != Guid.Empty )
							ThornsPlayerGameplay.Local?.RequestRadioSell( _stationId, kind, index, 1 );
					} );
			}
		}
	}

	static int CountCurrencyInSnapshot( string itemId )
	{
		var total = 0;
		foreach ( var slot in ThornsUiClientState.Snapshot.Inventory?.Slots ?? [] )
		{
			if ( slot.ItemId == itemId )
				total += slot.Count;
		}

		return total;
	}
}
