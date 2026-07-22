namespace FinalOutpost;

/// <summary>Shared startup path for <see cref="OutpostBootstrap"/> and optional <see cref="SceneBoot"/>.</summary>
public static class GameBoot
{
	static Scene _bootingScene;

	/// <summary>
	/// Only boot/recover while actually playing a game scene.
	/// GameObjectSystems also live on editor preview scenes — those must stay quiet.
	/// </summary>
	public static bool ShouldAttemptBoot( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return false;

		// Scene open in the editor without Play Mode — do not boot or spam recovery.
		if ( scene.IsEditor && !Game.IsPlaying )
			return false;

		// Prefer IsPlaying; InGame covers some published-build cases where IsPlaying is false.
		if ( !Game.IsPlaying && !Game.InGame )
			return false;

		return true;
	}

	/// <summary>True when a live GameCore owns this scene.</summary>
	public static bool HasRunningCore( Scene scene )
	{
		if ( scene is null ) return false;

		var core = GameCore.Instance;
		if ( core is not null && core.IsValid() && core.Scene == scene )
			return true;

		// Fallback: Instance may lag one frame behind Create; scene query is authoritative.
		return scene.GetAllComponents<GameCore>().Any( c => c.IsValid() );
	}

	public static void Run( Scene scene )
	{
		if ( scene is null )
		{
			Log.Error( "[FinalOutpost] GameBoot.Run aborted — scene is null" );
			return;
		}

		if ( !ShouldAttemptBoot( scene ) )
		{
			Log.Info( "[FinalOutpost] GameBoot.Run skipped — not in an active play session" );
			return;
		}

		if ( HasRunningCore( scene ) )
		{
			Log.Info( "[FinalOutpost] GameBoot.Run skipped — GameCore already running in this scene" );
			return;
		}

		if ( _bootingScene == scene )
		{
			Log.Info( "[FinalOutpost] GameBoot.Run skipped — boot already in progress" );
			return;
		}

		_bootingScene = scene;
		Log.Info( "[FinalOutpost] GameBoot.Run begin" );

		// Camera + HUD first so a later exception still leaves something on screen.
		EnsureFallbackCamera( scene );
		EnsureBootScreen( scene );
		EnsureDiagnostics( scene );

		try
		{
			Log.Info( "[FinalOutpost] Boot starting..." );

			WarmCoreAssets();

			if ( !scene.GetAllComponents<AmbiencePlayer>().Any() )
			{
				var systemsGo = CreateSceneObject( scene, "RuntimeSystems" );
				systemsGo.Components.Create<AmbiencePlayer>();
				systemsGo.Components.Create<NightCombatMusicPlayer>();
				systemsGo.Components.Create<DayNightLighting>();
				systemsGo.Components.Create<WeaponModelLoader>();
			}

			if ( !HasRunningCore( scene ) )
			{
				// Must use Scene.CreateObject — bare `new GameObject` from a system tick
				// was creating objects that never received OnAwake (Instance stayed null).
				var coreGo = CreateSceneObject( scene, "GameCore" );
				var core = coreGo.Components.Create<GameCore>();
				if ( core is null || !core.IsValid() )
					throw new Exception( "Components.Create<GameCore>() returned invalid" );

				Log.Info( $"[FinalOutpost] GameCore created — Instance={(GameCore.Instance is not null)} valid={core.IsValid()}" );
			}

			// Single-player: do not CreateLobby — a public/private lobby can leave the
			// session in a networked state with a black screen / no main camera.
			Log.Info( "[FinalOutpost] Boot finished." );
		}
		catch ( Exception e )
		{
			GameCore.SetBootError( e.Message );
			Log.Error( $"[FinalOutpost] Boot failed: {e}" );
			EnsureBootScreen( scene );
			EnsureFallbackCamera( scene );
		}
		finally
		{
			if ( _bootingScene == scene )
				_bootingScene = null;
		}
	}

	/// <summary>Spawn a root object owned by <paramref name="scene"/> so lifecycle hooks fire.</summary>
	static GameObject CreateSceneObject( Scene scene, string name )
	{
		var go = scene.CreateObject( true );
		go.Name = name;
		return go;
	}

	static void WarmCoreAssets()
	{
		_ = MeshPrimitives.Mat;
		_ = MeshPrimitives.Box;
		_ = MeshPrimitives.Quad;
		_ = MeshPrimitives.Cylinder;
		_ = MeshPrimitives.Pyramid;
		_ = AssetSafe.Model( CharacterModel.CitizenVmdl );

		// Warm FO materials + verify every packaged mat/sound/UI path players need.
		ContentShipGate.WarmAndVerify();

		Log.Info( "[FinalOutpost] Core assets warmed." );
	}

	// Intentionally unused: this game is single-player (MaxPlayers 1). Creating a lobby
	// was linked to black-screen boots; keep the helper for a future multiplayer path.
	private static void TryCreateLobby()
	{
		if ( !Game.IsPlaying && !Game.InGame )
			return;

		if ( Networking.IsActive )
			return;

		try
		{
			Networking.CreateLobby( new Sandbox.Network.LobbyConfig
			{
				MaxPlayers = 1,
				Name = "The Final Outpost",
				Privacy = Sandbox.Network.LobbyPrivacy.Private,
				Hidden = true
			} );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[FinalOutpost] Lobby create skipped: {e.Message}" );
		}
	}

	public static void EnsureBootScreen( Scene scene )
	{
		if ( scene.GetAllComponents<HudHost>().Any() )
			return;

		Log.Info( "[FinalOutpost] EnsureBootScreen creating BootHUD" );
		var hudGo = CreateSceneObject( scene, "BootHUD" );
		hudGo.Components.Create<HudHost>();
	}

	public static void DedupeHudHosts( Scene scene )
	{
		var hosts = scene.GetAllComponents<HudHost>().ToList();
		for ( var i = 1; i < hosts.Count; i++ )
		{
			Log.Warning( $"[FinalOutpost] Removing duplicate HudHost '{hosts[i].GameObject.Name}'" );
			hosts[i].GameObject.Destroy();
		}
	}

	public static void EnsureDiagnostics( Scene scene )
	{
		if ( scene.GetAllComponents<BootDiagnosticsRunner>().Any() )
			return;

		var go = CreateSceneObject( scene, "BootDiagnostics" );
		go.Components.Create<BootDiagnosticsRunner>();
	}

	public static void EnsureFallbackCamera( Scene scene )
	{
		foreach ( var existingCam in scene.GetAllComponents<CameraComponent>() )
		{
			if ( existingCam.GameObject.Name == "StartupCamera" )
				existingCam.GameObject.Destroy();
		}

		if ( scene.GetAllComponents<CameraComponent>().Any( c => c.IsMainCamera ) )
			return;

		Log.Info( "[FinalOutpost] EnsureFallbackCamera creating FallbackCamera" );
		var camGo = CreateSceneObject( scene, "FallbackCamera" );
		var fallbackCam = camGo.Components.Create<CameraComponent>();
		fallbackCam.FieldOfView = GameConstants.CameraFov;
		fallbackCam.ZNear = 10f;
		fallbackCam.ZFar = 20000f;
		fallbackCam.BackgroundColor = new Color( 0.55f, 0.78f, 0.95f );
		fallbackCam.IsMainCamera = true;
		camGo.WorldPosition = new Vector3( GameConstants.U( -600f ), GameConstants.U( -600f ), GameConstants.H( 900f ) );
		camGo.WorldRotation = Rotation.From( new Angles( 55f, 45f, 0f ) );
	}
}
