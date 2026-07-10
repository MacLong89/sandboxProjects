namespace Terraingen.UI;

using Sandbox.UI;

/// <summary>Gameplay HUD visual tokens — glass, accents, typography. Logic-neutral styling only.</summary>
public static class ThornsHudTheme
{
	/// <summary>Shared width for minimap diameter and objectives / pinned-goal panel.</summary>
	public const int RightHudColumnWidthPx = 340;

	public const int RightHudColumnTopPx = 22;
	public const int RightHudColumnRightPx = 28;

	/// <summary>Vertical space between minimap and objectives panel.</summary>
	public const int RightHudColumnSectionGapPx = 22;

	/// <summary>Top-right alerts for airdrops, bloom seeds, guild expansion, and milestone moments.</summary>
	public const int WorldEventTopPx = 18;
	public const int WorldEventRightPx = 18;
	public const int WorldEventWidthPx = 320;

	/// <summary>Approximate minimap column height (time row + square map).</summary>
	public const int MinimapTimeRowPx = 42;

	/// <summary>Bottom hotbar + XP strip cluster scale (design baseline = 1).</summary>
	public const float HotbarHudScale = 0.8f;

	static int HotbarScaled( int designPx ) => Math.Max( 1, (int)MathF.Round( designPx * HotbarHudScale ) );

	/// <summary>Matches <see cref="ThornsHotbarHud"/> bottom offset.</summary>
	public static int HotbarBottomPx => HotbarScaled( 36 );

	/// <summary>Hotbar slots frame + XP strip (approximate).</summary>
	public static int HotbarStackHeightPx => HotbarScaled( 236 );

	public static int HotbarSlotPx => HotbarScaled( 116 );
	public static int HotbarRootWidthPx => HotbarScaled( 1200 );
	public static int HotbarXpWidthPx => HotbarScaled( 1196 );
	public static int HotbarMarginLeftPx => -HotbarScaled( 600 );
	public static int HotbarFramePaddingPx => HotbarScaled( 6 );
	public static int HotbarXpMarginTopPx => HotbarScaled( 10 );
	public static int HotbarXpTrackHeightPx => HotbarScaled( 3 );
	public static int HotbarXpValueMarginTopPx => HotbarScaled( 4 );
	public static int HotbarXpValueFontPx => HotbarScaled( 10 );
	public static int HotbarSlotKeyFontPx => HotbarScaled( 10 );
	public static int HotbarSlotKeyLeftPx => HotbarScaled( 4 );
	public static int HotbarSlotKeyTopPx => HotbarScaled( 2 );

	public static int HotbarScaledSlotInsetPx( int designPx ) => HotbarScaled( designPx );

	public const int HudPromptGapAboveHotbarPx = 16;

	/// <summary>Extra offset lifting the prompt toward screen center.</summary>
	public const int HudPromptLiftPx = 100;

	public const int HudPromptWidthPx = 300;

	public static int HudPromptBottomPx =>
		HotbarBottomPx + HotbarStackHeightPx + HudPromptGapAboveHotbarPx + HudPromptLiftPx;

	public const int LootFeedGapAboveHotbarPx = 14;
	public const int LootFeedMaxWidthPx = 360;

	public static int LootFeedBottomPx =>
		HotbarBottomPx + HotbarStackHeightPx + LootFeedGapAboveHotbarPx;

	public static Color GlassBg => ThornsUiSkinTokens.Active.GlassBg;
	public static Color GlassBorder => ThornsUiSkinTokens.Active.GlassBorder;
	public static Color Gold => ThornsUiSkinTokens.Active.Gold;
	public static Color TextWarm => ThornsUiSkinTokens.Active.TextWarm;
	public static Color TextMuted => ThornsUiSkinTokens.Active.TextMuted;

	public static Color HealthFill => ThornsUiSkinTokens.Active.HealthFill;
	public static Color ThirstFill => ThornsUiSkinTokens.Active.ThirstFill;
	public static Color HungerFill => ThornsUiSkinTokens.Active.HungerFill;
	public static Color StaminaFill => ThornsUiSkinTokens.Active.StaminaFill;
	public static Color XpFill => ThornsUiSkinTokens.Active.XpFill;

	/// <summary>Classic mockup HUD layout — top-left quest stack.</summary>
	public const int ClassicLeftColumnTopPx = 18;
	public const int ClassicLeftColumnLeftPx = 18;
	public const int ClassicLeftColumnWidthPx = 340;

	/// <summary>Classic mockup — bottom-left vitals cluster.</summary>
	public const int ClassicVitalsBottomPx = 24;
	public const int ClassicVitalsLeftPx = 18;

	/// <summary>Classic mockup — bottom-right minimap.</summary>
	public const int ClassicMinimapBottomPx = 24;
	public const int ClassicMinimapRightPx = 18;
	public static int ClassicMinimapSizePx => 200;

	/// <summary>Top-left vitals + level cluster scale (design baseline = 1).</summary>
	public const float VitalsHudScale = 0.8f;

	static int VitalsScaled( int designPx ) => Math.Max( 1, (int)MathF.Round( designPx * VitalsHudScale ) );

	/// <summary>Vertical gap between vitals bar rows (must match <c>.hud-vitals-bars</c> gap in SCSS).</summary>
	public static int VitalsBarGapPx => VitalsScaled( 12 );

	public static int VitalsBarShellPaddingVerticalPx => 0;

	/// <summary>Icon to the left of each vitals track (health / thirst / hunger).</summary>
	public static int VitalsBarIconPx => VitalsScaled( 32 );

	/// <summary>Shared track height for health, thirst, and hunger HUD bars.</summary>
	public static int VitalsBarTrackPx => VitalsScaled( 26 );

	public static int VitalsBarTrackLargePx => VitalsBarTrackPx;
	public static int VitalsBarTrackMediumPx => VitalsBarTrackPx;
	public static int VitalsBarTrackSmallPx => VitalsBarTrackPx;

	/// <summary>Classic concept HUD — thin stat bar strip inside the vitals panel.</summary>
	public static int ClassicVitalsBarTrackPx => VitalsScaled( 11 );

	public static int ClassicVitalsBarIconPx => VitalsScaled( 22 );

	public static int ClassicVitalsPanelWidthPx => VitalsScaled( 300 );

	public static int ClassicVitalsBarTrackMinWidthPx => 0;

	public static int ClassicVitalsBarValueMinWidthPx => VitalsScaled( 64 );

	public static int ClassicVitalsBarValueMarginLeftPx => VitalsScaled( 6 );

	public static int ClassicVitalsBarIconGapPx => VitalsScaled( 6 );

	public static int ClassicVitalsValueFontPx => VitalsScaled( 12 );

	public static int VitalsBarRowWidthPx => VitalsScaled( 544 );

	public static int VitalsBarRowLargeWidthPx => VitalsBarRowWidthPx;
	public static int VitalsBarRowMediumWidthPx => VitalsBarRowWidthPx;
	public static int VitalsBarRowSmallWidthPx => VitalsBarRowWidthPx;
	public static int VitalsBarTrackMinWidthPx => VitalsScaled( 200 );

	public static int VitalsBarRowGapPx => VitalsScaled( 10 );
	public static int VitalsBarIconGapPx => VitalsScaled( 10 );
	public static int VitalsBarValueMarginLeftPx => VitalsScaled( 12 );
	public static int VitalsBarValueMinWidthPx => VitalsScaled( 84 );

	public static int VitalsLevelWrapExtraWidthPx => VitalsScaled( 8 );
	public static int VitalsBarsMarginLeftPx => VitalsScaled( 12 );
	public static int VitalsLevelNumberFontPx => VitalsScaled( 20 );

	static int VitalsBarBlockPx( int trackPx ) => VitalsBarShellPaddingVerticalPx + trackPx;

	/// <summary>Single vitals row — icon column is taller than the track shell.</summary>
	public static int VitalsBarRowHeightPx =>
		Math.Max( VitalsBarIconPx, VitalsBarBlockPx( VitalsBarTrackPx ) );

	/// <summary>Total height of the three vitals tracks (top of health → bottom of hunger).</summary>
	public static int VitalsBarStackHeightPx =>
		VitalsBarRowHeightPx * 3 + VitalsBarGapPx * 2;

	/// <summary>Square side so a 45° diamond spans <see cref="VitalsBarStackHeightPx"/> vertically.</summary>
	public static int VitalsLevelDiamondSidePx =>
		(int)MathF.Round( VitalsBarStackHeightPx / MathF.Sqrt( 2f ) );

	public static int VitalsClusterLeftPx => 28;
	public static int VitalsClusterTopPx => 24;
	public static int LevelUpToastGapBelowVitalsPx => 16;

	/// <summary>Width of the level diamond + vitals bar column (level-up toast aligns to this).</summary>
	public static int VitalsClusterWidthPx =>
		VitalsLevelDiamondSidePx + VitalsLevelWrapExtraWidthPx + VitalsBarsMarginLeftPx + VitalsBarRowWidthPx;

	public static int LevelUpToastTopPx => VitalsClusterTopPx + VitalsBarStackHeightPx + LevelUpToastGapBelowVitalsPx;

	/// <summary>Approximate level-up card height — status toasts stack below this slot.</summary>
	public const int LevelUpToastEstimatedHeightPx = 56;

	public const int StatusToastGapBelowLevelUpPx = 8;

	/// <summary>Transient player feedback (craft, economy, warnings) — never on the minimap column.</summary>
	public static int StatusToastTopPx =>
		LevelUpToastTopPx + LevelUpToastEstimatedHeightPx + StatusToastGapBelowLevelUpPx;

	public static void ApplyLevelUpToastPanel( Panel panel )
	{
		if ( panel is null || !panel.IsValid )
			return;

		panel.AddClass( "level-up-toast" );
		ApplyObjectivesHudPanel( panel );
		panel.Style.PaddingTop = Length.Pixels( 7 );
		panel.Style.PaddingBottom = Length.Pixels( 7 );
		panel.Style.PaddingLeft = Length.Pixels( 12 );
		panel.Style.PaddingRight = Length.Pixels( 12 );
		panel.Style.MaxWidth = Length.Pixels( VitalsClusterWidthPx );
		panel.Style.PointerEvents = PointerEvents.None;
		panel.Style.FlexShrink = 0;
	}

	public static void ApplyHudGlass( Panel panel )
	{
		if ( panel is null || !panel.IsValid )
			return;

		panel.AddClass( "thorns-hud-glass" );

		if ( ThornsUiSkin.Active == ThornsUiSkinKind.Classic )
		{
			panel.AddClass( "hud-toast-classic" );
			ApplyHudWoodPanel( panel );
			return;
		}

		panel.Style.BackgroundColor = GlassBg;
		panel.Style.BorderColor = GlassBorder;
		panel.Style.BorderWidth = Length.Pixels( 1 );
	}

	public static void ApplyHudPromptCard( Panel panel )
	{
		if ( panel is null || !panel.IsValid )
			return;

		panel.AddClass( "hud-prompt-card" );
		panel.Style.Display = DisplayMode.Flex;
		panel.Style.FlexDirection = FlexDirection.Column;
		panel.Style.FlexShrink = 0;

		if ( ThornsUiSkin.Active == ThornsUiSkinKind.Classic )
		{
			panel.AddClass( "hud-prompt-classic" );
			ApplyHudWoodPanel( panel );
		}
		panel.Style.Position = PositionMode.Absolute;
		panel.Style.Bottom = Length.Pixels( HudPromptBottomPx );
		panel.Style.Left = Length.Percent( 50 );
		panel.Style.MarginLeft = Length.Pixels( -HudPromptWidthPx / 2 );
		panel.Style.Width = Length.Pixels( HudPromptWidthPx );
		panel.Style.FlexDirection = FlexDirection.Column;
		panel.Style.AlignItems = Align.Stretch;
		panel.Style.PaddingTop = Length.Pixels( 8 );
		panel.Style.PaddingBottom = Length.Pixels( 8 );
		panel.Style.PaddingLeft = Length.Pixels( 10 );
		panel.Style.PaddingRight = Length.Pixels( 10 );
		panel.Style.PointerEvents = PointerEvents.None;
		panel.Style.FlexShrink = 0;
	}

	public static void ApplyObjectivesHudPanel( Panel panel )
	{
		if ( panel is null || !panel.IsValid )
			return;

		panel.AddClass( "objectives-hud" );
		panel.Style.Display = DisplayMode.Flex;
		panel.Style.FlexDirection = FlexDirection.Column;
		panel.Style.FlexShrink = 0;

		if ( ThornsUiSkin.Active == ThornsUiSkinKind.Classic )
		{
			panel.AddClass( "hud-panel-classic" );
			ApplyHudWoodPanel( panel );
			return;
		}

		var p = ThornsUiSkinTokens.Active;
		panel.Style.BackgroundColor = p.ObjectivesBg;
		panel.Style.BorderColor = p.ObjectivesBorder;
		panel.Style.BorderWidth = Length.Pixels( 1 );
		panel.Style.PaddingTop = Length.Pixels( 16 );
		panel.Style.PaddingRight = Length.Pixels( 18 );
		panel.Style.PaddingBottom = Length.Pixels( 14 );
		panel.Style.PaddingLeft = Length.Pixels( 18 );
	}

	public static void ApplyClassicVitalsPanel( Panel panel )
	{
		ApplyHudWoodPanel( panel );
	}

	/// <summary>Opaque wood panel surface — same texture as the menu tab rail.</summary>
	public static void ApplyHudWoodPanel( Panel panel )
	{
		if ( panel is null || !panel.IsValid || ThornsUiSkin.Active != ThornsUiSkinKind.Classic )
			return;

		panel.AddClass( "hud-wood-panel" );
		ThornsMenuChrome.ApplyHudWoodSurface( panel );
	}

	/// <summary>Opaque wood slot surface for in-world HUD hotbar cells.</summary>
	public static void ApplyHudWoodSlot( Panel slot, bool selected = false )
	{
		if ( slot is null || !slot.IsValid || ThornsUiSkin.Active != ThornsUiSkinKind.Classic )
			return;

		slot.AddClass( "hud-wood-slot" );
		ThornsMenuChrome.ApplyHudWoodSurface( slot, slotChrome: true );

		if ( selected )
			slot.Style.BorderColor = Gold;
	}

	/// <summary>Charcoal-brown slot surface for menu inventory / hotbar cells.</summary>
	public static void ApplyCharcoalSlotChrome( Panel slot, bool selected = false )
	{
		if ( slot is null || !slot.IsValid )
			return;

		var p = ThornsUiSkinTokens.Active;
		slot.Style.BorderWidth = Length.Pixels( 1 );
		slot.Style.BackgroundImage = null;
		slot.Style.BorderImageSource = null;
		slot.Style.BorderTopWidth = Length.Pixels( 1 );
		slot.Style.BorderBottomWidth = Length.Pixels( 1 );
		slot.Style.BorderLeftWidth = Length.Pixels( 1 );
		slot.Style.BorderRightWidth = Length.Pixels( 1 );

		if ( selected )
		{
			slot.Style.BorderColor = Gold;
			slot.Style.BackgroundColor = new Color( 50f / 255f, 44f / 255f, 36f / 255f, 1f );
			return;
		}

		slot.Style.BackgroundColor = p.HotbarSlotBg;
		slot.Style.BorderColor = p.HotbarSlotBorder;
	}

	public static void ApplyHudBarTrack( Panel track )
	{
		if ( track is null || !track.IsValid )
			return;

		track.AddClass( "hud-bar-track" );

		var p = ThornsUiSkinTokens.Active;
		track.Style.BackgroundColor = p.HudBarTrackBg;
		track.Style.BorderColor = p.HudBarTrackBorder;
		track.Style.BorderWidth = Length.Pixels( 1 );
	}

	public static void ApplyHotbarSlotBase( Panel slot )
	{
		if ( slot is null || !slot.IsValid )
			return;

		slot.AddClass( "hotbar-slot" );
		slot.Style.Position = PositionMode.Relative;
		slot.Style.Overflow = OverflowMode.Hidden;

		if ( ThornsUiSkin.Active == ThornsUiSkinKind.Classic )
		{
			ApplyHudWoodSlot( slot );
			return;
		}

		var p = ThornsUiSkinTokens.Active;
		slot.Style.BackgroundColor = p.HotbarSlotBg;
		slot.Style.BorderColor = p.HotbarSlotBorder;
		slot.Style.BorderWidth = Length.Pixels( 1 );
	}

	public static void ApplyHotbarSlotSelected( Panel slot, bool selected )
	{
		if ( slot is null || !slot.IsValid )
			return;

		slot.SetClass( "selected", selected );

		if ( ThornsUiSkin.Active == ThornsUiSkinKind.Classic )
		{
			ApplyHudWoodSlot( slot, selected );
			return;
		}

		var p = ThornsUiSkinTokens.Active;

		if ( selected )
		{
			slot.Style.BorderColor = Gold;
			slot.Style.BorderWidth = Length.Pixels( 1 );
			slot.Style.BackgroundColor = new Color( 50f / 255f, 44f / 255f, 36f / 255f, 0.94f );
			return;
		}

		ApplyHotbarSlotBase( slot );
	}

	public static void ApplyHotbarFrame( Panel panel )
	{
		if ( panel is null || !panel.IsValid )
			return;

		var p = ThornsUiSkinTokens.Active;
		panel.AddClass( "hotbar-slots-frame" );
		panel.Style.Padding = Length.Pixels( HotbarFramePaddingPx );
		panel.Style.BackgroundColor = p.HotbarFrameBg;
		panel.Style.BorderColor = p.HotbarFrameBorder;
		panel.Style.BorderWidth = Length.Pixels( 1 );
	}

	public static void ApplyStatNumber( Label label )
	{
		if ( label is null || !label.IsValid )
			return;

		label.AddClass( "hud-stat-value" );
		label.Style.FontColor = TextWarm;
	}
}
