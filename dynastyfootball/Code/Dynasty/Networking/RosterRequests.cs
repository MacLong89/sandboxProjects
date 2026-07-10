using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dynasty;
using Dynasty.Core.Commands;
using Dynasty.Core.Identifiers;
using Dynasty.Data;
using Dynasty.LeagueNet;
using Dynasty.Persistence;

namespace Dynasty.Networking;

/// <summary>
/// Client-friendly entry point for roster/depth chart commands. Host executes locally; clients RPC to host.
/// </summary>
public static class RosterRequests
{
	static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		Converters =
		{
			new JsonStringEnumConverter( JsonNamingPolicy.CamelCase ),
			new TeamIdJsonConverter(),
			new PlayerIdJsonConverter()
		}
	};

	public static LeagueCommandResult Execute( ILeagueCommand command, ulong steamId = 0 )
	{
		DynastyApp.Initialize();

		if ( DynastyApp.League.State == null )
			return LeagueCommandResult.Fail( "No league loaded." );

		if ( GameNetworking.IsActive && !GameNetworking.IsHost )
		{
			var host = Game.ActiveScene?.GetAllComponents<LeagueHostComponent>().FirstOrDefault();
			if ( host == null )
				return LeagueCommandResult.Fail( "League host not found." );

			var payload = JsonSerializer.Serialize( command, command.GetType(), JsonOptions );
			host.SubmitCommand( command.CommandType, payload, command.ExpectedStateRevision );
			var notification = DynastyApp.LastCommandResult;
			return notification != null && notification.Success
				? LeagueCommandResult.Ok( DynastyApp.League.State.StateRevision )
				: LeagueCommandResult.Fail( notification?.Error ?? "Command failed." );
		}

		return DynastyApp.League.ExecuteCommand( command, steamId, true );
	}

	public static LeagueCommandResult SetDepthChartStarter( TeamId teamId, string slotKey, PlayerId playerId, ulong expectedRevision, ulong steamId = 0 )
		=> Execute( new SetDepthChartStarterCommand
		{
			TeamId = teamId,
			SlotKey = slotKey,
			PlayerId = playerId,
			ExpectedStateRevision = expectedRevision
		}, steamId );

	public static LeagueCommandResult SetTeamFormation( TeamId teamId, FormationSide side, FormationType formationType, ulong expectedRevision, ulong steamId = 0 )
		=> Execute( new SetTeamFormationCommand
		{
			TeamId = teamId,
			Side = side,
			FormationType = formationType,
			ExpectedStateRevision = expectedRevision
		}, steamId );
}
