namespace Sandbox;

/// <summary>Which victims count toward a <see cref="ThornsMilestoneKind.Kill"/> milestone (host classification only).</summary>
public enum ThornsMilestoneKillFilter
{
	/// <summary>Victim has <see cref="ThornsBanditBrain"/> (humanoid AI).</summary>
	Bandit,

	/// <summary>No <see cref="ThornsPawn"/> on victim root (AI / wildlife / props with health — not another player).</summary>
	NonPlayer,

	/// <summary>Any lethal contribution attributed to the killer (includes PvP if wired).</summary>
	Any,

	/// <summary>Wild creatures only (excludes players and bandit NPCs).</summary>
	Wildlife,
}
