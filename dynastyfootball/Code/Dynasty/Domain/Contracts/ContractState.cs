using Dynasty.Core.Identifiers;

namespace Dynasty.Domain.Contracts;

public sealed class ContractState
{
	public int YearsRemaining { get; set; }
	public int AnnualSalary { get; set; }
	public int GuaranteedMoney { get; set; }
	public int SigningBonus { get; set; }
	public bool IsFranchiseTagged { get; set; }
	public TeamId SignedWithTeamId { get; set; }
}

public sealed class ContractOffer
{
	public Guid OfferId { get; set; } = Guid.NewGuid();
	public PlayerId PlayerId { get; set; }
	public TeamId TeamId { get; set; }
	public int Years { get; set; }
	public int AnnualSalary { get; set; }
	public int GuaranteedMoney { get; set; }
	public DateTime ExpiresUtc { get; set; }
	public bool Accepted { get; set; }
}
