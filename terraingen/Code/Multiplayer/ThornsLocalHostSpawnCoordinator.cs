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
	static bool _presentationComplete;

	public static bool IsDeferredPending => _deferredStep > 0;

	public static bool IsHandling( GameObject player ) =>
		player.IsValid() && ReferenceEquals( _player, player );

	public static bool HasCompletedPresentation( GameObject player ) =>
		player.IsValid() && ReferenceEquals( _player, player ) && _presentationComplete;

	public static void Queue( Scene scene, GameObject player )
	{
		if ( !player.IsValid() || scene is null || !scene.IsValid )
			return;

		if ( IsHandling( player ) || HasCompletedPresentation( player ) )
			return;

		ThornsJoinFlowDebug.LogMilestone( $"SpawnCoordinator.Queue player={player.Name}" );

		_scene = scene;
		_player = player;
		_deferredStep = 0;
		_stepRetries = 0;
		_deferredQueuedAt = 0;
		_loggedStall = false;
		_lastTickRealtime = -1;
		_presentationComplete = false;

		try
		{
			ThornsWorldBootGate.EnsureDriver();

			var bootstrap = scene.GetAllComponents<ThornsTerrainBootstrap>().FirstOrDefault();
			if ( bootstrap?.IsWorldApplied != true )
				ThornsWorldBootGate.BeginLocalBoot();

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
			ThornsJoinFlowDebug.JoinInfo( $"SpawnCoordinator step 1 (HUD) — {ThornsJoinFlowDebug.DescribeHud()}" );
			ThornsMenuJoinDriver.NotifyLocalPawnSpawned();
			ThornsMenuJoinHandoff.TryComplete();
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
			ThornsJoinFlowDebug.JoinWarn(
				$"SpawnCoordinator stalled step={_deferredStep} for {_deferredQueuedAt:F1}s retries={_stepRetries} — {ThornsJoinFlowDebug.DescribeHud()}" );
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
						if ( _stepRetries == 1 || _stepRetries % 40 == 0 )
							ThornsJoinFlowDebug.JoinInfo( $"SpawnCoordinator step 1 waiting HUD try={_stepRetries} — {ThornsJoinFlowDebug.DescribeHud()}" );

						ThornsMenuJoinFlow.SetProgressMessage( "Loading HUD..." );
						return;
					}

					if ( !ThornsLocalPlayerPresentation.IsSnapshotReady() )
					{
						if ( gameplay.IsValid() )
							gameplay.RefreshMenuSnapshot();

						if ( _stepRetries++ < 240 )
						{
							if ( _stepRetries == 1 || _stepRetries % 40 == 0 )
								ThornsJoinFlowDebug.JoinInfo( $"SpawnCoordinator step 1 waiting snapshot try={_stepRetries} snapshot={ThornsUiClientState.HasSnapshot}" );

							ThornsMenuJoinFlow.SetProgressMessage( "Loading HUD..." );
							return;
						}
					}

					ThornsJoinFlowDebug.LogMilestone( "SpawnCoordinator step 1 complete (HUD ready)" );
					_stepRetries = 0;
					_deferredStep = 2;
					ThornsSessionEnterController.TryCompleteEnter( "hud" );
					return;
				case 2:
					ThornsJoinFlowDebug.LogMilestone( "SpawnCoordinator step 2 (queue cosmetics)" );
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

	static bool _loggedPresentationTimeout;

	static bool TryCompletePresentation( GameObject player, ThornsPlayerGameplay gameplay )
	{
		ThornsLocalPlayerPresentation.TickAssetBootstrap();

		if ( !ThornsLocalPlayerPresentation.IsBootReady()
		     || !ThornsLocalPlayerPresentation.IsHudReady()
		     || !ThornsLocalPlayerPresentation.IsSnapshotReady() )
		{
			if ( _stepRetries++ >= 300 )
			{
				if ( !_loggedPresentationTimeout )
				{
					_loggedPresentationTimeout = true;
					ThornsJoinFlowDebug.JoinWarn(
						$"SpawnCoordinator presentation timeout — boot={ThornsLocalPlayerPresentation.IsBootReady()} " +
						$"hud={ThornsLocalPlayerPresentation.IsHudReady()} snapshot={ThornsLocalPlayerPresentation.IsSnapshotReady()} " +
						$"{ThornsJoinFlowDebug.DescribeHud()} — entering anyway." );
				}
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
			_cosmeticsWaitStarted = true;
			// Stream nearby cosmetics in the background — do not block session enter or TAB input.
		}

		ThornsAudioWorldService.EnsureForScene( _scene );
		ThornsGameplaySfx.WarmHarvestToolSounds();
		FinishPresentation();
		return true;
	}

	static void FinishPresentation()
	{
		ThornsJoinFlowDebug.LogMilestone( "SpawnCoordinator presentation complete" );
		ThornsNearbyCosmeticsReadiness.Cancel();
		_presentationComplete = true;
		_deferredStep = 0;
		_stepRetries = 0;
		_cosmeticsWaitStarted = false;
		_loggedPresentationTimeout = false;
		_lastTickRealtime = -1;
		ThornsSessionEnterController.TryCompleteEnter( "presentation" );
	}

	static void Reset()
	{
		ThornsNearbyCosmeticsReadiness.Cancel();
		_cosmeticsWaitStarted = false;
		_loggedPresentationTimeout = false;
		_presentationComplete = false;
		_deferredStep = 0;
		_stepRetries = 0;
		_player = null;
		_scene = null;
		_lastTickRealtime = -1;
	}

	public static void ResetState() => Reset();

	/// <summary>Release cosmetics input hold without waiting — session enter owns overlay/input.</summary>
	public static void CancelCosmeticsHold()
	{
		ThornsNearbyCosmeticsReadiness.Cancel();
		_cosmeticsWaitStarted = false;
		if ( _deferredStep >= 3 )
			FinishPresentation();
	}
}
