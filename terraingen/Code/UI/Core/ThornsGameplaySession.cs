namespace Terraingen.UI.Core;

using Terraingen;
using Terraingen.Multiplayer;
using Terraingen.Player;
using Terraingen.UI;
using Terraingen.UI.Menu;

/// <summary>Gameplay scene boot — unlocks input and ensures HUD/menu hosts exist.</summary>
public static class ThornsGameplaySession
{
	public static void PrepareScene()
	{
		UiRevisionBus.ResetMenuListeners();
		ThornsHitmarkerState.Reset();
		ThornsDamageFlashState.Reset();
		ThornsUnderwaterViewState.Reset();
		ThornsMenuHost.ForceGameplayState();
		ThornsMenuJoinFlow.CompleteEnterWorld();
	}

	public static void EnsureLocalPlayerControl( bool skipCameraReclaim = false )
	{
		var scene = Game.ActiveScene;
		if ( scene is null || !scene.IsValid )
		{
			Log.Warning( "[Thorns Player] EnsureLocalPlayerControl: no active scene." );
			return;
		}

		var player = ResolveLocalPlayer( scene );
		if ( !player.IsValid() )
		{
			Log.Warning( "[Thorns Player] EnsureLocalPlayerControl: no local player found." );
			return;
		}

		if ( !skipCameraReclaim )
			ThornsSceneObserver.FocusLocalPlayer( scene, player );

		var controller = player.Components.Get<PlayerController>( FindMode.EverythingInSelf );
		if ( !controller.IsValid() )
		{
			Log.Warning( "[Thorns Player] EnsureLocalPlayerControl: missing PlayerController." );
			return;
		}

		var locomotion = player.Components.Get<ThornsPlayerLocomotion>() ?? player.Components.Create<ThornsPlayerLocomotion>();
		locomotion.ConfigurePlayerController();
		ThornsPlayerMovementDefaults.Apply( controller );
		ThornsPawnInputIsolation.ApplyForLocalPawn( scene, player );

		ThornsPlayerFirstPersonRig.EnsureLocalPresentationCamera( player );
		ThornsUiCursor.SyncForActiveContext();
	}

	static GameObject ResolveLocalPlayer( Scene scene )
	{
		if ( ThornsPlayerGameplay.Local.IsValid() )
			return ThornsPlayerGameplay.Local.GameObject;

		foreach ( var session in scene.GetAllComponents<ThornsPlayerSession>() )
		{
			if ( !session.IsValid() || !ThornsLocalPlayer.IsLocalConnectionSession( session ) )
				continue;

			if ( session.GameObject.IsValid() )
				return session.GameObject;
		}

		return ThornsSceneObserver.FindLocalPlayerObject( scene );
	}
}
