using System.Diagnostics;

namespace Sandbox;

/// <summary>
/// Spreads pre-chunk world-gen phases across frames to avoid boot hitches.
/// </summary>
public sealed class ThornsDeferredWorldGenerationSession : IDisposable
{
	static readonly IThornsWorldGenerationPhase[] Phases =
	[
		new ThornsWorldGenPhaseMacroTerrain(),
		new ThornsWorldGenPhaseSelectSettlementLocations(),
		new ThornsWorldGenPhaseSettlementTerrain(),
		new ThornsWorldGenPhaseRoadNetwork(),
		new ThornsWorldGenPhaseSettlementBlocks(),
		new ThornsWorldGenPhaseApplyRoadTerrain(),
		new ThornsWorldGenPhaseReserveBuildingFootprints(),
		new ThornsWorldGenPhaseGenerateBuildingLayouts(),
		new ThornsWorldGenPhaseSpawnBuildings()
	];

	readonly ThornsWorldGenerationHostBridge _host;
	ThornsWorldGenerationContext _context;
	int _phaseIndex;
	bool _disposed;

	ThornsDeferredWorldGenerationSession( ThornsWorldGenerationHostBridge host, ThornsWorldGenerationContext context )
	{
		_host = host;
		_context = context;
	}

	public static ThornsDeferredWorldGenerationSession Begin(
		ThornsWorldGenerationHostBridge host,
		ThornsTerrainNetSpec spec,
		float edgeInsetFraction )
	{
		var context = ThornsWorldGenerationContext.Create( spec, edgeInsetFraction );
		return new ThornsDeferredWorldGenerationSession( host, context );
	}

	public bool IsComplete => _phaseIndex >= Phases.Length;

	/// <summary>Runs the next phase. Returns true when all phases have finished.</summary>
	public bool TickOnePhase()
	{
		if ( IsComplete || _disposed || _context is null )
			return IsComplete;

		var phase = Phases[_phaseIndex++];
		Log.Info( $"[Thorns WorldGen] Phase {(int)phase.Id}: {phase.Name} …" );
		var sw = Stopwatch.StartNew();
		phase.Execute( _context, _host );
		sw.Stop();
		var ms = sw.Elapsed.TotalMilliseconds;
		ThornsPerfDebug.RecordWorldGenPhase( phase.Name, ms );
		if ( ThornsPerfDebug.Enabled )
			Log.Info( $"[Thorns WorldGen] Phase {(int)phase.Id}: {phase.Name} done in {ms:F1}ms" );

		return IsComplete;
	}

	public void FinalizeHost()
	{
		if ( _context is null || _disposed )
			return;

		ThornsWorldGenerationQaReport.PublishSummary( _context, _host );
		_host.FinalizePreChunkGeneration( _context );
		_context.Dispose();
		_context = null;
	}

	public void Dispose()
	{
		if ( _disposed )
			return;

		_disposed = true;
		_context?.Dispose();
		_context = null;
	}
}
