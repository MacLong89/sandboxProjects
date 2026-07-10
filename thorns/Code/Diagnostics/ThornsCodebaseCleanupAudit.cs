namespace Sandbox;

/// <summary>
/// Codebase consolidation audit — dead code, duplicates, legacy systems, and cleanup actions.
/// Dump: <c>cleanup_audit</c> console command.
/// </summary>
public static class ThornsCodebaseCleanupAudit
{
	public static readonly string[] RemovedThisPass =
	[
		"Wildlife/ThornsWildlifeWolfAnimLoop.cs — zero refs; wolf uses ThornsWildlifeElkAnimDriver",
		"World/Celestial/ThornsCelestialSkyPresets.cs — zero refs; ThornsCelestialSkyCarrier is canonical",
		"Terrain/Repair/ThornsTerrainMeshStability.cs — zero refs; repair pipeline replaced it",
		"AI/StateMachine/ThornsBanditThreatSystem.cs — unwired stub (TryAcquireTarget is active path)",
		"AI/StateMachine/ThornsBanditLocomotionAnim.cs + ThornsBanditLocomotionAnimSelector.cs — unused (CitizenBodyDriver handles bandit anim)",
		"ThornsTerrainGenerator.ToTerrainHeightmap() — zero callers",
		"ThornsBanditBrain.HostTransitionToLeashWander() — uncalled after state-machine migration",
		"ThornsBanditBrain.BanditAiState nested enum — superseded by ThornsBanditAiState",
	];

	public static readonly string[] MergedOrCanonical =
	[
		"Terrain height: FillHeightmap + ThornsHeightmapBakeCache (world-gen uses incremental phases + cache register)",
		"Terrain smooth: TerrainSculptPipeline + ThornsTerrainRepairPipeline (spec EnableSmoothing is network-compat only)",
		"Wildlife AI: ThornsAnimalStateMachine sole authority; DecisionService + TargetingService + ThreatPipeline",
		"Bandit AI: ThornsBanditStateMachine + TryAcquireTarget + ThornsBanditDetectionSystem",
		"Sky/atmosphere: ThornsCelestialSystem (ThornsSun kept as scene-compat alias)",
		"Tree foliage LOD: ThornsFoliageFoundation / ThornsFoliageLod",
		"Decor foliage cull: ThornsFoliageDistanceCullSystem (inactive when terraingen enabled)",
		"Player spatial cache: ThornsPopulationDirector facade → ThornsWildlifeDirector (impl); bandit queries consolidated",
		"Population authority: ThornsPopulationDirector owns player spatial cache + budgets; directors are wrappers",
		"Persistence: ThornsWorldPersistence facade + domain snapshot services + ThornsPersistenceSerializer",
		"Terrain: ThornsTerrainSystem facade + chunk/worldgen/scatter/heightmap/replica services",
		"UI shell: ThornsGameShell facade + ThornsHudCoordinator (toast/interaction/menu buses)",
		"Inventory: ThornsInventory facade + replication/consumable/crafting/storage services",
		"Weapon: ThornsWeapon facade + input/combat/ammo/reload/fx/observer services",
		"Population registries: internal impl only (WildlifePopulation, BanditPopulation)",
		"Spawn factories: ThornsWildlifeSpawn, ThornsNpcHumanBanditSpawn (schedulers are separate triggers)",
		"Supply events: ThornsDynamicSupplyDirector (airdrop guard spawner is helper only)",
	];

	public static readonly string[] FutureCleanup =
	[
		"Wire city/airdrop spawn budgets through ThornsPopulationDirector",
		"Centralize spawner cap values into ThornsPopulationBudgetPolicy",
		"Optional: wildlife states call ThornsAnimalDecisionService directly (drop StateMachineRun* bridges)",
		"Split persistence save format by domain (player/structure/wildlife shards) after network version bump",
		"Move ThornsTerrainDecorScatter into ThornsWorldScatterService for full scatter unification",
		"Wire ThornsBanditLocomotionAnimSelector into ThornsCitizenBodyDriver.TickBanditCitizenPresentation when bandit anim decoupling is desired",
		"Extract GameShell overlay partials (Storage/Workbench/Campfire/Radio) into domain menu presenters",
		"ThornsInventoryMutationService — ServerAddItem/Move/Swap off facade",
		"ThornsInventoryEquipmentService — unify ThornsHotbarEquipment + ThornsArmorEquipment routing",
		"Weapon attachment/rarity modifiers via ThornsWeaponAmmoService + HostCombatService hooks",
		"Minimap: subscribe terrain-ready + POI version events instead of polling (ThornsMinimapHudCoordinator)",
		"Retire ThornsDebugHudHost duplicate chrome when all player prefabs use ThornsGameShell",
		"Remove ThornsSun after confirming no scene/prefab components (search Assets/ for ThornsSun)",
		"File structure migration: World/Terrain, AI/Animals, AI/Bandits, Atmosphere/ (552 files — do incrementally)",
		"Strip dead ThornsTerrainNetSpec.EnableSmoothing from runtime spec after network version bump",
		"ThornsGameplayDiagnostics.EnablePeriodicLogs — never enabled; wire or remove periodic flush path",
		"ClientGrassRenderer tile list init LINQ — move to static scratch if profiled hot",
	];

	public static readonly string[] ActiveDebugTools =
	[
		"perf_debug / perf_quality — ThornsPerfDebug + ThornsPerfDebugHost",
		"perf_audit — ThornsOptimizationAudit",
		"cleanup_audit — this report",
		"wildlife_ai_audit — ThornsWildlifeAiArchitectureReport",
		"persistence_audit — ThornsPersistenceArchitectureReport",
		"terrain_audit — ThornsTerrainArchitectureReport",
		"ui_audit — ThornsUiArchitectureReport",
		"inventory_weapon_audit — ThornsInventoryWeaponArchitectureReport",
		"sky_debug / freeze_time / set_time — ThornsCelestialDebug",
		"ThornsAnimalAiDebugViz / ThornsBanditAiDebugViz — opt-in gizmo components",
		"ThornsTerrainRepairDebug + settlement *DebugViz — gated by debug flags",
	];

	[ConCmd( "cleanup_audit" )]
	public static void CmdCleanupAudit()
	{
		Log.Info( "========== THORNS CODEBASE CLEANUP AUDIT ==========" );
		Log.Info( "=== REMOVED THIS PASS ===" );
		foreach ( var line in RemovedThisPass )
			Log.Info( $"  • {line}" );

		Log.Info( "=== CANONICAL / MERGED SYSTEMS ===" );
		foreach ( var line in MergedOrCanonical )
			Log.Info( $"  • {line}" );

		Log.Info( "=== FUTURE CLEANUP (no behavior change yet) ===" );
		foreach ( var line in FutureCleanup )
			Log.Info( $"  • {line}" );

		Log.Info( "=== ACTIVE DEBUG TOOLS ===" );
		foreach ( var line in ActiveDebugTools )
			Log.Info( $"  • {line}" );

		Log.Info( "=== VALIDATION CHECKLIST ===" );
		Log.Info( "  Terrain: world-gen pipeline + FillHeightmap cache + repair pass" );
		Log.Info( "  Settlements/Roads: ThornsWorldGenerationPipeline phases" );
		Log.Info( "  Wildlife: StateMachineTickActive + anim drivers (elk/panther)" );
		Log.Info( "  Bandits: StateMachineTickActive + ThornsNpcHumanBanditSpawn" );
		Log.Info( "  Atmosphere: ThornsCelestialSystem day/night + fog" );
		Log.Info( "  Streaming: ThornsTerrainSystem + deferred world-gen session" );
		Log.Info( "  Loot: ThornsLootCrate + airdrop guard loot on death" );
		Log.Info( "  UI: GameShell facade + toast/interaction/menu buses (ui_audit)" );
		Log.Info( "  Inventory/Weapon: facade + domain services (inventory_weapon_audit)" );
	}
}
