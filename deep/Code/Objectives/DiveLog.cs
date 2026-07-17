namespace Deep;

public sealed class DiveLogEntry
{
	public string Title { get; init; }
	public string IconPath { get; init; }
	public bool Known { get; init; }
	public float TimeStamp { get; init; }
}

/// <summary>Recent finds / events for the dive LOG widget.</summary>
public sealed class DiveLog
{
	private readonly List<DiveLogEntry> _entries = new();
	public const int MaxVisible = 3;

	public IReadOnlyList<DiveLogEntry> Entries => _entries;

	public void Clear() => _entries.Clear();

	public void AddEntry( string title, string iconPath, bool known )
	{
		_entries.Insert( 0, new DiveLogEntry
		{
			Title = title ?? "???",
			IconPath = string.IsNullOrEmpty( iconPath ) ? "/ui/icons/icon_unknown.png" : iconPath,
			Known = known,
			TimeStamp = Time.Now
		} );

		while ( _entries.Count > 12 )
			_entries.RemoveAt( _entries.Count - 1 );
	}

	public IEnumerable<DiveLogEntry> VisibleEntries()
	{
		var n = 0;
		foreach ( var e in _entries )
		{
			yield return e;
			if ( ++n >= MaxVisible ) yield break;
		}
	}
}
