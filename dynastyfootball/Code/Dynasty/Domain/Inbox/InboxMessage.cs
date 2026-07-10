using Dynasty.Core.Enums;
using Dynasty.Core.Identifiers;

namespace Dynasty.Domain.Inbox;

public sealed class InboxMessage
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public int Season { get; set; }
	public int Week { get; set; }
	public InboxCategory Category { get; set; }
	public InboxPriority Priority { get; set; } = InboxPriority.Normal;
	public string Subject { get; set; } = "";
	public string Body { get; set; } = "";
	public bool RequiresAction { get; set; }
	public bool IsRead { get; set; }
	public bool IsResolved { get; set; }
	public TeamId TeamId { get; set; }
	public PlayerId PlayerId { get; set; }
	public string NavigateTab { get; set; } = "";
	public FranchiseEventType? LinkedEventType { get; set; }
	public DateTime CreatedUtc { get; set; }
}
