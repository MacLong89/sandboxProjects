namespace Sandbox;

/// <summary>Wildlife AI states (THORNS_EVERYTHING_DOCUMENT §8 — explicit machine).</summary>
public enum ThornsWildlifeAiState
{
	Idle,
	Wander,
	/// <summary>Tamed owner follow — distinct from wild <see cref="Wander"/> so assist/flee/hunt never overlap follow locomotion.</summary>
	Follow,
	Flee,
	Hunt,
	Attack,
	ReturnToLeash,

	// --- Modular state machine v2 (append-only for sync ordinal stability) ---

	/// <summary>Noticed threat — evaluate before flee/hunt.</summary>
	Alert,
	/// <summary>Committed pursuit sprint (predator).</summary>
	Chase,
	/// <summary>Pack/herd spacing behind a leader.</summary>
	FollowLeader,
	Dead,
	/// <summary>Tamed: hold assigned position.</summary>
	Stay,
	/// <summary>Physically leashed to anchor.</summary>
	Leashed,
	/// <summary>Tamed: protect owner.</summary>
	GuardOwner,
	/// <summary>Tamed: protect base radius.</summary>
	GuardArea,
	/// <summary>Tamed: waypoint patrol.</summary>
	Patrol,
	/// <summary>Tamed: gather prey for owner.</summary>
	HuntForOwner,
	/// <summary>Rider-controlled — AI disabled.</summary>
	Mounted,

	/// <summary>Predator slow approach before commit (panther / cautious wolf).</summary>
	Stalk,
}
