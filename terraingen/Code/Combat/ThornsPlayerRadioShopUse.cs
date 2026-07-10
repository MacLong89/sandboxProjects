namespace Terraingen.Combat;

using Sandbox.Network;
using Terraingen.Buildings;
using Terraingen.Economy;
using Terraingen.Player;
using Terraingen.UI.Core;

/// <summary>Press Use (E) on radio stations to open the field supply shop.</summary>
public sealed class ThornsPlayerRadioShopUse : Component
{
	const float RadioLookSearchHoriz = 420f;

	protected override void OnUpdate()
	{
		if ( !IsLocallyControlled() )
			return;

		var gameplay = Components.Get<ThornsPlayerGameplay>();
		if ( !gameplay.IsValid() )
			return;

		if ( gameplay.HasOpenRadioShop )
		{
			if ( Input.Pressed( "Menu" ) || Input.Pressed( "Cancel" ) )
				gameplay.RequestCloseRadioShop();

			return;
		}

		if ( !Input.Pressed( "Use" ) )
			return;

		if ( UiBlocksRadioInteraction() )
			return;

		if ( _tamingHasTarget() )
			return;

		if ( Components.Get<ThornsPlayerMountUse>()?.HasMountTargetInFront() == true )
			return;

		if ( Components.Get<ThornsPlayerMountController>()?.IsMounted == true )
			return;

		if ( ThornsPlayerContainerUse.HasOpenableTargetInFront( GameObject )
		     || ThornsPlayerDoorUse.HasOwnedDoorTargetInFront( GameObject ) )
			return;

		if ( !TryResolveStation( out var stationId ) )
			return;

		gameplay.RequestOpenRadioShop( stationId );
		ThornsPlayerUseGrabPresentation.PlayPushButton( GameObject );
	}

	bool _tamingHasTarget()
	{
		var taming = Components.Get<ThornsPlayerAnimalTaming>();
		return taming is not null && taming.HasTameTargetInFront();
	}

	public static bool HasRadioShopTargetInFront( GameObject playerRoot ) =>
		TryResolveStation( playerRoot, out _ );

	static bool TryResolveStation( GameObject playerRoot, out Guid stationId )
	{
		stationId = Guid.Empty;
		if ( playerRoot is null || !playerRoot.IsValid() )
			return false;

		var station = ThornsRadioStation.FindBestUnderAimForPawn( playerRoot.Scene, playerRoot, RadioLookSearchHoriz );
		if ( !station.IsValid() || station.StationId == Guid.Empty )
			return false;

		stationId = station.StationId;
		return true;
	}

	bool TryResolveStation( out Guid stationId ) => TryResolveStation( GameObject, out stationId );

	static bool UiBlocksRadioInteraction()
	{
		if ( ThornsMenuHost.IsOpen || ThornsMenuHost.IsWorldContainerOpen || ThornsMenuHost.IsRadioShopOpen || ThornsMenuHost.IsResearchOpen )
			return true;

		var build = ThornsPlayerBuildingController.Local;
		if ( build?.IsHotbarPlaceModeActive == true )
			return true;

		return false;
	}

	bool IsLocallyControlled()
	{
		if ( !Networking.IsActive )
			return true;

		return Network.Owner == Connection.Local;
	}
}
