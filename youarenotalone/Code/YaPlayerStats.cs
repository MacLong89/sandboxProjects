using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Sandbox;

[Title( "YouAreNotAlone — Player stats" )]
[Category( "YouAreNotAlone" )]
[Icon( "leaderboard" )]
public sealed class YaPlayerStats : Component, Component.INetworkSpawn
{
	const string LifetimeStatsPath = "youarenotalone_lifetime_stats.json";

	[Sync( SyncFlags.FromHost )] public int SessionKills { get; private set; }
	[Sync( SyncFlags.FromHost )] public int SessionDeaths { get; private set; }
	[Sync( SyncFlags.FromHost )] public int SessionWins { get; private set; }
	[Sync( SyncFlags.FromHost )] public int RoundKills { get; private set; }
	[Sync( SyncFlags.FromHost )] public int WinStreak { get; private set; }
	[Sync( SyncFlags.FromHost )] public int LifetimeKills { get; private set; }
	[Sync( SyncFlags.FromHost )] public int LifetimeDeaths { get; private set; }
	[Sync( SyncFlags.FromHost )] public int LifetimeWins { get; private set; }
	[Sync( SyncFlags.FromHost )] public string DisplayName { get; private set; } = "Player";

	public Connection OwnerConnection { get; private set; }

	static bool _loadedLifetimeStats;
	static Dictionary<string, LifetimeEntry> _lifetimeStats = new();

	sealed class LifetimeEntry
	{
		public int Kills { get; set; }
		public int Deaths { get; set; }
		public int Wins { get; set; }
	}

	public void OnNetworkSpawn( Connection owner )
	{
		OwnerConnection = owner;
		if ( !Networking.IsHost )
			return;

		SessionKills = 0;
		SessionDeaths = 0;
		SessionWins = 0;
		RoundKills = 0;
		WinStreak = 0;
		DisplayName = string.IsNullOrWhiteSpace( owner?.DisplayName ) ? "Player" : owner.DisplayName.Trim();

		EnsureLifetimeLoaded();
		var key = owner?.Id.ToString() ?? string.Empty;
		if ( !string.IsNullOrWhiteSpace( key ) && _lifetimeStats.TryGetValue( key, out var row ) )
		{
			LifetimeKills = Math.Max( 0, row.Kills );
			LifetimeDeaths = Math.Max( 0, row.Deaths );
			LifetimeWins = Math.Max( 0, row.Wins );
		}
		else
		{
			LifetimeKills = 0;
			LifetimeDeaths = 0;
			LifetimeWins = 0;
		}
	}

	public static void HostRecordDeath( GameObject victimRoot )
	{
		if ( !Networking.IsHost || victimRoot is null || !victimRoot.IsValid() )
			return;

		var stats = victimRoot.Components.Get<YaPlayerStats>( FindMode.EnabledInSelf );
		if ( stats.IsValid() )
			stats.HostAddDeath();
	}

	public static void HostRecordKill( GameObject attackerRoot )
	{
		if ( !Networking.IsHost || attackerRoot is null || !attackerRoot.IsValid() )
			return;

		var stats = attackerRoot.Components.Get<YaPlayerStats>( FindMode.EnabledInSelf );
		if ( stats.IsValid() )
			stats.HostAddKill();
	}

	public static void HostRecordRoundWin( GameObject playerRoot )
	{
		if ( !Networking.IsHost || playerRoot is null || !playerRoot.IsValid() )
			return;

		var stats = playerRoot.Components.Get<YaPlayerStats>( FindMode.EnabledInSelf );
		if ( stats.IsValid() )
			stats.HostAddWin();
	}

	public static void HostResetRoundKills( GameObject playerRoot )
	{
		if ( !Networking.IsHost || playerRoot is null || !playerRoot.IsValid() )
			return;

		var stats = playerRoot.Components.Get<YaPlayerStats>( FindMode.EnabledInSelf );
		if ( stats.IsValid() )
			stats.HostClearRoundKills();
	}

	public static void HostResetWinStreak( GameObject playerRoot )
	{
		if ( !Networking.IsHost || playerRoot is null || !playerRoot.IsValid() )
			return;

		var stats = playerRoot.Components.Get<YaPlayerStats>( FindMode.EnabledInSelf );
		if ( stats.IsValid() )
			stats.HostResetWinStreakLocal();
	}

	void HostAddKill()
	{
		if ( !Networking.IsHost )
			return;

		SessionKills++;
		RoundKills++;
		LifetimeKills++;
		PersistLifetime();
		RpcNotifyChallengeProgressLocal( SessionKills );
	}

	void HostAddDeath()
	{
		if ( !Networking.IsHost )
			return;

		SessionDeaths++;
		LifetimeDeaths++;
		PersistLifetime();
	}

	void HostAddWin()
	{
		if ( !Networking.IsHost )
			return;

		SessionWins++;
		WinStreak++;
		LifetimeWins++;
		PersistLifetime();
	}

	void HostClearRoundKills()
	{
		if ( !Networking.IsHost )
			return;

		RoundKills = 0;
	}

	void HostResetWinStreakLocal()
	{
		if ( !Networking.IsHost )
			return;

		WinStreak = 0;
	}

	[Rpc.Owner]
	void RpcNotifyChallengeProgressLocal( int sessionKills )
	{
		if ( sessionKills == 3 )
		{
			var hud = GameObject.Components.GetInDescendantsOrSelf<YaPlayerHud>( true );
			hud?.NotifyFloatingMessageLocal( "Challenge: 3 kills" );
		}
		else if ( sessionKills == 5 )
		{
			var hud = GameObject.Components.GetInDescendantsOrSelf<YaPlayerHud>( true );
			hud?.NotifyFloatingMessageLocal( "Challenge: 5 kills" );
		}
	}

	void PersistLifetime()
	{
		var key = OwnerConnection?.Id.ToString();
		if ( string.IsNullOrWhiteSpace( key ) )
			return;

		EnsureLifetimeLoaded();
		_lifetimeStats[key] = new LifetimeEntry
		{
			Kills = LifetimeKills,
			Deaths = LifetimeDeaths,
			Wins = LifetimeWins
		};

		try
		{
			using var stream = FileSystem.Data.OpenWrite( LifetimeStatsPath );
			JsonSerializer.Serialize( stream, _lifetimeStats );
		}
		catch ( Exception ex )
		{
			Log.Warning( $"[YA] Failed writing lifetime stats: {ex.Message}" );
		}
	}

	static void EnsureLifetimeLoaded()
	{
		if ( _loadedLifetimeStats )
			return;

		_loadedLifetimeStats = true;
		try
		{
			if ( !FileSystem.Data.FileExists( LifetimeStatsPath ) )
				return;

			using var stream = FileSystem.Data.OpenRead( LifetimeStatsPath );
			var parsed = JsonSerializer.Deserialize<Dictionary<string, LifetimeEntry>>( stream );
			if ( parsed is { Count: > 0 } )
				_lifetimeStats = parsed;
		}
		catch ( Exception ex )
		{
			Log.Warning( $"[YA] Failed reading lifetime stats: {ex.Message}" );
		}
	}
}
