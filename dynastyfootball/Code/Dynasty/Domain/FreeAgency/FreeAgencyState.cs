using Dynasty.Core.Identifiers;
using Dynasty.Domain.Contracts;

namespace Dynasty.Domain.FreeAgency;

public sealed class FreeAgencyState
{
	public bool IsOpen { get; set; }
	public List<PlayerId> AvailablePlayers { get; set; } = new();
	public List<ContractOffer> PendingOffers { get; set; } = new();
	public List<Guid> CompletedOfferIds { get; set; } = new();
}
