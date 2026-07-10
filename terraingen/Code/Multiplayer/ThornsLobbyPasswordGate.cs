namespace Terraingen.Multiplayer;

using System.Security.Cryptography;
using System.Text;
using Sandbox.Network;

/// <summary>Password-gates listed s&box lobbies with metadata, matching the full Thorns project pattern.</summary>
public static class ThornsLobbyPasswordGate
{
	public const string DataKeyGate = "thorns_gate";
	public const string DataKeyHash = "thorns_pwh";

	const string HashVersionPrefix = "v1:";
	static readonly UTF8Encoding Utf8 = new( false );

	public static bool LobbyRequiresPassword( LobbyInformation lobby )
	{
		try
		{
			return string.Equals( lobby.Get( DataKeyGate, "" ), "pwd", StringComparison.Ordinal );
		}
		catch
		{
			return false;
		}
	}

	public static string ComputePasswordHashForLobby( string password )
	{
		var normalized = (password ?? "").Trim();
		var bytes = Utf8.GetBytes( "ThornsLobbyPw.v1\x1e" + normalized );
		Span<byte> hash = stackalloc byte[32];
		SHA256.HashData( bytes, hash );
		return HashVersionPrefix + Convert.ToBase64String( hash );
	}

	public static bool VerifyPasswordAgainstLobby( string passwordAttempt, LobbyInformation lobby )
	{
		string expected;
		try
		{
			expected = lobby.Get( DataKeyHash, "" );
		}
		catch
		{
			return false;
		}
		if ( string.IsNullOrEmpty( expected ) )
			return false;

		return string.Equals( ComputePasswordHashForLobby( passwordAttempt ), expected, StringComparison.Ordinal );
	}
}
