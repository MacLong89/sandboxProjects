namespace Sandbox;

/// <summary>Console diagnostics for arena visibility, spawns, and cameras.</summary>
public static class AimboxArenaDiagnostics
{
	static string _lastCameraMode = "";

	public static void LogWorldState( string reason )
	{
		var game = AimboxGame.Instance;
		var scene = Game.ActiveScene;
		if ( scene is null || !scene.IsValid() )
		{
			Log.Warning( $"[Aimbox Debug] ({reason}) No active scene." );
			return;
		}

		var local = scene.GetAllComponents<AimboxPlayerController>().FirstOrDefault( x => !x.IsProxy );
		var phase = game?.Phase.ToString() ?? "none";
		var map = game?.ActiveArenaMap.ToString() ?? "none";
		var mode = game?.Match.Mode.ToString() ?? "none";

		Log.Info( $"[Aimbox Debug] === {reason} === phase={phase} map={map} mode={mode}" );

		LogArenaRoots( scene );
		LogSampleFloorBlock( scene );
		AimboxArenaMaterialDebug.ProbeArena( reason );
		LogSpawnGroundTrace( scene, local );
		LogCameras( scene, local );
		LogPlayer( local );
	}

	public static void LogCameraHandoff( string reason, bool gameplayCamera )
	{
		var mode = gameplayCamera ? "gameplay" : "scene-preview";
		if ( mode == _lastCameraMode )
			return;

		_lastCameraMode = mode;
		Log.Info( $"[Aimbox Debug] Camera handoff ({reason}): {mode}" );
		LogCameras( Game.ActiveScene, Game.ActiveScene?.GetAllComponents<AimboxPlayerController>().FirstOrDefault( x => !x.IsProxy ) );
	}

	public static void LogSpawnResolution(
		string actorName,
		string spawnName,
		Vector3 requested,
		Vector3 resolved,
		bool groundHit,
		float groundZ )
	{
		Log.Info(
			$"[Aimbox Debug] Spawn '{actorName}' via '{spawnName}': requested={requested}, resolved={resolved}, groundHit={groundHit}, groundZ={groundZ}." );
	}

	[ConCmd( "aimbox_debug_world" )]
	public static void DebugWorldConsole() => LogWorldState( "console" );

	static void LogArenaRoots( Scene scene )
	{
		var anchor = gameObjectOrNull( AimboxGame.Instance?.ArenaAnchor );
		Log.Info( $"[Aimbox Debug] Arena anchor valid={anchor.IsValid()} pos={Format( anchor.IsValid() ? anchor.WorldPosition : Vector3.Zero )}" );

		foreach ( var rootName in AimboxArenaWorld.ArenaRootNames )
		{
			var root = AimboxArenaWorld.FindArenaRoot( rootName );
			if ( !root.IsValid() )
			{
				Log.Info( $"[Aimbox Debug]   {rootName}: missing" );
				continue;
			}

			var blockCount = root.Children.Count( c => c.IsValid() );
			var enabledRenderers = 0;
			var gameLayerRenderers = 0;
			foreach ( var child in root.Children )
			{
				if ( !child.IsValid() )
					continue;

				var renderer = child.Components.Get<ModelRenderer>();
				if ( renderer is null || !renderer.Enabled )
					continue;

				enabledRenderers++;
				if ( renderer.RenderOptions.Game )
					gameLayerRenderers++;
			}

			Log.Info(
				$"[Aimbox Debug]   {rootName}: pos={Format( root.WorldPosition )} blocks={blockCount} renderers={enabledRenderers} gameLayer={gameLayerRenderers}" );
		}

		var legacyUnderGame = 0;
		var gameGo = AimboxGame.Instance?.GameObject;
		if ( gameGo.IsValid() )
		{
			foreach ( var child in gameGo.Children )
			{
				if ( !child.IsValid() )
					continue;

				foreach ( var rootName in AimboxArenaWorld.ArenaRootNames )
				{
					if ( string.Equals( child.Name, rootName, StringComparison.OrdinalIgnoreCase ) )
					{
						legacyUnderGame++;
						Log.Warning( $"[Aimbox Debug]   orphan arena root '{child.Name}' parent='{child.Parent?.Name ?? "none"}' pos={Format( child.WorldPosition )}" );
						break;
					}
				}
			}
		}

		if ( legacyUnderGame == 0 )
			Log.Info( "[Aimbox Debug]   no orphan arena roots under Aimbox Game" );
	}

	static void LogSampleFloorBlock( Scene scene )
	{
		_ = scene;
		GameObject floor = default;

		foreach ( var rootName in AimboxArenaWorld.ArenaRootNames )
		{
			var root = AimboxArenaWorld.FindArenaRoot( rootName );
			if ( !root.IsValid() )
				continue;

			foreach ( var child in root.Children )
			{
				if ( !child.IsValid() )
					continue;

				if ( !string.Equals( child.Name, "Floor Slab", StringComparison.OrdinalIgnoreCase )
				     && !string.Equals( child.Name, "Street Asphalt", StringComparison.OrdinalIgnoreCase ) )
					continue;

				if ( child.Components.Get<ModelRenderer>() is not { Enabled: true } )
					continue;

				floor = child;
				break;
			}

			if ( floor.IsValid() )
				break;
		}

		if ( !floor.IsValid() )
		{
			Log.Warning( "[Aimbox Debug] No floor sample block found (Floor Slab / Street Asphalt)." );
			return;
		}

		var renderer = floor.Components.Get<ModelRenderer>();
		var collider = floor.Components.Get<BoxCollider>();
		Log.Info(
			$"[Aimbox Debug] Floor sample '{floor.Name}': pos={Format( floor.WorldPosition )} scale={Format( floor.WorldScale )} enabled={floor.Enabled} renderer={renderer?.Enabled} gameLayer={renderer?.RenderOptions.Game} collider={collider?.Enabled} static={collider?.Static}" );
	}

	static void LogSpawnGroundTrace( Scene scene, AimboxPlayerController local )
	{
		if ( local is null )
			return;

		var pos = local.WorldPosition;
		var tr = scene.Trace.Ray( pos + Vector3.Up * AimboxCitizenMovementMotor.GroundTraceUp, pos + Vector3.Down * AimboxCitizenMovementMotor.GroundTraceDown )
			.IgnoreGameObjectHierarchy( local.GameObject )
			.Run();

		if ( tr.Hit )
		{
			Log.Info(
				$"[Aimbox Debug] Ground trace at player: hit={tr.GameObject?.Name ?? "unknown"} pos={Format( tr.HitPosition )} normal={Format( tr.Normal )} distance={tr.Distance}" );
		}
		else
		{
			Log.Warning( $"[Aimbox Debug] Ground trace at player {Format( pos )}: NO HIT (void below feet)." );
		}
	}

	static void LogCameras( Scene scene, AimboxPlayerController local )
	{
		if ( scene is null || !scene.IsValid() )
			return;

		foreach ( var cam in scene.GetAllComponents<CameraComponent>() )
		{
			if ( cam?.GameObject is not { IsValid: true } go )
				continue;

			var isMain = cam.Enabled && cam.IsMainCamera;
			Log.Info(
				$"[Aimbox Debug] Camera '{go.Name}': enabled={cam.Enabled} goEnabled={go.Enabled} main={cam.IsMainCamera} activeMain={isMain} priority={cam.Priority} pos={Format( go.WorldPosition )} rot={go.WorldRotation.Angles()}" );
		}

		if ( local is not null )
		{
			Log.Info(
				$"[Aimbox Debug] Player eye={Format( local.EyePosition )} bodyRot={local.WorldRotation.Angles()} pitch via controller at {Format( local.WorldPosition )}" );
		}
	}

	static void LogPlayer( AimboxPlayerController local )
	{
		if ( local is null )
		{
			Log.Warning( "[Aimbox Debug] No local player." );
			return;
		}

		var arenaCenter = AimboxSpawnResolve.GetArenaCenter( local.Scene );
		var distToCenter = local.WorldPosition.Distance( arenaCenter.WithZ( local.WorldPosition.z ) );
		Log.Info(
			$"[Aimbox Debug] Player '{local.GameObject.Name}': pos={Format( local.WorldPosition )} team={local.Team} alive={local.IsAlive} distToArenaCenter={distToCenter:0}" );
	}

	static GameObject gameObjectOrNull( GameObject go ) => go.IsValid() ? go : default;

	static string Format( Vector3 v ) => $"{v.x:0.##},{v.y:0.##},{v.z:0.##}";
}
