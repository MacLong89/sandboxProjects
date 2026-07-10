namespace Terraingen.UI;

/// <summary>Milestone moments — routed to the top-center world event feed.</summary>
public static class ThornsMilestoneHudBus
{
	public const int MaxVisible = 2;

	public static IReadOnlyList<ThornsWorldEventHudEntry> Active =>
		ThornsWorldEventHudBus.Active.Where( e => e.Kind == ThornsWorldEventFeedKind.Milestone ).ToList();

	public static void Push( string title, int xpReward = 0, float seconds = 5.5f ) =>
		ThornsWorldEventHudBus.PushMilestone( title, xpReward, seconds );

	public static void Tick( float delta ) => ThornsWorldEventHudBus.Tick( delta );
}
