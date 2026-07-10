namespace Terraingen.UI.Hud;

using Sandbox.UI;
using Terraingen.GameData;
using Terraingen.UI;
using Terraingen.UI.Core;

public enum ThornsHudBarTier
{
	Large,
	Medium,
	Small
}

/// <summary>Custom-shaped stat bar — track + fill + right-aligned values (visual only).</summary>
public sealed class ThornsHudBar
{
	readonly Panel _fill;
	readonly Label _value;
	readonly Panel _root;
	readonly string _fieldTag;
	bool _lowWarning;

	public ThornsHudBar( Panel parent, string barClass, Color fillColor, ThornsHudBarTier tier = ThornsHudBarTier.Medium, bool fillParentWidth = false, string iconPath = null )
	{
		var classicVitals = fillParentWidth && ThornsHudClassicChrome.IsActive;
		_fieldTag = ThornsUiSkin.Active == ThornsUiSkinKind.Field
			? barClass switch
			{
				"health" => "HP",
				"thirst" => "H2O",
				"hunger" => "FOOD",
				_ => null
			}
			: null;
		_root = ThornsUiFactory.AddPanel( parent, $"hud-bar-row hud-bar-{barClass} hud-bar-tier-{tier.ToString().ToLower()}" );
		if ( classicVitals )
			_root.AddClass( "hud-bar-row-classic" );
		if ( classicVitals )
		{
			_root.Style.MinWidth = Length.Pixels( 0 );
			_root.Style.MaxWidth = Length.Percent( 100 );
			_root.Style.Overflow = OverflowMode.Hidden;
		}

		_root.Style.FlexDirection = FlexDirection.Row;
		_root.Style.AlignItems = Align.Center;
		_root.Style.BorderWidth = Length.Pixels( 2 );
		_root.Style.BorderColor = Color.Transparent;
		_root.Style.Width = fillParentWidth
			? Length.Percent( 100 )
			: Length.Pixels( tier switch
			{
				ThornsHudBarTier.Large => ThornsHudTheme.VitalsBarRowLargeWidthPx,
				ThornsHudBarTier.Small => ThornsHudTheme.VitalsBarRowSmallWidthPx,
				_ => ThornsHudTheme.VitalsBarRowMediumWidthPx
			} );

		var icon = ThornsUiFactory.AddPanel( _root, "hud-bar-icon slot-icon" );
		var iconPx = classicVitals ? ThornsHudTheme.ClassicVitalsBarIconPx : ThornsHudTheme.VitalsBarIconPx;
		icon.Style.Width = Length.Pixels( iconPx );
		icon.Style.Height = Length.Pixels( iconPx );
		icon.Style.MinWidth = Length.Pixels( iconPx );
		icon.Style.MinHeight = Length.Pixels( iconPx );
		icon.Style.FlexShrink = 0;
		icon.Style.MarginRight = Length.Pixels( classicVitals
			? ThornsHudTheme.ClassicVitalsBarIconGapPx
			: ThornsHudTheme.VitalsBarIconGapPx );
		ThornsIconCache.ApplyToPanel( icon, string.IsNullOrWhiteSpace( iconPath ) ? ThornsIconRegistry.Hud( barClass ) : iconPath );

		var trackShell = ThornsUiFactory.AddPanel( _root, "hud-bar-shell" );
		trackShell.Style.FlexGrow = 1;
		trackShell.Style.FlexShrink = 1;
		trackShell.Style.MinWidth = Length.Pixels( classicVitals
			? ThornsHudTheme.ClassicVitalsBarTrackMinWidthPx
			: ThornsHudTheme.VitalsBarTrackMinWidthPx );
		trackShell.Style.Position = PositionMode.Relative;
		trackShell.Style.JustifyContent = Justify.Center;

		var track = ThornsUiFactory.AddPanel( trackShell, "hud-bar-track" );
		track.Style.Width = Length.Percent( 100 );
		track.Style.MinWidth = Length.Pixels( classicVitals
			? ThornsHudTheme.ClassicVitalsBarTrackMinWidthPx
			: ThornsHudTheme.VitalsBarTrackMinWidthPx );
		track.Style.Height = Length.Pixels( classicVitals
			? ThornsHudTheme.ClassicVitalsBarTrackPx
			: tier switch
			{
				ThornsHudBarTier.Large => ThornsHudTheme.VitalsBarTrackLargePx,
				ThornsHudBarTier.Small => ThornsHudTheme.VitalsBarTrackSmallPx,
				_ => ThornsHudTheme.VitalsBarTrackMediumPx
			} );
		track.Style.Position = PositionMode.Relative;
		track.Style.Overflow = OverflowMode.Hidden;
		ThornsHudTheme.ApplyHudBarTrack( track );

		var shine = ThornsUiFactory.AddPanel( track, "hud-bar-track-shine" );
		if ( classicVitals )
			shine.Style.Display = DisplayMode.None;

		_fill = ThornsUiFactory.AddPanel( track, $"hud-bar-fill hud-bar-fill-{barClass}" );
		_fill.Style.Position = PositionMode.Absolute;
		_fill.Style.Left = Length.Pixels( 0 );
		_fill.Style.Top = Length.Pixels( 0 );
		_fill.Style.Height = Length.Percent( 100 );
		_fill.Style.Width = Length.Percent( 0 );
		_fill.Style.BackgroundColor = fillColor;
		if ( classicVitals )
			_fill.AddClass( "hud-bar-fill-classic" );

		_value = ThornsUiFactory.AddLabel( _root, "", "hud-bar-value" );
		ThornsHudTheme.ApplyStatNumber( _value );
		_value.Style.MarginLeft = Length.Pixels( classicVitals
			? ThornsHudTheme.ClassicVitalsBarValueMarginLeftPx
			: ThornsHudTheme.VitalsBarValueMarginLeftPx );
		_value.Style.MinWidth = Length.Pixels( classicVitals
			? ThornsHudTheme.ClassicVitalsBarValueMinWidthPx
			: ThornsHudTheme.VitalsBarValueMinWidthPx );
		_value.Style.MaxWidth = Length.Pixels( classicVitals
			? ThornsHudTheme.ClassicVitalsBarValueMinWidthPx
			: ThornsHudTheme.VitalsBarValueMinWidthPx );
		_value.Style.FlexShrink = 0;
		_value.Style.WhiteSpace = WhiteSpace.NoWrap;
		_value.Style.TextAlign = TextAlign.Right;
		if ( classicVitals )
			_value.Style.FontSize = Length.Pixels( ThornsHudTheme.ClassicVitalsValueFontPx );
	}

	public void Set( float current, float max, string text = null )
	{
		var frac = max > 0f ? Math.Clamp( current / max, 0f, 1f ) : 0f;
		_fill.Style.Width = Length.Percent( frac * 100f );
		if ( string.IsNullOrEmpty( text ) )
		{
			var values = $"{current:0}/{max:0}";
			_value.Text = _fieldTag is not null ? $"{_fieldTag} {values}" : $"{current:0} / {max:0}";
		}
		else
		{
			_value.Text = text;
		}
	}

	public void SetVisible( bool visible )
	{
		if ( !_root.IsValid )
			return;

		_root.Style.Display = visible ? DisplayMode.Flex : DisplayMode.None;
	}

	public void SetLowWarning( bool active )
	{
		_lowWarning = active;
		if ( !_root.IsValid )
			return;

		_root.SetClass( "hud-bar-low-warning", active );
		if ( !active )
			_root.Style.BorderColor = Color.Transparent;
	}

	public void TickLowWarningPulse()
	{
		if ( !_lowWarning || !_root.IsValid )
			return;

		var alpha = 0.28f + 0.32f * (0.5f + 0.5f * MathF.Sin( (float)Time.Now * 5.5f ));
		_root.Style.BorderColor = new Color( 0.92f, 0.18f, 0.18f, alpha );
	}
}
