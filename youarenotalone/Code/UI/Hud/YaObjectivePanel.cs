using Sandbox.UI;

namespace Sandbox;

public sealed class YaObjectivePanel : Panel
{
	readonly Label _label;

	public YaObjectivePanel()
	{
		AddClass( "ya-hud-objective-panel" );
		Style.Width = Length.Fraction( 1f );
		Style.FlexDirection = FlexDirection.Column;
		Style.AlignItems = Align.Center;
		_label = AddChild( new Label( "", "ya-hud-objective-panel__text" ) );
		_label.Style.FontSize = YaUiDesignTokens.TopStackObjectiveFontPx;
		_label.Style.FontWeight = 600;
		_label.Style.FontColor = YaHudTheme.TextSecondary;
		_label.Style.MarginTop = YaUiDesignTokens.TopStackRowGapPx;
		_label.Style.TextAlign = TextAlign.Center;
		_label.Style.Width = Length.Fraction( 1f );
		_label.Style.MaxWidth = Length.Pixels( YaUiDesignTokens.TopStackTextMaxWidthPx );
	}

	public void ApplyFromRole( YaPlayerRole role )
	{
		_label.Text = role switch
		{
			YaPlayerRole.Alone => "Eliminate all Not Alone",
			YaPlayerRole.NotAlone => "Hunt the Alone — watch for mimic and paranoia",
			_ => ""
		};
	}
}
