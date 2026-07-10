using Dynasty.Core;
using Dynasty.Core.Enums;
using Dynasty.Core.Interfaces;
using Dynasty.Core.Identifiers;
using Dynasty.Domain.Coaches;
using Dynasty.Domain.League;
using Dynasty.Systems.Franchise;
using Dynasty.Systems.Season;

namespace Dynasty.Systems.Coaching;

public sealed class CoachingCarouselSystem : ILeagueSystem
{
	public string SystemId => "coaching_carousel";

	private LeagueSystemContext _context;
	private InboxSystem _inbox;

	public void Register( LeagueSystemContext context ) => _context = context;

	public void SetInboxSystem( InboxSystem inbox ) => _inbox = inbox;

	public void OnLeagueCreated( LeagueState state ) { }

	public void OnPhaseEntered( LeaguePhase phase, LeagueState state )
	{
		if ( phase == LeaguePhase.Offseason && state.OffseasonSubPhase == OffseasonSubPhase.CoachingChanges )
			RunCarousel( state );
	}

	public void OnWeekAdvanced( LeagueState state ) { }
	public void OnSeasonEnded( LeagueState state ) { }

	public void RunCarousel( LeagueState state )
	{
		if ( state.Inbox.Any( m =>
			m.Category == InboxCategory.Coaching
			&& m.Season == state.CurrentSeason
			&& m.Subject.Contains( "fire head coach" ) ) )
			return;

		var fired = 0;
		foreach ( var team in state.Teams.Values )
		{
			var hc = state.Coaches.Values.FirstOrDefault( c => c.TeamId.Value == team.Id.Value && c.Role == CoachRole.HeadCoach );
			if ( hc == null )
				continue;

			var stats = TeamRecordArchive.GetStandingsStats( state, team );
			var winPct = stats.Wins + stats.Losses > 0
				? stats.Wins / (float)(stats.Wins + stats.Losses )
				: 0.5f;

			if ( winPct >= 0.35f && !_context.Random.Chance( 0.08f ) )
				continue;

			fired++;
			team.CoachIds.Remove( hc.Id );
			state.Coaches.Remove( hc.Id );

			var replacement = new CoachState
			{
				Id = CoachId.New(),
				FirstName = "Coach",
				LastName = $"{team.Identity.Abbreviation}-HC-New",
				Age = _context.Random.NextInt( 40, 62 ),
				Role = CoachRole.HeadCoach,
				TeamId = team.Id,
				Ratings = new CoachRatings
				{
					Overall = _context.Random.NextInt( 55, 88 ),
					Development = _context.Random.NextInt( 50, 90 ),
					GamePlanning = _context.Random.NextInt( 50, 90 ),
					Motivation = _context.Random.NextInt( 50, 90 ),
					Scouting = _context.Random.NextInt( 45, 80 )
				}
			};

			state.Coaches[replacement.Id] = replacement;
			team.CoachIds.Add( replacement.Id );

			var isHuman = !GmAssignmentHelper.GetHumanTeamId( state ).IsEmpty
				&& team.Id.Value == GmAssignmentHelper.GetHumanTeamId( state ).Value;

			_inbox?.Add( state, InboxCategory.Coaching, InboxPriority.Normal,
				isHuman ? "Your head coach was replaced" : $"{team.Identity.City} {team.Identity.Name} fire head coach",
				$"{hc.FirstName} {hc.LastName} replaced after a {TeamRecordArchive.FormatRecord( stats.Wins, stats.Losses, stats.Ties )} season.",
				false, team.Id, navigateTab: isHuman ? "team" : "news" );
		}

		if ( fired > 0 )
			state.BumpRevision( "coaching_carousel" );
	}
}
