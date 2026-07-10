namespace Terraingen.Animals;

/// <summary>Shared NPC contract for wildlife, bandits, and bosses.</summary>
public interface IThornsNpcBrain
{
	ThornsNpcBrainKind Kind { get; }
	bool IsDead { get; }
	bool IsValidBrain { get; }
	Vector3 WorldPosition { get; }
	float BodyRadius { get; }
	ThornsNpcLodTier LodTier { get; }
	void HostTickAi( float delta, ThornsNpcLodTier lodTier );
}

public enum ThornsNpcBrainKind : byte
{
	Wildlife,
	Bandit,
	Boss
}

/// <summary>Legacy bandit phase enum — superseded by full AI state machine in Terraingen.AI.</summary>
public enum ThornsBanditCombatPhase : byte
{
	Patrol,
	Investigate,
	BurstFire,
	Reposition,
	Retreat,
	Cover
}
