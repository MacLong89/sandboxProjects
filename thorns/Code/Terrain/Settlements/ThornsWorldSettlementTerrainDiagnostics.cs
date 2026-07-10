namespace Sandbox;

/// <summary>Terrain placement counters for settlement world-gen (reset each scatter pass).</summary>
public static class ThornsWorldSettlementTerrainDiagnostics
{
	public static int TerrainValidationPassed { get; private set; }
	public static int TerrainValidationRejected { get; private set; }
	public static int LocalFeatherPadsAdded { get; private set; }
	public static int TerrainPadsSkippedSteep { get; private set; }

	public static void Reset()
	{
		TerrainValidationPassed = 0;
		TerrainValidationRejected = 0;
		LocalFeatherPadsAdded = 0;
		TerrainPadsSkippedSteep = 0;
	}

	public static void RecordTerrainValidationPassed() => TerrainValidationPassed++;
	public static void RecordTerrainValidationRejected() => TerrainValidationRejected++;
	public static void RecordLocalFeatherPad() => LocalFeatherPadsAdded++;
	public static void RecordTerrainPadSkippedSteep() => TerrainPadsSkippedSteep++;

	public static void LogSummary()
	{
		Log.Info(
			$"[Thorns Terrain] Settlement terrain: validationPass={TerrainValidationPassed} "
			+ $"validationReject={TerrainValidationRejected} localFeatherPads={LocalFeatherPadsAdded} "
			+ $"padsSkippedSteep={TerrainPadsSkippedSteep} macroZones={ThornsWorldSettlementTerrainShaping.LastMacroZones.Count}" );
	}
}
