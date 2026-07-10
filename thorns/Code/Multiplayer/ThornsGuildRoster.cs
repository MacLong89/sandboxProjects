using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sandbox;

/// <summary>
/// Host-authoritative guild roster (stable account keys). Members on your roster are not damaged by you (see <see cref="ThornsGuildCombat"/>).
/// </summary>
[Title( "Thorns — Guild Roster" )]
[Category( "Thorns" )]
[Icon( "groups" )]
[Order( 72 )]
public sealed class ThornsGuildRoster : Component
{
	public const int MaxGuildMembers = 24;
	const char KeySeparator = '\n';

	[Sync( SyncFlags.FromHost )] public string GuildMemberKeysSync { get; private set; } = "";

	List<string> _parsedKeys = new();
	HashSet<string> _parsedKeySet = new( StringComparer.Ordinal );
	string _clientMirrorPacked = "";

	public IReadOnlyList<string> MemberAccountKeys => _parsedKeys;

	public int MemberCount => _parsedKeys.Count;

	protected override void OnStart()
	{
		ReparseKeys();
	}

	protected override void OnUpdate()
	{
		if ( GuildMemberKeysSync != _clientMirrorPacked )
		{
			_clientMirrorPacked = GuildMemberKeysSync ?? "";
			ReparseKeys();
		}
	}

	void ReparseKeys()
	{
		_parsedKeys = ParseKeys( GuildMemberKeysSync );
		_parsedKeySet = new HashSet<string>( _parsedKeys, StringComparer.Ordinal );
	}

	public static List<string> ParseKeys( string packed )
	{
		var list = new List<string>();
		if ( string.IsNullOrWhiteSpace( packed ) )
			return list;

		foreach ( var part in packed.Split( KeySeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) )
		{
			var k = NormalizeAccountKey( part );
			if ( string.IsNullOrEmpty( k ) )
				continue;
			if ( list.Contains( k, StringComparer.Ordinal ) )
				continue;
			list.Add( k );
		}

		return list;
	}

	public static string PackKeys( IEnumerable<string> keys )
	{
		if ( keys is null )
			return "";

		var list = new List<string>();
		foreach ( var raw in keys )
		{
			var k = NormalizeAccountKey( raw );
			if ( string.IsNullOrEmpty( k ) )
				continue;
			if ( list.Contains( k, StringComparer.Ordinal ) )
				continue;
			list.Add( k );
		}

		return string.Join( KeySeparator, list );
	}

	public static string NormalizeAccountKey( string raw )
	{
		if ( string.IsNullOrWhiteSpace( raw ) )
			return "";

		var t = raw.Trim();
		if ( t.StartsWith( "steam:", StringComparison.OrdinalIgnoreCase ) )
			return $"steam:{t["steam:".Length..].Trim()}";

		if ( t.StartsWith( "conn:", StringComparison.OrdinalIgnoreCase ) )
			return $"conn:{t["conn:".Length..].Trim()}";

		if ( ulong.TryParse( t, out var steam ) && steam != 0 )
			return $"steam:{steam}";

		return t;
	}

	public static string AccountKeyFromSteamId( ulong steamId ) =>
		steamId == 0 ? "" : $"steam:{steamId}";

	public static bool TryGetAccountKeyForPawnRoot( GameObject pawnRoot, out string accountKey )
	{
		accountKey = "";
		if ( pawnRoot is null || !pawnRoot.IsValid() )
			return false;

		var session = pawnRoot.Components.GetInDescendantsOrSelf<ThornsPlayer>( true );
		if ( session.IsValid() && !string.IsNullOrWhiteSpace( session.HostPersistenceAccountKey ) )
		{
			accountKey = NormalizeAccountKey( session.HostPersistenceAccountKey );
			return !string.IsNullOrEmpty( accountKey );
		}

		var conn = Connection.Find( pawnRoot.Network.OwnerId );
		if ( conn is null )
			return false;

		accountKey = NormalizeAccountKey( ThornsPersistenceIdentity.GetStableAccountKey( conn ) );
		return !string.IsNullOrEmpty( accountKey );
	}

	public bool ContainsAccountKey( string accountKey )
	{
		var k = NormalizeAccountKey( accountKey );
		if ( string.IsNullOrEmpty( k ) )
			return false;

		return _parsedKeySet.Contains( k );
	}

	public void HostApplyPersistedPackedKeys( string packed )
	{
		if ( !Networking.IsHost )
			return;

		GuildMemberKeysSync = PackKeys( ParseKeys( packed ) );
		ReparseKeys();
	}

	public string HostGetPackedForPersistence() => PackKeys( _parsedKeys );

	[Rpc.Host]
	public void RequestAddGuildMember( string accountKey )
	{
		if ( !Networking.IsHost )
			return;

		if ( !ThornsPawn.ValidateHostRpcCallerOwnsPawnRoot( GameObject ) )
			return;

		var key = NormalizeAccountKey( accountKey );
		if ( string.IsNullOrEmpty( key ) )
		{
			RpcGuildFeedback( false, "invalid_member" );
			return;
		}

		if ( TryGetAccountKeyForPawnRoot( GameObject, out var selfKey )
		     && string.Equals( key, selfKey, StringComparison.Ordinal ) )
		{
			RpcGuildFeedback( false, "cannot_add_self" );
			return;
		}

		if ( _parsedKeys.Count >= MaxGuildMembers )
		{
			RpcGuildFeedback( false, "roster_full" );
			return;
		}

		if ( ContainsAccountKey( key ) )
		{
			RpcGuildFeedback( false, "already_member" );
			return;
		}

		_parsedKeys.Add( key );
		GuildMemberKeysSync = PackKeys( _parsedKeys );
		RpcGuildFeedback( true, "" );
		Log.Info( $"[Thorns Guild] Added member key='{key}' owner={GameObject.Name}" );
	}

	[Rpc.Host]
	public void RequestRemoveGuildMember( string accountKey )
	{
		if ( !Networking.IsHost )
			return;

		if ( !ThornsPawn.ValidateHostRpcCallerOwnsPawnRoot( GameObject ) )
			return;

		var key = NormalizeAccountKey( accountKey );
		if ( string.IsNullOrEmpty( key )
		     || _parsedKeys.RemoveAll( k => string.Equals( k, key, StringComparison.Ordinal ) ) == 0 )
		{
			RpcGuildFeedback( false, "not_member" );
			return;
		}

		GuildMemberKeysSync = PackKeys( _parsedKeys );
		RpcGuildFeedback( true, "" );
		Log.Info( $"[Thorns Guild] Removed member key='{key}' owner={GameObject.Name}" );
	}

	[Rpc.Owner]
	void RpcGuildFeedback( bool ok, string reason )
	{
		if ( ok )
			return;

		var shell = Components.Get<ThornsGameShell>();
		if ( !shell.IsValid() )
			return;

		var msg = reason switch
		{
			"roster_full" => $"Guild is full ({MaxGuildMembers} members).",
			"already_member" => "Already in your guild.",
			"cannot_add_self" => "You can't add yourself.",
			"not_member" => "Not on your guild roster.",
			"invalid_member" => "Couldn't add that player.",
			_ => "Guild roster change failed."
		};
		shell.PushGameplayToast( msg, 2.4f, ThornsGameplayToastKind.Hint );
	}
}
