namespace Sandbox;

/// <summary>Live population pressure snapshot — caps remain on scene spawner components until centralized policy lands.</summary>
public readonly struct ThornsPopulationBudgetSnapshot
{
	public ThornsPopulationKind Kind { get; init; }

	/// <summary>Registered live entities for this kind (facade count; wanderers use full bandit registry today).</summary>
	public int LiveCount { get; init; }

	/// <summary>Configured global cap when known; <c>-1</c> when cap lives on a spawner component.</summary>
	public int GlobalCap { get; init; }

	public bool IsAtOrOverCap => GlobalCap >= 0 && LiveCount >= GlobalCap;
}
