namespace Sandbox;

public sealed class ThornsProcBuildingIdentityMeta
{
	public ThornsProcBuildingType Type { get; init; }
	public ThornsProcBuildingDistrict District { get; set; }
	public bool IsRuinVariant { get; init; }
	public ThornsProcBuildingFacadePlan Facade { get; init; } = new();
	public int LootThemeId { get; init; }

	/// <summary>ASCII interior/floorplan variant index when shell came from <see cref="ThornsInteriorFurnitureFloorplanAscii"/>; otherwise -1.</summary>
	public int InteriorAsciiVariantIndex { get; init; } = -1;

	public string DisplayName
	{
		get
		{
			if ( IsRuinVariant && Type != ThornsProcBuildingType.Ruin
			     && ThornsProcBuildingIdentityRegistry.IsVerticalLandmark( Type ) )
				return $"{ThornsProcBuildingIdentityRegistry.Get( Type ).DisplayName} (Damaged)";

			return ThornsProcBuildingIdentityRegistry.GetDisplayName( Type, IsRuinVariant );
		}
	}
}
