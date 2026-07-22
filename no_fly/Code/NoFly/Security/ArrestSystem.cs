namespace NoFly;

public static class ArrestSystem
{
	public static bool HasAuthorityToArrest( NoFlyPlayer officer, NoFlyPlayer suspect )
	{
		if ( officer is null || suspect is null ) return false;
		if ( suspect.IsArrested ) return false;

		if ( suspect.IsFlagged ) return true;
		if ( suspect.Exposed ) return true;
		if ( NoFlyGame.Instance?.Alerts.Any( a => a.TargetPlayerId == suspect.PlayerId && !a.Resolved ) == true ) return true;
		if ( officer.Role == RoleType.UndercoverAgent && officer.MarkedSuspectId == suspect.PlayerId ) return true;
		return false;
	}

	public static void TryDetain( NoFlyPlayer officer, NoFlyPlayer suspect )
	{
		if ( !Networking.IsHost ) return;
		if ( officer.Role != RoleType.SecurityOfficer ) return;
		suspect.IsDetained = true;
		suspect.FlowState = PassengerFlowState.Detained;
		officer.AddScore( 20, "security" );
	}

	public static void TryArrest( NoFlyPlayer officer, NoFlyPlayer suspect )
	{
		if ( !Networking.IsHost ) return;
		if ( officer.Role != RoleType.SecurityOfficer ) return;
		if ( Vector3.DistanceBetween( officer.WorldPosition, suspect.WorldPosition ) > 90f ) return;

		var justified = HasAuthorityToArrest( officer, suspect );
		if ( !justified )
		{
			officer.AddScore( -120, "security" );
			suspect.IsDetained = false;
			suspect.ActivePrompt = "Wrongfully arrested — released!";
			NoFlyGame.Instance?.AddAlert( new SecurityAlert
			{
				Type = AlertType.PassengerReport,
				Message = $"Unjustified arrest of {suspect.DisplayName}",
				TargetPlayerId = suspect.PlayerId,
				SourcePlayerId = officer.PlayerId,
				Position = suspect.WorldPosition
			} );
			return;
		}

		CompleteArrest( officer, suspect );
	}

	public static void UndercoverMark( NoFlyPlayer agent, NoFlyPlayer suspect )
	{
		if ( !Networking.IsHost ) return;
		if ( agent.Role != RoleType.UndercoverAgent || agent.UndercoverExposed ) return;
		agent.MarkedSuspectId = suspect.PlayerId;
		agent.AddScore( suspect.Role == RoleType.Smuggler ? 60 : -15, "security" );
		NoFlyGame.Instance?.AddAlert( new SecurityAlert
		{
			Type = AlertType.UndercoverMark,
			Message = $"Undercover marked {suspect.DisplayName}",
			TargetPlayerId = suspect.PlayerId,
			SourcePlayerId = agent.PlayerId,
			Position = suspect.WorldPosition
		} );
	}

	public static void UndercoverArrest( NoFlyPlayer agent, NoFlyPlayer suspect )
	{
		if ( !Networking.IsHost ) return;
		if ( agent.Role != RoleType.UndercoverAgent || !agent.ArrestAvailable || agent.UndercoverExposed ) return;
		if ( Vector3.DistanceBetween( agent.WorldPosition, suspect.WorldPosition ) > 90f ) return;

		agent.ArrestAvailable = false;
		if ( suspect.Role != RoleType.Smuggler )
		{
			agent.UndercoverExposed = true;
			agent.AddScore( -100, "security" );
			suspect.ActivePrompt = "Undercover blew their cover!";
			NoFlyGame.Instance?.FindPlayer( NoFlyGame.Instance.SmugglerId )?.SetPrompt( "Undercover exposed — stay sharp!" );
			return;
		}

		agent.AddScore( 150, "security" );
		CompleteArrest( agent, suspect );
	}

	static void CompleteArrest( NoFlyPlayer officer, NoFlyPlayer suspect )
	{
		suspect.IsArrested = true;
		suspect.IsDetained = true;
		suspect.FlowState = PassengerFlowState.Arrested;
		suspect.WorldPosition = NoFlyGame.Instance?.Airport?.GetSpawn( "holding" ) ?? suspect.WorldPosition;
		officer.AddScore( 200, "security" );

		if ( suspect.Role == RoleType.Smuggler )
			NoFlyGame.Instance?.EndRound( WinSide.Tsa );
	}
}

static class PromptExt
{
	public static void SetPrompt( this NoFlyPlayer p, string msg )
	{
		if ( p is not null ) p.ActivePrompt = msg;
	}
}

public sealed class BotController : Component
{
	NoFlyPlayer Player => Components.Get<NoFlyPlayer>();
	TimeSince _think;

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost ) return;
		var game = NoFlyGame.Instance;
		var p = Player;
		if ( game is null || p is null || !p.IsBot ) return;
		if ( game.State is RoundState.WaitingForPlayers or RoundState.LobbyCountdown or RoundState.Results ) return;
		if ( _think < 0.25f ) return;
		_think = 0;

		switch ( p.Role )
		{
			case RoleType.Smuggler: TickSmuggler( game, p ); break;
			case RoleType.RegularPassenger:
			case RoleType.UndercoverAgent: TickPassenger( game, p ); break;
			case RoleType.DocumentAgent: TickDocumentAgent( game, p ); break;
			case RoleType.ScannerAgent: TickScannerAgent( game, p ); break;
			case RoleType.SecurityOfficer: TickSecurity( game, p ); break;
		}
	}

	void TickSmuggler( NoFlyGame game, NoFlyPlayer p )
	{
		if ( game.State == RoundState.Preparation )
		{
			if ( !p.ForgeryComplete && p.Document is not null )
			{
				ForgerySystem.ApplyForgery( p, DocumentFieldType.CountrySymbol, "FISH", DiscrepancyDifficulty.Easy );
			}
			if ( !p.HideComplete && p.Bag is not null )
			{
				var layout = LuggageCatalog.GetLayout( p.Bag.LayoutId );
				LuggageHideSystem.HideBehind( p, layout.Slots[0].ItemId );
			}
			// Stay in waiting area until security opens.
			WanderWaitingArea( game, p );
			return;
		}

		if ( p.IsArrested || p.IsDetained ) return;
		if ( p.Exposed || game.ChaseActive )
		{
			QueueSystem.MoveToward( p, game.Airport.GetBoardSpawn( p.AssignedGate ), 280f );
			TryBoard( game, p );
			return;
		}

		TickPassenger( game, p );
	}

	void TickPassenger( NoFlyGame game, NoFlyPlayer p )
	{
		if ( p.IsArrested || p.HasBoarded ) return;

		if ( p.IsDetained )
		{
			QueueSystem.Leave( p );
			QueueSystem.MoveToward( p, game.Airport.GetSpawn( "holding" ), 140f );
			return;
		}

		// Pre-open: passengers just mill around the waiting area.
		if ( !game.IsSecurityOpen )
		{
			WanderWaitingArea( game, p );
			return;
		}

		if ( !p.DocumentApproved )
		{
			QueueSystem.JoinDocuments( p );
			QueueSystem.MoveToward( p, game.Airport.GetSpawn( "doc_queue_0" ), 140f );
			return;
		}

		if ( !p.BagCleared )
		{
			QueueSystem.JoinScanner( p );
			QueueSystem.MoveToward( p, game.Airport.GetSpawn( "scan_queue_0" ), 140f );
			return;
		}

		var obj = p.Objectives.FirstOrDefault( o => !o.Completed && o.ZoneTag is not null and not "any" );
		if ( obj is not null && game.RoundElapsed < game.Settings.BoardingStartsAtSeconds - 20f )
		{
			QueueSystem.MoveToward( p, game.Airport.GetZone( obj.ZoneTag ), 140f );
			ObjectiveSystem.TryCompleteZone( p, obj.ZoneTag );
			return;
		}

		QueueSystem.MoveToward( p, game.Airport.GetGateApproach( p.AssignedGate ), 150f );
		if ( game.State is RoundState.Boarding or RoundState.Chase )
			TryBoard( game, p );
	}

	void WanderWaitingArea( NoFlyGame game, NoFlyPlayer p )
	{
		var slot = Math.Abs( p.PlayerId.GetHashCode() ) % 5;
		var spot = game.Airport.GetSpawn( $"wait_{slot}" );
		// Gentle drift so the lobby feels alive without leaving the hold zone.
		var jitter = new Vector3(
			(float)(Math.Sin( Time.Now * 0.4f + slot ) * 28f),
			(float)(Math.Cos( Time.Now * 0.35f + slot * 1.7f ) * 28f),
			0f );
		QueueSystem.MoveToward( p, spot + jitter, 70f );
	}

	void TickDocumentAgent( NoFlyGame game, NoFlyPlayer p )
	{
		// Always walk to the documents desk during prep and while open.
		QueueSystem.MoveToward( p, game.GetStaffDesk( RoleType.DocumentAgent ), 140f );
		if ( !p.AtStation && StaffStation.NearOwnDesk( p ) )
			StaffStation.TryCheckIn( p, StaffStation.ZoneForRole( RoleType.DocumentAgent ) );
		if ( !game.IsSecurityOpen ) return;

		var station = game.Scene.GetAllComponents<DocumentStation>().FirstOrDefault();
		if ( station is null || !station.Busy ) return;
		var passenger = game.FindPlayer( station.CurrentPassengerId );
		if ( passenger?.Document is null ) return;

		if ( passenger.Document.IsForged && Random.Shared.NextDouble() > 0.35 )
			station.Reject( p, passenger.Document.ForgedField );
		else if ( !passenger.Document.IsForged && Random.Shared.NextDouble() > 0.1 )
			station.Approve( p );
		else if ( passenger.Document.IsForged )
			station.Approve( p );
		else
			station.Reject( p, DocumentFieldType.Photo );
	}

	void TickScannerAgent( NoFlyGame game, NoFlyPlayer p )
	{
		QueueSystem.MoveToward( p, game.GetStaffDesk( RoleType.ScannerAgent ), 140f );
		if ( !p.AtStation && StaffStation.NearOwnDesk( p ) )
			StaffStation.TryCheckIn( p, StaffStation.ZoneForRole( RoleType.ScannerAgent ) );
		if ( !game.IsSecurityOpen ) return;

		var station = game.Scene.GetAllComponents<ScannerStation>().FirstOrDefault();
		if ( station is null || !station.Busy || station.Searching ) return;
		var bag = station.ActiveBag;
		if ( bag is null ) return;

		// Act on the belt snapshot so NPC jobs work the same as player bags.
		if ( bag.HasContraband && Random.Shared.NextDouble() > 0.4 )
		{
			station.SelectedItemId = bag.HiddenBehindItemId ?? station.AnomalyItemId;
			station.SearchBag( p );
		}
		else if ( !bag.HasContraband && Random.Shared.NextDouble() > 0.15 )
			station.ClearBag( p );
		else if ( bag.HasContraband )
			station.ClearBag( p );
		else
			station.SearchBag( p );
	}

	void TickSecurity( NoFlyGame game, NoFlyPlayer p )
	{
		if ( game.State == RoundState.Preparation )
		{
			QueueSystem.MoveToward( p, game.GetStaffDesk( RoleType.SecurityOfficer ), 140f );
			if ( !p.AtStation && StaffStation.NearOwnDesk( p ) )
				StaffStation.TryCheckIn( p, StaffStation.ZoneForRole( RoleType.SecurityOfficer ) );
			return;
		}

		if ( !p.AtStation && StaffStation.NearOwnDesk( p ) )
			StaffStation.TryCheckIn( p, StaffStation.ZoneForRole( RoleType.SecurityOfficer ) );

		var alert = game.Alerts.FirstOrDefault( a => !a.Resolved );
		if ( alert is not null )
		{
			var target = game.FindPlayer( alert.TargetPlayerId );
			if ( target is not null )
			{
				QueueSystem.MoveToward( p, target.WorldPosition, game.ChaseActive ? 300f : 180f );
				if ( Vector3.DistanceBetween( p.WorldPosition, target.WorldPosition ) < 70f )
				{
					ArrestSystem.TryArrest( p, target );
					game.ResolveAlert( alert.Id );
				}
				return;
			}
		}

		QueueSystem.MoveToward( p, game.GetStaffDesk( RoleType.SecurityOfficer ), 120f );
	}

	void TryBoard( NoFlyGame game, NoFlyPlayer p )
	{
		if ( p.IsArrested || p.HasBoarded ) return;
		if ( !game.IsSecurityOpen ) return;

		var boardSpot = game.Airport.GetBoardSpawn( p.AssignedGate );
		if ( Vector3.DistanceBetween( p.WorldPosition, boardSpot ) > 110f ) return;

		var flight = game.Flights.FirstOrDefault( f => f.FlightNumber == p.FlightNumber )
			?? game.Flights.FirstOrDefault();
		if ( flight is null ) return;
		if ( flight.Status is FlightStatus.Closed or FlightStatus.Departed ) return;

		if ( p.Role == RoleType.Smuggler )
		{
			if ( !p.CanBoard && !p.Exposed ) return;
		}
		else if ( !p.CanBoard )
		{
			return;
		}

		p.HasBoarded = true;
		p.FlowState = PassengerFlowState.Boarded;
		p.AddScore( 150, "boarding" );
		if ( p.Role == RoleType.Smuggler )
			game.EndRound( WinSide.Smuggler );
	}
}

public static class BotDirector
{
	public static void Tick( NoFlyGame game )
	{
		// BotController components self-tick.
	}

	public static void ReplaceRole( NoFlyGame game, RoleType role )
	{
		if ( role == RoleType.None ) return;
		RoleManager.SpawnBot( game, role, $"Backup {RoleCatalog.Get( role ).DisplayName}" );
		if ( role == RoleType.Smuggler )
		{
			var bot = game.AllPlayers().FirstOrDefault( p => p.Role == RoleType.Smuggler );
			if ( bot is not null ) game.SmugglerId = bot.PlayerId;
		}
	}
}
