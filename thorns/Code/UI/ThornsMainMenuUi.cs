#nullable disable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sandbox.Network;
using Sandbox.UI;

namespace Sandbox;

/// <summary>Boot / main menu: PLAY → mode list, server browser (live lobbies), settings stub, exit.</summary>
[Title( "Thorns — Main Menu UI" )]
[Category( "Thorns/UI" )]
[Icon( "menu" )]
[Order( 5 )]
public sealed class ThornsMainMenuUI : PanelComponent
{
	/// <summary>Preferred gameplay scene (large POI-authored layout).</summary>
	public const string GameplayScenePath = "scenes/thorns_procedural.scene";

	/// <summary>Flat gallery for tuning <see cref="ThornsPlaceableFurnitureCatalog.Entry.WorldSizeInches"/> in the inspector.</summary>
	public const string FurnitureGalleryScenePath = "scenes/thorns_furniture_gallery.scene";

	/// <summary>
	/// Procedural gameplay scene — also tries <c>Assets/scenes/...</c> when mounts expose raw paths differently than <c>scenes/...</c>.
	/// </summary>
	static readonly string[] GameplayScenePathCandidates =
	{
		"scenes/thorns_procedural.scene",
		"Assets/scenes/thorns_procedural.scene",
		"scenes/thorns_terrain_boot.scene",
		"Assets/scenes/thorns_terrain_boot.scene",
		"scenes/thorns_flat.scene",
		"Assets/scenes/thorns_flat.scene",
	};

	enum MenuLayer
	{
		Root,
		PlayMenu,
		Browser,
		Settings
	}

	Panel _rootLayer;
	Panel _playLayer;
	Panel _browserLayer;
	Panel _settingsLayer;
	Panel _artLayer;
	Panel _bodyStack;
	Panel _backdropLayer;
	Panel _heroImagePanel;
	Panel _grainOverlay;
	Label _flavorTagline;
	Label _rotatingTip;
	Texture _grainTexture;
	Texture _logoTexture;
	int _taglineIndex;
	int _tipIndex;
	float _nextTaglineSwap;
	float _nextTipSwap;

	Panel _serverListHost;
	Panel _myServersListHost;
	Panel _detailPreview;
	Label _detailTitle;
	Label _detailSub;
	Label _detailSeed;
	Label _detailPing;
	Panel _joinBtn;
	Panel _startLocalWorldBtn;
	Panel _wipeLocalWorldBtn;
	Panel _removeLocalServerBtn;

	readonly List<Panel> _serverRowPanels = new();
	readonly List<Panel> _myServerRowPanels = new();
	List<LobbyInformation> _lobbies = new();
	int _selectedLobbyIndex = -1;
	List<ThornsLocalWorldSaves.Entry> _localSaves = new();
	string _selectedLocalRelativePath;

	Panel _wipeWorldModal;
	Label _wipeWorldErr;
	string _wipeWorldPendingPath;

	Panel _deleteServerModal;
	Label _deleteServerErr;
	string _deleteServerPendingPath;

	Panel _hostLocalModal;
	Label _hostLocalErr;
	TextEntry _hostLocalServerName;
	TextEntry _hostLocalPwd;
	Panel _hostVisPublic;
	Panel _hostVisPrivate;
	bool _hostLocalWantPassword;

	Panel _joinPwdModal;
	TextEntry _joinPwdEntry;
	Label _joinPwdErr;
	LobbyInformation _joinPwdLobby;

	bool _treeReady;

	protected override void OnStart()
	{
		if ( Scene.IsEditor && !Game.IsPlaying )
			return;

		TryBuildTree();
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying )
			return;

		if ( !_treeReady )
		{
			TryBuildTree();
			return;
		}

		TickMenuPresentation();
	}

	void TickMenuPresentation()
	{
		if ( _heroImagePanel.IsValid() && ThornsMainMenuHeroArt.HasSlides )
		{
			var slide = ThornsMainMenuHeroArt.CurrentSlide;
			if ( slide is not null && !slide.IsError )
			{
				_heroImagePanel.Style.BackgroundImage = slide;
				if ( _detailPreview.IsValid() )
					_detailPreview.Style.BackgroundImage = slide;
			}
		}

		if ( Time.Now >= _nextTaglineSwap && _flavorTagline.IsValid() )
		{
			_nextTaglineSwap = Time.Now + 7.5f;
			var tags = ThornsMainMenuPresentation.RotatingTaglines;
			if ( tags.Length > 0 )
			{
				_taglineIndex = ( _taglineIndex + 1 ) % tags.Length;
				_flavorTagline.Text = tags[_taglineIndex];
			}
		}

		if ( Time.Now >= _nextTipSwap && _rotatingTip.IsValid() )
		{
			_nextTipSwap = Time.Now + 11f;
			var tips = ThornsMainMenuPresentation.RotatingTips;
			if ( tips.Length > 0 )
			{
				_tipIndex = ( _tipIndex + 1 ) % tips.Length;
				_rotatingTip.Text = tips[_tipIndex];
			}
		}
	}

	/// <summary>Wrapped modal copy — constrain width via wrap panel; px line-height in SCSS.</summary>
	static void StyleModalParagraph( Label lbl, int marginBottomPx = 10 )
	{
		if ( lbl is null || !lbl.IsValid )
			return;

		lbl.Style.WhiteSpace = WhiteSpace.Normal;
		lbl.Style.FlexShrink = 0;
		lbl.Style.FlexGrow = 0;
		lbl.Style.MinWidth = 0;
		lbl.Style.MaxWidth = Length.Percent( 100 );
		lbl.Style.Width = Length.Percent( 100 );
		lbl.Style.PaddingTop = Length.Pixels( 3 );
		lbl.Style.PaddingBottom = Length.Pixels( 2 );
		lbl.Style.MarginBottom = Length.Pixels( marginBottomPx );
		lbl.Style.Overflow = OverflowMode.Visible;
	}

	static Panel StyleModalTextWrap( Panel wrap )
	{
		wrap.Style.FlexDirection = FlexDirection.Column;
		wrap.Style.FlexShrink = 0;
		wrap.Style.FlexGrow = 0;
		wrap.Style.MinWidth = 0;
		wrap.Style.MaxWidth = Length.Percent( 100 );
		wrap.Style.Width = Length.Percent( 100 );
		wrap.Style.Overflow = OverflowMode.Visible;
		return wrap;
	}

	static Label AddModalParagraph( Panel parent, string text, string styleClass = "thorns-mm-modal-body", int marginBottomPx = 10 )
	{
		var wrap = StyleModalTextWrap( ThornsUiPanelAdd.AddChildPanel( parent, "thorns-mm-modal-text-wrap" ) );
		var lbl = wrap.AddChild( new Label( text, styleClass ) );
		StyleModalParagraph( lbl, marginBottomPx );
		return lbl;
	}

	static void StyleHostModalFieldCap( Label lbl )
	{
		if ( lbl is null || !lbl.IsValid )
			return;

		lbl.Style.FlexShrink = 0;
		lbl.Style.MinWidth = 0;
		lbl.Style.MaxWidth = Length.Percent( 100 );
		lbl.Style.Width = Length.Percent( 100 );
		lbl.Style.PaddingTop = Length.Pixels( 2 );
		lbl.Style.Overflow = OverflowMode.Visible;
	}

	void TryBuildTree()
	{
		if ( _treeReady )
			return;

		EnsureMainMenuRenderCamera();

		if ( !Components.Get<ScreenPanel>( FindMode.EnabledInSelf ).IsValid() )
			_ = Components.Create<ScreenPanel>();

		var sp = Components.Get<ScreenPanel>( FindMode.EnabledInSelf );
		if ( sp.IsValid() )
		{
			sp.AutoScreenScale = true;
			sp.ZIndex = 200;
			var mainCam = TryFindEnabledMainCamera();
			if ( mainCam.IsValid() )
				sp.TargetCamera = mainCam;
		}

		if ( Panel is null || !Panel.IsValid )
			return;

		Panel.AddClass( "thorns-mm-root" );
		Panel.Style.Width = Length.Fraction( 1f );
		Panel.Style.Height = Length.Fraction( 1f );
		Panel.Style.PointerEvents = PointerEvents.All;
		Panel.Style.Position = PositionMode.Relative;

		BuildBackdropLayer( Panel );

		var shell = ThornsUiPanelAdd.AddChildPanel( Panel, "thorns-mm-shell" );
		shell.Style.Position = PositionMode.Relative;
		shell.Style.ZIndex = 2;
		shell.Style.FlexDirection = FlexDirection.Row;
		shell.Style.Width = Length.Fraction( 1f );
		shell.Style.Height = Length.Fraction( 1f );

		var leftRail = ThornsUiPanelAdd.AddChildPanel( shell, "thorns-mm-rail" );
		leftRail.Style.FlexDirection = FlexDirection.Column;
		leftRail.Style.FlexShrink = 0;
		leftRail.Style.Width = Length.Pixels( 320 );

		leftRail.AddChild( new Label( "MAIN MENU", "thorns-mm-kicker" ) );
		var kickerLine = leftRail.AddChild( new Panel() );
		kickerLine.AddClass( "thorns-mm-kicker-line" );

		_rootLayer = ThornsUiPanelAdd.AddChildPanel( leftRail, "thorns-mm-layer thorns-mm-layer--root" );
		_rootLayer.Style.FlexDirection = FlexDirection.Column;
		_rootLayer.Style.MarginTop = Length.Pixels( 18 );
		AddMenuButton( _rootLayer, "PLAY", true, () => ShowLayer( MenuLayer.PlayMenu ) );
		AddMenuButton( _rootLayer, "EXIT", false, () => ExitGame() );

		_playLayer = ThornsUiPanelAdd.AddChildPanel( leftRail, "thorns-mm-layer thorns-mm-layer--play thorns-mm-hidden" );
		_playLayer.Style.FlexDirection = FlexDirection.Column;
		_playLayer.Style.MarginTop = Length.Pixels( 18 );
		AddStoryModeComingSoonMenuRow( _playLayer );
		AddMenuButton( _playLayer, "MULTIPLAYER BROWSER", false, () => ShowLayer( MenuLayer.Browser ) );
		AddMenuButton( _playLayer, "SETTINGS", false, () => ShowLayer( MenuLayer.Settings ) );
		AddMenuButton( _playLayer, "BACK", false, () => ShowLayer( MenuLayer.Root ) );

		_bodyStack = ThornsUiPanelAdd.AddChildPanel( shell, "thorns-mm-bodystack" );
		_bodyStack.Style.FlexDirection = FlexDirection.Column;
		_bodyStack.Style.FlexGrow = 1;
		_bodyStack.Style.MinWidth = 0;
		_bodyStack.Style.MinHeight = 0;

		_artLayer = BuildHeroArtPanel( _bodyStack );

		BuildHostLocalSaveModal( Panel );
		BuildJoinPasswordModal( Panel );
		BuildWipeWorldModal( Panel );
		BuildDeleteServerModal( Panel );

		_browserLayer = BuildServerBrowser( _bodyStack );
		_settingsLayer = BuildSettingsPanel( _bodyStack );

		ThornsMainMenuHeroArt.EnsureLoaded( ThornsMainMenuPresentation.DefaultHeroTexturePaths );
		ApplyHeroSlideToPanels();
		_nextTaglineSwap = Time.Now + 1f;
		_nextTipSwap = Time.Now + 4f;

		_treeReady = true;
		Log.Info( "[Thorns] Main menu UI ready." );
	}

	void BuildBackdropLayer( Panel root )
	{
		_backdropLayer = ThornsUiPanelAdd.AddChildPanel( root, "thorns-mm-backdrop" );
		_backdropLayer.Style.Position = PositionMode.Absolute;
		_backdropLayer.Style.Left = 0;
		_backdropLayer.Style.Top = 0;
		_backdropLayer.Style.Right = 0;
		_backdropLayer.Style.Bottom = 0;
		_backdropLayer.Style.ZIndex = 0;
		_backdropLayer.Style.PointerEvents = PointerEvents.None;

		_heroImagePanel = ThornsUiPanelAdd.AddChildPanel( _backdropLayer, "thorns-mm-hero-image" );

		_grainTexture = ThornsMainMenuHeroArt.TryLoadOptional( ThornsMainMenuPresentation.GrainTexturePath );
		if ( _grainTexture is not null && !_grainTexture.IsError )
		{
			_grainOverlay = ThornsUiPanelAdd.AddChildPanel( _backdropLayer, "thorns-mm-grain" );
			_grainOverlay.Style.BackgroundImage = _grainTexture;
		}

	}

	Panel BuildHeroArtPanel( Panel parent )
	{
		var art = ThornsUiPanelAdd.AddChildPanel( parent, "thorns-mm-art" );
		art.Style.FlexGrow = 1;
		art.Style.MinHeight = 0;
		art.Style.Position = PositionMode.Relative;

		var stack = ThornsUiPanelAdd.AddChildPanel( art, "thorns-mm-art-stack" );

		_logoTexture = ThornsMainMenuHeroArt.TryLoadOptional( ThornsMainMenuPresentation.LogoTexturePath );
		if ( _logoTexture is not null && !_logoTexture.IsError )
		{
			var logo = ThornsUiPanelAdd.AddChildPanel( stack, "thorns-mm-logo" );
			logo.Style.BackgroundImage = _logoTexture;
		}

		stack.AddChild( new Label( "THORNS", "thorns-mm-brand" ) );

		var tags = ThornsMainMenuPresentation.RotatingTaglines;
		_flavorTagline = stack.AddChild( new Label(
			tags.Length > 0 ? tags[0] : "Survive the bloom.",
			"thorns-mm-art-tag" ) );

		var tips = ThornsMainMenuPresentation.RotatingTips;
		_rotatingTip = stack.AddChild( new Label(
			tips.Length > 0 ? tips[0] : "",
			"thorns-mm-art-tip" ) );

		return art;
	}

	void ApplyHeroSlideToPanels()
	{
		if ( !_heroImagePanel.IsValid() )
			return;

		var slide = ThornsMainMenuHeroArt.CurrentSlide;
		if ( slide is null || slide.IsError )
			return;

		_heroImagePanel.Style.BackgroundImage = slide;
		_heroImagePanel.SetClass( "thorns-mm-hero-image--ready", true );

		if ( _detailPreview.IsValid() )
		{
			_detailPreview.Style.BackgroundImage = slide;
			_detailPreview.SetClass( "thorns-mm-detail-preview--hero", true );
		}
	}

	void BuildHostLocalSaveModal( Panel root )
	{
		_hostLocalModal = ThornsUiPanelAdd.AddChildPanel( root, "thorns-mm-modal-backdrop thorns-mm-hidden" );
		_hostLocalModal.Style.Position = PositionMode.Absolute;
		_hostLocalModal.Style.Left = 0;
		_hostLocalModal.Style.Top = 0;
		_hostLocalModal.Style.Right = 0;
		_hostLocalModal.Style.Bottom = 0;
		_hostLocalModal.Style.ZIndex = 500;
		_hostLocalModal.Style.JustifyContent = Justify.FlexStart;
		_hostLocalModal.Style.AlignItems = Align.Center;
		_hostLocalModal.Style.Overflow = OverflowMode.Scroll;
		_hostLocalModal.Style.PaddingTop = Length.Pixels( 36 );
		_hostLocalModal.Style.PaddingBottom = Length.Pixels( 48 );
		_hostLocalModal.Style.PaddingLeft = Length.Pixels( 12 );
		_hostLocalModal.Style.PaddingRight = Length.Pixels( 12 );
		_hostLocalModal.Style.PointerEvents = PointerEvents.All;

		var card = ThornsUiPanelAdd.AddChildPanel( _hostLocalModal, "thorns-mm-modal-card thorns-mm-host-modal-card" );
		card.Style.FlexDirection = FlexDirection.Column;
		card.Style.AlignItems = Align.Stretch;
		card.Style.Width = Length.Pixels( 460 );
		card.Style.MaxWidth = Length.Pixels( 460 );
		card.Style.MinWidth = Length.Pixels( 320 );
		card.Style.Padding = Length.Pixels( 24 );
		card.Style.FlexShrink = 0;
		card.Style.Overflow = OverflowMode.Visible;

		var titleLbl = card.AddChild( new Label( "HOST A SERVER", "thorns-mm-modal-title" ) );
		StyleHostModalFieldCap( titleLbl );
		AddModalParagraph(
			card,
			"Public: anyone can join. Private: listed in the browser but joiners must enter your password first. Your world still saves on the host.",
			marginBottomPx: 16 );

		StyleHostModalFieldCap( card.AddChild( new Label( "Server name", "thorns-mm-modal-fieldcap" ) ) );
		AddModalParagraph(
			card,
			"Shown in the lobby list. The same name loads the same saved world on this machine.",
			marginBottomPx: 8 );
		_hostLocalServerName = card.AddChild( new TextEntry() );
		_hostLocalServerName.Placeholder = "e.g. My Friends Server";
		_hostLocalServerName.Style.MarginBottom = Length.Pixels( 14 );
		_hostLocalServerName.Style.FlexShrink = 0;
		_hostLocalServerName.Style.Width = Length.Percent( 100 );
		_hostLocalServerName.Style.MinWidth = 0;

		var visRow = ThornsUiPanelAdd.AddChildPanel( card, "thorns-mm-hostvis-row" );
		visRow.Style.FlexDirection = FlexDirection.Row;
		visRow.Style.MarginTop = Length.Pixels( 16 );
		visRow.Style.MarginBottom = Length.Pixels( 10 );

		_hostVisPublic = ThornsUiPanelAdd.AddChildPanel( visRow, "thorns-mm-hostvis-opt" );
		_hostVisPublic.Style.FlexGrow = 1;
		_hostVisPublic.Style.MarginRight = Length.Pixels( 8 );
		_hostVisPublic.Style.PointerEvents = PointerEvents.All;
		_hostVisPublic.AddEventListener( "onmousedown", _ => SetHostLocalVisibilityChoice( false ) );
		_hostVisPublic.AddChild( new Label( "PUBLIC", "thorns-mm-hostvis-lbl" ) );

		_hostVisPrivate = ThornsUiPanelAdd.AddChildPanel( visRow, "thorns-mm-hostvis-opt" );
		_hostVisPrivate.Style.FlexGrow = 1;
		_hostVisPrivate.Style.PointerEvents = PointerEvents.All;
		_hostVisPrivate.AddEventListener( "onmousedown", _ => SetHostLocalVisibilityChoice( true ) );
		_hostVisPrivate.AddChild( new Label( "PRIVATE", "thorns-mm-hostvis-lbl" ) );

		StyleHostModalFieldCap( card.AddChild( new Label( "Server password (private only)", "thorns-mm-modal-fieldcap" ) ) );
		_hostLocalPwd = card.AddChild( new TextEntry() );
		_hostLocalPwd.Placeholder = "Required when Private is selected";
		_hostLocalPwd.Style.MarginBottom = Length.Pixels( 14 );
		_hostLocalPwd.Style.Width = Length.Percent( 100 );
		_hostLocalPwd.Style.MinWidth = 0;
		_hostLocalPwd.Style.FlexShrink = 0;

		_hostLocalErr = card.AddChild( new Label( "", "thorns-mm-modal-err" ) );

		var actions = ThornsUiPanelAdd.AddChildPanel( card, "thorns-mm-modal-actions" );
		actions.Style.FlexDirection = FlexDirection.Row;
		actions.Style.MarginTop = Length.Pixels( 14 );
		actions.Style.JustifyContent = Justify.FlexEnd;

		var cancel = ThornsUiPanelAdd.AddChildPanel( actions, "thorns-mm-modal-btn thorns-mm-modal-btn--ghost" );
		cancel.Style.MarginRight = Length.Pixels( 10 );
		cancel.Style.PointerEvents = PointerEvents.All;
		cancel.AddEventListener( "onmousedown", _ => HideHostLocalModal() );
		cancel.AddChild( new Label( "CANCEL", "thorns-mm-modal-btn-lbl" ) );

		var go = ThornsUiPanelAdd.AddChildPanel( actions, "thorns-mm-modal-btn thorns-mm-modal-btn--primary" );
		go.Style.PointerEvents = PointerEvents.All;
		go.AddEventListener( "onmousedown", OnStartHostMouseDown );
		go.AddChild( new Label( "START HOST", "thorns-mm-modal-btn-lbl" ) );

		SetHostLocalVisibilityChoice( false );
	}

	void BuildJoinPasswordModal( Panel root )
	{
		_joinPwdModal = ThornsUiPanelAdd.AddChildPanel( root, "thorns-mm-modal-backdrop thorns-mm-hidden" );
		_joinPwdModal.Style.Position = PositionMode.Absolute;
		_joinPwdModal.Style.Left = 0;
		_joinPwdModal.Style.Top = 0;
		_joinPwdModal.Style.Right = 0;
		_joinPwdModal.Style.Bottom = 0;
		_joinPwdModal.Style.ZIndex = 500;
		_joinPwdModal.Style.JustifyContent = Justify.Center;
		_joinPwdModal.Style.AlignItems = Align.Center;
		_joinPwdModal.Style.PointerEvents = PointerEvents.All;

		var card = ThornsUiPanelAdd.AddChildPanel( _joinPwdModal, "thorns-mm-modal-card" );
		card.Style.FlexDirection = FlexDirection.Column;
		card.Style.MinWidth = Length.Pixels( 340 );
		card.Style.Padding = Length.Pixels( 22 );

		card.AddChild( new Label( "PRIVATE SERVER", "thorns-mm-modal-title" ) );
		AddModalParagraph( card, "Enter the server password to join." );

		_joinPwdEntry = card.AddChild( new TextEntry() );
		_joinPwdEntry.Placeholder = "Password";
		_joinPwdEntry.Style.MarginTop = Length.Pixels( 14 );
		_joinPwdEntry.Style.MarginBottom = Length.Pixels( 8 );

		_joinPwdErr = card.AddChild( new Label( "", "thorns-mm-modal-err" ) );

		var actions = ThornsUiPanelAdd.AddChildPanel( card, "thorns-mm-modal-actions" );
		actions.Style.FlexDirection = FlexDirection.Row;
		actions.Style.MarginTop = Length.Pixels( 14 );
		actions.Style.JustifyContent = Justify.FlexEnd;

		var cancel = ThornsUiPanelAdd.AddChildPanel( actions, "thorns-mm-modal-btn thorns-mm-modal-btn--ghost" );
		cancel.Style.MarginRight = Length.Pixels( 10 );
		cancel.Style.PointerEvents = PointerEvents.All;
		cancel.AddEventListener( "onmousedown", _ => HideJoinPasswordModal() );
		cancel.AddChild( new Label( "CANCEL", "thorns-mm-modal-btn-lbl" ) );

		var join = ThornsUiPanelAdd.AddChildPanel( actions, "thorns-mm-modal-btn thorns-mm-modal-btn--primary" );
		join.Style.PointerEvents = PointerEvents.All;
		join.AddEventListener( "onmousedown", _ => TryConfirmJoinPassword() );
		join.AddChild( new Label( "JOIN", "thorns-mm-modal-btn-lbl" ) );
	}

	void BuildWipeWorldModal( Panel root )
	{
		_wipeWorldModal = ThornsUiPanelAdd.AddChildPanel( root, "thorns-mm-modal-backdrop thorns-mm-hidden" );
		_wipeWorldModal.Style.Position = PositionMode.Absolute;
		_wipeWorldModal.Style.Left = 0;
		_wipeWorldModal.Style.Top = 0;
		_wipeWorldModal.Style.Right = 0;
		_wipeWorldModal.Style.Bottom = 0;
		_wipeWorldModal.Style.ZIndex = 500;
		_wipeWorldModal.Style.JustifyContent = Justify.Center;
		_wipeWorldModal.Style.AlignItems = Align.Center;
		_wipeWorldModal.Style.PointerEvents = PointerEvents.All;

		var card = ThornsUiPanelAdd.AddChildPanel( _wipeWorldModal, "thorns-mm-modal-card" );
		card.Style.FlexDirection = FlexDirection.Column;
		card.Style.MinWidth = Length.Pixels( 380 );
		card.Style.Padding = Length.Pixels( 22 );

		card.AddChild( new Label( "WIPE SAVE DATA?", "thorns-mm-modal-title" ) );
		AddModalParagraph(
			card,
			"This removes all buildings, world chest contents, tamed wildlife, and stored player progress for this map file. The empty map can be hosted again as a fresh start." );

		_wipeWorldErr = card.AddChild( new Label( "", "thorns-mm-modal-err" ) );

		var actions = ThornsUiPanelAdd.AddChildPanel( card, "thorns-mm-modal-actions" );
		actions.Style.FlexDirection = FlexDirection.Row;
		actions.Style.MarginTop = Length.Pixels( 14 );
		actions.Style.JustifyContent = Justify.FlexEnd;

		var cancel = ThornsUiPanelAdd.AddChildPanel( actions, "thorns-mm-modal-btn thorns-mm-modal-btn--ghost" );
		cancel.Style.MarginRight = Length.Pixels( 10 );
		cancel.Style.PointerEvents = PointerEvents.All;
		cancel.AddEventListener( "onmousedown", _ => HideWipeWorldModal() );
		cancel.AddChild( new Label( "CANCEL", "thorns-mm-modal-btn-lbl" ) );

		var wipe = ThornsUiPanelAdd.AddChildPanel( actions, "thorns-mm-modal-btn thorns-mm-modal-btn--primary" );
		wipe.Style.PointerEvents = PointerEvents.All;
		wipe.AddEventListener( "onmousedown", _ => ConfirmWipeWorldMouseDown() );
		wipe.AddChild( new Label( "WIPE", "thorns-mm-modal-btn-lbl" ) );
	}

	void ShowWipeWorldModal()
	{
		HideDeleteServerModal();
		if ( _wipeWorldModal is not null && _wipeWorldModal.IsValid )
		{
			_wipeWorldErr.Text = "";
			_wipeWorldModal.SetClass( "thorns-mm-hidden", false );
		}
	}

	void HideWipeWorldModal()
	{
		if ( _wipeWorldModal is not null && _wipeWorldModal.IsValid )
			_wipeWorldModal.SetClass( "thorns-mm-hidden", true );
	}

	void ConfirmWipeWorldMouseDown()
	{
		_wipeWorldErr.Text = "";
		if ( string.IsNullOrEmpty( _wipeWorldPendingPath ) )
		{
			HideWipeWorldModal();
			return;
		}

		if ( !ThornsWorldSaveWipe.TryWipeWorldFile( _wipeWorldPendingPath, out var err ) )
		{
			_wipeWorldErr.Text = string.IsNullOrEmpty( err ) ? "Wipe failed." : err;
			return;
		}

		HideWipeWorldModal();
		RebuildMyServerRowsAfterLocalSaveMutation( _wipeWorldPendingPath );
		_wipeWorldPendingPath = null;
	}

	void BuildDeleteServerModal( Panel root )
	{
		_deleteServerModal = ThornsUiPanelAdd.AddChildPanel( root, "thorns-mm-modal-backdrop thorns-mm-hidden" );
		_deleteServerModal.Style.Position = PositionMode.Absolute;
		_deleteServerModal.Style.Left = 0;
		_deleteServerModal.Style.Top = 0;
		_deleteServerModal.Style.Right = 0;
		_deleteServerModal.Style.Bottom = 0;
		_deleteServerModal.Style.ZIndex = 500;
		_deleteServerModal.Style.JustifyContent = Justify.Center;
		_deleteServerModal.Style.AlignItems = Align.Center;
		_deleteServerModal.Style.PointerEvents = PointerEvents.All;

		var card = ThornsUiPanelAdd.AddChildPanel( _deleteServerModal, "thorns-mm-modal-card" );
		card.Style.FlexDirection = FlexDirection.Column;
		card.Style.MinWidth = Length.Pixels( 400 );
		card.Style.Padding = Length.Pixels( 22 );

		card.AddChild( new Label( "DELETE THIS SERVER?", "thorns-mm-modal-title" ) );
		AddModalParagraph(
			card,
			"This permanently deletes the saved world file from disk. Buildings, loot, wildlife, inventory backups, and all player progress tied to this server slot are gone. You cannot undo this." );

		_deleteServerErr = card.AddChild( new Label( "", "thorns-mm-modal-err" ) );

		var actions = ThornsUiPanelAdd.AddChildPanel( card, "thorns-mm-modal-actions" );
		actions.Style.FlexDirection = FlexDirection.Row;
		actions.Style.MarginTop = Length.Pixels( 14 );
		actions.Style.JustifyContent = Justify.FlexEnd;

		var cancel = ThornsUiPanelAdd.AddChildPanel( actions, "thorns-mm-modal-btn thorns-mm-modal-btn--ghost" );
		cancel.Style.MarginRight = Length.Pixels( 10 );
		cancel.Style.PointerEvents = PointerEvents.All;
		cancel.AddEventListener( "onmousedown", _ => HideDeleteServerModal() );
		cancel.AddChild( new Label( "CANCEL", "thorns-mm-modal-btn-lbl" ) );

		var del = ThornsUiPanelAdd.AddChildPanel( actions, "thorns-mm-modal-btn thorns-mm-modal-btn--primary" );
		del.Style.PointerEvents = PointerEvents.All;
		del.AddEventListener( "onmousedown", _ => ConfirmDeleteServerMouseDown() );
		del.AddChild( new Label( "DELETE", "thorns-mm-modal-btn-lbl" ) );
	}

	void ShowDeleteServerModal()
	{
		HideWipeWorldModal();
		if ( _deleteServerModal is not null && _deleteServerModal.IsValid )
		{
			_deleteServerErr.Text = "";
			_deleteServerModal.SetClass( "thorns-mm-hidden", false );
		}
	}

	void HideDeleteServerModal()
	{
		if ( _deleteServerModal is not null && _deleteServerModal.IsValid )
			_deleteServerModal.SetClass( "thorns-mm-hidden", true );
	}

	void ConfirmDeleteServerMouseDown()
	{
		_deleteServerErr.Text = "";
		if ( string.IsNullOrEmpty( _deleteServerPendingPath ) )
		{
			HideDeleteServerModal();
			return;
		}

		if ( !ThornsWorldSaveWipe.TryDeleteWorldFile( _deleteServerPendingPath, out var err ) )
		{
			_deleteServerErr.Text = string.IsNullOrEmpty( err ) ? "Delete failed." : err;
			return;
		}

		HideDeleteServerModal();
		RebuildMyServerRowsAfterLocalSaveMutation( _deleteServerPendingPath );
		_deleteServerPendingPath = null;
	}

	void RebuildMyServerRowsAfterLocalSaveMutation( string affectedRelativePath )
	{
		RebuildMyServerRows();
		if ( string.Equals( _selectedLocalRelativePath, affectedRelativePath, StringComparison.OrdinalIgnoreCase ) )
			ShowEmptyServerDetail();
	}

	void SetHostLocalVisibilityChoice( bool passwordPrivate )
	{
		_hostLocalWantPassword = passwordPrivate;
		_hostVisPublic.SetClass( "thorns-mm-hostvis-opt--sel", !passwordPrivate );
		_hostVisPrivate.SetClass( "thorns-mm-hostvis-opt--sel", passwordPrivate );
	}

	void ShowHostLocalModal()
	{
		if ( !_treeReady || _hostLocalModal is null || !_hostLocalModal.IsValid )
			return;
		_hostLocalErr.Text = "";
		if ( _hostLocalServerName.IsValid() )
			_hostLocalServerName.Text = ThornsHostMenuPreferences.LoadLastHostedServerName();

		_hostLocalPwd.Text = "";
		SetHostLocalVisibilityChoice( false );
		_hostLocalModal.SetClass( "thorns-mm-hidden", false );
	}

	void HideHostLocalModal()
	{
		if ( _hostLocalModal is not null && _hostLocalModal.IsValid )
			_hostLocalModal.SetClass( "thorns-mm-hidden", true );
	}

	async Task ConfirmHostLocalSaveAsync()
	{
		_hostLocalErr.Text = "";

		var serverName = _hostLocalServerName?.Text?.Trim() ?? "";
		if ( serverName.Length < 1 || serverName.Length > 48 )
		{
			_hostLocalErr.Text = "Enter a server name (1–48 characters).";
			return;
		}

		var persistencePath = ThornsHostSavePaths.PersistencePathForServerName( serverName );
		ThornsHostMenuPreferences.SaveLastHostedServerName( serverName );

		if ( _hostLocalWantPassword )
		{
			var pw = _hostLocalPwd.Text?.Trim() ?? "";
			if ( pw.Length < 3 )
			{
				_hostLocalErr.Text = "Private servers need a password (at least 3 characters).";
				return;
			}

			ThornsSessionBootstrap.RequestHostFromLocalSaveNextGameplayLoad(
				new ThornsHostLocalSaveLobbyOptions( true, pw, serverName, persistencePath ) );
		}
		else
		{
			ThornsSessionBootstrap.RequestHostFromLocalSaveNextGameplayLoad(
				new ThornsHostLocalSaveLobbyOptions( false, "", serverName, persistencePath ) );
		}

		HideHostLocalModal();
		Log.Info( "[Thorns] Host a server → gameplay scene + listen lobby." );
		await LoadGameplayAsync();
	}

	void ShowJoinPasswordModal( LobbyInformation lobby )
	{
		_joinPwdLobby = lobby;
		_joinPwdErr.Text = "";
		_joinPwdEntry.Text = "";
		if ( _joinPwdModal is not null && _joinPwdModal.IsValid )
			_joinPwdModal.SetClass( "thorns-mm-hidden", false );
	}

	void HideJoinPasswordModal()
	{
		if ( _joinPwdModal is not null && _joinPwdModal.IsValid )
			_joinPwdModal.SetClass( "thorns-mm-hidden", true );
	}

	void TryConfirmJoinPassword()
	{
		_joinPwdErr.Text = "";
		if ( !ThornsLobbyPasswordGate.VerifyPasswordAgainstLobby( _joinPwdEntry.Text, _joinPwdLobby ) )
		{
			_joinPwdErr.Text = "Incorrect password.";
			return;
		}

		HideJoinPasswordModal();
		_ = ConnectToLobbyAfterPasswordOkAsync( _joinPwdLobby );
	}

	async Task ConnectToLobbyAfterPasswordOkAsync( LobbyInformation lobby )
	{
		ThornsSessionBootstrap.CancelHostFromLocalSaveRequest();
		try
		{
			if ( Networking.IsActive )
				Networking.Disconnect();
			await Task.DelayRealtimeSeconds( 0.06f );
			Networking.Connect( lobby.LobbyId );
			Log.Info( $"[Thorns] Connecting to lobby '{lobby.Name}' (id={lobby.LobbyId})." );
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns] Failed to connect to lobby." );
		}
	}

	Panel BuildServerBrowser( Panel shell )
	{
		var wrap = ThornsUiPanelAdd.AddChildPanel( shell, "thorns-mm-browser thorns-mm-hidden" );
		wrap.Style.FlexDirection = FlexDirection.Column;
		wrap.Style.FlexGrow = 1;
		wrap.Style.MinWidth = 0;
		wrap.Style.MinHeight = 0;

		var head = ThornsUiPanelAdd.AddChildPanel( wrap, "thorns-mm-browser-head" );
		head.Style.FlexDirection = FlexDirection.Row;
		head.Style.JustifyContent = Justify.SpaceBetween;
		head.Style.AlignItems = Align.Center;
		head.Style.MarginBottom = Length.Pixels( 12 );
		head.AddChild( new Label( "SERVER BROWSER", "thorns-mm-browser-title" ) );
		var headRight = ThornsUiPanelAdd.AddChildPanel( head, "thorns-mm-browser-head-right" );
		headRight.Style.FlexDirection = FlexDirection.Row;
		headRight.Style.AlignItems = Align.Center;

		var refresh = ThornsUiPanelAdd.AddChildPanel( headRight, "thorns-mm-refresh" );
		refresh.Style.PointerEvents = PointerEvents.All;
		refresh.Style.MarginRight = Length.Pixels( 14 );
		refresh.AddEventListener( "onmousedown", OnRefreshLobbiesMouseDown );
		refresh.AddChild( new Label( "REFRESH", "thorns-mm-refresh-lbl" ) );

		var online = ThornsUiPanelAdd.AddChildPanel( headRight, "thorns-mm-online" );
		online.Style.FlexDirection = FlexDirection.Row;
		online.Style.AlignItems = Align.Center;
		var onlineDot = online.AddChild( new Panel() );
		onlineDot.AddClass( "thorns-mm-online-dot" );
		online.AddChild( new Label( "LOBBIES", "thorns-mm-online-txt" ) );

		var body = ThornsUiPanelAdd.AddChildPanel( wrap, "thorns-mm-browser-body" );
		body.Style.FlexDirection = FlexDirection.Row;
		body.Style.FlexGrow = 1;
		body.Style.MinHeight = 0;

		var listCol = ThornsUiPanelAdd.AddChildPanel( body, "thorns-mm-browser-listcol" );
		listCol.Style.FlexDirection = FlexDirection.Column;
		listCol.Style.FlexGrow = 2;
		listCol.Style.FlexShrink = 1;
		listCol.Style.MinWidth = 0;

		listCol.AddChild( new Label( "MY SERVERS (LOCAL SAVES)", "thorns-mm-browser-subhead" ) );
		_myServersListHost = ThornsUiPanelAdd.AddChildPanel( listCol, "thorns-mm-server-scroll thorns-mm-myservers-scroll" );
		_myServersListHost.Style.FlexDirection = FlexDirection.Column;
		_myServersListHost.Style.FlexGrow = 0;
		_myServersListHost.Style.FlexShrink = 0;
		_myServersListHost.Style.MaxHeight = Length.Pixels( 220 );
		_myServersListHost.Style.MarginBottom = Length.Pixels( 14 );
		_myServersListHost.Style.Overflow = OverflowMode.Scroll;

		listCol.AddChild( new Label( "ONLINE LOBBIES", "thorns-mm-browser-subhead" ) );

		var filters = ThornsUiPanelAdd.AddChildPanel( listCol, "thorns-mm-filters" );
		filters.Style.FlexDirection = FlexDirection.Row;
		filters.Style.MarginBottom = Length.Pixels( 10 );
		AddFauxDropdown( filters, "All regions" );
		AddFauxDropdown( filters, "All modes" );
		AddFauxDropdown( filters, "Population" );

		_serverListHost = ThornsUiPanelAdd.AddChildPanel( listCol, "thorns-mm-server-scroll" );
		_serverListHost.Style.FlexDirection = FlexDirection.Column;
		_serverListHost.Style.FlexGrow = 1;
		_serverListHost.Style.MinHeight = 0;
		_serverListHost.Style.Overflow = OverflowMode.Scroll;

		var detail = ThornsUiPanelAdd.AddChildPanel( body, "thorns-mm-browser-detail" );
		detail.Style.FlexDirection = FlexDirection.Column;
		detail.Style.FlexGrow = 1;
		detail.Style.FlexShrink = 1;
		detail.Style.MinWidth = Length.Pixels( 220 );
		detail.Style.MarginLeft = Length.Pixels( 14 );

		_detailPreview = ThornsUiPanelAdd.AddChildPanel( detail, "thorns-mm-detail-preview" );
		_detailPreview.Style.FlexGrow = 1;
		_detailPreview.Style.MinHeight = Length.Pixels( 160 );
		_detailTitle = detail.AddChild( new Label( "Select a server", "thorns-mm-detail-title" ) );
		_detailSub = detail.AddChild( new Label( "", "thorns-mm-detail-sub" ) );
		_detailSeed = detail.AddChild( new Label( "", "thorns-mm-detail-seed" ) );
		_detailPing = detail.AddChild( new Label( "", "thorns-mm-detail-ping" ) );

		_joinBtn = ThornsUiPanelAdd.AddChildPanel( detail, "thorns-mm-join" );
		_joinBtn.Style.FlexShrink = 0;
		_joinBtn.Style.MarginTop = Length.Pixels( 14 );
		_joinBtn.Style.PointerEvents = PointerEvents.All;
		_joinBtn.AddEventListener( "onmousedown", _ => TryJoinSelected() );
		_joinBtn.AddChild( new Label( "JOIN SERVER", "thorns-mm-join-label" ) );

		_startLocalWorldBtn = ThornsUiPanelAdd.AddChildPanel( detail, "thorns-mm-join thorns-mm-join--localsave thorns-mm-hidden" );
		_startLocalWorldBtn.Style.FlexShrink = 0;
		_startLocalWorldBtn.Style.MarginTop = Length.Pixels( 10 );
		_startLocalWorldBtn.Style.PointerEvents = PointerEvents.All;
		_startLocalWorldBtn.AddEventListener( "onmousedown", _ => OnStartLocalWorldMouseDown() );
		_startLocalWorldBtn.AddChild( new Label( "START THIS WORLD", "thorns-mm-join-label" ) );

		_wipeLocalWorldBtn = ThornsUiPanelAdd.AddChildPanel( detail, "thorns-mm-join thorns-mm-join--wipe thorns-mm-hidden" );
		_wipeLocalWorldBtn.Style.FlexShrink = 0;
		_wipeLocalWorldBtn.Style.MarginTop = Length.Pixels( 8 );
		_wipeLocalWorldBtn.Style.PointerEvents = PointerEvents.All;
		_wipeLocalWorldBtn.AddEventListener( "onmousedown", _ => OnWipeLocalWorldMouseDown() );
		_wipeLocalWorldBtn.AddChild( new Label( "WIPE SAVE DATA", "thorns-mm-join-label" ) );

		var hostSaveBtn = ThornsUiPanelAdd.AddChildPanel( detail, "thorns-mm-join thorns-mm-join--hostsave" );
		hostSaveBtn.Style.FlexShrink = 0;
		hostSaveBtn.Style.MarginTop = Length.Pixels( 10 );
		hostSaveBtn.Style.PointerEvents = PointerEvents.All;
		hostSaveBtn.AddEventListener( "onmousedown", _ => ShowHostLocalModal() );
		hostSaveBtn.AddChild( new Label( "HOST A SERVER", "thorns-mm-join-label" ) );

		var bottomBar = ThornsUiPanelAdd.AddChildPanel( detail, "thorns-mm-browser-bottombar" );
		bottomBar.Style.FlexDirection = FlexDirection.Row;
		bottomBar.Style.AlignItems = Align.Center;
		bottomBar.Style.Width = Length.Fraction( 1f );

		var backBr = ThornsUiPanelAdd.AddChildPanel( bottomBar, "thorns-mm-browser-back" );
		backBr.Style.PointerEvents = PointerEvents.All;
		backBr.AddEventListener( "onmousedown", _ => ShowLayer( MenuLayer.PlayMenu ) );
		var backLbl = backBr.AddChild( new Label( "← BACK", "thorns-mm-join-label" ) );
		backLbl.AddClass( "thorns-mm-join-label--ghost" );

		var bottomSpacer = ThornsUiPanelAdd.AddChildPanel( bottomBar, "thorns-mm-browser-bottombar-spacer" );
		bottomSpacer.Style.FlexGrow = 1;
		bottomSpacer.Style.MinWidth = Length.Pixels( 12 );

		_removeLocalServerBtn = ThornsUiPanelAdd.AddChildPanel( bottomBar,
			"thorns-mm-join thorns-mm-join--remove thorns-mm-hidden" );
		_removeLocalServerBtn.Style.FlexShrink = 0;
		_removeLocalServerBtn.Style.MinWidth = Length.Pixels( 168 );
		_removeLocalServerBtn.Style.PointerEvents = PointerEvents.All;
		_removeLocalServerBtn.AddEventListener( "onmousedown", _ => OnRemoveLocalServerMouseDown() );
		_removeLocalServerBtn.AddChild( new Label( "REMOVE SERVER", "thorns-mm-join-label" ) );

		RebuildMyServerRows();
		RebuildLobbyRows();
		ShowEmptyServerDetail();
		return wrap;
	}

	void OnStartHostMouseDown( PanelEvent e )
	{
		_ = ConfirmHostLocalSaveAsync();
	}

	void OnRefreshLobbiesMouseDown( PanelEvent e )
	{
		_ = RefreshLobbyListAsync();
	}

	async Task RefreshLobbyListAsync()
	{
		if ( !_treeReady )
			return;

		try
		{
			// Match sessions for this game's package ident (same path the platform menu uses via lobby filters).
			var ident = Game.Ident;
			_lobbies = string.IsNullOrEmpty( ident )
				? await Networking.QueryLobbies( CancellationToken.None )
				: await Networking.QueryLobbies( ident, CancellationToken.None );
			if ( _lobbies is null )
				_lobbies = new List<LobbyInformation>();
			Log.Info( $"[Thorns] Server browser: Game.Ident={ident ?? "(null)"} → {_lobbies.Count} lobby/lobbies." );
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns] Server browser: lobby query failed." );
			_lobbies = new List<LobbyInformation>();
		}

		RebuildMyServerRows();
		RebuildLobbyRows();
		if ( _lobbies.Count == 0 )
		{
			_selectedLobbyIndex = -1;
			if ( string.IsNullOrEmpty( _selectedLocalRelativePath ) )
				ShowEmptyServerDetail();
		}
		else
		{
			_selectedLobbyIndex = Math.Clamp( _selectedLobbyIndex < 0 ? 0 : _selectedLobbyIndex, 0, _lobbies.Count - 1 );
			// Keep "My Servers" selection unless we're only showing online detail.
			if ( string.IsNullOrEmpty( _selectedLocalRelativePath ) )
				SelectServer( _selectedLobbyIndex );
		}
	}

	void RebuildMyServerRows()
	{
		if ( _myServersListHost is null || !_myServersListHost.IsValid )
			return;

		_myServersListHost.DeleteChildren();
		_myServerRowPanels.Clear();
		_localSaves = ThornsLocalWorldSaves.ListWorldSaves();

		if ( _localSaves.Count == 0 )
		{
			var empty = ThornsUiPanelAdd.AddChildPanel( _myServersListHost, "thorns-mm-server-row thorns-mm-server-row--empty" );
			empty.Style.Padding = Length.Pixels( 10 );
			empty.AddChild( new Label(
				"No local worlds yet. Use HOST A SERVER to create a named save, or host once to create Thorns/saves/world_*.json.",
				"thorns-mm-server-sub" ) );
			return;
		}

		for ( var i = 0; i < _localSaves.Count; i++ )
		{
			var entry = _localSaves[i];
			var row = ThornsUiPanelAdd.AddChildPanel( _myServersListHost, "thorns-mm-server-row thorns-mm-server-row--local" );
			row.Style.FlexDirection = FlexDirection.Row;
			row.Style.AlignItems = Align.Center;
			row.Style.PointerEvents = PointerEvents.All;
			row.AddEventListener( "onmousedown", _ => SelectLocalSave( entry ) );

			var nameCol = ThornsUiPanelAdd.AddChildPanel( row, "thorns-mm-server-namecol" );
			nameCol.Style.FlexDirection = FlexDirection.Column;
			nameCol.Style.FlexGrow = 1;
			nameCol.Style.MinWidth = 0;
			nameCol.AddChild( new Label( entry.DisplayLabel, "thorns-mm-server-name" ) );
			nameCol.AddChild( new Label( entry.RelativePath, "thorns-mm-server-sub" ) );

			_myServerRowPanels.Add( row );
		}
	}

	void SelectLocalSave( ThornsLocalWorldSaves.Entry entry )
	{
		HideDeleteServerModal();
		_selectedLobbyIndex = -1;
		_selectedLocalRelativePath = entry.RelativePath;
		for ( var i = 0; i < _serverRowPanels.Count; i++ )
			_serverRowPanels[i].SetClass( "thorns-mm-server-row--sel", false );

		for ( var i = 0; i < _myServerRowPanels.Count && i < _localSaves.Count; i++ )
			_myServerRowPanels[i].SetClass( "thorns-mm-server-row--sel",
				string.Equals( _localSaves[i].RelativePath, entry.RelativePath, StringComparison.OrdinalIgnoreCase ) );

		_detailTitle.Text = entry.DisplayLabel;
		_detailSub.Text = $"Local file · {entry.RelativePath}";
		_detailPing.Text = "Offline · listen host on this PC";

		SetJoinButtonEnabled( false );
		SetLocalWorldButtonsVisible( true );
	}

	void RebuildLobbyRows()
	{
		if ( _serverListHost is null || !_serverListHost.IsValid )
			return;

		_serverListHost.DeleteChildren();
		_serverRowPanels.Clear();

		if ( _lobbies.Count == 0 )
		{
			var empty = ThornsUiPanelAdd.AddChildPanel( _serverListHost, "thorns-mm-server-row thorns-mm-server-row--empty" );
			empty.Style.Padding = Length.Pixels( 12 );
			empty.AddChild( new Label(
				"No lobbies found for this game. Use REFRESH or HOST A SERVER.",
				"thorns-mm-server-sub" ) );
			return;
		}

		for ( var i = 0; i < _lobbies.Count; i++ )
		{
			var idx = i;
			var lobby = _lobbies[i];
			var row = ThornsUiPanelAdd.AddChildPanel( _serverListHost, "thorns-mm-server-row" );
			row.Style.FlexDirection = FlexDirection.Row;
			row.Style.AlignItems = Align.Center;
			row.Style.PointerEvents = PointerEvents.All;
			row.AddEventListener( "onmousedown", _ => SelectServer( idx ) );

			var nameCol = ThornsUiPanelAdd.AddChildPanel( row, "thorns-mm-server-namecol" );
			nameCol.Style.FlexDirection = FlexDirection.Column;
			nameCol.Style.FlexGrow = 1;
			nameCol.Style.MinWidth = 0;
			var lockSuffix = ThornsLobbyPasswordGate.LobbyRequiresPassword( lobby ) ? "  🔒" : "";
			nameCol.AddChild( new Label( (string.IsNullOrWhiteSpace( lobby.Name ) ? "Unnamed lobby" : lobby.Name) + lockSuffix, "thorns-mm-server-name" ) );
			var sub = $"{lobby.Members} / {lobby.MaxMembers} · {lobby.Map}";
			nameCol.AddChild( new Label( sub, "thorns-mm-server-sub" ) );

			var ping = lobby.Ping >= 0 ? $"{lobby.Ping} ms" : "—";
			_ = row.AddChild( new Label( ping, "thorns-mm-server-ping" ) );

			_serverRowPanels.Add( row );
		}
	}

	Panel BuildSettingsPanel( Panel shell )
	{
		var p = ThornsUiPanelAdd.AddChildPanel( shell, "thorns-mm-settings thorns-mm-hidden" );
		p.Style.FlexDirection = FlexDirection.Column;
		p.Style.FlexGrow = 1;
		p.Style.MinWidth = 0;
		p.Style.MinHeight = 0;
		p.Style.Padding = Length.Pixels( 20 );

		p.AddChild( new Label( "SETTINGS", "thorns-mm-browser-title" ) );
		p.AddChild( new Label(
			"Default key bindings for Thorns survival gameplay.",
			"thorns-mm-settings-intro" ) );

		var scroll = ThornsUiPanelAdd.AddChildPanel( p, "thorns-mm-settings-scroll" );
		scroll.Style.FlexGrow = 1;
		scroll.Style.MinHeight = 0;
		scroll.Style.Overflow = OverflowMode.Scroll;
		scroll.Style.FlexDirection = FlexDirection.Column;
		ThornsUiControlsReference.Populate( scroll, "thorns-mm-settings" );

		var back = ThornsUiPanelAdd.AddChildPanel( p, "thorns-mm-settings-back" );
		back.Style.MarginTop = Length.Pixels( 20 );
		back.Style.PointerEvents = PointerEvents.All;
		back.AddEventListener( "onmousedown", _ => ShowLayer( MenuLayer.PlayMenu ) );
		var backLbl = back.AddChild( new Label( "BACK", "thorns-mm-join-label" ) );
		backLbl.AddClass( "thorns-mm-join-label--ghost" );
		return p;
	}

	static void AddFauxDropdown( Panel row, string text )
	{
		var d = ThornsUiPanelAdd.AddChildPanel( row, "thorns-mm-filter" );
		d.Style.FlexGrow = 1;
		d.Style.MarginRight = Length.Pixels( 8 );
		d.AddChild( new Label( text, "thorns-mm-filter-lbl" ) );
	}

	static void AddStoryModeComingSoonMenuRow( Panel host )
	{
		var row = ThornsUiPanelAdd.AddChildPanel( host, "thorns-mm-btn thorns-mm-btn--primary thorns-mm-btn--story-disabled" );
		row.Style.FlexDirection = FlexDirection.Column;
		row.Style.JustifyContent = Justify.Center;
		row.Style.AlignItems = Align.Center;
		row.Style.MarginBottom = Length.Pixels( 10 );
		row.Style.Padding = Length.Pixels( 14 );
		row.Style.PointerEvents = PointerEvents.None;

		row.AddChild( new Label( "STORY MODE", "thorns-mm-btn-lbl thorns-mm-btn-story-crossed" ) ).Style.PointerEvents =
			PointerEvents.None;
		row.AddChild( new Label( "Coming Soon!", "thorns-mm-btn-story-soon" ) ).Style.PointerEvents = PointerEvents.None;
	}

	static void AddMenuButton( Panel host, string text, bool primary, Action onClick )
	{
		var btn = ThornsUiPanelAdd.AddChildPanel( host, primary ? "thorns-mm-btn thorns-mm-btn--primary" : "thorns-mm-btn" );
		btn.Style.FlexDirection = FlexDirection.Row;
		btn.Style.JustifyContent = Justify.Center;
		btn.Style.AlignItems = Align.Center;
		btn.Style.MarginBottom = Length.Pixels( 10 );
		btn.Style.Padding = Length.Pixels( 14 );
		btn.Style.PointerEvents = PointerEvents.All;
		btn.AddEventListener( "onmousedown", _ => onClick() );
		btn.AddChild( new Label( text, "thorns-mm-btn-lbl" ) );
	}

	void ShowLayer( MenuLayer layer )
	{
		if ( !_treeReady )
			return;

		if ( layer != MenuLayer.Browser )
		{
			HideWipeWorldModal();
			HideDeleteServerModal();
		}

		_rootLayer.SetClass( "thorns-mm-hidden", layer != MenuLayer.Root );
		_playLayer.SetClass( "thorns-mm-hidden", layer != MenuLayer.PlayMenu );
		_browserLayer.SetClass( "thorns-mm-hidden", layer != MenuLayer.Browser );
		_settingsLayer.SetClass( "thorns-mm-hidden", layer != MenuLayer.Settings );

		var showArt = layer is MenuLayer.Root or MenuLayer.PlayMenu;
		_artLayer.SetClass( "thorns-mm-hidden", !showArt );

		if ( layer == MenuLayer.Browser )
			_ = RefreshLobbyListAsync();
	}

	void SelectServer( int index )
	{
		HideDeleteServerModal();
		if ( _lobbies.Count == 0 )
		{
			_selectedLobbyIndex = -1;
			if ( string.IsNullOrEmpty( _selectedLocalRelativePath ) )
				ShowEmptyServerDetail();
			return;
		}

		_selectedLocalRelativePath = null;
		for ( var i = 0; i < _myServerRowPanels.Count; i++ )
			_myServerRowPanels[i].SetClass( "thorns-mm-server-row--sel", false );

		_selectedLobbyIndex = Math.Clamp( index, 0, _lobbies.Count - 1 );
		for ( var i = 0; i < _serverRowPanels.Count; i++ )
			_serverRowPanels[i].SetClass( "thorns-mm-server-row--sel", i == _selectedLobbyIndex );

		if ( _selectedLobbyIndex < 0 || _selectedLobbyIndex >= _lobbies.Count )
		{
			ShowEmptyServerDetail();
			return;
		}

		var lobby = _lobbies[_selectedLobbyIndex];
		_detailTitle.Text = string.IsNullOrWhiteSpace( lobby.Name ) ? "Lobby" : lobby.Name;
		var priv = ThornsLobbyPasswordGate.LobbyRequiresPassword( lobby ) ? " · Password required" : "";
		_detailSub.Text = $"{lobby.Members} / {lobby.MaxMembers} players · {lobby.Map}{priv}";
		_detailSeed.Text = ThornsLobbyWorldSeed.FormatLobbyDetailLine( lobby );
		var ping = lobby.Ping >= 0 ? $"{lobby.Ping} ms" : "Ping —";
		_detailPing.Text = $"{ping} · {lobby.Game}";

		SetJoinButtonEnabled( !lobby.IsFull );
		SetLocalWorldButtonsVisible( false );
	}

	void ShowEmptyServerDetail()
	{
		HideDeleteServerModal();
		_selectedLocalRelativePath = null;
		_detailTitle.Text = "No server selected";
		_detailSub.Text = "Pick a local world under My Servers, refresh online lobbies, or host a new game.";
		_detailSeed.Text = "";
		_detailPing.Text = "";
		SetJoinButtonEnabled( false );
		SetLocalWorldButtonsVisible( false );
		for ( var i = 0; i < _serverRowPanels.Count; i++ )
			_serverRowPanels[i].SetClass( "thorns-mm-server-row--sel", false );
		for ( var i = 0; i < _myServerRowPanels.Count; i++ )
			_myServerRowPanels[i].SetClass( "thorns-mm-server-row--sel", false );
	}

	void SetJoinButtonEnabled( bool enabled )
	{
		if ( _joinBtn is null || !_joinBtn.IsValid )
			return;
		_joinBtn.SetClass( "thorns-mm-join--disabled", !enabled );
		_joinBtn.Style.PointerEvents = enabled ? PointerEvents.All : PointerEvents.None;
	}

	void SetLocalWorldButtonsVisible( bool visible )
	{
		if ( _startLocalWorldBtn is not null && _startLocalWorldBtn.IsValid )
			_startLocalWorldBtn.SetClass( "thorns-mm-hidden", !visible );
		if ( _wipeLocalWorldBtn is not null && _wipeLocalWorldBtn.IsValid )
			_wipeLocalWorldBtn.SetClass( "thorns-mm-hidden", !visible );
		if ( _removeLocalServerBtn is not null && _removeLocalServerBtn.IsValid )
			_removeLocalServerBtn.SetClass( "thorns-mm-hidden", !visible );
	}

	async Task ConfirmStartLocalWorldAsync( string relativePath, string displayLabel )
	{
		ThornsHostMenuPreferences.SaveLastHostedServerName( displayLabel );
		ThornsHostWorldGenerationIntent worldGenIntent = default;
		if ( ThornsWorldPersistence.TryPeekWorldGenerationSeed( relativePath, out var persistedSeed ) )
			worldGenIntent = ThornsHostWorldGenerationIntent.Fixed( persistedSeed );

		ThornsSessionBootstrap.RequestHostFromLocalSaveNextGameplayLoad(
			new ThornsHostLocalSaveLobbyOptions( false, "", displayLabel, relativePath, worldGenIntent ) );
		Log.Info( $"[Thorns] Start local world '{displayLabel}' → {relativePath}" );
		await LoadGameplayAsync();
	}

	void OnStartLocalWorldMouseDown()
	{
		if ( string.IsNullOrEmpty( _selectedLocalRelativePath ) )
			return;

		for ( var i = 0; i < _localSaves.Count; i++ )
		{
			if ( string.Equals( _localSaves[i].RelativePath, _selectedLocalRelativePath, StringComparison.OrdinalIgnoreCase ) )
			{
				_ = ConfirmStartLocalWorldAsync( _localSaves[i].RelativePath, _localSaves[i].DisplayLabel );
				return;
			}
		}
	}

	void OnWipeLocalWorldMouseDown()
	{
		if ( string.IsNullOrEmpty( _selectedLocalRelativePath ) )
			return;

		HideDeleteServerModal();
		_wipeWorldPendingPath = _selectedLocalRelativePath;
		ShowWipeWorldModal();
	}

	void OnRemoveLocalServerMouseDown()
	{
		if ( string.IsNullOrEmpty( _selectedLocalRelativePath ) )
			return;

		HideWipeWorldModal();
		_deleteServerPendingPath = _selectedLocalRelativePath;
		ShowDeleteServerModal();
	}

	void TryJoinSelected()
	{
		if ( _selectedLobbyIndex < 0 || _selectedLobbyIndex >= _lobbies.Count )
			return;

		var lobby = _lobbies[_selectedLobbyIndex];
		if ( lobby.IsFull )
		{
			Log.Warning( "[Thorns] Selected lobby is full." );
			return;
		}

		ThornsSessionBootstrap.CancelHostFromLocalSaveRequest();

		if ( ThornsLobbyPasswordGate.LobbyRequiresPassword( lobby ) )
			ShowJoinPasswordModal( lobby );
		else
			_ = ConnectToLobbyAfterPasswordOkAsync( lobby );
	}

	async Task LoadGameplayAsync()
	{
		PrepareForGameplaySceneTransition();
		ThornsMenuAudioHandoff.ArmForGameplayTransition();
		await Task.DelayRealtimeSeconds( 0.08f );

		if ( !TryCreateGameplaySceneLoadOptions( out var opt, out var resolvedPath ) )
		{
			ThornsLoadingScreenHero.Clear();
			Log.Error(
				"[Thorns] Failed to resolve gameplay scene. Expected scenes/thorns_procedural.scene (or terrain_boot / flat fallbacks under Assets/scenes)." );
			return;
		}

		var loadedProceduralLayout = resolvedPath.Contains( "thorns_procedural", StringComparison.OrdinalIgnoreCase );
		if ( !loadedProceduralLayout )
			Log.Warning( $"[Thorns] Using fallback gameplay scene '{resolvedPath}' (full procedural layout missing or could not be loaded)." );

		Log.Info( $"[Thorns] Loading gameplay scene '{resolvedPath}'…" );
		if ( !Game.ChangeScene( opt ) )
		{
			ThornsLoadingScreenHero.Clear();
			Log.Error( "[Thorns] Game.ChangeScene rejected (see host / networking state)." );
			return;
		}

		Log.Info( $"[Thorns] Gameplay scene load started ({resolvedPath})." );
	}

	void PrepareForGameplaySceneTransition()
	{
		ThornsMainMenuAtmosphere.BeginMusicFadeOut( 1.5f );
		ThornsLoadingScreenHero.Show( "Thorns" );
		if ( Panel is not null && Panel.IsValid )
			Panel.Style.PointerEvents = PointerEvents.None;
	}

	/// <summary>Builds <see cref="SceneLoadOptions"/> from the first candidate that <see cref="SceneLoadOptions.SetScene"/> accepts.</summary>
	static bool TryCreateGameplaySceneLoadOptions( out SceneLoadOptions opt, out string resolvedPath )
	{
		opt = default;
		resolvedPath = null;

		foreach ( var path in GameplayScenePathCandidates )
		{
			var attempt = new SceneLoadOptions();
			var sceneResource = ResourceLibrary.Get<SceneFile>( path );
			if ( sceneResource is not null && attempt.SetScene( sceneResource ) )
			{
				opt = attempt;
				resolvedPath = path;
				Log.Info( $"[Thorns] Resolved gameplay scene via SceneFile: {path}" );
				return true;
			}

			if ( attempt.SetScene( path ) )
			{
				opt = attempt;
				resolvedPath = path;
				Log.Info( $"[Thorns] Resolved gameplay scene via path: {path}" );
				return true;
			}
		}

		return false;
	}

	static void ExitGame()
	{
		Log.Info( "[Thorns] Exit from main menu." );
		Game.Close();
	}

	/// <summary>
	/// <see cref="ScreenPanel"/> composites through a main <see cref="CameraComponent"/>; menu scenes often ship without one.
	/// Editor can also show a stale hierarchy while the on-disk scene has a camera — create one at runtime if nothing is active.
	/// </summary>
	void EnsureMainMenuRenderCamera()
	{
		if ( !Game.IsPlaying || Scene is null || !Scene.IsValid() )
			return;

		if ( TryFindEnabledMainCamera().IsValid() )
			return;

		var go = new GameObject( true, "ThornsMainMenuCamera" );
		go.WorldPosition = new Vector3( 0f, 0f, 200f );
		go.WorldRotation = Rotation.Identity;
		go.WorldScale = 1f;
		var cam = go.Components.Create<CameraComponent>();
		cam.IsMainCamera = true;
		cam.Enabled = true;
		cam.BackgroundColor = new Color( 0.04f, 0.05f, 0.08f, 1f );
		cam.FieldOfView = 70f;
		cam.ZNear = 1f;
		cam.ZFar = 10000f;
		Log.Info( "[Thorns] Main menu: created runtime main camera (scene had no enabled IsMainCamera)." );
	}

	CameraComponent TryFindEnabledMainCamera()
	{
		if ( Scene is null || !Scene.IsValid() )
			return default;

		foreach ( var cam in Scene.GetAllComponents<CameraComponent>() )
		{
			if ( cam.IsValid() && cam.Enabled && cam.IsMainCamera )
				return cam;
		}

		return default;
	}
}

