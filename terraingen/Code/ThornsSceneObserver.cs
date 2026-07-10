namespace Terraingen;

using Sandbox.Network;
using Terraingen.Animals;
using Terraingen.Multiplayer;
using Terraingen.Player;
using Terraingen.TerrainGen;

/// <summary>
/// Cached scene observer (local player, Terrain Explorer, gameplay camera) for foliage, clutter, and HUD.
/// </summary>
public static class ThornsSceneObserver
{
	const string ExplorerNameToken = "Terrain Explorer";
	const string PreviewCameraName = "Terrain Preview Camera";

	static GameObject _cachedLocalPlayer;
	static PlayerController _cachedLocalPlayerController;
	static Scene _cachedLocalPlayerScene;
	static readonly List<PlayerController> _cachedPlayers = new( 8 );
	static Scene _cachedPlayersScene;
	static TimeUntil _nextPlayerCacheRefresh;
	static bool _reclaimingLocalPawnCamera;

	public static Vector3 Resolve(
		Scene scene,
		ref GameObject explorerObject,
		ref CameraComponent mainCamera,
		ref TimeUntil nextRefresh )
	{
		if ( scene is null || !scene.IsValid )
			return Vector3.Zero;

		if ( nextRefresh || (!IsValid( explorerObject ) && !IsValid( mainCamera )) )
			Refresh( scene, ref explorerObject, ref mainCamera, ref nextRefresh );

		if ( TryGetCachedLocalPlayer( scene, out var cachedLocalPlayer ) )
			return cachedLocalPlayer.WorldPosition;

		var localPlayer = FindLocalPlayerObject( scene );
		if ( IsValid( localPlayer ) )
			return localPlayer.WorldPosition;

		if ( IsValid( explorerObject ) )
			return explorerObject.WorldPosition;

		if ( IsValid( mainCamera ) )
			return mainCamera.GameObject.WorldPosition;

		return Vector3.Zero;
	}

	public static void Refresh(
		Scene scene,
		ref GameObject explorerObject,
		ref CameraComponent mainCamera,
		ref TimeUntil nextRefresh )
	{
		if ( scene is null || !scene.IsValid )
			return;

		nextRefresh = IsValid( explorerObject ) ? 2f : 0.75f;

		explorerObject = FindLocalPlayerObject( scene );

		if ( !IsValid( explorerObject ) )
		{
			foreach ( var obj in scene.GetAllObjects( true ) )
			{
				if ( !IsValid( obj ) )
					continue;

				if ( !obj.Name.Contains( ExplorerNameToken, StringComparison.OrdinalIgnoreCase ) )
					continue;

				if ( obj.Components.Get<PlayerController>( FindMode.EverythingInSelf ).IsValid )
				{
					explorerObject = obj;
					break;
				}
			}
		}

		mainCamera = null;

		if ( scene.Camera is not null && scene.Camera.IsValid )
		{
			var camGo = scene.Camera.GameObject;
			if ( IsValid( camGo ) && !camGo.Name.Equals( PreviewCameraName, StringComparison.OrdinalIgnoreCase ) )
				mainCamera = scene.Camera;
		}

		if ( !IsValid( mainCamera ) )
		{
			foreach ( var cam in scene.GetAllComponents<CameraComponent>() )
			{
				if ( !cam.IsValid || !cam.IsMainCamera )
					continue;

				if ( cam.GameObject.Name.Equals( PreviewCameraName, StringComparison.OrdinalIgnoreCase ) )
					continue;

				mainCamera = cam;
				break;
			}
		}
	}

	public static bool TryGetMainCamera( Scene scene, out CameraComponent camera )
	{
		camera = null;
		if ( scene is null || !scene.IsValid )
			return false;

		if ( scene.Camera is not null && scene.Camera.IsValid() )
		{
			var camGo = scene.Camera.GameObject;
			if ( IsValid( camGo ) && !camGo.Name.Equals( PreviewCameraName, StringComparison.OrdinalIgnoreCase ) )
			{
				camera = scene.Camera;
				return true;
			}
		}

		foreach ( var cam in scene.GetAllComponents<CameraComponent>() )
		{
			if ( !IsValid( cam ) || !cam.IsMainCamera )
				continue;

			if ( cam.GameObject.Name.Equals( PreviewCameraName, StringComparison.OrdinalIgnoreCase ) )
				continue;

			camera = cam;
			return true;
		}

		return false;
	}

	public static GameObject FindLocalPlayerObject( Scene scene )
	{
		if ( scene is null || !scene.IsValid )
			return null;

		if ( TryGetCachedLocalPlayer( scene, out var cachedLocalPlayer ) )
			return cachedLocalPlayer;

		if ( ThornsPlayerGameplay.Local.IsValid() )
		{
			var local = ThornsPlayerGameplay.Local.GameObject;
			if ( local.IsValid() && local.Scene == scene )
			{
				CacheLocalPlayer( scene, local );
				return local;
			}
		}

		if ( Networking.IsActive )
		{
			foreach ( var session in scene.GetAllComponents<ThornsPlayerSession>() )
			{
				if ( !session.IsValid() || !ThornsLocalPlayer.IsLocalConnectionSession( session ) )
					continue;

				if ( session.GameObject.IsValid() )
				{
					CacheLocalPlayer( scene, session.GameObject );
					return session.GameObject;
				}
			}

			foreach ( var gameplay in scene.GetAllComponents<ThornsPlayerGameplay>() )
			{
				if ( !gameplay.IsValid() || !gameplay.IsLocalPlayer() )
					continue;

				if ( gameplay.GameObject.IsValid() )
				{
					CacheLocalPlayer( scene, gameplay.GameObject );
					return gameplay.GameObject;
				}
			}
		}

		ClearCachedLocalPlayer();
		return null;
	}

	static void CacheLocalPlayer( Scene scene, GameObject player )
	{
		_cachedLocalPlayer = player;
		_cachedLocalPlayerController = player.Components.Get<PlayerController>( FindMode.EverythingInSelf );
		_cachedLocalPlayerScene = scene;
	}

	static bool TryGetCachedLocalPlayer( Scene scene, out GameObject player )
	{
		player = _cachedLocalPlayer;
		return _cachedLocalPlayerScene == scene
		       && IsValid( player )
		       && ThornsLocalPlayer.IsLocalConnectionPlayerRoot( player );
	}

	/// <summary>
	/// Aim ray for combat and placement traces: active pawn camera + mouse cursor, never the terrain preview camera.
	/// </summary>
	public static bool TryResolveLocalAimRay( GameObject player, out Vector3 origin, out Vector3 direction, bool useScreenCenter = false )
	{
		origin = default;
		direction = Vector3.Forward;

		if ( !IsValid( player ) )
			return false;

		if ( ThornsPlayerFirstPersonRig.TryResolveActivePlayerCamera( player, out var pawnCamera )
		     && TryMouseRayFromCamera( pawnCamera, out origin, out direction, useScreenCenter ) )
			return true;

		if ( player.Scene is { IsValid: true } scene )
		{
			if ( IsValid( scene.Camera )
			     && scene.Camera.Enabled
			     && !scene.Camera.GameObject.Name.Equals( PreviewCameraName, StringComparison.OrdinalIgnoreCase )
			     && ( scene.Camera.IsMainCamera || IsDescendantOf( player, scene.Camera.GameObject ) )
			     && TryMouseRayFromCamera( scene.Camera, out origin, out direction, useScreenCenter ) )
				return true;

			foreach ( var cam in scene.GetAllComponents<CameraComponent>() )
			{
				if ( !IsValid( cam ) || !cam.Enabled || !cam.IsMainCamera )
					continue;

				if ( cam.GameObject.Name.Equals( PreviewCameraName, StringComparison.OrdinalIgnoreCase ) )
					continue;

				if ( TryMouseRayFromCamera( cam, out origin, out direction, useScreenCenter ) )
					return true;
			}
		}

		var controller = player.Components.Get<PlayerController>( FindMode.EverythingInSelf );
		if ( !controller.IsValid() )
			return false;

		const float eyeHeight = 64f;
		origin = player.WorldPosition + Vector3.Up * eyeHeight;
		direction = controller.EyeAngles.ToRotation().Forward.Normal;
		return direction.Length >= 0.95f;
	}

	static bool TryMouseRayFromCamera( CameraComponent camera, out Vector3 origin, out Vector3 direction, bool useScreenCenter = false )
	{
		origin = default;
		direction = Vector3.Forward;

		if ( !IsValid( camera ) || !camera.Enabled )
			return false;

		var aimRay = useScreenCenter
			? camera.ScreenNormalToRay( new Vector3( 0.5f, 0.5f, 0f ) )
			: camera.ScreenPixelToRay( Mouse.Position );
		origin = aimRay.Position;
		direction = aimRay.Forward.Normal;
		if ( direction.Length < 0.95f )
			direction = camera.GameObject.WorldRotation.Forward.Normal;

		return direction.Length >= 0.95f;
	}

	/// <summary>Demote preview/animal/UI cameras and restore the local pawn as <see cref="Scene.Camera"/>.</summary>
	public static void EnsureLocalPawnOwnsMainCamera( Scene scene, GameObject player )
	{
		if ( scene is null || !scene.IsValid || !IsValid( player ) )
			return;

		if ( !ThornsLocalPlayer.IsLocallyControlledPawn( player ) )
			return;

		if ( _reclaimingLocalPawnCamera )
			return;

		_reclaimingLocalPawnCamera = true;
		try
		{
			SuppressTerrainPreviewMainCamera( scene );
			ReclaimMainCameraForLocalPawn( scene, player );
			Terraingen.Player.ThornsPlayerFirstPersonRig.EnsureLocalPresentationCamera( player );
		}
		finally
		{
			_reclaimingLocalPawnCamera = false;
		}
	}

	/// <summary>Disable preview fly cam and give the local pawn the main camera + input.</summary>
	public static void ClearCachedLocalPlayer()
	{
		_cachedLocalPlayer = null;
		_cachedLocalPlayerController = null;
		_cachedLocalPlayerScene = null;
	}

	public static void FocusLocalPlayer( Scene scene, GameObject player )
	{
		if ( scene is null || !scene.IsValid || !IsValid( player ) )
			return;

		if ( !ThornsLocalPlayer.IsLocallyControlledPawn( player ) )
		{
			Log.Warning( $"[Thorns Scene] FocusLocalPlayer skipped — '{player.Name}' is not locally owned on this machine." );
			return;
		}

		Log.Info( $"[Thorns Scene] FocusLocalPlayer: begin '{player.Name}'." );
		ThornsPawnInputIsolation.ApplyForLocalPawn( scene, player );
		Terraingen.Player.ThornsPlayerPresentationBootstrap.EnsureFirstPersonPresentation( player );
		Log.Info( "[Thorns Scene] FocusLocalPlayer: reclaim main camera." );
		EnsureLocalPawnOwnsMainCamera( scene, player );

		var controller = player.Components.Get<PlayerController>( FindMode.EverythingInSelf );
		if ( controller.IsValid() )
		{
			var locomotion = player.Components.Get<Terraingen.Player.ThornsPlayerLocomotion>()
			                 ?? player.Components.Create<Terraingen.Player.ThornsPlayerLocomotion>();
			locomotion.ConfigurePlayerController();
			controller.Enabled = true;
		}

		Terraingen.Player.ThornsPlayerFirstPersonRig.ReleaseDeathCameraPin( player );

		CacheLocalPlayer( scene, player );
		Log.Info( "[Thorns Scene] FocusLocalPlayer: done." );
	}

	static void ReclaimMainCameraForLocalPawn( Scene scene, GameObject player )
	{
		ThornsAnimalCameraGuard.SuppressWildlifeCamerasInScene( scene );

		var demoted = 0;
		foreach ( var cam in scene.GetAllComponents<CameraComponent>() )
		{
			if ( !IsValid( cam ) )
				continue;

			if ( cam.GameObject.Name.Equals( PreviewCameraName, StringComparison.OrdinalIgnoreCase ) )
			{
				if ( cam.IsMainCamera || cam.Enabled )
				{
					cam.IsMainCamera = false;
					cam.Enabled = false;
					demoted++;
				}

				continue;
			}

			if ( ThornsAnimalCameraGuard.IsWildlifeCamera( cam.GameObject ) )
			{
				if ( cam.IsMainCamera || cam.Enabled )
				{
					cam.IsMainCamera = false;
					cam.Enabled = false;
					demoted++;
				}

				continue;
			}

			if ( !cam.IsMainCamera )
				continue;

			if ( IsDescendantOf( player, cam.GameObject ) )
				continue;

			cam.IsMainCamera = false;
			demoted++;
		}

		var rig = Terraingen.Player.ThornsPlayerFirstPersonRig.ResolvePresentationCameraObject( player );
		if ( rig.IsValid() )
		{
			var pawnCam = rig.Components.Get<CameraComponent>();
			if ( IsValid( pawnCam ) )
			{
				pawnCam.Enabled = true;
				pawnCam.IsMainCamera = true;
			}
		}

		if ( demoted > 0 )
			Log.Info( $"[Thorns Scene] ReclaimMainCamera: demoted {demoted} foreign main camera(s)." );
	}

	static bool IsDescendantOf( GameObject root, GameObject candidate )
	{
		if ( !IsValid( root ) || !IsValid( candidate ) )
			return false;

		for ( var node = candidate; IsValid( node ); node = node.Parent )
		{
			if ( node == root )
				return true;
		}

		return false;
	}

	/// <summary>Let PlayerController own the main camera when a pawn spawns (multiplayer / explorer).</summary>
	public static void SuppressTerrainPreviewMainCamera( Scene scene )
	{
		if ( scene is null || !scene.IsValid )
			return;

		foreach ( var cam in scene.GetAllComponents<CameraComponent>() )
		{
			if ( !IsValid( cam ) || !IsValid( cam.GameObject ) )
				continue;

			if ( !cam.GameObject.Name.Equals( PreviewCameraName, StringComparison.OrdinalIgnoreCase ) )
				continue;

			cam.IsMainCamera = false;
			cam.Enabled = false;

			var fly = cam.Components.Get<TerrainFlyCamera>();
			if ( fly.IsValid() )
				fly.Enabled = false;
		}
	}

	static bool IsValid( GameObject obj ) => obj is not null && obj.IsValid();

	static bool IsValid( CameraComponent cam ) => cam is not null && cam.IsValid;

	/// <summary>Cached player controllers — refreshed ~4×/sec to avoid repeated scene scans.</summary>
	public static void ForEachPlayer( Scene scene, Action<PlayerController> action )
	{
		if ( scene is null || !scene.IsValid || action is null )
			return;

		RefreshPlayerCache( scene );

		for ( var i = 0; i < _cachedPlayers.Count; i++ )
		{
			var controller = _cachedPlayers[i];
			if ( controller.IsValid() )
				action( controller );
		}
	}

	static void RefreshPlayerCache( Scene scene )
	{
		if ( _cachedPlayersScene == scene && _nextPlayerCacheRefresh )
			return;

		_nextPlayerCacheRefresh = 0.25f;
		_cachedPlayersScene = scene;
		_cachedPlayers.Clear();

		foreach ( var controller in scene.GetAllComponents<PlayerController>() )
		{
			if ( controller.IsValid() && controller.GameObject.IsValid() )
				_cachedPlayers.Add( controller );
		}
	}
}
