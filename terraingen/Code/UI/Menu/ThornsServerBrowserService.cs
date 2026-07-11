namespace Terraingen.UI.Menu;

using System.Threading;
using System.Threading.Tasks;
using Sandbox.Network;
using Terraingen;
using Terraingen.Multiplayer;
using Terraingen.Player;
using Terraingen.TerrainGen;
using Terraingen.UI.Core;

/// <summary>Lobby query, tab filtering, and join/password helpers (presentation-free).</summary>
public static class ThornsServerBrowserService
{
	const int JoinConnectionTimeoutMs = 45000;
	const int JoinWorldSeedTimeoutMs = 45000;
	const int JoinLocalPawnTimeoutMs = 60000;

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
		ThornsJoinFlowDebug.LogMilestone( "TryJoinAsync start" );
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
			// Load the gameplay scene WHILE OFFLINE, then Connect.
			// Game.ChangeScene is rejected for non-host clients after Networking.Connect,
			// and disconnect alone dumps into s&box menu-main — so joiners must enter
			// thorns_terrain first. Host still owns lobby seed / networked pawns; the
			// join flag blocks local lobby create and premature default-seed terrain.
			ThornsMenuAudioHandoff.ArmForGameplayTransition();
			ThornsMainMenuAtmosphere.BeginMusicFadeOut( 1.5f );

			ThornsMenuJoinFlow.SetProgressMessage( "Loading world..." );
			ThornsMenuJoinFlow.SetStage( ThornsMenuJoinStage.LoadingWorld );
			ThornsJoinFlowDebug.LogMilestone( "LoadGameplayAsync" );
			await ThornsMenuSceneLoader.LoadGameplayAsync();

			if ( !ThornsMenuSceneLoader.IsInGameplayScene() )
			{
				Log.Warning( "[Thorns Menu] Join failed — gameplay scene did not load before connect." );
				await AbortJoinAttemptAsync();
				return (false, "Could not load the world scene — try again." );
			}

			ThornsMenuSceneLoader.DismissActiveMainMenuUi();
			ThornsMenuJoinFlow.SetProgressMessage( "Connecting..." );
			ThornsMenuJoinFlow.SetStage( ThornsMenuJoinStage.Connecting );
			ThornsSessionEnterController.BeginAwaitEnter();
			ThornsJoinFlowDebug.LogMilestone( $"Networking.Connect lobby={lobby.LobbyId}" );
			Networking.Connect( lobby.LobbyId );

			if ( !await WaitForClientConnectionAsync( JoinConnectionTimeoutMs ) )
			{
				ThornsJoinFlowDebug.JoinWarn( "Connect timed out" );
				Log.Warning( "[Thorns Menu] Join failed — could not connect to the host (timed out)." );
				await AbortJoinAttemptAsync();
				return (false, "Connection timed out — the host may be offline or your network blocked the join.");
			}

			ThornsMenuJoinFlow.SetProgressMessage( "Syncing host world..." );
			ThornsMenuJoinFlow.SetStage( ThornsMenuJoinStage.LoadingWorld );

			ThornsJoinFlowDebug.LogMilestone( "Connected — waiting for host world seed" );

			if ( !await WaitForLobbyWorldSeedAsync( JoinWorldSeedTimeoutMs ) )
			{
				Log.Warning( "[Thorns Menu] Join failed — host world is not ready yet (missing world seed)." );
				await AbortJoinAttemptAsync();
				return (false, "The host world is still starting — wait a moment and try again.");
			}

			ThornsMenuJoinFlow.SetProgressMessage( "Entering world..." );
			ThornsMenuJoinFlow.SetStage( ThornsMenuJoinStage.EnteringWorld );

			ThornsJoinFlowDebug.LogMilestone( "World seed ready — waiting for enter" );

			if ( !await ThornsSessionEnterController.WaitUntilReadyAsync( JoinLocalPawnTimeoutMs ) )
			{
				ThornsJoinFlowDebug.JoinWarn(
					$"TryJoinAsync enter wait failed — {ThornsJoinFlowDebug.DescribeEnterGates( compact: false )}" );

				if ( await TryRecoverJoinInWorldAsync() )
				{
					Log.Warning( "[Thorns Menu] Join enter wait timed out — recovered in loaded world." );
					return (true, "");
				}

				Log.Warning( "[Thorns Menu] Join failed — could not enter gameplay in time." );
				await AbortJoinAttemptAsync();
				return (false, "Your character did not spawn in time — the server may still be loading.");
			}

			return (true, "");
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns Menu] Join failed." );
			await AbortJoinAttemptAsync();
			return (false, "Join failed unexpectedly — check your connection and try again.");
		}
	}

	static async Task AbortJoinAttemptAsync()
	{
		if ( await TryRecoverJoinInWorldAsync() )
		{
			Log.Warning( "[Thorns Menu] Join aborted — recovered in loaded world instead of disconnecting." );
			return;
		}

		ThornsSessionBootstrap.CancelJoinRemoteLobbyRequest();

		if ( Networking.IsActive )
		{
			Networking.Disconnect();
			await Task.Delay( 300 );
		}

		ThornsNetworkSessionReset.ResetStaticState( "join-abort" );
		ThornsMenuJoinFlow.ResetForMainMenu();
		ThornsLoadingScreenUtil.Dismiss();

		await ThornsMenuSceneLoader.LoadMainMenuAsync();
	}

	static bool HasJoinWorldProgress()
	{
		if ( Networking.IsActive && Networking.IsHost )
			return false;

		if ( !ThornsJoinLocalPlayer.TryResolve( out _, out _ ) )
			return false;

		return ThornsMenuSceneLoader.IsInGameplayScene()
		       || ThornsTerrainBootstrap.Instance?.IsWorldApplied == true;
	}

	static async Task<bool> TryRecoverJoinInWorldAsync()
	{
		if ( !HasJoinWorldProgress() )
			return false;

		ThornsMenuSceneLoader.DismissActiveMainMenuUi();
		ThornsSessionEnterController.BeginAwaitEnter();

		if ( !await ThornsSessionEnterController.WaitUntilReadyAsync( 20000 ) )
			return false;

		return true;
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
			if ( !ThornsWorldSession.TryReadFromLobby() )
			{
				await Task.Delay( 50 );
				continue;
			}

			var bootstrap = ThornsTerrainBootstrap.Instance;
			if ( bootstrap?.Config is not null )
				ThornsWorldSession.ApplyConfig( bootstrap.Config );

			if ( ThornsWorldSession.IsAuthoritativeForJoin( bootstrap?.Config ) )
				return true;

			await Task.Delay( 50 );
		}

		return ThornsWorldSession.IsAuthoritativeForJoin( ThornsTerrainBootstrap.Instance?.Config );
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

		ThornsMenuJoinFlow.SetProgressMessage( "Entering world..." );
		ThornsMenuJoinFlow.SetStage( ThornsMenuJoinStage.EnteringWorld );
		ThornsSessionEnterController.BeginAwaitEnter();

		if ( !await ThornsSessionEnterController.WaitUntilReadyAsync( JoinLocalPawnTimeoutMs ) )
		{
			Log.Warning( "[Thorns Menu] Host failed — could not enter gameplay in time." );
			if ( Networking.IsActive )
				Networking.Disconnect();
			ThornsNetworkSessionReset.ResetStaticState( "host-abort" );
			ThornsMenuJoinFlow.ResetForMainMenu();
			await ThornsMenuSceneLoader.LoadMainMenuAsync();
			return;
		}
	}

	public static void HostLocalServer( ThornsHostedServerDto server ) => _ = HostLocalServerAsync( server );
}
