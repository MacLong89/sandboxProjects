namespace Terraingen.UI;

using Terraingen.GameData;
using Terraingen.UI.Core;

/// <summary>Top-right world event alerts — airdrops, bloom seeds, guild expansion.</summary>
public enum ThornsWorldEventAlertKind : byte
{
	Generic,
	Airdrop,
	BloomSeed,
	OutpostGrowth
}

public enum ThornsWorldEventFeedKind : byte
{
	WorldEvent,
	Milestone
}

public sealed class ThornsWorldEventHudEntry
{
	public string Id { get; set; } = Guid.NewGuid().ToString( "N" );
	public ThornsWorldEventFeedKind Kind { get; set; } = ThornsWorldEventFeedKind.WorldEvent;
	public ThornsWorldEventAlertKind AlertKind { get; set; } = ThornsWorldEventAlertKind.Generic;
	public string Title { get; set; } = "";
	public string Message { get; set; } = "";
	public string IconPath { get; set; } = "";
	public int XpReward { get; set; }
	public float SecondsRemaining { get; set; } = 6f;
	public bool ShowTimer { get; set; }
}

public static class ThornsWorldEventHudBus
{
	const int MaxVisible = 1;
	const float DedupeWindowSeconds = 1.25f;
	const float AirdropAlertSeconds = 165f;
	const float BloomAlertSeconds = 45f;
	const float OutpostAlertSeconds = 30f;

	static readonly List<ThornsWorldEventHudEntry> Entries = new();
	static float _secondsSinceLastPush;
	static string _lastMessage;

	public static IReadOnlyList<ThornsWorldEventHudEntry> Active => Entries;

	public static void PushWorldEvent( string message, float seconds = 6f )
	{
		if ( string.IsNullOrWhiteSpace( message ) )
			return;

		if ( !ThornsUiInputGate.AllowsTransientFeedback )
			return;

		InsertEntry( new ThornsWorldEventHudEntry
		{
			Kind = ThornsWorldEventFeedKind.WorldEvent,
			AlertKind = ThornsWorldEventAlertKind.Generic,
			Title = "WORLD EVENT",
			Message = message.Trim(),
			IconPath = ThornsIconRegistry.Hud( "events" ),
			SecondsRemaining = seconds,
			ShowTimer = false
		} );
	}

	public static void PushAirdropIncoming( float worldX, float worldY, float seconds = AirdropAlertSeconds )
	{
		if ( !ThornsUiInputGate.AllowsTransientFeedback )
			return;

		var location = ThornsWorldEventLocation.ResolveNear( worldX, worldY );
		InsertEntry( new ThornsWorldEventHudEntry
		{
			Kind = ThornsWorldEventFeedKind.WorldEvent,
			AlertKind = ThornsWorldEventAlertKind.Airdrop,
			Title = "AIRDROP INCOMING",
			Message = $"A supply drop is falling near {location}.",
			IconPath = ThornsIconRegistry.MapMarker( ThornsMapMarkerKind.Airdrop ),
			SecondsRemaining = seconds,
			ShowTimer = true
		} );
	}

	public static void PushBloomSeedDetected( float worldX, float worldY, float seconds = BloomAlertSeconds )
	{
		if ( !ThornsUiInputGate.AllowsTransientFeedback )
			return;

		var location = ThornsWorldEventLocation.ResolveNear( worldX, worldY );
		InsertEntry( new ThornsWorldEventHudEntry
		{
			Kind = ThornsWorldEventFeedKind.WorldEvent,
			AlertKind = ThornsWorldEventAlertKind.BloomSeed,
			Title = "BLOOM SEED DETECTED",
			Message = $"A Bloom Seed has appeared near {location}. Check your map.",
			IconPath = ThornsIconRegistry.MapMarker( ThornsMapMarkerKind.BloomSeed ),
			SecondsRemaining = seconds,
			ShowTimer = true
		} );
	}

	public static void PushOutpostGrowth( string guildName, int count, int target, float seconds = OutpostAlertSeconds )
	{
		if ( !ThornsUiInputGate.AllowsTransientFeedback )
			return;

		var name = string.IsNullOrWhiteSpace( guildName ) ? "A rival guild" : guildName.Trim();
		InsertEntry( new ThornsWorldEventHudEntry
		{
			Kind = ThornsWorldEventFeedKind.WorldEvent,
			AlertKind = ThornsWorldEventAlertKind.OutpostGrowth,
			Title = "OUTPOST GROWTH",
			Message = $"{name} raised a new outpost ({count}/{target}).",
			IconPath = ThornsIconRegistry.MapMarker( ThornsMapMarkerKind.NpcGuildOutpost ),
			SecondsRemaining = seconds,
			ShowTimer = true
		} );
	}

	public static void PushMilestone( string title, int xpReward = 0, float seconds = 5.5f )
	{
		if ( string.IsNullOrWhiteSpace( title ) )
			return;

		InsertEntry( new ThornsWorldEventHudEntry
		{
			Kind = ThornsWorldEventFeedKind.Milestone,
			AlertKind = ThornsWorldEventAlertKind.Generic,
			Title = title.Trim().ToUpperInvariant(),
			Message = "",
			IconPath = ThornsIconRegistry.Hud( "events" ),
			XpReward = Math.Max( 0, xpReward ),
			SecondsRemaining = seconds,
			ShowTimer = false
		} );
	}

	static void InsertEntry( ThornsWorldEventHudEntry entry )
	{
		var dedupeKey = $"{entry.AlertKind}|{entry.Title}|{entry.Message}";
		if ( string.Equals( _lastMessage, dedupeKey, StringComparison.OrdinalIgnoreCase )
		     && _secondsSinceLastPush <= DedupeWindowSeconds )
			return;

		Entries.Insert( 0, entry );

		while ( Entries.Count > MaxVisible )
			Entries.RemoveAt( Entries.Count - 1 );

		_lastMessage = dedupeKey;
		_secondsSinceLastPush = 0f;
		UiRevisionBus.Publish( UiRevisionChannel.WorldEvents );
	}

	public static void Tick( float delta )
	{
		_secondsSinceLastPush += delta;

		if ( Entries.Count == 0 )
			return;

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

		if ( changed || Entries.Count > 0 )
			UiRevisionBus.Publish( UiRevisionChannel.WorldEvents );
	}

	public static string FormatTimer( float secondsRemaining )
	{
		var total = Math.Max( 0, (int)MathF.Ceiling( secondsRemaining ) );
		var minutes = total / 60;
		var seconds = total % 60;
		return $"{minutes:00}:{seconds:00}";
	}
}
