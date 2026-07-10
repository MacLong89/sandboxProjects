namespace Sandbox;

/// <summary>Per-frame scope PiP layout published by <see cref="AimboxM700ScopePipHud"/> for UI compositing.</summary>
public readonly struct AimboxM700ScopePipFrame
{
	public static AimboxM700ScopePipFrame Inactive => default;

	public bool Active { get; init; }
	public Vector2 Center { get; init; }
	public float Radius { get; init; }
	public Texture ScopeView { get; init; }
}

public static class AimboxM700ScopePipState
{
	public static AimboxM700ScopePipFrame Frame { get; private set; }

	public static void Publish( in AimboxM700ScopePipFrame frame ) => Frame = frame;

	public static void Clear() => Frame = AimboxM700ScopePipFrame.Inactive;
}
