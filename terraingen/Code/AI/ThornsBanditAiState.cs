namespace Terraingen.AI;

/// <summary>Bandit finite state machine.</summary>
public enum ThornsBanditAiState
{
	Patrol,
	Investigate,
	Combat,
	Chase,
	Reposition,
	Retreat,
	Dead,
}

public enum ThornsBanditSkillLevel
{
	Poor,
	Average,
	Veteran,
}
