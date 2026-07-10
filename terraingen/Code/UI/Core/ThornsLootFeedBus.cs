namespace Terraingen.UI;

public enum ThornsLootFeedKind : byte
{
	Item,
	Tame
}

public sealed class ThornsLootFeedEntry
{
	public string Id { get; set; } = Guid.NewGuid().ToString( "N" );
	public ThornsLootFeedKind Kind { get; set; } = ThornsLootFeedKind.Item;
	public string ItemId { get; set; } = "";
	public string SpeciesKey { get; set; } = "";
	public string SpeciesName { get; set; } = "";
	public int Tier { get; set; } = 1;
	public int Count { get; set; }
	public float SecondsRemaining { get; set; } = 2.75f;
}

/// <summary>Bottom-left pickup toasts — merged, capped, and combat-safe.</summary>
public static class ThornsLootFeedBus
{
	const float LifetimeSeconds = 2.75f;
	const float MergeWindowSeconds = 0.65f;
	const int MaxEntries = 5;
	const int MaxBatchItems = 8;

	static readonly List<ThornsLootFeedEntry> Entries = new();
	static float _secondsSinceLastPush;

	public static IReadOnlyList<ThornsLootFeedEntry> Active => Entries;

	public static void Push( string itemId, int count )
	{
		if ( count <= 0 || string.IsNullOrWhiteSpace( itemId ) )
			return;

		if ( Terraingen.UI.Core.ThornsUiInputGate.BlocksGameplayInput )
			return;

		var merge = _secondsSinceLastPush <= MergeWindowSeconds
		           && Entries.Count > 0
		           && Entries[^1].Kind == ThornsLootFeedKind.Item
		           && Entries[^1].ItemId == itemId;

		if ( merge )
		{
			var last = Entries[^1];
			last.Count += count;
			last.SecondsRemaining = LifetimeSeconds;
		}
		else
		{
			Entries.Add( new ThornsLootFeedEntry
			{
				ItemId = itemId,
				Count = count,
				SecondsRemaining = LifetimeSeconds
			} );

			TrimEntries();
		}

		_secondsSinceLastPush = 0f;
		UiRevisionBus.Publish( UiRevisionChannel.LootFeed );
	}

	public static void PushTame( string speciesKey, string speciesName, int tier, int count = 1 )
	{
		if ( count <= 0 || string.IsNullOrWhiteSpace( speciesName ) )
			return;

		if ( Terraingen.UI.Core.ThornsUiInputGate.BlocksGameplayInput )
			return;

		var merge = _secondsSinceLastPush <= MergeWindowSeconds
		           && Entries.Count > 0
		           && Entries[^1].Kind == ThornsLootFeedKind.Tame
		           && string.Equals( Entries[^1].SpeciesKey, speciesKey, StringComparison.OrdinalIgnoreCase );

		if ( merge )
		{
			var last = Entries[^1];
			last.Count += count;
			last.SecondsRemaining = LifetimeSeconds;
		}
		else
		{
			Entries.Add( new ThornsLootFeedEntry
			{
				Kind = ThornsLootFeedKind.Tame,
				SpeciesKey = speciesKey ?? "",
				SpeciesName = speciesName,
				Tier = Math.Clamp( tier, 1, 4 ),
				Count = count,
				SecondsRemaining = LifetimeSeconds
			} );

			TrimEntries();
		}

		_secondsSinceLastPush = 0f;
		UiRevisionBus.Publish( UiRevisionChannel.LootFeed );
	}

	static void TrimEntries()
	{
		while ( Entries.Count > MaxEntries )
			Entries.RemoveAt( 0 );

		var totalItems = Entries.Sum( e => e.Count );
		while ( totalItems > MaxBatchItems && Entries.Count > 1 )
		{
			Entries.RemoveAt( 0 );
			totalItems = Entries.Sum( e => e.Count );
		}
	}

	public static void Tick( float delta )
	{
		if ( Entries.Count == 0 && _secondsSinceLastPush > MergeWindowSeconds )
			return;

		_secondsSinceLastPush += delta;

		var changed = false;
		for ( var i = Entries.Count - 1; i >= 0; i-- )
		{
			Entries[i].SecondsRemaining -= delta;
			if ( Entries[i].SecondsRemaining <= 0f )
			{
				Entries.RemoveAt( i );
				changed = true;
			}
		}

		if ( changed )
			UiRevisionBus.Publish( UiRevisionChannel.LootFeed );
	}
}
