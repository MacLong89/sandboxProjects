namespace Sandbox;

/// <summary>Single server-driven countdown with one active purpose at a time. <see cref="SyncedRemaining"/> is replicated.</summary>
[Title( "YouAreNotAlone — Server timer" )]
[Category( "YouAreNotAlone" )]
[Icon( "timer" )]
[Order( 5 )]
public sealed class YaServerTimerSystem : Component
{
	[Sync( SyncFlags.FromHost )] public YaTimerPurpose ActivePurpose { get; set; } = YaTimerPurpose.None;

	[Sync( SyncFlags.FromHost )] public float SyncedRemaining { get; set; }

	double _endTime;
	bool _running;

	/// <summary>Host-only: fired once when the active countdown reaches zero.</summary>
	public event Action<YaTimerPurpose> HostExpired;

	/// <summary>Host: start or replace the active countdown.</summary>
	public void HostBegin( YaTimerPurpose purpose, float durationSeconds )
	{
		if ( !Networking.IsHost )
			return;

		if ( durationSeconds <= 0f )
		{
			HostStop();
			return;
		}

		ActivePurpose = purpose;
		_endTime = Time.Now + durationSeconds;
		_running = true;
		SyncedRemaining = durationSeconds;
	}

	public void HostStop()
	{
		if ( !Networking.IsHost )
			return;

		_running = false;
		ActivePurpose = YaTimerPurpose.None;
		SyncedRemaining = 0f;
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost || !_running || ActivePurpose == YaTimerPurpose.None )
			return;

		var left = (float)(_endTime - Time.Now);
		SyncedRemaining = Math.Max( 0f, left );

		if ( left > 0f )
			return;

		_running = false;
		var p = ActivePurpose;
		ActivePurpose = YaTimerPurpose.None;
		SyncedRemaining = 0f;
		var handler = HostExpired;
		if ( handler != null )
			handler( p );
	}
}

/// <summary>Which gameplay phase the timer is measuring.</summary>
public enum YaTimerPurpose
{
	None,
	Intermission,
	Round
}
