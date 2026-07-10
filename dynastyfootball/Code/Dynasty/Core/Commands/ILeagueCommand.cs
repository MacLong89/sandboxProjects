using Dynasty.Core.Enums;
using Dynasty.Core.Identifiers;
using Dynasty.Data;
using Dynasty.Domain.Contracts;
using Dynasty.Domain.League;

namespace Dynasty.Core.Commands;

/// <summary>
/// Server-authoritative command envelope. Clients submit commands; host validates and executes.
/// </summary>
public interface ILeagueCommand
{
	string CommandType { get; }
	Guid CommandId { get; }
	ulong ExpectedStateRevision { get; }
}

public interface ILeagueCommandHandler
{
	string CommandType { get; }
	LeagueCommandResult Handle( LeagueState state, ILeagueCommand command, LeagueCommandContext context );
}

public sealed class LeagueCommandContext
{
	public ulong ExecutingSteamId { get; init; }
	public bool IsHost { get; init; }
	public Core.Events.LeagueEventBus Events { get; init; }
	public Core.Interfaces.ILeagueRandom Random { get; init; }
	public Core.Interfaces.ILeagueClock Clock { get; init; }
}

public sealed class LeagueCommandResult
{
	public bool Success { get; init; }
	public string Error { get; init; }
	public ulong NewStateRevision { get; init; }

	public static LeagueCommandResult Ok( ulong revision ) => new() { Success = true, NewStateRevision = revision };
	public static LeagueCommandResult Fail( string error ) => new() { Success = false, Error = error ?? "" };
}

public sealed class AdvanceWeekCommand : ILeagueCommand
{
	public string CommandType => "advance_week";
	public Guid CommandId { get; init; } = Guid.NewGuid();
	public ulong ExpectedStateRevision { get; init; }
}

public sealed class AdvanceDayCommand : ILeagueCommand
{
	public string CommandType => "advance_day";
	public Guid CommandId { get; init; } = Guid.NewGuid();
	public ulong ExpectedStateRevision { get; init; }
}

public sealed class AdvanceToNextEventCommand : ILeagueCommand
{
	public string CommandType => "advance_next_event";
	public Guid CommandId { get; init; } = Guid.NewGuid();
	public ulong ExpectedStateRevision { get; init; }
}

public sealed class AdvanceToTargetCommand : ILeagueCommand
{
	public string CommandType => "advance_to_target";
	public Guid CommandId { get; init; } = Guid.NewGuid();
	public ulong ExpectedStateRevision { get; init; }
	public TimeAdvanceTarget Target { get; init; }
}

public sealed class ResolveInboxMessageCommand : ILeagueCommand
{
	public string CommandType => "resolve_inbox";
	public Guid CommandId { get; init; } = Guid.NewGuid();
	public ulong ExpectedStateRevision { get; init; }
	public Guid MessageId { get; init; }
}

public sealed class SubmitTradeCommand : ILeagueCommand
{
	public string CommandType => "submit_trade";
	public Guid CommandId { get; init; } = Guid.NewGuid();
	public ulong ExpectedStateRevision { get; init; }
	public TeamId FromTeamId { get; init; }
	public TeamId ToTeamId { get; init; }
	public PlayerId PlayerId { get; init; }
	public PlayerId ReturnPlayerId { get; init; }
}

public sealed class ReleasePlayerCommand : ILeagueCommand
{
	public string CommandType => "release_player";
	public Guid CommandId { get; init; } = Guid.NewGuid();
	public ulong ExpectedStateRevision { get; init; }
	public TeamId TeamId { get; init; }
	public PlayerId PlayerId { get; init; }
}

public sealed class ExtendContractCommand : ILeagueCommand
{
	public string CommandType => "extend_contract";
	public Guid CommandId { get; init; } = Guid.NewGuid();
	public ulong ExpectedStateRevision { get; init; }
	public TeamId TeamId { get; init; }
	public PlayerId PlayerId { get; init; }
	public int Years { get; init; }
	public int AnnualSalary { get; init; }
}

public sealed class TrainPlayerAttributeCommand : ILeagueCommand
{
	public string CommandType => "train_player_attribute";
	public Guid CommandId { get; init; } = Guid.NewGuid();
	public ulong ExpectedStateRevision { get; init; }
	public TeamId TeamId { get; init; }
	public PlayerId PlayerId { get; init; }
	public string AttributeKey { get; init; }
}

public sealed class UnlockPlayerTraitCommand : ILeagueCommand
{
	public string CommandType => "unlock_player_trait";
	public Guid CommandId { get; init; } = Guid.NewGuid();
	public ulong ExpectedStateRevision { get; init; }
	public TeamId TeamId { get; init; }
	public PlayerId PlayerId { get; init; }
}

public sealed class SimulateGameCommand : ILeagueCommand
{
	public string CommandType => "simulate_game";
	public Guid CommandId { get; init; } = Guid.NewGuid();
	public ulong ExpectedStateRevision { get; init; }
	public GameId GameId { get; init; }
}

public sealed class MarkInboxReadCommand : ILeagueCommand
{
	public string CommandType => "mark_inbox_read";
	public Guid CommandId { get; init; } = Guid.NewGuid();
	public ulong ExpectedStateRevision { get; init; }
	public Guid MessageId { get; init; }
}

public sealed class SimulateDraftPickCommand : ILeagueCommand
{
	public string CommandType => "simulate_draft_pick";
	public Guid CommandId { get; init; } = Guid.NewGuid();
	public ulong ExpectedStateRevision { get; init; }
}

public sealed class SimDraftToHumanCommand : ILeagueCommand
{
	public string CommandType => "sim_draft_to_human";
	public Guid CommandId { get; init; } = Guid.NewGuid();
	public ulong ExpectedStateRevision { get; init; }
}

public sealed class SimRestOfDraftCommand : ILeagueCommand
{
	public string CommandType => "sim_rest_of_draft";
	public Guid CommandId { get; init; } = Guid.NewGuid();
	public ulong ExpectedStateRevision { get; init; }
}

public sealed class SimSmartDraftCommand : ILeagueCommand
{
	public string CommandType => "sim_smart_draft";
	public Guid CommandId { get; init; } = Guid.NewGuid();
	public ulong ExpectedStateRevision { get; init; }
}

public sealed class MakeDraftPickCommand : ILeagueCommand
{
	public string CommandType => "make_draft_pick";
	public Guid CommandId { get; init; } = Guid.NewGuid();
	public ulong ExpectedStateRevision { get; init; }
	public PlayerId ProspectId { get; init; }
	public TeamId PickingTeamId { get; init; }
}

public sealed class SubmitContractOfferCommand : ILeagueCommand
{
	public string CommandType => "submit_contract_offer";
	public Guid CommandId { get; init; } = Guid.NewGuid();
	public ulong ExpectedStateRevision { get; init; }
	public ContractOffer Offer { get; init; }
}

public sealed class UpgradeFacilityCommand : ILeagueCommand
{
	public string CommandType => "upgrade_facility";
	public Guid CommandId { get; init; } = Guid.NewGuid();
	public ulong ExpectedStateRevision { get; init; }
	public TeamId TeamId { get; init; }
	public FacilityType FacilityType { get; init; }
}

public sealed class SetDepthChartStarterCommand : ILeagueCommand
{
	public string CommandType => "set_depth_chart_starter";
	public Guid CommandId { get; init; } = Guid.NewGuid();
	public ulong ExpectedStateRevision { get; init; }
	public TeamId TeamId { get; init; }
	public string SlotKey { get; init; } = "";
	public PlayerId PlayerId { get; init; }
}

public sealed class SetTeamFormationCommand : ILeagueCommand
{
	public string CommandType => "set_team_formation";
	public Guid CommandId { get; init; } = Guid.NewGuid();
	public ulong ExpectedStateRevision { get; init; }
	public TeamId TeamId { get; init; }
	public FormationSide Side { get; init; }
	public FormationType FormationType { get; init; }
}

public sealed class SetWeeklyGamePlanCommand : ILeagueCommand
{
	public string CommandType => "set_weekly_game_plan";
	public Guid CommandId { get; init; } = Guid.NewGuid();
	public ulong ExpectedStateRevision { get; init; }
	public TeamId TeamId { get; init; }
	public WeeklyGamePlan Plan { get; init; }
}

public sealed class RespondTradeOfferCommand : ILeagueCommand
{
	public string CommandType => "respond_trade_offer";
	public Guid CommandId { get; init; } = Guid.NewGuid();
	public ulong ExpectedStateRevision { get; init; }
	public Guid OfferId { get; init; }
	public bool Accept { get; init; }
}

public sealed class ClaimTeamCommand : ILeagueCommand
{
	public string CommandType => "claim_team";
	public Guid CommandId { get; init; } = Guid.NewGuid();
	public ulong ExpectedStateRevision { get; init; }
	public string TeamAbbreviation { get; init; } = "";
}
