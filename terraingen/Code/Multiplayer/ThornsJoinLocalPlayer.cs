namespace Terraingen.Multiplayer;

using Sandbox.Network;
using Terraingen;
using Terraingen.Player;
using Terraingen.TerrainGen;
using Terraingen.UI.Menu;

/// <summary>Resolves the local joiner's pawn across bootstrap vs active scene (async join must not miss the player).</summary>
public static class ThornsJoinLocalPlayer
{
	static Guid? _lastResolvedPlayerId;
	static string _lastResolvePath = "";

	public static bool TryResolve( out GameObject player, out Scene scene )
	{
		player = null;
		scene = null;

		if ( ThornsPlayerGameplay.Local is { IsValid: true } localGameplay )
		{
			player = localGameplay.GameObject;
			scene = player.Scene;
			if ( scene.IsValid )
			{
				LogResolveSuccess( player, "Local gameplay", scene );
				return true;
			}
		}

		foreach ( var candidateScene in EnumerateCandidateScenes() )
		{
			if ( TryResolveInScene( candidateScene, out player ) )
			{
				scene = candidateScene;
				LogResolveSuccess( player, $"scene '{candidateScene.Name}'", scene );
				return true;
			}
		}

		_lastResolvedPlayerId = null;
		_lastResolvePath = "";
		ThornsJoinFlowDebug.JoinInfo( $"TryResolve failed — {DescribePawnResolveFailure()}" );
		return false;
	}

	static void LogResolveSuccess( GameObject player, string path, Scene scene )
	{
		var playerId = player.Id;
		var signature = $"{path}|{scene.Name}|{player.Name}";
		if ( _lastResolvedPlayerId == playerId && _lastResolvePath == signature )
			return;

		_lastResolvedPlayerId = playerId;
		_lastResolvePath = signature;
		ThornsJoinFlowDebug.JoinInfo( $"TryResolve via {path} '{player.Name}' scene={scene.Name}" );
	}

	public static bool IsReadyForJoinHandoff()
	{
		if ( !TryResolve( out var player, out var scene ) )
			return false;

		if ( ThornsLocalHostSpawnCoordinator.IsHandling( player )
		     || ThornsLocalHostSpawnCoordinator.HasCompletedPresentation( player ) )
			return true;

		if ( ThornsLocalPlayer.IsLocallyControlledPawn( player ) )
			return true;

		return player.Components.Get<ThornsPlayerGameplay>().IsValid()
		       || player.Components.Get<ThornsPlayerSession>().IsValid();
	}

	static bool TryResolveInScene( Scene candidateScene, out GameObject player )
	{
		player = null;
		if ( candidateScene is null || !candidateScene.IsValid )
			return false;

		var observerPlayer = ThornsSceneObserver.FindLocalPlayerObject( candidateScene );
		if ( observerPlayer.IsValid() )
		{
			player = observerPlayer;
			return true;
		}

		var localConn = Connection.Local;
		var online = Networking.IsActive;

		foreach ( var session in candidateScene.GetAllComponents<ThornsPlayerSession>() )
		{
			if ( !session.IsValid() || !session.GameObject.IsValid() )
				continue;

			if ( MatchesLocalConnection( session.GameObject, session.OwnerConnection, localConn, online ) )
			{
				player = session.GameObject;
				return true;
			}
		}

		foreach ( var gameplay in candidateScene.GetAllComponents<ThornsPlayerGameplay>() )
		{
			if ( !gameplay.IsValid() || !gameplay.GameObject.IsValid() )
				continue;

			if ( MatchesLocalConnection( gameplay.GameObject, null, localConn, online ) )
			{
				player = gameplay.GameObject;
				return true;
			}
		}

		if ( TryResolveByDisplayName( candidateScene, localConn, out player ) )
			return true;

		return false;
	}

	static bool TryResolveByDisplayName( Scene scene, Connection localConn, out GameObject player )
	{
		player = null;
		if ( localConn is null )
			return false;

		var token = localConn.DisplayName;
		if ( string.IsNullOrWhiteSpace( token ) )
			return false;

		foreach ( var obj in scene.GetAllObjects( true ) )
		{
			if ( !obj.IsValid() || !obj.Name.Contains( token, StringComparison.OrdinalIgnoreCase ) )
				continue;

			if ( !obj.Components.Get<PlayerController>( FindMode.EverythingInSelf ).IsValid() )
				continue;

			if ( !obj.Components.Get<ThornsPlayerGameplay>( FindMode.EverythingInSelf ).IsValid()
			     && !obj.Components.Get<ThornsPlayerSession>( FindMode.EverythingInSelf ).IsValid() )
				continue;

			player = obj;
			return true;
		}

		return false;
	}

	static IEnumerable<Scene> EnumerateCandidateScenes()
	{
		var seen = new HashSet<Scene>();

		void TryAdd( Scene candidate )
		{
			if ( candidate is { IsValid: true } && seen.Add( candidate ) )
			{
				/* tracked */
			}
		}

		TryAdd( ThornsTerrainBootstrap.Instance?.Scene );
		TryAdd( Game.ActiveScene );

		if ( ThornsMenuSceneLoader.TryGetGameplayScene( null, out var gameplayScene ) )
			TryAdd( gameplayScene );

		return seen;
	}

	static bool MatchesLocalConnection(
		GameObject pawn,
		Connection sessionOwner,
		Connection localConn,
		bool online )
	{
		if ( !pawn.IsValid() )
			return false;

		if ( ThornsLocalHostSpawnCoordinator.IsHandling( pawn )
		     || ThornsLocalHostSpawnCoordinator.HasCompletedPresentation( pawn ) )
			return true;

		if ( ThornsLocalPlayer.IsLocallyControlledPawn( pawn ) )
			return true;

		if ( !online )
			return true;

		if ( localConn is null )
			return false;

		if ( sessionOwner?.Id == localConn.Id )
			return true;

		if ( pawn.Network.OwnerId == localConn.Id )
			return true;

		if ( pawn.Network.Owner == localConn )
			return true;

		return pawn.Name.Contains( localConn.DisplayName, StringComparison.OrdinalIgnoreCase );
	}

	static string DescribePawnResolveFailure() => ThornsJoinFlowDebug.DescribePawnResolveFailure();
}
