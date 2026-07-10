namespace Terraingen.UI.Menu.Panels;

using Sandbox.UI;
using Terraingen.UI;
using Terraingen.UI.Core;

/// <summary>Centered yes/no modal (same layer as <see cref="MainMenuWorldNamePrompt"/>).</summary>
public sealed class MainMenuConfirmPrompt
{
	readonly Panel _overlay;
	readonly Label _title;
	readonly Label _message;
	readonly ThornsClickPanel _confirmBtn;
	readonly Label _confirmLabel;
	readonly ThornsClickPanel _cancelBtn;
	readonly Label _cancelLabel;

	Action _onConfirm;

	public bool IsVisible => _overlay.IsValid && _overlay.HasClass( "visible" );

	public MainMenuConfirmPrompt( Panel parent )
	{
		_overlay = ThornsUiFactory.AddPanel( parent, "mainmenu-prompt-overlay" );
		_overlay.Style.Position = PositionMode.Absolute;
		_overlay.Style.Left = Length.Pixels( 0 );
		_overlay.Style.Top = Length.Pixels( 0 );
		_overlay.Style.Width = Length.Percent( 100 );
		_overlay.Style.Height = Length.Percent( 100 );
		_overlay.Style.JustifyContent = Justify.Center;
		_overlay.Style.AlignItems = Align.Center;
		_overlay.Style.PointerEvents = PointerEvents.All;
		ThornsUiLayer.ApplyModalSurface( _overlay, ThornsUiPriority.CriticalPopup );
		_overlay.Style.Display = DisplayMode.None;
		_overlay.AddEventListener( "onmousedown", OnBackdropMouseDown );

		var card = ThornsUiFactory.AddPanel( _overlay, "mainmenu-prompt-card thorns-glass" );
		card.Style.FlexDirection = FlexDirection.Column;
		card.Style.AlignItems = Align.Stretch;
		card.Style.Padding = Length.Pixels( 36 );
		card.Style.MinWidth = Length.Pixels( 400 );
		card.Style.MaxWidth = Length.Pixels( 520 );
		card.Style.PointerEvents = PointerEvents.All;

		_title = ThornsUiFactory.AddLabel( card, "ARE YOU SURE?", "mainmenu-prompt-title" );
		_title.Style.TextAlign = TextAlign.Center;

		_message = ThornsUiFactory.AddLabel( card, "", "mainmenu-prompt-hint" );
		_message.Style.TextAlign = TextAlign.Center;
		_message.Style.MarginTop = Length.Pixels( 8 );
		_message.Style.MarginBottom = Length.Pixels( 20 );

		var actions = ThornsUiFactory.AddPanel( card, "mainmenu-prompt-actions" );
		actions.Style.FlexDirection = FlexDirection.Row;
		actions.Style.JustifyContent = Justify.Center;

		_cancelBtn = ThornsUiFactory.AddClickable( actions, "mainmenu-btn-secondary", OnCancelClicked );
		_cancelLabel = ThornsUiFactory.AddPassiveLabel( _cancelBtn, "Cancel" );
		_cancelLabel.Style.PointerEvents = PointerEvents.None;
		_confirmBtn = ThornsUiFactory.AddClickable( actions, "mainmenu-btn-primary", OnConfirmClicked );
		_confirmLabel = ThornsUiFactory.AddPassiveLabel( _confirmBtn, "Confirm" );
		_confirmLabel.Style.PointerEvents = PointerEvents.None;

		Hide();
	}

	public void Show(
		string title,
		string message,
		string confirmLabel,
		Action onConfirm,
		string cancelLabel = "Cancel" )
	{
		_onConfirm = onConfirm;
		_title.Text = string.IsNullOrWhiteSpace( title ) ? "ARE YOU SURE?" : title;
		_message.Text = message ?? "";
		_message.SetClass( "error", false );
		_confirmLabel.Text = string.IsNullOrWhiteSpace( confirmLabel ) ? "Confirm" : confirmLabel;
		_cancelLabel.Text = string.IsNullOrWhiteSpace( cancelLabel ) ? "Cancel" : cancelLabel;

		_overlay.Style.Display = DisplayMode.Flex;
		_overlay.SetClass( "visible", true );

		ThornsUiManager.Register(
			"mainmenu-confirm",
			ThornsUiPriority.CriticalPopup,
			_overlay,
			capturesInput: true,
			blocksGameplay: true,
			isModal: true,
			onEscape: Hide,
			onConflictClose: Hide,
			context: ThornsUiManager.UiContext.MainMenu,
			kind: ThornsUiWindowKind.MainMenuConfirm );
	}

	public void Hide()
	{
		_onConfirm = null;
		ThornsUiManager.Unregister( "mainmenu-confirm" );
		if ( !_overlay.IsValid )
			return;

		_overlay.Style.Display = DisplayMode.None;
		_overlay.SetClass( "visible", false );
	}

	void OnBackdropMouseDown( PanelEvent e )
	{
		if ( e.Target == _overlay )
			OnCancelClicked();
	}

	void OnCancelClicked() => Hide();

	void OnConfirmClicked()
	{
		var cb = _onConfirm;
		Hide();
		cb?.Invoke();
	}
}
