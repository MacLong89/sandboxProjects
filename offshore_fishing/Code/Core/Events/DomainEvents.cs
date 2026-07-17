namespace OffshoreFishing.Core;

public interface IDomainEvent { }

public sealed class GoldChangedEvent : IDomainEvent
{
	public int Gold { get; init; }
	public int Delta { get; init; }
}

public sealed class ModeChangedEvent : IDomainEvent
{
	public GameMode Mode { get; init; }
}

public sealed class FishingPhaseChangedEvent : IDomainEvent
{
	public FishingPhase Phase { get; init; }
	public string StatusText { get; init; }
}

public sealed class FishCaughtEvent : IDomainEvent
{
	public CaughtFish Fish { get; init; }
	public bool IsNewSpecies { get; init; }
}

public sealed class FishSoldEvent : IDomainEvent
{
	public int Count { get; init; }
	public int GoldGained { get; init; }
}

public sealed class ItemPurchasedEvent : IDomainEvent
{
	public string ItemId { get; init; }
	public int Price { get; init; }
}

public sealed class ObjectiveUpdatedEvent : IDomainEvent
{
	public string ObjectiveId { get; init; }
	public int Progress { get; init; }
	public int Target { get; init; }
	public bool Completed { get; init; }
}

public sealed class ZoneUnlockedEvent : IDomainEvent
{
	public string ZoneId { get; init; }
}

public sealed class TutorialPromptEvent : IDomainEvent
{
	public string Text { get; init; }
}

public sealed class NotificationEvent : IDomainEvent
{
	public string Text { get; init; }
}

public sealed class HiredBoatReturnedEvent : IDomainEvent
{
	public string HiredBoatId { get; init; }
	public int GoldGained { get; init; }
}

public sealed class CatchRevealClosedEvent : IDomainEvent { }
