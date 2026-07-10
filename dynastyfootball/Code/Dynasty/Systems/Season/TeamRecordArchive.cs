using Dynasty.Core.Identifiers;
using Dynasty.Domain.History;
using Dynasty.Domain.League;
using Dynasty.Domain.Teams;

namespace Dynasty.Systems.Season;

public static class TeamRecordArchive
{
	public static void ArchiveAndResetAll( LeagueState state, int completedSeason )
	{
		foreach ( var team in state.Teams.Values )
			ArchiveAndReset( state, team, completedSeason );
	}

	public static void ArchiveAndReset( LeagueState state, TeamState team, int completedSeason )
	{
		var played = team.Record.Wins + team.Record.Losses + team.Record.Ties;
		if ( played > 0 )
		{
			state.History.SeasonRecords.Add( new TeamSeasonRecordEntry
			{
				TeamId = team.Id,
				Season = completedSeason,
				Wins = team.Record.Wins,
				Losses = team.Record.Losses,
				Ties = team.Record.Ties,
				PointsFor = team.Record.PointsFor,
				PointsAgainst = team.Record.PointsAgainst
			} );

			team.LifetimeRecord.Wins += team.Record.Wins;
			team.LifetimeRecord.Losses += team.Record.Losses;
			team.LifetimeRecord.Ties += team.Record.Ties;
		}

		ResetCurrent( team );
	}

	public static void ResetCurrent( TeamState team )
	{
		team.Record = new TeamRecord();
	}

	public static TeamSeasonRecordEntry GetMostRecentSeasonRecord( LeagueState state, TeamId teamId )
		=> state.History.SeasonRecords
			.Where( r => r.TeamId.Value == teamId.Value )
			.OrderByDescending( r => r.Season )
			.FirstOrDefault();

	public static (int Wins, int Losses, int Ties, int PointsFor, int PointsAgainst) GetStandingsStats(
		LeagueState state,
		TeamState team )
	{
		var recent = GetMostRecentSeasonRecord( state, team.Id );
		if ( recent != null )
		{
			return (recent.Wins, recent.Losses, recent.Ties, recent.PointsFor, recent.PointsAgainst);
		}

		return (team.Record.Wins, team.Record.Losses, team.Record.Ties, team.Record.PointsFor, team.Record.PointsAgainst);
	}

	public static string FormatRecord( int wins, int losses, int ties = 0 )
	{
		var tiesPart = ties > 0 ? $"-{ties}" : "";
		return $"{wins}-{losses}{tiesPart}";
	}

	public static string FormatLifetime( TeamLifetimeRecord record )
		=> FormatRecord( record.Wins, record.Losses, record.Ties );
}
