namespace Terraingen.UI.Hud;

using Sandbox.UI;
using Terraingen.UI;
using Terraingen.UI.Core;

public sealed class ThornsJoinAnnouncementHud
{
	readonly Panel _root;
	Action<UiRevisionChannel, int> _onRevision;

	public ThornsJoinAnnouncementHud( Panel parent )
	{
		_root = ThornsUiFactory.AddPanel( parent, "join-announcement-hud" );
		_root.Style.Position = PositionMode.Absolute;
		_root.Style.Left = Length.Pixels( ThornsHudSafeZones.Scaled( 24 ) );
		_root.Style.Bottom = Length.Pixels( ThornsHudRegions.JoinAnnouncementStackBottomPx );
		_root.Style.FlexDirection = FlexDirection.Column;
		_root.Style.JustifyContent = Justify.FlexEnd;
		_root.Style.PointerEvents = PointerEvents.None;
		ThornsUiLayer.Apply( _root, ThornsUiPriority.Toast );

		_onRevision = (_, _) => Refresh();
		UiRevisionBus.MenuRevisionChanged += _onRevision;
		Refresh();
	}

	public void Dispose() => UiRevisionBus.MenuRevisionChanged -= _onRevision;

	public void Refresh()
	{
		if ( !_root.IsValid )
			return;

		_root.DeleteChildren( true );
		foreach ( var entry in ThornsJoinAnnouncementBus.Active )
		{
			var row = ThornsUiFactory.AddPanel( _root, "join-announcement-row" );
			ThornsHudTheme.ApplyHudGlass( row );
			ThornsUiFactory.AddLabel( row, entry.Message, "join-announcement-label" );
		}
	}
}
