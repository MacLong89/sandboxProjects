namespace Terraingen.GameData;

/// <summary>Display copy for journal world-event entries (CompletedEventIds).</summary>
public static class ThornsJournalEventCatalog
{
	static readonly Dictionary<string, string> DisplayNames = new( StringComparer.OrdinalIgnoreCase )
	{
		["event_supply_drop"] = "Supply Drop Looted",
		["event_military_cache"] = "Military Cache Opened",
		["event_town_visit"] = "Town Discovered",
		["event_guild_outpost"] = "Rival Guild Outpost Found",
		["event_radio_shop"] = "Radio Shop Visited",
		["event_bloom_purified"] = "Bloom Seed Purified"
	};

	static readonly Dictionary<string, string> MilestoneTokens = new( StringComparer.OrdinalIgnoreCase )
	{
		["loot_airdrop"] = "event_supply_drop",
		["loot_military"] = "event_military_cache",
		["visit_town"] = "event_town_visit",
		["discover_guild_outpost"] = "event_guild_outpost",
		["open_radio_shop"] = "event_radio_shop"
	};

	public static string DisplayName( string eventId )
	{
		if ( string.IsNullOrWhiteSpace( eventId ) )
			return "";

		return DisplayNames.TryGetValue( eventId, out var name ) ? name : eventId;
	}

	public static bool TryJournalEventIdForMilestoneToken( string eventToken, out string journalEventId ) =>
		MilestoneTokens.TryGetValue( eventToken ?? "", out journalEventId );
}
