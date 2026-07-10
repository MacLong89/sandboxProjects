using Dynasty.Domain.League;

namespace Dynasty.Bootstrap;

/// <summary>
/// Scene entry point. Attach to a persistent GameObject in the startup scene.
/// </summary>
[Title( "Dynasty Bootstrap" )]
[Category( "Dynasty" )]
[Icon( "play_arrow" )]
public sealed class DynastyBootstrap : Component
{
	[Property] public bool CreateOfflineLobby { get; set; } = true;
	[Property] public bool AutoCreateLeague { get; set; } = false;
	[Property] public string LeagueName { get; set; } = "Sunday Dynasty";

	protected override async Task OnLoad()
	{
		if ( Scene.IsEditor )
			return;

		DynastyApp.Initialize();

		if ( CreateOfflineLobby && !GameNetworking.IsActive )
		{
			LoadingScreen.Title = "Starting Dynasty";
			await Task.DelayRealtimeSeconds( 0.1f );
			GameNetworking.CreateLobby( new() );
		}
	}

	protected override void OnStart()
	{
		if ( Scene.IsEditor || !GameNetworking.IsHost || !AutoCreateLeague )
			return;

		if ( DynastyApp.League.State != null )
			return;

		DynastyApp.League.CreateNewLeague( new LeagueSettings { LeagueName = LeagueName } );
	}
}
