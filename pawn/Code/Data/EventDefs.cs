namespace PawnShop;

/// <summary>A market or shop event that colors one day.</summary>
public sealed class EventDef
{
	public string Id { get; init; }
	public string Name { get; init; }
	public string Description { get; init; }
	public string Icon { get; init; } = "event";
	/// <summary>Categories whose demand/value shifts today (multiplier).</summary>
	public ItemCategory[] Categories { get; init; } = Array.Empty<ItemCategory>();
	public float DemandMult { get; init; } = 1f;
	/// <summary>Customer traffic multiplier.</summary>
	public float TrafficMult { get; init; } = 1f;
	/// <summary>Scam/counterfeit frequency multiplier.</summary>
	public float ScamMult { get; init; } = 1f;
	/// <summary>Theft risk multiplier.</summary>
	public float TheftMult { get; init; } = 1f;
	public bool IsGood { get; init; } = true;
}

public static class EventCatalog
{
	public static readonly List<EventDef> All = new()
	{
		new EventDef { Id = "collector_con", Name = "Collector Convention", Icon = "toys", IsGood = true,
			Categories = new[]{ ItemCategory.Collectibles, ItemCategory.Memorabilia }, DemandMult = 1.5f, TrafficMult = 1.2f,
			Description = "A collector convention is in town — collectibles and memorabilia are hot today." },
		new EventDef { Id = "chip_shortage", Name = "Electronics Shortage", Icon = "memory", IsGood = true,
			Categories = new[]{ ItemCategory.Electronics, ItemCategory.Gaming, ItemCategory.Cameras }, DemandMult = 1.4f,
			Description = "A supply shortage has electronics prices spiking." },
		new EventDef { Id = "gold_rally", Name = "Gold Price Rally", Icon = "trending_up", IsGood = true,
			Categories = new[]{ ItemCategory.Jewelry, ItemCategory.Watches }, DemandMult = 1.45f,
			Description = "Precious metal prices rallied overnight. Jewelry and watches are worth more." },
		new EventDef { Id = "band_town", Name = "Touring Band In Town", Icon = "music_note", IsGood = true,
			Categories = new[]{ ItemCategory.Instruments }, DemandMult = 1.5f, TrafficMult = 1.15f,
			Description = "A touring band lost half its gear. Musicians are hunting instruments." },
		new EventDef { Id = "police_sweep", Name = "Police Sweep", Icon = "local_police", IsGood = false,
			ScamMult = 0.4f, TrafficMult = 0.85f,
			Description = "Police are checking pawn shops for stolen goods today. Anything flagged WILL be found." },
		new EventDef { Id = "heatwave", Name = "Heatwave", Icon = "device_thermostat", IsGood = true,
			Categories = new[]{ ItemCategory.Appliances }, DemandMult = 1.5f,
			Description = "A brutal heatwave. Appliances — anything with a fan — are flying off shelves." },
		new EventDef { Id = "tourist_week", Name = "Tourist Week", Icon = "luggage", IsGood = true,
			TrafficMult = 1.4f, Categories = new[]{ ItemCategory.Memorabilia, ItemCategory.Art }, DemandMult = 1.2f,
			Description = "Tourists everywhere. More walk-ins, and they love souvenirs and art." },
		new EventDef { Id = "market_dip", Name = "Market Dip", Icon = "trending_down", IsGood = false,
			Categories = new[]{ ItemCategory.Jewelry, ItemCategory.Art, ItemCategory.Watches }, DemandMult = 0.7f,
			Description = "A rough day for the markets. Luxury spending has dried up." },
		new EventDef { Id = "celebrity_rumor", Name = "Celebrity Rumor", Icon = "star", IsGood = true,
			Categories = new[]{ ItemCategory.Memorabilia, ItemCategory.Collectibles }, DemandMult = 1.6f,
			Description = "A film star was spotted downtown. Memorabilia fever grips the city." },
		new EventDef { Id = "power_flicker", Name = "Grid Problems", Icon = "power_off", IsGood = false,
			TrafficMult = 0.7f,
			Description = "Rolling power problems across the district. Foot traffic is way down." },
		new EventDef { Id = "street_festival", Name = "Street Festival", Icon = "festival", IsGood = true,
			TrafficMult = 1.5f, TheftMult = 1.4f,
			Description = "A festival packs the street — lots of customers, and lots of light fingers." },
		new EventDef { Id = "con_artist_crew", Name = "Con Artist Crew", Icon = "theater_comedy", IsGood = false,
			ScamMult = 2.5f,
			Description = "Word is a counterfeit crew is working the neighborhood. Check everything twice." },
		new EventDef { Id = "diy_season", Name = "DIY Season", Icon = "construction", IsGood = true,
			Categories = new[]{ ItemCategory.Tools }, DemandMult = 1.45f,
			Description = "Everyone's renovating. Tools are in serious demand." },
		new EventDef { Id = "big_game", Name = "Championship Weekend", Icon = "sports", IsGood = true,
			Categories = new[]{ ItemCategory.Sports, ItemCategory.Memorabilia }, DemandMult = 1.4f,
			Description = "The championship is this weekend. Sports gear and memorabilia surge." },
	};

	private static Dictionary<string, EventDef> _byId;
	public static EventDef Get( string id )
	{
		_byId ??= All.ToDictionary( d => d.Id );
		return id is not null && _byId.TryGetValue( id, out var d ) ? d : null;
	}

	public static EventDef Random() => All[Game.Random.Int( 0, All.Count - 1 )];
}
