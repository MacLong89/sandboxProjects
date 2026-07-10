namespace Terraingen.Combat;

using Sandbox.Network;
using Terraingen.Buildings;
using Terraingen.Player;

/// <summary>Press Use (E) on your placed door frame to open or close the hinged panel.</summary>
[Title( "Thorns Player Door Use" )]
[Category( "Player" )]
public sealed class ThornsPlayerDoorUse : Component
{
	ThornsPlayerAnimalTaming _taming;

	protected override void OnAwake()
	{
		_taming = Components.Get<ThornsPlayerAnimalTaming>();
	}

	protected override void OnUpdate()
	{
		if ( !IsLocallyControlled() )
			return;

		if ( !Input.Pressed( "Use" ) && !Input.Pressed( "use" ) )
			return;

		var gameplay = Components.Get<ThornsPlayerGameplay>();
		if ( gameplay.IsValid() && gameplay.HasOpenWorldContainer )
			return;

		if ( _taming is not null && _taming.HasTameTargetInFront() )
			return;

		if ( Components.Get<ThornsPlayerMountUse>()?.HasMountTargetInFront() == true )
			return;

		if ( Components.Get<ThornsPlayerMountController>()?.IsMounted == true )
			return;

		TryHandleUsePress( GameObject );
	}

	public static bool TryHandleUsePress( GameObject playerRoot )
	{
		if ( !TryFindOwnedDoorUnderAim( playerRoot, out var door ) || !door.IsValid() )
			return false;

		door.RequestToggleFromLocalOwner();
		ThornsPlayerUseGrabPresentation.PlayPushButton( playerRoot );
		return true;
	}

	public static bool HasOwnedDoorTargetInFront( GameObject playerRoot ) =>
		TryFindOwnedDoorUnderAim( playerRoot, out _ );

	public static bool TryGetPrompt( GameObject playerRoot, out string verbPhrase )
	{
		verbPhrase = "";
		if ( !TryFindOwnedDoorUnderAim( playerRoot, out var door ) || !door.IsValid() )
			return false;

		verbPhrase = door.DoorOpenSync ? "Close Door" : "Open Door";
		return true;
	}

	static bool TryFindOwnedDoorUnderAim( GameObject playerRoot, out ThornsPlayerDoor door ) =>
		ThornsPlayerDoor.TryFindBestUnderAimForOwner( playerRoot, ThornsPlayerDoor.InteractionRange, out door );

	bool IsLocallyControlled()
	{
		if ( !Networking.IsActive )
			return true;

		return Network.Owner == Connection.Local;
	}
}
