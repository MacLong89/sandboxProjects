using Dynasty.Core.Enums;
using Dynasty.Core.Interfaces;
using Dynasty.Domain.League;
using Dynasty.Domain.Players;

namespace Dynasty.Systems.Scouting;

public sealed class ScoutingSystem : ILeagueSystem
{
	public string SystemId => "scouting";

	private LeagueSystemContext _context;

	public void Register( LeagueSystemContext context ) => _context = context;

	public void OnLeagueCreated( LeagueState state ) { }

	public void OnPhaseEntered( LeaguePhase phase, LeagueState state )
	{
		if ( phase is LeaguePhase.Offseason or LeaguePhase.Draft )
			AdvanceScoutingForAllTeams( state );
	}

	public void OnWeekAdvanced( LeagueState state )
	{
		if ( state.Phase is LeaguePhase.Offseason or LeaguePhase.Draft )
			AdvanceScoutingForAllTeams( state );
	}

	public void OnSeasonEnded( LeagueState state ) { }

	public void RevealProspect( LeagueState state, TeamId teamId, PlayerState prospect, int scoutRating )
	{
		var reveal = prospect.Scouting;
		var confidence = Math.Clamp( scoutRating + state.Teams[teamId].Facilities.Levels.GetValueOrDefault( FacilityType.ScoutingDepartment, 1 ) * 5, 20, 95 );

		if ( !reveal.OverallRevealed && _context.Random.Chance( confidence / 120f ) )
			reveal.OverallRevealed = true;

		if ( !reveal.PotentialRevealed && _context.Random.Chance( confidence / 150f ) )
			reveal.PotentialRevealed = true;

		var keys = prospect.Ratings.Attributes.Keys.ToList();
		if ( keys.Count > 0 )
		{
			var key = _context.Random.Pick( keys );
			reveal.RevealedAttributes.Add( key );
		}

		if ( prospect.Traits.Count > 0 && _context.Random.Chance( confidence / 200f ) )
			reveal.RevealedTraits.Add( _context.Random.Pick( prospect.Traits ) );

		reveal.ScoutConfidence = confidence;
	}

	void AdvanceScoutingForAllTeams( LeagueState state )
	{
		foreach ( var team in state.Teams.Values )
		{
			var scoutRating = team.CoachIds
				.Select( id => state.Coaches.GetValueOrDefault( id ) )
				.Where( c => c?.Role == CoachRole.Scout )
				.Select( c => c.Ratings.Scouting )
				.DefaultIfEmpty( 60 )
				.Max();

			foreach ( var prospect in state.Draft.Prospects.Where( p => !p.IsDrafted ) )
				RevealProspect( state, team.Id, prospect.Player, scoutRating );
		}

		state.BumpRevision( "scouting" );
	}
}
