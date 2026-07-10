namespace Sandbox;

/// <summary>Recognizable procedural building archetypes (not fully random silhouettes).</summary>
public enum ThornsProcBuildingType
{
	House,
	Ruin,
	Warehouse,
	MilitaryComplex,
	Cabin,
	Store,
	Apartment,
	Factory,
	Barn,
	RadioOutpost,
	/// <summary>5×5×5 dense residential tower — rooftop PvP landmark.</summary>
	ApartmentTower,
	/// <summary>6×6×8 rare skyline landmark — extreme vertical PvP.</summary>
	Skyscraper,
	/// <summary>6×5×4 office block — open rotational urban fights.</summary>
	OfficeBuilding
}

/// <summary>World-gen district influencing type weights and spacing.</summary>
public enum ThornsProcBuildingDistrict
{
	Residential,
	Industrial,
	Military,
	Rural,
	Commercial,
	Mixed
}
