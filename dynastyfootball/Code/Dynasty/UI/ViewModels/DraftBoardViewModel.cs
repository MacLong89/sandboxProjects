using Dynasty.Core;
using Dynasty.Core.Enums;
using Dynasty.Core.Identifiers;
using Dynasty.Domain.League;

namespace Dynasty.UI.ViewModels;

public sealed class DraftBoardViewModel
{
	public bool IsActive { get; init; }
	public string DraftType { get; init; } = "";
	public int Season { get; init; }
	public int CurrentRound { get; init; }
	public int CurrentOverallPick { get; init; }
	public string OnClockTeam { get; init; } = "";
	public string OnClockFullName { get; init; } = "";
	public bool IsUserOnClock { get; init; }
	public int TotalPicks { get; init; }
	public int CompletedPicks { get; init; }
	public int PickTimerSeconds { get; init; } = 60;
	public int PickSecondsRemaining { get; init; }
	public IReadOnlyList<DraftBoardPickRow> Board { get; init; } = Array.Empty<DraftBoardPickRow>();
	public IReadOnlyList<DraftRecentPickRow> RecentPicks { get; init; } = Array.Empty<DraftRecentPickRow>();
	public IReadOnlyList<ProspectRow> AvailableProspects { get; init; } = Array.Empty<ProspectRow>();

	public static DraftBoardViewModel From( LeagueState state, TeamId humanTeamId )
	{
		if ( state == null )
			return new DraftBoardViewModel();

		var current = state.Draft.Order.ElementAtOrDefault( state.Draft.CurrentPickIndex );
		var onClockAbbr = "—";
		var onClockName = "—";
		var userOnClock = false;

		if ( current != null && state.Teams.TryGetValue( current.TeamId, out var onClockTeam ) )
		{
			onClockAbbr = onClockTeam.Identity.Abbreviation;
			onClockName = $"{onClockTeam.Identity.City} {onClockTeam.Identity.Name}";
			userOnClock = GmAssignmentHelper.IsHumanTeam( state, current.TeamId );
		}

		var board = state.Draft.Order
			.Select( entry => DraftBoardPickRow.From( state, entry, entry == current ) )
			.ToList();

		var recent = state.Draft.History
			.OrderByDescending( h => h.OverallPick )
			.Take( 12 )
			.Select( h => DraftRecentPickRow.From( state, h ) )
			.ToList();

		var prospects = state.Draft.Prospects
			.Where( p => !p.IsDrafted )
			.OrderBy( p => p.ConsensusRank )
			.Select( p => ProspectRow.From( state, p, humanTeamId ) )
			.ToList();

		var timerSeconds = state.Settings.DraftPickTimerSeconds;
		var secondsRemaining = timerSeconds;
		if ( state.Draft.IsActive && current != null && !current.IsComplete && state.Draft.OnClockSinceUtc != default )
		{
			var elapsed = (int)( DateTime.UtcNow - state.Draft.OnClockSinceUtc ).TotalSeconds;
			secondsRemaining = Math.Max( 0, timerSeconds - elapsed );
		}

		return new DraftBoardViewModel
		{
			IsActive = state.Draft.IsActive,
			DraftType = state.Draft.Type.ToString(),
			Season = state.Draft.Season,
			CurrentRound = state.Draft.CurrentRound,
			CurrentOverallPick = current?.OverallPick ?? state.Draft.Order.Count,
			OnClockTeam = onClockAbbr,
			OnClockFullName = onClockName,
			IsUserOnClock = userOnClock,
			TotalPicks = state.Draft.Order.Count,
			CompletedPicks = state.Draft.Order.Count( o => o.IsComplete ),
			PickTimerSeconds = timerSeconds,
			PickSecondsRemaining = secondsRemaining,
			Board = board,
			RecentPicks = recent,
			AvailableProspects = prospects
		};
	}
}

public sealed class DraftBoardPickRow
{
	public int OverallPick { get; init; }
	public int Round { get; init; }
	public string TeamAbbr { get; init; } = "";
	public string PlayerName { get; init; } = "";
	public string Position { get; init; } = "";
	public bool IsComplete { get; init; }
	public bool IsOnClock { get; init; }
	public bool IsUserTeam { get; init; }

	public static DraftBoardPickRow From( LeagueState state, Domain.Draft.DraftOrderEntry entry, bool isOnClock )
	{
		var teamAbbr = state.Teams.TryGetValue( entry.TeamId, out var team ) ? team.Identity.Abbreviation : "???";
		var history = state.Draft.History.FirstOrDefault( h => h.OverallPick == entry.OverallPick );
		var playerName = "—";
		var position = "";

		if ( history != null && state.Players.TryGetValue( history.PlayerId, out var player ) )
		{
			playerName = player.Identity.FullName;
			position = player.Identity.Position.ToString();
		}
		else if ( isOnClock && !entry.IsComplete )
		{
			playerName = "On the clock";
		}

		return new DraftBoardPickRow
		{
			OverallPick = entry.OverallPick,
			Round = entry.Round,
			TeamAbbr = teamAbbr,
			PlayerName = playerName,
			Position = position,
			IsComplete = entry.IsComplete,
			IsOnClock = isOnClock && !entry.IsComplete,
			IsUserTeam = GmAssignmentHelper.IsHumanTeam( state, entry.TeamId )
		};
	}
}

public sealed class DraftRecentPickRow
{
	public int OverallPick { get; init; }
	public string TeamAbbr { get; init; } = "";
	public string PlayerName { get; init; } = "";
	public string Detail { get; init; } = "";

	public static DraftRecentPickRow From( LeagueState state, Domain.Draft.DraftHistoryEntry entry )
	{
		var teamAbbr = state.Teams.TryGetValue( entry.TeamId, out var team ) ? team.Identity.Abbreviation : "???";
		if ( !state.Players.TryGetValue( entry.PlayerId, out var player ) )
		{
			return new DraftRecentPickRow
			{
				OverallPick = entry.OverallPick,
				TeamAbbr = teamAbbr,
				PlayerName = "Unknown"
			};
		}

		return new DraftRecentPickRow
		{
			OverallPick = entry.OverallPick,
			TeamAbbr = teamAbbr,
			PlayerName = player.Identity.FullName,
			Detail = $"{player.Identity.Position} · OVR {player.Ratings.Overall}"
		};
	}
}
