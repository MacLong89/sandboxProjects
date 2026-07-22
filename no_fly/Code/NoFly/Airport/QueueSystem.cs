namespace NoFly;

public static class QueueSystem
{
	static readonly List<string> DocQueue = new();
	static readonly List<string> ScanQueue = new();

	public static void ClearAll()
	{
		DocQueue.Clear();
		ScanQueue.Clear();
	}

	public static void Tick( NoFlyGame game )
	{
		var docs = game.Scene.GetAllComponents<DocumentStation>().FirstOrDefault();
		var scan = game.Scene.GetAllComponents<ScannerStation>().FirstOrDefault();

		// Prep hold: no serving passengers until security opens.
		if ( !game.IsSecurityOpen )
		{
			if ( docs.IsValid() ) docs.QueueCount = 0;
			if ( scan.IsValid() ) scan.QueueCount = 0;
			return;
		}

		AdvanceQueue( DocQueue, game, docs, ( station, passenger ) =>
		{
			if ( station.Busy ) return false;
			return station.BeginInspection( passenger );
		}, p => p.DocumentApproved || p.IsArrested || p.IsDetained );

		AdvanceQueue( ScanQueue, game, scan, ( station, passenger ) =>
		{
			if ( station.Busy || station.Searching ) return false;
			if ( !passenger.DocumentApproved ) return false;
			return station.BeginScan( passenger );
		}, p => p.BagCleared || p.IsArrested || p.IsDetained );

		// Crowd NPCs can also feed the bag scanner so the desk isn't idle.
		TryServeNpcScan( game, scan );

		if ( docs.IsValid() ) docs.QueueCount = DocQueue.Count;
		if ( scan.IsValid() ) scan.QueueCount = ScanQueue.Count;

		PositionQueued( DocQueue, game, "doc_queue_" );
		PositionQueued( ScanQueue, game, "scan_queue_" );
	}

	static void AdvanceQueue<T>( List<string> queue, NoFlyGame game, T station, Func<T, NoFlyPlayer, bool> tryServe, Func<NoFlyPlayer, bool> done )
		where T : Component
	{
		if ( !station.IsValid() ) return;
		queue.RemoveAll( id =>
		{
			var p = game.FindPlayer( id );
			return p is null || done( p );
		} );

		if ( queue.Count == 0 ) return;
		var front = game.FindPlayer( queue[0] );
		if ( front is null )
		{
			queue.RemoveAt( 0 );
			return;
		}

		if ( tryServe( station, front ) )
			queue.RemoveAt( 0 );
	}

	static void TryServeNpcScan( NoFlyGame game, ScannerStation station )
	{
		if ( !station.IsValid() || station.Busy || station.Searching ) return;
		var belt = game.Airport?.GetSpawn( "scan_queue_0" ) ?? default;
		foreach ( var npc in game.Scene.GetAllComponents<NpcPassenger>() )
		{
			if ( npc.FlowState is not (PassengerFlowState.GoingToScanner or PassengerFlowState.ScannerQueue) )
				continue;
			if ( Vector3.DistanceBetween( npc.WorldPosition.WithZ( 0 ), belt.WithZ( 0 ) ) > 80f )
				continue;
			if ( station.BeginScanNpc( npc ) )
				return;
		}
	}

	static void PositionQueued( List<string> queue, NoFlyGame game, string spawnPrefix )
	{
		for ( var i = 0; i < queue.Count; i++ )
		{
			var p = game.FindPlayer( queue[i] );
			if ( p is null || (!p.IsBot && !p.IsProxy && p.Network.IsOwner) ) continue;
			// Soft-guide bots/NPCs toward queue slots; humans walk themselves
			if ( !p.IsBot ) continue;
			var spot = game.Airport?.GetSpawn( $"{spawnPrefix}{Math.Min( i, 5 )}" ) ?? p.WorldPosition;
			MoveToward( p, spot, 120f );
		}
	}

	public static bool JoinDocuments( NoFlyPlayer player )
	{
		if ( player is null || DocQueue.Contains( player.PlayerId ) ) return false;
		if ( player.DocumentApproved || player.IsDetained || player.IsArrested ) return false;
		if ( NoFlyGame.Instance is not { IsSecurityOpen: true } ) return false;
		DocQueue.Add( player.PlayerId );
		player.FlowState = PassengerFlowState.DocumentQueue;
		return true;
	}

	public static bool JoinScanner( NoFlyPlayer player )
	{
		if ( player is null || ScanQueue.Contains( player.PlayerId ) ) return false;
		if ( !player.DocumentApproved || player.BagCleared ) return false;
		if ( player.IsDetained || player.IsArrested ) return false;
		if ( NoFlyGame.Instance is not { IsSecurityOpen: true } ) return false;
		ScanQueue.Add( player.PlayerId );
		player.FlowState = PassengerFlowState.ScannerQueue;
		return true;
	}

	public static void Leave( NoFlyPlayer player )
	{
		if ( player is null ) return;
		DocQueue.Remove( player.PlayerId );
		ScanQueue.Remove( player.PlayerId );
	}

	public static void MoveToward( NoFlyPlayer player, Vector3 target, float speed )
	{
		var delta = (target - player.WorldPosition).WithZ( 0 );
		if ( delta.Length < 8f )
		{
			player.AnimVelocity = Vector3.Zero;
			return;
		}

		var dir = delta.Normal;
		player.WorldPosition += dir * speed * Time.Delta;
		player.WorldRotation = Rotation.LookAt( dir );
		player.EyeAngles = new Angles( 0f, player.WorldRotation.Angles().yaw, 0f );
		player.AnimVelocity = dir * speed;
		player.AnimGrounded = true;
	}
}

public sealed class PlayerInteractor : Component
{
	public InteractableMarker Hovered { get; private set; }
	public NoFlyPlayer LookTarget { get; private set; }

	NoFlyPlayer Player => Components.Get<NoFlyPlayer>();
	TimeSince _nearPromptClear;

	protected override void OnUpdate()
	{
		if ( IsProxy || Player is null || Player.IsBot ) return;

		FindTargets();
		TryProximityStations();
		TryStaffStationPrompt();

		// Use / E — IPressable alone is unreliable without a stock PlayerController.
		if ( Hovered.IsValid() && (Input.Pressed( "use" ) || Input.Pressed( "attack1" ) && Player.Role is not RoleType.SecurityOfficer) )
		{
			if ( Hovered.Kind == InteractionKind.UseShop )
			{
				if ( ShopPanel.IsOpen )
					ShopPanel.Close();
				else
					ShopPanel.Open( Hovered.ZoneTag );
				return;
			}
			HandleInteraction( Hovered );
		}

		if ( Input.Pressed( "attack2" ) && LookTarget.IsValid() )
			TryReport( LookTarget );

		if ( Player.Role == RoleType.SecurityOfficer && Input.Pressed( "attack1" ) && LookTarget.IsValid() )
			ArrestSystem.TryArrest( Player, LookTarget );

		if ( Player.Role == RoleType.UndercoverAgent && Input.Pressed( "reload" ) && LookTarget.IsValid() )
			ArrestSystem.UndercoverMark( Player, LookTarget );

		if ( Player.Role == RoleType.UndercoverAgent && Input.Pressed( "slot1" ) && LookTarget.IsValid() )
			ArrestSystem.UndercoverArrest( Player, LookTarget );
	}

	void FindTargets()
	{
		Hovered = null;
		LookTarget = null;
		var eyes = Player.EyePosition;
		var forward = Rotation.From( Player.EyeAngles ).Forward;

		// Prefer what you're looking at, then fall back to nearest marker in range.
		var tr = Scene.Trace.Ray( eyes, eyes + forward * 160f ).WithoutTags( "player" ).Run();
		if ( tr.Hit && tr.GameObject.IsValid() )
			Hovered = tr.GameObject.Components.GetInParentOrSelf<InteractableMarker>();

		if ( !Hovered.IsValid() )
			Hovered = FindNearestMarker( 110f );

		if ( Hovered.IsValid() )
		{
			Player.ActivePrompt = PromptFor( Hovered );
			_nearPromptClear = 0;
		}
		else if ( _nearPromptClear > 0.4f && !string.IsNullOrEmpty( Player.ActivePrompt )
		          && !Player.ActivePrompt.StartsWith( "In document" )
		          && !Player.ActivePrompt.StartsWith( "Bag in" )
		          && !Player.ActivePrompt.StartsWith( "You boarded" )
		          && !Player.ActivePrompt.StartsWith( "Stationed" )
		          && !Player.ActivePrompt.StartsWith( "Already stationed" ) )
		{
			// Clear stale look-prompts; keep queue/status messages.
			if ( Player.ActivePrompt.Contains( "Press E" ) || Player.ActivePrompt.Contains( "Security opens" )
			     || Player.ActivePrompt.Contains( "Boarding hasn't" ) || Player.ActivePrompt.Contains( "Join" )
			     || Player.ActivePrompt.Contains( "Get closer" ) || Player.ActivePrompt.Contains( "Wrong desk" ) )
				Player.ActivePrompt = null;
		}

		var tr2 = Scene.Trace.Ray( eyes, eyes + forward * 180f ).Run();
		if ( tr2.Hit && tr2.GameObject.IsValid() )
		{
			LookTarget = tr2.GameObject.Components.GetInParentOrSelf<NoFlyPlayer>();
		}
	}

	InteractableMarker FindNearestMarker( float radius )
	{
		InteractableMarker best = null;
		var bestDist = radius;
		foreach ( var marker in Scene.GetAllComponents<InteractableMarker>() )
		{
			if ( !marker.IsValid() ) continue;
			var d = Vector3.DistanceBetween( Player.WorldPosition, marker.WorldPosition );
			if ( d < bestDist )
			{
				bestDist = d;
				best = marker;
			}
		}
		return best;
	}

	string PromptFor( InteractableMarker marker )
	{
		var game = NoFlyGame.Instance;
		if ( game is { IsSecurityOpen: false } && marker.Kind is InteractionKind.PresentDocument or InteractionKind.PlaceBag )
			return $"Security opens in {game.PhaseTimeLeft:0}s — wait in the lobby";

		return marker.Kind switch
		{
			InteractionKind.PresentDocument => Player.DocumentApproved
				? "Documents already cleared"
				: "Press E — join document queue",
			InteractionKind.PlaceBag => !Player.DocumentApproved
				? "Clear document check first"
				: Player.BagCleared
					? "Bag already cleared"
					: "Press E — join bag scan queue",
			InteractionKind.BoardFlight =>
				string.IsNullOrEmpty( Player.AssignedGate )
					? "Press E — board your flight"
					: Player.CanBoard || (Player.Role == RoleType.Smuggler && Player.Exposed)
						? $"Press E — board at Gate {Player.AssignedGate}"
						: $"Gate {Player.AssignedGate} — clear security first",
			InteractionKind.UseShop => "Press E — open shop",
			InteractionKind.Sit => "Press E — sit",
			InteractionKind.ManStation => PromptManStation( marker.ZoneTag ),
			_ => $"Press E — {marker.Prompt ?? "interact"}"
		};
	}

	string PromptManStation( string zoneTag )
	{
		var role = StaffStation.RoleForZone( zoneTag );
		if ( role is null ) return "Staff station";
		if ( Player.Role != role )
			return $"Staff only — {StaffStation.StationName( role.Value )}";
		if ( Player.AtStation )
			return $"On station ✓ — {StaffStation.StationName( Player.Role )}";
		return $"Press E — man {StaffStation.StationName( Player.Role )}";
	}

	/// <summary>
	/// Walking into the document / bag-scan approach automatically joins the queue
	/// so players don't need a perfect aim on a tiny marker.
	/// </summary>
	void TryProximityStations()
	{
		var game = NoFlyGame.Instance;
		if ( game is not { IsSecurityOpen: true } ) return;
		if ( !Player.NeedsSecurityCheck ) return;

		if ( !Player.DocumentApproved )
		{
			var doc = FindMarker( InteractionKind.PresentDocument );
			if ( doc.IsValid() && Vector3.DistanceBetween( Player.WorldPosition, doc.WorldPosition ) < 90f )
			{
				if ( QueueSystem.JoinDocuments( Player ) )
					Player.ActivePrompt = "In document queue — wait for the agent";
			}
			return;
		}

		if ( !Player.BagCleared )
		{
			var scan = FindMarker( InteractionKind.PlaceBag );
			if ( scan.IsValid() && Vector3.DistanceBetween( Player.WorldPosition, scan.WorldPosition ) < 90f )
			{
				if ( QueueSystem.JoinScanner( Player ) )
					Player.ActivePrompt = "Bag in scanner queue — wait for the agent";
			}
		}
	}

	/// <summary>Remind staff to press E when standing on their desk pad.</summary>
	void TryStaffStationPrompt()
	{
		if ( !StaffStation.IsStaff( Player.Role ) ) return;
		if ( Player.AtStation ) return;
		if ( !StaffStation.NearOwnDesk( Player ) ) return;

		// Don't stomp an active look-prompt for something else.
		if ( Hovered.IsValid() && Hovered.Kind != InteractionKind.ManStation ) return;

		Player.ActivePrompt = $"Press E — man {StaffStation.StationName( Player.Role )} (confirm you're on station)";
	}

	InteractableMarker FindMarker( InteractionKind kind ) =>
		Scene.GetAllComponents<InteractableMarker>().FirstOrDefault( m => m.Kind == kind );

	public void HandleInteraction( InteractableMarker marker )
	{
		if ( marker is null || !marker.IsValid() ) return;
		if ( !Networking.IsHost )
		{
			RpcInteract( marker.Kind, marker.ZoneTag ?? "" );
			return;
		}
		Apply( marker.Kind, marker.ZoneTag );
	}

	[Rpc.Host]
	public void RpcInteract( InteractionKind kind, string zoneTag )
	{
		Apply( kind, zoneTag );
	}

	void Apply( InteractionKind kind, string zoneTag )
	{
		var p = Player;
		var game = NoFlyGame.Instance;
		if ( p is null || game is null ) return;

		switch ( kind )
		{
			case InteractionKind.PresentDocument:
				if ( !game.IsSecurityOpen )
				{
					p.ActivePrompt = $"Security opens in {game.PhaseTimeLeft:0}s — wait in the lobby";
					break;
				}
				if ( p.DocumentApproved )
				{
					p.ActivePrompt = "Documents already cleared — head to bag scan";
					break;
				}
				p.ActivePrompt = QueueSystem.JoinDocuments( p ) ? "In document queue — wait for the agent" : "Already in document queue";
				break;
			case InteractionKind.PlaceBag:
				if ( !game.IsSecurityOpen )
				{
					p.ActivePrompt = $"Security opens in {game.PhaseTimeLeft:0}s — wait in the lobby";
					break;
				}
				if ( !p.DocumentApproved )
				{
					p.ActivePrompt = "Clear document check first";
					break;
				}
				if ( p.BagCleared )
				{
					p.ActivePrompt = "Bag already cleared — head to your gate";
					break;
				}
				p.ActivePrompt = QueueSystem.JoinScanner( p ) ? "Bag in scanner queue — wait for the agent" : "Already in scanner queue";
				break;
			case InteractionKind.UseShop:
				// Shop UI is opened on the local client; purchases complete objectives.
				p.ActivePrompt = "Browsing the shop…";
				break;
			case InteractionKind.Sit:
				ObjectiveSystem.TryCompleteZone( p, zoneTag );
				p.ActivePrompt = "You sat down. Ahh.";
				break;
			case InteractionKind.BoardFlight:
				TryBoard( p, game, zoneTag );
				break;
			case InteractionKind.LeaveQueue:
				QueueSystem.Leave( p );
				p.ActivePrompt = "Left the queue";
				break;
			case InteractionKind.JoinQueue:
				// Walk-through arch after scanner — no-op helper.
				p.ActivePrompt = p.BagCleared ? "You're cleared — head to the gates" : "Place your bag on the scanner first";
				break;
			case InteractionKind.ManStation:
				StaffStation.TryCheckIn( p, zoneTag );
				break;
		}
	}

	void TryBoard( NoFlyPlayer p, NoFlyGame game, string gateTag = null )
	{
		if ( p is null || game is null ) return;
		if ( p.IsArrested || p.HasBoarded ) return;
		if ( game.State is RoundState.Results or RoundState.Resetting or RoundState.WaitingForPlayers )
			return;

		if ( !game.IsSecurityOpen )
		{
			p.ActivePrompt = "Security hasn't opened yet — wait for the countdown";
			return;
		}

		// Must use the correct gate finger — wrong door = more chase time for security.
		if ( !string.IsNullOrEmpty( gateTag ) && !string.IsNullOrEmpty( p.AssignedGate )
		     && !string.Equals( gateTag, p.AssignedGate, StringComparison.OrdinalIgnoreCase ) )
		{
			p.ActivePrompt = $"Wrong gate — your flight boards at Gate {p.AssignedGate}";
			return;
		}

		var flight = game.Flights.FirstOrDefault( f => f.FlightNumber == p.FlightNumber )
			?? game.Flights.FirstOrDefault( f => f.Gate == p.AssignedGate )
			?? game.Flights.FirstOrDefault();
		if ( flight is null )
		{
			p.ActivePrompt = "No flight assigned";
			return;
		}

		if ( flight.Status is FlightStatus.Closed or FlightStatus.Departed )
		{
			p.MissedFlight = true;
			p.ActivePrompt = "Flight closed — you missed it!";
			return;
		}

		// Early boarding: once cleared (or exposed smuggler sprinting), you can board
		// without waiting for the official boarding call.
		if ( p.Role == RoleType.Smuggler )
		{
			if ( !p.CanBoard && !p.Exposed )
			{
				p.ActivePrompt = "Clear security first — or get exposed and sprint your gate";
				return;
			}
		}
		else if ( !p.CanBoard )
		{
			p.ActivePrompt = "Pass document check and bag scan before boarding";
			return;
		}

		p.HasBoarded = true;
		p.FlowState = PassengerFlowState.Boarded;
		p.AddScore( 150, "boarding" );
		p.ActivePrompt = $"Boarded Gate {p.AssignedGate ?? gateTag ?? "?"}!";

		// Smuggler escape ends the round immediately.
		if ( p.Role == RoleType.Smuggler )
		{
			game.EndRound( WinSide.Smuggler );
			return;
		}

		// Solo: boarding concludes the match so you always get a win screen.
		if ( game.SinglePlayerMode && !p.IsBot )
		{
			var smug = game.FindPlayer( game.SmugglerId );
			var winner = smug is { HasBoarded: true } ? WinSide.Smuggler
				: smug is { IsArrested: true } ? WinSide.Tsa
				: WinSide.Tsa;
			game.EndRound( winner );
		}
	}

	void TryReport( NoFlyPlayer target )
	{
		RpcReport( target.PlayerId, ReportReason.StrangeBehavior );
	}

	[Rpc.Host]
	public void RpcReport( string targetId, ReportReason reason )
	{
		var reporter = Player;
		var target = NoFlyGame.Instance?.FindPlayer( targetId );
		if ( reporter is null || target is null ) return;
		if ( reporter.TimeSinceReport < (NoFlyGame.Instance?.Settings.ReportCooldownSeconds ?? 20f) ) return;
		reporter.TimeSinceReport = 0;

		NoFlyGame.Instance.AddAlert( new SecurityAlert
		{
			Type = AlertType.PassengerReport,
			Message = $"{reporter.DisplayName} reports {target.DisplayName}: {reason}",
			TargetPlayerId = target.PlayerId,
			SourcePlayerId = reporter.PlayerId,
			Position = target.WorldPosition
		} );

		if ( target.Role == RoleType.Smuggler )
			reporter.AddScore( 40, "reports" );
		else
			reporter.AddScore( -10, "reports" );

		var reportObj = reporter.Objectives.FirstOrDefault( o => o.Id == "report_ok" && !o.Completed );
		if ( reportObj is not null && target.Role == RoleType.Smuggler )
			ObjectiveSystem.Complete( reporter, reportObj );
	}
}
