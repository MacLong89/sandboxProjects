#nullable disable

using Sandbox.UI;

namespace Sandbox;

public sealed partial class ThornsGameShell
{
	public bool RadioShopUiOpen { get; private set; }

	Guid _radioShopStationId;
	long _radioCatalogEpoch;
	string[] _radioCatalogItemIds;
	int[] _radioCatalogBuyPrices;
	int[] _radioCatalogMaxBuy;
	Label _radioShopMetalLabel;
	Label _radioShopRotationLabel;

	Panel _radioShopLayer;
	Panel _radioShopCard;
	Panel _radioShopOffersScroll;
	Panel _radioShopSellScroll;
	bool _radioShopUiBuilt;

	int _radioShopLastInvMirrorRev = int.MinValue;
	int _radioShopLastInvMetal = int.MinValue;
	double _radioShopNextForcedUiPulseRealtime;

	/// <summary>Owner-local: center-left card while aiming at a radio in range (same stack as tame stun prompt).</summary>
	public void SetRadioShopLookPrompt( bool active )
	{
		if ( !IsLocalOwned )
			return;

		_radioShopLookPromptActive = active;
	}

	public void ApplyRadioShopCatalog( long epoch, string[] itemIds, int[] buyPrices, int[] maxBuy )
	{
		_radioCatalogEpoch = epoch;
		_radioCatalogItemIds = itemIds ?? Array.Empty<string>();
		_radioCatalogBuyPrices = buyPrices ?? Array.Empty<int>();
		_radioCatalogMaxBuy = maxBuy ?? Array.Empty<int>();
	}

	public void SetRadioShopOpen( bool open, Guid stationId = default )
	{
		if ( open )
		{
			CloseStorageChestUi();
			CloseCampfireUi();
			CloseWorkbenchUi();
		}

		RadioShopUiOpen = open;
		_radioShopStationId = open ? stationId : Guid.Empty;

		if ( open )
		{
			SetRadioShopLookPrompt( false );
			EnsureRadioShopUiBuilt();
			if ( _radioShopLayer.IsValid )
				SetRadioShopLayerVisible( true );
			_radioShopLastInvMirrorRev = int.MinValue;
			_radioShopLastInvMetal = int.MinValue;
			_radioShopNextForcedUiPulseRealtime = 0;
			RefreshRadioShopPanels();
		}
		else
		{
			if ( _radioShopLayer.IsValid )
				SetRadioShopLayerVisible( false );
			RefreshRadioShopPanels();
		}
	}

	public void CloseRadioShopUi()
	{
		if ( !RadioShopUiOpen && !_radioShopUiBuilt )
			return;

		RadioShopUiOpen = false;
		_radioShopStationId = Guid.Empty;
		_radioShopLastInvMirrorRev = int.MinValue;
		_radioShopLastInvMetal = int.MinValue;
		if ( _radioShopLayer.IsValid )
			SetRadioShopLayerVisible( false );
	}

	void SetRadioShopLayerVisible( bool visible )
	{
		if ( !_radioShopLayer.IsValid )
			return;
		_radioShopLayer.Style.Display = visible ? DisplayMode.Flex : DisplayMode.None;
		_radioShopLayer.Style.PointerEvents = visible ? PointerEvents.All : PointerEvents.None;
	}

	void TickRadioShopProximity()
	{
		if ( !RadioShopUiOpen || _radioShopStationId == Guid.Empty )
			return;

		if ( !ThornsRadioStation.ActiveById.TryGetValue( _radioShopStationId, out var st ) || !st.IsValid() )
		{
			CloseRadioShopUi();
			return;
		}

		if ( !st.HostIsInRange( GameObject.WorldPosition ) )
			CloseRadioShopUi();
	}

	/// <summary>Throttled refresh while shop is open (inventory mirror + metal stacks + rotation countdown).</summary>
	void TickRadioShopUiRefreshFromMirror()
	{
		var inv = Components.Get<ThornsInventory>();
		var invRev = inv.IsValid() ? inv.ClientMirrorRevision : 0;
		var metal = inv.IsValid()
			? inv.ClientMirrorCountItemId( ThornsRadioShopCatalog.CurrencyItemId )
			: 0;
		var pulse = Time.Now >= _radioShopNextForcedUiPulseRealtime;
		if ( !pulse && invRev == _radioShopLastInvMirrorRev && metal == _radioShopLastInvMetal )
			return;

		_radioShopLastInvMirrorRev = invRev;
		_radioShopLastInvMetal = metal;
		if ( pulse )
			_radioShopNextForcedUiPulseRealtime = Time.Now + 0.35;
		RefreshRadioShopPanels();
	}

	void EnsureRadioShopUiBuilt()
	{
		if ( _radioShopUiBuilt || Panel is null || !Panel.IsValid )
			return;

		_radioShopUiBuilt = true;

		_radioShopLayer = ThornsUiPanelAdd.AddChildPanel( Panel, "thorns-radio-shop-layer thorns-storage-chest-layer" );
		_radioShopLayer.Style.Display = DisplayMode.Flex;
		_radioShopLayer.Style.Position = PositionMode.Absolute;
		_radioShopLayer.Style.Left = 0;
		_radioShopLayer.Style.Top = 0;
		_radioShopLayer.Style.Width = Length.Fraction( 1f );
		_radioShopLayer.Style.Height = Length.Fraction( 1f );
		_radioShopLayer.Style.BackgroundColor = new Color( 0.02f, 0.02f, 0.03f, 0.72f );
		_radioShopLayer.Style.JustifyContent = Justify.Center;
		_radioShopLayer.Style.AlignItems = Align.Center;
		_radioShopLayer.Style.Padding = Length.Pixels( 20 );
		_radioShopLayer.Style.Overflow = OverflowMode.Hidden;
		_radioShopLayer.Style.ZIndex = 205;
		_radioShopLayer.Style.PointerEvents = PointerEvents.All;
		_radioShopLayer.AddEventListener( "onmouseup", RadioShopOnLayerMouseUp );

		_radioShopCard = ThornsUiPanelAdd.AddChildPanel( _radioShopLayer, "thorns-radio-shop-card thorns-storage-chest-card" );
		_radioShopCard.Style.Display = DisplayMode.Flex;
		_radioShopCard.Style.FlexDirection = FlexDirection.Column;
		_radioShopCard.Style.Padding = 18;
		_radioShopCard.Style.BackgroundColor = new Color( 0.07f, 0.07f, 0.09f, 0.96f );
		_radioShopCard.Style.BorderWidth = 2;
		_radioShopCard.Style.BorderColor = new Color( 0.55f, 0.48f, 0.35f, 0.95f );
		_radioShopCard.Style.Width = Length.Fraction( 0.92f );
		_radioShopCard.Style.MinWidth = Length.Pixels( 640 );
		_radioShopCard.Style.MaxWidth = Length.Pixels( 1100 );
		_radioShopCard.Style.MaxHeight = Length.Fraction( 0.88f );
		_radioShopCard.Style.FlexShrink = 1;
		_radioShopCard.Style.Overflow = OverflowMode.Hidden;

		var headerRow = ThornsUiPanelAdd.AddChildPanel( _radioShopCard, "thorns-storage-chest-header" );
		headerRow.Style.Display = DisplayMode.Flex;
		headerRow.Style.FlexDirection = FlexDirection.Row;
		headerRow.Style.AlignItems = Align.Center;
		headerRow.Style.JustifyContent = Justify.SpaceBetween;
		headerRow.Style.MarginBottom = 8;

		var title = headerRow.AddChild( new Label( "FIELD SUPPLY — RADIO", "thorns-storage-chest-title" ) );
		title.Style.FontSize = 18;
		title.Style.FontWeight = 900;
		title.Style.FontColor = new Color( 0.92f, 0.88f, 0.76f, 1f );

		var closeBtn = ThornsUiPanelAdd.AddChildPanel( headerRow, "thorns-storage-chest-close" );
		closeBtn.Style.PointerEvents = PointerEvents.All;
		closeBtn.AddEventListener( "onmousedown", RadioShopCloseButtonMouseDown );
		closeBtn.AddChild( new Label( "×", "thorns-storage-chest-close-glyph" ) );

		var invForLabel = GameObject.Components.Get<ThornsInventory>();
		var metalCount = invForLabel.IsValid()
			? invForLabel.ClientMirrorCountItemId( ThornsRadioShopCatalog.CurrencyItemId )
			: 0;
		_radioShopMetalLabel = _radioShopCard.AddChild(
			new Label( invForLabel.IsValid() ? $"Metal in inventory: {metalCount}" : "Metal in inventory: —", "thorns-storage-chest-hint" ) );
		_radioShopMetalLabel.Style.FontSize = 14;
		_radioShopMetalLabel.Style.FontWeight = 800;
		_radioShopMetalLabel.Style.FontColor = new Color( 0.78f, 0.82f, 0.88f, 1f );
		_radioShopMetalLabel.Style.MarginBottom = 4;

		_radioShopRotationLabel = _radioShopCard.AddChild( new Label( "", "thorns-storage-chest-hint" ) );
		_radioShopRotationLabel.Style.FontSize = 11;
		_radioShopRotationLabel.Style.FontColor = new Color( 0.62f, 0.6f, 0.55f, 1f );
		_radioShopRotationLabel.Style.MarginBottom = 10;

		_radioShopCard.AddChild( new Label(
			"Pay with metal stacks in your inventory · Sell ×1 scraps one unit into metal · stock rerolls every 5 minutes.",
			"thorns-storage-chest-hint" ) );

		var split = ThornsUiPanelAdd.AddChildPanel( _radioShopCard, "thorns-radio-shop-split" );
		split.Style.Display = DisplayMode.Flex;
		split.Style.FlexDirection = FlexDirection.Row;
		split.Style.AlignItems = Align.Stretch;
		split.Style.Width = Length.Fraction( 1f );
		split.Style.FlexGrow = 1;
		split.Style.FlexShrink = 1;
		split.Style.MinHeight = Length.Pixels( 0 );
		split.Style.MarginTop = 8;

		var buyCol = ThornsUiPanelAdd.AddChildPanel( split, "thorns-radio-shop-col thorns-radio-shop-col--buy" );
		buyCol.Style.Display = DisplayMode.Flex;
		buyCol.Style.FlexDirection = FlexDirection.Column;
		buyCol.Style.FlexGrow = 1;
		buyCol.Style.FlexShrink = 1;
		buyCol.Style.FlexBasis = Length.Fraction( 0f );
		buyCol.Style.MinWidth = Length.Pixels( 0 );

		buyCol.AddChild( new Label( "Buy", "thorns-storage-chest-section" ) );

		_radioShopOffersScroll = ThornsUiPanelAdd.AddChildPanel( buyCol, "thorns-radio-shop-offers" );
		_radioShopOffersScroll.Style.Display = DisplayMode.Flex;
		_radioShopOffersScroll.Style.FlexDirection = FlexDirection.Column;
		_radioShopOffersScroll.Style.FlexGrow = 1;
		_radioShopOffersScroll.Style.FlexShrink = 1;
		_radioShopOffersScroll.Style.MinHeight = Length.Pixels( 0 );
		_radioShopOffersScroll.Style.Overflow = OverflowMode.Scroll;

		var sellCol = ThornsUiPanelAdd.AddChildPanel( split, "thorns-radio-shop-col thorns-radio-shop-col--sell" );
		sellCol.Style.Display = DisplayMode.Flex;
		sellCol.Style.FlexDirection = FlexDirection.Column;
		sellCol.Style.FlexGrow = 1;
		sellCol.Style.FlexShrink = 1;
		sellCol.Style.FlexBasis = Length.Fraction( 0f );
		sellCol.Style.MinWidth = Length.Pixels( 0 );

		sellCol.AddChild( new Label( "Your inventory — sell", "thorns-storage-chest-section" ) );

		_radioShopSellScroll = ThornsUiPanelAdd.AddChildPanel( sellCol, "thorns-radio-shop-sell" );
		_radioShopSellScroll.Style.Display = DisplayMode.Flex;
		_radioShopSellScroll.Style.FlexDirection = FlexDirection.Column;
		_radioShopSellScroll.Style.FlexGrow = 1;
		_radioShopSellScroll.Style.FlexShrink = 1;
		_radioShopSellScroll.Style.MinHeight = Length.Pixels( 0 );
		_radioShopSellScroll.Style.Overflow = OverflowMode.Scroll;

		SetRadioShopLayerVisible( false );
	}

	void RadioShopCloseButtonMouseDown( PanelEvent e )
	{
		e.StopPropagation();
		CloseRadioShopUi();
	}

	void RadioShopOnLayerMouseUp( PanelEvent e )
	{
		if ( e.Target == _radioShopLayer )
			CloseRadioShopUi();
	}

	void RefreshRadioShopPanels()
	{
		if ( !_radioShopOffersScroll.IsValid || !_radioShopSellScroll.IsValid )
			return;

		var inv = GameObject.Components.Get<ThornsInventory>();
		if ( _radioShopMetalLabel.IsValid && inv.IsValid() )
		{
			var metalCount = inv.ClientMirrorCountItemId( ThornsRadioShopCatalog.CurrencyItemId );
			_radioShopMetalLabel.Text = $"Metal in inventory: {metalCount}";
		}

		if ( _radioShopRotationLabel.IsValid )
		{
			var period = ThornsRadioShopRotation.RotationPeriodSeconds;
			var next = (ThornsRadioShopRotation.CurrentEpochIndexHost() + 1) * period;
			var remain = Math.Max( 0.0, next - Time.Now );
			var mm = (int)( remain / 60 );
			var ss = (int)( remain % 60 );
			_radioShopRotationLabel.Text = $"Stock refresh in {mm:00}:{ss:00} (epoch {_radioCatalogEpoch})";
		}

		if ( !RadioShopUiOpen )
		{
			foreach ( var ch in _radioShopOffersScroll.Children.ToArray() )
				ch.Delete();
			foreach ( var ch in _radioShopSellScroll.Children.ToArray() )
				ch.Delete();
			return;
		}

		// Rebuilding offer/sell rows while LMB is held deletes click targets and breaks mouse-up routing.
		if ( Input.Down( "Attack1" ) || Input.Down( "attack1" ) )
			return;

		foreach ( var ch in _radioShopOffersScroll.Children.ToArray() )
			ch.Delete();
		foreach ( var ch in _radioShopSellScroll.Children.ToArray() )
			ch.Delete();

		var shop = GameObject.Components.Get<ThornsRadioShopInteractor>();
		var stationId = _radioShopStationId;

		var ids = _radioCatalogItemIds;
		var prices = _radioCatalogBuyPrices;
		var maxB = _radioCatalogMaxBuy;
		var n = ids?.Length ?? 0;
		if ( n == 0 )
		{
			_radioShopOffersScroll.AddChild( new Label( "No offers — try again.", "thorns-storage-chest-hint" ) );
		}
		else
		{
			for ( var i = 0; i < n; i++ )
			{
				var slot = i;
				var itemId = ids[i];
				var unit = i < prices.Length ? prices[i] : 0;
				var cap = i < maxB.Length ? maxB[i] : 1;
				var def = ThornsItemRegistry.GetOrNull( itemId );
				var titleTxt = def?.DisplayName ?? itemId;

				var row = ThornsUiPanelAdd.AddChildPanel( _radioShopOffersScroll, $"thorns-radio-shop-offer-row radio-offer-{i}" );
				row.Style.Display = DisplayMode.Flex;
				row.Style.FlexDirection = FlexDirection.Row;
				row.Style.JustifyContent = Justify.SpaceBetween;
				row.Style.AlignItems = Align.Center;
				row.Style.FlexShrink = 0;
				row.Style.MinHeight = Length.Pixels( 52 );
				row.Style.Width = Length.Fraction( 1f );

				var txt = row.AddChild( new Label( $"{titleTxt}  —  {unit} metal each  (max stack {cap})", "thorns-radio-shop-offer-label" ) );
				txt.Style.FlexGrow = 1;
				txt.Style.FlexShrink = 1;
				txt.Style.MinWidth = Length.Pixels( 0 );
				txt.Style.WhiteSpace = WhiteSpace.Normal;

				var btnRow = ThornsUiPanelAdd.AddChildPanel( row, "thorns-radio-shop-buy-row" );
				btnRow.Style.Display = DisplayMode.Flex;
				btnRow.Style.FlexDirection = FlexDirection.Row;
				btnRow.Style.FlexShrink = 0;

				void TryBuy( int q )
				{
					if ( !shop.IsValid() || stationId == Guid.Empty )
						return;
					var qq = Math.Clamp( q, 1, Math.Max( 1, cap ) );
					shop.RequestRadioBuy( stationId, slot, qq );
				}

				AddRadioShopButton( btnRow, "×1", () => TryBuy( 1 ), buy: true );
				AddRadioShopButton( btnRow, "×5", () => TryBuy( 5 ), buy: true );
			}
		}

		if ( !inv.IsValid() )
			return;

		for ( var si = 0; si < ThornsInventory.TotalSlots; si++ )
		{
			var idx = si;
			var hasItem = inv.TryGetClientMirrorSlot( idx, out var net )
			              && !string.IsNullOrEmpty( net.ItemId )
			              && net.Quantity > 0;
			string label;
			if ( hasItem )
			{
				var d = ThornsItemRegistry.GetOrNull( net.ItemId );
				var nm = d?.DisplayName ?? net.ItemId;
				if ( ThornsRadioShopCatalog.IsMetalTradeBlockedFromRadioShop( net.ItemId ) )
					label = $"{nm} ×{net.Quantity}  —  not sold here";
				else
				{
					var quote = ThornsRadioShopCatalog.ClientEstimateSellMetalDisplay( net );
					label = $"{nm} ×{net.Quantity}  →  ~{quote} metal / unit";
				}
			}
			else
			{
				label = $"Slot {idx}: empty";
			}

			var rowCls = hasItem
				? $"thorns-radio-shop-sell-row thorns-radio-shop-sell-row--filled radio-sell-{idx}"
				: $"thorns-radio-shop-sell-row thorns-radio-shop-sell-row--empty radio-sell-{idx}";

			var sRow = ThornsUiPanelAdd.AddChildPanel( _radioShopSellScroll, rowCls );
			sRow.Style.Display = DisplayMode.Flex;
			sRow.Style.FlexDirection = FlexDirection.Row;
			sRow.Style.JustifyContent = Justify.SpaceBetween;
			sRow.Style.AlignItems = Align.Center;
			sRow.Style.FlexShrink = 0;
			sRow.Style.MinHeight = Length.Pixels( 52 );
			sRow.Style.Width = Length.Fraction( 1f );

			var line = sRow.AddChild( new Label( label, "thorns-radio-shop-sell-label" ) );
			line.Style.FlexGrow = 1;
			line.Style.FlexShrink = 1;
			line.Style.MinWidth = Length.Pixels( 0 );
			line.Style.WhiteSpace = WhiteSpace.Normal;

			var actions = ThornsUiPanelAdd.AddChildPanel( sRow, "thorns-radio-shop-sell-actions" );
			actions.Style.Display = DisplayMode.Flex;
			actions.Style.FlexDirection = FlexDirection.Row;
			actions.Style.FlexShrink = 0;
			actions.Style.JustifyContent = Justify.FlexEnd;
			actions.Style.AlignItems = Align.Center;

			if ( hasItem && !ThornsRadioShopCatalog.IsMetalTradeBlockedFromRadioShop( net.ItemId ) )
			{
				AddRadioShopButton( actions, "Sell ×1", () =>
				{
					if ( !shop.IsValid() || stationId == Guid.Empty )
						return;
					shop.RequestRadioSell( stationId, idx, 1 );
				}, buy: false );
			}
			else if ( hasItem )
			{
				_ = actions.AddChild( new Label( "—", "thorns-radio-shop-sell-dash" ) );
			}
			else
			{
				_ = actions.AddChild( new Label( "—", "thorns-radio-shop-sell-dash" ) );
			}
		}
	}

	static Panel AddRadioShopButton( Panel parent, string text, Action onClick, bool buy )
	{
		var panelCls = buy
			? "thorns-radio-shop-btn thorns-radio-shop-btn--buy"
			: "thorns-radio-shop-btn thorns-radio-shop-btn--sell";
		return ThornsUiPanelAdd.AddClickableLabel(
			parent,
			text,
			onClick,
			"thorns-radio-shop-btn-label",
			panelCls );
	}
}
