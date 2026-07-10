using Sandbox.UI;

namespace Sandbox;

public sealed class YaUiSurfaceBinding
{
	public YaUiRequest Request { get; init; }
	public Panel Panel { get; init; }
	public Func<YaUiFrameContext, bool> WantsVisible { get; init; }
	public Action<bool> OnVisibilityChanged { get; init; }
	public bool ManagesOpacity { get; init; } = true;

	public YaUiSurfaceId Id => Request.Surface;
}
