namespace Terraingen.Core;

using System.Text;
using Terraingen;
using Terraingen.AI;
using Terraingen.Clutter;
using Terraingen.Foliage;
using Terraingen.Minerals;
using Terraingen.TerrainGen;
using Terraingen.Animals;
using Terraingen.World.Environment;

/// <summary>Console snapshots for performance regression hunting.</summary>
public static class ThornsPerformanceDiagnostics
{
	[ConCmd( "perf_report" )]
	public static void PerfReport()
	{
		var sb = new StringBuilder( 2048 );
		sb.AppendLine( "=== Thorns perf_report ===" );
		AppendNetwork( sb );
		AppendEntities( sb );
		AppendLoading( sb );
		AppendRendering( sb );
		AppendMemoryHints( sb );
		AppendOptimizationStatus( sb );
		Log.Info( sb.ToString() );
	}

	[ConCmd( "perf_network" )]
	public static void PerfNetwork()
	{
		var sb = new StringBuilder( 512 );
		sb.AppendLine( "=== perf_network ===" );
		AppendNetwork( sb );
		Log.Info( sb.ToString() );
	}

	[ConCmd( "perf_entities" )]
	public static void PerfEntities()
	{
		var sb = new StringBuilder( 768 );
		sb.AppendLine( "=== perf_entities ===" );
		AppendEntities( sb );
		Log.Info( sb.ToString() );
	}

	[ConCmd( "perf_memory" )]
	public static void PerfMemory()
	{
		var sb = new StringBuilder( 512 );
		sb.AppendLine( "=== perf_memory ===" );
		AppendMemoryHints( sb );
		Log.Info( sb.ToString() );
	}

	[ConCmd( "perf_loading" )]
	public static void PerfLoading()
	{
		var sb = new StringBuilder( 768 );
		sb.AppendLine( "=== perf_loading ===" );
		AppendLoading( sb );
		Log.Info( sb.ToString() );
	}

	[ConCmd( "perf_rendering" )]
	public static void PerfRendering()
	{
		var sb = new StringBuilder( 768 );
		sb.AppendLine( "=== perf_rendering ===" );
		AppendRendering( sb );
		Log.Info( sb.ToString() );
	}

	[ConCmd( "perf_authority" )]
	public static void PerfAuthority()
	{
		var sb = new StringBuilder( 768 );
		sb.AppendLine( "=== perf_authority ===" );
		AppendAuthority( sb );
		Log.Info( sb.ToString() );
	}

	static void AppendNetwork( StringBuilder sb )
	{
		var connectionCount = 0;
		foreach ( var _ in Connection.All )
			connectionCount++;
		sb.AppendLine( $"networked={ThornsMultiplayer.IsNetworked} host={Networking.IsHost} connections={connectionCount}" );
		sb.AppendLine( "environment: TimeOfDayHours synced from host; terrain/foliage local per peer unless shared cache" );

		var bootstrap = FindBootstrap();
		if ( bootstrap?.Config is not null )
		{
			var c = bootstrap.Config;
			sb.AppendLine( $"terrain HostAuthoritative={c.HostAuthoritative} ClientsGenerateDeterministic={c.ClientsGenerateDeterministic}" );
		}
	}

	static void AppendEntities( StringBuilder sb )
	{
		var scene = Game.ActiveScene;
		if ( !scene.IsValid() )
		{
			sb.AppendLine( "no active scene" );
			return;
		}

		sb.AppendLine( $"host_sim={ThornsMultiplayer.IsHostOrOffline} client={(ThornsMultiplayer.IsNetworked && !Networking.IsHost)}" );

		var foliage = scene.GetAllComponents<ThornsFoliageFoundation>().FirstOrDefault();
		if ( foliage is { IsValid: true } )
			sb.AppendLine( $"foliage: {foliage.GetHudSummary()}" );

		var grass = scene.GetAllComponents<ClientGrassRenderer>().FirstOrDefault();
		if ( grass is { IsValid: true } )
			sb.AppendLine( $"grass: {grass.GetDebugSummary()}" );

		var minerals = scene.GetAllComponents<ThornsMineralFoundation>().FirstOrDefault();
		if ( minerals is { IsValid: true } )
			sb.AppendLine( $"minerals: {minerals.GetDebugSummary()}" );

		var players = 0;
		foreach ( var _ in scene.GetAllComponents<PlayerController>() )
			players++;

		sb.AppendLine( $"player_controllers={players}" );

		var live = ThornsAnimalManager.CountLiveAnimals();
		var tamed = ThornsAnimalManager.CountLiveTamed();
		sb.AppendLine( $"animals_live={live} tamed={tamed} registry={ThornsAnimalManager.AnimalRegistry.Count}" );

		var sleeping = 0;
		var reduced = 0;
		var full = 0;
		foreach ( var brain in ThornsAnimalManager.AnimalRegistry )
		{
			if ( !brain.IsValid || brain.IsDead )
				continue;

			switch ( brain.LodTier )
			{
				case ThornsNpcLodTier.Sleeping: sleeping++; break;
				case ThornsNpcLodTier.Reduced: reduced++; break;
				default: full++; break;
			}
		}

		sb.AppendLine( $"animal_lod full={full} reduced={reduced} sleeping={sleeping}" );

		var banditSleeping = 0;
		var banditReduced = 0;
		var banditFull = 0;
		var banditLive = 0;
		foreach ( var bandit in ThornsBanditPopulation.HostBrainsReadOnly )
		{
			if ( !bandit.IsValid() || bandit.IsDead )
				continue;

			banditLive++;
			switch ( bandit.LodTier )
			{
				case ThornsNpcLodTier.Sleeping: banditSleeping++; break;
				case ThornsNpcLodTier.Reduced: banditReduced++; break;
				default: banditFull++; break;
			}
		}

		sb.AppendLine( $"bandits_live={banditLive} bandit_lod full={banditFull} reduced={banditReduced} sleeping={banditSleeping}" );
	}

	static void AppendLoading( StringBuilder sb )
	{
		var bootstrap = FindBootstrap();
		if ( bootstrap?.Config is null )
		{
			sb.AppendLine( "no ThornsTerrainBootstrap" );
			return;
		}

		var c = bootstrap.Config;
		sb.AppendLine( $"terrain res={c.TerrainResolution} seed={c.WorldSeed} terrain_cache={(ThornsTerrainCache.Current is not null)}" );
	}

	static void AppendRendering( StringBuilder sb )
	{
		sb.AppendLine( $"grass per-tile instanced draw radius={ClientGrassRenderer.GrassRadiusMeters}m" );
		sb.AppendLine( "trees: instanced path when enabled on foliage foundation" );
		sb.AppendLine( "environment: cached sun/sky/fog refs + dirty sky pushes" );

		var time = Game.ActiveScene?.GetAllComponents<ThornsTimeOfDaySystem>().FirstOrDefault();
		if ( time is { IsValid: true } )
			sb.AppendLine( $"time={time.ResolvedHours:F2} sunI={time.CurrentState.SunIntensity:F2}" );
	}

	static void AppendMemoryHints( StringBuilder sb )
	{
		sb.AppendLine( "mitigated: sky uniform cache | perception target refresh | debug HUD off by default" );
		sb.AppendLine( "convars: thorns_debug_hud | thorns_perf_trace" );
	}

	static void AppendAuthority( StringBuilder sb )
	{
		sb.AppendLine( $"host_or_offline={ThornsMultiplayer.IsHostOrOffline} is_host={Networking.IsHost} active={Networking.IsActive}" );
		sb.AppendLine( "cosmetic: foliage/grass/minerals populate on all peers (deterministic heightfield)" );
		sb.AppendLine( "minerals: player pocket scatter host-only" );
		sb.AppendLine( "persistence: ThornsWorldPersistence uses IsHostOrOffline (offline + host save)" );
	}

	static void AppendOptimizationStatus( StringBuilder sb )
	{
		sb.AppendLine( "--- implemented ---" );
		sb.AppendLine( "celestial cache + sky dirty push | terrain cache | grass per-tile draws" );
		sb.AppendLine( "height cache + RPC stream | instanced trees | async terrain | observer cache" );
		sb.AppendLine( "hud: throttled interaction prompt + minimap blip | hotbar index-only RPC" );
		sb.AppendLine( "ai: animal/bandit sleeping LOD | player root cache | spatial separation filter" );
		sb.AppendLine( "bandit: director staggered tick | spatial grid | perception distance cull" );
		sb.AppendLine( "net: interest-filtered world RPCs | async persistence | deferred join cosmetics" );
		sb.AppendLine( "npc: central tick scheduler | visual LOD hide | simulation partials" );
	}

	static ThornsTerrainBootstrap FindBootstrap()
	{
		var scene = Game.ActiveScene;
		if ( !scene.IsValid() )
			return null;
		return scene.GetAllComponents<ThornsTerrainBootstrap>().FirstOrDefault();
	}
}
