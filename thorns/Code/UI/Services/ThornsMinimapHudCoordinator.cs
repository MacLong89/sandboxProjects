namespace Sandbox;

/// <summary>Minimap polling architecture — documents event hooks without changing refresh cadence.</summary>
public static class ThornsMinimapHudCoordinator
{
	public static double NextUiTickIntervalSeconds( ThornsMinimapHud hud ) =>
		Math.Max( 0.05f, hud.UiUpdateIntervalSeconds );

	public static double NextDynamicBlipIntervalSeconds( ThornsMinimapHud hud ) =>
		Math.Max( 0.1f, hud.DynamicBlipUpdateIntervalSeconds );

	/// <summary>Future: subscribe to terrain replica ready instead of polling content hash each tick.</summary>
	public static bool ShouldRefreshTerrainOverview(
		long lastContentToken,
		long currentContentToken,
		int lastBoundsHash,
		int currentBoundsHash ) =>
		lastContentToken != currentContentToken || lastBoundsHash != currentBoundsHash;

	/// <summary>Future: POI dataset version change event — currently polled via ThornsPoiAuthority.</summary>
	public static bool ShouldRefreshPoiLayer( int lastDatasetVersion, int currentDatasetVersion, long lastToken, long currentToken ) =>
		lastDatasetVersion != currentDatasetVersion || lastToken != currentToken;
}
