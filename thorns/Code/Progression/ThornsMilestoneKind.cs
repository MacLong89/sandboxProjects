namespace Sandbox;

/// <summary>Milestone goal families (THORNS_EVERYTHING_DOCUMENT §10 / §progression milestones).</summary>
public enum ThornsMilestoneKind
{
	Collect,
	Build,
	Kill,
	Tame,
	Craft,
	/// <summary>One-shot host events (<see cref="ThornsMilestoneEventTokens"/>).</summary>
	Event,
}
