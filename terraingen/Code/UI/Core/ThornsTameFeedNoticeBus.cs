namespace Terraingen.UI;

public sealed class ThornsTameFeedNoticeEntry
{
	public string Message { get; set; } = "";
	public string Kind { get; set; } = "info";
	public float SecondsRemaining { get; set; } = 4f;
}

/// <summary>Transient feed feedback shown in the Tames menu near the feed command.</summary>
public static class ThornsTameFeedNoticeBus
{
	static ThornsTameFeedNoticeEntry _active;

	public static ThornsTameFeedNoticeEntry Active => _active;

	public static void Push( string message, string kind = "info", float seconds = 4f )
	{
		if ( string.IsNullOrWhiteSpace( message ) )
			return;

		_active = new ThornsTameFeedNoticeEntry { Message = message, Kind = kind, SecondsRemaining = seconds };
		UiRevisionBus.Publish( UiRevisionChannel.TameFeedNotice );
	}

	public static void Tick( float delta )
	{
		if ( _active is null )
			return;

		_active.SecondsRemaining -= delta;
		if ( _active.SecondsRemaining > 0f )
			return;

		_active = null;
		UiRevisionBus.Publish( UiRevisionChannel.TameFeedNotice );
	}
}
