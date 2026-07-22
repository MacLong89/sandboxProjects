namespace NoFly;

public sealed class FlightInfo
{
	public string FlightNumber { get; set; }
	public string Airline { get; set; }
	public string Destination { get; set; }
	public string Gate { get; set; }
	public FlightStatus Status { get; set; } = FlightStatus.CheckIn;
	public float BoardingOpenAt { get; set; }
	public float BoardingCloseAt { get; set; }
}

public sealed class ObjectiveDef
{
	public string Id { get; init; }
	public string Label { get; init; }
	public string Hint { get; init; }
	public string ZoneTag { get; init; }
	public int Score { get; init; } = 50;
}

public sealed class PlayerObjective
{
	public string Id { get; set; }
	public string Label { get; set; }
	public string Hint { get; set; }
	public string ZoneTag { get; set; }
	public int Score { get; set; }
	public bool Completed { get; set; }
}

public static class FlightCatalog
{
	public static readonly string[] Airlines = { "SkyNoodle Air", "CloudHop", "BananaJet", "Waffle Wings", "PuddleHopper" };
	public static readonly string[] Destinations = { "Sunnyvale", "Frostport", "Melody Bay", "Cactus City", "Moon Harbor", "Pepper Peak" };
	public static readonly string[] Gates = { "A1", "B2", "C3" };

	public static List<FlightInfo> GenerateFlights( RoundSettings settings )
	{
		var list = new List<FlightInfo>();
		for ( var i = 0; i < Gates.Length; i++ )
		{
			list.Add( new FlightInfo
			{
				FlightNumber = $"{Airlines[i % Airlines.Length][..2].ToUpper()}-{100 + i * 17}",
				Airline = Airlines[i % Airlines.Length],
				Destination = Destinations[i % Destinations.Length],
				Gate = Gates[i],
				Status = FlightStatus.SecurityOpen,
				BoardingOpenAt = settings.BoardingStartsAtSeconds,
				BoardingCloseAt = settings.RoundDurationSeconds - 5f
			} );
		}
		return list;
	}
}

public static class ObjectiveCatalog
{
	public static readonly List<ObjectiveDef> All = new()
	{
		new() { Id = "buy_drink", Label = "Buy a Drink", Hint = "Visit the food stand.", ZoneTag = "shop_food", Score = 40 },
		new() { Id = "buy_snack", Label = "Buy a Snack", Hint = "Grab something from the food stand.", ZoneTag = "shop_food", Score = 40 },
		new() { Id = "gift_shop", Label = "Visit Gift Shop", Hint = "Browse the gift shop.", ZoneTag = "shop_gift", Score = 40 },
		new() { Id = "sit_wait", Label = "Sit and Wait", Hint = "Sit in the waiting area.", ZoneTag = "seating", Score = 30 },
		new() { Id = "photo_spot", Label = "Take a Photo", Hint = "Pose at the landmark windows.", ZoneTag = "landmark", Score = 35 },
		new() { Id = "find_gate", Label = "Find Your Gate", Hint = "Reach your assigned gate early.", ZoneTag = "gate", Score = 45 },
		new() { Id = "restroom", Label = "Find Restrooms", Hint = "Visit the restroom hallway.", ZoneTag = "restroom", Score = 25 },
		new() { Id = "report_ok", Label = "Stay Alert", Hint = "Report real suspicious activity if you see it.", ZoneTag = "any", Score = 50 },
		new() { Id = "speak_npc", Label = "Chat with Traveler", Hint = "Talk to another passenger near seating.", ZoneTag = "seating", Score = 30 },
		new() { Id = "departure_board", Label = "Check Departures", Hint = "Read the departure board.", ZoneTag = "departure_board", Score = 25 }
	};

	public static List<PlayerObjective> PickForPassenger( int count = 2 )
	{
		var picks = All.OrderBy( _ => Random.Shared.NextDouble() ).Take( count ).ToList();
		return picks.Select( p => new PlayerObjective
		{
			Id = p.Id,
			Label = p.Label,
			Hint = p.Hint,
			ZoneTag = p.ZoneTag,
			Score = p.Score
		} ).ToList();
	}
}

public static class ClueCatalog
{
	public static List<string> BuildClues( NoFlyPlayer smuggler, BagInstance bag, DocumentInstance doc )
	{
		var clues = new List<string>();
		if ( smuggler is null ) return clues;

		var options = new List<string>
		{
			$"Suspect is traveling to Gate {smuggler.AssignedGate?[..1] ?? "C"}",
			bag is not null ? $"Suspect carries a {(bag.SuitcaseColor.r > 0.7f ? "bright" : "dark")} suitcase" : "Suspect carries unusual luggage",
			doc is not null ? $"Suspect has a document from {DocumentCatalog.GetTemplate( doc.TemplateId ).CountryName}" : "Suspect uses a foreign document",
			bag?.HasContraband == true ? $"Contraband is {LuggageCatalog.GetContraband( bag.ContrabandId ).Category}" : "Contraband is unusual",
			smuggler.AppearanceHasHat ? "Suspect is wearing a hat" : "Suspect entered through the main entrance",
			"Suspect's flight departs soon",
			"Suspect seems oddly calm in queues"
		};

		return options.OrderBy( _ => Random.Shared.NextDouble() ).Take( 3 ).ToList();
	}
}
