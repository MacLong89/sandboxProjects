namespace Sandbox;

/// <summary>Bandit archetype — behavior differs via <see cref="ThornsBanditArchetypeConfig"/>, not separate AI code paths.</summary>
public enum ThornsBanditType
{
	/// <summary>Opportunistic roamers (legacy wanderer spawns).</summary>
	Scavenger,
	CityDefender,
	AirdropDefender,
}
