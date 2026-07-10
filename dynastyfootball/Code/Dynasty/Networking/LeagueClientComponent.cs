using Dynasty.Persistence;
using Dynasty.Services;

namespace Dynasty.LeagueNet;

/// <summary>
/// Client-side read-only league mirror. Applies host snapshots; never runs simulation.
/// </summary>
[Title( "League Client" )]
[Category( "Dynasty" )]
[Icon( "cloud_download" )]
public sealed class LeagueClientComponent : Component
{
	[Property] public LeagueHostComponent Host { get; set; }

	private ulong _lastAppliedRevision;

	protected override void OnUpdate()
	{
		if ( Scene.IsEditor || Host == null || !Host.LeagueReady )
			return;

		if ( Host.StateRevision == _lastAppliedRevision )
			return;

		if ( string.IsNullOrEmpty( Host.LeagueSnapshotJson ) )
			return;

		var state = LeagueSaveSerializer.Deserialize( Host.LeagueSnapshotJson );
		DynastyApp.Initialize( DynastyApp.League );
		DynastyApp.League.LoadLeague( state );
		_lastAppliedRevision = Host.StateRevision;
	}
}
