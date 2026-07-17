namespace FinalOutpost;

/// <summary>Cure-mode Knowledge awards from play actions (spent in the Tech Tree).</summary>
public static class KnowledgeGain
{
	public static void Award( GameCore core, double amount, string toast = null )
	{
		if ( core is null || !core.IsCure || amount <= 0 ) return;

		core.Resources.Add( ResourceKind.Knowledge, amount );
		if ( !string.IsNullOrEmpty( toast ) )
			core.ShowToast( toast );
	}

	public static void OnBuildingPlaced( GameCore core ) =>
		Award( core, CureConstants.KnowledgeFromPlaceBuilding );

	public static void OnPlotClaimed( GameCore core ) =>
		Award( core, CureConstants.KnowledgeFromClaimPlot );

	public static void OnPlotCleared( GameCore core ) =>
		Award( core, CureConstants.KnowledgeFromClearPlot );

	public static void OnThreatSurvived( GameCore core ) =>
		Award( core, CureConstants.KnowledgeFromThreatSurvived );

	public static void OnWorkerHired( GameCore core ) =>
		Award( core, CureConstants.KnowledgeFromHire );
}
