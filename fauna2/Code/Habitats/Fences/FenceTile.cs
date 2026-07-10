namespace Fauna2;

/// <summary>One perimeter fence cell — generated once when a habitat is built.</summary>
public sealed class FenceTile
{
	public Vector2Int GridPosition { get; init; }
	public Vector3 WorldCenter { get; init; }
	public string Sprite { get; init; } = "fence_h";
	public bool CollisionEnabled { get; init; } = true;
	public FenceRenderLayer RenderLayer { get; init; } = FenceRenderLayer.Back;
}
