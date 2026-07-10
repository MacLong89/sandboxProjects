namespace Terraingen.GameData;

public enum ThornsMapMarkerKind : byte
{
	You,
	GuildMember,
	Home,
	LastDeath,
	Airdrop,
	BloomSeed,
	SpecialEvent,
	Boss,
	Settlement,
	Town,
	Metropolis,
	City,
	Suburb,
	RuralPoi,
	MilitaryPoi,
	CabinSite,
	Farmstead,
	NpcGuildOutpost,
	Goal,
	CustomWaypoint
}

public sealed class ThornsMapMarkerDto
{
	public string Id { get; set; } = "";
	public ThornsMapMarkerKind Kind { get; set; }
	public float WorldX { get; set; }
	public float WorldY { get; set; }
	public string Label { get; set; } = "";
}

public sealed class ThornsMapSnapshotDto
{
	public string MapTexturePath { get; set; } = "";
	public float WorldMinX { get; set; }
	public float WorldMinY { get; set; }
	public float WorldMaxX { get; set; }
	public float WorldMaxY { get; set; }
	public List<ThornsMapMarkerDto> Markers { get; set; } = new();
}
