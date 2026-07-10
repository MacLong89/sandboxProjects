namespace Sandbox;

/// <summary>Parameters forwarded to existing spawner cap logic — values remain owned by scene spawner components.</summary>
public readonly struct ThornsPopulationSpawnRequest
{
	public int GlobalCap { get; init; }

	public int PerPlayerNearbyCap { get; init; }

	public float PerPlayerNearbyRadius { get; init; }

	/// <summary>Anchor for per-player wildlife density checks.</summary>
	public Vector3? AnchorWorldPosition { get; init; }

	/// <summary>Required for bandit wanderer global counts.</summary>
	public Scene Scene { get; init; }
}
