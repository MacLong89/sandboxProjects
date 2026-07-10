using Dynasty.Core.Identifiers;

namespace Dynasty.Domain.Trades;

public sealed class PendingTradeOffer
{
	public Guid OfferId { get; set; } = Guid.NewGuid();
	public TradeProposal Proposal { get; set; } = new();
	public TeamId FromTeamId { get; set; }
	public TeamId ToTeamId { get; set; }
	public int Season { get; set; }
	public int Week { get; set; }
	public string Summary { get; set; } = "";
}
