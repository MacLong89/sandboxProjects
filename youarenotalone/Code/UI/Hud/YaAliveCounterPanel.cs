using Sandbox.UI;



namespace Sandbox;



/// <summary>Living NotAlone count from replicated health across pawns.</summary>

public sealed class YaAliveCounterPanel : Panel

{

	readonly Label _label;



	public YaAliveCounterPanel()

	{

		AddClass( "ya-hud-alive-panel" );

		Style.Width = Length.Fraction( 1f );

		Style.FlexDirection = FlexDirection.Column;

		Style.AlignItems = Align.Center;

		_label = AddChild( new Label( "", "ya-hud-alive-panel__text" ) );

		_label.Style.FontSize = YaUiDesignTokens.TopStackCounterFontPx;

		_label.Style.FontWeight = 700;

		_label.Style.FontColor = YaHudTheme.Teal;

		_label.Style.MarginTop = YaUiDesignTokens.TopStackRowGapPx + 1f;
		_label.Style.TextAlign = TextAlign.Center;
		_label.Style.Width = Length.Fraction( 1f );
		_label.Style.MaxWidth = Length.Pixels( YaUiDesignTokens.TopStackTextMaxWidthPx );

	}



	public void SetAccent( Color accent ) => _label.Style.FontColor = accent;

	public void SetCount( int n, bool hunterTeamRoster = false )
	{
		_label.Text = hunterTeamRoster
			? $"Hunters alive: {n}"
			: $"Humans alive: {n}";
	}

}


