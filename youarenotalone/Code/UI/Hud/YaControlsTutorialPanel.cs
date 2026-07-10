using Sandbox.UI;

namespace Sandbox;

/// <summary>Full-screen tactical controls reference (hold C / View).</summary>
public sealed class YaControlsTutorialPanel : Panel
{
	const float LayoutWidthPx = 880f;
	const float ColumnWidthPx = 428f;
	const float RowTextWidthPx = 268f;
	const float ColumnGapPx = 24f;

	public YaControlsTutorialPanel()
	{
		AddClass( "ya-controls-tutorial" );

		Style.Position = PositionMode.Absolute;
		Style.Left = 0;
		Style.Top = 0;
		Style.Width = Length.Fraction( 1f );
		Style.Height = Length.Fraction( 1f );
		Style.PointerEvents = PointerEvents.None;
		Style.FlexDirection = FlexDirection.Column;
		Style.AlignItems = Align.Center;
		Style.JustifyContent = Justify.Center;

		AddChild<Panel>( "ya-controls-tutorial__vignette" );

		var stack = AddChild<Panel>( "ya-controls-tutorial__stack" );
		stack.Style.Width = Length.Pixels( LayoutWidthPx );
		stack.Style.MaxWidth = Length.Fraction( 0.96f );
		stack.Style.FlexDirection = FlexDirection.Column;
		stack.Style.AlignItems = Align.Center;

		var titleRow = stack.AddChild<Panel>( "ya-controls-tutorial__title-row" );
		titleRow.Style.FlexDirection = FlexDirection.Row;
		titleRow.Style.AlignItems = Align.Center;
		titleRow.Style.JustifyContent = Justify.Center;
		titleRow.Style.MarginBottom = 4;

		AddText( titleRow, "PRESS", "ya-controls-tutorial__title-part", 22, fit: true );
		var keyBadge = titleRow.AddChild<Panel>( "ya-controls-tutorial__title-key" );
		keyBadge.Style.MinWidth = 44;
		keyBadge.Style.Height = 37;
		keyBadge.Style.MarginLeft = 8;
		keyBadge.Style.MarginRight = 8;
		keyBadge.Style.JustifyContent = Justify.Center;
		keyBadge.Style.AlignItems = Align.Center;
		AddText( keyBadge, "C", "ya-controls-tutorial__title-key-text", 22, fit: true );
		AddText( titleRow, "FOR CONTROLS", "ya-controls-tutorial__title-part", 22, fit: true );

		var subtitle = AddText( stack, "TAB — SCOREBOARD", "ya-controls-tutorial__subtitle", 10, fullWidthPx: LayoutWidthPx );
		subtitle.Style.TextAlign = TextAlign.Center;
		subtitle.Style.MarginBottom = 10;

		var columns = stack.AddChild<Panel>( "ya-controls-tutorial__columns" );
		columns.Style.Width = Length.Pixels( LayoutWidthPx );
		columns.Style.FlexDirection = FlexDirection.Row;
		columns.Style.JustifyContent = Justify.Center;
		columns.Style.AlignItems = Align.FlexStart;

		BuildRoleSection(
			columns,
			"ya-controls-tutorial__section ya-controls-tutorial__section--not-alone",
			"NOT ALONE",
			"TACTICAL HUNTERS",
			marginRight: ColumnGapPx,
			rows: new (string key, string action, string desc)[]
			{
				( "1", "Equip M4", "Primary rifle" ),
				( "2", "Equip Shotgun", "Close-quarters shotgun" ),
				( "LMB", "Shoot", "Fire equipped weapon" ),
				( "RMB", "Aim Down Sights", "Steady aim" ),
				( "R", "Reload", "Refill magazine" ),
			} );

		BuildRoleSection(
			columns,
			"ya-controls-tutorial__section ya-controls-tutorial__section--alone",
			"ALONE",
			"THE HIDDEN",
			marginRight: 0,
			rows: new (string key, string action, string desc)[]
			{
				( "LMB", "Light Slash", "Fast melee attack" ),
				( "RMB", "Heavy Attack", "Hold to charge, release" ),
				( "Q", "Dash", "Burst movement" ),
				( "E", "Paranoia", "Debuff hunters" ),
				( "F", "Mimic", "Look like a hunter" ),
				( "Passive", "Stick to Walls", "Hold Space" ),
			} );

		var brand = AddText( stack, "YOU ARE NOT ALONE", "ya-controls-tutorial__brand", 9, fullWidthPx: LayoutWidthPx );
		brand.Style.TextAlign = TextAlign.Center;
		brand.Style.MarginTop = 8;
	}

	static Label AddText( Panel parent, string text, string cssClass, int fontSizePx, bool fit = false, float fullWidthPx = 0f )
	{
		var lbl = parent.AddChild( new Label( text, cssClass ) );
		lbl.Style.FontSize = fontSizePx;
		lbl.Style.LineHeight = Length.Pixels( fontSizePx + 6 );
		lbl.Style.WhiteSpace = WhiteSpace.Normal;
		lbl.Style.FlexShrink = 0;

		if ( !fit && fullWidthPx > 1f )
			lbl.Style.Width = Length.Pixels( fullWidthPx );

		return lbl;
	}

	static void BuildRoleSection(
		Panel parent,
		string sectionClass,
		string roleTitle,
		string roleTagline,
		float marginRight,
		(string key, string action, string desc)[] rows )
	{
		var section = parent.AddChild<Panel>( sectionClass );
		section.Style.Width = Length.Pixels( ColumnWidthPx );
		section.Style.FlexDirection = FlexDirection.Column;
		section.Style.FlexShrink = 0;
		if ( marginRight > 0.5f )
			section.Style.MarginRight = Length.Pixels( marginRight );

		var header = section.AddChild<Panel>( "ya-controls-tutorial__section-header" );
		header.Style.Width = Length.Pixels( ColumnWidthPx );
		header.Style.FlexDirection = FlexDirection.Column;
		header.Style.MarginBottom = 8;
		header.Style.PaddingBottom = 8;
		header.Style.BorderBottomWidth = 1;
		header.Style.BorderBottomColor = new Color( 1f, 1f, 1f, 0.07f );

		var titleLbl = AddText( header, roleTitle, "ya-controls-tutorial__role-title", 18, fullWidthPx: ColumnWidthPx );
		titleLbl.Style.MarginBottom = 2;
		AddText( header, roleTagline, "ya-controls-tutorial__role-tagline", 9, fullWidthPx: ColumnWidthPx );

		var list = section.AddChild<Panel>( "ya-controls-tutorial__rows" );
		list.Style.Width = Length.Pixels( ColumnWidthPx );
		list.Style.FlexDirection = FlexDirection.Column;

		for ( var i = 0; i < rows.Length; i++ )
		{
			var row = rows[i];
			AddControlRow( list, row.key, row.action, row.desc, isLast: i == rows.Length - 1 );
		}
	}

	static void AddControlRow( Panel parent, string key, string action, string desc, bool isLast )
	{
		var row = parent.AddChild<Panel>( "ya-controls-tutorial__row" );
		row.Style.Width = Length.Pixels( ColumnWidthPx );
		row.Style.FlexDirection = FlexDirection.Row;
		row.Style.AlignItems = Align.FlexStart;
		row.Style.PaddingTop = 6;
		row.Style.PaddingBottom = 6;
		row.Style.FlexShrink = 0;

		if ( !isLast )
		{
			row.Style.BorderBottomWidth = 1;
			row.Style.BorderBottomColor = new Color( 1f, 1f, 1f, 0.05f );
		}

		var keyBox = row.AddChild<Panel>( "ya-controls-tutorial__key" );
		keyBox.Style.Width = 44;
		keyBox.Style.Height = 30;
		keyBox.Style.FlexShrink = 0;
		keyBox.Style.JustifyContent = Justify.Center;
		keyBox.Style.AlignItems = Align.Center;
		keyBox.Style.MarginRight = 8;
		var keyLbl = AddText( keyBox, key, "ya-controls-tutorial__key-text", 10, fit: true );
		keyLbl.Style.TextAlign = TextAlign.Center;

		var arrow = AddText( row, "→", "ya-controls-tutorial__arrow", 12, fit: true );
		arrow.Style.Width = 18;
		arrow.Style.MarginRight = 6;
		arrow.Style.MarginTop = 4;
		arrow.Style.TextAlign = TextAlign.Center;
		arrow.Style.FlexShrink = 0;

		var textCol = row.AddChild<Panel>( "ya-controls-tutorial__row-text" );
		textCol.Style.Width = Length.Pixels( RowTextWidthPx );
		textCol.Style.FlexDirection = FlexDirection.Column;
		textCol.Style.FlexShrink = 0;

		var actionLbl = AddText( textCol, action, "ya-controls-tutorial__action", 12, fullWidthPx: RowTextWidthPx );
		actionLbl.Style.Width = Length.Pixels( RowTextWidthPx );
		actionLbl.Style.MarginBottom = 1;
		actionLbl.Style.LineHeight = Length.Pixels( 16 );

		if ( !string.IsNullOrWhiteSpace( desc ) )
		{
			var descLbl = AddText( textCol, desc, "ya-controls-tutorial__desc", 10, fullWidthPx: RowTextWidthPx );
			descLbl.Style.Width = Length.Pixels( RowTextWidthPx );
			descLbl.Style.LineHeight = Length.Pixels( 14 );
		}
	}
}
