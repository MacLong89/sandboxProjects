namespace Terraingen.UI.Components;

using Sandbox.UI;
using Terraingen.GameData;
using Terraingen.UI;
using Terraingen.UI.Core;

/// <summary>Segmented vitals bar for the inventory menu left column (visual only).</summary>
public sealed class ThornsMenuStatBar
{
	const int SegmentCount = 10;

	readonly Panel[] _segments = new Panel[SegmentCount];
	readonly Label _valueLabel;

	public ThornsMenuStatBar( Panel parent, string label, string iconKey, string toneClass )
	{
		var row = ThornsUiFactory.AddPanel( parent, $"menu-stat-row menu-stat-tone-{toneClass}" );
		row.Style.FlexDirection = FlexDirection.Row;
		row.Style.AlignItems = Align.Center;
		row.Style.Width = Length.Percent( 100 );
		row.Style.FlexShrink = 0;
		row.Style.MarginBottom = Length.Pixels( 5 );

		if ( !string.IsNullOrWhiteSpace( iconKey ) )
		{
			var icon = ThornsUiFactory.AddPanel( row, "menu-stat-icon slot-icon" );
			icon.Style.Width = Length.Pixels( ThornsUiMetrics.MenuStatIcon );
			icon.Style.Height = Length.Pixels( ThornsUiMetrics.MenuStatIcon );
			icon.Style.MinWidth = Length.Pixels( ThornsUiMetrics.MenuStatIcon );
			icon.Style.MinHeight = Length.Pixels( ThornsUiMetrics.MenuStatIcon );
			icon.Style.FlexShrink = 0;
			ThornsIconCache.ApplyToPanel( icon, ThornsIconRegistry.Hud( iconKey ) );
		}

		var body = ThornsUiFactory.AddPanel( row, "menu-stat-body" );
		body.Style.FlexDirection = FlexDirection.Column;
		body.Style.FlexGrow = 1;
		body.Style.MinWidth = Length.Pixels( 0 );

		ThornsUiFactory.AddPassiveLabel( body, label, "menu-stat-label" );

		var track = ThornsUiFactory.AddPanel( body, "menu-stat-track" );
		track.Style.Width = Length.Percent( 100 );

		var segments = ThornsUiFactory.AddPanel( track, "menu-stat-segments" );
		segments.Style.FlexDirection = FlexDirection.Row;
		segments.Style.Width = Length.Percent( 100 );

		for ( var i = 0; i < SegmentCount; i++ )
		{
			_segments[i] = ThornsUiFactory.AddPanel( segments, $"menu-stat-segment menu-stat-segment-{toneClass}" );
			_segments[i].Style.FlexGrow = 1;
			_segments[i].Style.Height = Length.Pixels( ThornsUiMetrics.MenuStatSegmentHeight );
			_segments[i].Style.MinWidth = Length.Pixels( 3 );
		}

		_valueLabel = ThornsUiFactory.AddPassiveLabel( row, "", "menu-stat-value" );
		_valueLabel.Style.FlexShrink = 0;
		_valueLabel.Style.MinWidth = Length.Pixels( 58 );
		_valueLabel.Style.FontSize = Length.Pixels( 10 );
		_valueLabel.Style.TextAlign = TextAlign.Right;
	}

	public void Set( float current, float max )
	{
		var frac = max > 0f ? Math.Clamp( current / max, 0f, 1f ) : 0f;
		var filled = (int)MathF.Round( frac * SegmentCount );

		for ( var i = 0; i < SegmentCount; i++ )
			_segments[i].SetClass( "filled", i < filled );

		_valueLabel.Text = $"{current:0} / {max:0}";
	}
}
