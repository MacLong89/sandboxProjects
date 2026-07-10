namespace Terraingen.UI;

using Sandbox.UI;

/// <summary>Shared Thorns UI tokens and panel helpers.</summary>
public static class ThornsTheme
{
	static ThornsUiSkinPalette P => ThornsUiSkinTokens.Active;

	public static Color PanelBg => P.PanelBg;
	public static Color Border => P.Border;
	public static Color TextPrimary => P.TextPrimary;
	public static Color TextSecondary => P.TextSecondary;
	public static Color Accent => P.Accent;
	public static Color Health => P.Health;
	public static Color Stamina => P.Stamina;
	public static Color Hunger => P.Hunger;

	public static Color HudHealth => ThornsHudTheme.HealthFill;
	public static Color HudThirst => ThornsHudTheme.ThirstFill;
	public static Color HudHunger => ThornsHudTheme.HungerFill;
	public static Color HudXp => ThornsHudTheme.XpFill;
	public static Color Danger => P.Danger;

	public static void ApplyGlassPanel( Panel panel )
	{
		if ( panel is null || !panel.IsValid )
			return;

		panel.AddClass( "thorns-glass" );
		panel.Style.BackgroundColor = PanelBg;
		panel.Style.BorderColor = Border;
		panel.Style.BorderWidth = Length.Pixels( 1 );
	}

	/// <summary>Solid panel (HUD popups and non-framed surfaces).</summary>
	public static void ApplyOpaquePanel( Panel panel )
	{
		if ( panel is null || !panel.IsValid )
			return;

		panel.AddClass( "thorns-menu-opaque" );
		panel.Style.BackgroundColor = P.OpaquePanelBg;
		panel.Style.BorderColor = Border;
		panel.Style.BorderWidth = Length.Pixels( 1 );
	}

	/// <summary>Tab menu column — parchment section on shared menu backdrop (no nested wood frame).</summary>
	public static void ApplyMenuPanel( Panel panel ) => ApplyParchmentColumn( panel );

	/// <summary>Light inset panel on parchment — no wood 9-slice frame.</summary>
	public static void ApplyConceptSection( Panel panel )
	{
		if ( panel is null || !panel.IsValid )
			return;

		panel.AddClass( "concept-section" );
	}

	/// <summary>Flat dark inventory/hotbar slot — no 9-slice slot frame.</summary>
	public static void ApplyConceptSlot( Panel panel )
	{
		if ( panel is null || !panel.IsValid )
			return;

		panel.AddClass( "concept-slot" );
	}

	/// <summary>Parchment card for main-menu browser/settings panels.</summary>
	public static void ApplyParchmentCard( Panel panel )
	{
		if ( panel is null || !panel.IsValid )
			return;

		panel.AddClass( "mainmenu-parchment-card mainmenu-browser-panel-card thorns-parchment-flat" );
		panel.Style.BackgroundColor = ThornsUiSkin.Active == ThornsUiSkinKind.Classic
			? new Color( 14f / 255f, 10f / 255f, 6f / 255f, 0.92f )
			: ThornsUiSkinTokens.Active.OpaquePanelBg;
	}

	/// <summary>Parchment column — transparent on shared menu backdrop (no edge vignette).</summary>
	public static void ApplyParchmentColumn( Panel panel )
	{
		if ( panel is null || !panel.IsValid )
			return;

		panel.AddClass( "thorns-parchment-column" );
		panel.Style.BackgroundColor = Color.Transparent;
		panel.Style.BorderTopWidth = Length.Pixels( 0 );
		panel.Style.BorderBottomWidth = Length.Pixels( 0 );
		panel.Style.BorderLeftWidth = Length.Pixels( 0 );
		panel.Style.BorderRightWidth = Length.Pixels( 0 );
	}

	/// <summary>Menu column shell — transparent; full-screen parchment comes from the menu overlay.</summary>
	public static Panel CreateMenuSectionWindow( Panel parent, string extraClass, float flexWeight = 1f )
	{
		var column = ThornsUiFactory.AddPanel( parent, $"inventory-column-window thorns-menu-column {extraClass}" );
		column.Style.FlexDirection = FlexDirection.Column;
		column.Style.FlexGrow = (int)flexWeight;
		column.Style.FlexShrink = 1;
		column.Style.FlexBasis = Length.Pixels( 0 );
		column.Style.MinWidth = Length.Pixels( 0 );
		column.Style.MinHeight = Length.Pixels( 0 );
		column.Style.AlignSelf = Align.Stretch;
		column.Style.Overflow = OverflowMode.Hidden;
		column.Style.BackgroundColor = Color.Transparent;
		column.Style.PaddingTop = Length.Pixels( 8 );
		column.Style.PaddingRight = Length.Pixels( 8 );
		column.Style.PaddingBottom = Length.Pixels( 8 );
		column.Style.PaddingLeft = Length.Pixels( 8 );
		return column;
	}

	/// <inheritdoc cref="CreateMenuSectionWindow"/>
	public static Panel CreateInventoryColumnWindow( Panel parent, string extraClass, float flexWeight = 1f ) =>
		CreateMenuSectionWindow( parent, extraClass, flexWeight );

	/// <summary>Parchment column for in-world station overlays (storage, radio, research).</summary>
	public static Panel CreateStationColumn( Panel parent, string extraClass = "" )
	{
		var columnClass = string.IsNullOrWhiteSpace( extraClass )
			? "thorns-menu-column thorns-station-column"
			: $"thorns-menu-column thorns-station-column {extraClass}";
		var column = CreateMenuSectionWindow( parent, columnClass );
		column.Style.FlexDirection = FlexDirection.Column;
		column.Style.FlexGrow = 1;
		column.Style.FlexShrink = 1;
		ApplyMenuPanel( column );
		return column;
	}

	/// <summary>Shared overlay title row — wood-framed station header.</summary>
	public static Panel CreateStationOverlayHeader( Panel parent, out Label titleLabel, string title, Action onClose, string titleClass = "" )
	{
		var header = ThornsUiFactory.AddPanel( parent, "thorns-station-header" );
		header.Style.FlexDirection = FlexDirection.Row;
		header.Style.AlignItems = Align.Center;
		header.Style.JustifyContent = Justify.SpaceBetween;
		header.Style.MarginBottom = Length.Pixels( 8 );
		header.Style.PaddingBottom = Length.Pixels( 10 );
		header.Style.FlexShrink = 0;
		header.Style.Width = Length.Percent( 100 );

		var titleClassName = string.IsNullOrWhiteSpace( titleClass )
			? "thorns-header thorns-station-title"
			: $"thorns-header thorns-station-title {titleClass}";
		titleLabel = ThornsUiFactory.AddLabel( header, title, titleClassName );
		titleLabel.Style.FlexGrow = 1;
		ThornsUiFactory.AddClickable( header, "close thorns-station-close", "×", onClose );
		return header;
	}

	/// <summary>Vertical wood beam between menu columns (concept sectioning).</summary>
	public static Panel CreateWoodColumnDivider( Panel parent )
	{
		return ThornsUiFactory.AddPanel( parent, "menu-wood-divider" );
	}

	public static Label CreateHeader( Panel parent, string text, string extraClass = "" ) =>
		ThornsUiFactory.AddLabel( parent, text, string.IsNullOrEmpty( extraClass ) ? "thorns-header" : $"thorns-header {extraClass}" );

	/// <summary>Centered section title with ornamental rule (Classic fantasy menus).</summary>
	public static Panel CreateSectionHeader( Panel parent, string text, string extraClass = "" )
	{
		var wrap = ThornsUiFactory.AddPanel( parent, "thorns-section-header" );
		if ( !string.IsNullOrEmpty( extraClass ) )
			wrap.AddClass( extraClass );

		var rule = ThornsUiFactory.AddPanel( wrap, "thorns-section-header-rule" );
		ThornsUiFactory.AddPanel( rule, "thorns-section-header-line" );
		ThornsUiFactory.AddLabel( rule, text, "thorns-section-header-label" );
		ThornsUiFactory.AddPanel( rule, "thorns-section-header-line" );
		return wrap;
	}

	public static Label CreateMuted( Panel parent, string text ) =>
		ThornsUiFactory.AddLabel( parent, text, "thorns-muted" );
}
