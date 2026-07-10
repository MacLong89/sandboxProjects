namespace Sandbox;

/// <summary>Inventory + weapon facade decomposition audit — responsibility ownership, RPC safety, validation checklist.</summary>
public static class ThornsInventoryWeaponArchitectureReport
{
	const int InventoryMainBeforeLines = 1828;
	const int WeaponMainBeforeLines = 2026;
	const int InventoryMainAfterLines = 1204;
	const int InventoryServicesPartialLines = 98;
	const int InventoryServiceLayerLines = 873;
	const int WeaponMainAfterLines = 922;
	const int WeaponServicesPartialLines = 223;
	const int WeaponServiceLayerLines = 1339;

	[ConCmd( "inventory_weapon_audit" )]
	public static void ConCmdInventoryWeaponAudit()
	{
		Log.Info( "=== THORNS Inventory + Weapon Architecture Audit ===" );
		Log.Info( "" );
		LogResponsibilityReport();
		Log.Info( "" );
		LogTargetArchitecture();
		Log.Info( "" );
		LogArchitectureBeforeAfter();
		Log.Info( "" );
		LogMultiplayerSafetyAudit();
		Log.Info( "" );
		LogValidationChecklist();
		Log.Info( "" );
		LogMigrationMetrics();
		Log.Info( "" );
		LogRemainingDebt();
		Log.Info( "=== end inventory_weapon_audit ===" );
	}

	static void LogResponsibilityReport()
	{
		Log.Info( "INVENTORY RESPONSIBILITY (Owner | Future Owner | Risk)" );
		Log.Info( "  Slot mutations / ServerAddItem     ThornsInventory facade          CRITICAL — host authority" );
		Log.Info( "  Owner mirror / delta sync          ThornsInventoryReplicationService HIGH — replication correctness" );
		Log.Info( "  Consumables / channel use          ThornsInventoryConsumableService  HIGH — vitals + timing" );
		Log.Info( "  Crafting recipes                   ThornsInventoryCraftingService    HIGH — economy invariant" );
		Log.Info( "  Storage/campfire/workbench RPCs    ThornsInventoryStorageService     MED — structure transfers" );
		Log.Info( "  Hotbar equip                       ThornsHotbarEquipment (separate)  MED — future EquipmentService" );
		Log.Info( "  Armor equip                        ThornsArmorEquipment (separate)   MED — future EquipmentService" );
		Log.Info( "  Item definitions                   ThornsItemRegistry                LOW — data" );
		Log.Info( "" );
		Log.Info( "WEAPON RESPONSIBILITY (Owner | Future Owner | Risk)" );
		Log.Info( "  Client input / fire intent         ThornsWeaponInputService          MED — intent only" );
		Log.Info( "  Host fire validation + hitscan     ThornsWeaponHostCombatService     CRITICAL — combat authority" );
		Log.Info( "  Ammo consume + HUD mirror          ThornsWeaponAmmoService           CRITICAL — ammo correctness" );
		Log.Info( "  Reload timing / pump sessions      ThornsWeaponReloadService         HIGH — reload rules" );
		Log.Info( "  Owner FX / hitmarker / RpcFireOutcome ThornsWeaponClientFxService    MED — presentation" );
		Log.Info( "  Observer gunshot sync              ThornsWeaponObserverSyncService   MED — bandwidth + spatial" );
		Log.Info( "  Hitscan trace / damage resolve     ThornsSharedHostHitscan           CRITICAL — shared combat" );
		Log.Info( "  Weapon definitions                 ThornsWeaponDefinitions           LOW — data" );
		Log.Info( "  FP/TP presentation                 ThornsWeapon facade               MED — view layer" );
	}

	static void LogTargetArchitecture()
	{
		Log.Info( "TARGET ARCHITECTURE (implemented)" );
		Log.Info( "  ThornsInventory + ThornsInventoryCoordinator" );
		Log.Info( "    ThornsInventoryReplicationService — mirror, delta, full snapshot fallback" );
		Log.Info( "    ThornsInventoryConsumableService — food/water/medical channel" );
		Log.Info( "    ThornsInventoryCraftingService — recipe validation + output" );
		Log.Info( "    ThornsInventoryStorageService — chest/campfire/workbench RPC bodies" );
		Log.Info( "  ThornsWeapon + ThornsWeaponCoordinator" );
		Log.Info( "    ThornsWeaponInputService — Attack1/2 intent, consumable-from-attack" );
		Log.Info( "    ThornsWeaponHostCombatService — RequestFire host authority" );
		Log.Info( "    ThornsWeaponAmmoService — loaded/reserve mirror, per-shot consume" );
		Log.Info( "    ThornsWeaponReloadService — RequestReload + HostReloadAsync" );
		Log.Info( "    ThornsWeaponClientFxService — RpcFireOutcome owner presentation" );
		Log.Info( "    ThornsWeaponObserverSyncService — spatial gunshot broadcast" );
		Log.Info( "" );
		Log.Info( "  RPC attributes remain on ThornsInventory / ThornsWeapon components (network boundary unchanged)" );
	}

	static void LogArchitectureBeforeAfter()
	{
		Log.Info( "ARCHITECTURE BEFORE → AFTER" );
		Log.Info( "" );
		Log.Info( "ThornsInventory (~1828 lines monolith)" );
		Log.Info( "  BEFORE: replication + consumables + crafting + storage RPCs + slot API in one file" );
		Log.Info( "  AFTER:  facade (~1204) + Services partial + 4 domain services" );
		Log.Info( "  REMOVED from facade: mirror state, consumable pending, craft logic, structure transfer bodies" );
		Log.Info( "  REMAINING: _slots[], ServerAddItem/Move/Swap, ammo helpers, armor placement, dev RPCs, persistence hooks" );
		Log.Info( "" );
		Log.Info( "ThornsWeapon (~2026 lines monolith)" );
		Log.Info( "  BEFORE: input + host fire + reload + ammo + FX + observer sync intertwined" );
		Log.Info( "  AFTER:  facade (~922) + Services partial + 6 domain services" );
		Log.Info( "  REMOVED from facade: RequestFire body, HostReloadAsync, RpcFireOutcome body, observer gunshot" );
		Log.Info( "  REMAINING: properties, FP presentation, hitscan helpers, cooldown/recoil fields, TP world visual" );
	}

	static void LogMultiplayerSafetyAudit()
	{
		Log.Info( "MULTIPLAYER RPC SAFETY (behavior unchanged — audit only)" );
		Log.Info( "" );
		Log.Info( "ThornsInventory [Rpc.Host] — all validate ThornsPawn.ValidateHostRpcCallerOwnsPawnRoot + alive check" );
		Log.Info( "  RequestMoveItem / RequestSwapSlots     caller=owner  rate=implicit move cadence  fields=_slots" );
		Log.Info( "  RequestUseItemFromSlot                 caller=owner  channel+cooldown gate       fields=_slots,vitals" );
		Log.Info( "  RequestCraftRecipe                     caller=owner  recipe+tier validation      fields=_slots" );
		Log.Info( "  RequestOpen*/Transfer* (storage)       caller=owner  structure range validation  fields=chest+bench" );
		Log.Info( "  RequestDropInventorySlotToWorld        caller=owner  spawn+remove atomic         fields=_slots" );
		Log.Info( "ThornsInventory [Rpc.Owner] — owner-only mirror push" );
		Log.Info( "  ClientReceiveInventorySnapshot/Delta   authority=host  client mirror only        non-authoritative UI" );
		Log.Info( "" );
		Log.Info( "ThornsWeapon [Rpc.Host]" );
		Log.Info( "  RequestFire(dir,variant,ads)           caller=owner  aim+origin+rate+ammo host   fields=slot ammo/dur" );
		Log.Info( "  RequestReload()                        caller=owner  reload state machine        fields=slot+reserve" );
		Log.Info( "ThornsWeapon [Rpc.Owner]" );
		Log.Info( "  RpcFireOutcome / ClientReceiveWeaponHudState / RpcPlayOwnerWeaponSound — presentation only" );
		Log.Info( "ThornsWeapon [Rpc.Broadcast]" );
		Log.Info( "  RpcObserversPlayerGunWorldShot         rate=ObserverPlayerGunshotMaxRpcHz + spatial cull" );
		Log.Info( "" );
		Log.Info( "Shared: ThornsSharedHostHitscan — host-only damage resolution (no client trust)" );
	}

	static void LogValidationChecklist()
	{
		Log.Info( "VALIDATION CHECKLIST (manual in-editor — no behavior changes intended)" );
		Log.Info( "  Inventory opens, items move/stack, loot pickup, crafting, consumables, armor, storage" );
		Log.Info( "  Ammo counts, reload (mag + shotgun pump), weapons fire, damage, hitmarkers, FX/audio" );
		Log.Info( "  Observer gunshot sync, death loot, persistence inventory snapshot on quit" );
	}

	static void LogMigrationMetrics()
	{
		var invFacadeAfter = InventoryMainAfterLines + InventoryServicesPartialLines;
		var invDecompBefore = 0;
		var invDecompAfter = (int)Math.Round(
			InventoryServiceLayerLines * 100.0 / ( InventoryServiceLayerLines + invFacadeAfter ) );
		var wpnFacadeAfter = WeaponMainAfterLines + WeaponServicesPartialLines;
		var wpnDecompAfter = (int)Math.Round(
			WeaponServiceLayerLines * 100.0 / ( WeaponServiceLayerLines + wpnFacadeAfter ) );

		Log.Info( "MIGRATION METRICS" );
		Log.Info( $"  ThornsInventory main BEFORE:     ~{InventoryMainBeforeLines} lines" );
		Log.Info( $"  ThornsInventory main AFTER:      ~{InventoryMainAfterLines} lines  (−{InventoryMainBeforeLines - InventoryMainAfterLines})" );
		Log.Info( $"  Inventory service layer:         ~{InventoryServiceLayerLines} lines (9 files)" );
		Log.Info( $"  Inventory decomposition BEFORE:  ~{invDecompBefore}%" );
		Log.Info( $"  Inventory decomposition AFTER:   ~{invDecompAfter}%" );
		Log.Info( "" );
		Log.Info( $"  ThornsWeapon main BEFORE:        ~{WeaponMainBeforeLines} lines" );
		Log.Info( $"  ThornsWeapon main AFTER:         ~{WeaponMainAfterLines} lines  (−{WeaponMainBeforeLines - WeaponMainAfterLines})" );
		Log.Info( $"  Weapon service layer:            ~{WeaponServiceLayerLines} lines (10 files)" );
		Log.Info( $"  Weapon decomposition BEFORE:     ~{invDecompBefore}%" );
		Log.Info( $"  Weapon decomposition AFTER:      ~{wpnDecompAfter}%" );
		Log.Info( "" );
		Log.Info( "  Files ADDED:   21 (inventory 9 + weapon 10 + 2 partials)" );
		Log.Info( "  Files MODIFIED: ThornsInventory.cs, ThornsWeapon.cs, ThornsCodebaseCleanupAudit.cs" );
		Log.Info( "  Files DELETED: 0" );
		Log.Info( "" );
		Log.Info( "  REMAINING RISKS:" );
		Log.Info( "    Slot mutation API still on inventory facade (~1200 lines)" );
		Log.Info( "    Host combat still couples to ThornsHealth/ThornsSharedHostHitscan" );
		Log.Info( "    Armor/hotbar equipment not yet folded into inventory coordinator" );
		Log.Info( "    Verify s&box RPC codegen accepts internal RequestFire/RequestReload on partial" );
	}

	static void LogRemainingDebt()
	{
		Log.Info( "REMAINING TECHNICAL DEBT" );
		Log.Info( "  ThornsInventoryEquipmentService — fold ThornsHotbarEquipment + ThornsArmorEquipment RPC routing" );
		Log.Info( "  Extract ServerAddItem/Move/Swap into ThornsInventoryMutationService" );
		Log.Info( "  Weapon attachment/rarity hooks on ThornsWeaponAmmoService + combat service" );
		Log.Info( "  Rate-limit inventory move RPCs if profiling shows spam" );
		Log.Info( "" );
		Log.Info( "  Code/Diagnostics/ThornsCodebaseCleanupAudit.cs — cleanup_audit" );
		Log.Info( "  Code/Diagnostics/ThornsInventoryWeaponArchitectureReport.cs — inventory_weapon_audit" );
	}
}
