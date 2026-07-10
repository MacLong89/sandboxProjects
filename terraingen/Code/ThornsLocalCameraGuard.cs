namespace Terraingen;

using Terraingen.Animals;

/// <summary>Keeps the local pawn as <see cref="Scene.Camera"/> and disables wildlife bone cameras.</summary>
[Title( "Thorns Local Camera Guard" )]
[Category( "Thorns/Player" )]
public sealed class ThornsLocalCameraGuard : Component
{
	TimeUntil _nextWildlifeSweep;

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying )
			return;

		var scene = Scene;
		if ( scene is null || !scene.IsValid() )
			return;

		if ( !_nextWildlifeSweep )
		{
			_nextWildlifeSweep = 0.15f;
			ThornsAnimalCameraGuard.SuppressWildlifeCamerasInScene( scene );
		}

		var player = ThornsSceneObserver.FindLocalPlayerObject( scene );
		if ( !player.IsValid() )
			return;

		var sceneCam = scene.Camera;
		if ( sceneCam.IsValid()
		     && sceneCam.Enabled
		     && CameraBelongsToPawn( player, sceneCam.GameObject ) )
			return;

		if ( sceneCam.IsValid()
		     && sceneCam.Enabled
		     && ThornsAnimalCameraGuard.IsWildlifeCamera( sceneCam.GameObject ) )
		{
			Log.Warning( $"[Thorns Scene] Wildlife camera owned the view on '{sceneCam.GameObject.Name}' — reclaiming for '{player.Name}'." );
		}

		ThornsSceneObserver.EnsureLocalPawnOwnsMainCamera( scene, player );
	}

	static bool CameraBelongsToPawn( GameObject pawn, GameObject cameraObject )
	{
		if ( !pawn.IsValid() || !cameraObject.IsValid() )
			return false;

		for ( var node = cameraObject; node.IsValid(); node = node.Parent )
		{
			if ( node == pawn )
				return true;
		}

		return false;
	}

	public static void EnsureOn( GameObject host )
	{
		if ( !host.IsValid() )
			return;

		if ( host.Components.Get<ThornsLocalCameraGuard>( FindMode.EnabledInSelf ).IsValid() )
			return;

		host.Components.Create<ThornsLocalCameraGuard>();
	}
}
