namespace Terraingen.UI.Menu.Panels;

using Sandbox.UI;
using Terraingen.UI;
using Terraingen.UI.Core;

/// <summary>Centered modal for naming a world before create/host (same layer as <see cref="MainMenuProgressOverlay"/>).</summary>
public sealed class MainMenuWorldNamePrompt
{
	readonly Panel _overlay;
	readonly Label _title;
	readonly Label _hint;
	readonly TextEntry _entry;
	readonly ThornsClickPanel _confirmBtn;
	readonly Label _confirmLabel;
	readonly ThornsClickPanel _cancelBtn;

	Action<string> _onConfirm;

	public bool IsVisible => _overlay.IsValid && _overlay.HasClass( "visible" );

	public MainMenuWorldNamePrompt( Panel parent )
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

		_title = ThornsUiFactory.AddLabel( card, "WORLD NAME", "mainmenu-prompt-title" );
		_title.Style.TextAlign = TextAlign.Center;

		_hint = ThornsUiFactory.AddLabel( card, "", "mainmenu-prompt-hint" );
		_hint.Style.TextAlign = TextAlign.Center;
		_hint.Style.MarginTop = Length.Pixels( 8 );

		_entry = card.AddChild( new TextEntry() );
		if ( _entry is null || !_entry.IsValid )
			throw new InvalidOperationException( "[Thorns Menu] TextEntry could not be created for world name prompt." );
		_entry.AddClass( "mainmenu-prompt-field" );
		_entry.Placeholder = "World name...";
		_entry.Style.MarginTop = Length.Pixels( 18 );
		_entry.Style.MarginBottom = Length.Pixels( 20 );
		_entry.AddEventListener( "onsubmit", OnEntrySubmitted );

		var actions = ThornsUiFactory.AddPanel( card, "mainmenu-prompt-actions" );
		actions.Style.FlexDirection = FlexDirection.Row;
		actions.Style.JustifyContent = Justify.Center;

		_cancelBtn = ThornsUiFactory.AddClickable( actions, "mainmenu-btn-secondary", "Cancel", OnCancelClicked );
		_confirmBtn = ThornsUiFactory.AddClickable( actions, "mainmenu-btn-primary", OnConfirmClicked );
		_confirmLabel = ThornsUiFactory.AddPassiveLabel( _confirmBtn, "Create" );
		_confirmLabel.Style.PointerEvents = PointerEvents.None;

		Hide();
	}

	public void Show(
		string title,
		string hint,
		string confirmLabel,
		string placeholder,
		string initialText,
		Action<string> onConfirm )
	{
		_onConfirm = onConfirm;
		_title.Text = title ?? "WORLD NAME";
		_hint.Text = hint ?? "";
		_hint.SetClass( "error", false );
		_confirmLabel.Text = string.IsNullOrWhiteSpace( confirmLabel ) ? "Confirm" : confirmLabel;
		_entry.Placeholder = placeholder ?? "World name...";
		_entry.Text = initialText ?? "";

		_overlay.Style.Display = DisplayMode.Flex;
		_overlay.SetClass( "visible", true );

		ThornsUiManager.Register(
			"mainmenu-world-name",
			ThornsUiPriority.CriticalPopup,
			_overlay,
			capturesInput: true,
			blocksGameplay: true,
			isModal: true,
			onEscape: Hide,
			onConflictClose: Hide,
			context: ThornsUiManager.UiContext.MainMenu,
			kind: ThornsUiWindowKind.MainMenuWorldName );
	}

	public void Hide()
	{
		_onConfirm = null;
		ThornsUiManager.Unregister( "mainmenu-world-name" );
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

	void OnEntrySubmitted()
	{
		ThornsUiSfx.PlayButtonClick();
		OnConfirmClicked();
	}

	void OnConfirmClicked()
	{
		var name = _entry.Text?.Trim() ?? "";
		if ( string.IsNullOrWhiteSpace( name ) )
		{
			_hint.Text = "Enter a world name to continue.";
			_hint.SetClass( "error", true );
			return;
		}

		var cb = _onConfirm;
		Hide();
		cb?.Invoke( name );
	}
}
