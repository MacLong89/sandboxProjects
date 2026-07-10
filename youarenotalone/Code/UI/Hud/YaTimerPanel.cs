using System;
using Sandbox.UI;

namespace Sandbox;

public sealed class YaTimerPanel : Panel
{
	readonly Label _line;

	public YaTimerPanel()
	{
		AddClass( "ya-hud-timer-panel" );
		Style.Width = Length.Fraction( 1f );
		Style.FlexDirection = FlexDirection.Column;
		Style.AlignItems = Align.Center;
		_line = AddChild( new Label( "", "ya-hud-timer-panel__line" ) );
		_line.Style.FontSize = YaUiDesignTokens.TopStackTimerFontPx;
		_line.Style.FontWeight = 700;
		_line.Style.FontColor = YaHudTheme.TextPrimary;
		_line.Style.TextAlign = TextAlign.Center;
		_line.Style.Width = Length.Fraction( 1f );
		_line.Style.MaxWidth = Length.Pixels( YaUiDesignTokens.TopStackTextMaxWidthPx );
	}

	public void Apply( YaGameState state, float countdownSeconds, int connectedPlayers )
	{
		_ = connectedPlayers;
		var sec = Math.Max( 0, (int)Math.Ceiling( countdownSeconds ) );
		_line.RemoveClass( "ya-hud-timer-panel__line--urgent" );

		if ( state == YaGameState.Lobby )
		{
			_line.Text = "Waiting for other players…";
			return;
		}

		if ( state == YaGameState.Intermission )
		{
			_line.Text = sec > 0
				? $"Next round in {sec}s — free roam, no combat"
				: "Next round starting…";
			return;
		}

		if ( state == YaGameState.InRound )
		{
			if ( sec <= 0 )
			{
				_line.Text = "";
				return;
			}

			_line.Text = sec <= 30
				? $"Round ends in {sec}s — hurry!"
				: $"Round ends in {sec}s";

			if ( sec <= 30 )
				_line.AddClass( "ya-hud-timer-panel__line--urgent" );
			return;
		}

		_line.Text = "";
	}
}
