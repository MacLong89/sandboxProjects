namespace Terraingen.Multiplayer;

using Terraingen.TerrainGen;

/// <summary>
/// Lobby-synced world identity — single source of truth for deterministic subsystems.
/// Host publishes seed/version only after terrain height data exists; clients must not apply terrain until ready.
/// </summary>
public static class ThornsWorldSession
{
	public const string DataKeySeed = "thorns_world_seed";
	public const string DataKeyVersion = "thorns_world_version";
	public const string DataKeyReady = "thorns_world_ready";

	public static int WorldSeed { get; private set; } = 42069;
	public static int WorldBuildVersion { get; private set; } = 1;
	public static bool WorldReady { get; private set; }

	public static void ApplyConfig( ThornsTerrainConfig config )
	{
		if ( config is null )
			return;

		config.WorldSeed = WorldSeed;
	}

	/// <summary>Call when creating a lobby — blocks joiners until terrain is published.</summary>
	public static void MarkLobbyPending()
	{
		WorldReady = false;

		if ( !Networking.IsActive || !Networking.IsHost )
			return;

		Networking.SetData( DataKeyReady, "0" );
	}

	/// <summary>Host-only: publish after height cache exists and terrain is applied.</summary>
	public static void PublishFromHost( ThornsTerrainConfig config )
	{
		if ( config is null )
			return;

		WorldSeed = config.WorldSeed;
		WorldBuildVersion = config.WorldBuildVersion;
		WorldReady = true;

		if ( !Networking.IsActive || !Networking.IsHost )
			return;

		Networking.SetData( DataKeySeed, WorldSeed.ToString() );
		Networking.SetData( DataKeyVersion, WorldBuildVersion.ToString() );
		Networking.SetData( DataKeyReady, "1" );
		Log.Info( $"[Thorns World] Published lobby world seed={WorldSeed} version={WorldBuildVersion}." );
	}

	public static bool TryReadFromLobby()
	{
		if ( !Networking.IsActive )
			return false;

		var seedText = Networking.GetData( DataKeySeed );
		var versionText = Networking.GetData( DataKeyVersion );
		var readyText = Networking.GetData( DataKeyReady );

		if ( string.IsNullOrWhiteSpace( seedText ) || readyText != "1" )
			return false;

		if ( !int.TryParse( seedText, out var seed ) )
			return false;

		WorldSeed = seed;
		WorldBuildVersion = int.TryParse( versionText, out var version ) ? version : 1;
		WorldReady = true;
		return true;
	}

	/// <summary>True when lobby advertises a ready world and local terrain matches that seed.</summary>
	public static bool IsAuthoritativeForJoin( ThornsTerrainConfig config )
	{
		if ( !WorldReady || !TryReadFromLobby() )
			return false;

		if ( config is null )
			return true;

		return config.WorldSeed == WorldSeed && config.WorldBuildVersion == WorldBuildVersion;
	}

	public static void Reset()
	{
		WorldReady = false;
	}
}
