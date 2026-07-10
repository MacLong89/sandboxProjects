namespace Terraingen.UI.Menu;

using System.Threading;
using System.Threading.Tasks;
using Sandbox.Network;
using Terraingen;
using Terraingen.Multiplayer;
using Terraingen.Player;
using Terraingen.UI.Core;

/// <summary>Lobby query, tab filtering, and join/password helpers (presentation-free).</summary>
public static class ThornsServerBrowserService
{
	const int JoinConnectionTimeoutMs = 20000;
	const int JoinWorldSeedTimeoutMs = 12000;
	const int JoinLocalPawnTimeoutMs = 25000;

	public static async Task<IReadOnlyList<LobbyInformation>> QueryLobbiesAsync( CancellationToken token = default )
	{
		try
		{
			return string.IsNullOrEmpty( Game.Ident )
				? await Networking.QueryLobbies( token )
				: await Networking.QueryLobbies( Game.Ident, token );
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns Menu] Lobby query failed." );
			return Array.Empty<LobbyInformation>();
		}
	}

	public static IReadOnlyList<LobbyInformation> Filter(
		IReadOnlyList<LobbyInformation> source,
		ThornsServerBrowserTab tab,
		string search )
	{
		var list = source?.ToList() ?? new List<LobbyInformation>();

		list = tab switch
		{
			ThornsServerBrowserTab.Official => list.Where( l => ThornsLobbyMetadata.IsOfficial( l ) ).ToList(),
			ThornsServerBrowserTab.Community => list.Where( l => !ThornsLobbyMetadata.IsOfficial( l ) ).ToList(),
			ThornsServerBrowserTab.Favorites => list.Where( l => ThornsMenuServerPrefs.IsFavorite( l.LobbyId.ToString() ) ).ToList(),
			ThornsServerBrowserTab.Recent => FilterRecent( list ),
			_ => list
		};

		if ( !string.IsNullOrWhiteSpace( search ) )
		{
			var q = search.Trim();
			list = list.Where( l => (l.Name ?? "").Contains( q, StringComparison.OrdinalIgnoreCase ) ).ToList();
		}

		return list;
	}

	static List<LobbyInformation> FilterRecent( List<LobbyInformation> live )
	{
		var byId = live.ToDictionary( l => l.LobbyId.ToString(), l => l );
		var ordered = new List<LobbyInformation>();
		foreach ( var recent in ThornsMenuServerPrefs.Current.RecentServers )
		{
			if ( byId.TryGetValue( recent.LobbyId, out var lobby ) )
				ordered.Add( lobby );
		}

		return ordered;
	}

	public static async Task<(bool Success, string ErrorMessage)> TryJoinAsync( LobbyInformation lobby, string passwordAttempt )
	{
		if ( lobby.IsFull )
			return (false, "That server is full — pick another or host your own world.");

		if ( ThornsLobbyPasswordGate.LobbyRequiresPassword( lobby )
		     && !ThornsLobbyPasswordGate.VerifyPasswordAgainstLobby( passwordAttempt ?? "", lobby ) )
			return (false, "Wrong password — check the join password and try again.");

		ThornsSessionBootstrap.CancelHostFromLocalSaveRequest();
		ThornsSessionBootstrap.RequestJoinRemoteLobbyNextGameplayLoad();
		ThornsMenuJoinFlow.SetStage( ThornsMenuJoinStage.Connecting );
		ThornsMenuJoinFlow.SetProgressMessage( "Connecting..." );

		if ( Networking.IsActive )
		{
			Networking.Disconnect();
			await Task.Delay( 120 );
		}

		ThornsMenuServerPrefs.RecordRecent( lobby );

		try
		{
			ThornsMenuJoinFlow.SetProgressMessage( "Loading world..." );
			await ThornsMenuSceneLoader.LoadGameplayAsync();

			if ( !ThornsMenuSceneLoader.IsInGameplayScene() )
			{
				Log.Warning( "[Thorns Menu] Join failed — gameplay scene did not load." );
				AbortJoinAttempt();
				return (false, "Could not load the game world — try again or restart the game.");
			}

			ThornsMenuJoinFlow.SetProgressMessage( "Connecting..." );
			Networking.Connect( lobby.LobbyId );

			if ( !await WaitForClientConnectionAsync( JoinConnectionTimeoutMs ) )
			{
				Log.Warning( "[Thorns Menu] Join failed — could not connect to the host (timed out)." );
				AbortJoinAttempt();
				return (false, "Connection timed out — the host may be offline or your network blocked the join.");
			}

			if ( !await WaitForLobbyWorldSeedAsync( JoinWorldSeedTimeoutMs ) )
			{
				Log.Warning( "[Thorns Menu] Join failed — host world is not ready yet (missing world seed)." );
				AbortJoinAttempt();
				return (false, "The host world is still starting — wait a moment and try again.");
			}

			ThornsMenuJoinFlow.SetProgressMessage( "Spawning character..." );
			ThornsMenuJoinFlow.SetStage( ThornsMenuJoinStage.SyncCharacter );

			if ( !await WaitForLocalPawnReadyAsync( JoinLocalPawnTimeoutMs ) )
			{
				Log.Warning( "[Thorns Menu] Join failed — local player pawn did not spawn in time." );
				AbortJoinAttempt();
				return (false, "Your character did not spawn in time — the server may still be loading.");
			}

			ThornsMenuJoinFlow.SetProgressMessage( "Loading HUD..." );
			ThornsMenuJoinFlow.SetStage( ThornsMenuJoinStage.SyncInventory );
			if ( !await WaitForLocalPresentationReadyAsync( JoinLocalPawnTimeoutMs ) )
				Log.Warning( "[Thorns Menu] Join presentation timed out — continuing with partial readiness." );

			ThornsMenuJoinFlow.SetProgressMessage( "Entering world..." );
			ThornsMenuJoinFlow.SetStage( ThornsMenuJoinStage.EnteringWorld );
			return (true, "");
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns Menu] Join failed." );
			AbortJoinAttempt();
			return (false, "Join failed unexpectedly — check your connection and try again.");
		}
	}

	static void AbortJoinAttempt()
	{
		ThornsSessionBootstrap.CancelJoinRemoteLobbyRequest();
		if ( Networking.IsActive )
			Networking.Disconnect();
		ThornsNetworkSessionReset.ResetStaticState();
	}

	static async Task<bool> WaitForLocalPawnReadyAsync( int timeoutMs )
	{
		var deadline = DateTime.UtcNow.AddMilliseconds( timeoutMs );
		while ( DateTime.UtcNow < deadline )
		{
			if ( ThornsPlayerGameplay.Local.IsValid()
			     && ThornsLocalPlayer.IsLocallyControlledPawn( ThornsPlayerGameplay.Local.GameObject ) )
				return true;

			var scene = Game.ActiveScene;
			if ( scene is { IsValid: true } )
			{
				var player = ThornsSceneObserver.FindLocalPlayerObject( scene );
				if ( player.IsValid() && ThornsLocalPlayer.IsLocallyControlledPawn( player ) )
					return true;
			}

			await Task.Delay( 50 );
		}

		return ThornsPlayerGameplay.Local.IsValid();
	}

	static async Task<bool> WaitForLocalEnterWorldAsync( int timeoutMs )
	{
		var deadline = DateTime.UtcNow.AddMilliseconds( timeoutMs );
		while ( DateTime.UtcNow < deadline )
		{
			if ( !ThornsMenuJoinFlow.IsProgressVisible && !ThornsLocalHostSpawnCoordinator.IsDeferredPending )
				return true;

			await Task.Delay( 50 );
		}

		return !ThornsMenuJoinFlow.IsProgressVisible && !ThornsLocalHostSpawnCoordinator.IsDeferredPending;
	}

	static async Task<bool> WaitForLocalPresentationReadyAsync( int timeoutMs )
	{
		var deadline = DateTime.UtcNow.AddMilliseconds( timeoutMs );
		while ( DateTime.UtcNow < deadline )
		{
			if ( ThornsLocalPlayerPresentation.IsFullyReady() )
				return true;

			await Task.Delay( 50 );
		}

		return ThornsLocalPlayerPresentation.IsFullyReady();
	}

	static async Task<bool> WaitForClientConnectionAsync( int timeoutMs )
	{
		var deadline = DateTime.UtcNow.AddMilliseconds( timeoutMs );
		while ( DateTime.UtcNow < deadline )
		{
			if ( Networking.IsActive && !Networking.IsHost )
				return true;

			await Task.Delay( 50 );
		}

		return Networking.IsActive && !Networking.IsHost;
	}

	static async Task<bool> WaitForLobbyWorldSeedAsync( int timeoutMs )
	{
		var deadline = DateTime.UtcNow.AddMilliseconds( timeoutMs );
		while ( DateTime.UtcNow < deadline )
		{
			if ( ThornsWorldSession.TryReadFromLobby() )
				return true;

			await Task.Delay( 50 );
		}

		return ThornsWorldSession.TryReadFromLobby();
	}

	public static void ContinueLastWorld()
	{
		ThornsHostedServerCatalog.Load();
		var server = ThornsHostedServerCatalog.GetLastHosted()
		             ?? ThornsHostedServerCatalog.ListOrdered().FirstOrDefault();

		if ( server is null )
		{
			server = ThornsHostedServerCatalog.CreateNew( ThornsHostMenuPreferences.LoadLastHostedServerName() );
		}

		HostLocalServer( server );
	}

	public static async Task HostLocalServerAsync( ThornsHostedServerDto server )
	{
		if ( server is null || string.IsNullOrEmpty( server.Id ) )
			return;

		ThornsSessionBootstrap.CancelJoinRemoteLobbyRequest();
		ThornsHostedServerCatalog.RecordHosted( server );
		ThornsSessionBootstrap.RequestHostFromLocalSaveNextGameplayLoad(
			new ThornsHostLocalSaveLobbyOptions(
				server.RequireJoinPassword,
				server.JoinPassword,
				server.DisplayName,
				server.PersistenceRelativePath ) );

		ThornsMenuJoinFlow.SetStage( ThornsMenuJoinStage.Connecting );

		if ( Networking.IsActive )
		{
			Networking.Disconnect();
			await Task.Delay( 60 );
		}

		await ThornsMenuSceneLoader.LoadGameplayAsync();

		if ( !ThornsMenuSceneLoader.IsInGameplayScene() )
		{
			Log.Warning( "[Thorns Menu] Host failed — gameplay scene did not load." );
			ThornsMenuJoinFlow.ResetForMainMenu();
			return;
		}

		ThornsMenuJoinFlow.SetProgressMessage( "Spawning character..." );
		ThornsMenuJoinFlow.SetStage( ThornsMenuJoinStage.SyncCharacter );

		if ( !await WaitForLocalPawnReadyAsync( JoinLocalPawnTimeoutMs ) )
		{
			Log.Warning( "[Thorns Menu] Host failed — local player pawn did not spawn in time." );
			ThornsMenuJoinFlow.CompleteEnterWorld();
			return;
		}

		if ( !await WaitForLocalEnterWorldAsync( JoinLocalPawnTimeoutMs ) )
			Log.Warning( "[Thorns Menu] Host spawn presentation timed out — forcing gameplay handoff." );

		if ( !await WaitForLocalPresentationReadyAsync( JoinLocalPawnTimeoutMs ) )
			Log.Warning( "[Thorns Menu] Host presentation readiness timed out — continuing with partial readiness." );

		ThornsMenuJoinFlow.CompleteEnterWorld();
		ThornsGameplaySession.EnsureLocalPlayerControl();
	}

	public static void HostLocalServer( ThornsHostedServerDto server ) => _ = HostLocalServerAsync( server );
}
