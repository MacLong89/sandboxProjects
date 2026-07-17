namespace Offshore;

/// <summary>
/// Unique bait × time × weather × fish (and tag) interactions.
/// Returns spawn / value / bite multipliers plus a short flavor label for the catch card.
/// </summary>
public static class FishInteractionSystem
{
	public readonly struct Result
	{
		public float SpawnMul { get; init; }
		public float ValueMul { get; init; }
		public float BiteMul { get; init; }
		public string Label { get; init; }

		public static Result Neutral => new() { SpawnMul = 1f, ValueMul = 1f, BiteMul = 1f, Label = "" };
	}

	public static Result Evaluate( FishDefinition fish, FishConditionContext ctx )
	{
		if ( fish is null || ctx is null )
			return Result.Neutral;

		var spawn = 1f;
		var value = 1f;
		var bite = 1f;
		string bestLabel = null;
		var bestScore = 0f;

		void Hit( float spawnMul, float valueMul, float biteMul, string label, float score = 1f )
		{
			spawn *= spawnMul;
			value *= valueMul;
			bite *= biteMul;
			if ( score > bestScore && !string.IsNullOrWhiteSpace( label ) )
			{
				bestScore = score;
				bestLabel = label;
			}
		}

		var bait = ctx.BaitId ?? "worm";
		var tod = ctx.TimeOfDay;
		var weather = ctx.Weather;
		var offshore = ctx.Offshore01;

		// --- Bait × species (strong match) ---
		switch ( bait, fish.Id )
		{
			case ("worm", "bluegill"):
			case ("worm", "perch"):
				Hit( 1.2f, 1.05f, 1.1f, "Worms love dock panfish", 1.1f );
				break;
			case ("minnow", "bass"):
			case ("minnow", "pike"):
				Hit( 1.35f, 1.08f, 1.12f, "Live minnow match", 1.4f );
				break;
			case ("shrimp", "redsnapper"):
				Hit( 1.4f, 1.12f, 1.1f, "Shrimp on the reef", 1.5f );
				break;
			case ("squid", "tuna"):
			case ("squid", "mahi"):
			case ("squid", "ocean_tuna"):
				Hit( 1.35f, 1.15f, 1.08f, "Squid in blue water", 1.5f );
				break;
			case ("deep_lure", "marlin"):
			case ("deep_lure", "great_white"):
			case ("deep_lure", "swordfish_juv"):
				Hit( 1.55f, 1.2f, 0.95f, "Deep lure on a legend", 2f );
				break;
		}

		// --- Bait × tags ---
		if ( HasTag( fish, "bottom" ) && bait == "worm" )
			Hit( 1.15f, 1.04f, 1.05f, "Worms on the bottom", 0.9f );
		if ( HasTag( fish, "predator" ) && bait == "minnow" )
			Hit( 1.2f, 1.06f, 1.08f, "Predator on minnows", 1f );
		if ( HasTag( fish, "reef" ) && bait == "shrimp" )
			Hit( 1.25f, 1.1f, 1.05f, "Reef shrimp bite", 1.2f );
		if ( HasTag( fish, "pelagic" ) && bait == "squid" )
			Hit( 1.22f, 1.12f, 1.05f, "Pelagic squid run", 1.3f );
		if ( HasTag( fish, "deep" ) && bait == "deep_lure" )
			Hit( 1.3f, 1.14f, 1f, "Deep lure working", 1.4f );

		// --- Time of day ---
		if ( tod == TimeOfDay.Dawn )
		{
			if ( fish.Rarity == FishRarity.Common || HasTag( fish, "surface" ) )
				Hit( 1.2f, 1.05f, 1.15f, "Dawn surface bite", 1.2f );
			if ( HasTag( fish, "nocturnal" ) )
				Hit( 0.7f, 1f, 0.9f, null, 0f );
		}

		if ( tod == TimeOfDay.Day && HasTag( fish, "pelagic" ) && weather == WeatherType.Clear )
			Hit( 1.18f, 1.08f, 1.05f, "Clear-day bluewater", 1.3f );

		if ( tod == TimeOfDay.Dusk )
		{
			if ( HasTag( fish, "predator" ) || fish.Rarity >= FishRarity.Uncommon )
				Hit( 1.25f, 1.1f, 1.1f, "Dusk predator window", 1.4f );
		}

		if ( tod == TimeOfDay.Night )
		{
			if ( HasTag( fish, "nocturnal" ) || fish.Id is "catfish" or "pike" or "great_white" or "walleye" )
				Hit( 1.4f, 1.15f, 1.12f, "Night stalker", 1.6f );
			if ( HasTag( fish, "surface" ) && fish.Rarity == FishRarity.Common )
				Hit( 0.65f, 1f, 0.85f, null, 0f );
		}

		// --- Weather ---
		if ( weather == WeatherType.Rain )
		{
			if ( HasTag( fish, "bottom" ) || fish.Rarity >= FishRarity.Uncommon )
				Hit( 1.2f, 1.08f, 0.95f, "Rain stirs the bottom", 1.3f );
			if ( bait == "worm" && HasTag( fish, "bottom" ) )
				Hit( 1.15f, 1.06f, 1.05f, "Rain + worms", 1.5f );
		}

		if ( weather == WeatherType.Storm )
		{
			if ( fish.Rarity >= FishRarity.Rare || HasTag( fish, "deep" ) )
				Hit( 1.45f, 1.22f, 0.85f, "Storm brings giants", 1.8f );
			if ( bait == "deep_lure" && (fish.IsLegendary || HasTag( fish, "deep" )) )
				Hit( 1.35f, 1.25f, 0.9f, "Storm + deep lure!", 2.2f );
			if ( HasTag( fish, "surface" ) && fish.Rarity == FishRarity.Common )
				Hit( 0.55f, 1f, 0.8f, null, 0f );
		}

		if ( weather == WeatherType.Fog )
		{
			if ( HasTag( fish, "ambush" ) || fish.Id is "pike" or "bass" or "walleye" )
				Hit( 1.35f, 1.12f, 1.05f, "Fog ambush", 1.5f );
		}

		if ( weather == WeatherType.Windy )
		{
			if ( HasTag( fish, "surface" ) || HasTag( fish, "pelagic" ) )
				Hit( 1.2f, 1.08f, 1.05f, "Wind chops the surface", 1.2f );
			if ( bait == "minnow" && HasTag( fish, "predator" ) )
				Hit( 1.15f, 1.05f, 1.08f, "Windy minnow bite", 1.3f );
		}

		if ( weather == WeatherType.Cloudy && tod == TimeOfDay.Day && fish.Rarity >= FishRarity.Uncommon )
			Hit( 1.12f, 1.05f, 1.05f, "Overcast window", 0.8f );

		// --- Combo specialties ---
		if ( bait == "shrimp" && weather == WeatherType.Rain && HasTag( fish, "reef" ) )
			Hit( 1.25f, 1.18f, 1.1f, "Rainy reef shrimp feast", 2f );

		if ( bait == "squid" && tod == TimeOfDay.Night && HasTag( fish, "pelagic" ) )
			Hit( 1.3f, 1.2f, 1.1f, "Night squid run", 2f );

		if ( bait == "minnow" && tod == TimeOfDay.Dusk && weather != WeatherType.Storm && HasTag( fish, "predator" ) )
			Hit( 1.22f, 1.12f, 1.15f, "Dusk minnow hunt", 1.7f );

		if ( bait == "deep_lure" && tod == TimeOfDay.Night && weather == WeatherType.Storm && fish.IsLegendary )
			Hit( 1.5f, 1.35f, 0.9f, "Legendary storm night!", 3f );

		if ( bait == "worm" && tod == TimeOfDay.Dawn && weather == WeatherType.Clear && HasTag( fish, "surface" ) )
			Hit( 1.25f, 1.08f, 1.2f, "Clear dawn dock bite", 1.6f );

		// Offshore amplifies “unique” interaction value a bit (far = conditions matter more).
		if ( offshore > 0.45f && bestScore >= 1.4f )
			value *= MathX.Lerp( 1f, 1.18f, (offshore - 0.45f) / 0.55f );

		return new Result
		{
			SpawnMul = Math.Clamp( spawn, 0.2f, 6f ),
			ValueMul = Math.Clamp( value, 0.85f, 2.5f ),
			BiteMul = Math.Clamp( bite, 0.55f, 1.6f ),
			Label = bestLabel ?? ""
		};
	}

	public static float CombinedBiteSpeed( PlayerProgressionData progress, FishDefinition fish, FishConditionContext ctx )
	{
		var weather = TimeWeatherSystem.BiteSpeedMultiplier( progress );
		var interaction = Evaluate( fish, ctx ).BiteMul;
		// Slightly snappier bites far from the dock when conditions align.
		var offshoreBite = MathX.Lerp( 1f, 1.08f, ctx?.Offshore01 ?? 0f );
		return weather * interaction * offshoreBite;
	}

	public static bool HasTag( FishDefinition fish, string tag )
	{
		if ( fish?.Tags is null || string.IsNullOrWhiteSpace( tag ) )
			return false;

		foreach ( var t in fish.Tags )
		{
			if ( string.Equals( t, tag, StringComparison.OrdinalIgnoreCase ) )
				return true;
		}

		return false;
	}
}
