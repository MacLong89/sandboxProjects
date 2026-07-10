namespace Terraingen.UI.Hud;

using Sandbox.UI;
using Terraingen.UI;

/// <summary>Thin gold XP line integrated beneath the hotbar.</summary>
public sealed class ThornsHudXpStrip
{
	readonly Panel _fill;
	readonly Label _value;

	public ThornsHudXpStrip( Panel parent )
	{
		var root = ThornsUiFactory.AddPanel( parent, "hotbar-xp-strip" );
		root.Style.Width = Length.Percent( 100 );
		root.Style.FlexDirection = FlexDirection.Column;
		root.Style.MarginTop = Length.Pixels( ThornsHudTheme.HotbarXpMarginTopPx );

		var track = ThornsUiFactory.AddPanel( root, "hotbar-xp-track" );
		track.Style.Width = Length.Percent( 100 );
		track.Style.Height = Length.Pixels( ThornsHudTheme.HotbarXpTrackHeightPx );
		track.Style.Position = PositionMode.Relative;
		track.Style.Overflow = OverflowMode.Hidden;
		if ( !ThornsHudClassicChrome.IsActive )
		{
			track.Style.BackgroundColor = new Color( 0f, 0f, 0f, 0.45f );
			track.Style.BorderColor = new Color( 1f, 1f, 1f, 0.06f );
			track.Style.BorderWidth = Length.Pixels( 1 );
		}

		_fill = ThornsUiFactory.AddPanel( track, "hotbar-xp-fill" );
		_fill.Style.Height = Length.Percent( 100 );
		_fill.Style.Width = Length.Percent( 0 );
		_fill.Style.BackgroundColor = ThornsHudTheme.XpFill;

		_value = ThornsUiFactory.AddLabel( root, "", "hotbar-xp-value" );
		ThornsHudTheme.ApplyStatNumber( _value );
		_value.Style.MarginTop = Length.Pixels( ThornsHudTheme.HotbarXpValueMarginTopPx );
		_value.Style.FontSize = Length.Pixels( ThornsHudTheme.HotbarXpValueFontPx );
		_value.Style.TextAlign = TextAlign.Right;
		_value.Style.FontColor = ThornsHudTheme.Gold;
		_value.Style.Opacity = 0.85f;
	}

	public void Set( float current, float max, string text = null )
	{
		var frac = max > 0f ? Math.Clamp( current / max, 0f, 1f ) : 0f;
		_fill.Style.Width = Length.Percent( frac * 100f );
		_value.Text = string.IsNullOrEmpty( text ) ? $"{current:0} / {max:0} XP" : text;
	}
}
