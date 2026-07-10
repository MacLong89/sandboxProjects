using Dynasty.Core.Enums;
using Dynasty.Domain.Inbox;
using Dynasty.Domain.League;

namespace Dynasty.UI.ViewModels;

public sealed class InboxViewModel
{
	public IReadOnlyList<InboxRow> Messages { get; init; } = Array.Empty<InboxRow>();
	public int UnreadCount { get; init; }
	public int ActionRequiredCount { get; init; }

	public static InboxViewModel From( LeagueState state )
	{
		if ( state == null )
			return new InboxViewModel();

		var rows = state.Inbox
			.Where( m => !m.IsResolved )
			.Select( InboxRow.From )
			.ToList();

		return new InboxViewModel
		{
			Messages = rows,
			UnreadCount = state.Inbox.Count( m => !m.IsRead && !m.IsResolved ),
			ActionRequiredCount = state.Inbox.Count( m => m.RequiresAction && !m.IsResolved )
		};
	}
}

public sealed class InboxRow
{
	public Guid Id { get; init; }
	public string Category { get; init; }
	public string Priority { get; init; }
	public string Subject { get; init; }
	public string Preview { get; init; }
	public bool RequiresAction { get; init; }
	public bool IsRead { get; init; }
	public string WeekLabel { get; init; }

	public static InboxRow From( InboxMessage msg ) => new()
	{
		Id = msg.Id,
		Category = msg.Category.ToString(),
		Priority = msg.Priority.ToString(),
		Subject = msg.Subject,
		Preview = msg.Body,
		RequiresAction = msg.RequiresAction,
		IsRead = msg.IsRead,
		WeekLabel = $"S{msg.Season} W{msg.Week}"
	};
}
