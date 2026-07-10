using Dynasty.Core.Enums;
using Dynasty.Core.Identifiers;
using Dynasty.Domain.History;
using Dynasty.Domain.League;
using Dynasty.Systems.Season;

namespace Dynasty.UI.ViewModels;

public sealed class LegacyViewModel
{
	public int ChampionshipCount { get; init; }
	public int AwardCount { get; init; }
	public int RetiredCount { get; init; }
	public string LifetimeRecord { get; init; } = "0-0";
	public string LifetimeRecordDetail { get; init; } = "";
	public IReadOnlyList<SeasonRecordRow> SeasonHistory { get; init; } = Array.Empty<SeasonRecordRow>();
	public IReadOnlyList<ChampionshipRow> Championships { get; init; } = Array.Empty<ChampionshipRow>();
	public IReadOnlyList<AwardRow> Awards { get; init; } = Array.Empty<AwardRow>();
	public IReadOnlyList<HallOfFameRow> HallOfFame { get; init; } = Array.Empty<HallOfFameRow>();
	public int DynastyScore { get; init; }
	public int OwnerJobSecurity { get; init; } = 80;
	public string ChallengeStatus { get; init; } = "";
	public string SeasonObjectiveStatus { get; init; } = "";

	public static LegacyViewModel From( LeagueState state, TeamId userTeamId )
	{
		if ( state == null )
			return new LegacyViewModel();

		var championships = state.History.Championships
			.OrderByDescending( c => c.Season )
			.Select( c => ChampionshipRow.From( state, c, userTeamId ) )
			.ToList();

		var awards = state.History.Awards
			.OrderByDescending( a => a.Season )
			.Select( a => AwardRow.From( state, a, userTeamId ) )
			.ToList();

		var hof = state.History.HallOfFame
			.OrderByDescending( h => h.InductionSeason )
			.Select( h => HallOfFameRow.From( state, h ) )
			.ToList();

		var userChamps = !userTeamId.IsEmpty
			? championships.Count( c => c.IsUserTeam )
			: 0;

		var lifetimeRecord = "0-0";
		var lifetimeDetail = "Win-loss record across all completed seasons.";
		var seasonHistory = Array.Empty<SeasonRecordRow>();
		if ( !userTeamId.IsEmpty && state.Teams.TryGetValue( userTeamId, out var userTeam ) )
		{
			lifetimeRecord = TeamRecordArchive.FormatRecord(
				userTeam.LifetimeRecord.Wins + userTeam.Record.Wins,
				userTeam.LifetimeRecord.Losses + userTeam.Record.Losses,
				userTeam.LifetimeRecord.Ties + userTeam.Record.Ties );

			var current = TeamRecordArchive.FormatRecord( userTeam.Record.Wins, userTeam.Record.Losses, userTeam.Record.Ties );
			var archived = TeamRecordArchive.FormatLifetime( userTeam.LifetimeRecord );
			lifetimeDetail = userTeam.Record.Wins + userTeam.Record.Losses + userTeam.Record.Ties > 0
				? $"This season {current} · Completed seasons {archived}"
				: userTeam.LifetimeRecord.Wins + userTeam.LifetimeRecord.Losses + userTeam.LifetimeRecord.Ties > 0
					? $"Completed seasons {archived}"
					: "No games completed yet.";
			seasonHistory = state.History.SeasonRecords
				.Where( r => r.TeamId.Value == userTeamId.Value )
				.OrderByDescending( r => r.Season )
				.Select( SeasonRecordRow.From )
				.ToArray();
		}

		var progress = state.FranchiseProgress;
		var challengeStatus = BuildChallengeStatus( state, progress );
		var seasonObjectiveStatus = BuildSeasonObjectiveStatus( state, progress );

		return new LegacyViewModel
		{
			ChampionshipCount = userChamps > 0 ? userChamps : championships.Count,
			AwardCount = awards.Count( a => a.IsUserTeam ),
			RetiredCount = state.History.RetiredPlayers.Count,
			LifetimeRecord = lifetimeRecord,
			LifetimeRecordDetail = lifetimeDetail,
			SeasonHistory = seasonHistory,
			Championships = championships,
			Awards = awards,
			HallOfFame = hof,
			DynastyScore = progress?.DynastyScore ?? 0,
			OwnerJobSecurity = progress?.OwnerJobSecurity ?? 80,
			ChallengeStatus = challengeStatus,
			SeasonObjectiveStatus = seasonObjectiveStatus
		};
	}

	static string BuildSeasonObjectiveStatus( LeagueState state, Domain.Franchise.FranchiseProgressState progress )
	{
		var obj = progress?.SeasonObjective;
		if ( obj == null || obj.AssignedSeason != state.CurrentSeason )
			return "";

		if ( obj.Completed )
			return $"Season objective complete: {obj.Title}";

		if ( obj.Failed )
			return $"Season objective missed: {obj.Title}";

		return $"{obj.Title} — {obj.Progress}/{obj.Target}";
	}

	static string BuildChallengeStatus( LeagueState state, Domain.Franchise.FranchiseProgressState progress )
	{
		if ( state.Settings.ChallengeMode == ChallengeMode.Standard || progress == null )
			return "Standard franchise — build your legacy.";

		if ( progress.ChallengeCompleted )
			return $"{state.Settings.ChallengeMode}: Completed";

		if ( progress.ChallengeFailed )
			return $"{state.Settings.ChallengeMode}: Failed — owner confidence shattered.";

		if ( progress.IsFired )
			return "You have been fired. Start a new franchise to continue.";

		return state.Settings.ChallengeMode switch
		{
			ChallengeMode.Rebuild => $"Rebuild: reach playoffs by Season 3 ({Math.Min( state.CurrentSeason, 3 )}/3)",
			ChallengeMode.WinNow => $"Win Now: championship by Season 5 ({Math.Min( state.CurrentSeason, 5 )}/5)",
			ChallengeMode.DraftGenius => $"Draft Genius: {progress.DraftStealsFound}/3 steals found",
			_ => state.Settings.ChallengeMode.ToString()
		};
	}
}

public sealed class SeasonRecordRow
{
	public int Season { get; init; }
	public string Record { get; init; } = "";

	public static SeasonRecordRow From( TeamSeasonRecordEntry entry ) => new()
	{
		Season = entry.Season,
		Record = TeamRecordArchive.FormatRecord( entry.Wins, entry.Losses, entry.Ties )
	};
}

public sealed class ChampionshipRow
{
	public int Season { get; init; }
	public string Label { get; init; } = "";
	public string Score { get; init; } = "";
	public bool IsUserTeam { get; init; }

	public static ChampionshipRow From( LeagueState state, ChampionshipRecord record, TeamId userTeamId )
	{
		state.Teams.TryGetValue( record.ChampionId, out var champ );
		state.Teams.TryGetValue( record.RunnerUpId, out var runner );
		var isUser = !userTeamId.IsEmpty && record.ChampionId.Value == userTeamId.Value;

		return new ChampionshipRow
		{
			Season = record.Season,
			Label = $"{champ?.Identity.Abbreviation ?? "?"} defeats {runner?.Identity.Abbreviation ?? "?"}",
			Score = $"{record.ChampionScore}-{record.RunnerUpScore}",
			IsUserTeam = isUser
		};
	}
}

public sealed class AwardRow
{
	public int Season { get; init; }
	public string AwardName { get; init; } = "";
	public string PlayerName { get; init; } = "";
	public bool IsUserTeam { get; init; }

	public static AwardRow From( LeagueState state, AwardRecord record, TeamId userTeamId )
	{
		state.Players.TryGetValue( record.PlayerId, out var player );
		var isUser = !userTeamId.IsEmpty && record.TeamId.Value == userTeamId.Value;

		return new AwardRow
		{
			Season = record.Season,
			AwardName = record.AwardName,
			PlayerName = player?.Identity.FullName ?? "Unknown",
			IsUserTeam = isUser
		};
	}
}

public sealed class HallOfFameRow
{
	public int Season { get; init; }
	public string PlayerName { get; init; } = "";
	public string Citation { get; init; } = "";

	public static HallOfFameRow From( LeagueState state, HallOfFameEntry entry )
	{
		state.Players.TryGetValue( entry.PlayerId, out var player );
		return new HallOfFameRow
		{
			Season = entry.InductionSeason,
			PlayerName = player?.Identity.FullName ?? "Unknown",
			Citation = entry.Citation
		};
	}
}
