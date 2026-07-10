namespace Terraingen.UI.Core;

/// <summary>Shared UI transition timing — all panels must use these constants.</summary>
public static class ThornsUiAnimations
{
	public const float FadeMs = 200f;
	public const float ScaleMs = 150f;
	public const float SlideMs = 250f;
	public const float TooltipFadeMs = 120f;

	public static string FadeTransition => $"opacity {FadeMs}ms ease-out";
	public static string SlideTransition => $"transform {SlideMs}ms ease-out, opacity {FadeMs}ms ease-out";
}
