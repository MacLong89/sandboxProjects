namespace Terraingen.UI.Hud;

using Sandbox.UI;
using Terraingen.UI;
using Terraingen.UI.Core;

/// <summary>Brief summary when leaving a session (progress saved, next goal hint).</summary>
public sealed class ThornsSessionRecapHud
{
	const string WindowId = "session-recap";

	readonly Panel _backdrop;
	readonly Label _body;
	double _hideAt;

	public ThornsSessionRecapHud( Panel parent )
	{
		_backdrop = ThornsUiFactory.AddPanel( parent, "session-recap-backdrop" );
		_backdrop.Style.Position = PositionMode.Absolute;
		_backdrop.Style.Left = Length.Pixels( 0 );
		_backdrop.Style.Top = Length.Pixels( 0 );
		_backdrop.Style.Width = Length.Percent( 100 );
		_backdrop.Style.Height = Length.Percent( 100 );
		_backdrop.Style.BackgroundColor = new Color( 0f, 0f, 0f, 0.82f );
		_backdrop.Style.JustifyContent = Justify.Center;
		_backdrop.Style.AlignItems = Align.Center;
		ThornsUiLayer.ApplyModalSurface( _backdrop, ThornsUiPriority.CriticalPopup );
		_backdrop.Style.Display = DisplayMode.None;

		var card = ThornsUiFactory.AddPanel( _backdrop, "session-recap-card thorns-hud-glass" );
		card.Style.Width = Length.Pixels( 460 );
		card.Style.MaxWidth = Length.Percent( 90 );
		card.Style.Padding = Length.Pixels( 20 );
		card.Style.FlexDirection = FlexDirection.Column;
		card.Style.Opacity = 1f;

		ThornsUiFactory.AddLabel( card, "SESSION END", "thorns-header" );
		_body = ThornsUiFactory.AddPassiveLabel( card, "", "thorns-muted" );
		_body.Style.WhiteSpace = WhiteSpace.Normal;
		ThornsUiFactory.AddClickable( card, "session-recap-dismiss thorns-accent", "Continue", Hide );
	}

	public void Show( string nextGoalTitle )
	{
		if ( !_backdrop.IsValid || !_body.IsValid )
			return;

		_body.Text = string.IsNullOrWhiteSpace( nextGoalTitle )
			? "Your progress is saved. See you next time, survivor."
			: $"Progress saved.\n\nNext goal when you return:\n{nextGoalTitle}";
		_hideAt = Time.Now + 6f;
		_backdrop.Style.Display = DisplayMode.Flex;

		ThornsUiManager.Register(
			WindowId,
			ThornsUiPriority.CriticalPopup,
			_backdrop,
			capturesInput: true,
			blocksGameplay: true,
			isModal: true,
			onEscape: Hide,
			onConflictClose: Hide,
			kind: ThornsUiWindowKind.SessionRecap );
	}

	public void Tick()
	{
		if ( _backdrop is null || !_backdrop.IsValid || _backdrop.Style.Display == DisplayMode.None )
			return;

		if ( Time.Now >= _hideAt
		     || Input.Pressed( "Use" )
		     || Input.Pressed( "Tab" )
		     || Input.Pressed( "Menu" )
		     || Input.Pressed( "Cancel" )
		     || Input.EscapePressed )
			Hide();
	}

	void Hide()
	{
		ThornsUiManager.Unregister( WindowId );
		if ( _backdrop.IsValid )
			_backdrop.Style.Display = DisplayMode.None;
	}
}
