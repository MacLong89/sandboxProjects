namespace FinalOutpost;

/// <summary>
/// Primary scene entry via <see cref="ISceneStartup"/>. Does not rely on scene-authored components.
///
/// IMPORTANT: GameObjectSystems are created for editor scenes too. Never Listen/Update boot
/// while the editor is open without play mode — that spammed GameBoot forever
/// ("lobby outside of a game" + GameCore missing loop).
/// </summary>
public sealed class OutpostBootstrap : GameObjectSystem<OutpostBootstrap>, ISceneStartup
{
	const int MaxRecoveryAttempts = 5;

	float _retryCooldown;
	int _recoveryAttempts;
	bool _recoveryGaveUp;

	public OutpostBootstrap( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, 0, TickBootRecovery, "FinalOutpost Boot Recovery" );
	}

	void ISceneStartup.OnHostPreInitialize( SceneFile scene )
	{
	}

	void ISceneStartup.OnHostInitialize()
	{
		_recoveryAttempts = 0;
		_recoveryGaveUp = false;
		Log.Info( "[FinalOutpost] OutpostBootstrap.OnHostInitialize" );
		GameBoot.Run( Scene );
	}

	void ISceneStartup.OnClientInitialize()
	{
		_recoveryAttempts = 0;
		_recoveryGaveUp = false;
		Log.Info( "[FinalOutpost] OutpostBootstrap.OnClientInitialize" );
		GameBoot.Run( Scene );
	}

	void TickBootRecovery()
	{
		// Editor scene open ≠ playing. OutpostBootstrap exists on those scenes and
		// used to retry GameBoot every 0.5s indefinitely — including when not Playing.
		if ( !GameBoot.ShouldAttemptBoot( Scene ) )
			return;

		if ( GameBoot.HasRunningCore( Scene ) )
		{
			_recoveryAttempts = 0;
			_recoveryGaveUp = false;
			return;
		}

		if ( _recoveryGaveUp )
			return;

		_retryCooldown -= Time.Delta;
		if ( _retryCooldown > 0f )
			return;

		_retryCooldown = 1.0f;
		_recoveryAttempts++;

		if ( _recoveryAttempts > MaxRecoveryAttempts )
		{
			_recoveryGaveUp = true;
			Log.Error( $"[FinalOutpost] Boot recovery gave up after {MaxRecoveryAttempts} attempts — Stop/Play the scene (GameCore never woke)." );
			GameCore.SetBootError( "GameCore failed to start. Stop and Play the game scene again." );
			return;
		}

		Log.Warning( $"[FinalOutpost] GameCore missing — recovery attempt {_recoveryAttempts}/{MaxRecoveryAttempts}" );
		GameBoot.Run( Scene );
	}
}
