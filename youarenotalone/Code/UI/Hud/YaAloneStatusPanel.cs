using Sandbox.UI;

namespace Sandbox;

/// <summary>Visible to NotAlone only: Alone eliminated or alive (from replicated IDs + health).</summary>
public sealed class YaAloneStatusPanel : Panel
{
	readonly Label _label;

	public YaAloneStatusPanel()
	{
		AddClass( "ya-hud-alone-status" );
		Style.Width = Length.Fraction( 1f );
		Style.FlexDirection = FlexDirection.Column;
		Style.AlignItems = Align.Center;
		_label = AddChild( new Label( "", "ya-hud-alone-status__text" ) );
		_label.Style.FontSize = YaUiDesignTokens.TopStackStatusFontPx;
		_label.Style.FontWeight = 700;
		_label.Style.MarginTop = YaUiDesignTokens.TopStackRowGapPx;
		_label.Style.TextAlign = TextAlign.Center;
		_label.Style.Width = Length.Fraction( 1f );
		_label.Style.MaxWidth = Length.Pixels( YaUiDesignTokens.TopStackTextMaxWidthPx );
	}

	public void Apply( bool aloneAlive )
	{
		if ( aloneAlive )
		{
			_label.Text = "Alone: Alive";
			_label.Style.FontColor = YaHudTheme.Danger;
		}
		else
		{
			_label.Text = "Alone: Eliminated";
			_label.Style.FontColor = YaHudTheme.Success;
		}
	}
}
