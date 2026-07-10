using Dynasty.Core.Attributes;
using Dynasty.Core.Identifiers;
using Dynasty.Domain.League;

namespace Dynasty.UI.ViewModels;

public sealed class ProspectDetailViewModel
{
	public PlayerId Id { get; init; }
	public string Name { get; init; } = "";
	public string Position { get; init; } = "";
	public int Age { get; init; }
	public string College { get; init; } = "";
	public string Hometown { get; init; } = "";
	public string Overall { get; init; } = "";
	public string Potential { get; init; } = "";
	public int ConsensusRank { get; init; }
	public string Backstory { get; init; } = "";
	public string Health { get; init; } = "Healthy";
	public string Morale { get; init; } = "";
	public IReadOnlyList<ScoutedAttributeRow> Attributes { get; init; } = Array.Empty<ScoutedAttributeRow>();
	public IReadOnlyList<string> Traits { get; init; } = Array.Empty<string>();
	public IReadOnlyList<PersonalityRow> Personality { get; init; } = Array.Empty<PersonalityRow>();
	public string PersonalityNote { get; init; } = "";
	public int ScoutConfidence { get; init; }
	public bool HasHiddenTraits { get; init; }
	public string HiddenTraitHint { get; init; } = "";

	public static ProspectDetailViewModel From( LeagueState state, PlayerId prospectId )
	{
		if ( state == null )
			return null;

		var prospect = state.Draft.Prospects.FirstOrDefault( p => p.Id.Value == prospectId.Value );
		if ( prospect == null )
			return null;

		var player = prospect.Player;
		var scouting = player.Scouting;
		var confidence = scouting.ScoutConfidence;

		return new ProspectDetailViewModel
		{
			Id = prospect.Id,
			Name = player.Identity.FullName,
			Position = player.Identity.Position.ToString(),
			Age = player.Identity.Age,
			College = player.Identity.College,
			Hometown = string.IsNullOrEmpty( player.Identity.Hometown ) ? "Unknown" : player.Identity.Hometown,
			Overall = scouting.OverallRevealed ? player.Ratings.Overall.ToString() : "??",
			Potential = scouting.PotentialRevealed ? player.Ratings.Potential.ToString() : "??",
			ConsensusRank = prospect.ConsensusRank,
			Backstory = player.Identity.Backstory,
			Health = "Healthy",
			Morale = confidence >= 60 ? player.Morale.Morale.ToString() : "??",
			Attributes = BuildAttributes( player, scouting ),
			Traits = scouting.RevealedTraits.Select( t => t.ToString() ).ToList(),
			Personality = BuildPersonality( player, confidence, out var note ),
			PersonalityNote = note,
			ScoutConfidence = confidence,
			HasHiddenTraits = player.HiddenTraits.Count > 0 && !player.HiddenTraitsRevealed,
			HiddenTraitHint = player.HiddenTraits.Count > 0 && !player.HiddenTraitsRevealed
				? "Scouts sense unknown traits — may reveal mid-rookie season."
				: ""
		};
	}

	static IReadOnlyList<ScoutedAttributeRow> BuildAttributes( Domain.Players.PlayerState player, Domain.Players.ScoutingRevealState scouting )
	{
		if ( !PlayerAttributeKeys.ByPosition.TryGetValue( player.Identity.Position, out var keys ) )
			return Array.Empty<ScoutedAttributeRow>();

		return keys
			.Select( key => new ScoutedAttributeRow
			{
				Key = FormatAttribute( key ),
				RawKey = key,
				Value = scouting.RevealedAttributes.Contains( key ) && player.Ratings.Attributes.TryGetValue( key, out var val )
					? val.ToString()
					: "??"
			} )
			.ToList();
	}

	static IReadOnlyList<PersonalityRow> BuildPersonality( Domain.Players.PlayerState player, int confidence, out string note )
	{
		if ( confidence >= 75 )
		{
			note = "";
			return new[]
			{
				new PersonalityRow { Label = "Ambition", Value = player.Personality.Ambition },
				new PersonalityRow { Label = "Loyalty", Value = player.Personality.Loyalty },
				new PersonalityRow { Label = "Leadership", Value = player.Personality.Leadership },
				new PersonalityRow { Label = "Work Ethic", Value = player.Personality.WorkEthic },
				new PersonalityRow { Label = "Temperament", Value = player.Personality.Temperament },
				new PersonalityRow { Label = "Ego", Value = player.Personality.Ego },
				new PersonalityRow { Label = "Marketability", Value = player.Personality.Marketability }
			};
		}

		if ( confidence >= 50 )
		{
			note = "";
			return new[]
			{
				new PersonalityRow { Label = "Ambition", Value = player.Personality.Ambition },
				new PersonalityRow { Label = "Work Ethic", Value = player.Personality.WorkEthic },
				new PersonalityRow { Label = "Leadership", Value = player.Personality.Leadership },
				new PersonalityRow { Label = "Temperament", Value = player.Personality.Temperament }
			};
		}

		if ( confidence >= 25 )
		{
			note = "";
			return new[]
			{
				new PersonalityRow { Label = "Work Ethic", Value = player.Personality.WorkEthic },
				new PersonalityRow { Label = "Temperament", Value = player.Personality.Temperament }
			};
		}

		note = "Increase scout confidence to reveal personality.";
		return Array.Empty<PersonalityRow>();
	}

	static string FormatAttribute( string key ) => key.Replace( '_', ' ' );
}

public sealed class ScoutedAttributeRow
{
	public string Key { get; init; } = "";
	public string RawKey { get; init; } = "";
	public string Value { get; init; } = "";
}
