using Dynasty.Core.Identifiers;

using Dynasty.Core.Identifiers;

namespace Dynasty.Domain.Franchise;

/// <summary>
/// Long-term franchise meta-progress: dynasty score, owner pressure, and challenge tracking.
/// </summary>
public sealed class FranchiseProgressState
{
	public int DynastyScore { get; set; }
	public int OwnerJobSecurity { get; set; } = 80;
	public bool ChallengeCompleted { get; set; }
	public bool ChallengeFailed { get; set; }
	public int DraftStealsFound { get; set; }
	public int PlayoffAppearances { get; set; }
	public bool IsFired { get; set; }
	public HashSet<string> MilestonesReached { get; set; } = new();
	public SeasonObjectiveState SeasonObjective { get; set; }
	public HashSet<string> RecordChaseAlertsSent { get; set; } = new();
	public HashSet<string> NearMissAlertsSent { get; set; } = new();

	/// <summary>First-session guidance — compress preseason, auto-replay, staged tabs.</summary>
	public bool IsFtueActive { get; set; } = true;
	public int HumanGamesSimulated { get; set; }
	public int HumanDraftCeremoniesShown { get; set; }
	public bool HasWonFirstGame { get; set; }
	public string RivalTeamAbbreviation { get; set; } = "";
	public TeamId RivalTeamId { get; set; }
}
