namespace Terraingen.Multiplayer;

using Sandbox;
using Terraingen.Audio;
using Terraingen;
using Terraingen.Animals;
using Terraingen.Combat;
using Terraingen.TerrainGen;
using Terraingen.UI;
using Terraingen.UI.Core;
using Terraingen.UI.Menu;
using Terraingen.Clutter;
using Terraingen.Player;

/// <summary>Local pawn spawn presentation for listen-server host and joining clients (sync essentials + deferred HUD/cosmetics).</summary>
public static class ThornsLocalHostSpawnCoordinator
{
	static GameObject _player;
	static Scene _scene;
	static int _deferredStep;
	static int _stepRetries;
	static TimeSince _deferredQueuedAt;
	static bool _loggedStall;
	static double _lastTickRealtime = -1;
	static bool _cosmeticsWaitStarted;

	public static bool IsDeferredPending => _deferredStep > 0;

	public static bool IsHandling( GameObject player ) =>
		player.IsValid() && ReferenceEquals( _player, player );

	public static void Queue( Scene scene, GameObject player )
	{
		_scene = scene;
		_player = player;
		_deferredStep = 0;
		_stepRetries = 0;
		_deferredQueuedAt = 0;
		_loggedStall = false;
		_lastTickRealtime = -1;

		try
		{
			ThornsWorldBootGate.EnsureDriver();

			var bootstrap = scene.GetAllComponents<ThornsTerrainBootstrap>().FirstOrDefault();
			var joiningClient = Networking.IsActive && !Networking.IsHost;
			if ( joiningClient || ( Networking.IsActive && Networking.IsHost ) )
			{
				if ( bootstrap?.IsWorldApplied != true )
					ThornsWorldBootGate.BeginLocalBoot();
			}

			ThornsSceneObserver.SuppressTerrainPreviewMainCamera( scene );
			ThornsMenuJoinFlow.SetStage( ThornsMenuJoinStage.EnteringWorld );
			ThornsMenuJoinFlow.SetProgressMessage( "Loading world around you..." );

			ThornsSceneObserver.FocusLocalPlayer( scene, player );
			ThornsGameplaySession.EnsureLocalPlayerControl( skipCameraReclaim: true );
			ThornsGameplayUiHost.RefreshScreenPanelCamera( scene );

			if ( bootstrap.IsValid() )
				ThornsCombatFeedbackHost.EnsureOn( bootstrap.GameObject );

			ThornsPlayerFirstPersonRig.EnsureLocalPresentationCamera( player );

			if ( bootstrap.IsValid() && player.IsValid() )
				bootstrap.EnsureMineralPocketNearPlayer( player.WorldPosition );

			ThornsGameplaySfx.WarmHarvestToolSounds();

			_deferredStep = 1;
			_deferredQueuedAt = 0;
			_stepRetries = 0;
			ThornsMenuJoinFlow.SetProgressMessage( "Loading HUD..." );
			ThornsLocalHostSpawnDriver.Ensure();
		}
		catch ( Exception e )
		{
			Log.Error( e, "[Thorns Terrain] Local player setup failed." );
			ThornsMenuJoinFlow.CompleteEnterWorld();
			Reset();
		}
	}

	public static void TickDeferred()
	{
		if ( _deferredStep <= 0 || !Game.IsPlaying )
			return;

		var now = Time.Now;
		if ( now <= _lastTickRealtime )
			return;

		_lastTickRealtime = now;

		if ( !_loggedStall && _deferredQueuedAt > 3f )
		{
			_loggedStall = true;
			Log.Warning( $"[Thorns Terrain] Local player deferred setup stalled at step {_deferredStep} for {_deferredQueuedAt:F1}s." );
		}

		var player = _player;
		if ( player is null || !player.IsValid() || _scene is null || !_scene.IsValid )
		{
			Log.Warning( "[Thorns Terrain] Local player deferred setup: aborted (player or scene invalid)." );
			Reset();
			return;
		}

		var bootstrap = _scene.GetAllComponents<ThornsTerrainBootstrap>().FirstOrDefault();
		var gameplay = player.Components.Get<ThornsPlayerGameplay>();

		try
		{
			switch ( _deferredStep )
			{
				case 1:
					ThornsLocalPlayerPresentation.EnsureLocalReady( _scene, player );

					if ( !ThornsLocalPlayerPresentation.IsHudReady() && _stepRetries++ < 240 )
					{
						ThornsMenuJoinFlow.SetProgressMessage( "Loading HUD..." );
						return;
					}

					if ( !ThornsLocalPlayerPresentation.IsSnapshotReady() )
					{
						if ( gameplay.IsValid() )
							gameplay.RefreshMenuSnapshot();

						if ( _stepRetries++ < 240 )
						{
							ThornsMenuJoinFlow.SetProgressMessage( "Loading HUD..." );
							return;
						}
					}

					_stepRetries = 0;
					_deferredStep = 2;
					return;
				case 2:
					bootstrap?.QueueLocalPlayerCosmetics();
					_deferredStep = 3;
					return;
				case 3:
					ThornsLocalPlayerPresentation.EnsureLocalReady( _scene, player );
					player.Components.Get<ThornsFpPresentation>()?.RefreshFromActiveHotbar();
					if ( gameplay.IsValid() )
						ThornsTameSummonUtil.HostSummonOwnedTamesNearPlayer( _scene, gameplay.AccountKey );

					if ( TryCompletePresentation( player, gameplay ) )
						return;

					ThornsMenuJoinFlow.SetProgressMessage( "Entering world..." );
					return;
			}
		}
		catch ( Exception e )
		{
			Log.Error( e, $"[Thorns Terrain] Local player deferred setup failed at step {_deferredStep}." );
			Reset();
		}
	}

	static bool TryCompletePresentation( GameObject player, ThornsPlayerGameplay gameplay )
	{
		ThornsLocalPlayerPresentation.TickAssetBootstrap();

		if ( !ThornsLocalPlayerPresentation.IsBootReady()
		     || !ThornsLocalPlayerPresentation.IsHudReady()
		     || !ThornsLocalPlayerPresentation.IsSnapshotReady() )
		{
			if ( _stepRetries++ >= 300 )
			{
				Log.Warning(
					$"[Thorns Terrain] Local player presentation timed out (boot={ThornsLocalPlayerPresentation.IsBootReady()} " +
					$"hud={ThornsLocalPlayerPresentation.IsHudReady()} snapshot={ThornsLocalPlayerPresentation.IsSnapshotReady()}) — entering anyway." );
			}
			else
			{
				if ( gameplay.IsValid() && !ThornsLocalPlayerPresentation.IsSnapshotReady() )
					gameplay.RefreshMenuSnapshot();

				return false;
			}
		}

		if ( !_cosmeticsWaitStarted )
		{
			ThornsNearbyCosmeticsReadiness.Begin( player );
			_cosmeticsWaitStarted = true;
			return false;
		}

		if ( !ThornsNearbyCosmeticsReadiness.TryComplete( _scene ) )
			return false;

		_cosmeticsWaitStarted = false;
		ThornsAudioWorldService.EnsureForScene( _scene );
		ThornsGameplaySfx.WarmHarvestToolSounds();
		ThornsMenuJoinFlow.CompleteEnterWorld();
		Reset();
		return true;
	}

	static void Reset()
	{
		ThornsNearbyCosmeticsReadiness.Cancel();
		_cosmeticsWaitStarted = false;
		_deferredStep = 0;
		_stepRetries = 0;
		_player = null;
		_scene = null;
		_lastTickRealtime = -1;
	}

	public static void ResetState() => Reset();
}
