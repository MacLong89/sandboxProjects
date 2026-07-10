namespace Fauna2;

/// <summary>Summary of one save slot for the main menu.</summary>
public sealed class SaveSlotInfo
{
	public int SlotId { get; init; }
	public bool Exists { get; init; }
	public string Label { get; init; }
	public string ZooName { get; init; }
	public int Level { get; init; }
	public int Money { get; init; }
	public string StarterProfileId { get; init; }
	public long SavedAtUnix { get; init; }
	public bool IsLegacy { get; init; }

	public string SavedAtText
	{
		get
		{
			if ( !Exists || SavedAtUnix <= 0 ) return "Empty";
			return DateTimeOffset.FromUnixTimeSeconds( SavedAtUnix ).LocalDateTime.ToString( "MMM d, h:mm tt" );
		}
	}

	public string SummaryText => Exists
		? $"{ZooName} · Lv.{Level} · ${Money:n0} · {SavedAtText}"
		: "Empty slot";
}
