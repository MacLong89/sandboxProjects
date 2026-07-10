namespace Terraingen.UI.Hud;

using Sandbox.UI;
using Terraingen.GameData;
using Terraingen.UI;
using Terraingen.UI.Core;

/// <summary>World event alerts — top-right airdrop-style cards.</summary>
public sealed class ThornsWorldEventHud
{
	readonly Panel _root;
	Action<UiRevisionChannel, int> _onRevision;

	public ThornsWorldEventHud( Panel parent )
	{
		_root = ThornsUiFactory.AddPanel( parent, "world-event-hud" );
		_root.Style.Position = PositionMode.Absolute;
		_root.Style.Top = Length.Pixels( ThornsHudSafeZones.Scaled( ThornsHudTheme.WorldEventTopPx ) );
		_root.Style.Right = Length.Pixels( ThornsHudSafeZones.Scaled( ThornsHudTheme.WorldEventRightPx ) );
		_root.Style.Left = Length.Auto;
		_root.Style.Width = Length.Pixels( ThornsHudTheme.WorldEventWidthPx );
		_root.Style.MaxWidth = Length.Pixels( ThornsHudTheme.WorldEventWidthPx );
		_root.Style.FlexDirection = FlexDirection.Column;
		_root.Style.AlignItems = Align.Stretch;
		_root.Style.PointerEvents = PointerEvents.None;
		ThornsUiLayer.Apply( _root, ThornsUiPriority.Toast );

		_onRevision = (channel, _) =>
		{
			if ( channel is UiRevisionChannel.WorldEvents or UiRevisionChannel.Milestones )
				Refresh();
		};
		UiRevisionBus.MenuRevisionChanged += _onRevision;
		Refresh();
	}

	public void Dispose() => UiRevisionBus.MenuRevisionChanged -= _onRevision;

	public void Refresh()
	{
		if ( !_root.IsValid )
			return;

		_root.DeleteChildren( true );

		var active = ThornsWorldEventHudBus.Active;
		_root.Style.Display = active.Count > 0 ? DisplayMode.Flex : DisplayMode.None;

		foreach ( var entry in active )
			BuildAlertCard( entry );
	}

	void BuildAlertCard( ThornsWorldEventHudEntry entry )
	{
		var card = ThornsUiFactory.AddPanel( _root, "world-event-alert" );
		card.SetClass( "world-event-kind-event", entry.Kind == ThornsWorldEventFeedKind.WorldEvent );
		card.SetClass( "world-event-kind-milestone", entry.Kind == ThornsWorldEventFeedKind.Milestone );
		card.SetClass( $"world-event-alert-{entry.AlertKind.ToString().ToLowerInvariant()}", true );
		ThornsHudTheme.ApplyObjectivesHudPanel( card );
		card.Style.FlexDirection = FlexDirection.Row;
		card.Style.AlignItems = Align.Stretch;
		card.Style.FlexShrink = 0;
		card.Style.PaddingTop = Length.Pixels( 10 );
		card.Style.PaddingBottom = Length.Pixels( 10 );
		card.Style.PaddingLeft = Length.Pixels( 12 );
		card.Style.PaddingRight = Length.Pixels( 12 );
		card.Style.MarginBottom = Length.Pixels( 8 );

		var iconWrap = ThornsUiFactory.AddPanel( card, "world-event-alert-icon-wrap" );
		iconWrap.Style.Width = Length.Pixels( 48 );
		iconWrap.Style.MinWidth = Length.Pixels( 48 );
		iconWrap.Style.FlexShrink = 0;
		iconWrap.Style.JustifyContent = Justify.Center;
		iconWrap.Style.AlignItems = Align.Center;

		var icon = ThornsUiFactory.AddPanel( iconWrap, "world-event-alert-icon slot-icon" );
		icon.Style.Width = Length.Pixels( 40 );
		icon.Style.Height = Length.Pixels( 40 );
		icon.Style.FlexShrink = 0;
		ThornsIconCache.ApplyToPanel( icon, entry.IconPath );

		var body = ThornsUiFactory.AddPanel( card, "world-event-alert-body-col" );
		body.Style.FlexDirection = FlexDirection.Column;
		body.Style.FlexGrow = 1;
		body.Style.MinWidth = Length.Pixels( 0 );
		body.Style.MarginLeft = Length.Pixels( 10 );

		var title = ThornsUiFactory.AddPassiveLabel( body, entry.Title, "world-event-alert-title thorns-header" );
		title.Style.WhiteSpace = WhiteSpace.Normal;

		if ( !string.IsNullOrWhiteSpace( entry.Message ) )
		{
			var message = ThornsUiFactory.AddPassiveLabel( body, entry.Message, "world-event-alert-message" );
			message.Style.WhiteSpace = WhiteSpace.Normal;
			message.Style.MarginTop = Length.Pixels( 4 );
		}

		if ( entry.ShowTimer )
		{
			var timer = ThornsUiFactory.AddPassiveLabel(
				body,
				ThornsWorldEventHudBus.FormatTimer( entry.SecondsRemaining ),
				"world-event-alert-timer" );
			timer.Style.MarginTop = Length.Pixels( 6 );
		}

		if ( entry.Kind == ThornsWorldEventFeedKind.Milestone && entry.XpReward > 0 )
		{
			var xp = ThornsUiFactory.AddPassiveLabel( body, $"+{entry.XpReward} XP", "world-event-xp thorns-accent" );
			xp.Style.MarginTop = Length.Pixels( 6 );
		}
	}
}
