using System.Linq;
using Dynasty.Core;
using Dynasty.Core.Commands;
using Dynasty.Core.Enums;
using Dynasty.Core.Events;
using Dynasty.Core.Interfaces;
using Dynasty.Core.Random;
using Dynasty.Data;
using Dynasty.Domain.Factories;
using Dynasty.Domain.League;
using Dynasty.Systems.Chemistry;
using Dynasty.Systems.Coaching;
using Dynasty.Systems.Contracts;
using Dynasty.Systems.DepthChart;
using Dynasty.Systems.Development;
using Dynasty.Systems.Draft;
using Dynasty.Systems.Facilities;
using Dynasty.Systems.Fans;
using Dynasty.Systems.Franchise;
using Dynasty.Systems.History;
using Dynasty.Systems.Injury;
using Dynasty.Systems.News;
using Dynasty.Systems.Retirement;
using Dynasty.Systems.Roster;
using Dynasty.Systems.Runtime;
using Dynasty.Systems.Scouting;
using Dynasty.Systems.Season;
using Dynasty.Systems.Stats;
using Dynasty.Systems.Trade;

namespace Dynasty.Services;

/// <summary>
/// Primary application service. Owns league state and coordinates commands through server-authoritative systems.
/// </summary>
public sealed class LeagueService
{
	private readonly LeagueRuntime _runtime = new();
	private readonly LeagueEventBus _events = new();
	private readonly Dictionary<string, ILeagueCommandHandler> _commandHandlers = new();

	private readonly SeasonSimulationSystem _seasonSim = new();
	private readonly PlayoffBracketSystem _playoffs = new();
	private readonly PlayerDevelopmentSystem _development = new();
	private readonly InjurySystem _injury = new();
	private readonly DraftSystem _draft = new();
	private readonly TradeSystem _trade = new();
	private readonly ContractSystem _contracts = new();
	private readonly ScoutingSystem _scouting = new();
	private readonly NewsSystem _news = new();
	private readonly HistorySystem _history = new();
	private readonly PlayerStatsSystem _playerStats = new();
	private readonly FacilitySystem _facilities = new();
	private readonly FanSystem _fans = new();
	private readonly ChemistrySystem _chemistry = new();
	private readonly RetirementSystem _retirement = new();
	private readonly FranchiseWorkflowSystem _workflow = new();
	private readonly InboxSystem _inbox = new();
	private readonly CoachingCarouselSystem _coachingCarousel = new();
	private readonly SeasonScheduleSystem _seasonSchedule = new();
	private readonly RosterManagementSystem _roster = new();
	private readonly DepthChartSystem _depthChart = new();
	private readonly FranchiseNarrativeSystem _narrative = new();
	private readonly AiFreeAgencySystem _aiFreeAgency = new();
	private readonly PlayerMoraleSystem _playerMorale = new();
	private readonly FranchiseRetentionSystem _franchiseRetention = new();
	private readonly AiTradeSystem _aiTrade = new();
	private readonly WeeklyGamePlanSystem _weeklyGamePlan = new();
	private readonly FranchiseMilestoneSystem _milestones = new();
	private readonly SeasonObjectiveSystem _seasonObjective = new();
	private readonly WeeklyRecapSystem _weeklyRecap = new();
	private readonly FreeAgencyAlertSystem _faAlerts = new();
	private readonly RivalrySystem _rivalry = new();
	private readonly NearMissSystem _nearMiss = new();

	private ILeagueRandom _random;
	private ILeagueClock _clock = new LeagueClock();
	private ILeagueDataDefinitions _definitions = new LeagueDataDefinitions();
	private bool _eventsWired;

	public LeagueState State { get; private set; }
	public LeagueEventBus Events => _events;
	public LeagueRuntime Runtime => _runtime;

	public void CreateNewLeague( LeagueSettings settings = null )
	{
		settings ??= new LeagueSettings();
		_random = new LeagueRandom();
		_definitions = new LeagueDataDefinitions();

		var context = BuildContext();
		_runtime.Configure( context, BuildSystems(), _workflow, _coachingCarousel );
		WireSystemDependencies();
		_retirement.SetHistorySystem( _history );

		State = LeagueFactory.CreateNewLeague( settings, _definitions, _random );
		GmAssignmentHelper.AssignHumanTeam( State, settings.HumanTeamAbbreviation );
		_runtime.OnLeagueCreated( State );
		RegisterCommandHandlers();
		_eventsWired = false;
		WireCrossSystemEvents();
		NotifyStateLoaded( "league_created" );
	}

	public void LoadLeague( LeagueState state )
	{
		_random = new LeagueRandom( state.RandomSeed );
		_definitions = new LeagueDataDefinitions();

		var context = BuildContext();
		_runtime.Configure( context, BuildSystems(), _workflow, _coachingCarousel );
		WireSystemDependencies();
		_retirement.SetHistorySystem( _history );

		State = state;
		GmAssignmentHelper.EnsureSoloHumanGm( State );
		RegisterCommandHandlers();
		_eventsWired = false;
		WireCrossSystemEvents();

		if ( State.Phase == LeaguePhase.Draft && State.Draft.IsActive )
			_draft.EnsurePickClock( State );

		foreach ( var team in State.Teams.Values )
			_depthChart.EnsureTeamDepthChart( State, team.Id );

		SalaryCapHelper.RecalculateAllTeams( State );
		DraftPickRegistry.EnsureFuturePickInventory( State );

		_playoffs.EnsureBracket( State );

		NotifyStateLoaded( "league_loaded" );
	}

	public void UnloadLeague() => State = null;

	void NotifyStateLoaded( string source )
	{
		_events.Publish( new LeagueStateMutatedEvent(
			_events.NextSequence(),
			_clock.UtcNow,
			State.StateRevision,
			source ) );
	}

	void WireCrossSystemEvents()
	{
		if ( _eventsWired )
			return;

		_eventsWired = true;
		_events.Subscribe<PlayerInjuredEvent>( OnPlayerInjured );
		_events.Subscribe<GameSimulatedEvent>( OnGameSimulated );
	}

	void OnPlayerInjured( PlayerInjuredEvent e ) => _news.PublishInjury( State, e );

	void OnGameSimulated( GameSimulatedEvent e ) => _fans.ApplyGameResult( State, e );

	public LeagueCommandResult ExecuteCommand( ILeagueCommand command, ulong steamId, bool isHost )
	{
		if ( State == null )
			return LeagueCommandResult.Fail( "No league loaded." );

		if ( command.ExpectedStateRevision != 0 && command.ExpectedStateRevision != State.StateRevision )
			return LeagueCommandResult.Fail( $"Stale state revision. Expected {command.ExpectedStateRevision}, current {State.StateRevision}." );

		if ( !_commandHandlers.TryGetValue( command.CommandType, out var handler ) )
			return LeagueCommandResult.Fail( $"Unknown command: {command.CommandType}" );

		var ctx = new LeagueCommandContext
		{
			ExecutingSteamId = steamId,
			IsHost = isHost,
			Events = _events,
			Random = _random,
			Clock = _clock
		};

		var result = handler.Handle( State, command, ctx );
		if ( result.Success )
		{
			_events.Publish( new LeagueStateMutatedEvent(
				_events.NextSequence(),
				_clock.UtcNow,
				State.StateRevision,
				command.CommandType ) );
		}

		return result;
	}

	public LeagueCommandResult AdvanceWeek( ulong steamId = 0, bool isHost = true )
		=> ExecuteCommand( new AdvanceWeekCommand { ExpectedStateRevision = State.StateRevision }, steamId, isHost );

	public LeagueCommandResult AdvanceDay( ulong steamId = 0, bool isHost = true )
		=> ExecuteCommand( new AdvanceDayCommand { ExpectedStateRevision = State.StateRevision }, steamId, isHost );

	public LeagueCommandResult AdvanceToNextEvent( ulong steamId = 0, bool isHost = true )
		=> ExecuteCommand( new AdvanceToNextEventCommand { ExpectedStateRevision = State.StateRevision }, steamId, isHost );

	public LeagueCommandResult AdvanceToTarget( TimeAdvanceTarget target, ulong steamId = 0, bool isHost = true )
		=> ExecuteCommand( new AdvanceToTargetCommand { Target = target, ExpectedStateRevision = State.StateRevision }, steamId, isHost );

	public void TickDraftTimer()
	{
		if ( State == null || State.Phase != LeaguePhase.Draft || !State.Draft.IsActive )
			return;

		if ( !_draft.TryProcessTimedPick( State ) )
			return;

		var lastPick = State.Draft.History.LastOrDefault();
		if ( lastPick != null )
			_depthChart.EnsureTeamDepthChart( State, lastPick.TeamId );

		_runtime.CompleteDraftIfFinished( State );
		_events.Publish( new LeagueStateMutatedEvent(
			_events.NextSequence(),
			_clock.UtcNow,
			State.StateRevision,
			"draft_timer_pick" ) );
	}

	LeagueSystemContext BuildContext() => new()
	{
		Events = _events,
		Random = _random,
		Clock = _clock,
		Definitions = _definitions
	};

	IEnumerable<ILeagueSystem> BuildSystems()
	{
		yield return _seasonSim;
		yield return _playoffs;
		yield return _development;
		yield return _injury;
		yield return _draft;
		yield return _trade;
		yield return _contracts;
		yield return _scouting;
		yield return _news;
		yield return _history;
		yield return _playerStats;
		yield return _facilities;
		yield return _fans;
		yield return _chemistry;
		yield return _retirement;
		yield return _workflow;
		yield return _inbox;
		yield return _narrative;
		yield return _coachingCarousel;
		yield return _seasonSchedule;
		yield return _roster;
		yield return _depthChart;
		yield return _aiFreeAgency;
		yield return _playerMorale;
		yield return _franchiseRetention;
		yield return _milestones;
		yield return _seasonObjective;
		yield return _weeklyRecap;
		yield return _faAlerts;
		yield return _aiTrade;
		yield return _weeklyGamePlan;
		yield return _rivalry;
		yield return _nearMiss;
	}

	void WireSystemDependencies()
	{
		_coachingCarousel.SetInboxSystem( _inbox );
		_draft.SetInboxSystem( _inbox );
		_draft.SetNewsSystem( _news );
		_draft.SetFranchiseRetentionSystem( _franchiseRetention );
		_narrative.SetInboxSystem( _inbox );
		_seasonSim.SetNewsSystem( _news );
		_seasonSim.SetPlayoffBracketSystem( _playoffs );
		_seasonSim.SetPlayerMoraleSystem( _playerMorale );
		_seasonSim.SetFranchiseRetentionSystem( _franchiseRetention );
		_seasonSim.SetFranchiseMilestoneSystem( _milestones );
		_seasonSim.SetWeeklyRecapSystem( _weeklyRecap );
		_seasonSim.SetSeasonObjectiveSystem( _seasonObjective );
		_seasonSim.SetRivalrySystem( _rivalry );
		_seasonSim.SetNearMissSystem( _nearMiss );
		_playoffs.SetHistorySystem( _history );
		_playoffs.SetNewsSystem( _news );
		_playoffs.SetFranchiseRetentionSystem( _franchiseRetention );
		_playoffs.SetFranchiseMilestoneSystem( _milestones );
		_playerMorale.SetInboxSystem( _inbox );
		_franchiseRetention.SetInboxSystem( _inbox );
		_milestones.SetInboxSystem( _inbox );
		_milestones.SetFranchiseRetentionSystem( _franchiseRetention );
		_seasonObjective.SetInboxSystem( _inbox );
		_seasonObjective.SetFranchiseRetentionSystem( _franchiseRetention );
		_weeklyRecap.SetInboxSystem( _inbox );
		_faAlerts.SetInboxSystem( _inbox );
		_contracts.SetFreeAgencyAlertSystem( _faAlerts );
		_aiFreeAgency.SetContractSystem( _contracts );
		_trade.SetDepthChartSystem( _depthChart );
		_aiTrade.SetTradeSystem( _trade );
		_aiTrade.SetInboxSystem( _inbox );
		_aiTrade.SetNewsSystem( _news );
		_weeklyGamePlan.SetInboxSystem( _inbox );
		_weeklyGamePlan.SetSeasonObjectiveSystem( _seasonObjective );
		_facilities.SetSeasonObjectiveSystem( _seasonObjective );
		_rivalry.SetInboxSystem( _inbox );
		_rivalry.SetNewsSystem( _news );
		_nearMiss.SetInboxSystem( _inbox );
	}

	void RegisterCommandHandlers()
	{
		_commandHandlers.Clear();
		_commandHandlers["advance_week"] = new AdvanceWeekCommandHandler( _runtime );
		_commandHandlers["advance_day"] = new AdvanceDayCommandHandler( _runtime );
		_commandHandlers["advance_next_event"] = new AdvanceToNextEventCommandHandler( _runtime );
		_commandHandlers["advance_to_target"] = new AdvanceToTargetCommandHandler( _runtime, _workflow, _draft, _depthChart );
		_commandHandlers["resolve_inbox"] = new ResolveInboxCommandHandler();
		_commandHandlers["make_draft_pick"] = new MakeDraftPickCommandHandler( _draft, _inbox, _runtime, _depthChart );
		_commandHandlers["submit_contract_offer"] = new SubmitContractOfferCommandHandler( _contracts );
		_commandHandlers["upgrade_facility"] = new UpgradeFacilityCommandHandler( _facilities );
		_commandHandlers["release_player"] = new ReleasePlayerCommandHandler( _roster, _depthChart );
		_commandHandlers["extend_contract"] = new ExtendContractCommandHandler( _roster );
		_commandHandlers["submit_trade"] = new SubmitTradeCommandHandler( _roster, _trade, _depthChart );
		_commandHandlers["train_player_attribute"] = new TrainPlayerAttributeCommandHandler( _roster );
		_commandHandlers["unlock_player_trait"] = new UnlockPlayerTraitCommandHandler( _roster );
		_commandHandlers["simulate_game"] = new SimulateGameCommandHandler( _seasonSim );
		_commandHandlers["mark_inbox_read"] = new MarkInboxReadCommandHandler();
		_commandHandlers["simulate_draft_pick"] = new SimulateDraftPickCommandHandler( _draft, _runtime, _depthChart );
		_commandHandlers["sim_draft_to_human"] = new SimDraftToHumanCommandHandler( _draft, _runtime, _depthChart );
		_commandHandlers["sim_rest_of_draft"] = new SimRestOfDraftCommandHandler( _draft, _runtime, _depthChart );
		_commandHandlers["sim_smart_draft"] = new SimSmartDraftCommandHandler( _draft, _runtime, _depthChart );
		_commandHandlers["set_depth_chart_starter"] = new SetDepthChartStarterCommandHandler( _depthChart );
		_commandHandlers["set_team_formation"] = new SetTeamFormationCommandHandler( _depthChart );
		_commandHandlers["set_weekly_game_plan"] = new SetWeeklyGamePlanCommandHandler( _weeklyGamePlan );
		_commandHandlers["respond_trade_offer"] = new RespondTradeOfferCommandHandler( _trade, _inbox );
		_commandHandlers["claim_team"] = new ClaimTeamCommandHandler();
	}
}

internal sealed class AdvanceDayCommandHandler : ILeagueCommandHandler
{
	private readonly LeagueRuntime _runtime;
	public AdvanceDayCommandHandler( LeagueRuntime runtime ) => _runtime = runtime;
	public string CommandType => "advance_day";

	public LeagueCommandResult Handle( LeagueState state, ILeagueCommand command, LeagueCommandContext context )
		=> _runtime.AdvanceDay( state )
			? LeagueCommandResult.Ok( state.StateRevision )
			: LeagueCommandResult.Fail( FranchiseWorkflowSystem.GetBlockingReason( state ) );
}

internal sealed class AdvanceToNextEventCommandHandler : ILeagueCommandHandler
{
	private readonly LeagueRuntime _runtime;
	public AdvanceToNextEventCommandHandler( LeagueRuntime runtime ) => _runtime = runtime;
	public string CommandType => "advance_next_event";

	public LeagueCommandResult Handle( LeagueState state, ILeagueCommand command, LeagueCommandContext context )
		=> _runtime.AdvanceToNextEvent( state )
			? LeagueCommandResult.Ok( state.StateRevision )
			: LeagueCommandResult.Fail( FranchiseWorkflowSystem.GetBlockingReason( state ) );
}

internal sealed class ResolveInboxCommandHandler : ILeagueCommandHandler
{
	public string CommandType => "resolve_inbox";

	public LeagueCommandResult Handle( LeagueState state, ILeagueCommand command, LeagueCommandContext context )
	{
		if ( command is not ResolveInboxMessageCommand resolve )
			return LeagueCommandResult.Fail( "Invalid command payload." );

		if ( !InboxSystem.TryResolve( state, resolve.MessageId, out var error ) )
			return LeagueCommandResult.Fail( error );

		return LeagueCommandResult.Ok( state.StateRevision );
	}
}

internal sealed class AdvanceWeekCommandHandler : ILeagueCommandHandler
{
	private readonly LeagueRuntime _runtime;

	public AdvanceWeekCommandHandler( LeagueRuntime runtime ) => _runtime = runtime;

	public string CommandType => "advance_week";

	public LeagueCommandResult Handle( LeagueState state, ILeagueCommand command, LeagueCommandContext context )
		=> _runtime.AdvanceWeek( state )
			? LeagueCommandResult.Ok( state.StateRevision )
			: LeagueCommandResult.Fail( FranchiseWorkflowSystem.GetBlockingReason( state ) );
}

internal sealed class AdvanceToTargetCommandHandler : ILeagueCommandHandler
{
	private readonly LeagueTimeAdvance _timeAdvance = new();
	private readonly LeagueRuntime _runtime;
	private readonly FranchiseWorkflowSystem _workflow;
	private readonly DraftSystem _draft;
	private readonly DepthChartSystem _depthChart;

	public AdvanceToTargetCommandHandler(
		LeagueRuntime runtime,
		FranchiseWorkflowSystem workflow,
		DraftSystem draft,
		DepthChartSystem depthChart )
	{
		_runtime = runtime;
		_workflow = workflow;
		_draft = draft;
		_depthChart = depthChart;
	}

	public string CommandType => "advance_to_target";

	public LeagueCommandResult Handle( LeagueState state, ILeagueCommand command, LeagueCommandContext context )
	{
		if ( command is not AdvanceToTargetCommand advance )
			return LeagueCommandResult.Fail( "Invalid command payload." );

		if ( !_timeAdvance.TryAdvanceToTarget( state, advance.Target, _runtime, _workflow, _draft, out var error ) )
			return LeagueCommandResult.Fail( error );

		_depthChart.EnsureAllTeamsDepthCharts( state );
		_runtime.CompleteDraftIfFinished( state );
		return LeagueCommandResult.Ok( state.StateRevision );
	}
}

internal sealed class MakeDraftPickCommandHandler : ILeagueCommandHandler
{
	private readonly DraftSystem _draft;
	private readonly InboxSystem _inbox;
	private readonly LeagueRuntime _runtime;
	private readonly DepthChartSystem _depthChart;

	public MakeDraftPickCommandHandler( DraftSystem draft, InboxSystem inbox, LeagueRuntime runtime, DepthChartSystem depthChart )
	{
		_draft = draft;
		_inbox = inbox;
		_runtime = runtime;
		_depthChart = depthChart;
	}

	public string CommandType => "make_draft_pick";

	public LeagueCommandResult Handle( LeagueState state, ILeagueCommand command, LeagueCommandContext context )
	{
		if ( command is not MakeDraftPickCommand pick )
			return LeagueCommandResult.Fail( "Invalid command payload." );

		if ( !GmAssignmentHelper.TryAuthorizeTeamCommand( state, pick.PickingTeamId, context.ExecutingSteamId, out var authError ) )
			return LeagueCommandResult.Fail( authError );

		if ( !_draft.TryMakePick( state, pick.PickingTeamId, pick.ProspectId ) )
			return LeagueCommandResult.Fail( "Invalid draft pick." );

		_depthChart.EnsureTeamDepthChart( state, pick.PickingTeamId );

		foreach ( var msg in state.Inbox.Where( m => m.Category == InboxCategory.Draft && !m.IsResolved ).ToList() )
			msg.IsResolved = true;

		_runtime.CompleteDraftIfFinished( state );
		_draft.NotifyHumanOnClockPublic( state );
		return LeagueCommandResult.Ok( state.StateRevision );
	}
}

internal sealed class SubmitContractOfferCommandHandler : ILeagueCommandHandler
{
	private readonly ContractSystem _contracts;

	public SubmitContractOfferCommandHandler( ContractSystem contracts ) => _contracts = contracts;

	public string CommandType => "submit_contract_offer";

	public LeagueCommandResult Handle( LeagueState state, ILeagueCommand command, LeagueCommandContext context )
	{
		if ( command is not SubmitContractOfferCommand offerCmd )
			return LeagueCommandResult.Fail( "Invalid command payload." );

		if ( !GmAssignmentHelper.TryAuthorizeTeamCommand( state, offerCmd.Offer.TeamId, context.ExecutingSteamId, out var authError ) )
			return LeagueCommandResult.Fail( authError );

		if ( !_contracts.SubmitOffer( state, offerCmd.Offer ) )
			return LeagueCommandResult.Fail( "Offer rejected — check cap space and free agency window." );

		return LeagueCommandResult.Ok( state.StateRevision );
	}
}

internal sealed class UpgradeFacilityCommandHandler : ILeagueCommandHandler
{
	private readonly FacilitySystem _facilities;

	public UpgradeFacilityCommandHandler( FacilitySystem facilities ) => _facilities = facilities;

	public string CommandType => "upgrade_facility";

	public LeagueCommandResult Handle( LeagueState state, ILeagueCommand command, LeagueCommandContext context )
	{
		if ( command is not UpgradeFacilityCommand upgrade )
			return LeagueCommandResult.Fail( "Invalid command payload." );

		if ( !GmAssignmentHelper.TryAuthorizeTeamCommand( state, upgrade.TeamId, context.ExecutingSteamId, out var authError ) )
			return LeagueCommandResult.Fail( authError );

		if ( !_facilities.TryUpgrade( state, upgrade.TeamId, upgrade.FacilityType ) )
			return LeagueCommandResult.Fail( "Upgrade failed." );

		return LeagueCommandResult.Ok( state.StateRevision );
	}
}

internal sealed class ReleasePlayerCommandHandler : ILeagueCommandHandler
{
	private readonly RosterManagementSystem _roster;
	private readonly DepthChartSystem _depthChart;

	public ReleasePlayerCommandHandler( RosterManagementSystem roster, DepthChartSystem depthChart )
	{
		_roster = roster;
		_depthChart = depthChart;
	}

	public string CommandType => "release_player";

	public LeagueCommandResult Handle( LeagueState state, ILeagueCommand command, LeagueCommandContext context )
	{
		if ( command is not ReleasePlayerCommand release )
			return LeagueCommandResult.Fail( "Invalid command payload." );

		if ( !GmAssignmentHelper.TryAuthorizeTeamCommand( state, release.TeamId, context.ExecutingSteamId, out var authError ) )
			return LeagueCommandResult.Fail( authError );

		if ( !_roster.ReleasePlayer( state, release.TeamId, release.PlayerId ) )
			return LeagueCommandResult.Fail( "Could not release player." );

		_depthChart.OnPlayerReleased( state, release.TeamId, release.PlayerId );
		return LeagueCommandResult.Ok( state.StateRevision );
	}
}

internal sealed class ExtendContractCommandHandler : ILeagueCommandHandler
{
	private readonly RosterManagementSystem _roster;

	public ExtendContractCommandHandler( RosterManagementSystem roster ) => _roster = roster;

	public string CommandType => "extend_contract";

	public LeagueCommandResult Handle( LeagueState state, ILeagueCommand command, LeagueCommandContext context )
	{
		if ( command is not ExtendContractCommand extend )
			return LeagueCommandResult.Fail( "Invalid command payload." );

		if ( !GmAssignmentHelper.TryAuthorizeTeamCommand( state, extend.TeamId, context.ExecutingSteamId, out var authError ) )
			return LeagueCommandResult.Fail( authError );

		if ( !_roster.ExtendContract( state, extend.TeamId, extend.PlayerId, extend.Years, extend.AnnualSalary ) )
			return LeagueCommandResult.Fail( "Extension failed. Check cap space and terms." );

		return LeagueCommandResult.Ok( state.StateRevision );
	}
}

internal sealed class SubmitTradeCommandHandler : ILeagueCommandHandler
{
	private readonly RosterManagementSystem _roster;
	private readonly TradeSystem _trade;
	private readonly DepthChartSystem _depthChart;

	public SubmitTradeCommandHandler( RosterManagementSystem roster, TradeSystem trade, DepthChartSystem depthChart )
	{
		_roster = roster;
		_trade = trade;
		_depthChart = depthChart;
	}

	public string CommandType => "submit_trade";

	public LeagueCommandResult Handle( LeagueState state, ILeagueCommand command, LeagueCommandContext context )
	{
		if ( command is not SubmitTradeCommand tradeCmd )
			return LeagueCommandResult.Fail( "Invalid command payload." );

		if ( !GmAssignmentHelper.TryAuthorizeTeamCommand( state, tradeCmd.FromTeamId, context.ExecutingSteamId, out var authError ) )
			return LeagueCommandResult.Fail( authError );

		if ( tradeCmd.ReturnPlayerId.IsEmpty )
			return LeagueCommandResult.Fail( "Select a player to receive in the trade." );

		var proposal = _roster.BuildPlayerTradeProposal(
			state,
			tradeCmd.FromTeamId,
			tradeCmd.PlayerId,
			tradeCmd.ToTeamId,
			tradeCmd.ReturnPlayerId );

		if ( proposal == null )
			return LeagueCommandResult.Fail( "Invalid trade — check both players are on the correct teams." );

		if ( !TradeSystem.IsTradeWindowOpen( state ) )
			return LeagueCommandResult.Fail( TradeSystem.GetTradeWindowMessage( state ) );

		if ( !_trade.TryExecute( state, proposal ) )
			return LeagueCommandResult.Fail( "Trade rejected by AI. Offer was not fair enough." );

		return LeagueCommandResult.Ok( state.StateRevision );
	}
}

internal sealed class TrainPlayerAttributeCommandHandler : ILeagueCommandHandler
{
	private readonly RosterManagementSystem _roster;

	public TrainPlayerAttributeCommandHandler( RosterManagementSystem roster ) => _roster = roster;

	public string CommandType => "train_player_attribute";

	public LeagueCommandResult Handle( LeagueState state, ILeagueCommand command, LeagueCommandContext context )
	{
		if ( command is not TrainPlayerAttributeCommand train )
			return LeagueCommandResult.Fail( "Invalid command payload." );

		if ( !GmAssignmentHelper.TryAuthorizeTeamCommand( state, train.TeamId, context.ExecutingSteamId, out var authError ) )
			return LeagueCommandResult.Fail( authError );

		if ( !_roster.TrainAttribute( state, train.TeamId, train.PlayerId, train.AttributeKey ) )
			return LeagueCommandResult.Fail( "Training failed. Need more development points." );

		return LeagueCommandResult.Ok( state.StateRevision );
	}
}

internal sealed class UnlockPlayerTraitCommandHandler : ILeagueCommandHandler
{
	private readonly RosterManagementSystem _roster;

	public UnlockPlayerTraitCommandHandler( RosterManagementSystem roster ) => _roster = roster;

	public string CommandType => "unlock_player_trait";

	public LeagueCommandResult Handle( LeagueState state, ILeagueCommand command, LeagueCommandContext context )
	{
		if ( command is not UnlockPlayerTraitCommand unlock )
			return LeagueCommandResult.Fail( "Invalid command payload." );

		if ( !GmAssignmentHelper.TryAuthorizeTeamCommand( state, unlock.TeamId, context.ExecutingSteamId, out var authError ) )
			return LeagueCommandResult.Fail( authError );

		if ( !_roster.UnlockPositiveTrait( state, unlock.TeamId, unlock.PlayerId ) )
			return LeagueCommandResult.Fail( "Trait unlock failed." );

		return LeagueCommandResult.Ok( state.StateRevision );
	}
}

internal sealed class SimulateGameCommandHandler : ILeagueCommandHandler
{
	private readonly SeasonSimulationSystem _seasonSim;

	public SimulateGameCommandHandler( SeasonSimulationSystem seasonSim ) => _seasonSim = seasonSim;

	public string CommandType => "simulate_game";

	public LeagueCommandResult Handle( LeagueState state, ILeagueCommand command, LeagueCommandContext context )
	{
		if ( command is not SimulateGameCommand sim )
			return LeagueCommandResult.Fail( "Invalid command payload." );

		if ( context.ExecutingSteamId != 0 )
		{
			var game = state.Schedule.Games.FirstOrDefault( g => g.Id.Value == sim.GameId.Value );
			if ( game == null )
				return LeagueCommandResult.Fail( "Game not found." );

			var controlsHome = GmAssignmentHelper.TryAuthorizeTeamCommand( state, game.HomeTeamId, context.ExecutingSteamId, out _ );
			var controlsAway = GmAssignmentHelper.TryAuthorizeTeamCommand( state, game.AwayTeamId, context.ExecutingSteamId, out _ );
			if ( !controlsHome && !controlsAway )
				return LeagueCommandResult.Fail( "You do not control either team in this game." );
		}

		if ( !_seasonSim.TrySimulateGame( state, sim.GameId ) )
			return LeagueCommandResult.Fail( "Game cannot be simulated right now." );

		return LeagueCommandResult.Ok( state.StateRevision );
	}
}

internal sealed class MarkInboxReadCommandHandler : ILeagueCommandHandler
{
	public string CommandType => "mark_inbox_read";

	public LeagueCommandResult Handle( LeagueState state, ILeagueCommand command, LeagueCommandContext context )
	{
		if ( command is not MarkInboxReadCommand read )
			return LeagueCommandResult.Fail( "Invalid command payload." );

		InboxSystem.MarkRead( state, read.MessageId );
		return LeagueCommandResult.Ok( state.StateRevision );
	}
}

internal sealed class SimulateDraftPickCommandHandler : ILeagueCommandHandler
{
	private readonly DraftSystem _draft;
	private readonly LeagueRuntime _runtime;
	private readonly DepthChartSystem _depthChart;

	public SimulateDraftPickCommandHandler( DraftSystem draft, LeagueRuntime runtime, DepthChartSystem depthChart )
	{
		_draft = draft;
		_runtime = runtime;
		_depthChart = depthChart;
	}

	public string CommandType => "simulate_draft_pick";

	public LeagueCommandResult Handle( LeagueState state, ILeagueCommand command, LeagueCommandContext context )
	{
		var current = state.Draft.Order.ElementAtOrDefault( state.Draft.CurrentPickIndex );
		if ( !_draft.TrySimulateOnePick( state ) )
			return LeagueCommandResult.Fail( "No AI pick to simulate." );

		if ( current != null )
			_depthChart.EnsureTeamDepthChart( state, current.TeamId );

		_runtime.CompleteDraftIfFinished( state );
		return LeagueCommandResult.Ok( state.StateRevision );
	}
}

internal sealed class SimDraftToHumanCommandHandler : ILeagueCommandHandler
{
	private readonly DraftSystem _draft;
	private readonly LeagueRuntime _runtime;
	private readonly DepthChartSystem _depthChart;

	public SimDraftToHumanCommandHandler( DraftSystem draft, LeagueRuntime runtime, DepthChartSystem depthChart )
	{
		_draft = draft;
		_runtime = runtime;
		_depthChart = depthChart;
	}

	public string CommandType => "sim_draft_to_human";

	public LeagueCommandResult Handle( LeagueState state, ILeagueCommand command, LeagueCommandContext context )
	{
		var count = _draft.SimulateToHumanPick( state );
		_depthChart.EnsureAllTeamsDepthCharts( state );
		_runtime.CompleteDraftIfFinished( state );
		if ( count == 0 && state.Draft.IsActive )
			return LeagueCommandResult.Fail( "You are already on the clock." );

		return LeagueCommandResult.Ok( state.StateRevision );
	}
}

internal sealed class SimRestOfDraftCommandHandler : ILeagueCommandHandler
{
	private readonly DraftSystem _draft;
	private readonly LeagueRuntime _runtime;
	private readonly DepthChartSystem _depthChart;

	public SimRestOfDraftCommandHandler( DraftSystem draft, LeagueRuntime runtime, DepthChartSystem depthChart )
	{
		_draft = draft;
		_runtime = runtime;
		_depthChart = depthChart;
	}

	public string CommandType => "sim_rest_of_draft";

	public LeagueCommandResult Handle( LeagueState state, ILeagueCommand command, LeagueCommandContext context )
	{
		if ( !state.Draft.IsActive )
			return LeagueCommandResult.Fail( "The draft is not active." );

		var count = _draft.SimulateRestOfDraft( state );
		_depthChart.EnsureAllTeamsDepthCharts( state );
		_runtime.CompleteDraftIfFinished( state );
		if ( count == 0 )
			return LeagueCommandResult.Fail( "No picks left to simulate." );

		return LeagueCommandResult.Ok( state.StateRevision );
	}
}

internal sealed class SimSmartDraftCommandHandler : ILeagueCommandHandler
{
	private readonly DraftSystem _draft;
	private readonly LeagueRuntime _runtime;
	private readonly DepthChartSystem _depthChart;

	public SimSmartDraftCommandHandler( DraftSystem draft, LeagueRuntime runtime, DepthChartSystem depthChart )
	{
		_draft = draft;
		_runtime = runtime;
		_depthChart = depthChart;
	}

	public string CommandType => "sim_smart_draft";

	public LeagueCommandResult Handle( LeagueState state, ILeagueCommand command, LeagueCommandContext context )
	{
		if ( !state.Draft.IsActive )
			return LeagueCommandResult.Fail( "The draft is not active." );

		var count = _draft.SimulateSmartDraft( state );
		if ( count == 0 )
			return LeagueCommandResult.Fail( "Nothing to simulate — you're on the clock or draft is complete." );

		_depthChart.EnsureAllTeamsDepthCharts( state );
		_runtime.CompleteDraftIfFinished( state );
		return LeagueCommandResult.Ok( state.StateRevision );
	}
}

internal sealed class SetDepthChartStarterCommandHandler : ILeagueCommandHandler
{
	private readonly DepthChartSystem _depthChart;

	public SetDepthChartStarterCommandHandler( DepthChartSystem depthChart ) => _depthChart = depthChart;

	public string CommandType => "set_depth_chart_starter";

	public LeagueCommandResult Handle( LeagueState state, ILeagueCommand command, LeagueCommandContext context )
	{
		if ( command is not SetDepthChartStarterCommand set )
			return LeagueCommandResult.Fail( "Invalid command payload." );

		if ( !_depthChart.TrySetStarter( state, set.TeamId, set.SlotKey, set.PlayerId, context.ExecutingSteamId, context.IsHost, out var error ) )
			return LeagueCommandResult.Fail( error );

		return LeagueCommandResult.Ok( state.StateRevision );
	}
}

internal sealed class SetTeamFormationCommandHandler : ILeagueCommandHandler
{
	private readonly DepthChartSystem _depthChart;

	public SetTeamFormationCommandHandler( DepthChartSystem depthChart ) => _depthChart = depthChart;

	public string CommandType => "set_team_formation";

	public LeagueCommandResult Handle( LeagueState state, ILeagueCommand command, LeagueCommandContext context )
	{
		if ( command is not SetTeamFormationCommand set )
			return LeagueCommandResult.Fail( "Invalid command payload." );

		if ( !_depthChart.TrySetFormation( state, set.TeamId, set.Side, set.FormationType, context.ExecutingSteamId, context.IsHost, out var error ) )
			return LeagueCommandResult.Fail( error );

		return LeagueCommandResult.Ok( state.StateRevision );
	}
}

internal sealed class SetWeeklyGamePlanCommandHandler : ILeagueCommandHandler
{
	private readonly WeeklyGamePlanSystem _gamePlan;

	public SetWeeklyGamePlanCommandHandler( WeeklyGamePlanSystem gamePlan ) => _gamePlan = gamePlan;

	public string CommandType => "set_weekly_game_plan";

	public LeagueCommandResult Handle( LeagueState state, ILeagueCommand command, LeagueCommandContext context )
	{
		if ( command is not SetWeeklyGamePlanCommand set )
			return LeagueCommandResult.Fail( "Invalid command payload." );

		if ( !DepthChartSystem.CanUserControlTeam( state, set.TeamId, context.ExecutingSteamId ) && context.ExecutingSteamId != 0 )
			return LeagueCommandResult.Fail( "You do not control this team." );

		if ( !_gamePlan.TrySetGamePlan( state, set.TeamId, set.Plan ) )
			return LeagueCommandResult.Fail( "Could not set game plan for this week." );

		foreach ( var msg in state.Inbox.Where( m =>
			!m.IsResolved && m.Subject == "Set this week's game plan" && m.TeamId.Value == set.TeamId.Value ) )
		{
			msg.IsResolved = true;
			msg.IsRead = true;
		}

		return LeagueCommandResult.Ok( state.StateRevision );
	}
}

internal sealed class RespondTradeOfferCommandHandler : ILeagueCommandHandler
{
	private readonly TradeSystem _trade;

	public RespondTradeOfferCommandHandler( TradeSystem trade, InboxSystem inbox ) => _trade = trade;

	public string CommandType => "respond_trade_offer";

	public LeagueCommandResult Handle( LeagueState state, ILeagueCommand command, LeagueCommandContext context )
	{
		if ( command is not RespondTradeOfferCommand respond )
			return LeagueCommandResult.Fail( "Invalid command payload." );

		var offer = state.PendingTradeOffers.FirstOrDefault( o => o.OfferId == respond.OfferId );
		if ( offer == null )
			return LeagueCommandResult.Fail( "Trade offer expired or not found." );

		if ( !DepthChartSystem.CanUserControlTeam( state, offer.ToTeamId, context.ExecutingSteamId ) && context.ExecutingSteamId != 0 )
			return LeagueCommandResult.Fail( "You do not control this team." );

		if ( respond.Accept )
		{
			if ( !_trade.TryAcceptPendingOffer( state, respond.OfferId ) )
				return LeagueCommandResult.Fail( "Trade could not be completed — assets may have changed." );
		}
		else
		{
			_trade.RejectPendingOffer( state, respond.OfferId );
		}

		foreach ( var msg in state.Inbox.Where( m =>
			!m.IsResolved && m.Category == InboxCategory.Trade && m.Subject.StartsWith( "Trade offer:" ) ).ToList() )
		{
			msg.IsResolved = true;
			msg.IsRead = true;
		}

		return LeagueCommandResult.Ok( state.StateRevision );
	}
}

internal sealed class ClaimTeamCommandHandler : ILeagueCommandHandler
{
	public string CommandType => "claim_team";

	public LeagueCommandResult Handle( LeagueState state, ILeagueCommand command, LeagueCommandContext context )
	{
		if ( command is not ClaimTeamCommand claim )
			return LeagueCommandResult.Fail( "Invalid command payload." );

		if ( !GmAssignmentHelper.TryClaimTeam( state, context.ExecutingSteamId, claim.TeamAbbreviation, out var error ) )
			return LeagueCommandResult.Fail( error );

		state.BumpRevision( "team_claimed" );
		return LeagueCommandResult.Ok( state.StateRevision );
	}
}
