using Dynasty.Core.Enums;
using Dynasty.Core.Identifiers;
using Dynasty.Domain.Inbox;
using Dynasty.Domain.League;

namespace Dynasty.UI.ViewModels;

public sealed class InboxDetailViewModel
{
	public Guid Id { get; init; }
	public string Category { get; init; } = "";
	public string Priority { get; init; } = "";
	public string Subject { get; init; } = "";
	public string Body { get; init; } = "";
	public string WeekLabel { get; init; } = "";
	public bool RequiresAction { get; init; }
	public bool IsRead { get; init; }
	public PlayerId PlayerId { get; init; }
	public TeamId TeamId { get; init; }
	public string SuggestedTab { get; init; } = "";
	public string ActionHint { get; init; } = "";

	public static InboxDetailViewModel From( LeagueState state, Guid messageId )
	{
		if ( state == null )
			return null;

		var msg = state.Inbox.FirstOrDefault( m => m.Id == messageId );
		if ( msg == null )
			return null;

		var tab = !string.IsNullOrEmpty( msg.NavigateTab )
			? msg.NavigateTab
			: CategoryToTab( msg.Category );

		var hint = msg.Category switch
		{
			InboxCategory.Draft => "Head to the Draft room to make your pick.",
			InboxCategory.Injury => "Review the player profile and adjust your depth chart.",
			InboxCategory.Contract => "Open Free Agency or extend contracts on Team.",
			InboxCategory.Roster => "Check roster morale, lineup, or playing time.",
			InboxCategory.Trade => "Open Trade Center to negotiate a deal.",
			InboxCategory.Coaching => "Review coaching staff changes in Team or News.",
			InboxCategory.League => "Follow the guided next step for this phase.",
			InboxCategory.General => "Open Facilities to invest in your franchise.",
			_ => "Tap Take Action to go to the right screen."
		};

		return new InboxDetailViewModel
		{
			Id = msg.Id,
			Category = msg.Category.ToString(),
			Priority = msg.Priority.ToString(),
			Subject = msg.Subject,
			Body = msg.Body,
			WeekLabel = $"Season {msg.Season} · Week {msg.Week}",
			RequiresAction = msg.RequiresAction,
			IsRead = msg.IsRead,
			PlayerId = msg.PlayerId,
			TeamId = msg.TeamId,
			SuggestedTab = tab,
			ActionHint = hint
		};
	}

	static string CategoryToTab( InboxCategory category ) => category switch
	{
		InboxCategory.Draft => "draft",
		InboxCategory.Contract => "freeagency",
		InboxCategory.Trade => "trades",
		InboxCategory.Coaching => "news",
		InboxCategory.General => "facilities",
		InboxCategory.Roster or InboxCategory.Injury => "team",
		InboxCategory.League => "home",
		_ => "inbox"
	};
}
