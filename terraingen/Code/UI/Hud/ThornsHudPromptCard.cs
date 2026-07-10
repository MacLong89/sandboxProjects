namespace Terraingen.UI.Hud;

using Sandbox.UI;
using Terraingen.UI;
using Terraingen.UI.Core;

/// <summary>Reusable journal-style HUD prompt (icon, message, optional hold bar).</summary>
public sealed class ThornsHudPromptCard
{
	readonly Panel _root;
	readonly Label _message;
	readonly Panel _holdBar;
	readonly Panel _holdFill;

	public ThornsHudPromptCard( Panel parent, string rootClass = "interaction-hud hud-prompt-card" )
	{
		_root = ThornsUiFactory.AddPanel( parent, rootClass );
		ThornsHudTheme.ApplyHudPromptCard( _root );
		_root.Style.Display = DisplayMode.None;

		var head = ThornsUiFactory.AddPanel( _root, "hud-prompt-head" );
		head.Style.FlexDirection = FlexDirection.Row;
		head.Style.AlignItems = Align.Center;
		head.Style.Width = Length.Percent( 100 );

		if ( !ThornsHudClassicChrome.IsActive )
			ThornsHudPinAlertIcon.CreateNotificationGraphic( head, out _, ThornsUiMetrics.HudPromptNotificationIcon );

		var messageWrap = ThornsUiFactory.AddPanel( head, "hud-prompt-message-wrap" );
		messageWrap.Style.FlexGrow = 1;
		messageWrap.Style.FlexShrink = 1;
		messageWrap.Style.MinWidth = Length.Pixels( 0 );
		messageWrap.Style.JustifyContent = Justify.Center;
		messageWrap.Style.AlignItems = Align.Center;

		_message = ThornsUiFactory.AddLabel( messageWrap, "", "hud-prompt-message" );
		_message.Style.TextAlign = TextAlign.Center;

		_holdBar = ThornsUiFactory.AddPanel( _root, "hud-prompt-hold-bar" );
		_holdBar.Style.Display = DisplayMode.None;
		_holdFill = ThornsUiFactory.AddPanel( _holdBar, "hud-prompt-hold-fill" );
	}

	public void Apply( ThornsHudPromptState state )
	{
		if ( !_root.IsValid )
			return;

		if ( !state.IsVisible )
		{
			_root.Style.Display = DisplayMode.None;
			return;
		}

		_root.Style.Display = DisplayMode.Flex;

		if ( _message.IsValid )
			_message.Text = state.Message;

		var showHold = state.HoldFraction > 0.001f;
		if ( _holdBar.IsValid )
			_holdBar.Style.Display = showHold ? DisplayMode.Flex : DisplayMode.None;

		if ( showHold && _holdFill.IsValid )
			_holdFill.Style.Width = Length.Percent( state.HoldFraction * 100f );
	}
}
