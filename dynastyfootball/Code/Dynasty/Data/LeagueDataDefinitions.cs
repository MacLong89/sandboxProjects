using Dynasty.Core.Attributes;
using Dynasty.Core.Enums;
using Dynasty.Core.Interfaces;

namespace Dynasty.Data;

/// <summary>
/// Static data definitions. Replace file loading with JSON/asset pipeline as content grows.
/// </summary>
public sealed class LeagueDataDefinitions : ILeagueDataDefinitions
{
	public IReadOnlyList<TeamDefinition> Teams { get; } = BuildTeams();

	public IReadOnlyDictionary<Position, IReadOnlyList<string>> AttributeKeysByPosition { get; } =
		PlayerAttributeKeys.ByPosition.ToDictionary( kvp => kvp.Key, kvp => (IReadOnlyList<string>)kvp.Value );

	public IReadOnlyList<string> FirstNames { get; } = new List<string>
	{
		"Marcus", "Jaylen", "Tyler", "DeShawn", "Caleb", "Jordan", "Aiden", "Malik",
		"Connor", "Elijah", "Nate", "Darius", "Kai", "Liam", "Andre", "Carlos"
	};

	public IReadOnlyList<string> LastNames { get; } = new List<string>
	{
		"Johnson", "Williams", "Brown", "Davis", "Miller", "Wilson", "Moore", "Taylor",
		"Anderson", "Thomas", "Jackson", "White", "Harris", "Martin", "Thompson", "Robinson"
	};

	public IReadOnlyList<string> Colleges { get; } = new List<string>
	{
		"Alabama", "Ohio State", "Georgia", "Michigan", "Texas", "USC", "Oregon", "Clemson",
		"LSU", "Penn State", "Notre Dame", "Florida", "Oklahoma", "Miami", "Wisconsin", "Stanford"
	};

	static List<TeamDefinition> BuildTeams()
	{
		var cities = new List<(string City, string Name, string Abbr)>
		{
			("Kansas City", "Chiefs", "KC"),
			("Buffalo", "Bills", "BUF"),
			("Philadelphia", "Eagles", "PHI"),
			("San Francisco", "49ers", "SF"),
			("Dallas", "Cowboys", "DAL"),
			("Miami", "Dolphins", "MIA"),
			("Detroit", "Lions", "DET"),
			("Baltimore", "Ravens", "BAL"),
			("Cincinnati", "Bengals", "CIN"),
			("Green Bay", "Packers", "GB"),
			("Seattle", "Seahawks", "SEA"),
			("Minnesota", "Vikings", "MIN"),
			("Jacksonville", "Jaguars", "JAX"),
			("Los Angeles", "Chargers", "LAC"),
			("Cleveland", "Browns", "CLE"),
			("Houston", "Texans", "HOU"),
			("Pittsburgh", "Steelers", "PIT"),
			("Atlanta", "Falcons", "ATL"),
			("Tampa Bay", "Buccaneers", "TB"),
			("Indianapolis", "Colts", "IND"),
			("New Orleans", "Saints", "NO"),
			("Las Vegas", "Raiders", "LV"),
			("Denver", "Broncos", "DEN"),
			("Arizona", "Cardinals", "ARI"),
			("Chicago", "Bears", "CHI"),
			("Los Angeles", "Rams", "LAR"),
			("New York", "Giants", "NYG"),
			("New York", "Jets", "NYJ"),
			("Tennessee", "Titans", "TEN"),
			("New England", "Patriots", "NE"),
			("Washington", "Commanders", "WAS"),
			("Carolina", "Panthers", "CAR")
		};

		var list = new List<TeamDefinition>();
		for ( var i = 0; i < cities.Count; i++ )
		{
			var entry = cities[i];
			list.Add( new TeamDefinition
			{
				Key = entry.Abbr.ToLowerInvariant(),
				City = entry.City,
				Name = entry.Name,
				Abbreviation = entry.Abbr,
				PrimaryColor = "#1a1a2e",
				SecondaryColor = "#e94560",
				Stadium = $"{entry.City} Stadium"
			} );
		}

		return list;
	}
}
