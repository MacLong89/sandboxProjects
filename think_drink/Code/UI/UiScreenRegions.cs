namespace ThinkDrink.UI;

/// <summary>Reserved screen regions — HUD elements must stay inside their assigned zone.</summary>
public static class UiScreenRegions
{
	public const float TopHudMaxPercent = 22f;
	public const float NotificationBandStartPercent = 10f;
	public const float NotificationBandStepPercent = 5.5f;
	public const float CenterModalMarginPercent = 8f;
	public const float BottomControlsMinPx = 72f;

	public static float NotificationTopPercent( int stackIndex ) =>
		NotificationBandStartPercent + stackIndex * NotificationBandStepPercent;
}
