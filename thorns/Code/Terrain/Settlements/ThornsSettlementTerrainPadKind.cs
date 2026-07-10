namespace Sandbox;

/// <summary>How a terrain pad influences the heightfield (macro vs local building feather).</summary>
public enum ThornsSettlementTerrainPadKind
{
	/// <summary>Broad city/town radial shaping — soft plateau, not a hard slab.</summary>
	MacroSettlement = 0,

	/// <summary>Small footprint feather for foundation blending after macro prep.</summary>
	LocalBuilding = 1,

	/// <summary>Radial hub dome: full flatten at center, weight 0 at hub rim (matches natural ground).</summary>
	HubPlateau = 2
}
