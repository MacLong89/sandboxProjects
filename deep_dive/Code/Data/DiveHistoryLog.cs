namespace DeepDive;

public sealed class DiveHistoryEntry
{
	public int DiveNumber { get; init; }
	public int DayNumber { get; init; }
	public string TimeOfDay { get; init; }
	public float MaxDepthMeters { get; init; }
	public float DurationSeconds { get; init; }
	public float MoneyEarned { get; init; }
	public bool Success { get; init; }
	public List<string> LootIcons { get; init; } = new();
	public List<string> LootLabels { get; init; } = new();
}

/// <summary>Ring buffer of recent dive outcomes for the Diver Hub history column.</summary>
public sealed class DiveHistoryLog
{
	private readonly List<DiveHistoryEntry> _entries = new();
	public const int MaxEntries = 12;

	public IReadOnlyList<DiveHistoryEntry> Entries => _entries;

	public void Add( DiveHistoryEntry entry )
	{
		if ( entry is null ) return;
		_entries.Insert( 0, entry );
		while ( _entries.Count > MaxEntries )
			_entries.RemoveAt( _entries.Count - 1 );
	}

	public void Clear() => _entries.Clear();

	public List<DiveHistoryEntry> ToSaveList() => new( _entries );

	public void ApplySaveList( List<DiveHistoryEntry> list )
	{
		_entries.Clear();
		if ( list is null ) return;
		foreach ( var e in list.Take( MaxEntries ) )
			_entries.Add( e );
	}
}
