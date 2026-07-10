#nullable disable

using System;
using System.Collections.Generic;
using Sandbox.UI;

namespace Sandbox;

/// <summary>Gameplay toast kinds — rendered in the center-left HUD column with tame/level-up banners.</summary>
public enum ThornsGameplayToastKind
{
	Positive = 0,
	Combat = 1,
	Loot = 2,
	Hint = 3,
	Economy = 4,
	LevelUp = 5
}

/// <summary>
/// Modular tab shell + gameplay HUD chrome (TAB menu). Builds once; switches tab visibility without recreating trees.
/// When present on the pawn, <see cref="ThornsDebugHudHost"/> skips legacy crosshair/player card/toolbar/full-inventory overlay.
/// </summary>
[Title( "Thorns — Game UI Shell" )]
[Category( "Thorns/UI" )]
[Icon( "dashboard" )]
[Order( 23 )]
public sealed partial class ThornsGameShell : PanelComponent, Component.INetworkSpawn
{
	public bool MenuOpen => _hud.Menu.MenuOpen;
	public ThornsMainUiTab ActiveTab => _hud.Menu.ActiveTab;

	/// <summary>True after <see cref="TryInit"/> has built the root panel tree.</summary>
	public bool IsLocalHudReady => _root is not null && _root.IsValid;

	Panel _root;
	Panel _hudMaskedLayer;
	Panel _crosshairLayer;
	Panel _menuLayer;
	Panel _shellMenuCard;
	Panel _tabBodiesHost;

	readonly ThornsUiGridSlot[] _hotbarSlots = new ThornsUiGridSlot[ThornsInventory.HotbarSlotCount];
	readonly Dictionary<ThornsMainUiTab, Panel> _tabBodies = new();

	ThornsUiTabButton[] _tabButtons;

	Label _alertLabel;
	Panel _toastFeed;
	Label _interactionHint;

	readonly ThornsUiStatBar[] _vitalsBars = new ThornsUiStatBar[5];
	ThornsUiInventoryTabBody _invBody;
	ThornsUiSkillsTabBody _skillsTabBody;
	ThornsUiTamesTabBody _tamesTabBody;
	ThornsUiJournalTabBody _journalTabBody;
	ThornsUiGuildTabBody _guildTabBody;
	double _nextDataTick = -1;
	int _selectedInventorySlot = -1;
	int _selectedArmorSlot = -1;

	/// <summary>Crafting column: inspect output item id (cleared when selecting an inventory slot).</summary>
	string _contextCraftInspectOutputItemId = "";

	int _lastToolbarIndex = -1;
	int _craftUiLastInvRev = int.MinValue;
	int _inspectContextLastInvMirrorRev = int.MinValue;
	int _inspectContextLastArmorMirrorRev = int.MinValue;
	int _craftUiLastTierCached = int.MinValue;
	ThornsCraftingFilter _craftUiFilter = ThornsCraftingFilter.All;
	ThornsCraftingFilter _craftUiLastFilterCached = (ThornsCraftingFilter)(-1);
	readonly Dictionary<ThornsCraftingFilter, Panel> _craftFilterButtons = new();

	int? _shellPendingMoveSlot;
	int? _shellPendingArmorSlot;
	int? _shellHoverSlot;
	int? _shellHoverArmorSlot;
	int? _shellLastValidDropSlot;
	int? _shellLastValidDropArmorSlot;
	/// <summary>Under LMB-drag, sibling slots only get :hover if capture is released; merges with flaky screen hit-tests.</summary>
	int? _shellUiPointerInventorySlot;
	int? _shellUiPointerArmorSlot;
	bool _shellQueuedCentralRelease;

	/// <summary>LMB down on a filled inventory slot — inspect vs drag decided on release or after move threshold.</summary>
	int? _shellPressInventorySlot;

	/// <summary>LMB down on a filled armor slot — drag starts only after move threshold.</summary>
	int? _shellPressArmorSlot;

	Vector2 _shellPressStartScreen;
	Vector2 _shellPressSyntheticScreen;
	Vector2 _shellPressLastHardwareMouse;
	bool _shellPressSyntheticCursorReady;

	const float ShellDragThresholdPx = 8f;

	/// <summary>
	/// TAB backpack cells use ~76×76 (.thorns-inv-grid-row). Matching the ghost avoids collapsed flex icon area (was ~60 centered).
	/// </summary>
	const float InventoryDragGhostWidthPx = 76f;
	const float InventoryDragGhostHeightPx = 76f;

	/// <summary>Set true only while diagnosing — logs every frame during drag (heavy).</summary>
	static readonly bool ShellDnDVerboseLogging = false;

	int _shellDnDVerboseTick;

	/// <summary>
	/// While LMB UI-drag is active, <see cref="Mouse.Position"/> often stays frozen; we integrate <see cref="Input.MouseDelta"/> into screen space for hit-tests.
	/// </summary>
	Vector2 _shellDnDSyntheticScreen;
	Vector2 _shellDnDLastHardwareMouse;
	bool _shellDnDSyntheticCursorReady;

	int? _shellDnDLastLoggedInv;
	int? _shellDnDLastLoggedArmor;

	Panel _shellDragGhost;
	Label _toolbarAmmoMiniLabel;
	Panel _toolbarDockHost;

	Panel _shellDamageVignetteLayer;
	double _shellDamageVignetteDecayStart;
	double _shellDamageVignetteFadeEnd;
	float _shellDamageVignetteDisplay01;

	Panel _shellLevelUpVignetteLayer;
	float _shellLevelUpVignetteDisplay01;
	double _shellLevelUpVignetteDecayStart;
	double _shellLevelUpVignetteFadeEnd;
	double _shellLevelUpBarGlowUntil;

	public bool StorageChestUiOpen { get; private set; }

	public bool CampfireUiOpen { get; private set; }

	public bool WorkbenchUiOpen { get; private set; }

	/// <summary>TAB menu, chest overlay, campfire, workbench, or radio shop — blocks movement/combat chrome.</summary>
	public bool BlocksGameplayShellOverlay =>
		MenuOpen || StorageChestUiOpen || CampfireUiOpen || WorkbenchUiOpen || RadioShopUiOpen;

	/// <summary>True while an inventory or armor stack is being dragged in the TAB shell (suppresses mirror refresh churn).</summary>
	public bool IsInventoryDragActive =>
		MenuOpen && (_shellPendingMoveSlot.HasValue || _shellPendingArmorSlot.HasValue);

	bool IsAnyCentralDragActive =>
		_shellPendingMoveSlot.HasValue || _shellPendingArmorSlot.HasValue || _storageDragFromChest.HasValue
		|| _campfireDragFromCampfire.HasValue || _workbenchDragFromWorkbench.HasValue;

	Guid _storageChestStructureId;
	ThornsInventorySlotNet[] _storageChestMirror;
	Panel _storageChestLayer;
	Panel _storageChestCard;
	ThornsUiGridSlot[] _storageChestSlots;
	ThornsUiGridSlot[] _storageOverlayPlayerSlots;
	bool _storageChestUiBuilt;
	bool? _storageDragFromChest;
	int _storageDragSlot = -1;
	int? _storageHoverChestSlot;
	int? _storageHoverPlayerSlot;
	Panel _storageDragGhost;

	Guid _campfireStructureId;
	ThornsInventorySlotNet[] _campfireMirror;
	float _campfireProgress01;
	float _campfireRemainingSec;
	string _campfireInputLabel;
	string _campfireOutputLabel;
	Panel _campfireLayer;
	Panel _campfireCard;
	Panel _campfireProgressTrack;
	Panel _campfireProgressFill;
	Label _campfireProgressLabel;
	ThornsUiGridSlot[] _campfireSlots;
	ThornsUiGridSlot[] _campfireOverlayPlayerSlots;
	bool _campfireUiBuilt;
	bool? _campfireDragFromCampfire;
	int _campfireDragSlot = -1;
	int? _campfireHoverCampfireSlot;
	int? _campfireHoverPlayerSlot;
	Panel _campfireDragGhost;

	Guid _workbenchStructureId;
	ThornsInventorySlotNet[] _workbenchMirror;
	float _workbenchProgress01;
	float _workbenchRemainingSec;
	string _workbenchProcessingLabel;
	Panel _workbenchLayer;
	Panel _workbenchCard;
	Panel _workbenchProgressTrack;
	Panel _workbenchProgressFill;
	Label _workbenchProgressLabel;
	ThornsUiGridSlot[] _workbenchSlots;
	ThornsUiGridSlot[] _workbenchOverlayPlayerSlots;
	bool _workbenchUiBuilt;
	bool? _workbenchDragFromWorkbench;
	int _workbenchDragSlot = -1;
	int? _workbenchHoverWorkbenchSlot;
	int? _workbenchHoverPlayerSlot;
	Panel _workbenchDragGhost;

	public void OnNetworkSpawn( Connection owner ) => TryInit();
	protected override void OnStart() => TryInit();

	protected override void OnDestroy()
	{
		ShellDestroyDragGhost();
		if ( MenuOpen )
			_hud.Menu.ForceCloseForDestroy();
		base.OnDestroy();
	}

	public void ToggleMenu()
	{
		if ( MenuOpen )
			ShellClearDragState();

		_hud.Menu.Toggle(
			onOpened: () =>
			{
				ApplyMenuVisibility();
				Components.Get<ThornsPlayerMilestones>()?.ClientOrHostRecordEvent( ThornsMilestoneEventTokens.OpenTab );
			},
			onClosed: () =>
			{
				ApplyMenuVisibility();
				RefreshTabsVisual();
			},
			onTabSelected: ShellOnTabSelected );
	}

	/// <summary>Closes the TAB menu (e.g. Tames panel ×) without toggling open.</summary>
	public void CloseMenu()
	{
		if ( !MenuOpen )
			return;

		ShellClearDragState();
		_hud.Menu.Close( () =>
		{
			ApplyMenuVisibility();
			RefreshTabsVisual();
		} );
	}

	public void SetActiveTab( ThornsMainUiTab tab ) =>
		_hud.Menu.SetActiveTab( tab, ShellOnTabSelected );

	void ShellOnTabSelected( ThornsMainUiTab tab )
	{
		ApplyTabBodiesVisibility();
		RefreshTabsVisual();
		if ( tab == ThornsMainUiTab.Tames && _tamesTabBody is { IsValid: true } )
			_tamesTabBody.RefreshFromPawn( GameObject, force: true );
		if ( tab == ThornsMainUiTab.Skills && _skillsTabBody is { IsValid: true } )
			_skillsTabBody.RefreshFromPawn( GameObject, force: true );
		if ( tab == ThornsMainUiTab.Journal && _journalTabBody is { IsValid: true } )
			_journalTabBody.RefreshFromPawn( GameObject, force: true );
		if ( tab == ThornsMainUiTab.Guild && _guildTabBody is { IsValid: true } )
			_guildTabBody.RefreshFromPawn( GameObject, force: true );
	}

	/// <summary>Local client: pinned journal goal id for HUD chip (empty = none).</summary>
	public string ClientPinnedJournalGoalId => _hud.Menu.ClientPinnedJournalGoalId ?? "";

	public bool ClientJournalHudPinExplicit => _hud.Menu.ClientJournalHudPinExplicit;

	void ClientApplyJournalHudPin( string goalIdOrEmpty, bool pinExplicit ) =>
		_hud.Menu.ClientApplyJournalHudPin( goalIdOrEmpty, pinExplicit );

	void ClientEnsureDefaultPinnedJournalGoal() =>
		_hud.Menu.ClientEnsureDefaultPinnedJournalGoal( Components.Get<ThornsPlayerMilestones>() );

	/// <summary>Host → owner client toast (loot crates, economy).</summary>
	public static void HostPushToastForPawnRoot(
		GameObject pawnRoot,
		string message,
		float durationSeconds = 3.2f,
		ThornsGameplayToastKind kind = ThornsGameplayToastKind.Positive ) =>
		ThornsToastBus.HostPushForPawnRoot( pawnRoot, message, durationSeconds, kind );

	/// <summary>Host → owner: stun-for-tame prompt uses the same center-left card as the taming HUD (not the toast feed).</summary>
	public static void HostPushTameStunBannerForPawnRoot(
		GameObject pawnRoot,
		string title,
		string subtitle,
		float durationSeconds = 4.2f ) =>
		ThornsToastBus.HostPushTameStunBannerForPawnRoot( pawnRoot, title, subtitle, durationSeconds );

	public static void HostPushLootPickupToast( ThornsInventory inv, string itemId, int qty, string subtitle ) =>
		ThornsToastBus.HostPushLootPickupToast( inv, itemId, qty, subtitle );

	[Rpc.Owner]
	internal void RpcReceiveGameplayToast( string message, float durationSeconds, int kindInt )
	{
		var k = (ThornsGameplayToastKind)Math.Clamp( kindInt, 0, 5 );
		_hud.Toast.ReceiveFromNetwork( message, durationSeconds, k );
	}

	[Rpc.Owner]
	internal void RpcReceiveTameStunBanner( string title, string subtitle, float durationSeconds ) =>
		_hud.Toast.ReceiveTameStunBannerFromNetwork( title, subtitle, durationSeconds );

	/// <summary>Owner-local: center-left tame HUD card (same stack as stun-for-tame prompt; not the toast rail).</summary>
	public void PushTameHudBanner( string title, string subtitle, float durationSeconds = 4.2f ) =>
		_hud.Toast.PushTameHudBanner( title, subtitle, durationSeconds );

	/// <summary>Transient hint in the center-left HUD column (taming prompts, etc.).</summary>
	public void SetGameplayInteractionHint( string message ) =>
		_hud.Interaction.Set( message, null, default, false );

	public void SetGameplayInteractionHint( string message, Vector3 worldAnchor, bool hasWorldAnchor ) =>
		_hud.Interaction.Set( message, null, worldAnchor, hasWorldAnchor );

	public void SetGameplayInteractionHint( string message, GameObject target, Vector3 fallbackWorldAnchor, bool hasWorldAnchor ) =>
		_hud.Interaction.Set( message, target, fallbackWorldAnchor, hasWorldAnchor );

	public void PushGameplayToast(
		string message,
		float durationSeconds = 3.2f,
		ThornsGameplayToastKind kind = ThornsGameplayToastKind.Positive ) =>
		_hud.Toast.Push( message, durationSeconds, kind );

	void TickGameplayToastsAndHints()
	{
		UpdateInteractionHintWorldPosition();
		_hud.Toast.TickExpire( Time.Now, _ => { } );
	}

	void ClearGameplayToastsAndHints()
	{
		ClearHotTips();
		_hud.Toast.Clear( e => ((IThornsHudPresenter)this).OnToastRemoved( e ) );
		if ( _alertLabel is { IsValid: true } )
			_alertLabel.Text = "";
		_hud.Interaction.Clear();
		ThornsTameHoldHudBridge.Clear();
		SetRadioShopLookPrompt( false );
	}

	public bool IsLocalOwned => Connection.Local is { } lc && GameObject.Network.OwnerId == lc.Id && ThornsPawn.IsLocalConnectionOwner( this );

	void TryInit()
	{
		if ( _root is not null && _root.IsValid )
			return;

		if ( !IsLocalOwned )
			return;

		if ( ThornsWorldBootGate.BlocksLocalOwnerPresentation )
			return;

		BindHudServices();

		if ( !Components.Get<ScreenPanel>( FindMode.EnabledInSelf ).IsValid() )
			_ = Components.Create<ScreenPanel>();

		var sp = Components.Get<ScreenPanel>( FindMode.EnabledInSelf );
		if ( sp.IsValid() )
		{
			sp.AutoScreenScale = true;
			sp.ZIndex = 46;
		}

		if ( Panel is null || !Panel.IsValid )
			return;

		BuildTree();
		Log.Info( "[Thorns UI] ThornsGameShell initialized (TAB shell + HUD)." );
	}

	void BuildTree()
	{
		Panel.AddClass( "thorns-shell-root" );
		Panel.Style.PointerEvents = PointerEvents.None;
		Panel.Style.Width = Length.Fraction( 1f );
		Panel.Style.Height = Length.Fraction( 1f );

		_root = Panel;

		_hudMaskedLayer = ThornsUiPanelAdd.AddChildPanel(Panel,  "thorns-shell-hud-masked" );

		var hudTopLeft = ThornsUiPanelAdd.AddChildPanel( _hudMaskedLayer, "thorns-shell-hud-top-left" );
		hudTopLeft.Style.FlexDirection = FlexDirection.Column;
		hudTopLeft.Style.AlignItems = Align.Stretch;

		var tabMenuHint = ThornsUiPanelAdd.AddChildPanel( hudTopLeft, "thorns-shell-tab-menu-hint" );
		tabMenuHint.Style.PointerEvents = PointerEvents.None;
		tabMenuHint.AddChild( new Label( "TAB -> MENU", "thorns-shell-tab-menu-hint__text" ) ).Style.PointerEvents =
			PointerEvents.None;

		BuildServerChatPanel( hudTopLeft );

		var hudBottomLeft = ThornsUiPanelAdd.AddChildPanel( _hudMaskedLayer, "thorns-shell-hud-bottom-left" );
		hudBottomLeft.Style.FlexDirection = FlexDirection.Column;
		hudBottomLeft.Style.AlignItems = Align.Stretch;

		var statsCard = ThornsUiPanelAdd.AddChildPanel<ThornsUiCardPanel>( hudBottomLeft, "thorns-shell-stats-card" );

		void AddMeter( int i, string cap, string cls )
		{
			var b = statsCard.AddChild( new ThornsUiStatBar( cap, cls ) );
			b.Style.PointerEvents = PointerEvents.None;
			_vitalsBars[i] = b;
		}

		AddMeter( 0, "HP", "thorns-stat--hp" );
		AddMeter( 1, "Food", "thorns-stat--food" );
		AddMeter( 2, "Water", "thorns-stat--water" );
		AddMeter( 3, "Stam", "thorns-stat--stam" );
		AddMeter( 4, "XP", "thorns-stat--xp" );

		_crosshairLayer = BuildCrosshair( _hudMaskedLayer );

		_menuLayer = ThornsUiPanelAdd.AddChildPanel(Panel,  "thorns-shell-menu-layer" );
		_menuLayer.Style.PointerEvents = PointerEvents.All;

		var backdrop = ThornsUiPanelAdd.AddChildPanel(_menuLayer,  "thorns-shell-menu-backdrop" );
		backdrop.Style.PointerEvents = PointerEvents.All;
		backdrop.AddEventListener( "onmousedown", ShellAbsorbMenuBackdropMouseDown ); // absorbs clicks beneath
		backdrop.AddEventListener( "onmouseup", ShellOnMenuBackdropMouseUp );

		_shellMenuCard = ThornsUiPanelAdd.AddChildPanel(_menuLayer,  "thorns-shell-menu-card" );
		_shellMenuCard.Style.PointerEvents = PointerEvents.All;
		_shellMenuCard.AddEventListener( "onmouseup", ShellOnMenuCardMouseUp );

		var header = ThornsUiPanelAdd.AddChildPanel(_shellMenuCard,  "thorns-shell-menu-head" );

		header.AddChild( new Label( "SURVIVAL OPS", "thorns-shell-brand" ) ).Style.PointerEvents =
			PointerEvents.None;

		var tabStrip = ThornsUiPanelAdd.AddChildPanel(header,  "thorns-shell-tabstrip" );

		_tabButtons =
		[
			new ThornsUiTabButton( ThornsMainUiTab.Inventory, "Inventory", () => SetActiveTab( ThornsMainUiTab.Inventory ) ),
			new ThornsUiTabButton( ThornsMainUiTab.Skills, "Skills", () => SetActiveTab( ThornsMainUiTab.Skills ) ),
			new ThornsUiTabButton( ThornsMainUiTab.Tames, "Tames", () => SetActiveTab( ThornsMainUiTab.Tames ) ),
			new ThornsUiTabButton( ThornsMainUiTab.Guild, "Guild", () => SetActiveTab( ThornsMainUiTab.Guild ) ),
			new ThornsUiTabButton( ThornsMainUiTab.Journal, "Journal", () => SetActiveTab( ThornsMainUiTab.Journal ) ),
			new ThornsUiTabButton( ThornsMainUiTab.Settings, "Settings", () => SetActiveTab( ThornsMainUiTab.Settings ) ),
		];
		foreach ( var tb in _tabButtons )
			tabStrip.AddChild( tb );

		var closeHint = header.AddChild( new Label( "TAB · close    ESC · dismiss", "thorns-shell-hint" ) );
		closeHint.Style.PointerEvents = PointerEvents.None;

		_tabBodiesHost = ThornsUiPanelAdd.AddChildPanel(_shellMenuCard,  "thorns-shell-tab-bodies-host" );

		_invBody = _tabBodiesHost.AddChild( new ThornsUiInventoryTabBody() );
		_tabBodies[ThornsMainUiTab.Inventory] = _invBody;
		BuildCraftingFilterButtons();
		WireShellInventoryDragInteractions();

		_skillsTabBody = _tabBodiesHost.AddChild( new ThornsUiSkillsTabBody() );
		_tabBodies[ThornsMainUiTab.Skills] = _skillsTabBody;

		_tamesTabBody = _tabBodiesHost.AddChild( new ThornsUiTamesTabBody( CloseMenu ) );
		_tabBodies[ThornsMainUiTab.Tames] = _tamesTabBody;

		_guildTabBody = _tabBodiesHost.AddChild( new ThornsUiGuildTabBody() );
		_tabBodies[ThornsMainUiTab.Guild] = _guildTabBody;

		_journalTabBody = _tabBodiesHost.AddChild( new ThornsUiJournalTabBody(
			ClientApplyJournalHudPin,
			() => _hud.Menu.ClientPinnedJournalGoalId,
			() => _hud.Menu.ClientJournalHudPinExplicit,
			ClientEnsureDefaultPinnedJournalGoal ) );
		_tabBodies[ThornsMainUiTab.Journal] = _journalTabBody;

		var settings = _tabBodiesHost.AddChild( new ThornsUiSettingsTabBody() );
		_tabBodies[ThornsMainUiTab.Settings] = settings;

		var toolbarHost = ThornsUiPanelAdd.AddChildPanel(Panel,  "thorns-shell-toolbar-dock" );
		toolbarHost.Style.PointerEvents = PointerEvents.All;
		_toolbarDockHost = toolbarHost;

		var hotBarAmmoRow = ThornsUiPanelAdd.AddChildPanel(toolbarHost,  "thorns-shell-hotbar-ammo-row" );
		hotBarAmmoRow.Style.FlexDirection = FlexDirection.Row;
		hotBarAmmoRow.Style.AlignItems = Align.Center;
		hotBarAmmoRow.Style.JustifyContent = Justify.Center;

		var hotRow = ThornsUiPanelAdd.AddChildPanel(hotBarAmmoRow,  "thorns-hotbar-row" );
		for ( var i = 0; i < ThornsInventory.HotbarSlotCount; i++ )
		{
			var k = i;
			var cs = hotRow.AddChild( new ThornsUiGridSlot( i, toolbar: true ) );
			cs.OnInventoryPointerDown = ( idx, btn ) => ShellOnDockHotbarMouseDown( idx, btn );
			cs.OnInventoryPointerUp = ShellOnDockHotbarMouseUp;
			cs.OnHoverEnter = ShellOnDockToolbarHoverEnter;
			cs.OnHoverLeave = ShellOnDockToolbarHoverLeave;
			_hotbarSlots[i] = cs;
		}

		_toolbarAmmoMiniLabel = hotBarAmmoRow.AddChild(
			new Label( "", "thorns-shell-toolbar-ammo-mini thorns-shell-toolbar-ammo-mini--hidden" ) );
		_toolbarAmmoMiniLabel.Style.PointerEvents = PointerEvents.None;

		ApplyMenuVisibility();
		ApplyTabBodiesVisibility();
		RefreshTabsVisual();
		UpdateInventoryContextPlaceholder();
		EnsureShellDamageVignetteLayer();
		EnsureShellLevelUpVignetteLayer();
	}

	/// <summary>Local owner: brief red screen-edge vignette when HP drops (see <see cref="ThornsHealth.RpcDamagedNotify"/>).</summary>
	public void NotifyLocalDamageVignette( float lastDamage, float healthAfter )
	{
		if ( lastDamage <= 0.001f )
			return;
		if ( Panel is null || !Panel.IsValid )
			return;

		EnsureShellDamageVignetteLayer();
		var denom = Math.Max( 1f, lastDamage + Math.Max( 0f, healthAfter ) );
		var hitFrac = Math.Clamp( lastDamage / denom, 0.05f, 1f );
		var inner = Math.Clamp( 0.56f + hitFrac * 1.38f, 0.52f, 1f );
		var peak = Math.Clamp( inner * 1.32f, 0f, 1f );
		_shellDamageVignetteDisplay01 = Math.Max( _shellDamageVignetteDisplay01, peak );
		_shellDamageVignetteDecayStart = Time.Now + 0.14;
		_shellDamageVignetteFadeEnd = _shellDamageVignetteDecayStart + 0.72;
	}

	void EnsureShellDamageVignetteLayer()
	{
		if ( _shellDamageVignetteLayer is { IsValid: true } )
			return;
		if ( Panel is null || !Panel.IsValid )
			return;

		_shellDamageVignetteLayer = ThornsUiPanelAdd.AddChildPanel( Panel, "thorns-shell-damage-vignette thorns-damage-vignette-root" );
		_shellDamageVignetteLayer.Style.PointerEvents = PointerEvents.None;
		_shellDamageVignetteLayer.Style.Position = PositionMode.Absolute;
		_shellDamageVignetteLayer.Style.Left = 0;
		_shellDamageVignetteLayer.Style.Top = 0;
		_shellDamageVignetteLayer.Style.Right = 0;
		_shellDamageVignetteLayer.Style.Bottom = 0;
		_shellDamageVignetteLayer.Style.ZIndex = 125;
		_shellDamageVignetteLayer.Style.Opacity = 0f;
	}

	void TickShellDamageVignettePresentation()
	{
		if ( _shellDamageVignetteLayer is null || !_shellDamageVignetteLayer.IsValid )
			return;

		var now = Time.Now;
		if ( now < _shellDamageVignetteDecayStart )
		{
			_shellDamageVignetteLayer.Style.Opacity = _shellDamageVignetteDisplay01;
			return;
		}

		if ( now >= _shellDamageVignetteFadeEnd )
		{
			_shellDamageVignetteLayer.Style.Opacity = 0f;
			_shellDamageVignetteDisplay01 = 0f;
			return;
		}

		var span = Math.Max( 0.001, _shellDamageVignetteFadeEnd - _shellDamageVignetteDecayStart );
		var t = (float)( ( now - _shellDamageVignetteDecayStart ) / span );
		var fade = MathF.Pow( 1f - t, 1.35f );
		_shellDamageVignetteLayer.Style.Opacity = _shellDamageVignetteDisplay01 * fade;
	}

	void EnsureShellLevelUpVignetteLayer()
	{
		if ( _shellLevelUpVignetteLayer is { IsValid: true } )
			return;
		if ( Panel is null || !Panel.IsValid )
			return;

		_shellLevelUpVignetteLayer = ThornsUiPanelAdd.AddChildPanel( Panel, "thorns-shell-levelup-vignette thorns-levelup-vignette-root" );
		_shellLevelUpVignetteLayer.Style.PointerEvents = PointerEvents.None;
		_shellLevelUpVignetteLayer.Style.Position = PositionMode.Absolute;
		_shellLevelUpVignetteLayer.Style.Left = 0;
		_shellLevelUpVignetteLayer.Style.Top = 0;
		_shellLevelUpVignetteLayer.Style.Right = 0;
		_shellLevelUpVignetteLayer.Style.Bottom = 0;
		_shellLevelUpVignetteLayer.Style.ZIndex = 124;
		_shellLevelUpVignetteLayer.Style.Opacity = 0f;
	}

	/// <summary>Local owner: brief gold screen-edge cue (left-weighted) + XP bar glow; level-up copy is shown on the center-left HUD card via <see cref="PushGameplayToast"/>.</summary>
	public void NotifyLocalLevelUpFlash()
	{
		if ( !IsLocalOwned )
			return;
		if ( Panel is null || !Panel.IsValid )
			return;

		EnsureShellLevelUpVignetteLayer();
		var peak = 0.58f;
		_shellLevelUpVignetteDisplay01 = Math.Max( _shellLevelUpVignetteDisplay01, peak );
		_shellLevelUpVignetteDecayStart = Time.Now + 0.12;
		_shellLevelUpVignetteFadeEnd = _shellLevelUpVignetteDecayStart + 0.95;
		_shellLevelUpBarGlowUntil = Time.Now + 2.15;
		if ( _vitalsBars[4] is { IsValid: true } )
			_vitalsBars[4].SetClass( "thorns-stat-bar--levelup-glow", true );
	}

	void TickShellLevelUpCelebrationPresentation()
	{
		var now = Time.Now;
		if ( _shellLevelUpVignetteLayer is { IsValid: true } )
		{
			if ( now < _shellLevelUpVignetteDecayStart )
				_shellLevelUpVignetteLayer.Style.Opacity = _shellLevelUpVignetteDisplay01;
			else if ( now >= _shellLevelUpVignetteFadeEnd )
			{
				_shellLevelUpVignetteLayer.Style.Opacity = 0f;
				_shellLevelUpVignetteDisplay01 = 0f;
			}
			else
			{
				var span = Math.Max( 0.001, _shellLevelUpVignetteFadeEnd - _shellLevelUpVignetteDecayStart );
				var t = (float)( ( now - _shellLevelUpVignetteDecayStart ) / span );
				var fade = MathF.Pow( 1f - t, 1.25f );
				_shellLevelUpVignetteLayer.Style.Opacity = _shellLevelUpVignetteDisplay01 * fade;
			}
		}

		if ( _vitalsBars[4] is { IsValid: true } && _shellLevelUpBarGlowUntil > 0.0 && Time.Now >= _shellLevelUpBarGlowUntil )
		{
			_vitalsBars[4].SetClass( "thorns-stat-bar--levelup-glow", false );
			_shellLevelUpBarGlowUntil = 0.0;
		}
	}

	void WireShellInventoryDragInteractions()
	{
		foreach ( var s in _invBody.BackpackSlots )
		{
			s.Style.PointerEvents = PointerEvents.All;
			s.OnInventoryPointerDown = ShellOnBackpackMouseDown;
			s.OnInventoryPointerUp = ShellOnBackpackMouseUp;
			s.OnHoverEnter = ShellOnInventorySlotHoverEnter;
			s.OnHoverLeave = ShellOnInventorySlotHoverLeave;
		}

		foreach ( var s in _invBody.MenuToolbarSlots )
		{
			s.Style.PointerEvents = PointerEvents.All;
			s.OnInventoryPointerDown = ShellOnMenuToolbarMouseDown;
			s.OnInventoryPointerUp = ShellOnMenuToolbarMouseUp;
			s.OnHoverEnter = ShellOnInventorySlotHoverEnter;
			s.OnHoverLeave = ShellOnInventorySlotHoverLeave;
		}

		foreach ( var a in _invBody.ArmorSlots )
		{
			a.OnArmorPointerDown = ShellOnArmorSlotMouseDown;
			a.OnArmorPointerUp = ShellOnArmorSlotMouseUp;
			a.OnArmorHoverEnter = ShellOnArmorSlotHoverEnter;
			a.OnArmorHoverLeave = ShellOnArmorSlotHoverLeave;
		}
	}

	void WireShellDockHotbarPointerUp()
	{
		if ( _hotbarSlots is null )
			return;

		foreach ( var cs in _hotbarSlots )
		{
			if ( cs is null )
				continue;

			cs.OnInventoryPointerUp = ShellOnDockHotbarMouseUp;
		}
	}

	void ShellSelectInspectSlot( int slotIndex )
	{
		_contextCraftInspectOutputItemId = "";
		_selectedInventorySlot = slotIndex;
		_selectedArmorSlot = -1;
		UpdateInventoryContextPlaceholder();
		var syncInv = Components.Get<ThornsInventory>();
		if ( syncInv.IsValid() )
			_inspectContextLastInvMirrorRev = syncInv.ClientMirrorRevision;

		foreach ( var s in _invBody.BackpackSlots )
			s.SetSelected( s.SlotIndex == slotIndex );

		for ( var i = 0; i < _invBody.MenuToolbarSlots.Length; i++ )
			_invBody.MenuToolbarSlots[i].SetSelected( i == slotIndex && slotIndex < ThornsInventory.HotbarSlotCount );

		for ( var i = 0; i < _hotbarSlots.Length; i++ )
			_hotbarSlots[i].SetSelected( i == slotIndex && slotIndex < ThornsInventory.HotbarSlotCount );

		foreach ( var a in _invBody.ArmorSlots )
			a.SetSelected( false );
	}

	void ShellSelectInspectArmorSlot( int armorSlotIndex )
	{
		_contextCraftInspectOutputItemId = "";
		_selectedInventorySlot = -1;
		_selectedArmorSlot = armorSlotIndex;
		UpdateInventoryContextPlaceholder();

		foreach ( var s in _invBody.BackpackSlots )
			s.SetSelected( false );

		for ( var i = 0; i < _invBody.MenuToolbarSlots.Length; i++ )
			_invBody.MenuToolbarSlots[i].SetSelected( false );

		for ( var i = 0; i < _hotbarSlots.Length; i++ )
			_hotbarSlots[i].SetSelected( false );

		foreach ( var a in _invBody.ArmorSlots )
			a.SetSelected( a.ArmorSlotIndex == armorSlotIndex );

		var armor = Components.Get<ThornsArmorEquipment>();
		if ( armor.IsValid() )
			_inspectContextLastArmorMirrorRev = armor.ClientArmorMirrorRevision;
	}

	void ShellSelectCraftOutputInspect( string outputItemId )
	{
		if ( string.IsNullOrWhiteSpace( outputItemId ) )
			return;

		_contextCraftInspectOutputItemId = outputItemId.Trim();
		_selectedInventorySlot = -1;
		_selectedArmorSlot = -1;

		foreach ( var s in _invBody.BackpackSlots )
			s.SetSelected( false );

		for ( var i = 0; i < _invBody.MenuToolbarSlots.Length; i++ )
			_invBody.MenuToolbarSlots[i].SetSelected( false );

		foreach ( var a in _invBody.ArmorSlots )
			a.SetSelected( false );

		RefreshToolbarSelectionVisual();
		UpdateInventoryContextPlaceholder();
		var syncInvCraft = Components.Get<ThornsInventory>();
		if ( syncInvCraft.IsValid() )
			_inspectContextLastInvMirrorRev = syncInvCraft.ClientMirrorRevision;
	}

	void ShellOnDockHotbarMouseDown( int slotIndex, MouseButtons btn )
	{
		if ( !MenuOpen )
		{
			var inv = Components.Get<ThornsInventory>();
			if ( btn == MouseButtons.Right && inv.IsValid() )
				inv.RequestUseItemFromSlot( slotIndex );
			else if ( btn == MouseButtons.Left )
				ThornsHotbarEquipStub( slotIndex );

			return;
		}

		ShellOnMenuToolbarMouseDown( slotIndex, btn );
	}

	void ShellOnDockHotbarMouseUp( int slotIndex, MouseButtons btn )
	{
		if ( !MenuOpen )
			return;

		ShellOnInventorySlotMouseUp( slotIndex, btn );
	}

	void ShellOnBackpackMouseDown( int slotIndex, MouseButtons btn ) =>
		ShellOnInventorySlotMouseDown( slotIndex, btn, preferHotbarSelectOnEmpty: false );

	void ShellOnMenuToolbarMouseDown( int slotIndex, MouseButtons btn ) =>
		ShellOnInventorySlotMouseDown( slotIndex, btn, preferHotbarSelectOnEmpty: true );

	void ShellOnBackpackMouseUp( int slotIndex, MouseButtons btn ) =>
		ShellOnInventorySlotMouseUp( slotIndex, btn );

	void ShellOnMenuToolbarMouseUp( int slotIndex, MouseButtons btn ) =>
		ShellOnInventorySlotMouseUp( slotIndex, btn );

	void ShellAbsorbMenuBackdropMouseDown( PanelEvent e )
	{
	}

	void ShellOnMenuBackdropMouseUp( PanelEvent e )
	{
		ShellHandleMenuChromeMouseUp();
	}

	void ShellOnMenuCardMouseUp( PanelEvent e )
	{
		if ( e.Target != _shellMenuCard )
			return;

		ShellHandleMenuChromeMouseUp();
	}

	void ShellHandleMenuChromeMouseUp()
	{
		if ( !MenuOpen )
			return;

		Log.Info( "[Thorns][Shell DnD] mouse up (menu chrome / backdrop)" );
		if ( _shellPendingMoveSlot.HasValue || _shellPendingArmorSlot.HasValue )
		{
			_shellQueuedCentralRelease = true;
			return;
		}

		ShellHandleLeftUp( inventorySlotFromPanel: null, armorSlotFromPanel: null );
	}

	void ShellOnInventorySlotMouseUp( int slotIndex, MouseButtons btn )
	{
		if ( !MenuOpen || btn != MouseButtons.Left )
			return;

		Log.Info( $"[Thorns][Shell DnD] mouse up on inventory grid slot={slotIndex}" );
		if ( _shellPendingMoveSlot.HasValue || _shellPendingArmorSlot.HasValue )
		{
			_shellQueuedCentralRelease = true;
			return;
		}

		ShellHandleLeftUp( inventorySlotFromPanel: slotIndex, armorSlotFromPanel: null );
	}

	void ShellClearPointerPress()
	{
		_shellPressInventorySlot = null;
		_shellPressArmorSlot = null;
		_shellPressSyntheticCursorReady = false;
	}

	void ShellBeginPointerPressTracking()
	{
		_shellPressStartScreen = Mouse.Position;
		_shellPressSyntheticScreen = _shellPressStartScreen;
		_shellPressLastHardwareMouse = _shellPressStartScreen;
		_shellPressSyntheticCursorReady = true;
	}

	Vector2 ShellAdvancePressSyntheticCursor()
	{
		if ( !_shellPressSyntheticCursorReady )
			ShellBeginPointerPressTracking();

		var hw = Mouse.Position;
		const float hwEpsSq = 0.25f * 0.25f;
		if ( (hw - _shellPressLastHardwareMouse).LengthSquared > hwEpsSq )
		{
			_shellPressSyntheticScreen = hw;
			_shellPressLastHardwareMouse = hw;
			return _shellPressSyntheticScreen;
		}

		_shellPressSyntheticScreen += ShellUiPointerFrameDelta();
		_shellPressLastHardwareMouse = hw;
		return _shellPressSyntheticScreen;
	}

	/// <summary>
	/// Frame delta for integrating a synthetic cursor when hardware <see cref="Mouse.Position"/> stalls.
	/// Combines <see cref="Input.MouseDelta"/> with <see cref="Input.AnalogLook"/> (horizontal=yaw, vertical=-pitch).
	/// </summary>
	static Vector2 ShellUiPointerFrameDelta()
	{
		var d = Input.MouseDelta;
		var look = Input.AnalogLook;
		var a = new Vector2( look.yaw, -look.pitch );
		// Prefer summing both: with the menu open, delta is sometimes split across channels for slow moves.
		return d + a;
	}

	void ShellPromotePressCursorToDragCursor()
	{
		if ( !_shellPressSyntheticCursorReady )
			ShellBeginPointerPressTracking();

		_shellDnDSyntheticScreen = _shellPressSyntheticScreen;
		_shellDnDLastHardwareMouse = _shellPressLastHardwareMouse;
		_shellDnDSyntheticCursorReady = true;
	}

	/// <summary>Starts real drag after pointer moved past threshold while LMB held.</summary>
	void ShellTickDragThreshold()
	{
		if ( !MenuOpen )
			return;

		if ( _shellPendingMoveSlot.HasValue || _shellPendingArmorSlot.HasValue )
			return;

		if ( !_shellPressInventorySlot.HasValue && !_shellPressArmorSlot.HasValue )
			return;

		var p = ShellAdvancePressSyntheticCursor();
		if ( (p - _shellPressStartScreen).LengthSquared < ShellDragThresholdPx * ShellDragThresholdPx )
			return;

		if ( _shellPressInventorySlot is int invSlot )
		{
			Log.Info( $"[Thorns][Shell DnD] drag threshold crossed — starting drag from inv slot {invSlot}" );
			ShellBeginDragInventorySlot( invSlot );
			return;
		}

		if ( _shellPressArmorSlot is int arSlot )
		{
			var armor = Components.Get<ThornsArmorEquipment>();
			Log.Info( $"[Thorns][Shell DnD] drag threshold crossed — starting armor drag slot {arSlot}" );
			ShellBeginDragArmorSlot( arSlot, armor );
		}
	}

	/// <summary>Fallback for cases where mouse capture sends release to the press source or no slot panel at all.</summary>
	void ShellTickDragReleaseFallback()
	{
		if ( !MenuOpen )
			return;

		if ( !_shellPendingMoveSlot.HasValue && !_shellPendingArmorSlot.HasValue )
			return;

		if ( !_shellQueuedCentralRelease && !(Input.Released( "Attack1" ) || Input.Released( "attack1" )) )
			return;

		_shellQueuedCentralRelease = false;
		Log.Info( "[Thorns][Shell DnD] global mouse release fallback" );
		ShellHandleLeftUp( inventorySlotFromPanel: null, armorSlotFromPanel: null );
	}

	/// <param name="inventorySlotFromPanel">Grid/hotbar slot when mouse-up fired on that control; null otherwise.</param>
	/// <param name="armorSlotFromPanel">Armor equip index when mouse-up fired on that control; null otherwise.</param>
	void ShellHandleLeftUp( int? inventorySlotFromPanel, int? armorSlotFromPanel )
	{
		if ( !MenuOpen )
			return;

		var hoverInv = _shellHoverSlot;
		var hoverArmor = _shellHoverArmorSlot;
		Log.Info(
			$"[Thorns][Shell DnD] ShellHandleLeftUp invSlot={inventorySlotFromPanel?.ToString() ?? "null"} armorSlot={armorSlotFromPanel?.ToString() ?? "null"} hoverInv={hoverInv} hoverArmor={hoverArmor}" );

		if ( _shellPendingMoveSlot.HasValue || _shellPendingArmorSlot.HasValue )
		{
			ShellFinalizeDragUsingHover( inventorySlotFromPanel, armorSlotFromPanel );
			return;
		}

		if ( _shellPressInventorySlot.HasValue || _shellPressArmorSlot.HasValue )
		{
			if ( _shellPressArmorSlot.HasValue )
			{
				var armorInspectIdx = armorSlotFromPanel ?? hoverArmor ?? _shellPressArmorSlot;
				if ( armorInspectIdx.HasValue )
				{
					Log.Info( $"[Thorns][Shell DnD] pointer up - inspect armor slot {armorInspectIdx.Value} (no drag)" );
					ShellSelectInspectArmorSlot( armorInspectIdx.Value );
				}

				ShellClearPointerPress();
				return;
			}

			var inspectIdx = inventorySlotFromPanel ?? hoverInv ?? _shellPressInventorySlot;
			if ( inspectIdx.HasValue )
			{
				Log.Info( $"[Thorns][Shell DnD] pointer up — inspect slot {inspectIdx.Value} (no drag)" );
				ShellSelectInspectSlot( inspectIdx.Value );
			}

			ShellClearPointerPress();
		}
	}

	/// <summary>Merges hit-test, hover events, and mouse-up panel — capture / stale root mouse must not lose the real drop slot.</summary>
	void ShellFinalizeDragUsingHover( int? inventorySlotFromRelease, int? armorSlotFromRelease )
	{
		var eventHoverInv = _shellHoverSlot;
		var eventHoverArmor = _shellHoverArmorSlot;

		ShellUpdateDropTargetUnderCursor();

		var hitInv = _shellHoverSlot;
		var hitArmor = _shellHoverArmorSlot;

		var srcInv = _shellPendingMoveSlot;
		var srcArmor = _shellPendingArmorSlot;

		var releaseInv = inventorySlotFromRelease;
		var releaseArmor = armorSlotFromRelease;
		if ( srcInv.HasValue && releaseInv == srcInv )
			releaseInv = null;
		if ( srcArmor.HasValue && releaseArmor == srcArmor )
			releaseArmor = null;

		int? dropInv = hitInv ?? eventHoverInv ?? _shellLastValidDropSlot ?? releaseInv;
		int? dropArmor = hitArmor ?? eventHoverArmor ?? _shellLastValidDropArmorSlot ?? releaseArmor;

		if ( releaseInv.HasValue )
			dropInv = releaseInv;

		if ( releaseArmor.HasValue )
			dropArmor = releaseArmor;

		if ( srcInv.HasValue && inventorySlotFromRelease == srcInv && eventHoverInv.HasValue && eventHoverInv != srcInv )
			dropInv = eventHoverInv;

		if ( srcArmor.HasValue && armorSlotFromRelease == srcArmor && eventHoverArmor.HasValue && eventHoverArmor != srcArmor )
			dropArmor = eventHoverArmor;

		if ( srcArmor.HasValue && armorSlotFromRelease == srcArmor && eventHoverInv.HasValue )
			dropInv = eventHoverInv;

		if ( srcInv.HasValue && inventorySlotFromRelease == srcInv && eventHoverArmor.HasValue )
			dropArmor = eventHoverArmor;

		_shellHoverSlot = dropInv;
		_shellHoverArmorSlot = dropArmor;

		Log.Info( $"[Thorns][Shell DnD] finalize drag dropTargetInv={dropInv} dropTargetArmor={dropArmor}" );

		if ( _shellPendingArmorSlot.HasValue )
		{
			if ( !dropInv.HasValue )
			{
				Log.Info( "[Thorns][Shell DnD] cancel armor drag — no inventory drop target" );
				ShellClearDragState();
				return;
			}

			ShellFinalizeInventoryDragDropOnInventorySlot( dropInv.Value );
			return;
		}

		if ( !_shellPendingMoveSlot.HasValue )
		{
			ShellClearDragState();
			return;
		}

		if ( dropArmor.HasValue )
			ShellFinalizeInventoryDragDropOnArmorSlot();
		else if ( dropInv.HasValue )
			ShellFinalizeInventoryDragDropOnInventorySlot( dropInv.Value );
		else
		{
			Log.Info( "[Thorns][Shell DnD] cancel inv drag — no drop target" );
			ShellClearDragState();
		}
	}

	void ShellOnInventorySlotMouseDown( int slotIndex, MouseButtons btn, bool preferHotbarSelectOnEmpty )
	{
		if ( !MenuOpen )
			return;

		var inv = Components.Get<ThornsInventory>();
		var armor = Components.Get<ThornsArmorEquipment>();
		if ( !inv.IsValid() )
			return;

		if ( btn == MouseButtons.Right )
		{
			if ( _shellPendingMoveSlot.HasValue || _shellPendingArmorSlot.HasValue )
			{
				ShellClearDragState();
				return;
			}

			inv.RequestUseItemFromSlot( slotIndex );
			return;
		}

		if ( btn != MouseButtons.Left )
			return;

		if ( _shellPendingMoveSlot.HasValue || _shellPendingArmorSlot.HasValue )
			return;

		if ( Input.Keyboard.Down( "Shift" ) )
		{
			ThornsInventoryClientTransfer.SubmitShiftEquipArmorFromInventorySlot( armor, slotIndex );
			return;
		}

		if ( !inv.TryGetClientMirrorSlot( slotIndex, out var net )
		     || net.Quantity <= 0 || string.IsNullOrWhiteSpace( net.ItemId ) )
		{
			Log.Info( $"[Thorns][Shell DnD] pointer down empty slot={slotIndex} (inspect only)" );
			ShellSelectInspectSlot( slotIndex );

			if ( preferHotbarSelectOnEmpty && slotIndex >= 0 && slotIndex < ThornsInventory.HotbarSlotCount )
				ThornsHotbarEquipStub( slotIndex );

			ShellClearDragState();
			return;
		}

		_shellPressArmorSlot = null;
		_shellPressInventorySlot = slotIndex;
		ShellBeginPointerPressTracking();
		Log.Info( $"[Thorns][Shell DnD] pointer down potential drag / click inspect slot={slotIndex} (await threshold or release)" );
	}

	void ShellOnArmorSlotMouseDown( int armorIdx, MouseButtons btn )
	{
		if ( !MenuOpen || btn != MouseButtons.Left )
			return;

		var armor = Components.Get<ThornsArmorEquipment>();
		if ( !armor.IsValid() )
			return;

		if ( _shellPendingMoveSlot.HasValue || _shellPendingArmorSlot.HasValue )
			return;

		if ( Input.Keyboard.Down( "Shift" ) )
		{
			armor.GetClientMirrorEquippedPiece( armorIdx, out var id, out _ );
			if ( !string.IsNullOrWhiteSpace( id ) )
				armor.RequestUnequipArmor( armorIdx );

			ShellClearDragState();
			return;
		}

		armor.GetClientMirrorEquippedPiece( armorIdx, out var pieceId, out _ );
		if ( string.IsNullOrWhiteSpace( pieceId ) )
			return;

		_shellPressInventorySlot = null;
		_shellPressArmorSlot = armorIdx;
		ShellBeginPointerPressTracking();
		Log.Info( $"[Thorns][Shell DnD] pointer down armor slot={armorIdx} (await threshold or release)" );
	}

	void ShellBeginDragInventorySlot( int idx )
	{
		var inv = Components.Get<ThornsInventory>();
		if ( !inv.IsValid() )
			return;
		if ( !inv.TryGetClientMirrorSlot( idx, out var slot ) || slot.Quantity <= 0
		     || string.IsNullOrWhiteSpace( slot.ItemId ) )
			return;

		ShellPromotePressCursorToDragCursor();
		ShellClearPointerPress();
		_shellPendingArmorSlot = null;
		_shellPendingMoveSlot = idx;
		_shellHoverSlot = idx;
		_shellHoverArmorSlot = null;
		_shellUiPointerInventorySlot = idx;
		_shellUiPointerArmorSlot = null;
		Log.Info( $"[Thorns][Shell DnD] drag start from inventory slot {idx}" );
		ShellSetBackpackGridScrollLocked( true );
		ShellRebuildDragGhost();
	}

	void ShellBeginDragArmorSlot( int armorSlotIndex, ThornsArmorEquipment armor )
	{
		if ( !armor.IsValid() )
			return;
		armor.GetClientMirrorEquippedPiece( armorSlotIndex, out var id, out _ );
		if ( string.IsNullOrWhiteSpace( id ) )
			return;

		ShellPromotePressCursorToDragCursor();
		ShellClearPointerPress();
		_shellPendingMoveSlot = null;
		_shellPendingArmorSlot = armorSlotIndex;
		_shellHoverSlot = null;
		_shellHoverArmorSlot = armorSlotIndex;
		_shellUiPointerArmorSlot = armorSlotIndex;
		_shellUiPointerInventorySlot = null;
		Log.Info( $"[Thorns][Shell DnD] drag start from armor slot {armorSlotIndex}" );
		ShellSetBackpackGridScrollLocked( true );
		ShellRebuildDragGhost();
	}

	void ShellOnArmorSlotMouseUp( int armorIdx, MouseButtons btn )
	{
		if ( !MenuOpen || btn != MouseButtons.Left )
			return;

		Log.Info( $"[Thorns][Shell DnD] mouse up on armor slot={armorIdx}" );
		if ( _shellPendingMoveSlot.HasValue || _shellPendingArmorSlot.HasValue )
		{
			_shellQueuedCentralRelease = true;
			return;
		}

		ShellHandleLeftUp( inventorySlotFromPanel: null, armorSlotFromPanel: armorIdx );
	}

	/// <summary>Completes a drag when the cursor releases over an inventory/hotbar grid slot.</summary>
	void ShellFinalizeInventoryDragDropOnInventorySlot( int dropInventorySlotIndex )
	{
		if ( !MenuOpen )
		{
			ShellClearDragState();
			return;
		}

		var inv = Components.Get<ThornsInventory>();
		var armor = Components.Get<ThornsArmorEquipment>();
		if ( !inv.IsValid() )
		{
			ShellClearDragState();
			return;
		}

		if ( _shellPendingArmorSlot.HasValue )
		{
			var a = _shellPendingArmorSlot.Value;
			ShellClearDragState();
			ThornsInventoryClientTransfer.SubmitUnequipArmorToInventorySlot( armor, a, dropInventorySlotIndex );
			return;
		}

		if ( !_shellPendingMoveSlot.HasValue )
		{
			ShellClearDragState();
			return;
		}

		var from = _shellPendingMoveSlot.Value;
		ShellClearDragState();

		if ( Input.Keyboard.Down( "Shift" ) )
		{
			ThornsInventoryClientTransfer.SubmitShiftEquipArmorFromInventorySlot( armor, from );
			return;
		}

		var idx = dropInventorySlotIndex;
		if ( from == idx )
			return;

		ThornsInventoryClientTransfer.SubmitMoveOrSwapInventorySlots( inv, from, idx );
	}

	/// <summary>Completes a drag when the cursor releases over an armor equip slot (inventory → armor).</summary>
	void ShellFinalizeInventoryDragDropOnArmorSlot()
	{
		if ( !MenuOpen )
		{
			ShellClearDragState();
			return;
		}

		var inv = Components.Get<ThornsInventory>();
		var armor = Components.Get<ThornsArmorEquipment>();
		if ( !inv.IsValid() )
		{
			ShellClearDragState();
			return;
		}

		if ( !_shellPendingMoveSlot.HasValue )
		{
			ShellClearDragState();
			return;
		}

		var from = _shellPendingMoveSlot.Value;
		ShellClearDragState();

		if ( Input.Keyboard.Down( "Shift" ) )
		{
			ThornsInventoryClientTransfer.SubmitShiftEquipArmorFromInventorySlot( armor, from );
			return;
		}

		ThornsInventoryClientTransfer.SubmitEquipArmorFromInventorySlot( armor, from );
	}

	void ShellOnInventorySlotHoverEnter( int slotIndex )
	{
		if ( !_shellPendingMoveSlot.HasValue && !_shellPendingArmorSlot.HasValue
		     && !_shellPressInventorySlot.HasValue && !_shellPressArmorSlot.HasValue )
			return;

		_shellUiPointerInventorySlot = slotIndex;
		_shellUiPointerArmorSlot = null;
		_shellHoverSlot = slotIndex;
		_shellHoverArmorSlot = null;
		ShellRefreshDragSlotDecorations();
	}

	void ShellOnInventorySlotHoverLeave( int slotIndex )
	{
		if ( _shellHoverSlot == slotIndex )
			_shellHoverSlot = null;

		if ( _shellUiPointerInventorySlot == slotIndex )
			_shellUiPointerInventorySlot = null;

		ShellRefreshDragSlotDecorations();
	}

	void ShellOnArmorSlotHoverEnter( int armorIdx )
	{
		if ( !_shellPendingMoveSlot.HasValue && !_shellPendingArmorSlot.HasValue
		     && !_shellPressInventorySlot.HasValue && !_shellPressArmorSlot.HasValue )
			return;

		_shellUiPointerArmorSlot = armorIdx;
		_shellUiPointerInventorySlot = null;
		_shellHoverArmorSlot = armorIdx;
		_shellHoverSlot = null;
		ShellRefreshDragSlotDecorations();
	}

	void ShellOnArmorSlotHoverLeave( int armorIdx )
	{
		if ( _shellHoverArmorSlot == armorIdx )
			_shellHoverArmorSlot = null;

		if ( _shellUiPointerArmorSlot == armorIdx )
			_shellUiPointerArmorSlot = null;

		ShellRefreshDragSlotDecorations();
	}

	void ShellOnDockToolbarHoverEnter( int slotIndex ) =>
		ShellOnInventorySlotHoverEnter( slotIndex );

	void ShellOnDockToolbarHoverLeave( int slotIndex ) =>
		ShellOnInventorySlotHoverLeave( slotIndex );

	void ShellClearDragState()
	{
		ShellClearPointerPress();
		_shellPendingMoveSlot = null;
		_shellPendingArmorSlot = null;
		_shellHoverSlot = null;
		_shellHoverArmorSlot = null;
		_shellLastValidDropSlot = null;
		_shellLastValidDropArmorSlot = null;
		_shellUiPointerInventorySlot = null;
		_shellUiPointerArmorSlot = null;
		_shellQueuedCentralRelease = false;
		_shellDnDLastLoggedInv = null;
		_shellDnDLastLoggedArmor = null;
		_shellDnDVerboseTick = 0;
		_shellDnDSyntheticCursorReady = false;
		ShellSetBackpackGridScrollLocked( false );
		ShellDestroyDragGhost();
		ShellRefreshDragSlotDecorations();
	}

	/// <summary>Root has <see cref="PointerEvents.None"/> — menu layer owns pointer; use it for cursor math.</summary>
	Panel ShellCoordPanel => _menuLayer is { IsValid: true } ? _menuLayer : Panel;

	/// <summary>TAB drag preview should live under the menu layer so fractions match pointer hit-testing geometry (root + menu padding mismatch skewed the ghost).</summary>
	Panel ShellInventoryDragGhostHostParent() =>
		_menuLayer is { IsValid: true } ? _menuLayer : Panel;

	/// <summary>Storage overlay drag ghost must share the same basis as <see cref="ShellCurrentMouseScreenPosition"/> (root vs fullscreen layer skew).</summary>
	Panel ShellStorageChestDragGhostHostParent() =>
		_storageChestLayer is { IsValid: true } ? _storageChestLayer : Panel;

	/// <summary>Same idea as <see cref="ShellStorageChestDragGhostHostParent"/> for the campfire fullscreen layer.</summary>
	Panel ShellCampfireDragGhostHostParent() =>
		_campfireLayer is { IsValid: true } ? _campfireLayer : Panel;

	/// <summary>Same idea as <see cref="ShellStorageChestDragGhostHostParent"/> for the workbench fullscreen layer.</summary>
	Panel ShellWorkbenchDragGhostHostParent() =>
		_workbenchLayer is { IsValid: true } ? _workbenchLayer : Panel;

	/// <summary>
	/// Screen point that matches the menu subtree's idea of the pointer (for ghost alignment). Falls back if the coord panel is missing.
	/// </summary>
	Vector2 ShellTabInventoryGhostScreenPoint()
	{
		if ( ShellCoordPanel is not { IsValid: true } cp )
			return ShellCurrentMouseScreenPosition();

		return cp.PanelPositionToScreenPosition( cp.MousePosition );
	}

	void ShellSetBackpackGridScrollLocked( bool locked )
	{
		if ( _invBody?.BackpackGridWrap is not { IsValid: true } wrap )
			return;

		wrap.SetClass( "thorns-inv-grid-wrap--dnd-lock", locked );
		wrap.Style.Overflow = locked ? OverflowMode.Hidden : OverflowMode.Scroll;
	}

	/// <summary>
	/// Prefer a backpack cell that is not the drag source when both overlap (capture / bad ordering); otherwise allow source so drag-cancel still works.
	/// </summary>
	int? ShellHitBackpackSlotUnderCursor( Vector2 screenPos )
	{
		if ( _invBody is null || !_invBody.IsValid )
			return null;

		var slots = _invBody.BackpackSlots;
		var src = _shellPendingMoveSlot;

		if ( src.HasValue )
		{
			foreach ( var s in slots )
			{
				if ( s.SlotIndex == src.Value )
					continue;

				if ( ShellPanelContainsScreenPoint( s, screenPos ) )
					return s.SlotIndex;
			}

			foreach ( var s in slots )
			{
				if ( s.SlotIndex != src.Value )
					continue;

				if ( ShellPanelContainsScreenPoint( s, screenPos ) )
					return src.Value;
			}

			return null;
		}

		foreach ( var s in slots )
		{
			if ( ShellPanelContainsScreenPoint( s, screenPos ) )
				return s.SlotIndex;
		}

		return null;
	}

	static bool ShellPanelContainsScreenPoint( Panel p, Vector2 screenPos )
	{
		if ( p is null || !p.IsValid )
			return false;

		var d = p.ScreenPositionToPanelDelta( screenPos );
		return d.x >= 0f && d.x <= 1f && d.y >= 0f && d.y <= 1f;
	}

	/// <summary>
	/// Screen hit-tests can stay on the drag source while the UI's :hover moved to another cell; merge in pointer-hover slot indices.
	/// </summary>
	void ShellMergeGeomDnDWithUiPointerSlots( ref int? inv, ref int? armor )
	{
		if ( _shellPendingMoveSlot is int srcInv )
		{
			// Armor row: only merge when geometry is still pinned to the backpack source (stale screen pos).
			if ( armor is null && inv == srcInv && _shellUiPointerArmorSlot is int uiAr )
			{
				armor = uiAr;
				inv = null;
				return;
			}

			if ( _shellUiPointerInventorySlot is int uiInv )
			{
				if ( armor is null && !inv.HasValue )
					inv = uiInv;
				else if ( armor is null && inv == srcInv && uiInv != srcInv )
					inv = uiInv;
			}

			return;
		}

		if ( _shellPendingArmorSlot is int srcArm )
		{
			if ( inv is null && armor == srcArm && _shellUiPointerInventorySlot is int uiInvDrop )
				inv = uiInvDrop;
		}
	}

	/// <summary>
	/// Hit-tests slot panels under the cursor each frame while dragging — combined with UI :hover when geometry lags.
	/// </summary>
	void ShellUpdateDropTargetUnderCursor()
	{
		if ( !_shellPendingMoveSlot.HasValue && !_shellPendingArmorSlot.HasValue )
			return;

		if ( Panel is null || !Panel.IsValid || _invBody is null || !_invBody.IsValid )
			return;

		ShellAdvanceSyntheticCursorForDnD();

		var screenPos = ShellCurrentMouseScreenPosition();

		int? inv = null;
		int? armor = null;

		foreach ( var a in _invBody.ArmorSlots )
		{
			if ( !ShellPanelContainsScreenPoint( a, screenPos ) )
				continue;

			armor = a.ArmorSlotIndex;
			inv = null;
			break;
		}

		if ( !armor.HasValue )
			inv = ShellHitBackpackSlotUnderCursor( screenPos );

		if ( !armor.HasValue && !inv.HasValue )
		{
			for ( var i = 0; i < _invBody.MenuToolbarSlots.Length; i++ )
			{
				var s = _invBody.MenuToolbarSlots[i];
				if ( !ShellPanelContainsScreenPoint( s, screenPos ) )
					continue;

				inv = i;
				break;
			}
		}

		if ( !armor.HasValue && !inv.HasValue )
		{
			for ( var i = 0; i < _hotbarSlots.Length; i++ )
			{
				if ( !ShellPanelContainsScreenPoint( _hotbarSlots[i], screenPos ) )
					continue;

				inv = i;
				break;
			}
		}

		ShellMergeGeomDnDWithUiPointerSlots( ref inv, ref armor );

		var prevInv = _shellHoverSlot;
		var prevArmor = _shellHoverArmorSlot;

		if ( !armor.HasValue )
			_shellHoverArmorSlot = null;
		else
			_shellHoverArmorSlot = armor;

		_shellHoverSlot = inv;

		if ( inv.HasValue )
		{
			_shellLastValidDropSlot = inv;
			_shellLastValidDropArmorSlot = null;
		}
		else if ( armor.HasValue )
		{
			_shellLastValidDropArmorSlot = armor;
			_shellLastValidDropSlot = null;
		}

		if ( ShellDnDVerboseLogging )
		{
			_shellDnDVerboseTick++;
			var dnd = _invBody?.InventoryDnDStack;
			var dndPos = dnd is { IsValid: true } ? dnd.MousePosition : default;
			var menuPos = _menuLayer is { IsValid: true } ? _menuLayer.MousePosition : default;
			var rootPos = Panel is { IsValid: true } ? Panel.MousePosition : default;
			Log.Info(
				$"[DnD][verbose] tick={_shellDnDVerboseTick} Mouse.Position={Mouse.Position} syntheticScreen={_shellDnDSyntheticScreen} mouseDelta={Input.MouseDelta} screenHit={screenPos} menuLocal={menuPos} dndStackLocal={dndPos} rootLocal={rootPos} hitInv={inv} hitArmor={armor} pendingMove={_shellPendingMoveSlot} pendingArmor={_shellPendingArmorSlot}" );
		}

		if ( inv != _shellDnDLastLoggedInv || armor != _shellDnDLastLoggedArmor )
		{
			Log.Info(
				$"[DnD] CURRENT DROP TARGET inv={(inv.HasValue ? inv.Value.ToString() : "null")} armor={(armor.HasValue ? armor.Value.ToString() : "null")}" );
			_shellDnDLastLoggedInv = inv;
			_shellDnDLastLoggedArmor = armor;
		}

		if ( prevInv != inv || prevArmor != _shellHoverArmorSlot )
			ShellRefreshDragSlotDecorations();
	}

	Vector2 ShellCurrentMouseScreenPosition()
	{
		if ( _shellPendingMoveSlot.HasValue || _shellPendingArmorSlot.HasValue )
		{
			// Matches hardware cursor + MouseDelta when Mouse.Position stalls under UI drag; seeded on drag promote.
			if ( _shellDnDSyntheticCursorReady )
				return _shellDnDSyntheticScreen;
			// Root Panel has PointerEvents.None; menu captures pointer — Panel.MousePosition is the wrong projection and
			// drifts toward screen edges versus the visible cursor while the ghost maps through ScreenPositionToPanelDelta(Panel).
			var cp = ShellCoordPanel;
			if ( cp is { IsValid: true } )
				return cp.PanelPositionToScreenPosition( cp.MousePosition );
			return Mouse.Position;
		}

		if ( _storageDragFromChest.HasValue )
		{
			if ( _storageChestLayer is { IsValid: true } )
				return _storageChestLayer.PanelPositionToScreenPosition( _storageChestLayer.MousePosition );
			if ( _shellDnDSyntheticCursorReady )
				return _shellDnDSyntheticScreen;
			return Mouse.Position;
		}

		if ( _campfireDragFromCampfire.HasValue )
		{
			if ( _campfireLayer is { IsValid: true } )
				return _campfireLayer.PanelPositionToScreenPosition( _campfireLayer.MousePosition );
			if ( _shellDnDSyntheticCursorReady )
				return _shellDnDSyntheticScreen;
			return Mouse.Position;
		}

		if ( _workbenchDragFromWorkbench.HasValue )
		{
			if ( _workbenchLayer is { IsValid: true } )
				return _workbenchLayer.PanelPositionToScreenPosition( _workbenchLayer.MousePosition );
			if ( _shellDnDSyntheticCursorReady )
				return _shellDnDSyntheticScreen;
			return Mouse.Position;
		}

		return Mouse.Position;
	}

	/// <summary>
	/// Hardware cursor position often freezes during LMB press-drag on UI slots; mouse delta still updates — accumulate into <see cref="_shellDnDSyntheticScreen"/>.
	/// </summary>
	void ShellAdvanceSyntheticCursorForDnD()
	{
		if ( !IsAnyCentralDragActive )
			return;

		var hw = Mouse.Position;

		if ( !_shellDnDSyntheticCursorReady )
		{
			_shellDnDSyntheticScreen = hw;
			_shellDnDLastHardwareMouse = hw;
			_shellDnDSyntheticCursorReady = true;
			return;
		}

		const float hwEpsSq = 0.25f * 0.25f;
		if ( (hw - _shellDnDLastHardwareMouse).LengthSquared > hwEpsSq )
		{
			_shellDnDSyntheticScreen = hw;
			_shellDnDLastHardwareMouse = hw;
			return;
		}

		_shellDnDSyntheticScreen += ShellUiPointerFrameDelta();
		_shellDnDLastHardwareMouse = hw;
	}

	void ShellDestroyDragGhost()
	{
		if ( _shellDragGhost is not null && _shellDragGhost.IsValid )
			_shellDragGhost.Delete();

		_shellDragGhost = null;
	}

	static void ThornsConfigureInventoryDragGhostHost( Panel host, int zIndex )
	{
		host.Style.Position = PositionMode.Absolute;
		host.Style.Width = Length.Pixels( (int)InventoryDragGhostWidthPx );
		host.Style.Height = Length.Pixels( (int)InventoryDragGhostHeightPx );
		host.Style.ZIndex = zIndex;
		host.Style.PointerEvents = PointerEvents.None;
		host.Style.FlexDirection = FlexDirection.Column;
		host.Style.JustifyContent = Justify.FlexStart;
		host.Style.AlignItems = Align.Stretch;
		host.Style.Overflow = OverflowMode.Visible;
	}

	static void ThornsConfigureDragGhostPreviewSlot( ThornsUiGridSlot previewSlot )
	{
		previewSlot.Style.PointerEvents = PointerEvents.None;
		previewSlot.Style.FlexGrow = 1;
		previewSlot.Style.FlexShrink = 0;
		previewSlot.Style.MinHeight = 0;
		previewSlot.Style.Width = Length.Fraction( 1f );
		previewSlot.Style.Height = Length.Fraction( 1f );
	}

	void ShellRebuildDragGhost()
	{
		ShellDestroyDragGhost();

		void FinishGhost( ThornsUiGridSlot previewSlot, bool armorStyleRing )
		{
			ThornsConfigureDragGhostPreviewSlot( previewSlot );
			_shellDragGhost.AddClass( armorStyleRing
				? "thorns-shell-inv-drag-ghost thorns-shell-inv-drag-ghost--armor"
				: "thorns-shell-inv-drag-ghost" );
			_shellDragGhost.Style.MarginLeft = 0;
			_shellDragGhost.Style.MarginTop = 0;
			UpdateShellDragGhostPosition();
		}

		var inv = Components.Get<ThornsInventory>();
		var armor = Components.Get<ThornsArmorEquipment>();

		if ( _shellPendingMoveSlot is int pm && inv.IsValid()
		     && inv.TryGetClientMirrorSlot( pm, out var dragNet )
		     && dragNet.Quantity > 0 && !string.IsNullOrWhiteSpace( dragNet.ItemId ) )
		{
			_shellDragGhost = ThornsUiPanelAdd.AddChildPanel(
				ShellInventoryDragGhostHostParent(), "thorns-shell-inv-drag-ghost-host" );
			ThornsConfigureInventoryDragGhostHost( _shellDragGhost, zIndex: 500 );

			var toolbar = pm >= 0 && pm < ThornsInventory.HotbarSlotCount;
			var preview = _shellDragGhost.AddChild( new ThornsUiGridSlot( pm, toolbar: toolbar ) );
			if ( toolbar )
				preview.SetToolbarFromMirror( dragNet, pm + 1 );
			else
			{
				Color? rowTint = null;
				if ( ThornsUiWeaponInspectFormatting.TryGetWeaponInventoryTitleTint( dragNet, out var wt ) )
					rowTint = wt;
				else if ( ThornsUiArmorInspectFormatting.TryGetArmorInventoryTitleTint( dragNet, out var at ) )
					rowTint = at;
				preview.SetMirrorSlotVisual( dragNet, rowTint );
			}

			FinishGhost( preview, armorStyleRing: false );
			return;
		}

		if ( _shellPendingArmorSlot is int pa && armor.IsValid() )
		{
			armor.GetClientMirrorEquippedPieceFull( pa, out var armId, out var armDur, out var armRoll );
			if ( string.IsNullOrWhiteSpace( armId ) )
				return;

			var armorNet = new ThornsInventorySlotNet
			{
				ItemId = armId,
				Quantity = 1,
				Durability = armDur,
				WeaponInstanceId = "",
				WeaponLoadedAmmo = 0,
				WeaponRollPayload = "",
				ArmorRollPayload = armRoll ?? ""
			};
			if ( ThornsItemRegistry.TryGet( armId, out var adef )
			     && adef.ItemType == ThornsItemType.Armor && adef.ArmorMaxDurability > 0.001f )
				armorNet.HasDurability = 1;

			_shellDragGhost = ThornsUiPanelAdd.AddChildPanel(
				ShellInventoryDragGhostHostParent(), "thorns-shell-inv-drag-ghost-host" );
			ThornsConfigureInventoryDragGhostHost( _shellDragGhost, zIndex: 500 );

			var preview = _shellDragGhost.AddChild( new ThornsUiGridSlot( 0, toolbar: false ) );
			preview.SetMirrorSlotVisual( armorNet, weaponRowTint: null );

			FinishGhost( preview, armorStyleRing: true );
		}
	}

	void UpdateShellDragGhostPosition()
	{
		if ( _shellDragGhost is null || !_shellDragGhost.IsValid || !MenuOpen )
			return;

		if ( !_shellPendingMoveSlot.HasValue && !_shellPendingArmorSlot.HasValue )
			return;

		PositionInventoryDragGhostUnderCursor( _shellDragGhost );
	}

	/// <summary>
	/// Keeps the drag preview under the pointer. Uses <see cref="Panel.ScreenPositionToPanelDelta"/> against <see cref="ghost.Parent"/>
	/// — that parent must be the same panel subtree used to build <see cref="ShellCurrentMouseScreenPosition"/> (TAB: <see cref="ShellInventoryDragGhostHostParent"/>;
	/// storage / campfire / workbench: each fullscreen overlay layer), otherwise the icon drifts from the hardware cursor.
	/// </summary>
	void PositionInventoryDragGhostUnderCursor( Panel ghost )
	{
		if ( ghost is null || !ghost.IsValid )
			return;

		var basis = ghost.Parent;
		if ( basis is null || !basis.IsValid )
			return;

		var screen = MenuOpen && (_shellPendingMoveSlot.HasValue || _shellPendingArmorSlot.HasValue)
			? ShellTabInventoryGhostScreenPoint()
			: ShellCurrentMouseScreenPosition();

		var d = basis.ScreenPositionToPanelDelta( screen );
		var inner = basis.Box.RectInner;
		var w = inner.Width;
		var h = inner.Height;
		if ( w < 0.001f || h < 0.001f )
			return;

		var halfFracW = (InventoryDragGhostWidthPx / w) * 0.5f;
		var halfFracH = (InventoryDragGhostHeightPx / h) * 0.5f;
		ghost.Style.Left = Length.Fraction( d.x - halfFracW );
		ghost.Style.Top = Length.Fraction( d.y - halfFracH );
		ghost.Style.MarginLeft = 0;
		ghost.Style.MarginTop = 0;
	}

	void ShellRefreshDragSlotDecorations()
	{
		if ( _invBody is null || !_invBody.IsValid )
			return;

		var dragging = _shellPendingMoveSlot.HasValue || _shellPendingArmorSlot.HasValue;
		var pressTrack = _shellPressInventorySlot.HasValue || _shellPressArmorSlot.HasValue;
		var slotDecor = dragging || pressTrack;
		var hs = _shellHoverSlot;

		foreach ( var s in _invBody.BackpackSlots )
		{
			var h = slotDecor && hs == s.SlotIndex;
			s.SetHighlighted( h );
			s.SetDragSource( _shellPendingMoveSlot == s.SlotIndex );
		}

		for ( var i = 0; i < _invBody.MenuToolbarSlots.Length; i++ )
		{
			var s = _invBody.MenuToolbarSlots[i];
			var h = slotDecor && hs == i;
			s.SetHighlighted( h );
			s.SetDragSource( _shellPendingMoveSlot == i );
		}

		for ( var i = 0; i < _hotbarSlots.Length; i++ )
		{
			var s = _hotbarSlots[i];
			var h = slotDecor && hs == i;
			s.SetHighlighted( h );
			s.SetDragSource( _shellPendingMoveSlot == i );
		}

		for ( var i = 0; i < _invBody.ArmorSlots.Length; i++ )
		{
			var a = _invBody.ArmorSlots[i];
			var h = slotDecor && _shellHoverArmorSlot == i;
			a.SetHoverDrop( h );
			a.SetDragSource( _shellPendingArmorSlot == i );
		}
	}

	void BuildCraftingFilterButtons()
	{
		if ( _invBody is null || !_invBody.IsValid || _invBody.CraftingFilterHost is null ||
		     !_invBody.CraftingFilterHost.IsValid )
			return;

		_invBody.CraftingFilterHost.DeleteChildren();
		_craftFilterButtons.Clear();
		AddCraftingFilterButton( ThornsCraftingFilter.All, "ALL" );
		AddCraftingFilterButton( ThornsCraftingFilter.WeaponsAmmo, "WEAPONS & AMMO" );
		AddCraftingFilterButton( ThornsCraftingFilter.Tool, "TOOL" );
		AddCraftingFilterButton( ThornsCraftingFilter.Armor, "ARMOR" );
		AddCraftingFilterButton( ThornsCraftingFilter.MedicalSustenance, "MED/SUST" );
		AddCraftingFilterButton( ThornsCraftingFilter.Placeables, "BASE" );
		AddCraftingFilterButton( ThornsCraftingFilter.Random, "RANDOM" );
		UpdateCraftingFilterButtonStyles();
	}

	void AddCraftingFilterButton( ThornsCraftingFilter filter, string label )
	{
		var btn = ThornsUiPanelAdd.AddChildPanel(_invBody.CraftingFilterHost,  "thorns-inv-craft-filter-btn" );
		btn.AddClass( $"thorns-inv-craft-filter-{filter.ToString().ToLowerInvariant()}" );
		btn.Style.PointerEvents = PointerEvents.All;
		_craftFilterButtons[filter] = btn;
		btn.AddEventListener( "onmousedown", _ =>
		{
			if ( _craftUiFilter == filter )
				return;

			_craftUiFilter = filter;
			_craftUiLastFilterCached = (ThornsCraftingFilter)(-1);
			UpdateCraftingFilterButtonStyles();
		} );

		var text = btn.AddChild( new Label( label, "thorns-inv-craft-filter-label" ) );
		text.Style.PointerEvents = PointerEvents.None;
	}

	void UpdateCraftingFilterButtonStyles()
	{
		if ( _invBody is null || !_invBody.IsValid || _invBody.CraftingFilterHost is null ||
		     !_invBody.CraftingFilterHost.IsValid )
			return;

		foreach ( var kv in _craftFilterButtons )
			kv.Value.SetClass( "thorns-inv-craft-filter-btn--active", kv.Key == _craftUiFilter );
	}

	void UpdateInventoryContextPlaceholder()
	{
		if ( _invBody is null || !_invBody.IsValid || _invBody.ContextDetailsBody is null ||
		     !_invBody.ContextDetailsBody.IsValid )
			return;

		var body = _invBody.ContextDetailsBody;
		body.DeleteChildren();

		var craftDisabledFooter = new ThornsUiItemInspectPanel.FooterModel
		{
			EquipEnabled = false,
			UseEnabled = false,
			ModifyEnabled = false,
			DropEnabled = false
		};

		if ( !string.IsNullOrWhiteSpace( _contextCraftInspectOutputItemId ) )
		{
			var itemId = _contextCraftInspectOutputItemId.Trim();
			if ( !ThornsItemRegistry.TryGet( itemId, out var defCraft ) )
			{
				body.AddChild( new Label(
					$"Unknown output · {itemId}",
					"thorns-tab-context-placeholder" ) );
				return;
			}

			var previewNet = new ThornsInventorySlotNet { ItemId = itemId, Quantity = 1 };
			ThornsUiItemInspectPanel.Rebuild( body, previewNet, defCraft, isCraftPreview: true, craftDisabledFooter );
			return;
		}

		var armorEquip = Components.Get<ThornsArmorEquipment>();

		if ( _selectedArmorSlot >= 0 )
		{
			if ( !armorEquip.IsValid() )
			{
				body.AddChild( new Label( "Armor unavailable.", "thorns-tab-context-placeholder" ) );
				return;
			}

			armorEquip.GetClientMirrorEquippedPieceFull( _selectedArmorSlot, out var armorId, out var armorDur, out var armorRoll );
			if ( string.IsNullOrWhiteSpace( armorId ) )
			{
				body.AddChild( new Label( "Armor slot empty.", "thorns-tab-context-placeholder" ) );
				return;
			}

			if ( !ThornsItemRegistry.TryGet( armorId, out var defArmor ) )
			{
				body.AddChild( new Label( $"{armorId.ToUpperInvariant()}\n\nUnknown armor item id.", "thorns-tab-context-placeholder" ) );
				return;
			}

			var armorNet = new ThornsInventorySlotNet
			{
				ItemId = armorId,
				Quantity = 1,
				HasDurability = 1,
				Durability = armorDur,
				ArmorRollPayload = armorRoll ?? ""
			};
			var armorFooter = new ThornsUiItemInspectPanel.FooterModel
			{
				EquipEnabled = false,
				UseEnabled = false,
				ModifyEnabled = false,
				DropEnabled = false
			};
			ThornsUiItemInspectPanel.Rebuild( body, armorNet, defArmor, isCraftPreview: false, armorFooter );
			return;
		}

		if ( _selectedInventorySlot < 0 )
		{
			body.AddChild( new Label(
				"No item selected.\n\nSuggested: craft bandages when you have cloth.",
				"thorns-tab-context-placeholder" ) );
			return;
		}

		var inv = GameObject.Components.Get<ThornsInventory>();
		if ( !inv.IsValid() )
		{
			body.AddChild( new Label( "Inventory unavailable.", "thorns-tab-context-placeholder" ) );
			return;
		}

		if ( !inv.TryGetClientMirrorSlot( _selectedInventorySlot, out var net )
		     || string.IsNullOrWhiteSpace( net.ItemId ) )
		{
			body.AddChild(
				new Label( $"Slot {_selectedInventorySlot}\n(empty)", "thorns-tab-context-placeholder" ) );
			return;
		}

		if ( !ThornsItemRegistry.TryGet( net.ItemId, out var defInspect ) )
		{
			body.AddChild( new Label(
				$"{ThornsUiInventoryFormatting.SlotPrimaryLine( net ).ToUpperInvariant()}\n\nUnknown item id.",
				"thorns-tab-context-placeholder" ) );
			return;
		}

		var slot = _selectedInventorySlot;
		var footer = new ThornsUiItemInspectPanel.FooterModel
		{
			ModifyEnabled = false,
			DropEnabled = net.Quantity > 0,
			OnDrop = () =>
			{
				var invDrop = Components.Get<ThornsInventory>();
				if ( invDrop.IsValid() )
					invDrop.RequestDropInventorySlotToWorld( slot );
			}
		};

		if ( slot < ThornsInventory.HotbarSlotCount && net.Quantity > 0 )
		{
			footer.EquipEnabled = true;
			footer.OnEquip = () => ThornsHotbarEquipStub( slot );
		}
		else if ( defInspect.ItemType == ThornsItemType.Armor && armorEquip.IsValid() )
		{
			footer.EquipEnabled = true;
			footer.OnEquip = () => ThornsInventoryClientTransfer.SubmitEquipArmorFromInventorySlot( armorEquip, slot );
		}
		else
			footer.EquipEnabled = false;

		var useSlot = slot;
		var canInspectUse = ThornsItemRegistry.IsUsableConsumable( defInspect ) && net.Quantity > 0;
		footer.UseEnabled = canInspectUse;
		if ( canInspectUse )
		{
			footer.OnUse = () =>
			{
				var invReq = Components.Get<ThornsInventory>();
				if ( invReq.IsValid() )
					invReq.RequestUseItemFromSlot( useSlot );
			};
		}

		ThornsUiItemInspectPanel.Rebuild( body, net, defInspect, isCraftPreview: false, footer );
	}

	void ThornsHotbarEquipStub( int slot )
	{
		var hb = Components.Get<ThornsHotbarEquipment>();
		if ( hb.IsValid() )
			hb.RequestSelectHotbarSlot( slot );
		_lastToolbarIndex = slot;
		RefreshToolbarSelectionVisual();
	}

	void RefreshToolbarSelectionVisual()
	{
		var cur = Components.Get<ThornsHotbarEquipment>();
		var idx = cur.IsValid() ? cur.ClientMirrorSelectedHotbar : _lastToolbarIndex;
		for ( var i = 0; i < _hotbarSlots.Length; i++ )
		{
			var sel = i == idx;
			_hotbarSlots[i].SetSelected( sel );
			if ( _invBody is { IsValid: true } && i < _invBody.MenuToolbarSlots.Length )
				_invBody.MenuToolbarSlots[i].SetSelected( sel );
		}
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying )
			return;

		if ( !IsLocalOwned )
			return;

		TryInit();
		if ( _root is null || !_root.IsValid )
			return;

		TickGameplayToastsAndHints();
		TickShellDamageVignettePresentation();
		TickShellLevelUpCelebrationPresentation();
		TickTameHoldHud();
		TickServerChatInput();
		TickServerChatDeferredEntryClear();

		if ( Input.Keyboard.Pressed( "Escape" ) )
		{
			if ( ServerChatTryConsumeEscape() )
				return;

			if ( RadioShopUiOpen )
				CloseRadioShopUi();
			else if ( StorageChestUiOpen )
				CloseStorageChestUi();
			else if ( CampfireUiOpen )
				CloseCampfireUi();
			else if ( WorkbenchUiOpen )
				CloseWorkbenchUi();
			else if ( MenuOpen )
				ToggleMenu();
		}

		if ( Input.Keyboard.Pressed( "tab" ) )
		{
			if ( RadioShopUiOpen )
				CloseRadioShopUi();
			else if ( StorageChestUiOpen )
				CloseStorageChestUi();
			else if ( CampfireUiOpen )
				CloseCampfireUi();
			else if ( WorkbenchUiOpen )
				CloseWorkbenchUi();
			else
				ToggleMenu();
		}

		_root.SetClass( "menu-open", MenuOpen );
		UpdateCrosshairVisibility();
		UpdateToolbarDockForBuildMode();

		if ( MenuOpen )
			ShellTickDragThreshold();

		if ( MenuOpen && ( _shellPendingMoveSlot.HasValue || _shellPendingArmorSlot.HasValue ) )
		{
			ShellUpdateDropTargetUnderCursor();
			UpdateShellDragGhostPosition();
			ShellTickDragReleaseFallback();
		}

		if ( StorageChestUiOpen && _storageDragFromChest.HasValue )
		{
			StorageUpdateDropTargetUnderCursor();
			UpdateStorageChestDragGhostPosition();
			StorageTickDragReleaseFallback();
		}

		if ( CampfireUiOpen && _campfireDragFromCampfire.HasValue )
		{
			CampfireUpdateDropTargetUnderCursor();
			UpdateCampfireDragGhostPosition();
			CampfireTickDragReleaseFallback();
		}

		if ( WorkbenchUiOpen && _workbenchDragFromWorkbench.HasValue )
		{
			WorkbenchUpdateDropTargetUnderCursor();
			UpdateWorkbenchDragGhostPosition();
			WorkbenchTickDragReleaseFallback();
		}

		if ( StorageChestUiOpen )
			TickStorageChestProximity();

		if ( CampfireUiOpen )
			TickCampfireProximity();

		if ( WorkbenchUiOpen )
			TickWorkbenchProximity();

		if ( RadioShopUiOpen )
			TickRadioShopProximity();

		// Vitals/XP/health HUD must track synced vitals immediately (XP from kills was lagging behind ~150ms throttle).
		RefreshHudData();

		var health = Components.Get<ThornsHealth>();
		var deadUi = health.IsValid() && health.IsDeadState;
		_root?.SetClass( "thorns-shell--dead-player", deadUi );
		if ( deadUi )
		{
			ShellClearDragState();
			CloseStorageChestUi();
			CloseCampfireUi();
			CloseWorkbenchUi();
			CloseRadioShopUi();
			_hud.Menu.ForceCloseForDestroy();
			_root.SetClass( "menu-open", false );
			if ( _alertLabel is { IsValid: true } )
				_alertLabel.Text = "";
			ClearGameplayToastsAndHints();
		}

		if ( Time.Now < _nextDataTick )
			return;
		_nextDataTick = Time.Now + 0.15;

		RefreshSlotsFromInventory();

		if ( MenuOpen && ActiveTab == ThornsMainUiTab.Tames && _tamesTabBody is { IsValid: true } )
			_tamesTabBody.RefreshFromPawn( GameObject, force: false );

		if ( MenuOpen && ActiveTab == ThornsMainUiTab.Skills && _skillsTabBody is { IsValid: true } )
			_skillsTabBody.RefreshFromPawn( GameObject, force: false );

		if ( MenuOpen && ActiveTab == ThornsMainUiTab.Journal && _journalTabBody is { IsValid: true } )
			_journalTabBody.RefreshFromPawn( GameObject, force: false );

		if ( MenuOpen && ActiveTab == ThornsMainUiTab.Guild && _guildTabBody is { IsValid: true } )
			_guildTabBody.RefreshFromPawn( GameObject, force: false );
	}

	static float HudXpFillFraction( int totalXp, int characterLevel )
	{
		var start = ThornsVitals.CumulativeXpToEnterLevel( characterLevel );
		var next = ThornsVitals.CumulativeXpToEnterLevel( characterLevel + 1 );
		if ( next <= start )
			return 1f;
		return Math.Clamp( (totalXp - start) / (float)(next - start), 0f, 1f );
	}

	void RefreshHudData()
	{
		ClientEnsureDefaultPinnedJournalGoal();

		var health = Components.Get<ThornsHealth>();
		if ( health.IsValid() )
		{
			var ht = $"{health.CurrentHealth:F0} / {health.MaxHealth:F0}";
			_vitalsBars[0].SetCaption( ht );
			_vitalsBars[0].SetFraction01( health.MaxHealth > 0.01f
				? Math.Clamp( health.CurrentHealth / health.MaxHealth, 0f, 1f )
				: 0f );
		}

		var vitals = Components.Get<ThornsVitals>();
		if ( vitals.IsValid() )
		{
			var foodF =
				vitals.MaxHunger > 0.01f ? Math.Clamp( vitals.Hunger / vitals.MaxHunger, 0f, 1f ) : 0f;
			var watF =
				vitals.MaxThirst > 0.01f ? Math.Clamp( vitals.Thirst / vitals.MaxThirst, 0f, 1f ) : 0f;
			var stF =
				vitals.MaxStamina > 0.01f ? Math.Clamp( vitals.Stamina / vitals.MaxStamina, 0f, 1f ) : 0f;

			_vitalsBars[1].SetCaption( $"{vitals.Hunger:F0}/{vitals.MaxHunger:F0}" );
			_vitalsBars[1].SetFraction01( foodF );
			_vitalsBars[2].SetCaption( $"{vitals.Thirst:F0}/{vitals.MaxThirst:F0}" );
			_vitalsBars[2].SetFraction01( watF );
			_vitalsBars[3].SetCaption( $"{vitals.Stamina:F0}/{vitals.MaxStamina:F0}" );
			_vitalsBars[3].SetFraction01( stF );

			var xpF = HudXpFillFraction( vitals.TotalXp, vitals.CharacterLevel );
			_vitalsBars[4].SetCaption( $"Lv.{vitals.CharacterLevel}" );
			_vitalsBars[4].SetFraction01( xpF );
			if ( _shellLevelUpBarGlowUntil > 0.0 && Time.Now < _shellLevelUpBarGlowUntil )
				_vitalsBars[4].SetClass( "thorns-stat-bar--levelup-glow", true );
		}

		RefreshToolbarSelectionVisual();

		var weaponAmmo = Components.Get<ThornsWeapon>();
		if ( _toolbarAmmoMiniLabel is not null && _toolbarAmmoMiniLabel.IsValid )
		{
			var showAmmo = ThornsWeapon.HudShouldShowGunAmmoCounters( weaponAmmo );
			if ( showAmmo && weaponAmmo.IsValid() )
			{
				_toolbarAmmoMiniLabel.Text =
					$"{weaponAmmo.ClientMirrorLoadedAmmo} / {weaponAmmo.ClientMirrorReserveAmmo}";
				_toolbarAmmoMiniLabel.SetClass( "thorns-shell-toolbar-ammo-mini--hidden", false );
			}
			else
			{
				_toolbarAmmoMiniLabel.Text = "";
				_toolbarAmmoMiniLabel.SetClass( "thorns-shell-toolbar-ammo-mini--hidden", true );
			}
		}
	}

	void RefreshSlotsFromInventory()
	{
		var inv = Components.Get<ThornsInventory>();
		if ( !inv.IsValid() )
			return;

		// Do not rebuild/delete the drag ghost here — it runs every ~150ms and was causing layout/icon shift.
		if ( IsInventoryDragActive )
		{
			ShellRefreshDragSlotDecorations();
			return;
		}

		for ( var i = 0; i < _hotbarSlots.Length; i++ )
		{
			inv.TryGetClientMirrorSlot( i, out var net );
			_hotbarSlots[i].SetToolbarFromMirror( net, i + 1 );
		}

		if ( _invBody is null || !_invBody.IsValid )
			return;

		foreach ( var s in _invBody.BackpackSlots )
		{
			inv.TryGetClientMirrorSlot( s.SlotIndex, out var net );
			Color? rowTint = null;
			if ( ThornsUiWeaponInspectFormatting.TryGetWeaponInventoryTitleTint( net, out var wt ) )
				rowTint = wt;
			else if ( ThornsUiArmorInspectFormatting.TryGetArmorInventoryTitleTint( net, out var at ) )
				rowTint = at;
			s.SetMirrorSlotVisual( net, rowTint );
		}

		for ( var ti = 0; ti < _invBody.MenuToolbarSlots.Length; ti++ )
		{
			inv.TryGetClientMirrorSlot( ti, out var tnet );
			_invBody.MenuToolbarSlots[ti].SetToolbarFromMirror( tnet, ti + 1 );
		}

		var armor = Components.Get<ThornsArmorEquipment>();
		if ( armor.IsValid() )
		{
			foreach ( var a in _invBody.ArmorSlots )
				a.ApplyMirror( armor );
		}

		ShellRefreshDragSlotDecorations();

		RefreshCraftingColumn();

		if ( MenuOpen && ActiveTab == ThornsMainUiTab.Inventory )
		{
			var invRevBump = inv.ClientMirrorRevision;
			var armorRevBump = armor.IsValid() ? armor.ClientArmorMirrorRevision : int.MinValue;
			if ( (_selectedInventorySlot >= 0
			      || _selectedArmorSlot >= 0
			      || !string.IsNullOrWhiteSpace( _contextCraftInspectOutputItemId ))
			     && (invRevBump != _inspectContextLastInvMirrorRev
			         || armorRevBump != _inspectContextLastArmorMirrorRev) )
			{
				_inspectContextLastInvMirrorRev = invRevBump;
				_inspectContextLastArmorMirrorRev = armorRevBump;
				UpdateInventoryContextPlaceholder();
			}
		}

		if ( StorageChestUiOpen )
			RefreshStorageChestPanelSlots();

		if ( CampfireUiOpen )
			RefreshCampfirePanelSlots();

		if ( WorkbenchUiOpen )
			RefreshWorkbenchPanelSlots();

		if ( RadioShopUiOpen )
			TickRadioShopUiRefreshFromMirror();
	}

	void RefreshCraftingColumn()
	{
		if ( _invBody is null || !_invBody.IsValid
		     || MenuOpen != true || ActiveTab != ThornsMainUiTab.Inventory )
			return;

		var inv = Components.Get<ThornsInventory>();
		var vitals = Components.Get<ThornsVitals>();
		var upgrades = Components.Get<ThornsPlayerUpgrades>();
		var tier = upgrades.IsValid()
			? upgrades.GetEffectiveCraftingTier()
			: vitals.IsValid()
				? vitals.CharacterLevel
				: 1;

		var invRev = inv.IsValid() ? inv.ClientMirrorRevision : int.MinValue;
		var needRebuild = invRev != _craftUiLastInvRev
		                  || tier != _craftUiLastTierCached
		                  || _craftUiFilter != _craftUiLastFilterCached;

		if ( _invBody.CraftingTierLabel.IsValid )
			_invBody.CraftingTierLabel.Text = $"Effective crafting tier: T{tier}";

		if ( !_invBody.CraftingScrollHost.IsValid || !needRebuild )
			return;

		_craftUiLastInvRev = invRev;
		_craftUiLastTierCached = tier;
		_craftUiLastFilterCached = _craftUiFilter;
		ThornsUiShellCraftingList.Rebuild(
			_invBody.CraftingScrollHost,
			inv,
			vitals,
			upgrades,
			_craftUiFilter,
			ShellSelectCraftOutputInspect );
	}

	void ApplyMenuVisibility()
	{
		if ( !_menuLayer.IsValid )
			return;
		_menuLayer.SetClass( "thorns-shell-menu-hidden", !MenuOpen );
	}

	void ApplyTabBodiesVisibility()
	{
		foreach ( var kv in _tabBodies )
			kv.Value.SetClass( "thorns-tab-body--inactive", kv.Key != ActiveTab );
	}

	Panel BuildCrosshair( Panel parent )
	{
		var layer = ThornsUiPanelAdd.AddChildPanel(parent,  "thorns-shell-crosshair-layer" );

		var stack = ThornsUiPanelAdd.AddChildPanel(layer,  "thorns-shell-crosshair-stack" );

		var top = ThornsUiPanelAdd.AddChildPanel(stack,  "thorns-ch-arm-wrap" );
		top.AddClass( "thorns-ch-arm thorns-ch-v" );

		var mid = ThornsUiPanelAdd.AddChildPanel(stack,  "thorns-shell-crosshair-mid" );
		var left = ThornsUiPanelAdd.AddChildPanel(mid,  "thorns-ch-arm-wrap" );
		left.AddClass( "thorns-ch-arm thorns-ch-h" );
		var hole = ThornsUiPanelAdd.AddChildPanel(mid,  "thorns-ch-gap" );
		_ = hole;
		var right = ThornsUiPanelAdd.AddChildPanel(mid,  "thorns-ch-arm-wrap" );
		right.AddClass( "thorns-ch-arm thorns-ch-h" );

		var bottom = ThornsUiPanelAdd.AddChildPanel(stack,  "thorns-ch-arm-wrap" );
		bottom.AddClass( "thorns-ch-arm thorns-ch-v" );

		return layer;
	}

	void UpdateToolbarDockForBuildMode()
	{
		if ( _toolbarDockHost is null || !_toolbarDockHost.IsValid )
			return;

		var build = Components.Get<ThornsBuildingController>();
		var hideCombatDock = build.IsValid() && build.BuildModeActive;
		_toolbarDockHost.SetClass( "thorns-shell-toolbar-dock--build-mode-hidden", hideCombatDock );
	}

	void UpdateCrosshairVisibility()
	{
		if ( _crosshairLayer is null || !_crosshairLayer.IsValid )
			return;

		var health = Components.Get<ThornsHealth>();
		if ( !health.IsValid() || !health.IsAlive || health.IsDeadState || BlocksGameplayShellOverlay )
		{
			_crosshairLayer.SetClass( "thorns-ch-hidden", true );
			return;
		}

		var weapon = Components.Get<ThornsWeapon>();
		var hasWeapon = weapon is { IsValid: true } && !string.IsNullOrWhiteSpace( weapon.ClientMirrorCombatDefinitionId );
		var combatId = weapon?.ClientMirrorCombatDefinitionId ?? "";
		var melee = ThornsWeaponDefinitions.TreatsAsMeleeWeapon( ThornsWeaponDefinitions.Get( combatId ), combatId );
		var fpAllowsAds = weapon is null || !weapon.IsValid() || weapon.ClientMirrorFpPresentationAllowsCombatLayers();
		var ads = hasWeapon && !melee && fpAllowsAds && (Input.Down( "Attack2" ) || Input.Down( "attack2" ));
		_crosshairLayer.SetClass( "thorns-ch-hidden", ads );
	}

	void RefreshTabsVisual()
	{
		if ( _tabButtons == null )
			return;
		foreach ( var b in _tabButtons )
			b.SetSelected( b.Tab == ActiveTab );

		UpdateInventoryContextPlaceholder();
	}
}
