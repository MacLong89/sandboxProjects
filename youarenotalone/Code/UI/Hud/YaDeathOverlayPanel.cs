using Sandbox.UI;

namespace Sandbox;

/// <summary>Full-screen overlay when local pawn is dead (replicated health).</summary>
public sealed class YaDeathOverlayPanel : Panel
{
	readonly Label _title;
	readonly Label _sub;
	readonly Label _spectateHint;
	readonly Label _nextRound;

	public YaDeathOverlayPanel()
	{
		AddClass( "ya-hud-death-overlay" );
		Style.Position = PositionMode.Absolute;
		Style.Left = 0;
		Style.Top = 0;
		Style.Width = Length.Fraction( 1f );
		Style.Height = Length.Fraction( 1f );
		Style.JustifyContent = Justify.Center;
		Style.AlignItems = Align.Center;
		Style.BackgroundColor = new Color( 8f / 255f, 10f / 255f, 12f / 255f, 1f );
		Style.PointerEvents = PointerEvents.None;

		var col = AddChild<Panel>( "ya-hud-death-overlay__col" );
		col.Style.FlexDirection = FlexDirection.Column;
		col.Style.AlignItems = Align.Center;

		_title = col.AddChild( new Label( "You died", "ya-hud-death-overlay__title" ) );
		_title.Style.FontSize = 36;
		_title.Style.FontWeight = 900;
		_title.Style.FontColor = YaHudTheme.TextPrimary;
		_title.Style.MarginBottom = 12;

		_sub = col.AddChild( new Label( "Spectating…", "ya-hud-death-overlay__sub" ) );
		_sub.Style.FontSize = 18;
		_sub.Style.FontWeight = 600;
		_sub.Style.FontColor = YaHudTheme.TextSecondary;
		_sub.Style.MarginBottom = 8;

		_spectateHint = col.AddChild( new Label( "LMB / RMB — switch spectate target", "ya-hud-death-overlay__hint" ) );
		_spectateHint.Style.FontSize = 14;
		_spectateHint.Style.FontWeight = 600;
		_spectateHint.Style.FontColor = YaHudTheme.Teal;
		_spectateHint.Style.MarginBottom = 6;

		_nextRound = col.AddChild( new Label( "", "ya-hud-death-overlay__next" ) );
		_nextRound.Style.FontSize = 13;
		_nextRound.Style.FontWeight = 600;
		_nextRound.Style.FontColor = YaHudTheme.TextMuted;
	}

	public void Apply( int nextRoundSecondsRemaining )
	{
		if ( nextRoundSecondsRemaining > 0 )
			_nextRound.Text = $"Next round in {nextRoundSecondsRemaining}s";
		else
			_nextRound.Text = "Next round starting soon…";
	}
}
