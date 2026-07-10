namespace Terraingen.Combat;

using Sandbox.Network;
using Terraingen.Buildings;
using Terraingen.GameData;
using Terraingen.Player;

// Press Use (E) on a placed or world workbench to open repair/upgrade.
[Title( "Thorns Player Workbench Use" )]
[Category( "Player" )]
public sealed class ThornsPlayerWorkbenchUse : Component
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
		     || ThornsPlayerDoorUse.HasOwnedDoorTargetInFront( GameObject )
		     || ThornsPlayerResearchStationUse.HasResearchStationTargetInFront( GameObject )
		     || ThornsPlayerCampfireUse.HasCampfireTargetInFront( GameObject ) )
			return;

		if ( !TryPickWorkbenchTarget( GameObject, out var workbench, out var isWorldFurniture ) )
			return;

		if ( !isWorldFurniture && ( !workbench.IsValid() || string.IsNullOrWhiteSpace( workbench.InstanceKey ) ) )
			return;

		gameplay.RequestOpenWorkbench( isWorldFurniture ? "" : workbench.InstanceKey, isWorldFurniture );
		ThornsPlayerUseGrabPresentation.PlayPushButton( GameObject );
	}

	public static bool HasWorkbenchTargetInFront( GameObject playerRoot ) =>
		TryPickWorkbenchTarget( playerRoot, out _, out _ );

	public static bool TryGetPrompt( GameObject playerRoot, out string verbPhrase )
	{
		verbPhrase = "";
		if ( !TryPickWorkbenchTarget( playerRoot, out _, out _ ) )
			return false;

		verbPhrase = ThornsPlacedStructureInteraction.PromptVerbForStructure( "workbench" );
		return !string.IsNullOrWhiteSpace( verbPhrase );
	}

	public static bool TryPickWorkbenchTarget(
		GameObject playerRoot,
		out ThornsPlacedBuildStructure structure,
		out bool isWorldFurniture )
	{
		structure = null;
		isWorldFurniture = false;

		if ( !playerRoot.IsValid() )
			return false;

		if ( ThornsPlacedStructureInteraction.TryPickStructureInFront( playerRoot, out structure )
		     && structure.IsValid()
		     && string.Equals( structure.StructureId, "workbench", StringComparison.OrdinalIgnoreCase ) )
			return true;

		if ( !ThornsPlacedStructureInteraction.TryPickCraftStationInFront( playerRoot, out structure, out var station )
		     || station != ThornsCraftStationKind.Workbench )
			return false;

		if ( structure.IsValid() )
			return true;

		isWorldFurniture = true;
		structure = null;
		return true;
	}

	bool IsLocallyControlled()
	{
		if ( !Networking.IsActive )
			return true;

		return Network.Owner == Connection.Local;
	}
}
