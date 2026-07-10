namespace Terraingen.Combat;

using Sandbox.Network;
using Terraingen.Buildings;
using Terraingen.Player;

// Press Use (E) on a placed Research Station to open Ascension research.
[Title( "Thorns Player Research Station Use" )]
[Category( "Player" )]
public sealed class ThornsPlayerResearchStationUse : Component
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

		if ( !Input.Pressed( "Use" ) )
			return;

		var gameplay = Components.Get<ThornsPlayerGameplay>();
		if ( !gameplay.IsValid() || gameplay.HasOpenWorldContainer || gameplay.HasOpenRadioShop || gameplay.HasOpenResearch )
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

		if ( !ThornsPlacedStructureInteraction.TryPickResearchStationInFront( GameObject, out var station )
		     || !station.IsValid()
		     || string.IsNullOrWhiteSpace( station.InstanceKey ) )
			return;

		gameplay.RequestOpenResearchStation( station.InstanceKey );
		ThornsPlayerUseGrabPresentation.PlayPushButton( GameObject );
	}

	public static bool HasResearchStationTargetInFront( GameObject playerRoot ) =>
		ThornsPlacedStructureInteraction.TryPickResearchStationInFront( playerRoot, out var station )
		&& station.IsValid();

	public static bool TryGetPrompt( GameObject playerRoot, out string verbPhrase )
	{
		verbPhrase = "";
		if ( !ThornsPlacedStructureInteraction.TryPickResearchStationInFront( playerRoot, out var station )
		     || !station.IsValid() )
			return false;

		verbPhrase = ThornsPlacedStructureInteraction.PromptVerbForStructure( station.StructureId );
		return !string.IsNullOrWhiteSpace( verbPhrase );
	}

	bool IsLocallyControlled()
	{
		if ( !Networking.IsActive )
			return true;

		return Network.Owner == Connection.Local;
	}
}
