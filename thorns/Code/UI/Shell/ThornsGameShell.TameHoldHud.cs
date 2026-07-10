#nullable disable

using Sandbox.UI;

namespace Sandbox;

public sealed partial class ThornsGameShell
{
	Panel _tameHudColumn;
	Panel _levelUpBannerRoot;
	Label _levelUpBannerTitleLabel;
	Label _levelUpBannerSubtitleLabel;
	Panel _tameStunBannerRoot;
	Label _tameStunBannerTitleLabel;
	Label _tameStunBannerSubtitleLabel;
	Panel _radioShopLookRoot;
	Label _radioShopLookTitleLabel;
	Label _radioShopLookSubtitleLabel;
	bool _radioShopLookPromptActive;
	Panel _tameHudRoot;
	Label _tameHudTitle;
	Label _tameHudSubtitle;
	Panel _tameHudTrack;
	Panel _tameHudFill;
	bool _tameHudUiBuilt;

	void EnsureTameHudBuilt()
	{
		if ( _tameHudUiBuilt || _hudMaskedLayer is null || !_hudMaskedLayer.IsValid )
			return;

		_tameHudUiBuilt = true;

		_tameHudColumn = ThornsUiPanelAdd.AddChildPanel( _hudMaskedLayer, "thorns-tame-hud-column" );
		_tameHudColumn.Style.Position = PositionMode.Absolute;
		_tameHudColumn.Style.Left = 0;
		_tameHudColumn.Style.Top = 0;
		_tameHudColumn.Style.Right = 0;
		_tameHudColumn.Style.Bottom = 0;
		_tameHudColumn.Style.Display = DisplayMode.Flex;
		_tameHudColumn.Style.FlexDirection = FlexDirection.Column;
		_tameHudColumn.Style.JustifyContent = Justify.Center;
		_tameHudColumn.Style.AlignItems = Align.FlexStart;
		_tameHudColumn.Style.PointerEvents = PointerEvents.None;
		_tameHudColumn.Style.ZIndex = 126;

		var alertsRoot = ThornsUiPanelAdd.AddChildPanel( _tameHudColumn, "thorns-shell-alerts" );
		alertsRoot.Style.Display = DisplayMode.Flex;
		alertsRoot.Style.FlexDirection = FlexDirection.Row;
		alertsRoot.Style.JustifyContent = Justify.FlexStart;
		alertsRoot.Style.AlignItems = Align.FlexStart;
		alertsRoot.Style.FlexShrink = 0;
		alertsRoot.Style.PointerEvents = PointerEvents.None;

		_alertLabel = alertsRoot.AddChild( new Label( "", "thorns-shell-alert-label" ) );
		_alertLabel.Style.PointerEvents = PointerEvents.None;
		_alertLabel.Style.TextAlign = TextAlign.Left;
		_alertLabel.Style.WhiteSpace = WhiteSpace.Normal;

		_toastFeed = ThornsUiPanelAdd.AddChildPanel( _tameHudColumn, "thorns-shell-toast-feed" );
		_toastFeed.Style.Display = DisplayMode.Flex;
		_toastFeed.Style.FlexDirection = FlexDirection.Column;
		_toastFeed.Style.JustifyContent = Justify.FlexStart;
		_toastFeed.Style.AlignItems = Align.FlexStart;
		_toastFeed.Style.FlexShrink = 0;
		_toastFeed.Style.PointerEvents = PointerEvents.None;

		_interactionHint = _tameHudColumn.AddChild( new Label( "", "thorns-shell-interaction-hint thorns-shell-interaction-hint--hidden" ) );
		_interactionHint.Style.PointerEvents = PointerEvents.None;
		_interactionHint.Style.TextAlign = TextAlign.Center;
		_interactionHint.Style.WhiteSpace = WhiteSpace.Normal;
		_interactionHint.Style.Position = PositionMode.Absolute;

		_levelUpBannerRoot = ThornsUiPanelAdd.AddChildPanel( _tameHudColumn, "thorns-tame-hud-root thorns-tame-hud-root--levelup thorns-tame-hud-root--hidden" );
		_levelUpBannerRoot.Style.FlexDirection = FlexDirection.Column;
		_levelUpBannerRoot.Style.AlignItems = Align.Stretch;
		_levelUpBannerRoot.Style.Padding = Length.Pixels( 12 );
		_levelUpBannerRoot.Style.PointerEvents = PointerEvents.None;

		_levelUpBannerTitleLabel = _levelUpBannerRoot.AddChild( new Label( "", "thorns-tame-hud-title" ) );
		_levelUpBannerTitleLabel.Style.TextAlign = TextAlign.Left;
		_levelUpBannerTitleLabel.Style.PointerEvents = PointerEvents.None;
		_levelUpBannerTitleLabel.Style.FlexShrink = 0;

		_levelUpBannerSubtitleLabel = _levelUpBannerRoot.AddChild( new Label( "", "thorns-tame-hud-subtitle" ) );
		_levelUpBannerSubtitleLabel.Style.TextAlign = TextAlign.Left;
		_levelUpBannerSubtitleLabel.Style.PointerEvents = PointerEvents.None;
		_levelUpBannerSubtitleLabel.Style.WhiteSpace = WhiteSpace.Normal;
		_levelUpBannerSubtitleLabel.Style.FlexShrink = 0;

		_tameStunBannerRoot = ThornsUiPanelAdd.AddChildPanel( _tameHudColumn, "thorns-tame-hud-root thorns-tame-hud-root--hidden" );
		_tameStunBannerRoot.Style.FlexDirection = FlexDirection.Column;
		_tameStunBannerRoot.Style.AlignItems = Align.Stretch;
		_tameStunBannerRoot.Style.Padding = Length.Pixels( 12 );
		_tameStunBannerRoot.Style.PointerEvents = PointerEvents.None;

		_tameStunBannerTitleLabel = _tameStunBannerRoot.AddChild( new Label( "", "thorns-tame-hud-title" ) );
		_tameStunBannerTitleLabel.Style.TextAlign = TextAlign.Left;
		_tameStunBannerTitleLabel.Style.PointerEvents = PointerEvents.None;
		_tameStunBannerTitleLabel.Style.FlexShrink = 0;

		_tameStunBannerSubtitleLabel = _tameStunBannerRoot.AddChild( new Label( "", "thorns-tame-hud-subtitle" ) );
		_tameStunBannerSubtitleLabel.Style.TextAlign = TextAlign.Left;
		_tameStunBannerSubtitleLabel.Style.PointerEvents = PointerEvents.None;
		_tameStunBannerSubtitleLabel.Style.WhiteSpace = WhiteSpace.Normal;
		_tameStunBannerSubtitleLabel.Style.FlexShrink = 0;

		_radioShopLookRoot = ThornsUiPanelAdd.AddChildPanel(
			_tameHudColumn,
			"thorns-tame-hud-root thorns-tame-hud-root--radio-look thorns-tame-hud-root--hidden" );
		_radioShopLookRoot.Style.FlexDirection = FlexDirection.Column;
		_radioShopLookRoot.Style.AlignItems = Align.Stretch;
		_radioShopLookRoot.Style.Padding = Length.Pixels( 12 );
		_radioShopLookRoot.Style.PointerEvents = PointerEvents.None;

		_radioShopLookTitleLabel = _radioShopLookRoot.AddChild( new Label( "", "thorns-tame-hud-title" ) );
		_radioShopLookTitleLabel.Style.TextAlign = TextAlign.Left;
		_radioShopLookTitleLabel.Style.PointerEvents = PointerEvents.None;
		_radioShopLookTitleLabel.Style.FlexShrink = 0;

		_radioShopLookSubtitleLabel = _radioShopLookRoot.AddChild( new Label( "", "thorns-tame-hud-subtitle" ) );
		_radioShopLookSubtitleLabel.Style.TextAlign = TextAlign.Left;
		_radioShopLookSubtitleLabel.Style.PointerEvents = PointerEvents.None;
		_radioShopLookSubtitleLabel.Style.WhiteSpace = WhiteSpace.Normal;
		_radioShopLookSubtitleLabel.Style.FlexShrink = 0;

		_tameHudRoot = ThornsUiPanelAdd.AddChildPanel( _tameHudColumn, "thorns-tame-hud-root thorns-tame-hud-root--hidden" );
		_tameHudRoot.Style.FlexDirection = FlexDirection.Column;
		_tameHudRoot.Style.AlignItems = Align.Stretch;
		_tameHudRoot.Style.Padding = Length.Pixels( 12 );
		_tameHudRoot.Style.PointerEvents = PointerEvents.None;

		_tameHudTitle = _tameHudRoot.AddChild( new Label( "", "thorns-tame-hud-title" ) );
		_tameHudTitle.Style.TextAlign = TextAlign.Left;
		_tameHudTitle.Style.PointerEvents = PointerEvents.None;
		_tameHudTitle.Style.FlexShrink = 0;

		_tameHudSubtitle = _tameHudRoot.AddChild( new Label( "", "thorns-tame-hud-subtitle" ) );
		_tameHudSubtitle.Style.TextAlign = TextAlign.Left;
		_tameHudSubtitle.Style.PointerEvents = PointerEvents.None;
		_tameHudSubtitle.Style.WhiteSpace = WhiteSpace.Normal;
		_tameHudSubtitle.Style.FlexShrink = 0;

		_tameHudTrack = ThornsUiPanelAdd.AddChildPanel( _tameHudRoot, "thorns-tame-hud-track" );
		_tameHudTrack.Style.Height = Length.Pixels( 10 );
		_tameHudTrack.Style.MarginTop = Length.Pixels( 10 );
		_tameHudTrack.Style.Overflow = OverflowMode.Hidden;
		_tameHudTrack.Style.PointerEvents = PointerEvents.None;

		_tameHudFill = ThornsUiPanelAdd.AddChildPanel( _tameHudTrack, "thorns-tame-hud-fill" );
		_tameHudFill.Style.Height = Length.Fraction( 1f );
		_tameHudFill.Style.Width = Length.Fraction( 0f );
		_tameHudFill.Style.PointerEvents = PointerEvents.None;
	}

	void TickTameHoldHud()
	{
		if ( !IsLocalOwned || _hudMaskedLayer is null || !_hudMaskedLayer.IsValid )
			return;

		EnsureTameHudBuilt();
		if ( !_tameHudColumn.IsValid )
			return;

		var tameActive = ThornsTameHoldHudBridge.Phase != ThornsTameHudPhase.Hidden;
		var toast = _hud.Toast;
		var stunActive = Time.Now < toast.TameStunBannerExpireAt
		                 && ( !string.IsNullOrWhiteSpace( toast.TameStunBannerTitle )
		                      || !string.IsNullOrWhiteSpace( toast.TameStunBannerSubtitle ) );
		var levelUpActive = Time.Now < toast.LevelUpBannerExpireAt
		                    && ( !string.IsNullOrWhiteSpace( toast.LevelUpBannerTitle )
		                         || !string.IsNullOrWhiteSpace( toast.LevelUpBannerSubtitle ) );

		if ( _levelUpBannerRoot.IsValid )
		{
			_levelUpBannerRoot.SetClass( "thorns-tame-hud-root--hidden", !levelUpActive );
			if ( levelUpActive )
			{
				_levelUpBannerTitleLabel.Text = toast.LevelUpBannerTitle.Trim();
				var sub = toast.LevelUpBannerSubtitle.Trim();
				_levelUpBannerSubtitleLabel.Text = sub;
				_levelUpBannerSubtitleLabel.SetClass( "thorns-tame-hud-subtitle--hidden", string.IsNullOrWhiteSpace( sub ) );
			}
		}

		if ( _tameStunBannerRoot.IsValid )
		{
			var showStun = stunActive && !tameActive;
			_tameStunBannerRoot.SetClass( "thorns-tame-hud-root--hidden", !showStun );
			if ( showStun )
			{
				_tameStunBannerTitleLabel.Text = toast.TameStunBannerTitle.Trim();
				_tameStunBannerSubtitleLabel.Text = toast.TameStunBannerSubtitle.Trim();
			}
		}

		if ( _radioShopLookRoot.IsValid )
		{
			var showRadioLook = _radioShopLookPromptActive && !tameActive && !levelUpActive;
			_radioShopLookRoot.SetClass( "thorns-tame-hud-root--hidden", !showRadioLook );
			if ( showRadioLook )
			{
				_radioShopLookTitleLabel.Text = "Field Supply — Radio";
				_radioShopLookSubtitleLabel.Text = ThornsInteractionPromptText.Format(
					"Press Use (E) to open the shop and trade metal." );
			}
		}

		if ( !_tameHudRoot.IsValid )
			return;

		if ( ThornsTameHoldHudBridge.Phase == ThornsTameHudPhase.Hidden )
		{
			_tameHudRoot.SetClass( "thorns-tame-hud-root--hidden", true );
			return;
		}

		_tameHudRoot.SetClass( "thorns-tame-hud-root--hidden", false );

		var name = string.IsNullOrWhiteSpace( ThornsTameHoldHudBridge.CreatureLabel )
			? "Creature"
			: ThornsTameHoldHudBridge.CreatureLabel.Trim();
		var hpPct = Math.Clamp( ThornsTameHoldHudBridge.CreatureHp01 * 100f, 0f, 100f );
		var thPct = Math.Clamp( ThornsTameHoldHudBridge.ThresholdHp01 * 100f, 0f, 100f );

		switch ( ThornsTameHoldHudBridge.Phase )
		{
			case ThornsTameHudPhase.WeakenMore:
				_tameHudTitle.Text = ThornsInteractionPromptText.Format( "Taming — weaken first" );
				_tameHudSubtitle.Text = ThornsInteractionPromptText.Format(
					$"{name} is still too healthy ({hpPct:F0}% HP). Reduce it to about {thPct:F0}% HP or less, then look at it and hold Use (E) to bond." );
				_tameHudTrack.SetClass( "thorns-tame-hud-track--hidden", true );
				_tameHudFill.Style.Width = Length.Fraction( 0f );
				break;
			case ThornsTameHudPhase.ReadyHold:
				_tameHudTitle.Text = ThornsInteractionPromptText.Format( "Ready to tame" );
				_tameHudSubtitle.Text = ThornsInteractionPromptText.Format(
					$"{name} is calm enough ({hpPct:F0}% HP). Hold Use (E) to bond — release to cancel." );
				_tameHudTrack.SetClass( "thorns-tame-hud-track--hidden", false );
				_tameHudTrack.SetClass( "thorns-tame-hud-track--pulse", true );
				_tameHudFill.Style.Width = Length.Fraction( 0f );
				break;
			case ThornsTameHudPhase.Holding:
				_tameHudTitle.Text = "Bonding…";
				_tameHudSubtitle.Text = ThornsInteractionPromptText.Format( $"{name} — keep holding Use (E)" );
				_tameHudTrack.SetClass( "thorns-tame-hud-track--hidden", false );
				_tameHudTrack.SetClass( "thorns-tame-hud-track--pulse", false );
				_tameHudFill.Style.Width = Length.Fraction( Math.Clamp( ThornsTameHoldHudBridge.HoldProgress01, 0f, 1f ) );
				break;
			default:
				_tameHudRoot.SetClass( "thorns-tame-hud-root--hidden", true );
				break;
		}
	}
}
