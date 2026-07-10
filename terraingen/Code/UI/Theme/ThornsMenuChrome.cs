namespace Terraingen.UI;

using Sandbox.UI;
using Terraingen;
using Terraingen.UI.Core;
using Terraingen.UI.Menu;

/// <summary>Menu overlay chrome — backdrop, 9-slice borders, and framed panels.</summary>
public static class ThornsMenuChrome
{
	public const string ChromeRoot = "ui/menu/chrome";

	public const string PanelFramePath = "ui/menu/chrome/frame_panel_9.png";
	public const string SectionFramePath = "ui/menu/chrome/frame_section_9.png";
	public const string CardFramePath = "ui/menu/chrome/frame_card_9.png";
	public const string SlotFramePath = "ui/menu/chrome/frame_slot_9.png";
	public const string ParchmentTilePath = "ui/menu/chrome/parchment_clean.png";
	public const string VineCornerPath = "ui/menu/chrome/vine_corner_tl.png";
	public const string TabRailPath = "ui/menu/chrome/tab_rail.png";

	/// <summary>Full-screen in-game menu backdrop (hand-authored parchment art).</summary>
	public const string BackdropPath = "ui/menu/chrome/menu_backdrop.png";
	public const string BackdropAltPath = "ui/menu/menu_backdrop.png";
	public const string BackdropMainMenuPath = ThornsMainMenuBackdrop.DefaultPath;
	public const string BorderFramePath = PanelFramePath;
	public const string ExplorerPortraitPath = "ui/menu/chrome/explorer_portrait.png";

	static bool _loggedFantasyKit;

	readonly struct FrameSliceSpec
	{
		public string Path { get; init; }
		public float SliceSourcePx { get; init; }
		public float RenderedBorderPx { get; init; }
		public float PaddingPx { get; init; }
	}

	static readonly Dictionary<string, Texture> TextureCache = new( StringComparer.OrdinalIgnoreCase );

	public static void WarmClassicTextures()
	{
		if ( ThornsUiSkin.Active != ThornsUiSkinKind.Classic )
			return;

		foreach ( var path in new[]
		         {
			         PanelFramePath,
			         SectionFramePath,
			         CardFramePath,
			         SlotFramePath,
			         ParchmentTilePath,
			         VineCornerPath,
			         TabRailPath,
			         BackdropPath,
			         BackdropAltPath
		         } )
			TryLoadTexture( path );
	}

	public enum MenuFrameKind
	{
		Panel,
		Section,
		Card,
		Slot,
		Compact
	}

	public static bool HasFantasyKit => HasTexture( PanelFramePath );

	public static void ApplyMenuOverlay( Panel overlay )
	{
		if ( overlay is null || !overlay.IsValid )
			return;

		switch ( ThornsUiSkin.Active )
		{
			case ThornsUiSkinKind.Survive:
				overlay.AddClass( "thorns-menu-overlay-survive" );
				overlay.Style.BackgroundColor = ThornsUiSkinTokens.Active.MenuOverlayBg;
				return;
			case ThornsUiSkinKind.Field:
				overlay.AddClass( "thorns-menu-overlay-field" );
				overlay.Style.BackgroundColor = ThornsUiSkinTokens.Active.MenuOverlayBg;
				return;
		}

		overlay.AddClass( "thorns-menu-overlay-fantasy thorns-menu-overlay-has-backdrop thorns-menu-overlay-fantasy-textured" );
		overlay.Style.Overflow = OverflowMode.Hidden;
		ThornsMainMenuBackdrop.ApplyTabMenuBackdrop( overlay );
	}

	static void EnsureMenuBackdropLayer( Panel overlay )
	{
		_ = overlay;
	}

	/// <summary>Full-bleed parchment backdrop for tab menu and modal overlays.</summary>
	public static bool ApplyMenuBackdropImage( Panel panel ) =>
		ThornsMainMenuBackdrop.ApplyTabMenuBackdrop( panel );

	static void ApplyBackdropSizing( Panel panel )
	{
		_ = panel;
	}

	/// <summary>Full-bleed parchment backdrop for tab menu and modal overlays.</summary>
	public static bool TryApplyMenuBackdrop( Panel panel ) => ApplyMenuBackdropImage( panel );

	/// <summary>Transparent menu body — parchment comes from the overlay backdrop.</summary>
	public static void ApplyMenuBodyParchment( Panel body )
	{
		if ( body is null || !body.IsValid )
			return;

		body.AddClass( "thorns-menu-body-parchment" );
		body.Style.BackgroundColor = Color.Transparent;
	}

	/// <summary>Outer wood border on fullscreen menu / overlay shell.</summary>
	public static void ApplyOuterFrame( Panel panel )
	{
		if ( panel is null || !panel.IsValid || ThornsUiSkin.Active != ThornsUiSkinKind.Classic )
			return;

		if ( ApplyNineSliceFrame( panel, new FrameSliceSpec
		     {
			     Path = "ui/menu/chrome/frame_outer_9slice.png",
			     SliceSourcePx = 128f,
			     RenderedBorderPx = 36f,
			     PaddingPx = 0f
		     } ) )
		{
			panel.AddClass( "thorns-menu-outer-framed" );
			panel.Style.BackgroundColor = new Color( 12f / 255f, 9f / 255f, 6f / 255f, 1f );
		}
	}

	/// <summary>Shared parchment shell for world overlays and modals.</summary>
	public static (Panel Backdrop, Panel Root) CreateOverlayShell( Panel parent, string rootClass, int maxWidthPx = 920 )
	{
		var backdrop = CreateOverlayBackdrop( parent );

		var root = ThornsUiFactory.AddPanel( backdrop, rootClass );
		ApplyWoodPanel( root );
		ApplyStationParchmentFill( root );
		root.AddClass( "thorns-overlay-shell" );
		root.Style.Width = Length.Pixels( maxWidthPx );
		root.Style.MaxWidth = Length.Percent( 94 );
		root.Style.MaxHeight = Length.Percent( 90 );
		root.Style.FlexDirection = FlexDirection.Column;
		root.Style.PaddingTop = Length.Pixels( 12 );
		root.Style.PaddingRight = Length.Pixels( 14 );
		root.Style.PaddingBottom = Length.Pixels( 12 );
		root.Style.PaddingLeft = Length.Pixels( 14 );
		root.Style.PointerEvents = PointerEvents.All;
		root.Style.Overflow = OverflowMode.Hidden;
		root.Style.FlexShrink = 0;
		root.Style.MinHeight = Length.Pixels( 0 );
		return (backdrop, root);
	}

	/// <summary>Full-screen wood-framed overlay for research and large station UIs.</summary>
	public static (Panel Backdrop, Panel Root) CreateFullscreenOverlayShell( Panel parent, string rootClass )
	{
		var backdrop = CreateOverlayBackdrop( parent );
		backdrop.Style.JustifyContent = Justify.FlexStart;
		backdrop.Style.AlignItems = Align.Stretch;

		var root = ThornsUiFactory.AddPanel( backdrop, rootClass );
		ApplyWoodPanel( root );
		ApplyStationParchmentFill( root );
		root.AddClass( "thorns-overlay-shell thorns-station-shell-full" );
		root.Style.FlexDirection = FlexDirection.Column;
		root.Style.FlexGrow = 1;
		root.Style.MinHeight = Length.Pixels( 0 );
		root.Style.MinWidth = Length.Pixels( 0 );
		root.Style.MarginLeft = Length.Pixels( ThornsUiMetrics.MenuScreenSideInset );
		root.Style.MarginRight = Length.Pixels( ThornsUiMetrics.MenuScreenSideInset );
		root.Style.MarginTop = Length.Pixels( ThornsUiMetrics.MenuScreenEdgeInset );
		root.Style.MarginBottom = Length.Pixels( ThornsUiMetrics.MenuScreenEdgeInset );
		root.Style.PaddingTop = Length.Pixels( 14 );
		root.Style.PaddingRight = Length.Pixels( 16 );
		root.Style.PaddingBottom = Length.Pixels( 14 );
		root.Style.PaddingLeft = Length.Pixels( 16 );
		root.Style.PointerEvents = PointerEvents.All;
		root.Style.Overflow = OverflowMode.Hidden;
		return (backdrop, root);
	}

	static Panel CreateOverlayBackdrop( Panel parent )
	{
		var backdrop = ThornsUiFactory.AddPanel( parent, "thorns-overlay-backdrop thorns-station-overlay thorns-station-overlay-dim" );
		backdrop.Style.Position = PositionMode.Absolute;
		backdrop.Style.Left = Length.Pixels( 0 );
		backdrop.Style.Top = Length.Pixels( 0 );
		backdrop.Style.Width = Length.Percent( 100 );
		backdrop.Style.Height = Length.Percent( 100 );
		backdrop.Style.FlexDirection = FlexDirection.Column;
		backdrop.Style.JustifyContent = Justify.Center;
		backdrop.Style.AlignItems = Align.Center;
		backdrop.Style.PointerEvents = PointerEvents.All;
		ApplyStationOverlayBackdrop( backdrop );
		return backdrop;
	}

	/// <summary>Dim world backdrop for in-world station overlays — parchment lives inside the wood frame.</summary>
	public static void ApplyStationOverlayBackdrop( Panel backdrop )
	{
		if ( backdrop is null || !backdrop.IsValid )
			return;

		backdrop.Style.BackgroundImage = null;

		switch ( ThornsUiSkin.Active )
		{
			case ThornsUiSkinKind.Survive:
				backdrop.Style.BackgroundColor = new Color( 0.02f, 0.03f, 0.05f, 0.72f );
				return;
			case ThornsUiSkinKind.Field:
				backdrop.Style.BackgroundColor = new Color( 0.03f, 0.04f, 0.06f, 0.68f );
				return;
			default:
				backdrop.Style.BackgroundColor = new Color( 4f / 255f, 6f / 255f, 10f / 255f, 0.62f );
				break;
		}
	}

	/// <summary>Shrunk repeating parchment tile inside wood-framed station panels.</summary>
	public static void ApplyStationParchmentFill( Panel panel )
	{
		if ( panel is null || !panel.IsValid || ThornsUiSkin.Active != ThornsUiSkinKind.Classic )
			return;

		panel.AddClass( "thorns-station-parchment-fill" );
		panel.Style.BackgroundColor = new Color( 227f / 255f, 217f / 255f, 198f / 255f, 1f );
		TryApplyBackgroundImage( panel, ParchmentTilePath );
	}

	public static void ApplyWoodPanel( Panel panel ) => ApplyFrame( panel, MenuFrameKind.Panel );

	public static void ApplyMenuSlot( Panel panel ) => ApplyFrame( panel, MenuFrameKind.Slot );

	public static void ApplyMenuSubFrame( Panel panel ) => ApplyFrame( panel, MenuFrameKind.Section );

	/// <summary>Compact framed panel for in-world HUD clusters (no vine overlays).</summary>
	public static void ApplyHudCardFrame( Panel panel ) => ApplyFrame( panel, MenuFrameKind.Card );

	public static void ApplyMenuTopBar( Panel panel )
	{
		if ( panel is null || !panel.IsValid )
			return;

		if ( ThornsUiSkin.Active != ThornsUiSkinKind.Classic )
			return;

		panel.AddClass( "thorns-menu-topbar-fantasy" );

		if ( !HasFantasyKit )
			return;

		panel.AddClass( "thorns-menu-topbar-textured" );
		panel.Style.BackgroundColor = Color.Transparent;
		TryApplyBackgroundImage( panel, TabRailPath );
	}

	/// <summary>Opaque charcoal HUD surface — solid fill only (no tab-rail stretch seam).</summary>
	public static void ApplyHudWoodSurface( Panel panel, bool slotChrome = false )
	{
		if ( panel is null || !panel.IsValid || ThornsUiSkin.Active != ThornsUiSkinKind.Classic )
			return;

		var p = ThornsUiSkinTokens.Active;
		panel.Style.BackgroundColor = new Color( 43f / 255f, 38f / 255f, 34f / 255f, 1f );
		panel.Style.BackgroundImage = null;
		panel.Style.BorderColor = slotChrome ? p.HotbarSlotBorder : new Color( 72f / 255f, 66f / 255f, 58f / 255f, 1f );
		panel.Style.BorderWidth = Length.Pixels( 1 );
		panel.Style.BorderTopWidth = Length.Pixels( 1 );
		panel.Style.BorderBottomWidth = Length.Pixels( 1 );
		panel.Style.BorderLeftWidth = Length.Pixels( 1 );
		panel.Style.BorderRightWidth = Length.Pixels( 1 );
		panel.Style.BorderImageSource = null;
	}

	public static void ApplyFrame( Panel panel, MenuFrameKind kind )
	{
		if ( panel is null || !panel.IsValid )
			return;

		switch ( ThornsUiSkin.Active )
		{
			case ThornsUiSkinKind.Survive:
				panel.AddClass( "thorns-survive-panel" );
				panel.AddClass( kind switch
				{
					MenuFrameKind.Section => "thorns-survive-section",
					MenuFrameKind.Card => "thorns-survive-card",
					MenuFrameKind.Slot => "thorns-survive-slot",
					MenuFrameKind.Compact => "thorns-survive-compact",
					_ => "thorns-survive-frame"
				} );
				return;
			case ThornsUiSkinKind.Field:
				panel.AddClass( "thorns-field-panel" );
				panel.AddClass( kind switch
				{
					MenuFrameKind.Section => "thorns-field-section",
					MenuFrameKind.Card => "thorns-field-card",
					MenuFrameKind.Slot => "thorns-field-slot",
					MenuFrameKind.Compact => "thorns-field-compact",
					_ => "thorns-field-frame"
				} );
				return;
		}

		panel.AddClass( "thorns-menu-framed" );
		panel.AddClass( kind switch
		{
			MenuFrameKind.Section => "thorns-menu-frame-section",
			MenuFrameKind.Card => "thorns-menu-frame-card",
			MenuFrameKind.Slot => "thorns-menu-frame-slot",
			MenuFrameKind.Compact => "thorns-menu-frame-compact",
			_ => "thorns-menu-frame-panel"
		} );

		if ( !ApplyNineSliceFrame( panel, FrameSpec( kind ) ) )
			return;

		panel.AddClass( "thorns-menu-framed-textured" );
		panel.Style.Position = PositionMode.Relative;
		panel.Style.Overflow = OverflowMode.Visible;

		if ( kind is MenuFrameKind.Panel or MenuFrameKind.Section )
			AttachVineOverlays( panel );

		LogFantasyKitOnce();
	}

	/// <summary>Wood section window for menu columns — frame with optional vine corners.</summary>
	public static void ApplyMenuSectionWindow( Panel shell )
	{
		if ( shell is null || !shell.IsValid || ThornsUiSkin.Active != ThornsUiSkinKind.Classic )
			return;

		shell.AddClass( "thorns-menu-framed thorns-menu-frame-section thorns-menu-section-window" );

		if ( ApplyNineSliceFrame( shell, new FrameSliceSpec
		     {
			     Path = SectionFramePath,
			     SliceSourcePx = 96f,
			     RenderedBorderPx = 16f,
			     PaddingPx = 0f
		     } ) )
		{
			shell.AddClass( "thorns-menu-framed-textured" );
			shell.Style.BackgroundColor = Color.Transparent;
		}

		shell.Style.Position = PositionMode.Relative;
		shell.Style.Overflow = OverflowMode.Visible;
		AttachVineOverlays( shell );
		LogFantasyKitOnce();
	}

	public static string FramePath( MenuFrameKind kind ) => FrameSpec( kind ).Path;

	static FrameSliceSpec FrameSpec( MenuFrameKind kind ) => kind switch
	{
		MenuFrameKind.Section => new FrameSliceSpec
		{
			Path = SectionFramePath,
			SliceSourcePx = 96f,
			RenderedBorderPx = 24f,
			PaddingPx = 14f
		},
		MenuFrameKind.Card => new FrameSliceSpec
		{
			Path = CardFramePath,
			SliceSourcePx = 72f,
			RenderedBorderPx = 18f,
			PaddingPx = 10f
		},
		MenuFrameKind.Slot => new FrameSliceSpec
		{
			Path = SlotFramePath,
			SliceSourcePx = 48f,
			RenderedBorderPx = 10f,
			PaddingPx = 4f
		},
		MenuFrameKind.Compact => new FrameSliceSpec
		{
			Path = CardFramePath,
			SliceSourcePx = 72f,
			RenderedBorderPx = 22f,
			PaddingPx = 10f
		},
		_ => new FrameSliceSpec
		{
			Path = PanelFramePath,
			SliceSourcePx = 128f,
			RenderedBorderPx = 30f,
			PaddingPx = 16f
		}
	};

	static bool ApplyNineSliceFrame( Panel panel, FrameSliceSpec spec )
	{
		var frameTex = TryLoadTexture( spec.Path );
		if ( frameTex is not { IsValid: true } )
			return false;

		var slice = Length.Pixels( spec.SliceSourcePx );
		var border = Length.Pixels( spec.RenderedBorderPx );
		var pad = Length.Pixels( spec.PaddingPx );

		panel.Style.BorderImageSource = frameTex;
		panel.Style.BorderImageWidthTop = slice;
		panel.Style.BorderImageWidthBottom = slice;
		panel.Style.BorderImageWidthLeft = slice;
		panel.Style.BorderImageWidthRight = slice;
		panel.Style.BorderTopWidth = border;
		panel.Style.BorderBottomWidth = border;
		panel.Style.BorderLeftWidth = border;
		panel.Style.BorderRightWidth = border;
		panel.Style.BorderImageFill = BorderImageFill.Unfilled;
		panel.Style.BorderImageRepeat = BorderImageRepeat.Stretch;
		panel.Style.PaddingTop = pad;
		panel.Style.PaddingBottom = pad;
		panel.Style.PaddingLeft = pad;
		panel.Style.PaddingRight = pad;
		panel.Style.BackgroundColor = ThornsUiSkinTokens.Active.OpaquePanelBg;

		return true;
	}

	static void AttachVineOverlays( Panel panel )
	{
		if ( panel is null || !panel.IsValid || !HasTexture( VineCornerPath ) )
			return;

		if ( panel.HasClass( "thorns-frame-vines-attached" ) )
			return;

		panel.AddClass( "thorns-frame-vines-attached" );
		var vineSize = panel.HasClass( "thorns-menu-section-window" ) ? 72 : 110;
		AddVineCorner( panel, "tl", vineSize );
		AddVineCorner( panel, "tr", vineSize );
		AddVineCorner( panel, "bl", vineSize );
		AddVineCorner( panel, "br", vineSize );
	}

	static void AddVineCorner( Panel parent, string corner, int sizePx = 110 )
	{
		var vine = ThornsUiFactory.AddPanel( parent, $"thorns-frame-vine thorns-frame-vine-{corner}" );
		vine.Style.Position = PositionMode.Absolute;
		vine.Style.PointerEvents = PointerEvents.None;
		vine.Style.Width = Length.Pixels( sizePx );
		vine.Style.Height = Length.Pixels( sizePx );
		vine.Style.ZIndex = 5;

		var inset = sizePx <= 72 ? -4 : -10;

		if ( corner.StartsWith( 't' ) )
			vine.Style.Top = Length.Pixels( inset );
		else
			vine.Style.Bottom = Length.Pixels( inset );

		if ( corner.EndsWith( 'l' ) )
			vine.Style.Left = Length.Pixels( inset );
		else
			vine.Style.Right = Length.Pixels( inset );

		TryApplyBackgroundImage( vine, VineCornerPath );
	}

	static void LogFantasyKitOnce()
	{
		if ( _loggedFantasyKit || !HasFantasyKit )
			return;

		_loggedFantasyKit = true;
		Log.Info( $"[Thorns UI] Classic fantasy chrome active ({ChromeRoot})." );
	}

	static bool HasTexture( string path ) => TryLoadTexture( path ) is { IsValid: true };

	static bool TryApplyBackgroundImage( Panel panel, string path )
	{
		if ( panel is null || !panel.IsValid || string.IsNullOrWhiteSpace( path ) )
			return false;

		foreach ( var attempt in PathAttempts( path ) )
		{
			try
			{
				panel.Style.SetBackgroundImage( attempt );
				return true;
			}
			catch ( Exception e )
			{
				Log.Warning( e, $"[Thorns UI] SetBackgroundImage failed for '{attempt}'." );
			}

			var tex = TryLoadTexture( attempt );
			if ( tex is { IsValid: true } )
			{
				try
				{
					panel.Style.BackgroundImage = tex;
					return true;
				}
				catch ( Exception e )
				{
					Log.Warning( e, $"[Thorns UI] BackgroundImage assignment failed for '{attempt}'." );
				}
			}
		}

		return false;
	}

	static Texture TryLoadTexture( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return default;

		foreach ( var attempt in PathAttempts( path ) )
		{
			if ( TextureCache.TryGetValue( attempt, out var cached ) && cached is not null && cached.IsValid )
				return cached;

			try
			{
				var tex = Texture.Load( attempt );
				if ( tex is not null && tex.IsValid )
				{
					TextureCache[attempt] = tex;
					return tex;
				}
			}
			catch ( Exception e )
			{
				Log.Warning( e, $"[Thorns UI] Menu chrome load failed for '{attempt}'." );
			}
		}

		return default;
	}

	static IEnumerable<string> PathAttempts( string path ) => ThornsContentPath.Candidates( path );
}
