namespace Terraingen.Combat;

using Sandbox.Network;
using Terraingen.Buildings;
using Terraingen.Player;

// Press Use (E) on a placed campfire to open smelting.
[Title( "Thorns Player Campfire Use" )]
[Category( "Player" )]
public sealed class ThornsPlayerCampfireUse : Component
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
		if ( !gameplay.IsValid()
		     || gameplay.HasOpenWorldContainer
		     || gameplay.HasOpenRadioShop
		     || gameplay.HasOpenResearch
		     || gameplay.HasOpenCampfire
		     || gameplay.HasOpenWorkbench )
			return;

		if ( _taming is not null && _taming.HasTameTargetInFront() )
			return;

		if ( Components.Get<ThornsPlayerMountUse>()?.HasMountTargetInFront() == true )
			return;

		if ( Components.Get<ThornsPlayerMountController>()?.IsMounted == true )
			return;

		if ( ThornsPlayerRadioShopUse.HasRadioShopTargetInFront( GameObject )
		     || ThornsPlayerContainerUse.HasOpenableTargetInFront( GameObject )
		     || ThornsPlayerDoorUse.HasOwnedDoorTargetInFront( GameObject ) )
			return;

		if ( ThornsPlayerWorkbenchUse.HasWorkbenchTargetInFront( GameObject ) )
			return;

		if ( !TryPickCampfireInFront( GameObject, out var campfire )
		     || !campfire.IsValid()
		     || string.IsNullOrWhiteSpace( campfire.InstanceKey ) )
			return;

		gameplay.RequestOpenCampfire( campfire.InstanceKey );
		ThornsPlayerUseGrabPresentation.PlayPushButton( GameObject );
	}

	public static bool HasCampfireTargetInFront( GameObject playerRoot ) =>
		TryPickCampfireInFront( playerRoot, out var campfire ) && campfire.IsValid();

	public static bool TryGetPrompt( GameObject playerRoot, out string verbPhrase )
	{
		verbPhrase = "";
		if ( !TryPickCampfireInFront( playerRoot, out var campfire ) || !campfire.IsValid() )
			return false;

		verbPhrase = ThornsPlacedStructureInteraction.PromptVerbForStructure( "campfire" );
		return !string.IsNullOrWhiteSpace( verbPhrase );
	}

	static bool TryPickCampfireInFront( GameObject playerRoot, out ThornsPlacedBuildStructure structure )
	{
		structure = null;
		if ( !ThornsPlacedStructureInteraction.TryPickStructureInFront( playerRoot, out structure )
		     || !structure.IsValid() )
			return false;

		return string.Equals( structure.StructureId, "campfire", StringComparison.OrdinalIgnoreCase );
	}

	bool IsLocallyControlled()
	{
		if ( !Networking.IsActive )
			return true;

		return Network.Owner == Connection.Local;
	}
}
