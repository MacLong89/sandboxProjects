namespace Terraingen.UI;

public sealed class ThornsNotificationEntry
{
	public string Id { get; set; } = Guid.NewGuid().ToString( "N" );
	public string Message { get; set; } = "";
	public string Kind { get; set; } = "info";
	public float SecondsRemaining { get; set; } = 4f;
}

/// <summary>Player status toasts (left column) — queued, deduplicated, combat-safe.</summary>
public static class ThornsNotificationBus
{
	const int MaxVisible = 5;
	const float DefaultSeconds = 4f;
	const float DedupeWindowSeconds = 1.5f;

	static readonly List<ThornsNotificationEntry> Entries = new();
	static readonly Queue<(string message, string kind, float seconds)> Pending = new();
	static float _secondsSinceLastPush;
	static string _lastMessage;

	public static IReadOnlyList<ThornsNotificationEntry> Active => Entries;

	public static void Push( string message, string kind = "info", float seconds = DefaultSeconds )
	{
		if ( string.IsNullOrWhiteSpace( message ) )
			return;

		// Dedupe rapid identical messages.
		if ( string.Equals( _lastMessage, message, StringComparison.OrdinalIgnoreCase )
		     && _secondsSinceLastPush <= DedupeWindowSeconds )
			return;

		if ( string.Equals( kind, "milestone", StringComparison.OrdinalIgnoreCase ) )
		{
			ThornsWorldEventHudBus.PushMilestone( message, 0, seconds );
			return;
		}

		// Defer non-critical toasts while modal UI is active.
		if ( !IsCritical( kind ) && Terraingen.UI.Core.ThornsUiInputGate.BlocksGameplayInput )
		{
			EnqueuePending( message, kind, seconds );
			return;
		}

		InsertEntry( message, kind, seconds );
	}

	public static void Tick( float delta )
	{
		_secondsSinceLastPush += delta;

		if ( !Terraingen.UI.Core.ThornsUiInputGate.AllowsTransientFeedback && Pending.Count > 0 )
			return;

		FlushPendingIfAllowed();

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
			UiRevisionBus.Publish( UiRevisionChannel.Notifications );
	}

	static void InsertEntry( string message, string kind, float seconds )
	{
		Entries.Insert( 0, new ThornsNotificationEntry { Message = message, Kind = kind, SecondsRemaining = seconds } );
		while ( Entries.Count > MaxVisible )
			Entries.RemoveAt( Entries.Count - 1 );

		_lastMessage = message;
		_secondsSinceLastPush = 0f;
		UiRevisionBus.Publish( UiRevisionChannel.Notifications );
	}

	static void EnqueuePending( string message, string kind, float seconds )
	{
		while ( Pending.Count >= MaxVisible )
			Pending.Dequeue();

		Pending.Enqueue( (message, kind, seconds) );
	}

	static void FlushPendingIfAllowed()
	{
		if ( !Terraingen.UI.Core.ThornsUiInputGate.AllowsTransientFeedback )
			return;

		while ( Pending.Count > 0 )
		{
			var (message, kind, seconds) = Pending.Dequeue();
			InsertEntry( message, kind, seconds );
		}
	}

	static bool IsCritical( string kind ) =>
		string.Equals( kind, "error", StringComparison.OrdinalIgnoreCase )
		|| string.Equals( kind, "warning", StringComparison.OrdinalIgnoreCase );
}
