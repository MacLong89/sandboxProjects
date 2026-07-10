namespace Sandbox;

/// <summary>World-gen counters for settlement layout resolution (reset each scatter pass).</summary>
public static class ThornsProcBuildingSettlementDiagnostics
{
	public static int BlueprintCompileSuccess { get; private set; }
	public static int BlueprintCompileFailed { get; private set; }
	public static int BlueprintStrictValidationPassed { get; private set; }
	public static int BlueprintStrictValidationFailed { get; private set; }
	public static int FallbackUsed { get; private set; }
	public static int FallbackStrictValidationPassed { get; private set; }
	public static int FallbackStrictValidationFailed { get; private set; }
	public static int PlacementRejected { get; private set; }

	public static void Reset()
	{
		BlueprintCompileSuccess = 0;
		BlueprintCompileFailed = 0;
		BlueprintStrictValidationPassed = 0;
		BlueprintStrictValidationFailed = 0;
		FallbackUsed = 0;
		FallbackStrictValidationPassed = 0;
		FallbackStrictValidationFailed = 0;
		PlacementRejected = 0;
	}

	public static void RecordBlueprintCompileSuccess() => BlueprintCompileSuccess++;
	public static void RecordBlueprintCompileFailed() => BlueprintCompileFailed++;
	public static void RecordBlueprintStrictValidationPassed() => BlueprintStrictValidationPassed++;
	public static void RecordBlueprintStrictValidationFailed() => BlueprintStrictValidationFailed++;
	public static void RecordFallbackUsed() => FallbackUsed++;
	public static void RecordFallbackStrictValidationPassed() => FallbackStrictValidationPassed++;
	public static void RecordFallbackStrictValidationFailed() => FallbackStrictValidationFailed++;
	public static void RecordPlacementRejected() => PlacementRejected++;

	public static void LogSummary()
	{
		Log.Info(
			$"[Thorns ProcBuilding] Settlement layout stats: "
			+ $"compileOk={BlueprintCompileSuccess} compileFail={BlueprintCompileFailed} "
			+ $"blueprintValid={BlueprintStrictValidationPassed} blueprintInvalid={BlueprintStrictValidationFailed} "
			+ $"fallbackUsed={FallbackUsed} fallbackValid={FallbackStrictValidationPassed} fallbackInvalid={FallbackStrictValidationFailed} "
			+ $"placementRejected={PlacementRejected}" );
	}
}
