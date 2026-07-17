namespace Offshore;

public enum TimeOfDay
{
	Dawn,
	Day,
	Dusk,
	Night
}

public enum WeatherType
{
	Clear,
	Cloudy,
	Rain,
	Windy,
	Storm,
	Fog
}

public static class TimeWeatherSystem
{
	public static void AdvanceOnTravel( PlayerProgressionData p )
	{
		var sky = OffshoreGameController.Instance?.DayNight;
		if ( sky is not null )
			sky.AddHours( 3.5f );
		else
			p.TimeOfDay = (TimeOfDay)(((int)p.TimeOfDay + 1) % 4);
		RollWeather( p );
	}

	public static void AdvanceOnCatch( PlayerProgressionData p )
	{
		p.CatchesSinceTimeAdvance++;
		if ( p.CatchesSinceTimeAdvance < 4 )
			return;
		p.CatchesSinceTimeAdvance = 0;

		var sky = OffshoreGameController.Instance?.DayNight;
		if ( sky is not null )
			sky.AddHours( 1.75f );
		else
			p.TimeOfDay = (TimeOfDay)(((int)p.TimeOfDay + 1) % 4);

		if ( Game.Random.Float() < 0.35f )
			RollWeather( p );
	}

	public static void RollWeather( PlayerProgressionData p )
	{
		var roll = Game.Random.Float();
		p.Weather = roll switch
		{
			< 0.45f => WeatherType.Clear,
			< 0.65f => WeatherType.Cloudy,
			< 0.78f => WeatherType.Rain,
			< 0.88f => WeatherType.Windy,
			< 0.95f => WeatherType.Fog,
			_ => WeatherType.Storm
		};
	}

	public static float SpawnWeightMultiplier( PlayerProgressionData p, FishDefinition fish )
	{
		var mult = 1f;
		if ( p.Weather == WeatherType.Rain && fish.Rarity >= FishRarity.Uncommon )
			mult *= 1.15f;
		if ( p.Weather == WeatherType.Storm && fish.Rarity >= FishRarity.Rare )
			mult *= 1.25f;
		if ( p.TimeOfDay == TimeOfDay.Night && fish.Id is "catfish" or "pike" or "great_white" )
			mult *= 1.3f;
		if ( p.TimeOfDay == TimeOfDay.Dawn && fish.Rarity == FishRarity.Common )
			mult *= 1.1f;
		return mult;
	}

	public static float BiteSpeedMultiplier( PlayerProgressionData p ) =>
		p.Weather switch
		{
			WeatherType.Clear => 1.05f,
			WeatherType.Rain => 0.95f,
			WeatherType.Storm => 0.85f,
			WeatherType.Fog => 0.9f,
			_ => 1f
		};
}

public static class BaitSystem
{
	public static readonly string[] Baits = ["worm", "minnow", "shrimp", "squid", "deep_lure"];

	public static string DisplayName( string id ) => id switch
	{
		"worm" => "Worm",
		"minnow" => "Minnow",
		"shrimp" => "Shrimp",
		"squid" => "Squid",
		"deep_lure" => "Deep Lure",
		_ => id
	};

	public static float Price( string id ) => id switch
	{
		"worm" => 0f,
		"minnow" => 35f,
		"shrimp" => 55f,
		"squid" => 90f,
		"deep_lure" => 160f,
		_ => 50f
	};

	public static string IconPath( string id ) => OffshoreSprites.Paths.IconHook;

	public static void EnsureDefaults( PlayerProgressionData p )
	{
		if ( p is null )
			return;
		p.OwnedBaitIds ??= new HashSet<string>( StringComparer.OrdinalIgnoreCase );
		p.OwnedBaitIds.Add( "worm" );
		if ( string.IsNullOrWhiteSpace( p.SelectedBaitId ) || !Owns( p, p.SelectedBaitId ) )
			p.SelectedBaitId = "worm";
	}

	public static bool Owns( PlayerProgressionData p, string baitId )
	{
		if ( p is null || string.IsNullOrWhiteSpace( baitId ) )
			return false;
		EnsureDefaults( p );
		return p.OwnedBaitIds.Contains( baitId );
	}

	public static bool TrySelect( PlayerProgressionData p, string baitId )
	{
		if ( p is null || !Baits.Contains( baitId ) || !Owns( p, baitId ) )
			return false;
		p.SelectedBaitId = baitId;
		return true;
	}

	public static bool TryBuy( OffshoreGameController game, string baitId )
	{
		if ( game?.Progression is null || !Baits.Contains( baitId ) )
			return false;

		var p = game.Progression;
		EnsureDefaults( p );
		if ( Owns( p, baitId ) )
		{
			TrySelect( p, baitId );
			game.SetStatus( $"Bait: {DisplayName( baitId )}" );
			return true;
		}

		var cost = Price( baitId );
		if ( p.Money < cost )
		{
			game.SetStatus( $"Need ${cost:N0} for {DisplayName( baitId )}" );
			return false;
		}

		p.Money -= cost;
		p.LifetimeMoneySpent += cost;
		p.OwnedBaitIds.Add( baitId );
		p.SelectedBaitId = baitId;
		OffshoreSaveSystem.Save( p );
		game.SetStatus( $"Bought {DisplayName( baitId )}  -  ${cost:N0}" );
		return true;
	}

	public static string CycleNext( PlayerProgressionData p )
	{
		EnsureDefaults( p );
		var owned = Baits.Where( b => Owns( p, b ) ).ToArray();
		if ( owned.Length == 0 )
		{
			p.SelectedBaitId = "worm";
			return "worm";
		}

		var cur = string.IsNullOrWhiteSpace( p.SelectedBaitId ) ? "worm" : p.SelectedBaitId;
		var idx = Array.FindIndex( owned, b => string.Equals( b, cur, StringComparison.OrdinalIgnoreCase ) );
		if ( idx < 0 )
			idx = 0;
		idx = (idx + 1) % owned.Length;
		p.SelectedBaitId = owned[idx];
		return p.SelectedBaitId;
	}

	public static float WeightMultiplier( string baitId, FishDefinition fish )
	{
		if ( fish is null )
			return 1f;
		return (baitId, fish.Id) switch
		{
			("worm", "bluegill") => 1.35f,
			("worm", "perch") => 1.25f,
			("minnow", "bass") => 1.4f,
			("minnow", "pike") => 1.35f,
			("shrimp", "redsnapper") => 1.4f,
			("squid", "tuna") => 1.35f,
			("squid", "mahi") => 1.3f,
			("deep_lure", "marlin") => 1.5f,
			("deep_lure", "great_white") => 1.45f,
			_ => 1f
		};
	}
}

public static class TournamentSystem
{
	public static bool IsActive( PlayerProgressionData p ) => p.TournamentActive;

	public static void StartHeaviest( PlayerProgressionData p )
	{
		p.TournamentActive = true;
		p.TournamentScore = 0f;
		p.TournamentBestWeight = 0f;
		p.TournamentName = "Heaviest Fish Challenge";
	}

	public static void NotifyCatch( PlayerProgressionData p, CatchRecord c )
	{
		if ( !p.TournamentActive )
			return;
		p.TournamentScore += c.Weight;
		if ( c.Weight > p.TournamentBestWeight )
			p.TournamentBestWeight = c.Weight;
	}

	public static string Finish( PlayerProgressionData p )
	{
		if ( !p.TournamentActive )
			return "No tournament active";

		p.TournamentActive = false;
		var aiScore = 2.5f + Game.Random.Float( 0f, 4f );
		var won = p.TournamentBestWeight >= aiScore;
		var reward = won ? 80f + p.TournamentBestWeight * 20f : 15f;
		p.Money += reward;
		p.LifetimeMoneyEarned += reward;
		p.TournamentsPlayed++;
		if ( won )
			p.TournamentsWon++;
		return won
			? $"Tournament WIN! Best {p.TournamentBestWeight:0.0} kg vs AI {aiScore:0.0} kg  -  +${reward:N0}"
			: $"Tournament loss. Best {p.TournamentBestWeight:0.0} kg vs AI {aiScore:0.0} kg  -  +${reward:N0}";
	}
}
