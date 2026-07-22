namespace PawnShop;

/// <summary>
/// Global UI dirty counter. Systems bump it whenever player-visible state changes,
/// and every panel folds it into BuildHash so the whole UI re-renders exactly once.
/// </summary>
public static class UiState
{
	public static int Version { get; private set; }

	public static void Bump() => Version++;
}
