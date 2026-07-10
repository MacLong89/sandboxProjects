namespace Sandbox;

/// <summary>Behavior states for humanoid bandit AI — independent from locomotion animation.</summary>
public enum ThornsBanditAiState
{
	Idle,
	Patrol,
	Roam,
	Investigate,
	Alert,
	SeekCover,
	Attack,
	Chase,
	Search,
	ReturnHome,
	Flee,
	Dead,

	// Legacy aliases (same ordinal intent as pre-refactor brain)
	Wander = Roam,
}
