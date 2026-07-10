using System.Globalization;
using Sandbox.Network;

namespace Sandbox;

/// <summary>Steam lobby metadata for the active procedural world seed (<see cref="ThornsTerrainNetSpec.Seed"/>).</summary>
public static class ThornsLobbyWorldSeed
{
	public const string DataKey = "thorns_seed";

	public static void PublishIfHost( int seed )
	{
		if ( !Networking.IsActive || !Networking.IsHost )
			return;

		try
		{
			Networking.SetData( DataKey, seed.ToString( CultureInfo.InvariantCulture ) );
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns] Could not publish world seed on lobby." );
		}
	}

	public static bool TryGetSeed( LobbyInformation lobby, out int seed )
	{
		seed = 0;
		var raw = lobby.Get( DataKey, "" );
		if ( string.IsNullOrWhiteSpace( raw ) )
			return false;

		return int.TryParse( raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out seed );
	}

	public static string FormatLobbyDetailLine( LobbyInformation lobby )
	{
		if ( TryGetSeed( lobby, out var seed ) )
			return $"World seed: {seed}";

		return "World seed: — (host still loading or older build)";
	}
}
