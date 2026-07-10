#nullable disable

using Sandbox.UI;

namespace Sandbox;

/// <summary>
/// Local game HUD: health, armor, weapon/ammo, hotbar, inventory screen, crate loot, death flow. Presentation only — server RPCs for actions.
/// </summary>
[Title( "Thorns — Game HUD" )]
[Category( "Thorns" )]
[Icon( "web" )]
[Order( 25 )]
public sealed partial class ThornsDebugHudHost : PanelComponent, Component.INetworkSpawn
{
	const float HealthBarWidth = 320f;

	public bool ShowDebugOverlay { get; private set; }
	public bool ShowFullInventory { get; private set; }

	/// <summary>Radio outpost buy/sell overlay (presentation — buys/sells via host RPCs).</summary>
	public bool ShowRadioShop { get; private set; }

	public Guid RadioShopStationId { get; private set; }

	long _radioCatalogEpoch;
	string[] _radioCatalogItemIds;
	int[] _radioCatalogBuyPrices;
	int[] _radioCatalogMaxBuy;

	public int? PendingMoveSlot => ThornsInventoryDragState.FromInventorySlot;

	/// <summary>Dragging an equipped armor piece (0=head, 1=chest, 2=pants) to drop on an inventory cell.</summary>
	public int? PendingArmorMoveSlot => ThornsInventoryDragState.FromArmorSlot;

	int? _hoverSlot;
	int? _overlayInspectSlot;
	Panel _inventoryDim;
	readonly Dictionary<int, InventorySlotPanel> _slotPanels = new();
	public Guid NearestCrateId { get; private set; }

	Panel _damageVignetteLayer;
	double _damageVignetteDecayStart;
	double _damageVignetteFadeEnd;
	float _damageVignetteDisplay01;

	double _nextCratePoll;
	double _nextHudRebuild = -1;
	bool _treeReady;
	bool _wasDeadForHudInterval;

	/// <summary>
	/// <see cref="ThornsPawn.IsLocal"/> is set in <c>OnNetworkSpawn</c>, which often runs <i>after</i> <see cref="OnStart"/>.
	/// Joining clients would bail in <see cref="OnStart"/> and never build the HUD unless we retry here and from <see cref="OnNetworkSpawn"/>.
	/// </summary>
	public void OnNetworkSpawn( Connection owner ) => TryInitializeLocalHud();

	protected override void OnStart() => TryInitializeLocalHud();

	void TryInitializeLocalHud()
	{
		if ( _treeReady )
			return;

		var local = Connection.Local;
		if ( local is null || GameObject.Network.OwnerId != local.Id )
			return;

		if ( ThornsWorldBootGate.BlocksLocalOwnerPresentation )
			return;

		if ( !Components.Get<ScreenPanel>( FindMode.EnabledInSelf ).IsValid() )
			_ = Components.Create<ScreenPanel>();

		var sp = Components.Get<ScreenPanel>( FindMode.EnabledInSelf );
		if ( sp.IsValid() )
		{
			sp.AutoScreenScale = true;
			sp.ZIndex = 50;
		}

		if ( Panel is not null && Panel.IsValid )
		{
			Panel.AddClass( "thorns-game-hud" );
			Panel.Style.Width = Length.Fraction( 1f );
			Panel.Style.Height = Length.Fraction( 1f );
			Panel.Style.PointerEvents = PointerEvents.None;
			_treeReady = true;
			_nextHudRebuild = 0;
			EnsureHitMarkerHost();
			EnsureDamageVignetteLayer();
			ThornsPerfDebugHost.EnsureOn( GameObject );
			ThornsPerfDebug.MarkLoadStarted();
			Log.Info( "[Thorns] UI: game HUD initialized (local owner)" );
		}
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying )
			return;

		var local = Connection.Local;
		if ( local is null || GameObject.Network.OwnerId != local.Id )
			return;

		if ( ThornsWorldBootGate.BlocksLocalOwnerPresentation )
		{
			if ( _treeReady && Panel is { IsValid: true } )
				Panel.Style.Display = DisplayMode.None;
			return;
		}

		if ( !_treeReady )
			TryInitializeLocalHud();

		if ( Panel is { IsValid: true } )
			Panel.Style.Display = DisplayMode.Flex;

		TickLocalDebugInputAndOverlays();

		if ( !_treeReady )
			return;

		var health = Components.Get<ThornsHealth>();
		var deadNow = health.IsValid() && health.IsDeadState;
		if ( _wasDeadForHudInterval && !deadNow )
			_nextHudRebuild = 0;
		_wasDeadForHudInterval = deadNow;

		if ( Input.Keyboard.Pressed( "F1" ) )
		{
			ShowDebugOverlay = !ShowDebugOverlay;
			Log.Info( $"[Thorns] UI: F1 developer panel={(ShowDebugOverlay ? "on" : "off")}" );
			_nextHudRebuild = 0;
		}

		if ( Input.Keyboard.Pressed( "F7" ) )
		{
			var scene = Game.ActiveScene;
			if ( scene is not null && scene.IsValid() )
				ThornsCollisionAudit.LogNearby( scene, GameObject.WorldPosition, 1600f );
			else
				Log.Warning( "[Thorns] collision_audit_nearby: no active scene." );
		}

		if ( Input.Keyboard.Pressed( "tab" ) )
		{
			var shell = Components.Get<ThornsGameShell>( FindMode.EnabledInSelf );
			// When ThornsGameShell is active it owns TAB; toggling inventory here too would undo its ToggleMenu().
			if ( shell is not { IsValid: true, Enabled: true } )
			{
				ShowFullInventory = !ShowFullInventory;
				if ( !ShowFullInventory )
					ClearPendingMoveSlot();
				Log.Info( $"[Thorns] UI: Tab inventory={(ShowFullInventory ? "open" : "closed")}" );
				_nextHudRebuild = 0;
			}
		}

		if ( Input.Pressed( "view" ) || Input.Keyboard.Pressed( "Escape" ) )
		{
			var shellUi = Components.Get<ThornsGameShell>( FindMode.EnabledInSelf );
			if ( shellUi is { IsValid: true, Enabled: true } && shellUi.RadioShopUiOpen )
				shellUi.CloseRadioShopUi();

			if ( ShowFullInventory || ShowDebugOverlay || ShowRadioShop )
			{
				ShowFullInventory = false;
				ShowDebugOverlay = false;
				SetRadioShopOpen( false );
				ClearPendingMoveSlot();
				Log.Info( "[Thorns] UI: closed overlays (Escape / view)" );
				_nextHudRebuild = 0;
			}
		}

		if ( Time.Now >= _nextCratePoll )
		{
			_nextCratePoll = Time.Now + 0.35;
			UpdateNearestCrateSnapshot();
		}

		if ( Time.Now >= _nextHudRebuild )
		{
			var shellUi = Components.Get<ThornsGameShell>( FindMode.EnabledInSelf );
			if ( !(shellUi is { IsValid: true, Enabled: true } && shellUi.IsInventoryDragActive) )
				RebuildHud();

			// Full RebuildHud nukes all panels; at 0.12s the death RESPAWN button is recreated before mouse-up, so clicks never register.
			var interval = deadNow ? 0.75 : 0.22;
			_nextHudRebuild = Time.Now + interval;
		}

		TickHitMarkerPresentation();
		TickCrosshairBumpDecay();
		TickDamageVignettePresentation();

		UpdateMouseMode( health );
		UpdateHoverSlotFromMouse();
		UpdateDragGhostPosition();

	}

	void TickLocalDebugInputAndOverlays()
	{
		if ( Input.Keyboard.Pressed( "F8" ) )
		{
			ThornsTraceDebug.ShowTraceOverlay = !ThornsTraceDebug.ShowTraceOverlay;
			Log.Info( $"[Thorns] trace_debug overlay (F8)={(ThornsTraceDebug.ShowTraceOverlay ? "on" : "off")}" );
		}

		if ( Input.Keyboard.Pressed( "F9" ) )
		{
			ThornsTraceDiagnostics.CountRays = !ThornsTraceDiagnostics.CountRays;
			Log.Info( $"[Thorns] trace ray counters (F9)={(ThornsTraceDiagnostics.CountRays ? "on" : "off")}" );
		}

		var traceScene = Game.ActiveScene;
		if ( traceScene is not null && traceScene.IsValid() )
			ThornsTraceDebug.TickDraw( traceScene );
	}

	void UpdateMouseMode( ThornsHealth health )
	{
		var shell = Components.Get<ThornsGameShell>( FindMode.EnabledInSelf );
		var shellMenu = shell is { IsValid: true } && shell.Enabled && shell.MenuOpen;
		var shellChest = shell is { IsValid: true } && shell.Enabled && shell.StorageChestUiOpen;
		var shellCampfire = shell is { IsValid: true } && shell.Enabled && shell.CampfireUiOpen;
		var shellWorkbench = shell is { IsValid: true } && shell.Enabled && shell.WorkbenchUiOpen;
		var shellRadio = shell is { IsValid: true } && shell.Enabled && shell.RadioShopUiOpen;
		var needCursor = shellMenu || shellChest || shellCampfire || shellWorkbench || shellRadio || ShowFullInventory || ShowDebugOverlay || ShowRadioShop
			|| (health.IsValid() && health.IsDeadState);
		Mouse.Visibility = needCursor ? MouseVisibility.Visible : MouseVisibility.Hidden;
	}

	void UpdateNearestCrateSnapshot()
	{
		var bridge = Components.Get<ThornsDebugUiBridge>();
		if ( !bridge.IsValid() )
			return;

		var scene = Game.ActiveScene;
		if ( scene is null || !scene.IsValid() )
			return;

		var best = Guid.Empty;
		var bestD = 999999f;
		foreach ( var c in ThornsDeathCrate.ActiveById.Values )
		{
			if ( !c.IsValid() || c.CrateId == Guid.Empty )
				continue;
			var d = (c.GameObject.WorldPosition - GameObject.WorldPosition).Length;
			if ( d < bestD )
			{
				bestD = d;
				best = c.CrateId;
			}
		}

		if ( bestD > 240f )
			best = Guid.Empty;

		NearestCrateId = best;

		if ( best != Guid.Empty )
			bridge.RequestDeathCrateSnapshotForUi( best );
	}

	public void ToggleShowFullInventory()
	{
		ShowFullInventory = !ShowFullInventory;
		_nextHudRebuild = 0;
	}

	public void SetPendingMoveSlot( int slot )
	{
		ThornsInventoryDragState.BeginInventoryDrag( slot, null, null );
		_hoverSlot = slot;
		RequestHudRebuild();
	}

	public void ClearPendingMoveSlot()
	{
		ThornsInventoryDragState.Clear();
		_overlayInspectSlot = null;
	}

	public void RequestHudRebuild() => _nextHudRebuild = 0;

	public void SetRadioShopOpen( bool open, Guid stationId = default )
	{
		ShowRadioShop = open;
		RadioShopStationId = open ? stationId : Guid.Empty;
		_nextHudRebuild = 0;
	}

	public void ApplyRadioShopCatalog( long epoch, string[] itemIds, int[] buyPrices, int[] maxBuy )
	{
		_radioCatalogEpoch = epoch;
		_radioCatalogItemIds = itemIds;
		_radioCatalogBuyPrices = buyPrices;
		_radioCatalogMaxBuy = maxBuy;
		_nextHudRebuild = 0;
	}

	void RebuildHud()
	{
		EnsureHitMarkerHost();
		foreach ( var ch in Panel.Children.ToArray() )
		{
			if ( IsHitMarkerHostPanel( ch ) )
				continue;
			if ( IsDamageVignetteRoot( ch ) )
				continue;
			ch.Delete();
		}
		_slotPanels.Clear();
		ThornsInventoryDragState.NotifyPanelsDestroyed();
		_inventoryDim = null;

		var lp = Components.Get<ThornsPawn>();
		if ( lp is null || !lp.IsValid() )
			return;

		var shellHud = Components.Get<ThornsGameShell>( FindMode.EnabledInSelf ) is { IsValid: true, Enabled: true };

		var rootGo = lp.GameObject;
		var inv = rootGo.Components.Get<ThornsInventory>();
		var hotbar = rootGo.Components.Get<ThornsHotbarEquipment>();
		var weapon = rootGo.Components.Get<ThornsWeapon>();
		var health = rootGo.Components.Get<ThornsHealth>();
		var armor = rootGo.Components.Get<ThornsArmorEquipment>();
		var bridge = rootGo.Components.Get<ThornsDebugUiBridge>();

		var vitals = rootGo.Components.Get<ThornsVitals>();
		var upgrades = rootGo.Components.Get<ThornsPlayerUpgrades>();
		var milestones = rootGo.Components.Get<ThornsPlayerMilestones>();
		var wallet = rootGo.Components.Get<ThornsWallet>();
		var radioShop = rootGo.Components.Get<ThornsRadioShopInteractor>();
		var buildCtl = rootGo.Components.Get<ThornsBuildingController>();

		if ( !shellHud && (!health.IsValid() || !health.IsDeadState) )
			BuildCrosshair( Panel, weapon );

		if ( !shellHud )
			BuildPlayerHudTopLeft( Panel, health, armor, vitals, upgrades, milestones, wallet );

		if ( ShowRadioShop && !shellHud )
			BuildRadioShopOverlay( Panel, inv, radioShop );
		BuildCrateSidebar( Panel, bridge );

		if ( !shellHud && ShowFullInventory )
			BuildInventoryOverlayRust( Panel, inv, hotbar, armor, vitals, upgrades );

		if ( buildCtl.IsValid() && buildCtl.BuildModeActive )
			BuildToolbarDockBuildMode( Panel, buildCtl, inv );
		else if ( !shellHud )
			BuildToolbarDockV2( Panel, inv, hotbar, weapon, armor );

		if ( ShowDebugOverlay )
			BuildDeveloperScreen( Panel );

		if ( health.IsValid() && health.IsDeadState )
			BuildDeathScreen( Panel, health );
	}

	void UpdateDragGhostPosition()
	{
		if ( !ShowFullInventory || _inventoryDim is null || !_inventoryDim.IsValid )
			return;
		if ( ThornsInventoryDragState.DragIcon is null || !ThornsInventoryDragState.DragIcon.IsValid )
			return;
		if ( !ThornsInventoryDragState.IsDragging )
			return;

		ThornsInventoryDragState.DragIcon.UpdateFollowMouse();
	}

	void UpdateHoverSlotFromMouse()
	{
		if ( !ShowFullInventory || _inventoryDim is null || !_inventoryDim.IsValid )
			return;
		if ( !ThornsInventoryDragState.IsDragging )
			return;

		var screenPos = _inventoryDim.PanelPositionToScreenPosition( _inventoryDim.MousePosition );
		_hoverSlot = ResolveSlotAtScreenPosition( screenPos );
	}

	/// <summary>Toolbar / backpack left click when full inventory is open — begins drag only (no click-to-swap).</summary>
	void HandleInventoryGridMouseDown( int idx, ThornsInventory inv, ThornsArmorEquipment armor, InventorySlotPanel slotPanel )
	{
		if ( Input.Keyboard.Down( "Shift" ) )
		{
			TryEquipArmorFromSlot( idx, armor );
			RequestHudRebuild();
			return;
		}

		BeginDragSlot( idx, inv, slotPanel );
	}

	void HandleInventorySlotMouseUp( int idx, ThornsInventory inv, ThornsArmorEquipment armor )
	{
		if ( ThornsInventoryDragState.FromArmorSlot.HasValue )
		{
			DropOnSlot( idx, inv, armor );
			return;
		}

		if ( !ThornsInventoryDragState.FromInventorySlot.HasValue )
			return;

		var from = ThornsInventoryDragState.FromInventorySlot.Value;
		if ( from == idx )
		{
			_overlayInspectSlot = idx;
			ThornsInventoryDragState.Clear();
			RequestHudRebuild();
			return;
		}

		DropOnSlot( idx, inv, armor );
	}

	void CancelInventoryDragFromBackdrop()
	{
		if ( !ThornsInventoryDragState.IsDragging )
			return;

		ThornsInventoryDragState.Clear();
		RequestHudRebuild();
	}

	void HandleArmorSlotMouseUp( int armorSlotIdx, ThornsArmorEquipment armor )
	{
		if ( !ShowFullInventory )
			return;

		if ( ThornsInventoryDragState.FromInventorySlot.HasValue )
		{
			DropOnArmor( armor );
			return;
		}

		if ( ThornsInventoryDragState.FromArmorSlot.HasValue )
		{
			ClearPendingMoveSlot();
			RequestHudRebuild();
		}
	}

	void BuildCrateSidebar( Panel root, ThornsDebugUiBridge bridge )
	{
		if ( !bridge.IsValid() || NearestCrateId == Guid.Empty )
			return;

		var panel = ThornsUiPanelAdd.AddChildPanel(root,  "thorns-crate-sidebar" );
		panel.Style.Position = PositionMode.Absolute;
		panel.Style.Top = 100;
		panel.Style.Right = 16;
		panel.Style.Width = 300;
		panel.Style.MaxHeight = Length.Fraction( 0.45f );
		panel.Style.FlexDirection = FlexDirection.Column;
		panel.Style.Overflow = OverflowMode.Scroll;
		panel.Style.PointerEvents = PointerEvents.All;

		_ = panel.AddChild( new Label( "NEARBY CRATE", "thorns-crate-head" ) );

		var snap = bridge.LastCrateSnapshotText ?? "";
		foreach ( var line in snap.Split( '\n', StringSplitOptions.RemoveEmptyEntries ) )
		{
			var parts = line.Split( '|' );
			if ( parts.Length < 2 )
				continue;

			var row = ThornsUiPanelAdd.AddChildPanel(panel,  "thorns-crate-line" );
			row.Style.FlexDirection = FlexDirection.Row;
			row.Style.AlignItems = Align.Center;
			row.Style.JustifyContent = Justify.SpaceBetween;
			row.Style.MarginBottom = 6;
			row.Style.PointerEvents = PointerEvents.All;

			if ( parts[0] == "G" && parts.Length >= 4 )
			{
				var slot = int.Parse( parts[1] );
				var item = parts[2];
				var qty = parts[3];
				var nm = ShortDisplayName( item );
				_ = row.AddChild( new Label( $"{nm}  ×{qty}", "thorns-crate-item" ) );
				var cid = NearestCrateId;
				var take = row.AddChild( new Button( "Take", () =>
				{
					Log.Info( $"[Thorns] UI: loot grid slot={slot}" );
					FindCrateById( cid )?.RequestLootInventorySlot( cid, slot );
				} ) );
				take.AddClass( "thorns-crate-take-btn" );
			}
			else if ( parts[0] == "A" && parts.Length >= 4 )
			{
				var aidx = int.Parse( parts[1] );
				var item = parts[2];
				_ = row.AddChild( new Label( $"{ShortDisplayName( item)}  (armor)", "thorns-crate-item" ) );
				var cid = NearestCrateId;
				var take = row.AddChild( new Button( "Take", () =>
				{
					Log.Info( $"[Thorns] UI: loot armor idx={aidx}" );
					FindCrateById( cid )?.RequestLootArmorPiece( cid, aidx );
				} ) );
				take.AddClass( "thorns-crate-take-btn" );
			}
		}
	}

	int? ResolveSlotAtScreenPosition( Vector2 screenPos )
	{
		foreach ( var kv in _slotPanels )
		{
			var p = kv.Value;
			if ( !p.IsValid )
				continue;
			var d = p.ScreenPositionToPanelDelta( screenPos );
			if ( d.x >= 0f && d.x <= 1f && d.y >= 0f && d.y <= 1f )
				return kv.Key;
		}

		return _hoverSlot;
	}

	void BeginDragSlot( int idx, ThornsInventory inv, InventorySlotPanel slotPanel )
	{
		if ( !inv.IsValid() )
			return;
		if ( !inv.TryGetClientMirrorSlot( idx, out var slot ) || slot.Quantity <= 0 || string.IsNullOrWhiteSpace( slot.ItemId ) )
			return;

		Log.Info( $"[Thorns][UI] Drag start from {idx}" );
		ThornsInventoryDragState.BeginInventoryDrag( idx, slotPanel, null );
		_hoverSlot = idx;
		RequestHudRebuild();
	}

	void BeginDragArmorSlot( int armorSlotIndex, ThornsArmorEquipment armor )
	{
		if ( !armor.IsValid() )
			return;
		armor.GetClientMirrorEquippedPiece( armorSlotIndex, out var id, out _ );
		if ( string.IsNullOrWhiteSpace( id ) )
			return;

		Log.Info( $"[Thorns][UI] Drag armor from slot {armorSlotIndex}" );
		ThornsInventoryDragState.BeginArmorDrag( armorSlotIndex );
		RequestHudRebuild();
	}

	void DropOnArmor( ThornsArmorEquipment armor )
	{
		if ( ThornsInventoryDragState.FromArmorSlot.HasValue )
		{
			ClearPendingMoveSlot();
			RequestHudRebuild();
			return;
		}

		if ( !ThornsInventoryDragState.FromInventorySlot.HasValue )
			return;
		var from = ThornsInventoryDragState.FromInventorySlot.Value;
		ClearPendingMoveSlot();
		TryEquipArmorFromSlot( from, armor );
		RequestHudRebuild();
	}

	void DropOnSlot( int idx, ThornsInventory inv, ThornsArmorEquipment armor )
	{
		if ( !inv.IsValid() )
			return;

		if ( ThornsInventoryDragState.FromArmorSlot.HasValue )
		{
			var a = ThornsInventoryDragState.FromArmorSlot.Value;
			ClearPendingMoveSlot();
			Log.Info( $"[Thorns][UI] Drop armor slot {a} -> inv {idx}" );
			ThornsInventoryClientTransfer.SubmitUnequipArmorToInventorySlot( armor, a, idx );
			RequestHudRebuild();
			return;
		}

		if ( !ThornsInventoryDragState.FromInventorySlot.HasValue )
			return;

		var from = ThornsInventoryDragState.FromInventorySlot.Value;
		ClearPendingMoveSlot();

		Log.Info( $"[Thorns][UI] Drop on {idx}" );

		if ( Input.Keyboard.Down( "Shift" ) )
		{
			ThornsInventoryClientTransfer.SubmitShiftEquipArmorFromInventorySlot( armor, from );
			RequestHudRebuild();
			return;
		}

		if ( from == idx )
		{
			RequestHudRebuild();
			return;
		}

		ThornsInventoryClientTransfer.SubmitMoveOrSwapInventorySlots( inv, from, idx );

		RequestHudRebuild();
	}

	static void TryEquipArmorFromSlot( int inventorySlotIndex, ThornsArmorEquipment armor ) =>
		ThornsInventoryClientTransfer.SubmitEquipArmorFromInventorySlot( armor, inventorySlotIndex );

	void BuildDeveloperScreen( Panel root )
	{
		var dim = ThornsUiPanelAdd.AddChildPanel(root,  "dev-modal-dim" );
		dim.Style.Position = PositionMode.Absolute;
		dim.Style.Left = 0;
		dim.Style.Top = 0;
		dim.Style.Width = Length.Fraction( 1f );
		dim.Style.Height = Length.Fraction( 1f );
		dim.Style.BackgroundColor = new Color( 0f, 0f, 0f, 0.55f );
		dim.Style.PointerEvents = PointerEvents.All;
		dim.Style.JustifyContent = Justify.Center;
		dim.Style.AlignItems = Align.Center;

		var card = ThornsUiPanelAdd.AddChildPanel(dim,  "dev-card" );
		card.Style.Width = 520;
		card.Style.MaxHeight = Length.Fraction( 0.75f );
		card.Style.BackgroundColor = new Color( 0.07f, 0.08f, 0.1f, 0.98f );
		card.Style.Padding = 16;
		card.Style.FlexDirection = FlexDirection.Column;
		card.Style.Overflow = OverflowMode.Scroll;
		card.Style.PointerEvents = PointerEvents.All;

		var head = ThornsUiPanelAdd.AddChildPanel(card,  "dev-head" );
		head.Style.FlexDirection = FlexDirection.Row;
		head.Style.JustifyContent = Justify.SpaceBetween;
		head.Style.MarginBottom = 12;
		var devTitle = head.AddChild( new Label( "Developer", "dev-title" ) );
		devTitle.Style.FontSize = 18;
		ThornsUiPanelAdd.AddClickableLabel(head,  "Close", () =>
		{
			ShowDebugOverlay = false;
			RequestHudRebuild();
		} );

		var lp = Components.Get<ThornsPawn>();
		if ( lp is null || !lp.IsValid() )
			return;

		var go = lp.GameObject;
		var conn = lp.OwnerConnection;
		var inv = go.Components.Get<ThornsInventory>();
		var weapon = go.Components.Get<ThornsWeapon>();
		var health = go.Components.Get<ThornsHealth>();
		var armor = go.Components.Get<ThornsArmorEquipment>();
		var vitals = go.Components.Get<ThornsVitals>();

		var music = go.Components.Get<ThornsAtmosphericMusic>();
		if ( music.IsValid() )
		{
			var musicLines = new List<string>();
			music.AppendDebugLines( musicLines );
			foreach ( var line in musicLines )
				AddDevLine( card, "Music", line );
		}

		AddDevLine( card, "Player", conn?.DisplayName ?? "?" );
		AddDevLine( card, "Connection id", conn?.Id.ToString() ?? "?" );
		AddDevLine( card, "Owner id", go.Network.OwnerId.ToString() );
		AddDevLine( card, "IsLocal / IsProxy", $"{lp.IsLocal} / {lp.IsProxy}" );
		AddDevLine( card, "Position", go.WorldPosition.ToString() );
		AddDevLine(
			card,
			"Collision debug",
			$"collision(P)={(ThornsCollisionDebug.ShowCollisionDebug ? "on" : "off")}  {(ThornsCollisionDebug.ShowCollisionDebug ? ThornsCollisionDebug.FormatNearbySummary() : "")}  spawnAudit={(ThornsCollisionAudit.SpawnValidationEnabled ? "on" : "off")}" );
		AddDevLine(
			card,
			"Trace debug",
			$"overlay(F8)={(ThornsTraceDebug.ShowTraceOverlay ? "on" : "off")}  rayCounts(F9)={(ThornsTraceDiagnostics.CountRays ? "on" : "off")}" );
		AddDevLine(
			card,
			"Performance",
			$"perf_debug={(ThornsPerfDebug.Enabled ? "on" : "off")}  quality={ThornsPerformanceQualityPresets.ActiveQuality}  load→play {ThornsPerfDebug.FormatLoadMs()}" );
		AddDevLine(
			card,
			"FPS / frame",
			$"{ThornsPerfDebug.Fps:F0} avg {ThornsPerfDebug.AvgFps:F0}  {_lastFrameMsLabel()}  max {ThornsPerfDebug.MaxFrameMs:F1}ms  worst={ThornsPerfDebug.WorstSystemThisFrame}" );
		AddDevLine(
			card,
			"Streaming",
			$"deferred q={ThornsPerfDebug.DeferredQueuePending}  foliage={ThornsPerfDebug.FoliageInstancesVisible}/{ThornsPerfDebug.FoliageChunksLoaded}  grass={ThornsPerfDebug.GrassInstancesVisible}  content≈{ThornsPerfDebug.ContentProxyWeight}" );
		var perfRow = ThornsUiPanelAdd.AddChildPanel( card, "dev-perf-btns" );
		perfRow.Style.FlexDirection = FlexDirection.Row;
		perfRow.Style.MarginTop = 6;
		ThornsUiPanelAdd.AddClickableLabel( perfRow, "perf_debug", () =>
		{
			ThornsPerfDebug.Enabled = !ThornsPerfDebug.Enabled;
			Log.Info( $"[Thorns] perf_debug={(ThornsPerfDebug.Enabled ? "on" : "off")}" );
			RequestHudRebuild();
		} );
		ThornsUiPanelAdd.AddClickableLabel( perfRow, "Low", () => ApplyPerfQuality( ThornsPerformanceQuality.Low ) );
		ThornsUiPanelAdd.AddClickableLabel( perfRow, "Med", () => ApplyPerfQuality( ThornsPerformanceQuality.Medium ) );
		ThornsUiPanelAdd.AddClickableLabel( perfRow, "High", () => ApplyPerfQuality( ThornsPerformanceQuality.High ) );
		ThornsUiPanelAdd.AddClickableLabel( perfRow, "Ultra", () => ApplyPerfQuality( ThornsPerformanceQuality.Ultra ) );
		var colRow = ThornsUiPanelAdd.AddChildPanel( card,  "dev-col-btns" );
		colRow.Style.FlexDirection = FlexDirection.Row;
		colRow.Style.MarginTop = 6;
		colRow.Style.JustifyContent = Justify.FlexStart;
		ThornsUiPanelAdd.AddClickableLabel( colRow,  "Toggle collision (P)", () =>
		{
			ThornsCollisionDebug.ToggleAndLog();
			RequestHudRebuild();
		} );
		ThornsUiPanelAdd.AddClickableLabel( colRow,  "Spawn audit logs", () =>
		{
			ThornsCollisionAudit.SpawnValidationEnabled = !ThornsCollisionAudit.SpawnValidationEnabled;
			Log.Info( $"[Thorns] collision spawn audit logs={(ThornsCollisionAudit.SpawnValidationEnabled ? "on" : "off")}" );
			RequestHudRebuild();
		} );
		ThornsUiPanelAdd.AddClickableLabel( colRow,  "Audit nearby (F7)", () =>
		{
			var scene = Game.ActiveScene;
			if ( scene is not null && scene.IsValid() )
				ThornsCollisionAudit.LogNearby( scene, go.WorldPosition, 1600f );
			else
				Log.Warning( "[Thorns] collision_audit_nearby: no active scene." );
		} );
		var traceRow = ThornsUiPanelAdd.AddChildPanel( card,  "dev-trace-btns" );
		traceRow.Style.FlexDirection = FlexDirection.Row;
		traceRow.Style.MarginTop = 6;
		traceRow.Style.JustifyContent = Justify.FlexStart;
		ThornsUiPanelAdd.AddClickableLabel( traceRow,  "Trace overlay (F8)", () =>
		{
			ThornsTraceDebug.ShowTraceOverlay = !ThornsTraceDebug.ShowTraceOverlay;
			Log.Info( $"[Thorns] trace_debug overlay={(ThornsTraceDebug.ShowTraceOverlay ? "on" : "off")}" );
			RequestHudRebuild();
		} );
		ThornsUiPanelAdd.AddClickableLabel( traceRow,  "Ray counters (F9)", () =>
		{
			ThornsTraceDiagnostics.CountRays = !ThornsTraceDiagnostics.CountRays;
			Log.Info( $"[Thorns] trace ray counters={(ThornsTraceDiagnostics.CountRays ? "on" : "off")}" );
			RequestHudRebuild();
		} );
		ThornsUiPanelAdd.AddClickableLabel( traceRow,  "Log trace counts", () =>
		{
			Log.Info(
				$"[Thorns][TraceDiag] hitscan={ThornsTraceDiagnostics.GetRayCount( ThornsTraceProfile.WeaponHitscan )} " +
				$"interaction={ThornsTraceDiagnostics.GetRayCount( ThornsTraceProfile.InteractionUse )} " +
				$"placement={ThornsTraceDiagnostics.GetRayCount( ThornsTraceProfile.BuildingPlacementView )} " +
				$"los={ThornsTraceDiagnostics.GetRayCount( ThornsTraceProfile.AiLineOfSight )} " +
				$"move={ThornsTraceDiagnostics.GetRayCount( ThornsTraceProfile.MovementProbe )}" );
		} );
		if ( Networking.IsActive )
		{
			AddDevLine( card, "Mount steer (local sent/s)", $"{ThornsMountInputNetMetrics.ClientMountSteerSentPerSec:F1}" );
			if ( Networking.IsHost )
			{
				AddDevLine(
					card,
					"Mount steer (host)",
					$"recv/s≈{ThornsMountInputNetMetrics.HostMountSteerRecvPerSec:F1} riders≈{ThornsMountInputNetMetrics.HostMountedRidersLastSample} avg/rider≈{ThornsMountInputNetMetrics.HostAvgRecvPerRiderPerSec:F2}" );

				AddDevLine(
					card,
					"AI perception (host)",
					$"wildlife={ThornsPopulationDirector.HostWildlifeGlobalCount}  percep/s≈{ThornsAiPerceptionMetrics.WildlifePerceptionCallsPerSec:F0}  LOS/s≈{ThornsAiPerceptionMetrics.LosTracesPerSec:F0}  LOS skip/s≈{ThornsAiPerceptionMetrics.LosBudgetSkipsPerSec:F1}  LOS cache hit/s≈{ThornsAiPerceptionMetrics.LosCacheHitsPerSec:F0}" );
				AddDevLine(
					card,
					"AI spatial (host)",
					$"grid players={ThornsAiPerceptionMetrics.LastSpatialGridPlayers} cells={ThornsAiPerceptionMetrics.LastSpatialGridCells}  queries/s≈{ThornsAiPerceptionMetrics.PlayerSpatialQueriesPerSec:F0}  avg cand/q≈{ThornsAiPerceptionMetrics.AvgPlayerCandidatesPerQuery:F1}  max cand window={ThornsAiPerceptionMetrics.MaxPlayerCandidatesSingleQueryWindow}" );
				AddDevLine(
					card,
					"AI perception caps (host)",
					$"player consider/s≈{ThornsAiPerceptionMetrics.PerceptionPlayerConsiderationsPerSec:F0}  cand-cap drops/s≈{ThornsAiPerceptionMetrics.PerceptionCandidateCapDropsPerSec:F1}  LOS think-cap/s≈{ThornsAiPerceptionMetrics.LosProbeThinkCapHitsPerSec:F1}  LOS used this fixed={ThornsPopulationDirector.HostLosTracesUsedThisFixed}/{ThornsPerformanceBudgets.HostWildlifeMaxLosRaysPerFixed}" );
			}

			AddDevLine(
				card,
				"World replica — terrain",
				$"v={ThornsWorldReplicaMetrics.TerrainSpecDescriptorVersion} hash={ThornsWorldReplicaMetrics.TerrainSpecContentHash:X}  b64Chars≈{ThornsWorldReplicaMetrics.LastTerrainPayloadBytes}  decodedBytes≈{ThornsWorldReplicaMetrics.LastTerrainDecodedPayloadBytes}  rebuilds={ThornsWorldReplicaMetrics.TerrainRebuildCount}  decodeMs≈{ThornsWorldReplicaMetrics.LastTerrainDecodeMs:F2}  rebuildMs≈{ThornsWorldReplicaMetrics.LastTerrainRebuildMs:F1}  last={ThornsWorldReplicaMetrics.LastTerrainClientRebuildReason}  hashOK={ThornsWorldReplicaMetrics.LastClientTerrainHashMatched}" );
			AddDevLine(
				card,
				"World replica — POI",
				$"v={ThornsWorldReplicaMetrics.PoiDescriptorVersion} hash={ThornsWorldReplicaMetrics.PoiContentDescriptorHash:X}  b64Chars≈{ThornsWorldReplicaMetrics.LastPoiPayloadBytes}  hostRebuilds={ThornsWorldReplicaMetrics.PoiDatasetRebuildCount}  uiHydrates={ThornsWorldReplicaMetrics.PoiDatasetClientHydrateCount}  parseMs≈{ThornsWorldReplicaMetrics.LastPoiParseMs:F2}  last={ThornsWorldReplicaMetrics.LastPoiClientHydrateReason}" );
		}

		AddDevLine( card, "Health", $"{health?.CurrentHealth:F0} / {health?.MaxHealth:F0}   dead={health?.IsDeadState}" );
		if ( vitals.IsValid() )
		{
			AddDevLine( card, "Vitals", $"hunger={vitals.Hunger:F0}/{vitals.MaxHunger:F0} thirst={vitals.Thirst:F0}/{vitals.MaxThirst:F0} stamina={vitals.Stamina:F0}/{vitals.MaxStamina:F0} poison={vitals.PoisonLevel:F0}" );
			AddDevLine( card, "XP / level", $"{vitals.TotalXp} → L{vitals.CharacterLevel}" );
			AddDevLine( card, "Move (server)", $"sprint={vitals.ServerSprinting} crouch={vitals.ServerCrouching}" );
		}

		var upg = go.Components.Get<ThornsPlayerUpgrades>();
		if ( upg.IsValid() )
		{
			AddDevLine( card, "Upgrade points", $"{upg.UnspentUpgradePoints} (host spend only)" );
			AddDevLine(
				card,
				"Ranks",
				$"HY={upg.HydrationRank} IG={upg.IronGutRank} SS={upg.StrongStomachRank} WV={upg.WeatheredRank} TH={upg.ThickHideRank} | EN={upg.EnduranceRank} GH={upg.GhostRank} BM={upg.BeastmasterRank} HD={upg.HardenedRank} LC={upg.LuckyChamberRank} | LJ={upg.LumberjackRank} MN={upg.MinerRank} SC={upg.ScavengerRank} RF={upg.ReinforcedRank} TC={upg.TechnicianRank} · T{upg.GetEffectiveCraftingTier()}" );
			var devUpRow = ThornsUiPanelAdd.AddChildPanel(card,  "dev-up-btns" );
			devUpRow.Style.FlexDirection = FlexDirection.Row;
			devUpRow.Style.MarginTop = 8;
			devUpRow.Style.JustifyContent = Justify.FlexStart;
			void AddUpBtn( string label, ThornsUpgradeCategory cat )
			{
				var ordinal = (int)cat;
				var b = ThornsUiPanelAdd.AddClickableLabel(devUpRow,  label, () =>
				{
					if ( !upg.IsValid() )
						return;
					upg.RpcRequestPurchaseUpgrade( ordinal );
					RequestHudRebuild();
				} );
				b.Style.MarginRight = 6;
				b.Style.MarginBottom = 4;
			}

			AddUpBtn( "+Hydr", ThornsUpgradeCategory.Hydration );
			AddUpBtn( "+Iron", ThornsUpgradeCategory.IronGut );
			AddUpBtn( "+End", ThornsUpgradeCategory.Endurance );
			AddUpBtn( "+Ghost", ThornsUpgradeCategory.Ghost );
			AddUpBtn( "+Lumb", ThornsUpgradeCategory.Lumberjack );
			AddUpBtn( "+Mine", ThornsUpgradeCategory.Miner );
			AddUpBtn( "+Tech", ThornsUpgradeCategory.Technician );
		}

		var milestones = go.Components.Get<ThornsPlayerMilestones>();
		if ( milestones.IsValid() )
		{
			var done = milestones.ClientCompletedGoalsCount();
			var n = ThornsMilestoneDefinitions.Count;
			var msLine = $"{done}/{n} goals complete · packed='{milestones.MilestoneProgressPacked}'";
			AddDevLine( card, "Journal goals (synced)", msLine );

			if ( Networking.IsHost )
			{
				var msRow = ThornsUiPanelAdd.AddChildPanel(card,  "dev-ms-btns" );
				msRow.Style.FlexDirection = FlexDirection.Row;
				msRow.Style.MarginTop = 4;
				var tameSim = ThornsUiPanelAdd.AddClickableLabel(msRow,  "Host sim: tame +1", () =>
				{
					if ( !milestones.IsValid() )
						return;
					milestones.HostRecordTameCompleted( 1 );
					RequestHudRebuild();
				} );
				tameSim.Style.MarginRight = 8;
			}
		}

		AddDevLine( card, "Weapon def", weapon?.ClientMirrorCombatDefinitionId ?? "" );
		AddDevLine( card, "Ammo", $"{weapon?.ClientMirrorLoadedAmmo} / {weapon?.ClientMirrorReserveAmmo}" );
		AddDevLine( card, "Inv mirror revision", (inv?.ClientMirrorRevision ?? 0).ToString() );
		AddDevLine( card, "Armor DR (mirror)", $"{armor?.GetClientUiTotalDrPercentCapped():F0}%" );

		var crates = ThornsDeathCrate.ActiveById.Count;
		AddDevLine( card, "Death crates in scene", crates.ToString() );
	}

	static void AddDevLine( Panel card, string k, string v )
	{
		var row = ThornsUiPanelAdd.AddChildPanel(card,  "dev-row" );
		row.Style.FlexDirection = FlexDirection.Row;
		row.Style.JustifyContent = Justify.SpaceBetween;
		row.Style.MarginBottom = 4;
		var kLbl = row.AddChild( new Label( k, "dev-k" ) );
		kLbl.Style.FontColor = new Color( 0.55f, 0.58f, 0.62f );
		var vLbl = row.AddChild( new Label( v, "dev-v" ) );
		vLbl.Style.FontColor = Color.White;
	}

	void BuildDeathScreen( Panel root, ThornsHealth health )
	{
		var dim = ThornsUiPanelAdd.AddChildPanel(root,  "thorns-death-dim" );
		dim.Style.Position = PositionMode.Absolute;
		dim.Style.Left = 0;
		dim.Style.Top = 0;
		dim.Style.Width = Length.Fraction( 1f );
		dim.Style.Height = Length.Fraction( 1f );
		dim.Style.PointerEvents = PointerEvents.All;
		dim.Style.FlexDirection = FlexDirection.Column;
		dim.Style.JustifyContent = Justify.Center;
		dim.Style.AlignItems = Align.Center;

		var card = ThornsUiPanelAdd.AddChildPanel(dim,  "thorns-death-card" );
		card.Style.FlexDirection = FlexDirection.Column;
		// Stretch so title/body labels get the card width — centered AlignItems made labels shrink-wrap
		// and long copy could overlap the title or clip oddly in flex layout.
		card.Style.AlignItems = Align.Stretch;
		card.Style.PointerEvents = PointerEvents.All;
		card.Style.FlexShrink = 0;
		card.Style.Overflow = OverflowMode.Visible;

		var titleLbl = card.AddChild( new Label( "YOU DIED", "thorns-death-title" ) );
		titleLbl.Style.TextAlign = TextAlign.Center;
		titleLbl.Style.Width = Length.Fraction( 1f );
		titleLbl.Style.FlexShrink = 0;
		titleLbl.Style.Overflow = OverflowMode.Visible;

		var subLbl = card.AddChild( new Label(
			"Recover at a rally point.\n\nYour gear may still be in a nearby death crate.",
			"thorns-death-sub" ) );
		subLbl.Style.TextAlign = TextAlign.Center;
		subLbl.Style.WhiteSpace = WhiteSpace.PreLine;
		subLbl.Style.Width = Length.Fraction( 1f );
		subLbl.Style.FlexShrink = 0;
		subLbl.Style.Overflow = OverflowMode.Visible;

		var respawn = card.AddChild( new Button( "RESPAWN", () =>
		{
			Log.Info( "[Thorns] UI: respawn" );
			if ( health.IsValid() )
				health.RequestRespawnFromDebugUi();
		} ) );
		respawn.AddClass( "thorns-death-respawn-btn" );
	}

	static ThornsDeathCrate FindCrateById( Guid id )
	{
		if ( id == Guid.Empty )
			return default;

		return ThornsDeathCrate.ActiveById.TryGetValue( id, out var crate ) && crate.IsValid()
			? crate
			: default;
	}

	static string ShortDisplayName( string itemId )
	{
		if ( string.IsNullOrWhiteSpace( itemId ) )
			return "—";
		var d = ThornsItemRegistry.GetOrNull( itemId );
		var s = d?.DisplayName ?? itemId;
		return s.Length <= 14 ? s : s[..14] + "…";
	}

	static string SlotPrimaryLine( ThornsInventorySlotNet net )
	{
		if ( string.IsNullOrWhiteSpace( net.ItemId ) || net.Quantity <= 0 )
			return "Empty";
		return ItemIcon( net.ItemId );
	}

	static string SlotSecondaryLine( ThornsInventorySlotNet net )
	{
		if ( string.IsNullOrWhiteSpace( net.ItemId ) || net.Quantity <= 0 )
			return "";
		var q = $"×{net.Quantity}";
		if ( net.HasDurability != 0 )
			return $"{q}   {net.Durability:F0} dur";
		return q;
	}

	static string ItemIcon( string itemId )
	{
		return itemId switch
		{
			"m4" => "🔫",
			"mp5" => "🔫",
			"shotgun" => "🔫",
			"pistol_ammo" => "\u25C6",
			"shotgun_ammo" => "\u25C6",
			"smg_ammo" => "\u25C6",
			"rifle_ammo" => "\u25C6",
			"sniper_ammo" => "\u25C6",
			"bandage" => "🩹",
			"kevlar_helmet" => "🪖",
			"kevlar_chest" => "🦺",
			"kevlar_pants" => "👖",
			"wood" => "🪵",
			"stone" => "🪨",
			"apple" => "🍎",
			"water" => "💧",
			"cloth" => "🧵",
			"metal" => "⚙",
			_ => "⬜"
		};
	}

	/// <summary>Local owner: brief red screen-edge vignette when HP drops (paired with <see cref="ThornsGameShell.NotifyLocalDamageVignette"/>).</summary>
	public void NotifyLocalDamageVignette( float lastDamage, float healthAfter )
	{
		if ( lastDamage <= 0.001f )
			return;
		if ( !_treeReady || Panel is null || !Panel.IsValid )
			return;

		EnsureDamageVignetteLayer();
		var denom = Math.Max( 1f, lastDamage + Math.Max( 0f, healthAfter ) );
		var hitFrac = Math.Clamp( lastDamage / denom, 0.05f, 1f );
		var inner = Math.Clamp( 0.56f + hitFrac * 1.38f, 0.52f, 1f );
		var peak = Math.Clamp( inner * 1.32f, 0f, 1f );
		_damageVignetteDisplay01 = Math.Max( _damageVignetteDisplay01, peak );
		_damageVignetteDecayStart = Time.Now + 0.14;
		_damageVignetteFadeEnd = _damageVignetteDecayStart + 0.72;
	}

	void EnsureDamageVignetteLayer()
	{
		if ( _damageVignetteLayer is { IsValid: true } )
			return;
		if ( Panel is null || !Panel.IsValid )
			return;

		_damageVignetteLayer = ThornsUiPanelAdd.AddChildPanel( Panel, "thorns-debug-damage-vignette thorns-damage-vignette-root" );
		_damageVignetteLayer.Style.PointerEvents = PointerEvents.None;
		_damageVignetteLayer.Style.Position = PositionMode.Absolute;
		_damageVignetteLayer.Style.Left = 0;
		_damageVignetteLayer.Style.Top = 0;
		_damageVignetteLayer.Style.Right = 0;
		_damageVignetteLayer.Style.Bottom = 0;
		_damageVignetteLayer.Style.ZIndex = 900;
		_damageVignetteLayer.Style.Opacity = 0f;
	}

	void TickDamageVignettePresentation()
	{
		if ( _damageVignetteLayer is null || !_damageVignetteLayer.IsValid )
			return;

		var now = Time.Now;
		if ( now < _damageVignetteDecayStart )
		{
			_damageVignetteLayer.Style.Opacity = _damageVignetteDisplay01;
			return;
		}

		if ( now >= _damageVignetteFadeEnd )
		{
			_damageVignetteLayer.Style.Opacity = 0f;
			_damageVignetteDisplay01 = 0f;
			return;
		}

		var span = Math.Max( 0.001, _damageVignetteFadeEnd - _damageVignetteDecayStart );
		var t = (float)((now - _damageVignetteDecayStart) / span);
		var fade = MathF.Pow(1f - t, 1.35f);
		_damageVignetteLayer.Style.Opacity = _damageVignetteDisplay01 * fade;
	}

	static bool IsDamageVignetteRoot( Panel ch ) =>
		ch is { IsValid: true } && ch.HasClass( "thorns-damage-vignette-root" );

	static string _lastFrameMsLabel() => $"{ThornsPerfDebug.LastFrameMs:F1}ms";

	void ApplyPerfQuality( ThornsPerformanceQuality quality )
	{
		ThornsPerformanceQualityPresets.ApplyToActiveScene( quality );
		Log.Info( $"[Thorns] Performance quality → {quality}" );
		RequestHudRebuild();
	}
}
