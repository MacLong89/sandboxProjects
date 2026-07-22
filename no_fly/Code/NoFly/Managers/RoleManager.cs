namespace NoFly;

public static class RoleManager
{
	public static void AssignRoles( NoFlyGame game )
	{
		var humans = game.AllPlayers( includeBots: false ).Where( p => !p.IsSpectator ).ToList();
		foreach ( var bot in game.AllPlayers().Where( p => p.IsBot ).ToList() )
			bot.GameObject.Destroy();

		if ( game.SinglePlayerMode && humans.Count == 1 )
		{
			AssignSolo( game, humans[0] );
			return;
		}

		var count = humans.Count;
		var plan = BuildPlan( count );

		// Preference weighting
		var pool = humans.OrderBy( _ => Random.Shared.NextDouble() ).ToList();
		var assigned = new HashSet<string>();

		NoFlyPlayer TakePreferred( TeamType preferTeam, RoleType role )
		{
			var match = pool.FirstOrDefault( p => !assigned.Contains( p.PlayerId ) && MatchesPref( p, preferTeam ) )
				?? pool.FirstOrDefault( p => !assigned.Contains( p.PlayerId ) );
			if ( match is null ) return null;
			assigned.Add( match.PlayerId );
			ApplyRole( match, role, game );
			return match;
		}

		TakePreferred( TeamType.Passenger, RoleType.Smuggler );
		if ( plan.HasUndercover ) TakePreferred( TeamType.Passenger, RoleType.UndercoverAgent );
		if ( plan.HasDocument ) TakePreferred( TeamType.Tsa, RoleType.DocumentAgent );
		if ( plan.HasScanner ) TakePreferred( TeamType.Tsa, RoleType.ScannerAgent );
		if ( plan.HasSecurity ) TakePreferred( TeamType.Tsa, RoleType.SecurityOfficer );

		foreach ( var p in pool.Where( p => !assigned.Contains( p.PlayerId ) ) )
			ApplyRole( p, RoleType.RegularPassenger, game );

		EnsureEssentialsWithBots( game, plan );
		SetupLoadouts( game );
	}

	static void AssignSolo( NoFlyGame game, NoFlyPlayer human )
	{
		ApplyRole( human, game.SoloForcedRole, game );
		var plan = new RolePlan { HasDocument = true, HasScanner = true, HasSecurity = true, HasUndercover = game.SoloForcedRole != RoleType.UndercoverAgent, HasSmuggler = game.SoloForcedRole != RoleType.Smuggler };
		EnsureEssentialsWithBots( game, plan );
		// Staff desks need a steady trickle of bags/docs — one passenger is not enough.
		if ( game.SoloForcedRole is RoleType.DocumentAgent or RoleType.ScannerAgent or RoleType.SecurityOfficer )
		{
			SpawnBot( game, RoleType.RegularPassenger, "Bot Passenger" );
			SpawnBot( game, RoleType.RegularPassenger, "Bot Passenger 2" );
			SpawnBot( game, RoleType.RegularPassenger, "Bot Passenger 3" );
		}
		else if ( game.SoloForcedRole != RoleType.RegularPassenger )
		{
			SpawnBot( game, RoleType.RegularPassenger, "Bot Passenger" );
		}
		SetupLoadouts( game );
	}

	static void EnsureEssentialsWithBots( NoFlyGame game, RolePlan plan )
	{
		bool Has( RoleType r ) => game.AllPlayers().Any( p => p.Role == r );
		if ( plan.HasSmuggler && !Has( RoleType.Smuggler ) ) SpawnBot( game, RoleType.Smuggler, "Bot Smuggler" );
		if ( plan.HasDocument && !Has( RoleType.DocumentAgent ) ) SpawnBot( game, RoleType.DocumentAgent, "Bot Docs" );
		if ( plan.HasScanner && !Has( RoleType.ScannerAgent ) ) SpawnBot( game, RoleType.ScannerAgent, "Bot Scanner" );
		if ( plan.HasSecurity && !Has( RoleType.SecurityOfficer ) ) SpawnBot( game, RoleType.SecurityOfficer, "Bot Security" );
		if ( plan.HasUndercover && !Has( RoleType.UndercoverAgent ) ) SpawnBot( game, RoleType.UndercoverAgent, "Bot Undercover" );
		if ( !game.AllPlayers().Any( p => p.Role == RoleType.RegularPassenger ) )
			SpawnBot( game, RoleType.RegularPassenger, "Bot Passenger" );
	}

	public static NoFlyPlayer SpawnBot( NoFlyGame game, RoleType role, string name )
	{
		var go = new GameObject( true, name );
		go.WorldPosition = game.Airport?.GetSpawn( "entrance" ) ?? game.WorldPosition;
		var bot = go.Components.Create<NoFlyPlayer>();
		bot.IsBot = true;
		bot.DisplayName = name;
		bot.PlayerId = $"bot_{Guid.NewGuid().ToString( "N" )[..6]}";
		bot.OutfitColor = Kit.RandomOutfit();
		bot.AppearanceHasHat = Random.Shared.NextDouble() > 0.6;
		go.Components.Create<BotController>();
		ApplyRole( bot, role, game );
		go.NetworkSpawn();
		return bot;
	}

	static void ApplyRole( NoFlyPlayer player, RoleType role, NoFlyGame game )
	{
		var info = RoleCatalog.Get( role );
		player.ResetForRound();
		player.Role = role;
		player.Team = info.Team;
		player.OutfitColor = player.OutfitColor == default ? Kit.RandomOutfit() : player.OutfitColor;
		player.ApplyAppearance();
		player.ObjectiveSummary = info.ShortObjective;
		game.TeleportToRoleSpawn( player );

		if ( role == RoleType.Smuggler ) game.SmugglerId = player.PlayerId;
		if ( role == RoleType.UndercoverAgent ) game.UndercoverId = player.PlayerId;
	}

	static void SetupLoadouts( NoFlyGame game )
	{
		foreach ( var p in game.AllPlayers() )
		{
			if ( p.NeedsSecurityCheck )
			{
				var flight = Random.Shared.FromList( game.Flights );
				p.FlightNumber = flight.FlightNumber;
				p.AssignedGate = flight.Gate;
				p.Document = DocumentCatalog.CreateValid( p.PlayerId );
				p.Document.Values[DocumentFieldType.Destination] = $"GATE {flight.Gate}";
				p.Document.Values[DocumentFieldType.Name] = p.DisplayName.ToUpperInvariant();
				p.Bag = p.Role == RoleType.Smuggler
					? LuggageCatalog.CreateSmugglerBag( p.PlayerId )
					: LuggageCatalog.CreateCleanBag( p.PlayerId );
				p.Objectives = ObjectiveCatalog.PickForPassenger( 2 );
			}

			if ( p.Role == RoleType.UndercoverAgent )
			{
				var smug = game.FindPlayer( game.SmugglerId );
				p.Clues = ClueCatalog.BuildClues( smug, smug?.Bag, smug?.Document );
				p.ArrestAvailable = true;
			}

			p.SyncLoadout();
		}
	}

	static bool MatchesPref( NoFlyPlayer p, TeamType team ) =>
		p.Preference == RolePreference.NoPreference
		|| (team == TeamType.Passenger && p.Preference == RolePreference.Passenger)
		|| (team == TeamType.Tsa && p.Preference == RolePreference.Tsa);

	static RolePlan BuildPlan( int count )
	{
		return new RolePlan
		{
			HasSmuggler = true,
			HasDocument = count >= 3,
			HasScanner = count >= 4,
			HasSecurity = count >= 4,
			HasUndercover = count >= 5
		};
	}

	struct RolePlan
	{
		public bool HasSmuggler;
		public bool HasDocument;
		public bool HasScanner;
		public bool HasSecurity;
		public bool HasUndercover;
	}
}

public static class FlightManager
{
	public static void UpdateFlights( NoFlyGame game )
	{
		foreach ( var flight in game.Flights )
		{
			var t = game.RoundElapsed;
			if ( t >= flight.BoardingCloseAt )
				flight.Status = FlightStatus.Closed;
			else if ( t >= flight.BoardingCloseAt - game.Settings.FinalCallSeconds )
				flight.Status = FlightStatus.FinalCall;
			else if ( t >= flight.BoardingOpenAt )
				flight.Status = FlightStatus.Boarding;
			else if ( t >= flight.BoardingOpenAt - 30f )
				flight.Status = FlightStatus.BoardingSoon;
			else
				flight.Status = FlightStatus.SecurityOpen;
		}

		foreach ( var p in game.AllPlayers() )
		{
			if ( !p.NeedsSecurityCheck || p.HasBoarded || p.IsArrested || p.MissedFlight ) continue;
			var flight = game.Flights.FirstOrDefault( f => f.FlightNumber == p.FlightNumber );
			if ( flight is null ) continue;
			if ( flight.Status == FlightStatus.Closed && p.FlowState != PassengerFlowState.Boarding )
			{
				p.MissedFlight = true;
				p.FlowState = PassengerFlowState.MissedFlight;
			}
		}
	}
}

public static class ResultsManager
{
	public static RoundResults Build( NoFlyGame game )
	{
		var smuggler = game.FindPlayer( game.SmugglerId );
		var undercover = game.FindPlayer( game.UndercoverId );
		var results = new RoundResults
		{
			Winner = game.Winner,
			Headline = game.Winner == WinSide.Smuggler ? "SMUGGLER WINS" : "TSA WINS",
			SmugglerName = smuggler?.DisplayName ?? "Unknown",
			UndercoverName = undercover?.DisplayName ?? "None",
			ForgedField = smuggler?.Document?.ForgedField is DocumentFieldType ff ? UiLabels.Field( ff ) : "None",
			ContrabandHide = smuggler?.Bag?.HiddenBehindItemId is string hide
				? $"{LuggageCatalog.GetContraband( smuggler.Bag.ContrabandId ).Label} behind {LuggageCatalog.GetItem( hide ).Label}"
				: "Not hidden"
		};

		foreach ( var p in game.AllPlayers( includeBots: false ).OrderByDescending( p => p.ScoreTotal ) )
		{
			results.Lines.Add( $"{p.DisplayName} — {RoleCatalog.Get( p.Role ).DisplayName} — {p.ScoreTotal} pts" +
				(p.HasBoarded ? " (boarded)" : p.IsArrested ? " (arrested)" : p.MissedFlight ? " (missed)" : "") );
		}

		results.Mvps.Add( PickMvp( game, "Best Inspector", p => p.Role is RoleType.DocumentAgent or RoleType.ScannerAgent, p => p.Score.Inspection ) );
		results.Mvps.Add( PickMvp( game, "Fastest Passenger", p => p.Role == RoleType.RegularPassenger && p.HasBoarded, p => p.Score.Boarding + p.Score.Objectives ) );
		results.Mvps.Add( PickMvp( game, "Best Liar", p => p.Role == RoleType.Smuggler, p => p.Score.Deception ) );
		results.Mvps.Add( PickMvp( game, "Hero of the Terminal", p => p.Role is RoleType.SecurityOfficer or RoleType.UndercoverAgent, p => p.Score.Security ) );
		results.Mvps.Add( "Most Suspicious: whoever ran first" );
		results.Mvps.Add( "Worst False Alarm: the queue of doom" );
		return results;
	}

	static string PickMvp( NoFlyGame game, string title, Func<NoFlyPlayer, bool> filter, Func<NoFlyPlayer, int> score )
	{
		var best = game.AllPlayers( includeBots: false ).Where( filter ).OrderByDescending( score ).FirstOrDefault();
		return best is null ? $"{title}: —" : $"{title}: {best.DisplayName}";
	}
}
