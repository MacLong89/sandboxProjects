using Dynasty.Core.Enums;
using Dynasty.Core.Identifiers;

namespace Dynasty.Domain.Franchise;

public sealed class FranchiseQueuedEvent
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public FranchiseEventType Type { get; set; }
	public int Season { get; set; }
	public int Week { get; set; }
	public string Title { get; set; } = "";
	public string Description { get; set; } = "";
	public bool IsBlocking { get; set; }
	public bool RequiresAction { get; set; }
	public bool IsComplete { get; set; }
	public TeamId TeamId { get; set; }
	public PlayerId PlayerId { get; set; }
	public int SortOrder { get; set; }
}
