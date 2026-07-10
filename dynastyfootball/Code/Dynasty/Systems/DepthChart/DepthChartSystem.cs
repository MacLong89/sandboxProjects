using Dynasty.Core;
using Dynasty.Core.Enums;
using Dynasty.Core.Identifiers;
using Dynasty.Core.Interfaces;
using Dynasty.Data;
using DepthChartData = Dynasty.Data.DepthChart;
using Dynasty.Domain.League;
using Dynasty.Domain.Players;
using Dynasty.Domain.Teams;
using Dynasty.Systems.Formation;

namespace Dynasty.Systems.DepthChart;

public sealed class DepthChartSystem : ILeagueSystem
{
	public string SystemId => "depth_chart";

	LeagueSystemContext _context;

	public void Register( LeagueSystemContext context ) => _context = context;

	public void OnLeagueCreated( LeagueState state ) => EnsureAllTeamsDepthCharts( state );

	public void OnPhaseEntered( LeaguePhase phase, LeagueState state )
	{
		if ( phase == LeaguePhase.Preseason )
			EnsureAllTeamsDepthCharts( state );
	}

	public void OnWeekAdvanced( LeagueState state ) { }
	public void OnSeasonEnded( LeagueState state ) { }

	public void EnsureAllTeamsDepthCharts( LeagueState state )
	{
		if ( state == null )
			return;

		foreach ( var team in state.Teams.Values )
			EnsureTeamDepthChart( state, team.Id );

		state.BumpRevision( "depth_charts_synced" );
	}

	public void EnsureTeamDepthChart( LeagueState state, TeamId teamId )
	{
		if ( !state.Teams.TryGetValue( teamId, out var team ) )
			return;

		team.DepthChart ??= new Dictionary<string, List<PlayerId>>();

		var offense = FormationLayoutRegistry.Get( team.ActiveOffenseFormation );
		var defense = FormationLayoutRegistry.Get( team.ActiveDefenseFormation );
		var special = FormationLayoutRegistry.GetSpecialTeams();
		var allSlots = offense.Slots.Concat( defense.Slots ).Concat( special.Slots ).Select( s => s.SlotKey ).Distinct();

		foreach ( var slotKey in allSlots )
			DepthChartData.EnsureSlotList( team.DepthChart, slotKey );

		AutoFillEmptyStarters( state, team, offense, defense, special );
		PopulateBackupDepth( state, team, offense, defense, special );
	}

	public bool TrySetStarter(
		LeagueState state,
		TeamId teamId,
		string slotKey,
		PlayerId playerId,
		ulong executingSteamId,
		bool isHost,
		out string error )
	{
		error = "";

		if ( !ValidateManagement( state, teamId, executingSteamId, isHost, out error ) )
			return false;

		if ( string.IsNullOrWhiteSpace( slotKey ) )
		{
			error = "Invalid depth chart slot.";
			return false;
		}

		if ( !state.Teams.TryGetValue( teamId, out var team ) )
		{
			error = "Team not found.";
			return false;
		}

		EnsureTeamDepthChart( state, teamId );

		if ( playerId.IsEmpty )
		{
			DepthChartData.SetStarter( team.DepthChart, slotKey, PlayerId.Empty );
			state.BumpRevision( "depth_chart_clear" );
			return true;
		}

		if ( !state.Players.TryGetValue( playerId, out var player ) )
		{
			error = "Player not found.";
			return false;
		}

		if ( player.TeamId.Value != teamId.Value || !team.RosterPlayerIds.Contains( playerId ) )
		{
			error = "Player is not on this roster.";
			return false;
		}

		if ( !DepthChartPositionRules.IsEligible( slotKey, player.Identity.Position ) )
		{
			error = $"{player.Identity.FullName} is not eligible for {slotKey}.";
			return false;
		}

		DepthChartData.SetStarter( team.DepthChart, slotKey, playerId );
		state.BumpRevision( "depth_chart_set" );
		return true;
	}

	public bool TrySetFormation(
		LeagueState state,
		TeamId teamId,
		FormationSide side,
		FormationType formationType,
		ulong executingSteamId,
		bool isHost,
		out string error )
	{
		error = "";

		if ( !ValidateManagement( state, teamId, executingSteamId, isHost, out error ) )
			return false;

		var layout = FormationLayoutRegistry.Get( formationType );
		if ( layout.Side != side )
		{
			error = "Formation does not match selected side.";
			return false;
		}

		if ( !state.Teams.TryGetValue( teamId, out var team ) )
		{
			error = "Team not found.";
			return false;
		}

		if ( side == FormationSide.Offense )
			team.ActiveOffenseFormation = formationType;
		else if ( side == FormationSide.Defense )
			team.ActiveDefenseFormation = formationType;
		else
		{
			error = "Special teams assignments are not tied to a formation preset.";
			return false;
		}

		EnsureTeamDepthChart( state, teamId );
		state.BumpRevision( "formation_changed" );
		return true;
	}

	public static (int Filled, int Total) GetStarterCompletion( LeagueState state, TeamId teamId )
	{
		if ( state == null || !state.Teams.TryGetValue( teamId, out var team ) )
			return (0, 0);

		var offense = FormationLayoutRegistry.Get( team.ActiveOffenseFormation );
		var defense = FormationLayoutRegistry.Get( team.ActiveDefenseFormation );
		var special = FormationLayoutRegistry.GetSpecialTeams();
		var slots = offense.Slots.Concat( defense.Slots ).Concat( special.Slots ).Where( s => !s.IsOptional ).ToList();
		if ( slots.Count == 0 )
			return (0, 0);

		var filled = slots.Count( slot => !DepthChartData.GetStarter( team.DepthChart, slot.SlotKey ).IsEmpty );
		return (filled, slots.Count);
	}

	public void OnPlayerReleased( LeagueState state, TeamId teamId, PlayerId playerId )
	{
		if ( !state.Teams.TryGetValue( teamId, out var team ) || team.DepthChart == null )
			return;

		DepthChartData.RemovePlayerFromAllSlots( team.DepthChart, playerId );
	}

	public static bool CanUserControlTeam( LeagueState state, TeamId teamId, ulong steamId )
	{
		if ( state == null || teamId.IsEmpty )
			return false;

		if ( !GmAssignmentHelper.IsHumanTeam( state, teamId ) )
			return false;

		if ( steamId == 0 )
			return true;

		return state.GmAssignments.TryGetValue( steamId, out var assignment )
			&& assignment.ControlType == GmControlType.Human
			&& assignment.TeamId.Value == teamId.Value;
	}

	bool ValidateManagement( LeagueState state, TeamId teamId, ulong executingSteamId, bool isHost, out string error )
	{
		error = "";

		if ( state == null )
		{
			error = "No league loaded.";
			return false;
		}

		if ( !isHost )
		{
			error = "Only the host can modify the depth chart.";
			return false;
		}

		if ( state.Draft.IsActive )
		{
			error = "Depth chart changes are locked during the draft.";
			return false;
		}

		if ( !CanUserControlTeam( state, teamId, executingSteamId ) )
		{
			error = "You do not control this team.";
			return false;
		}

		return true;
	}

	void AutoFillEmptyStarters( LeagueState state, TeamState team, params FormationLayout[] layouts )
	{
		var assigned = new HashSet<Guid>();
		var roster = GetActiveRoster( state, team );

		if ( roster.Count == 0 )
			return;

		foreach ( var layout in layouts )
		{
			var orderedSlots = layout.Slots
				.OrderBy( s => s.IsOptional )
				.ThenBy( s => s.SlotKey );

			foreach ( var slot in orderedSlots )
			{
				if ( !DepthChartData.GetStarter( team.DepthChart, slot.SlotKey ).IsEmpty )
				{
					assigned.Add( DepthChartData.GetStarter( team.DepthChart, slot.SlotKey ).Value );
					continue;
				}

				var candidate = FindBestForSlot( roster, assigned, slot );
				if ( candidate == null )
					continue;

				DepthChartData.SetStarter( team.DepthChart, slot.SlotKey, candidate.Id );
				assigned.Add( candidate.Id.Value );
			}
		}
	}

	void PopulateBackupDepth( LeagueState state, TeamState team, params FormationLayout[] layouts )
	{
		var roster = GetActiveRoster( state, team );
		if ( roster.Count == 0 )
			return;

		var onChart = new HashSet<Guid>();
		foreach ( var list in team.DepthChart.Values )
		{
			if ( list == null )
				continue;

			foreach ( var id in list )
				onChart.Add( id.Value );
		}

		var remaining = roster
			.Where( p => !onChart.Contains( p.Id.Value ) )
			.OrderByDescending( p => p.Ratings.Overall )
			.ToList();

		if ( remaining.Count == 0 )
			return;

		var changed = true;
		while ( remaining.Count > 0 && changed )
		{
			changed = false;
			foreach ( var layout in layouts )
			{
				foreach ( var slot in layout.Slots.OrderBy( s => DepthChartData.GetDepth( team.DepthChart, s.SlotKey ).Count ) )
				{
					var backup = remaining
						.FirstOrDefault( p => IsEligibleForSlot( slot, p.Identity.Position ) );

					if ( backup == null )
						continue;

					if ( !team.DepthChart.TryGetValue( slot.SlotKey, out var list ) || list == null )
					{
						list = new List<PlayerId>();
						team.DepthChart[slot.SlotKey] = list;
					}

					if ( list.Any( id => id.Value == backup.Id.Value ) )
						continue;

					list.Add( backup.Id );
					remaining.Remove( backup );
					changed = true;

					if ( remaining.Count == 0 )
						break;
				}
			}
		}
	}

	static List<PlayerState> GetActiveRoster( LeagueState state, TeamState team )
		=> team.RosterPlayerIds
			.Select( id => state.Players.GetValueOrDefault( id ) )
			.Where( p => p != null && !p.IsRetired )
			.ToList();

	static PlayerState FindBestForSlot( List<PlayerState> roster, HashSet<Guid> assigned, FormationSlot slot )
	{
		return roster
			.Where( p => !assigned.Contains( p.Id.Value ) )
			.Where( p => IsEligibleForSlot( slot, p.Identity.Position ) )
			.OrderByDescending( p => p.Ratings.Overall )
			.ThenBy( p => p.Identity.FullName )
			.FirstOrDefault();
	}

	static bool IsEligibleForSlot( FormationSlot slot, Position position )
	{
		if ( slot.EligiblePositions == null || slot.EligiblePositions.Length == 0 )
			return DepthChartPositionRules.IsEligible( slot.SlotKey, position );

		return slot.EligiblePositions.Contains( position );
	}
}
