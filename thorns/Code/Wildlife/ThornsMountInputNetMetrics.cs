namespace Sandbox;

using System.Threading;

/// <summary>Lightweight rolling counters for mount steer RPCs (developer HUD / profiling).</summary>
public static class ThornsMountInputNetMetrics
{
	static readonly object _windowLock = new();

	static double _windowStart = -1.0;
	static int _sentWindow;
	static int _recvWindow;

	/// <summary>Local owner: steer RPCs sent per second (last completed ~1s window).</summary>
	public static float ClientMountSteerSentPerSec { get; private set; }

	/// <summary>Host: accepted steer RPCs per second (all riders, last window).</summary>
	public static float HostMountSteerRecvPerSec { get; private set; }

	/// <summary>Host: <see cref="ThornsWildlifeIdentity"/> with a rider at last window tick.</summary>
	public static int HostMountedRidersLastSample { get; private set; }

	/// <summary>Host: <see cref="HostMountSteerRecvPerSec"/> divided by mounted rider count (last window).</summary>
	public static float HostAvgRecvPerRiderPerSec { get; private set; }

	public static void TickWindowIfNeeded()
	{
		if ( !Game.IsPlaying )
			return;

		lock ( _windowLock )
		{
			var now = Time.Now;
			if ( _windowStart < 0.0 )
			{
				_windowStart = now;
				return;
			}

			if ( now - _windowStart < 1.0 )
				return;

			var span = Math.Max( 0.001, now - _windowStart );
			var sent = Interlocked.Exchange( ref _sentWindow, 0 );
			var recv = Interlocked.Exchange( ref _recvWindow, 0 );
			ClientMountSteerSentPerSec = sent / (float)span;
			HostMountSteerRecvPerSec = recv / (float)span;
			HostMountedRidersLastSample = ThornsWildlifeTameRegistry.CountMountedRidersHost();
			HostAvgRecvPerRiderPerSec = HostMountedRidersLastSample > 0
				? HostMountSteerRecvPerSec / HostMountedRidersLastSample
				: 0f;
			_windowStart = now;
		}
	}

	public static void RecordClientSent() => Interlocked.Increment( ref _sentWindow );

	public static void RecordHostRecv() => Interlocked.Increment( ref _recvWindow );
}
