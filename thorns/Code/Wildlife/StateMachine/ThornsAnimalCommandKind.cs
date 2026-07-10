namespace Sandbox;

/// <summary>Owner commands for tamed animals — may change state and/or <see cref="ThornsAnimalBehaviorMode"/>.</summary>
public enum ThornsAnimalCommandKind
{
	Follow,
	Stay,
	GuardOwner,
	GuardArea,
	Patrol,
	Passive,
	Defensive,
	Aggressive,
	Hunt,
}
