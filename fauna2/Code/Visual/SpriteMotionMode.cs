namespace Fauna2;

/// <summary>How living sprites animate while moving. Walk cycles were retired.</summary>
public enum SpriteMotionMode
{
	/// <summary>Single idle pose per facing with procedural bob/squash.</summary>
	Bounce,
}

public static class SpriteMotionModeExtensions
{
	public static string ToKey( this SpriteMotionMode mode ) => "Bounce";

	public static SpriteMotionMode Parse( string value ) => SpriteMotionMode.Bounce;
}
