namespace Sandbox;

/// <summary>Atmospheric music context — higher <see cref="ThornsMusicCatalog.GetStatePriority"/> wins when multiple contexts apply.</summary>
public enum ThornsMusicState : byte
{
	None = 0,
	CalmExploration = 1,
	NightExploration = 2,
	Rain = 3,
	CampfireSafeZone = 4,
	PostCombatReflection = 5,
	BloomCorruption = 6,
	Storm = 7,
	TensionDanger = 8,
	MenuTheme = 9
}

[Flags]
public enum ThornsMusicSuppressionFlags : uint
{
	None = 0,
	RecentDamage = 1 << 0,
	NearbyGunfire = 1 << 1,
	HostileAwareness = 1 << 2,
	WorldEvent = 1 << 3,
	Dead = 1 << 4,
	SprintCombatPace = 1 << 5,
	SpawnGrace = 1 << 6,
	GlobalSilenceCooldown = 1 << 7,
	TrackStillPlaying = 1 << 8,
	CombatStress = 1 << 9
}

[Flags]
public enum ThornsMusicBlockReason : uint
{
	None = 0,
	Suppressed = 1 << 0,
	NoEligibleState = 1 << 1,
	NoTracks = 1 << 2,
	Cooldown = 1 << 3,
	WaitingPostCombatDelay = 1 << 4,
	ClientModalUi = 1 << 5,
	ClientCampfireOnly = 1 << 6,
	ProbabilityGate = 1 << 7
}
