namespace Sandbox;

/// <summary>Host-authoritative elimination events replicated to every client's kill feed.</summary>
public static class YaKillFeed
{
	public readonly struct Entry
	{
		public string Killer { get; init; }
		public YaPlayerRole KillerRole { get; init; }
		public string Victim { get; init; }
		public YaPlayerRole VictimRole { get; init; }
	}

	static readonly Queue<Entry> _pendingLocal = new();

	public static void HostNotifyElimination( GameObject attackerRoot, GameObject victimRoot )
	{
		if ( !Networking.IsHost || !attackerRoot.IsValid() || !victimRoot.IsValid() || attackerRoot == victimRoot )
			return;

		var flow = YaGameStateSystem.Instance;
		if ( !flow.IsValid() )
			return;

		flow.RpcPushKillFeedEntry(
			YaHudMatchSnapshot.GetPawnDisplayName( attackerRoot ),
			(int)YaTeamSystem.GetRole( attackerRoot ),
			YaHudMatchSnapshot.GetPawnDisplayName( victimRoot ),
			(int)YaTeamSystem.GetRole( victimRoot ) );
	}

	public static void HostNotifyInfo( string message )
	{
		if ( !Networking.IsHost || string.IsNullOrWhiteSpace( message ) )
			return;

		var flow = YaGameStateSystem.Instance;
		if ( !flow.IsValid() )
			return;

		flow.RpcPushKillFeedInfo( message.Trim() );
	}

	public static void EnqueueLocal( Entry entry ) => _pendingLocal.Enqueue( entry );

	public static void EnqueueLocalInfo( string message )
	{
		if ( string.IsNullOrWhiteSpace( message ) )
			return;

		_pendingLocal.Enqueue( new Entry
		{
			Killer = "",
			KillerRole = YaPlayerRole.Unassigned,
			Victim = message.Trim(),
			VictimRole = YaPlayerRole.Unassigned
		} );
	}

	public static void DrainPendingTo( YaKillFeedPanel panel )
	{
		if ( panel is null || !panel.IsValid() )
			return;

		while ( _pendingLocal.Count > 0 )
		{
			var entry = _pendingLocal.Dequeue();
			if ( string.IsNullOrWhiteSpace( entry.Killer ) )
				panel.PushInfo( entry.Victim );
			else
				panel.PushElimination( entry.Killer, entry.KillerRole, entry.Victim, entry.VictimRole );
		}
	}
}
