namespace Terraingen.Multiplayer;

using Terraingen.TerrainGen;

/// <summary>Lobby-synced world identity — single source of truth for deterministic subsystems.</summary>
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

	public static void Reset()
	{
		WorldReady = false;
	}
}
