namespace Offshore;

/// <summary>
/// Absolute minimum Play test — this file is the ONLY gameplay type compiled
/// while diagnosing the editor hard-crash. No other Offshore systems load.
/// </summary>
public sealed class GameManager : Component
{
	protected override void OnStart()
	{
		Log.Info( $"[Offshore] MINIMAL OnStart IsEditor={Scene.IsEditor} IsPlaying={Game.IsPlaying}" );

		if ( Scene.IsEditor )
			return;

		var camGo = new GameObject( true, "MinimalCamera" );
		var cam = camGo.Components.Create<CameraComponent>();
		cam.IsMainCamera = true;
		cam.FieldOfView = 60f;
		cam.ZNear = 1f;
		cam.ZFar = 10000f;
		cam.BackgroundColor = new Color( 0.1f, 0.4f, 0.7f );
		camGo.WorldPosition = new Vector3( 0f, -50f, 10f );
		camGo.WorldRotation = Rotation.LookAt( new Vector3( 0f, 1f, 0f ), Vector3.Up );

		Log.Info( "[Offshore] MINIMAL boot OK — solid blue camera only. Play is stable if you see this." );
	}
}
