using System;
using System.Threading;

namespace Sandbox;

/// <summary>Host-only counters for wildlife/bandit perception (F1 developer panel + profiling).</summary>
public static class ThornsAiPerceptionMetrics
{
	static readonly object _windowLock = new();
	static double _windowStart = -1.0;

	static long _playerSpatialQueries;
	static long _playerCandidatesSum;
	static int _playerCandidatesMaxSingleQuery;
	static long _losTraces;
	static long _losBudgetSkips;
	static long _losCacheHits;
	static long _perceptionPlayerConsiderations;
	static long _wildlifePerceptionCalls;
	static long _perceptionCandidateCapDrops;
	static long _losProbeThinkCapHits;

	public static float PlayerSpatialQueriesPerSec { get; private set; }
	public static float LosTracesPerSec { get; private set; }
	public static float LosBudgetSkipsPerSec { get; private set; }
	public static float LosCacheHitsPerSec { get; private set; }
	public static float PerceptionPlayerConsiderationsPerSec { get; private set; }
	public static float WildlifePerceptionCallsPerSec { get; private set; }
	public static float PerceptionCandidateCapDropsPerSec { get; private set; }
	public static float LosProbeThinkCapHitsPerSec { get; private set; }
	public static float AvgPlayerCandidatesPerQuery { get; private set; }
	public static int MaxPlayerCandidatesSingleQueryWindow { get; private set; }

	public static int LastSpatialGridCells { get; set; }
	public static int LastSpatialGridPlayers { get; set; }
	public static int LastWildlifeSpatialGridCells { get; set; }
	public static int LastWildlifeSpatialGridBrains { get; set; }

	static long _wildlifePeerSpatialQueries;
	static long _wildlifePeerCandidatesSum;
	static int _wildlifePeerCandidatesMaxSingleQuery;

	public static float WildlifePeerSpatialQueriesPerSec { get; private set; }
	public static float AvgWildlifePeerCandidatesPerQuery { get; private set; }
	public static int MaxWildlifePeerCandidatesSingleQueryWindow { get; private set; }

	public static void RecordWildlifePeerSpatialQuery( int candidateCount )
	{
		if ( !Networking.IsHost )
			return;

		Interlocked.Increment( ref _wildlifePeerSpatialQueries );
		Interlocked.Add( ref _wildlifePeerCandidatesSum, candidateCount );

		int currentMax;
		do
		{
			currentMax = _wildlifePeerCandidatesMaxSingleQuery;
			if ( candidateCount <= currentMax )
				break;
		}
		while ( Interlocked.CompareExchange( ref _wildlifePeerCandidatesMaxSingleQuery, candidateCount, currentMax )
		        != currentMax );
	}

	public static void TickWindowIfNeeded()
	{
		if ( !Game.IsPlaying || !Networking.IsHost )
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
			var q = Interlocked.Exchange( ref _playerSpatialQueries, 0 );
			var csum = Interlocked.Exchange( ref _playerCandidatesSum, 0 );
			var cmax = Interlocked.Exchange( ref _playerCandidatesMaxSingleQuery, 0 );
			var los = Interlocked.Exchange( ref _losTraces, 0 );
			var skip = Interlocked.Exchange( ref _losBudgetSkips, 0 );
			var chit = Interlocked.Exchange( ref _losCacheHits, 0 );
			var cons = Interlocked.Exchange( ref _perceptionPlayerConsiderations, 0 );
			var wcalls = Interlocked.Exchange( ref _wildlifePerceptionCalls, 0 );
			var capDrops = Interlocked.Exchange( ref _perceptionCandidateCapDrops, 0 );
			var probeCap = Interlocked.Exchange( ref _losProbeThinkCapHits, 0 );
			var wPeerQ = Interlocked.Exchange( ref _wildlifePeerSpatialQueries, 0 );
			var wPeerSum = Interlocked.Exchange( ref _wildlifePeerCandidatesSum, 0 );
			var wPeerMax = Interlocked.Exchange( ref _wildlifePeerCandidatesMaxSingleQuery, 0 );

			PlayerSpatialQueriesPerSec = q / (float)span;
			LosTracesPerSec = los / (float)span;
			LosBudgetSkipsPerSec = skip / (float)span;
			LosCacheHitsPerSec = chit / (float)span;
			PerceptionPlayerConsiderationsPerSec = cons / (float)span;
			WildlifePerceptionCallsPerSec = wcalls / (float)span;
			PerceptionCandidateCapDropsPerSec = capDrops / (float)span;
			LosProbeThinkCapHitsPerSec = probeCap / (float)span;
			AvgPlayerCandidatesPerQuery = q > 0 ? csum / (float)q : 0f;
			MaxPlayerCandidatesSingleQueryWindow = cmax;
			WildlifePeerSpatialQueriesPerSec = wPeerQ / (float)span;
			AvgWildlifePeerCandidatesPerQuery = wPeerQ > 0 ? wPeerSum / (float)wPeerQ : 0f;
			MaxWildlifePeerCandidatesSingleQueryWindow = wPeerMax;
			_windowStart = now;
		}
	}

	public static void RecordPlayerSpatialQuery( int candidateCount )
	{
		Interlocked.Increment( ref _playerSpatialQueries );
		Interlocked.Add( ref _playerCandidatesSum, candidateCount );

		int currentMax;
		do
		{
			// Plain read: s&box whitelist disallows System.Threading.Volatile.* ; CAS below still publishes updates safely for this metric.
			currentMax = _playerCandidatesMaxSingleQuery;
			if ( candidateCount <= currentMax )
				break;
		} while ( Interlocked.CompareExchange( ref _playerCandidatesMaxSingleQuery, candidateCount, currentMax ) != currentMax );
	}

	public static void RecordPerceptionPlayerConsiderations( int count ) =>
		Interlocked.Add( ref _perceptionPlayerConsiderations, count );

	public static void RecordWildlifePerceptionCall() => Interlocked.Increment( ref _wildlifePerceptionCalls );

	public static void RecordPerceptionCandidateCapDrop() => Interlocked.Increment( ref _perceptionCandidateCapDrops );

	public static void RecordLosProbeThinkCapHit() => Interlocked.Increment( ref _losProbeThinkCapHits );

	public static void RecordLosTrace() => Interlocked.Increment( ref _losTraces );

	public static void RecordLosBudgetSkip() => Interlocked.Increment( ref _losBudgetSkips );

	public static void RecordLosCacheHit() => Interlocked.Increment( ref _losCacheHits );
}
