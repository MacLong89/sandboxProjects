namespace Sandbox;

/// <summary>Host-wide cap on expensive wildlife→player LOS rays per physics step (shared across all AI). Reset cadence owned by <see cref="ThornsPopulationDirector"/>.</summary>
public static class ThornsWildlifeLosBudget
{
	static int _usedThisFixed;
	static int _fixedStepSerial;

	public static int HostTracesUsedThisFixed => _usedThisFixed;

	/// <summary>Increments once per host physics step — drives lazy wildlife spatial index rebuild.</summary>
	public static int HostFixedStepSerial => _fixedStepSerial;

	public static void HostResetForNewFixed()
	{
		if ( !Networking.IsHost )
			return;

		_fixedStepSerial++;
		_usedThisFixed = 0;
	}

	/// <summary>Returns false when the global LOS budget is exhausted (caller should treat as no line-of-sight).</summary>
	public static bool TryConsumeTrace()
	{
		if ( !Networking.IsHost )
			return true;

		if ( _usedThisFixed >= ThornsPerformanceBudgets.HostWildlifeMaxLosRaysPerFixed )
		{
			ThornsAiPerceptionMetrics.RecordLosBudgetSkip();
			return false;
		}

		_usedThisFixed++;
		return true;
	}
}
