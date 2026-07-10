using System.Diagnostics;

namespace Sandbox;

/// <summary>
/// Runtime performance profiling for Thorns — frame stats, phase timings, streaming budgets.
/// Enable overlay: <c>perf_debug 1</c> console command.
/// </summary>
public static class ThornsPerfDebug
{
	public const int MaxPhaseHistory = 32;
	public const int MaxSystemSamples = 24;

	public static bool Enabled { get; set; }

	/// <summary>Runtime quality preset: Low, Medium, High, or Ultra.</summary>
	[ConVar( "perf_quality" )]
	public static string QualityConVar
	{
		get => ThornsPerformanceQualityPresets.ActiveQuality.ToString();
		set
		{
			if ( !Enum.TryParse<ThornsPerformanceQuality>( value, true, out var q ) )
				return;

			ThornsPerformanceQualityPresets.ActiveQuality = q;
			ThornsPerformanceQualityPresets.ApplyToActiveScene( q );
		}
	}

	[ConCmd( "perf_debug" )]
	public static void CmdPerfDebug( int enabled = 1 ) => Enabled = enabled != 0;

	static bool _frameActive;
	static double _lastFrameMs;
	static double _avgFrameMs;
	static double _maxFrameMs;
	static int _frameCount;
	static string _worstSystemThisFrame = "";
	static double _worstSystemMsThisFrame;
	static string _worstSystemRolling = "";
	static double _worstSystemMsRolling;

	static readonly List<(string Name, double Ms)> _frameSystems = new( MaxSystemSamples );

	static readonly Dictionary<string, double> _worldGenPhaseMs = new( StringComparer.OrdinalIgnoreCase );
	static double _worldGenTotalMs;
	static double _loadPlayableMs = -1;
	static double _loadStartedAt = -1;

	public static int DeferredQueuePending { get; set; }
	public static int DeferredSpawnsThisFrame { get; set; }
	public static int FoliageInstancesVisible { get; set; }
	public static int FoliageChunksLoaded { get; set; }
	public static int FoliageChunksGeneratedThisFrame { get; set; }
	public static int GrassInstancesVisible { get; set; }
	public static int GrassTilesPending { get; set; }
	public static int ActiveBuildingsEstimate { get; set; }
	public static int ActiveLootEstimate { get; set; }
	public static int ActiveAiEstimate { get; set; }

	/// <summary>Heuristic scene content pressure (not managed heap — s&amp;box whitelist blocks <c>GC.GetTotalMemory</c>).</summary>
	public static int ContentProxyWeight { get; private set; }

	public static void MarkLoadStarted()
	{
		if ( _loadStartedAt < 0 )
			_loadStartedAt = Time.Now;
	}

	public static void MarkPlayable()
	{
		if ( _loadPlayableMs < 0 && _loadStartedAt >= 0 )
			_loadPlayableMs = (Time.Now - _loadStartedAt) * 1000.0;
	}

	public static void BeginFrame()
	{
		_frameActive = true;
		_worstSystemThisFrame = "";
		_worstSystemMsThisFrame = 0;
		_frameSystems.Clear();
		DeferredSpawnsThisFrame = 0;
		FoliageChunksGeneratedThisFrame = 0;
	}

	public static void AccumulateFrame( float deltaSeconds )
	{
		_frameActive = false;
		_lastFrameMs = Math.Max( 0.0, deltaSeconds ) * 1000.0;
		_frameCount++;
		_avgFrameMs = _avgFrameMs <= 0 ? _lastFrameMs : MathX.Lerp( _avgFrameMs, _lastFrameMs, 0.08f );
		if ( _lastFrameMs > _maxFrameMs )
			_maxFrameMs = _lastFrameMs;

		if ( _worstSystemMsThisFrame > _worstSystemMsRolling )
		{
			_worstSystemMsRolling = _worstSystemMsThisFrame;
			_worstSystemRolling = _worstSystemThisFrame;
		}

		if ( Enabled && _lastFrameMs >= ThornsPerformanceBudgets.ServerFrameSpikeWarnMs )
		{
			Log.Warning(
				$"[Thorns Perf] Spike {_lastFrameMs:F1}ms worst={_worstSystemThisFrame} ({_worstSystemMsThisFrame:F2}ms) deferred={DeferredQueuePending} foliage={FoliageInstancesVisible}" );
		}

		UpdateContentProxyWeight();
	}

	public static IDisposable Scope( string systemName )
	{
		if ( !_frameActive )
			return NoopScope.Instance;

		return new PerfScope( systemName );
	}

	public static void RecordSystem( string systemName, double milliseconds )
	{
		if ( string.IsNullOrWhiteSpace( systemName ) || milliseconds < 0.001 || !_frameActive )
			return;

		_frameSystems.Add( (systemName, milliseconds) );
		if ( milliseconds > _worstSystemMsThisFrame )
		{
			_worstSystemMsThisFrame = milliseconds;
			_worstSystemThisFrame = systemName;
		}
	}

	public static void RecordWorldGenPhase( string phaseName, double milliseconds )
	{
		if ( string.IsNullOrWhiteSpace( phaseName ) )
			return;

		_worldGenPhaseMs[phaseName] = milliseconds;
		_worldGenTotalMs += milliseconds;
		RecordSystem( $"WorldGen:{phaseName}", milliseconds );
	}

	public static float Fps => _lastFrameMs > 0.001 ? (float)(1000.0 / _lastFrameMs) : 0f;
	public static float AvgFps => _avgFrameMs > 0.001 ? (float)(1000.0 / _avgFrameMs) : 0f;
	public static double LastFrameMs => _lastFrameMs;
	public static double AvgFrameMs => _avgFrameMs;
	public static double MaxFrameMs => _maxFrameMs;
	public static string WorstSystemThisFrame => _worstSystemThisFrame;
	public static double WorstSystemMsThisFrame => _worstSystemMsThisFrame;
	public static string WorstSystemRolling => _worstSystemRolling;
	public static double WorldGenTotalMs => _worldGenTotalMs;
	public static double LoadPlayableMs => _loadPlayableMs;

	public static void RefreshEntityEstimates( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return;

		var buildings = 0;
		var loot = 0;
		foreach ( var host in scene.GetAllComponents<ThornsProcBuildingLayoutHost>() )
		{
			if ( host.IsValid() )
				buildings++;
		}

		foreach ( var crate in scene.GetAllComponents<ThornsLootCrate>() )
		{
			if ( crate.IsValid() )
				loot++;
		}

		var ai = (Networking.IsActive && !Networking.IsHost)
			? CountEnabledWildlife( scene )
			: ThornsPopulationDirector.HostWildlifeGlobalCount;

		ActiveBuildingsEstimate = buildings;
		ActiveLootEstimate = loot;
		ActiveAiEstimate = ai;
		UpdateContentProxyWeight();
	}

	static void UpdateContentProxyWeight()
	{
		ContentProxyWeight = FoliageInstancesVisible
		                     + GrassInstancesVisible
		                     + ActiveLootEstimate * 8
		                     + ActiveAiEstimate * 64
		                     + ActiveBuildingsEstimate * 128
		                     + DeferredQueuePending;
	}

	static readonly List<string> OverlayScratch = new( 24 );

	public static IReadOnlyList<string> BuildOverlayLines()
	{
		OverlayScratch.Clear();
		OverlayScratch.Add( $"FPS {Fps:F0}  avg {AvgFps:F0}  frame {_lastFrameMs:F1}ms  max {_maxFrameMs:F1}ms" );
		OverlayScratch.Add( $"Worst {WorstSystemThisFrame} ({WorstSystemMsThisFrame:F2}ms)  quality {ThornsPerformanceQualityPresets.ActiveQuality}" );
		OverlayScratch.Add( $"Deferred q={DeferredQueuePending}  spawned/frame={DeferredSpawnsThisFrame}" );
		OverlayScratch.Add( $"Foliage inst={FoliageInstancesVisible}  chunks={FoliageChunksLoaded}  +chunks/frame={FoliageChunksGeneratedThisFrame}" );
		OverlayScratch.Add( $"Grass inst={GrassInstancesVisible}  tiles pending={GrassTilesPending}" );
		OverlayScratch.Add( $"Buildings≈{ActiveBuildingsEstimate}  loot≈{ActiveLootEstimate}  AI≈{ActiveAiEstimate}" );
		OverlayScratch.Add( $"Content≈{ContentProxyWeight}  load→play {FormatLoadMs()}  worldGen {_worldGenTotalMs:F0}ms" );

		if ( _frameSystems.Count > 0 )
		{
			_frameSystems.Sort( ( a, b ) => b.Ms.CompareTo( a.Ms ) );
			var top = Math.Min( 3, _frameSystems.Count );
			for ( var i = 0; i < top; i++ )
				OverlayScratch.Add( $"  {_frameSystems[i].Name} {_frameSystems[i].Ms:F2}ms" );
		}

		return OverlayScratch;
	}

	public static string FormatLoadMs() => _loadPlayableMs >= 0 ? $"{_loadPlayableMs:F0}ms" : "…";

	static int CountEnabledWildlife( Scene scene )
	{
		var count = 0;
		foreach ( var brain in scene.GetAllComponents<ThornsWildlifeBrain>() )
		{
			if ( brain.IsValid() && brain.Enabled )
				count++;
		}

		return count;
	}

	sealed class PerfScope : IDisposable
	{
		readonly string _name;
		readonly Stopwatch _watch = Stopwatch.StartNew();

		public PerfScope( string name ) => _name = name;

		public void Dispose()
		{
			_watch.Stop();
			RecordSystem( _name, _watch.Elapsed.TotalMilliseconds );
		}
	}

	sealed class NoopScope : IDisposable
	{
		public static readonly NoopScope Instance = new();
		public void Dispose() { }
	}
}
