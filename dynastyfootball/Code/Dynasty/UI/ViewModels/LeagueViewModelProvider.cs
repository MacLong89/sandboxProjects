using Dynasty.Core;
using Dynasty.Core.Identifiers;
using Dynasty.Data;
using Dynasty.Domain.League;
using Sandbox;

namespace Dynasty.UI.ViewModels;

/// <summary>
/// Central factory for UI projections. Screens depend on this, not LeagueService or systems.
/// </summary>
public sealed class LeagueViewModelProvider
{
	public LeagueState State { get; private set; }
	public TeamId SelectedTeamId { get; private set; }

	public void Update( LeagueState state, TeamId selectedTeamId = default )
	{
		State = state;
		if ( !selectedTeamId.IsEmpty )
			SelectedTeamId = selectedTeamId;
		else if ( SelectedTeamId.IsEmpty && state != null )
		{
			var steamId = Connection.Local?.SteamId ?? 0;
			var human = GmAssignmentHelper.GetTeamForSteamId( state, steamId );
			if ( human.IsEmpty )
				human = GmAssignmentHelper.GetHumanTeamId( state );
			SelectedTeamId = !human.IsEmpty ? human : state.Teams.Keys.FirstOrDefault();
		}
	}

	public LeagueHomeViewModel GetHome() => LeagueHomeViewModel.From( State, SelectedTeamId );
	public FreeAgencyViewModel GetFreeAgency() => FreeAgencyViewModel.From( State, SelectedTeamId );
	public InboxViewModel GetInbox() => InboxViewModel.From( State );
	public TeamOverviewViewModel GetTeamOverview() => TeamOverviewViewModel.From( State, SelectedTeamId );
	public ScheduleViewModel GetSchedule( int week, bool userGamesOnly = false ) => ScheduleViewModel.From( State, week, userGamesOnly );
	public DraftRoomViewModel GetDraftRoom() => DraftRoomViewModel.From( State, SelectedTeamId );
	public DraftBoardViewModel GetDraftBoard() => DraftBoardViewModel.From( State, SelectedTeamId );
	public ActionTodoViewModel GetActionTodos() => ActionTodoViewModel.From( State, SelectedTeamId );
	public PlayerDetailViewModel GetPlayerDetail( PlayerId playerId, TeamId teamId = default )
		=> PlayerDetailViewModel.From( State, teamId.IsEmpty ? SelectedTeamId : teamId, playerId );

	public PlayerProfileViewModel GetPlayerProfile( PlayerId playerId, TeamId teamId = default )
	{
		if ( State == null || playerId.IsEmpty )
			return null;

		var resolvedTeam = teamId;
		if ( resolvedTeam.IsEmpty && State.Players.TryGetValue( playerId, out var player ) )
			resolvedTeam = player.TeamId;
		if ( resolvedTeam.IsEmpty )
			resolvedTeam = SelectedTeamId;

		var isUserTeam = !SelectedTeamId.IsEmpty && resolvedTeam.Value == SelectedTeamId.Value;
		var detail = PlayerDetailViewModel.From( State, resolvedTeam, playerId );
		return PlayerProfileViewModel.FromRoster( detail, resolvedTeam, isUserTeam );
	}

	public PlayerProfileViewModel GetProspectProfile( PlayerId prospectId, bool showDraftAction = false )
		=> PlayerProfileViewModel.FromProspect( GetProspectDetail( prospectId ), showDraftAction );
	public GameViewerViewModel GetGameViewer( GameId gameId, bool autoPlayReplay = false )
		=> GameViewerViewModel.From( State, gameId, SelectedTeamId, autoPlayReplay );
	public SessionSummaryViewModel GetSessionSummary() => SessionSummaryViewModel.From( State, SelectedTeamId );
	public NewsViewModel GetNews() => NewsViewModel.From( State );
	public NewsDetailViewModel GetNewsDetail( Guid articleId ) => NewsDetailViewModel.From( State, articleId );
	public InboxDetailViewModel GetInboxDetail( Guid messageId ) => InboxDetailViewModel.From( State, messageId );
	public ProspectDetailViewModel GetProspectDetail( PlayerId prospectId ) => ProspectDetailViewModel.From( State, prospectId );
	public FormationRosterViewModel GetFormationRoster( FormationSide side, FormationType? formationOverride = null )
		=> FormationRosterViewModel.From( State, SelectedTeamId, side, formationOverride );
	public DepthChartViewModel GetDepthChart() => DepthChartViewModel.From( State, SelectedTeamId );
	public PlayerPickerViewModel GetPlayerPicker( string slotKey, string slotLabel )
		=> PlayerPickerViewModel.From( State, SelectedTeamId, slotKey, slotLabel );
	public TeamContractsViewModel GetTeamContracts() => TeamContractsViewModel.From( State, SelectedTeamId );
	public FacilitiesViewModel GetFacilities() => FacilitiesViewModel.From( State, SelectedTeamId );
	public TradeCenterViewModel GetTradeCenter() => TradeCenterViewModel.From( State, SelectedTeamId );

	public IReadOnlyList<TradePlayerRow> GetTradePartnerRoster( TeamId partnerId )
		=> TradeCenterViewModel.GetPartnerRoster( State, partnerId );
	public LegacyViewModel GetLegacy() => LegacyViewModel.From( State, SelectedTeamId );
	public TeamProfileViewModel GetTeamProfile( TeamId teamId ) => TeamProfileViewModel.From( State, teamId, SelectedTeamId );
}
