using Dynasty.Core.Enums;
using Dynasty.Core.Identifiers;

namespace Dynasty.Domain.Trades;

public sealed class TradeProposal
{
	public Guid TradeId { get; set; } = Guid.NewGuid();
	public List<TradeParticipant> Participants { get; set; } = new();
	public TradeStatus Status { get; set; } = TradeStatus.Pending;
	public DateTime CreatedUtc { get; set; }
	public TeamId InitiatingTeamId { get; set; }
}

public enum TradeStatus
{
	Pending,
	Accepted,
	Rejected,
	Expired,
	Vetoed
}

public sealed class TradeParticipant
{
	public TeamId TeamId { get; set; }
	public List<TradeAsset> Sending { get; set; } = new();
	public List<TradeAsset> Receiving { get; set; } = new();
}

public sealed class TradeAsset
{
	public TradeAssetType Type { get; set; }
	public PlayerId PlayerId { get; set; }
	public DraftPickId PickId { get; set; }
	public int CashAmount { get; set; }
}
