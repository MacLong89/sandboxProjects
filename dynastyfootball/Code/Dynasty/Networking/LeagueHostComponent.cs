using Dynasty.Core.Commands;
using Dynasty.Domain.League;
using Dynasty.Networking;
using Dynasty.Persistence;
using Dynasty.Services;

namespace Dynasty.LeagueNet;

/// <summary>
/// Server-authoritative league host. All GM commands and simulation execute here.
/// Clients receive replicated snapshots via Sync properties.
/// </summary>
[Title( "League Host" )]
[Category( "Dynasty" )]
[Icon( "sports_football" )]
public sealed class LeagueHostComponent : Component
{
	[Sync( SyncFlags.FromHost )] public ulong StateRevision { get; private set; }
	[Sync( SyncFlags.FromHost )] public string LeagueSnapshotJson { get; private set; } = "";
	[Sync( SyncFlags.FromHost )] public bool LeagueReady { get; private set; }

	[Property] public bool AutoCreateLeague { get; set; } = false;
	[Property] public string LeagueName { get; set; } = "Sunday Dynasty";

	public LeagueService LeagueService => DynastyApp.League;

	protected override void OnStart()
	{
		if ( Scene.IsEditor )
			return;

		DynastyApp.Initialize( LeagueService );

		if ( !GameNetworking.IsHost )
			return;

		if ( AutoCreateLeague && LeagueService.State == null )
		{
			LeagueService.CreateNewLeague( new LeagueSettings { LeagueName = LeagueName } );
			PushSnapshot();
		}

		DynastyApp.League.Events.Subscribe<Core.Events.LeagueStateMutatedEvent>( OnLeagueStateMutated );
	}

	void OnLeagueStateMutated( Core.Events.LeagueStateMutatedEvent _ ) => PushSnapshot();

	[Rpc.Host]
	public void SubmitCommand( string commandType, string payloadJson, ulong expectedRevision )
	{
		if ( !GameNetworking.IsHost || LeagueService.State == null )
			return;

		ulong callerId = 0;
		var caller = Rpc.Caller;
		if ( caller != null )
			callerId = caller.SteamId;

		var result = DispatchCommand( commandType, payloadJson, expectedRevision, callerId );
		DynastyApp.LastCommandResult = new CommandResultNotification
		{
			Success = result.Success,
			Error = result.Error ?? ""
		};

		if ( !result.Success )
			Log.Warning( $"Command '{commandType}' failed: {result.Error}" );
	}

	LeagueCommandResult DispatchCommand( string commandType, string payloadJson, ulong expectedRevision, ulong steamId )
		=> LeagueCommandDispatcher.Dispatch( LeagueService, commandType, payloadJson, expectedRevision, steamId );

	void PushSnapshot()
	{
		if ( LeagueService.State == null )
			return;

		StateRevision = LeagueService.State.StateRevision;
		LeagueSnapshotJson = LeagueSaveSerializer.Serialize( LeagueService.State );
		LeagueReady = true;
	}
}

public sealed class CommandResultNotification
{
	public bool Success { get; set; }
	public string Error { get; set; } = "";
}
