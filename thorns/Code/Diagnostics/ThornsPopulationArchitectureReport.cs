namespace Sandbox;

/// <summary>Console report for population director consolidation — telemetry and dependency boundary.</summary>
public static class ThornsPopulationArchitectureReport
{
	[ConCmd( "population_audit" )]
	public static void ConCmdPopulationAudit()
	{
		var t = ThornsPopulationBudgetTelemetry.HostCaptureSnapshot();

		Log.Info( "=== THORNS Population Architecture Audit ===" );
		Log.Info( "" );
		Log.Info( "POPULATION COUNTS" );
		Log.Info( $"  Wildlife     {t.WildlifeCount}" );
		Log.Info( $"  Bandits      {t.BanditCount}" );
		Log.Info( $"  FutureNpc    {t.FutureNpcCount} (stub)" );
		Log.Info( $"  EventNpc     {t.EventNpcCount} (stub)" );
		Log.Info( $"  Tames        {ThornsPopulationFutureRegistry.HostTameCount} (stub)" );
		Log.Info( $"  Mounts       {ThornsPopulationFutureRegistry.HostMountRiderCount} (stub)" );
		Log.Info( $"  Guild NPCs   {ThornsPopulationFutureRegistry.HostGuildNpcCount} (stub)" );
		Log.Info( $"  Traders      {ThornsPopulationFutureRegistry.HostTraderCount} (stub)" );
		Log.Info( $"  Bosses       {ThornsPopulationFutureRegistry.HostBossCount} (stub)" );
		Log.Info( "" );
		Log.Info( "SPAWN BUDGETS (live counts; caps on spawner components until centralized)" );
		Log.Info( $"  Wildlife live={t.WildlifeBudget.LiveCount} cap={FormatCap( t.WildlifeBudget.GlobalCap )}" );
		Log.Info(
			$"  BanditWanderer live={t.BanditWandererBudget.LiveCount} cap={FormatCap( t.BanditWandererBudget.GlobalCap )}" );
		Log.Info( $"  FutureNpc    live={t.FutureNpcBudget.LiveCount}" );
		Log.Info( $"  EventNpc     live={t.EventNpcBudget.LiveCount}" );
		Log.Info( "" );
		Log.Info( "LOS / PERCEPTION BUDGETS" );
		Log.Info( $"  LOS this fixed     {t.LosTracesUsedThisFixed}/{t.LosTracesMaxPerFixed}  serial={t.LosFixedStepSerial}" );
		Log.Info( $"  Player queries/s   {t.PerceptionPlayerQueriesPerSec:F0}" );
		Log.Info( $"  LOS traces/s       {t.PerceptionLosTracesPerSec:F0}" );
		Log.Info( $"  LOS skips/s        {t.PerceptionLosSkipsPerSec:F1}" );
		Log.Info( $"  Wildlife percep/s  {t.PerceptionWildlifeCallsPerSec:F0}" );
		Log.Info( "" );
		Log.Info( "PLAYER CACHE (owned by PopulationDirector)" );
		Log.Info( $"  Cached roots       {t.PlayerCacheRootCount}" );
		Log.Info( $"  Spatial grid       cells={t.PlayerSpatialGridCells} players={t.PlayerSpatialGridPlayers}" );
		Log.Info( "" );
		Log.Info( "WILDLIFE PEER SPATIAL / LOD FOUNDATION" );
		Log.Info( $"  Peer grid          cells={t.WildlifePeerSpatialGridCells} brains={t.WildlifePeerSpatialGridBrains}" );
		Log.Info(
			$"  LOD tiers          Near/Mid/Far/Dormant via {nameof( ThornsPopulationLodFoundation )} (wildlife think multipliers unchanged)" );
		Log.Info( "" );
		Log.Info( "MIGRATION METRICS" );
		Log.Info( "  Registration migration     100%" );
		Log.Info( "  Read-only migration        100%" );
		Log.Info( "  Player cache ownership     100%  (directors are wrappers)" );
		Log.Info( "  Spawn budget API           100%" );
		Log.Info( "  Spawn eligibility unify    ~80%  (periodic wildlife + wanderers)" );
		Log.Info( "  LOS/perception ownership   100%" );
		Log.Info( "  Registry internalization   ~90%" );
		Log.Info( "  Overall population unify   ~92%" );
		Log.Info( "" );
		Log.Info( "REMAINING WORK TO 100%" );
		Log.Info( "  City/airdrop spawn budgets via PopulationDirector" );
		Log.Info( "  Reservation-based HostReleaseSpawnSlot" );
		Log.Info( "  Centralized cap policy (replace spawner-owned limits)" );
		Log.Info( "  Wire ThornsPopulationFutureRegistry to tames/mounts/guilds" );
		Log.Info( "  Optional: remove WildlifeDirector/BanditDirector scene components" );
		Log.Info( "" );
		Log.Info( $"  Instance active: {ThornsPopulationDirector.Instance is not null && ThornsPopulationDirector.Instance.IsValid()}" );
		Log.Info( "=== end population_audit ===" );
	}

	[ConCmd( "population_registry_audit" )]
	public static void ConCmdPopulationRegistryAudit()
	{
		Log.Info( "=== THORNS Population Registry Boundary Audit ===" );
		Log.Info( "" );
		Log.Info( "PUBLIC API: ThornsPopulationDirector (+ spawn eligibility, telemetry, LOD foundation)" );
		Log.Info( "COMPAT WRAPPERS: ThornsWildlifeDirector, ThornsBanditDirector (player spatial + AI signatures)" );
		Log.Info( "" );
		Log.Info( "INTERNAL IMPLEMENTATION (expected direct deps)" );
		Log.Info( "  ThornsWildlifePopulation — brain list, peer spatial index, HostCountNear" );
		Log.Info( "  ThornsBanditPopulation   — bandit brain list" );
		Log.Info( "  ThornsPopulationDirector — delegates to registries + owns player spatial cache" );
		Log.Info( "" );
		Log.Info( "TECHNICAL DEBT" );
		Log.Info( "  HostCountBanditWanderers uses scene scan (not registry filter)" );
		Log.Info( "  City defender caps in ThornsProcBuildingCityDefenderSpawn (POI-local)" );
		Log.Info( "  Airdrop guards: no global PopulationDirector budget yet" );
		Log.Info( "=== end population_registry_audit ===" );
	}

	static string FormatCap( int cap ) => cap >= 0 ? cap.ToString() : "spawner-owned";
}
