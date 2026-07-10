using Dynasty.Core.Enums;
using Dynasty.Core.Identifiers;
using Dynasty.Domain.League;
using Dynasty.Domain.Teams;

namespace Dynasty.Core;

public static class GmAssignmentHelper
{
	public static void AssignHumanTeam( LeagueState state, string abbreviation, ulong steamId = 0 )
	{
		if ( state == null || string.IsNullOrWhiteSpace( abbreviation ) )
		{
			EnsureSoloHumanGm( state, steamId );
			return;
		}

		var team = state.Teams.Values.FirstOrDefault( t =>
			t.Identity.Abbreviation.Equals( abbreviation.Trim(), StringComparison.OrdinalIgnoreCase ) );

		if ( team == null )
		{
			EnsureSoloHumanGm( state, steamId );
			return;
		}

		if ( !state.Settings.EnableMultiGm )
		{
			foreach ( var other in state.Teams.Values )
			{
				other.ControlType = GmControlType.AI;
				other.HumanOwnerSteamId = null;
			}

			state.GmAssignments.Clear();
		}
		else
		{
			ReleaseTeamFromOtherHumans( state, team.Id, steamId );
			ReleaseSteamAssignment( state, steamId );
		}

		state.GmAssignments[steamId] = new GmAssignment
		{
			SteamId = steamId,
			TeamId = team.Id,
			ControlType = GmControlType.Human,
			IsCommissioner = steamId == 0 || !state.GmAssignments.Values.Any( g => g.IsCommissioner )
		};

		team.ControlType = GmControlType.Human;
		team.HumanOwnerSteamId = steamId;
	}

	public static bool TryClaimTeam( LeagueState state, ulong steamId, string abbreviation, out string error )
	{
		error = "";

		if ( state == null )
		{
			error = "No league loaded.";
			return false;
		}

		if ( !state.Settings.EnableMultiGm )
		{
			error = "This league is solo-GM only.";
			return false;
		}

		var team = state.Teams.Values.FirstOrDefault( t =>
			t.Identity.Abbreviation.Equals( abbreviation.Trim(), StringComparison.OrdinalIgnoreCase ) );

		if ( team == null )
		{
			error = "Unknown team.";
			return false;
		}

		if ( team.ControlType == GmControlType.Human && team.HumanOwnerSteamId != steamId )
		{
			error = "That franchise is already claimed.";
			return false;
		}

		AssignHumanTeam( state, abbreviation, steamId );
		return true;
	}

	public static TeamId GetTeamForSteamId( LeagueState state, ulong steamId )
	{
		if ( state?.GmAssignments != null
			&& state.GmAssignments.TryGetValue( steamId, out var assignment )
			&& assignment.ControlType == GmControlType.Human )
			return assignment.TeamId;

		return TeamId.Empty;
	}

	public static IReadOnlyList<TeamState> GetClaimableTeams( LeagueState state )
	{
		if ( state == null )
			return Array.Empty<TeamState>();

		return state.Teams.Values
			.Where( t => t.ControlType != GmControlType.Human )
			.OrderBy( t => t.Identity.Abbreviation )
			.ToList();
	}

	public static IReadOnlyList<TeamState> GetHumanControlledTeams( LeagueState state )
	{
		if ( state == null )
			return Array.Empty<TeamState>();

		return state.Teams.Values
			.Where( t => t.ControlType == GmControlType.Human )
			.OrderBy( t => t.Identity.Abbreviation )
			.ToList();
	}

	static void ReleaseTeamFromOtherHumans( LeagueState state, TeamId teamId, ulong exceptSteamId )
	{
		foreach ( var pair in state.GmAssignments.ToList() )
		{
			if ( pair.Key == exceptSteamId )
				continue;

			if ( pair.Value.TeamId.Value != teamId.Value )
				continue;

			state.GmAssignments.Remove( pair.Key );
		}
	}

	static void ReleaseSteamAssignment( LeagueState state, ulong steamId )
	{
		if ( !state.GmAssignments.TryGetValue( steamId, out var prior ) )
			return;

		if ( state.Teams.TryGetValue( prior.TeamId, out var priorTeam ) )
		{
			priorTeam.ControlType = GmControlType.AI;
			priorTeam.HumanOwnerSteamId = null;
		}

		state.GmAssignments.Remove( steamId );
	}

	public static void EnsureSoloHumanGm( LeagueState state, ulong steamId = 0 )
	{
		if ( state == null || state.Teams.Count == 0 )
			return;

		if ( state.GmAssignments.Values.Any( g => g.ControlType == GmControlType.Human ) )
			return;

		TeamId teamId;
		if ( !string.IsNullOrWhiteSpace( state.Settings.HumanTeamAbbreviation ) )
		{
			var match = state.Teams.Values.FirstOrDefault( t =>
				t.Identity.Abbreviation.Equals( state.Settings.HumanTeamAbbreviation, StringComparison.OrdinalIgnoreCase ) );
			teamId = match?.Id ?? state.Teams.Keys.First();
		}
		else
		{
			teamId = state.Teams.Keys.First();
		}
		state.GmAssignments[steamId] = new GmAssignment
		{
			SteamId = steamId,
			TeamId = teamId,
			ControlType = GmControlType.Human,
			IsCommissioner = true
		};

		if ( state.Teams.TryGetValue( teamId, out var team ) )
		{
			team.ControlType = GmControlType.Human;
			team.HumanOwnerSteamId = steamId;
		}
	}

	public static bool IsHumanTeam( LeagueState state, TeamId teamId )
	{
		if ( teamId.IsEmpty || state == null )
			return false;

		if ( state.Teams.TryGetValue( teamId, out var team ) && team.ControlType == GmControlType.Human )
			return true;

		return state.GmAssignments.Values.Any( g => g.TeamId.Value == teamId.Value && g.ControlType == GmControlType.Human );
	}

	public static TeamId GetHumanTeamId( LeagueState state )
	{
		var assignment = state?.GmAssignments.Values.FirstOrDefault( g => g.ControlType == GmControlType.Human );
		if ( assignment != null && !assignment.TeamId.IsEmpty )
			return assignment.TeamId;

		return state?.Teams.Values.FirstOrDefault( t => t.ControlType == GmControlType.Human )?.Id ?? TeamId.Empty;
	}

	/// <summary>
	/// Validates multiplayer GM rights. Solo play (steamId 0) always passes.
	/// </summary>
	public static bool TryAuthorizeTeamCommand( LeagueState state, TeamId teamId, ulong steamId, out string error )
	{
		error = "";

		if ( steamId == 0 )
			return true;

		if ( teamId.IsEmpty || !IsHumanTeam( state, teamId ) )
		{
			error = "You do not control this team.";
			return false;
		}

		if ( state.GmAssignments.TryGetValue( steamId, out var assignment )
			&& assignment.ControlType == GmControlType.Human
			&& assignment.TeamId.Value == teamId.Value )
			return true;

		error = "You do not control this team.";
		return false;
	}
}
