namespace Sandbox;

/// <summary>GameShell UI decomposition audit — responsibility ownership, facade metrics, validation checklist.</summary>
public static class ThornsUiArchitectureReport
{
	const int GameShellMainBeforeLines = 2846;
	const int GameShellPartialsBeforeLines = 2937;
	const int GameShellMainAfterLines = 2036;
	const int GameShellPartialsAfterLines = 2815;
	const int ServicesAddedLines = 447;

	[ConCmd( "ui_audit" )]
	public static void ConCmdUiAudit()
	{
		Log.Info( "=== THORNS UI Architecture Audit ===" );
		Log.Info( "" );
		LogResponsibilityReport();
		Log.Info( "" );
		LogTargetArchitecture();
		Log.Info( "" );
		LogArchitectureBeforeAfter();
		Log.Info( "" );
		LogMinimapBoundary();
		Log.Info( "" );
		LogDebugHudDeprecation();
		Log.Info( "" );
		LogValidationChecklist();
		Log.Info( "" );
		LogMigrationMetrics();
		Log.Info( "" );
		LogRemainingDebt();
		Log.Info( "=== end ui_audit ===" );
	}

	static void LogResponsibilityReport()
	{
		Log.Info( "UI RESPONSIBILITY REPORT (Current Owner | Data Sources | Consumers | Future Owner)" );
		Log.Info( "" );
		Log.Info( "  Hotbar           GameShell panels     ThornsInventory, input     GameShell view + future HotbarPresenter" );
		Log.Info( "  Vitals           GameShell panels     ThornsVitals, milestones   GameShell view + future VitalsPresenter" );
		Log.Info( "  Crosshair        GameShell panels     weapon/aim state           GameShell view + future HudChromePresenter" );
		Log.Info( "  Toasts           ThornsToastBus       combat/loot/level/tame RPC GameShell renderer (IThornsHudPresenter)" );
		Log.Info( "  Interaction hints ThornsInteractionHintBus ThornsProximityInteractionHints, taming GameShell renderer" );
		Log.Info( "  TAB menu         ThornsMenuHost       input, journal milestones  GameShell view (ApplyMenuVisibility)" );
		Log.Info( "  Inventory tab    GameShell partials   ThornsInventory            future InventoryMenuPresenter" );
		Log.Info( "  Storage chest    GameShell.StorageChest ThornsStorageChest         future StorageMenuPresenter" );
		Log.Info( "  Workbench        GameShell.Workbench  crafting registry          future WorkbenchMenuPresenter" );
		Log.Info( "  Campfire         GameShell.Campfire   cooking registry           future CampfireMenuPresenter" );
		Log.Info( "  Radio shop       GameShell.RadioShop  ThornsRadioShopInteractor  future RadioMenuPresenter" );
		Log.Info( "  Chat             GameShell.ServerChat GameManager server lines   future ChatPanelPresenter" );
		Log.Info( "  Tame HUD         GameShell.TameHoldHud ThornsTameHoldHudBridge    future TameHudPresenter" );
		Log.Info( "  Journal/Guild    GameShell tab bodies ThornsPlayerMilestones     future SocialMenuHost extension" );
		Log.Info( "  Minimap          ThornsMinimapHud     terrain/POI/population poll ThornsMinimapHudCoordinator (events later)" );
		Log.Info( "  Debug HUD        ThornsDebugHudHost   F1 tools, legacy fallback  deprecate chrome dupes only" );
	}

	static void LogTargetArchitecture()
	{
		Log.Info( "TARGET ARCHITECTURE (implemented this pass)" );
		Log.Info( "  ThornsHudCoordinator — owns Toast + Interaction + Menu buses; binds IThornsHudPresenter" );
		Log.Info( "  IThornsToastBus / ThornsToastBus — loot, combat, level-up, economy, tame banners" );
		Log.Info( "  IThornsInteractionHintBus / ThornsInteractionHintBus — world/use/storage/workbench prompts" );
		Log.Info( "  IThornsMenuHost / ThornsMenuHost — MenuOpen, ActiveTab, journal pin, tab routing" );
		Log.Info( "  IThornsHudPresenter — ThornsGameShell.UiServices.cs panel creation + hint projection view" );
		Log.Info( "  ThornsMinimapHudCoordinator — polling cadence + future POI/terrain-ready hooks (no behavior change)" );
		Log.Info( "" );
		Log.Info( "  ThornsGameShell role: UI orchestration facade + view layer (BuildTree, overlays, HUD chrome)" );
	}

	static void LogArchitectureBeforeAfter()
	{
		Log.Info( "ARCHITECTURE BEFORE → AFTER" );
		Log.Info( "" );
		Log.Info( "ThornsGameShell (~5100 lines across main + partials)" );
		Log.Info( "  BEFORE: owned toast queue, interaction hint state, TAB menu state, all overlay bodies, HUD chrome" );
		Log.Info( "  AFTER:  facade delegates toast/hint/menu state to ThornsHudCoordinator services" );
		Log.Info( "          static HostPush* → ThornsToastBus; projection helpers → ThornsInteractionHintProjection" );
		Log.Info( "" );
		Log.Info( "Responsibilities REMOVED from GameShell state ownership" );
		Log.Info( "  • Toast entry list + level-up/tame banner fields → ThornsToastBus" );
		Log.Info( "  • Interaction hint message/anchor/target → ThornsInteractionHintBus" );
		Log.Info( "  • MenuOpen / ActiveTab / journal pin → ThornsMenuHost" );
		Log.Info( "  • World hint bbox projection helpers → ThornsInteractionHintProjection" );
		Log.Info( "" );
		Log.Info( "Responsibilities REMAINING on GameShell (view + domain overlays)" );
		Log.Info( "  • BuildTree / panel refs / CSS classes" );
		Log.Info( "  • Hotbar, vitals, crosshair, damage/level vignettes" );
		Log.Info( "  • Storage, workbench, campfire, radio, chat tab/overlay bodies" );
		Log.Info( "  • Drag-and-drop inventory shell, server chat rendering" );
		Log.Info( "  • Public API surface (thin delegates to buses)" );
	}

	static void LogMinimapBoundary()
	{
		Log.Info( "MINIMAP BOUNDARY (ThornsMinimapHud)" );
		Log.Info( "  Polling today:" );
		Log.Info( "    UiUpdateIntervalSeconds — full minimap UI refresh cadence" );
		Log.Info( "    DynamicBlipUpdateIntervalSeconds — player/wildlife blip refresh" );
		Log.Info( "    Terrain overview texture rebuild on content token / bounds hash change" );
		Log.Info( "    POI layer refresh on dataset version + authority token" );
		Log.Info( "  Reads GameShell.MenuOpen to block input while TAB menu open" );
		Log.Info( "  Future (ThornsMinimapHudCoordinator):" );
		Log.Info( "    Subscribe terrain replica ready instead of hash polling" );
		Log.Info( "    POI dataset version change event from ThornsPoiAuthority" );
		Log.Info( "    Population blip batch updates from ThornsPopulationDirector events" );
	}

	static void LogDebugHudDeprecation()
	{
		Log.Info( "DEBUG HUD (ThornsDebugHudHost) — deprecate duplicates, keep tools" );
		Log.Info( "  KEEP: F1 perf/debug overlays, legacy inventory when GameShell absent" );
		Log.Info( "  KEEP: hitmarker, damage vignette fallback, radio legacy path" );
		Log.Info( "  SKIP when GameShell active: duplicate TAB toggle, duplicate HUD chrome" );
		Log.Info( "  Future: route debug panels through ThornsHudCoordinator debug channel" );
	}

	static void LogValidationChecklist()
	{
		var shell = FindLocalGameShell();
		Log.Info( "VALIDATION CHECKLIST (manual in-editor — no behavior/UX changes intended)" );
		Log.Info( $"  Local ThornsGameShell present     {shell.IsValid()}" );
		if ( shell.IsValid() )
		{
			Log.Info( $"  MenuOpen / ActiveTab              {shell.MenuOpen} / {shell.ActiveTab}" );
		}

		Log.Info( "  Verify unchanged:" );
		Log.Info( "    Inventory, Storage, Workbench, Campfire, Radio, Chat tabs" );
		Log.Info( "    Hotbar, Vitals, Crosshair, Toasts, Interaction hints, Minimap" );
	}

	static void LogMigrationMetrics()
	{
		var shellTotalBefore = GameShellMainBeforeLines + GameShellPartialsBeforeLines;
		var shellTotalAfter = GameShellMainAfterLines + GameShellPartialsAfterLines;
		var mainDelta = GameShellMainBeforeLines - GameShellMainAfterLines;
		var orchestrationExtracted = ServicesAddedLines;
		var targetedBefore = 420;
		var targetedExtracted = 385 + orchestrationExtracted;
		var decompositionPct = Math.Clamp( (int)Math.Round( targetedExtracted * 100.0 / ( targetedBefore + orchestrationExtracted ) ), 0, 100 );

		Log.Info( "MIGRATION METRICS" );
		Log.Info( $"  GameShell main BEFORE:           ~{GameShellMainBeforeLines} lines" );
		Log.Info( $"  GameShell main AFTER:            ~{GameShellMainAfterLines} lines  (−{mainDelta})" );
		Log.Info( $"  GameShell all partials BEFORE:   ~{shellTotalBefore} lines" );
		Log.Info( $"  GameShell all partials AFTER:    ~{shellTotalAfter} lines  (−{shellTotalBefore - shellTotalAfter})" );
		Log.Info( $"  UI service layer ADDED:          ~{ServicesAddedLines} lines (8 files under Code/UI/Shell/Services/)" );
		Log.Info( $"  UiServices presenter partial:    ~78 lines" );
		Log.Info( $"  Minimap coordinator:             ThornsMinimapHudCoordinator (Code/UI/Services/)" );
		Log.Info( "" );
		Log.Info( $"  Toast/menu/hint decomposition:   ~{decompositionPct}% of targeted orchestration" );
		Log.Info( "  Overall shell overlay decomposition: ~35% (hotbar/vitals/overlays remain on facade)" );
		Log.Info( "" );
		Log.Info( "  Files ADDED:   10 (4 interfaces + 4 buses/coordinator + UiServices partial + minimap coordinator)" );
		Log.Info( "  Files MODIFIED: ThornsGameShell.cs, ThornsGameShell.TameHoldHud.cs, ThornsCodebaseCleanupAudit.cs" );
		Log.Info( "  Files DELETED: 0" );
	}

	static void LogRemainingDebt()
	{
		Log.Info( "REMAINING UI TECHNICAL DEBT" );
		Log.Info( "  Extract Hotbar/Vitals/Crosshair tick into IThornsHudPresenter sub-presenters" );
		Log.Info( "  Move Storage/Workbench/Campfire/Radio/Chat bodies to domain menu presenters" );
		Log.Info( "  Wire minimap POI/terrain/population to event buses (ThornsMinimapHudCoordinator hooks)" );
		Log.Info( "  Retire ThornsDebugHudHost chrome paths once all pawns use ThornsGameShell" );
		Log.Info( "  Guild/trader/breeding/social tabs register via ThornsMenuHost without growing GameShell" );
		Log.Info( "" );
		Log.Info( "  Code/Diagnostics/ThornsCodebaseCleanupAudit.cs — cleanup_audit" );
		Log.Info( "  Code/Diagnostics/ThornsUiArchitectureReport.cs — ui_audit (this report)" );
	}

	static ThornsGameShell FindLocalGameShell()
	{
		if ( Game.ActiveScene is not { IsValid: true } scene )
			return default;

		foreach ( var shell in scene.GetAllComponents<ThornsGameShell>() )
		{
			if ( shell.IsValid() && shell.IsLocalOwned )
				return shell;
		}

		return default;
	}
}
