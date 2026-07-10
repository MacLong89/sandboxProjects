namespace Sandbox;

/// <summary>
/// Optimization audit anchor — documents system costs, targets, and improvements applied.
/// Dump report: <c>perf_audit</c> console command.
/// </summary>
public static class ThornsOptimizationAudit
{
	public readonly struct SystemEntry
	{
		public string System { get; init; }
		public string CurrentCost { get; init; }
		public string ExpectedCost { get; init; }
		public string ImprovementPotential { get; init; }
	}

	static readonly SystemEntry[] Phase1Profile =
	[
		new() { System = "CPU — World-gen heightmap bake", CurrentCost = "Full FillHeightmap ×2 at boot (pre + mesh)", ExpectedCost = "Single bake + cache copy", ImprovementPotential = "High — implemented ThornsHeightmapBakeCache" },
		new() { System = "CPU — Terraingen field gen", CurrentCost = "Heightmap import + sculpt once per seed", ExpectedCost = "Cached in GetOrGenerateField", ImprovementPotential = "Low — already cached" },
		new() { System = "CPU — Foliage populate", CurrentCost = "Spread across frames (ChunksPerFrame budget)", ExpectedCost = "≤1 chunk/frame Medium preset", ImprovementPotential = "Medium — quality presets enforce caps" },
		new() { System = "CPU — Grass GPU instancing", CurrentCost = "Tile queue + 2ms build budget/frame", ExpectedCost = "≤1 tile/frame Low, 2 Ultra", ImprovementPotential = "Medium — ClientGrassRenderer budgets" },
		new() { System = "CPU — Wildlife AI", CurrentCost = "LOD think ×14 dormant; LOS cap 112/fixed", ExpectedCost = "Scales with nearby players", ImprovementPotential = "Low — ThornsWildlifeLOD + spatial index" },
		new() { System = "CPU — Bandit AI", CurrentCost = "Dormant 2.4s think; spatial player query", ExpectedCost = "Same as wildlife tier", ImprovementPotential = "Low — ThornsBanditDirector cache" },
		new() { System = "CPU — Celestial / sky", CurrentCost = "Preset-throttled Apply (8–20 Hz) + sprite draw", ExpectedCost = "No per-frame sky rebuild", ImprovementPotential = "Low — throttled Apply + sprite Hz" },
		new() { System = "CPU — Decor distance cull", CurrentCost = "Preset budget 1600–4000 proxies/tick", ExpectedCost = "Near-cell priority + round-robin", ImprovementPotential = "Medium — quality-tier step budgets" },
		new() { System = "GPU — Foliage draw", CurrentCost = "Tree LOD shadow/hide; chunk cull", ExpectedCost = "Instanced grass; culled chunks", ImprovementPotential = "Medium — ThornsFoliageLod + streaming tiers" },
		new() { System = "GPU — Terrain", CurrentCost = "Single Sandbox.Terrain chunk", ExpectedCost = "One rebuild per spec change", ImprovementPotential = "Medium — future multi-chunk streaming" },
		new() { System = "Memory / GC", CurrentCost = "ArrayPool heightmaps; clutter GO pool", ExpectedCost = "Minimal per-frame alloc", ImprovementPotential = "Medium — overlay list reuse, no LINQ hot paths" },
		new() { System = "Network — Terrain join", CurrentCost = "Binary v1 spec Base64 one-shot", ExpectedCost = "<512 KB downstream burst", ImprovementPotential = "Medium — compact replica already shipped" },
		new() { System = "Network — Inventory", CurrentCost = "Delta slots ≤10 else full snapshot", ExpectedCost = "<16 KB per RPC", ImprovementPotential = "Low — delta path exists" },
		new() { System = "Physics", CurrentCost = "Terrain collider + anchored structures", ExpectedCost = "Sleep on static world solids", ImprovementPotential = "Low — world solids tagged, no dynamic spam" },
		new() { System = "Input / camera", CurrentCost = "Local owner immediate look; smoothed Z eye", ExpectedCost = "Snappy vertical tracking", ImprovementPotential = "Medium — faster eye smooth defaults" },
	];

	public static IReadOnlyList<SystemEntry> GetPhase1Profile() => Phase1Profile;

	[ConCmd( "perf_audit" )]
	public static void CmdPerfAudit()
	{
		Log.Info( "========== THORNS OPTIMIZATION AUDIT (Phase 1) ==========" );
		Log.Info( $"{"System",-42} | {"Current",-28} | {"Expected",-28} | Potential" );
		foreach ( var e in Phase1Profile )
			Log.Info( $"{e.System,-42} | {e.CurrentCost,-28} | {e.ExpectedCost,-28} | {e.ImprovementPotential}" );

		Log.Info( "========== IMPLEMENTED THIS PASS ==========" );
		foreach ( var line in ImplementedChanges )
			Log.Info( $"  • {line}" );

		Log.Info( "========== RUNTIME ==========" );
		Log.Info( $"  Quality preset: {ThornsPerformanceQualityPresets.ActiveQuality}" );
		Log.Info( $"  FPS {ThornsPerfDebug.Fps:F0} avg {ThornsPerfDebug.AvgFps:F0} frame {ThornsPerfDebug.LastFrameMs:F1}ms" );
		Log.Info( $"  World-gen total: {ThornsPerfDebug.WorldGenTotalMs:F0}ms  load→playable: {ThornsPerfDebug.FormatLoadMs()}" );
		Log.Info( $"  Foliage visible: {ThornsPerfDebug.FoliageInstancesVisible}  grass: {ThornsPerfDebug.GrassInstancesVisible}" );
		Log.Info( $"  Deferred queue: {ThornsPerfDebug.DeferredQueuePending}" );
		Log.Info( "  Dead-code cleanup: run cleanup_audit for removed files and future opportunities" );
	}

	public static readonly string[] ImplementedChanges =
	[
		"ThornsHeightmapBakeCache — world-gen heightfield reused for terrain mesh + scatter (eliminates duplicate FillHeightmap)",
		"ThornsHeightmapBakeCache.RegisterMeshBakeCopy — client/host rebake skipped when TerrainSpecContentHash matches",
		"ThornsHeightmapBakeCache.TryDownsample — minimap overview from cached bake (FillHeightmapBase fallback)",
		"ThornsDynamicSupplyDirector — RentFilled heightmap path (cache-first)",
		"ThornsDeferredWorldGenerationSession — one pre-chunk world-gen phase per frame (TimeSlicePreChunkWorldGen)",
		"Interior furniture scatter — one building per deferred queue work item",
		"Macro terrain phase uses FillHeightmapBase only (settlement/road phases sculpt in-place)",
		"ThornsFoliageDistanceCullSystem — spatial buckets + near-cell priority processing",
		"ThornsFoliageDistanceCullSystem — quality-preset cull budgets (2400/step Medium @ 0.2s)",
		"ThornsCelestialSystem — throttled sky/light Apply (preset Hz, skip redundant per-frame work)",
		"ThornsCelestialSprites — throttled HUD draw + cached celestial Instance lookup",
		"ThornsWildlifeAnimSync — skip redundant locomotion ordinal net sync when unchanged",
		"ThornsWildlifeLocomotionAnimSelector — single host presentation authority with change detection",
		"ThornsPerfDebugHost — throttle playable pawn scene scan (0.5s)",
		"ThornsFoliageConfig — lower authoring defaults; quality preset LOD chunk + shadow caps",
		"ThornsFoliageFoundation — skip instance LOD updates for chunks beyond tree hide distance",
		"ThornsPawnCamera — snappier vertical eye smoothing",
	];
}
