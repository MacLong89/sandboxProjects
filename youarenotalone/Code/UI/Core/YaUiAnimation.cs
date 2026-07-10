namespace Sandbox;

/// <summary>Shared UI motion timing (AAA-style consistency).</summary>
public static class YaUiAnimation
{
	public const float FadeSeconds = 0.2f;
	public const float ScaleSeconds = 0.15f;
	public const float SlideSeconds = 0.25f;

	public static float EaseOut( float t01 ) => 1f - (1f - Math.Clamp( t01, 0f, 1f )) * (1f - Math.Clamp( t01, 0f, 1f ));
}
