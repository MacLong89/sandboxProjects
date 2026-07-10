namespace Terraingen.GameData;

public enum ThornsJournalSection : byte
{
	Goals,
	Discoveries,
	Events,
	Achievements,
	VictoryPaths
}

/// <summary>Survivor Journey groupings shown in the journal sidebar.</summary>
public enum ThornsJourneyCategory : byte
{
	Survival,
	Exploration,
	Combat,
	Building,
	Taming,
	World
}

public enum ThornsGoalState : byte
{
	Locked,
	Active,
	Completed
}

public enum ThornsMilestoneType : byte
{
	Collect,
	Build,
	Kill,
	Tame,
	Craft,
	Event
}

/// <summary>Collect goals that require holding items in inventory vs lifetime gathered.</summary>
public enum ThornsCollectTrackMode : byte
{
	Lifetime,
	HoldInInventory
}

public sealed class ThornsJournalGoalDefinition
{
	public string Id { get; set; } = "";
	// Short journal headline (list / toast).
	public string Title { get; set; } = "";
	// First-person survivor journal entry (detail panel).
	public string JournalEntry { get; set; } = "";
	// Legacy alias; mirrors JournalEntry when unset.
	public string Description { get; set; } = "";
	// Objective guidance shown on HUD (not tutorial commands).
	public string RequirementText { get; set; } = "";
	public string ImagePath { get; set; } = "";
	public int SortOrder { get; set; }
	public ThornsJourneyCategory JourneyCategory { get; set; } = ThornsJourneyCategory.Survival;
	public string PrerequisiteGoalId { get; set; } = "";
	// Also unlock when this discovery id is logged (optional).
	public string UnlockOnDiscoveryId { get; set; } = "";
	// Locked goals are hidden from the journal list when true.
	public bool HideWhenLocked { get; set; } = true;
	// Automatically pin to HUD while active (onboarding chain).
	public bool AutoPinUntilComplete { get; set; }
	public ThornsMilestoneType MilestoneType { get; set; }
	public string TargetKey { get; set; } = "";
	public int TargetCount { get; set; } = 1;
	public int XpReward { get; set; }
	public ThornsCollectTrackMode CollectMode { get; set; } = ThornsCollectTrackMode.Lifetime;
	public List<ThornsJournalRewardDto> Rewards { get; set; } = new();
	public List<ThornsJournalTaskDefinition> Tasks { get; set; } = new();
}

public sealed class ThornsJournalTaskDefinition
{
	public string Id { get; set; } = "";
	public string Label { get; set; } = "";
	public int TargetCount { get; set; } = 1;
}

public sealed class ThornsJournalRewardDto
{
	public string Label { get; set; } = "";
	public string IconPath { get; set; } = "";
	public string Kind { get; set; } = "";
}

public sealed class ThornsJournalGoalProgressDto
{
	public string GoalId { get; set; } = "";
	public ThornsGoalState State { get; set; }
	public List<ThornsJournalTaskProgressDto> Tasks { get; set; } = new();
}

public sealed class ThornsJournalTaskProgressDto
{
	public string TaskId { get; set; } = "";
	public int Current { get; set; }
	public int Target { get; set; }
}

public sealed class ThornsDiscoveryEntryDto
{
	public string Id { get; set; } = "";
	public string Title { get; set; } = "";
	public bool Discovered { get; set; }
	public string Category { get; set; } = "";
	public string IconPath { get; set; } = "";
}

public sealed class ThornsJournalSnapshotDto
{
	public const int CurrentJourneyContentVersion = 4;

	// Bumped when journey chains/copy change; triggers host migration on load.
	public int JourneyContentVersion { get; set; }

	public ThornsJournalSection ActiveSection { get; set; } = ThornsJournalSection.Goals;
	public string SelectedGoalId { get; set; } = "";
	public string SelectedDiscoveryId { get; set; } = "";
	// Goal shown on the gameplay HUD objectives tracker (must be Active).
	public string HudPinnedGoalId { get; set; } = "";
	public List<ThornsJournalGoalProgressDto> Goals { get; set; } = new();
	public List<ThornsDiscoveryEntryDto> Discoveries { get; set; } = new();
	// Host-tracked world events (see ThornsJournalEventCatalog + ThornsJournalProgress).
	public List<string> CompletedEventIds { get; set; } = new();
	// Goal IDs completed — display via ThornsJournalProgress.AchievementDisplayName.
	public List<string> UnlockedAchievementIds { get; set; } = new();
}
