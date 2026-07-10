using Dynasty.Core.Enums;
using Dynasty.Core.Identifiers;
using Dynasty.Domain.Players;

namespace Dynasty.Domain.Draft;

public sealed class DraftState
{
	public int Season { get; set; }
	public DraftType Type { get; set; } = DraftType.Rookie;
	public bool IsActive { get; set; }
	public int CurrentRound { get; set; } = 1;
	public int CurrentPickIndex { get; set; }
	public int OnClockOverallPick { get; set; }
	public DateTime OnClockSinceUtc { get; set; }
	public List<DraftOrderEntry> Order { get; set; } = new();
	public List<ProspectState> Prospects { get; set; } = new();
	public List<DraftHistoryEntry> History { get; set; } = new();
	public Dictionary<TeamId, List<PlayerId>> TeamDraftBoards { get; set; } = new();
}

public sealed class DraftOrderEntry
{
	public int OverallPick { get; set; }
	public int Round { get; set; }
	public int PickInRound { get; set; }
	public TeamId TeamId { get; set; }
	public DraftPickId PickAssetId { get; set; }
	public bool IsComplete { get; set; }
}

public sealed class ProspectState
{
	public PlayerId Id { get; set; }
	public PlayerState Player { get; set; } = new();
	public int ConsensusRank { get; set; }
	public bool IsDrafted { get; set; }
	public TeamId DraftedByTeamId { get; set; }
}

public sealed class DraftHistoryEntry
{
	public int Season { get; set; }
	public int Round { get; set; }
	public int OverallPick { get; set; }
	public TeamId TeamId { get; set; }
	public PlayerId PlayerId { get; set; }
}
