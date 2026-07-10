namespace Terraingen.Buildings;

using Terraingen.Economy;

/// <summary>Attaches <see cref="ThornsRadioStation"/> to proc-interior radio furniture.</summary>
public static class ThornsProcRadioShopAttach
{
	public static void TryAttach( GameObject furnitureRoot, string structureDefId )
	{
		if ( furnitureRoot is null || !furnitureRoot.IsValid() )
			return;

		if ( !string.Equals( structureDefId, "radio", StringComparison.OrdinalIgnoreCase ) )
			return;

		if ( !furnitureRoot.Tags.Contains( Terraingen.World.ThornsWorldUseAim.InteriorRadioRootTag ) )
			furnitureRoot.Tags.Add( Terraingen.World.ThornsWorldUseAim.InteriorRadioRootTag );

		var station = furnitureRoot.Components.Get<ThornsRadioStation>()
		              ?? furnitureRoot.Components.Create<ThornsRadioStation>();
		station.InteractionRadius = ThornsBuildingLootWorldService.InteractRange;
	}
}
