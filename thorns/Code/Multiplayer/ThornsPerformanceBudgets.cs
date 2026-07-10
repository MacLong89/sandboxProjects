namespace Sandbox;

/// <summary>
/// Production-scale performance <b>budgets</b> and <b>audit anchors</b> for Thorns (Source 2 / s&amp;box).
/// Values are engineering targets for profiling and design review — they do not clamp runtime behavior.
/// Update this file when ship bar or topology (player count, world size) changes.
/// </summary>
public static class ThornsPerformanceBudgets
{
	// --- Scale assumptions (design bar) ---
	/// <summary>Concurrent human players the netcode and server sim should tolerate without redesign.</summary>
	public const int DesignTargetConcurrentPlayers = 64;

	/// <summary>Order-of-magnitude world entities (players + AI + structures + loot) before dedicated partitioning.</summary>
	public const int DesignTargetWorldGameplayEntities = 12_000;

	// --- Server CPU (game thread) ---
	/// <summary>Soft cap: steady-state server frame budget at 60 Hz sim (ms). Leave headroom for GC / spikes.</summary>
	public const float ServerFrameBudgetMsAt60Hz = 12f;

	/// <summary>Hard spike budget: single slow frame (ms) before players feel hitches — investigate if exceeded during combat.</summary>
	public const float ServerFrameSpikeWarnMs = 22f;

	// --- Client CPU / render thread ---
	/// <summary>Soft cap: client frame budget (ms) at 60 FPS display — GPU may own more of the budget.</summary>
	public const float ClientFrameBudgetMsAt60Fps = 14f;

	/// <summary>Decorative props visible within draw distance — beyond this, rely on culling / impostors / instancing.</summary>
	public const int ClientDecorDrawBudgetVisibleProxies = 4_000;

	// --- Networking (aggregate) ---
	/// <summary>Soft cap: total downstream game-state bandwidth per client (KB/s) on a busy server.</summary>
	public const int TargetDownstreamKilobytesPerClientPerSecondSoftCap = 512;

	/// <summary>Soft cap: upstream intent (movement, weapon, UI commands) per client (KB/s).</summary>
	public const int TargetUpstreamKilobytesPerClientPerSecondSoftCap = 96;

	/// <summary>Inventory / UI bulk snapshots should stay under this size (bytes) where possible; split or delta above.</summary>
	public const int TargetMaxSingleOwnerRpcPayloadBytesSoftCap = 16_384;

	/// <summary>Legacy alias — mount steer uses <see cref="MaxMountInputRpcHz"/> (throttled + quantized <see cref="ThornsWildlifeMountInteractor"/>).</summary>
	public const float TargetMaxHighFrequencyInputRpcHz = 20f;

	// --- Mount rider input (upstream) — see ThornsWildlifeMountInteractor ---
	/// <summary>Upper bound on mount steer RPC rate per mounted player after throttling (Hz). At 64 riders worst-case ≈ this × 64 steer RPC/s if everyone holds a changing stick.</summary>
	public const float MaxMountInputRpcHz = 20f;

	/// <summary>Minimum spacing between mount steer sends from the owning client (<c>1 / <see cref="MaxMountInputRpcHz"/></c> seconds).</summary>
	public static float MountInputSendInterval => 1f / MaxMountInputRpcHz;

	/// <summary>Stick magnitude below this (local analog, before world rotation) is treated as neutral.</summary>
	public const float MountInputAnalogDeadzone = 0.085f;

	/// <summary>Quantization steps per axis for planar steer in <c>[-1,1]</c> before RPC (higher = finer).</summary>
	public const int MountInputQuantizationSteps = 14;

	/// <summary>Even if quantized steer is unchanged, resend at least this often so the host refreshes receive time and unreliable drops self-heal.</summary>
	public const float MountInputForceSendInterval = 0.22f;

	/// <summary>After this many seconds without an accepted steer RPC, host treats rider input as neutral (see <see cref="ThornsWildlifeBrain.HostTickMountedRiderMotorOnly"/>).</summary>
	public const float MountInputHostReceiveTimeoutSeconds = 0.38f;

	// --- Replication surface (audit checklist) ---
	/// <summary>Count of <c>[Sync]</c> fields on a single tame — high churn on tame XP / upgrades multiplies bandwidth. See <see cref="ThornsWildlifeIdentity"/>.</summary>
	public const int AuditTameIdentitySyncFieldCountApprox = 12;

	/// <summary>Max changed slots before inventory owner RPC falls back to full 38-slot snapshot.</summary>
	public const int InventoryDeltaMaxSlotsBeforeFullSnapshot = 10;

	/// <summary>Host wildlife planar speed anim hint sync rate (Hz) — see <see cref="ThornsWildlifeAnimSync.HostSetLocomotionPlanarSpeed"/>.</summary>
	public const float WildlifeLocomotionPlanarSpeedSyncHz = 12f;

	/// <summary>Per-player upgrade ranks replicated from host — see <see cref="ThornsPlayerUpgrades"/>.</summary>
	public const int AuditPlayerUpgradeSyncFieldCountApprox = 15;

	// --- Entity lifetime / replication count ---
	/// <summary>Decor grass/mushroom instances (local-only under terrain chunk) — no longer networked entities; see <see cref="ThornsTerrainDecorScatter"/>.</summary>
	public const int AuditDecorGrassPatchBudgetFromTerrainSettings = 1_500;

	/// <summary>Structures + crates should use stable instance IDs and registry lookups — prefer <see cref="ThornsPlacedStructure.ActiveByInstanceId"/> over scene scans.</summary>
	public const float AuditPreferredStructureLookupMsBudget = 0.05f;

	// --- Physics / traces ---
	/// <summary>Ray tests per weapon fire (segments) — see <see cref="ThornsSharedHostHitscan"/>.</summary>
	public const int AuditHostHitscanRaySegmentsMax = 4;

	/// <summary>Host wildlife LOS: shared per-physics-step cap + per-think probe cap — see <see cref="ThornsWildlifeLosBudget"/> / <see cref="ThornsWildlifePerception"/>.</summary>
	public const int AuditWildlifeLosTracesPerThink = 1;

	// --- AI ---
	/// <summary>Wildlife think interval multiplier at dormant LOD — see <see cref="ThornsWildlifeLOD"/>.</summary>
	public const float AuditWildlifeDormantThinkIntervalMultiplier = 14f;

	/// <summary>Player root cache refresh (s) — see <see cref="ThornsPopulationDirector"/> / <see cref="ThornsWildlifeDirector"/> / <see cref="ThornsBanditDirector"/>.</summary>
	public const float AuditDirectorPlayerCacheRefreshSecondsDefault = 2f;

	// --- Host AI perception / spatial (wildlife + bandits) ---
	/// <summary>Planar hash cell size for <see cref="ThornsHostPlayerSpatialIndex"/> (owned by <see cref="ThornsPopulationDirector"/>).</summary>
	public const float HostPlayerSpatialCellSizeWorld = 512f;

	/// <summary>Inflates spatial query radius so edge ghost multipliers / jitter still find the same candidates as a full scan.</summary>
	public const float HostPlayerSpatialQueryRadiusInflateMul = 1.32f;

	/// <summary>Planar hash cell size for <see cref="ThornsHostWildlifeSpatialIndex"/> (world units).</summary>
	public const float HostWildlifeSpatialCellSizeWorld = 128f;

	/// <summary>Inflates wildlife peer separation query radius at bucket edges.</summary>
	public const float HostWildlifeSpatialQueryRadiusInflateMul = 1.12f;

	/// <summary>Upper bound on fauna capsule radius used to size peer separation queries (boss-scale wildlife).</summary>
	public const float HostWildlifePeerMaxPlanarRadius = 96f;

	/// <summary>Hard cap on wildlife→player LOS rays per physics step across the whole server (shared budget).</summary>
	public const int HostWildlifeMaxLosRaysPerFixed = 112;

	/// <summary>Max player roots evaluated (distance/ghost filters) per predator perception call after spatial query.</summary>
	public const int HostWildlifeMaxPlayerCandidatesPerPredatorThink = 28;

	/// <summary>Max actual LOS ray attempts per predator player-target selection (after sorting by distance).</summary>
	public const int HostWildlifeMaxLosProbesPerPredatorThink = 5;

	/// <summary>TTL for positive-only wildlife LOS cache hits (seconds).</summary>
	public const float HostWildlifeLosPositiveCacheTtlSeconds = 0.16f;

	// --- GPU / memory (content-side; enforce in asset pipeline) ---
	/// <summary>Unique skinned wildlife species concurrently animating near camera — LOD / impostor beyond.</summary>
	public const int AuditSkinnedWildlifeNearCameraBudget = 32;

	/// <summary>Terrain: replicated spec is compact v1 binary (Base64) + hash — see <see cref="ThornsTerrainChunk.SyncSpecPayloadV1Base64"/>; legacy JSON fallback <see cref="ThornsTerrainChunk.SyncSpecJson"/>.</summary>
	public const int AuditTerrainChunkSyncJsonWarnBytesSoftCap = 262_144;

	/// <summary>Minimap POI: v1 binary replica + hash — see <see cref="ThornsPoiAuthority.PoiDatasetPayloadV1Base64"/>; legacy <see cref="ThornsPoiAuthority.PoiDatasetJson"/>.</summary>
	public const int AuditPoiAuthorityJsonWarnBytesSoftCap = 65_536;

	// --- GC ---
	/// <summary>Max inventory snapshot slot array length — <see cref="ThornsInventory.TotalSlots"/>; pooling candidate on host if profiling shows churn.</summary>
	public const int AuditInventorySlotArrayLength = 38;

	// --- Combat replication (PvP) — applied in code; tune from profiling ---
	/// <summary>Max <c>RpcDamageFloater</c> broadcasts per victim per second (coalesces chip damage).</summary>
	public const float DamageFloaterMaxBroadcastHz = 10f;

	/// <summary>Min spacing between observer gunshot <c>Rpc.Broadcast</c> per shooter (Hz).</summary>
	public const float ObserverPlayerGunshotMaxRpcHz = 14f;

	/// <summary>Min spacing between NPC gunshot <c>Rpc.Broadcast</c> per bandit (Hz).</summary>
	public const float ObserverNpcGunshotMaxRpcHz = 14f;

	/// <summary>Clients skip observer gunshot playback beyond this planar distance from local pawn (world units).</summary>
	public const float ObserverGunshotMaxHearingRadius = 4200f;

	/// <summary>Clients skip NPC gunshot playback beyond this planar distance (world units).</summary>
	public const float ObserverNpcGunshotMaxHearingRadius = 3800f;

	// --- Progressive streaming (enforced via <see cref="ThornsPerformanceQualityPresets"/> + queues) ---
	public const int StreamingMaxTerrainChunksGeneratedPerFrameDefault = 1;
	public const int StreamingMaxFoliageChunksPerFrameDefault = 1;
	public const int StreamingMaxFoliageInstancesPerFrameDefault = 16;
	public const int StreamingMaxDeferredHostSpawnsPerFrameDefault = 24;
	public const int StreamingMaxGrassTilesPerFrameDefault = 1;
	public const int StreamingMaxLootSpawnsPerFrameDefault = 10;
	public const int StreamingMaxBuildingInteriorsPerFrameDefault = 1;
}
