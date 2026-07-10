namespace Terraingen.UI.Core;

using Sandbox.UI;
using Terraingen.UI;

/// <summary>Modal dialog integrated with <see cref="ThornsUiManager"/> focus stack.</summary>
public static class ThornsModal
{
	const string ModalId = "gameplay-modal";

	public static void Show( Panel parent, string title, string message, Action onConfirm = null, Action onCancel = null )
	{
		if ( parent is null || !parent.IsValid )
			return;

		// Prevent duplicate modals.
		if ( ThornsUiManager.IsOpen( ModalId ) )
			return;

		var overlay = ThornsUiFactory.AddPanel( parent, "thorns-modal-overlay" );
		overlay.Style.Position = PositionMode.Absolute;
		overlay.Style.Width = Length.Percent( 100 );
		overlay.Style.Height = Length.Percent( 100 );
		overlay.Style.BackgroundColor = new Color( 0f, 0f, 0f, 0.72f );
		overlay.Style.JustifyContent = Justify.Center;
		overlay.Style.AlignItems = Align.Center;
		overlay.AddEventListener( "onmousedown", e =>
		{
			if ( e.Target == overlay )
				Dismiss( overlay, onCancel );
		} );

		ThornsUiLayer.ApplyModalSurface( overlay, ThornsUiPriority.CriticalPopup );

		var box = ThornsUiFactory.AddPanel( overlay, "thorns-glass thorns-modal-card thorns-overlay-shell" );
		ThornsMenuChrome.ApplyWoodPanel( box );
		box.Style.Padding = Length.Pixels( 24 );
		box.Style.FlexDirection = FlexDirection.Column;
		box.Style.MinWidth = Length.Pixels( 320 );
		box.Style.MaxWidth = Length.Pixels( 480 );
		box.Style.Opacity = 1f;
		box.Style.PointerEvents = PointerEvents.All;

		ThornsTheme.CreateHeader( box, title );
		ThornsTheme.CreateMuted( box, message );

		var actions = ThornsUiFactory.AddPanel( box, "thorns-modal-actions" );
		actions.Style.FlexDirection = FlexDirection.Row;
		actions.Style.JustifyContent = Justify.FlexEnd;
		actions.Style.MarginTop = Length.Pixels( 16 );

		ThornsUiFactory.AddClickable( actions, "thorns-btn-secondary", "Cancel", () => Dismiss( overlay, onCancel ) );
		ThornsUiFactory.AddClickable( actions, "thorns-btn-primary", "OK", () =>
		{
			onConfirm?.Invoke();
			Dismiss( overlay, null );
		} );

		ThornsUiManager.Register(
			ModalId,
			ThornsUiPriority.CriticalPopup,
			overlay,
			capturesInput: true,
			blocksGameplay: true,
			isModal: true,
			onEscape: () => Dismiss( overlay, onCancel ),
			onConflictClose: () => Dismiss( overlay, onCancel ),
			kind: ThornsUiWindowKind.GameplayModal );
	}

	static void Dismiss( Panel overlay, Action onCancel )
	{
		ThornsUiManager.Unregister( ModalId );
		onCancel?.Invoke();

		if ( overlay is not null && overlay.IsValid )
			overlay.Delete();
	}
}
