namespace Terraingen.UI.Hud;

using Sandbox.UI;
using Terraingen.UI;
using Terraingen.UI.Core;
using Terraingen.World.Environment;

/// <summary>Top-left Classic HUD stack: current goal + day/time.</summary>
public sealed class ThornsLeftHudColumn
{
	readonly Panel _root;
	readonly Label _dayLabel;
	readonly Label _timeLabel;
	readonly Label _timeIcon;

	public ThornsObjectivesHud Objectives { get; }

	public ThornsLeftHudColumn( Panel hudLayer )
	{
		var goalsAnchor = ThornsUiFactory.AddPanel( hudLayer, "left-objectives-anchor" );
		goalsAnchor.Style.Position = PositionMode.Absolute;
		goalsAnchor.Style.Top = Length.Pixels( ThornsHudTheme.ClassicLeftColumnTopPx );
		goalsAnchor.Style.Left = Length.Pixels( ThornsHudTheme.ClassicLeftColumnLeftPx );
		goalsAnchor.Style.Width = Length.Pixels( ThornsHudTheme.ClassicLeftColumnWidthPx );
		goalsAnchor.Style.FlexDirection = FlexDirection.Column;
		goalsAnchor.Style.AlignItems = Align.FlexStart;
		goalsAnchor.Style.PointerEvents = PointerEvents.None;

		Objectives = new ThornsObjectivesHud( goalsAnchor );

		_root = ThornsUiFactory.AddPanel( hudLayer, "left-hud-column hud-daytime-cluster hud-daytime-hidden" );
		_root.Style.Display = DisplayMode.None;
		_root.Style.Position = PositionMode.Absolute;
		_root.Style.Top = Length.Pixels( ThornsHudTheme.ClassicLeftColumnTopPx );
		_root.Style.Left = Length.Pixels( ThornsHudTheme.ClassicLeftColumnLeftPx );
		_root.Style.FlexDirection = FlexDirection.Row;
		_root.Style.AlignItems = Align.Center;
		_root.Style.PointerEvents = PointerEvents.None;

		_timeIcon = ThornsUiFactory.AddLabel( _root, "☀", "hud-time-icon" );

		var dayCol = ThornsUiFactory.AddPanel( _root, "hud-day-col" );
		dayCol.Style.FlexDirection = FlexDirection.Column;
		dayCol.Style.JustifyContent = Justify.Center;
		_dayLabel = ThornsUiFactory.AddLabel( dayCol, "LEVEL 1", "hud-day-label" );
		_timeLabel = ThornsUiFactory.AddLabel( dayCol, "12:00 PM", "hud-time-label" );

		UpdateDayTime();
	}

	public void RefreshObjectives() => Objectives.Refresh();

	public void UpdatePinAlert() => Objectives.UpdatePinAlert();

	public void UpdateDayTime()
	{
		if ( !_dayLabel.IsValid || !_timeLabel.IsValid )
			return;

		var level = Math.Max( 1, ThornsUiClientState.Snapshot?.Skills?.PlayerLevel ?? 1 );
		_dayLabel.Text = $"LEVEL {level}";

		if ( !ThornsTimeOfDaySystem.TryGet( Game.ActiveScene, out var time ) || !time.IsValid() )
		{
			_timeLabel.Text = DateTime.Now.ToString( "h:mm tt" );
			if ( _timeIcon.IsValid )
				_timeIcon.Text = "☀";
			return;
		}

		var hours = time.ResolvedHours;
		var h = (int)hours % 24;
		var m = (int)((hours - h) * 60f);
		_timeLabel.Text = DateTime.Today.AddHours( h ).AddMinutes( m ).ToString( "h:mm tt" );

		if ( _timeIcon.IsValid )
		{
			var night = time.CurrentState.NightFactor > 0.65f;
			_timeIcon.Text = night ? "☾" : "☀";
		}
	}
}
