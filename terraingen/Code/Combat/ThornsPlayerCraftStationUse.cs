namespace Terraingen.Combat;

using Sandbox.Network;
using Terraingen.Buildings;
using Terraingen.GameData;
using Terraingen.Player;
using Terraingen.UI.Core;

// Press Use (E) on a placed or world workbench to open crafting at that station.
[Title( "Thorns Player Craft Station Use" )]
[Category( "Player" )]
public sealed class ThornsPlayerCraftStationUse : Component
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

		var gameplay = Components.Get<ThornsPlayerGameplay>();
		if ( gameplay.IsValid() )
		{
			if ( ThornsPlacedStructureInteraction.TryPickCraftStationInFront( GameObject, out _, out var nearest ) )
				gameplay.SetNearestStation( nearest );
			else
				gameplay.SetNearestStation( ThornsCraftStationKind.Hand );
		}

		if ( !Input.Pressed( "Use" ) && !Input.Pressed( "use" ) )
			return;

		if ( !gameplay.IsValid()
		     || gameplay.HasOpenWorldContainer
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
		     || ThornsPlayerCampfireUse.HasCampfireTargetInFront( GameObject )
		     || ThornsPlayerWorkbenchUse.HasWorkbenchTargetInFront( GameObject ) )
			return;

		if ( !ThornsPlacedStructureInteraction.TryPickCraftStationInFront( GameObject, out _, out var station ) )
			return;

		if ( station == ThornsCraftStationKind.Campfire
		     || station == ThornsCraftStationKind.Workbench )
			return;

		ThornsMenuHost.Instance?.OpenCraftStation( station );
		ThornsPlayerUseGrabPresentation.PlayPushButton( GameObject );
	}

	public static bool HasCraftStationInFront( GameObject playerRoot ) =>
		TryGetCraftStationPrompt( playerRoot, out _ );

	public static bool TryGetCraftStationPrompt( GameObject playerRoot, out string verbPhrase )
	{
		verbPhrase = "";
		if ( !ThornsPlacedStructureInteraction.TryPickCraftStationInFront( playerRoot, out var structure, out var station )
		     || station == ThornsCraftStationKind.Hand
		     || station == ThornsCraftStationKind.Campfire
		     || station == ThornsCraftStationKind.Workbench )
			return false;

		verbPhrase = structure.IsValid()
			? ThornsPlacedStructureInteraction.PromptVerbForStructure( structure.StructureId )
			: station switch
			{
				ThornsCraftStationKind.Workbench => "Use Workbench",
				_ => "Use Craft Station"
			};
		return !string.IsNullOrWhiteSpace( verbPhrase );
	}

	bool IsLocallyControlled()
	{
		if ( !Networking.IsActive )
			return true;

		return Network.Owner == Connection.Local;
	}
}
