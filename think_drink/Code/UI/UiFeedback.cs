namespace ThinkDrink.UI;

/// <summary>Lightweight client-side feedback for UI interactions.</summary>
public static class UiFeedback
{
	public static void Click()
	{
		GameEvents.RaiseAudio( AudioEventId.UiClick );
		UiState.PulseInteraction();
	}

	public static void Success() => GameEvents.RaiseAudio( AudioEventId.Correct );

	public static void Error() => GameEvents.RaiseAudio( AudioEventId.Incorrect );
}
