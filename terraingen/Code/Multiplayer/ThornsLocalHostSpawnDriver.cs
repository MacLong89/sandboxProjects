namespace Terraingen.Multiplayer;

using Terraingen.Player;
using Terraingen.UI.Core;
using Terraingen.UI.Menu;

/// <summary>Non-networked tick driver — editor listen-server host pawns may not receive OnUpdate reliably.</summary>
[Title( "Thorns Local Host Spawn Driver" )]
[Category( "Thorns/Multiplayer" )]
public sealed class ThornsLocalHostSpawnDriver : Component
{
	TimeUntil _nextJoinerRepair;

	public static void Ensure()
	{
		ThornsWorldBootGate.EnsureDriver();

		var scene = Game.ActiveScene;
		if ( scene is null || !scene.IsValid )
			return;

		foreach ( var driver in scene.GetAllComponents<ThornsLocalHostSpawnDriver>() )
		{
			if ( driver.IsValid() )
				return;
		}

		var root = scene.GetAllObjects( true ).FirstOrDefault( o => o.Name == "Thorns Screen UI" );
		if ( !root.IsValid() )
		{
			root = new GameObject( true, "Thorns Local Host Spawn Driver" );
			root.SetParent( null );
		}

		if ( !root.Components.Get<ThornsLocalHostSpawnDriver>().IsValid() )
			root.Components.Create<ThornsLocalHostSpawnDriver>();
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying )
			return;

		ThornsLocalHostSpawnCoordinator.TickDeferred();
		TickLocalPawnPresentationRepair();
	}

	void TickLocalPawnPresentationRepair()
	{
		if ( _nextJoinerRepair > 0f )
			return;

		_nextJoinerRepair = 0.25f;
		if ( !ThornsMenuSceneLoader.TryGetGameplayScene( null, out var scene ) || !scene.IsValid )
			scene = Game.ActiveScene;

		if ( scene is null || !scene.IsValid )
			return;

		var player = ThornsJoinLocalPlayer.TryResolve( out var resolvedPlayer, out _ )
			? resolvedPlayer
			: ThornsSceneObserver.FindLocalPlayerObject( scene );
		if ( !player.IsValid() || !ThornsLocalPlayer.IsLocallyControlledPawn( player ) )
			return;

		var controller = player.Components.Get<PlayerController>( FindMode.EverythingInSelf );
		if ( !controller.IsValid() )
			return;

		var locomotion = player.Components.Get<ThornsPlayerLocomotion>() ?? player.Components.Create<ThornsPlayerLocomotion>();
		locomotion.ConfigurePlayerController();
		ThornsPlayerMovementDefaults.Apply( controller );
		ThornsPawnInputIsolation.ApplyForLocalPawn( scene, player );
		ThornsSceneObserver.EnsureLocalPawnOwnsMainCamera( scene, player );
		ThornsPlayerFirstPersonRig.EnsureLocalPresentationCamera( player );

		if ( !ThornsLocalPlayerPresentation.IsFullyReady() )
			ThornsLocalPlayerPresentation.EnsureLocalReady( scene, player );

		ThornsGameplayUiHost.RefreshScreenPanelCamera( scene );

		if ( !ThornsMenuHost.IsOpen )
			ThornsUiCursor.SyncForActiveContext();

		ThornsMenuJoinHandoff.TryComplete();
	}
}
