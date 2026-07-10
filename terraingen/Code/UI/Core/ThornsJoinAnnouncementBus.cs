namespace Terraingen.UI;

public sealed class ThornsJoinAnnouncementEntry
{
	public string Message { get; set; } = "";
	public float SecondsRemaining { get; set; } = 6f;
}

/// <summary>Bottom-left multiplayer join toasts.</summary>
public static class ThornsJoinAnnouncementBus
{
	static readonly List<ThornsJoinAnnouncementEntry> Entries = new();

	public static IReadOnlyList<ThornsJoinAnnouncementEntry> Active => Entries;

	public static void PushPlayerJoined( string playerName, float seconds = 6f )
	{
		var name = string.IsNullOrWhiteSpace( playerName ) ? "Someone" : playerName.Trim();
		Push( $"{name} joined!", seconds );
	}

	public static void Push( string message, float seconds = 6f )
	{
		if ( string.IsNullOrWhiteSpace( message ) )
			return;

		Entries.Insert( 0, new ThornsJoinAnnouncementEntry { Message = message.Trim(), SecondsRemaining = seconds } );
		if ( Entries.Count > 4 )
			Entries.RemoveAt( Entries.Count - 1 );

		UiRevisionBus.Publish( UiRevisionChannel.Vitals );
	}

	public static void Tick( float delta )
	{
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
			UiRevisionBus.Publish( UiRevisionChannel.Vitals );
	}
}
