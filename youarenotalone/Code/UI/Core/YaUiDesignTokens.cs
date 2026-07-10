namespace Sandbox;

/// <summary>Spacing, radii, and motion tokens — single source for visual consistency.</summary>
public static class YaUiDesignTokens
{
	public const float ScreenEdgeInsetPx = 14f;
	public const float PanelPaddingPx = 10f;
	public const float CardPaddingPx = 28f;
	public const float SectionGapPx = 8f;
	public const float RowGapPx = 6f;
	public const float BorderWidthPx = 1f;
	public const float BorderStrongWidthPx = 2f;

	public const float ModalScrimOpacity = 0.82f;
	public const float CardOpacity = 1f;

	/// <summary>Top-center match stack (timer, role, objective).</summary>
	public const float TopStackTextMaxWidthPx = 920f;
	public const float TopStackRowGapPx = 6f;
	public const float TopStackTimerFontPx = 24f;
	public const float TopStackMutatorFontPx = 17f;
	public const float TopStackIntermissionFontPx = 18f;
	public const float TopStackRoleFontPx = 34f;
	public const float TopStackObjectiveFontPx = 19f;
	public const float TopStackCounterFontPx = 20f;
	public const float TopStackStatusFontPx = 19f;

	/// <summary>Upper band for round banners (Valorant / CS-style), not screen center.</summary>
	public const float NotificationTopCenterPx = 148f;

	/// <summary>Top-right combat accolades sit under the kill-feed column.</summary>
	public const float NotificationTopRightBelowFeedPx = 168f;

	/// <summary>Personal feedback sits above the bottom-left health card.</summary>
	public const float NotificationBottomLeftAboveHealthPx = 148f;
}
