using System.Security.Cryptography;
using System.Text;
using Sandbox.Network;

namespace Sandbox;

/// <summary>
/// Steam lobby metadata has no first-class password field on <see cref="LobbyConfig"/>.
/// We expose "Private" in the Thorns UI as a password-gated <b>listed</b> lobby: <c>thorns_gate=pwd</c> plus a salted hash in <c>thorns_pwh</c>.
/// Joiners must match the hash locally before calling <see cref="Networking.Connect"/>.
/// </summary>
public static class ThornsLobbyPasswordGate
{
	public const string DataKeyGate = "thorns_gate";
	public const string DataKeyHash = "thorns_pwh";

	const string HashVersionPrefix = "v1:";

	static readonly UTF8Encoding Utf8 = new( false );

	/// <summary>Listed lobby that requires the join password flow (not Steam invite-only).</summary>
	public static bool LobbyRequiresPassword( LobbyInformation lobby ) =>
		string.Equals( lobby.Get( DataKeyGate, "" ), "pwd", StringComparison.Ordinal );

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
		var expected = lobby.Get( DataKeyHash, "" );
		if ( string.IsNullOrEmpty( expected ) )
			return false;
		return string.Equals(
			ComputePasswordHashForLobby( passwordAttempt ),
			expected,
			StringComparison.Ordinal );
	}
}
