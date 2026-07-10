namespace Terraingen.Player;

using Sandbox;
using Terraingen.UI.Core;

/// <summary>Ensures only the locally owned pawn receives input and main-camera promotion in multiplayer.</summary>
public static class ThornsPawnInputIsolation
{
	public static void ApplyForLocalPawn( Scene scene, GameObject localPlayer )
	{
		if ( scene is null || !scene.IsValid() || !localPlayer.IsValid() )
			return;

		if ( !ThornsLocalPlayer.IsLocallyControlledPawn( localPlayer ) )
			return;

		foreach ( var controller in scene.GetAllComponents<PlayerController>() )
		{
			if ( !controller.IsValid() )
				continue;

			var root = ThornsLocalPlayer.ResolvePawnRoot( controller.GameObject );
			var allow = root.IsValid() && root == localPlayer && !ThornsUiInputGate.BlocksGameplayInput;

			controller.UseInputControls = allow;
			controller.UseCameraControls = allow;
			controller.UseLookControls = allow;

			if ( !allow )
				SuppressPresentationCameras( root );
		}
	}

	public static void SuppressPresentationCameras( GameObject pawnRoot )
	{
		if ( !pawnRoot.IsValid() )
			return;

		foreach ( var cam in pawnRoot.Components.GetAll<CameraComponent>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( cam is null || !cam.IsValid() )
				continue;

			cam.Enabled = false;
			cam.IsMainCamera = false;
		}
	}
}
