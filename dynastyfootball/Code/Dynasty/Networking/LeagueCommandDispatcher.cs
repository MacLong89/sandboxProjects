using System.Text.Json;
using System.Text.Json.Serialization;
using Dynasty.Core.Commands;
using Dynasty.Persistence;
using Dynasty.Services;

namespace Dynasty.Networking;

/// <summary>
/// Host-side JSON command dispatch for multiplayer clients.
/// </summary>
public static class LeagueCommandDispatcher
{
	static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true,
		Converters =
		{
			new JsonStringEnumConverter( JsonNamingPolicy.CamelCase ),
			new TeamIdJsonConverter(),
			new PlayerIdJsonConverter(),
			new CoachIdJsonConverter(),
			new GameIdJsonConverter(),
			new DraftPickIdJsonConverter()
		}
	};

	public static LeagueCommandResult Dispatch( LeagueService service, string commandType, string payloadJson, ulong expectedRevision, ulong steamId )
	{
		if ( service?.State == null )
			return LeagueCommandResult.Fail( "No league loaded." );

		return commandType switch
		{
			"advance_week" => service.ExecuteCommand( new AdvanceWeekCommand { ExpectedStateRevision = expectedRevision }, steamId, true ),
			"advance_day" => service.ExecuteCommand( new AdvanceDayCommand { ExpectedStateRevision = expectedRevision }, steamId, true ),
			"advance_next_event" => service.ExecuteCommand( new AdvanceToNextEventCommand { ExpectedStateRevision = expectedRevision }, steamId, true ),
			"advance_to_target" => DeserializeAndExecute<AdvanceToTargetCommand>( service, payloadJson, steamId ),
			"release_player" => DeserializeAndExecute<ReleasePlayerCommand>( service, payloadJson, steamId ),
			"extend_contract" => DeserializeAndExecute<ExtendContractCommand>( service, payloadJson, steamId ),
			"submit_trade" => DeserializeAndExecute<SubmitTradeCommand>( service, payloadJson, steamId ),
			"train_player_attribute" => DeserializeAndExecute<TrainPlayerAttributeCommand>( service, payloadJson, steamId ),
			"unlock_player_trait" => DeserializeAndExecute<UnlockPlayerTraitCommand>( service, payloadJson, steamId ),
			"set_depth_chart_starter" => DeserializeAndExecute<SetDepthChartStarterCommand>( service, payloadJson, steamId ),
			"set_team_formation" => DeserializeAndExecute<SetTeamFormationCommand>( service, payloadJson, steamId ),
			"make_draft_pick" => DeserializeAndExecute<MakeDraftPickCommand>( service, payloadJson, steamId ),
			"simulate_draft_pick" => DeserializeAndExecute<SimulateDraftPickCommand>( service, payloadJson, steamId ),
			"sim_draft_to_human" => DeserializeAndExecute<SimDraftToHumanCommand>( service, payloadJson, steamId ),
			"sim_rest_of_draft" => DeserializeAndExecute<SimRestOfDraftCommand>( service, payloadJson, steamId ),
			"sim_smart_draft" => DeserializeAndExecute<SimSmartDraftCommand>( service, payloadJson, steamId ),
			"upgrade_facility" => DeserializeAndExecute<UpgradeFacilityCommand>( service, payloadJson, steamId ),
			"submit_contract_offer" => DeserializeAndExecute<SubmitContractOfferCommand>( service, payloadJson, steamId ),
			"simulate_game" => DeserializeAndExecute<SimulateGameCommand>( service, payloadJson, steamId ),
			"resolve_inbox" => DeserializeAndExecute<ResolveInboxMessageCommand>( service, payloadJson, steamId ),
			"mark_inbox_read" => DeserializeAndExecute<MarkInboxReadCommand>( service, payloadJson, steamId ),
			"set_weekly_game_plan" => DeserializeAndExecute<SetWeeklyGamePlanCommand>( service, payloadJson, steamId ),
			"respond_trade_offer" => DeserializeAndExecute<RespondTradeOfferCommand>( service, payloadJson, steamId ),
			"claim_team" => DeserializeAndExecute<ClaimTeamCommand>( service, payloadJson, steamId ),
			_ => LeagueCommandResult.Fail( $"Unsupported command: {commandType}" )
		};
	}

	static LeagueCommandResult DeserializeAndExecute<T>( LeagueService service, string payloadJson, ulong steamId ) where T : class, ILeagueCommand
	{
		var command = JsonSerializer.Deserialize<T>( payloadJson, JsonOptions );
		if ( command == null )
			return LeagueCommandResult.Fail( "Invalid command payload." );

		return service.ExecuteCommand( command, steamId, true );
	}
}
