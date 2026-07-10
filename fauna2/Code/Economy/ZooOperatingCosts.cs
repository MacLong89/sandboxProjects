namespace Fauna2;

/// <summary>
/// Recurring zoo costs — staff, feed, maintenance and crowd services.
/// Scales with what you've built so early zoos run at a loss until guest revenue catches up.
/// </summary>
public static class ZooOperatingCosts
{
	public static float PerMinute()
	{
		var cost = GameConstants.BaseOperatingCostPerMinute;
		cost += AnimalRegistry.Count * GameConstants.OperatingCostPerAnimalPerMinute;
		cost += HabitatRegistry.Count * GameConstants.OperatingCostPerHabitatPerMinute;
		cost += (PlotSystem.Instance?.PlotCount ?? 1) * GameConstants.OperatingCostPerPlotPerMinute;
		cost += (GuestSystem.Instance?.GuestCount ?? 0) * GameConstants.OperatingCostPerGuestPerMinute;
		cost += PlaceableRegistry.PlaceableOperatingCostPerMinute();

		return cost * GameConstants.GamePaceMultiplier;
	}

	public static float PerSecond() => PerMinute() / 60f;
}
