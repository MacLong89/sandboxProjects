namespace NoFly;

/// <summary>
/// Host-authoritative game loop, lobby, spawning, and shared round state.
/// </summary>
public sealed class NoFlyGame : Component, Component.INetworkListener
{
	public static NoFlyGame Instance { get; private set; }
	public static NoFlyPlayer LocalPlayer => Instance?.FindLocalPlayer();

	[Property] public bool StartServer { get; set; } = true;
	[Property] public RoundSettings Settings { get; set; } = new();

	[Sync( SyncFlags.FromHost )] public RoundState State { get; set; } = RoundState.WaitingForPlayers;
	[Sync( SyncFlags.FromHost )] public float PhaseTimeLeft { get; set; }
	[Sync( SyncFlags.FromHost )] public float RoundElapsed { get; set; }
	[Sync( SyncFlags.FromHost )] public WinSide Winner { get; set; } = WinSide.None;
	[Sync( SyncFlags.FromHost )] public string StatusMessage { get; set; } = "Welcome to NO FLY";
	[Sync( SyncFlags.FromHost )] public int ConnectedPlayers { get; set; }
	[Sync( SyncFlags.FromHost )] public int ReadyPlayers { get; set; }
	[Sync( SyncFlags.FromHost )] public bool SinglePlayerMode { get; set; }
	[Sync( SyncFlags.FromHost )] public RoleType SoloForcedRole { get; set; } = RoleType.Smuggler;
	[Sync( SyncFlags.FromHost )] public string SmugglerId { get; set; }
	[Sync( SyncFlags.FromHost )] public string UndercoverId { get; set; }
	[Sync( SyncFlags.FromHost )] public bool ChaseActive { get; set; }
	[Sync( SyncFlags.FromHost )] public string ResultsSummary { get; set; }
	[Sync( SyncFlags.FromHost )] public string AlertsJson { get; set; }
	[Sync( SyncFlags.FromHost )] public string ResultsJson { get; set; }

	public List<FlightInfo> Flights { get; private set; } = new();
	public List<SecurityAlert> Alerts
	{
		get => AlertNet.FromJson( AlertsJson );
		private set => AlertsJson = AlertNet.ToJson( value );
	}
	public AirportBuilder Airport { get; private set; }
	public RoundResults LastResults
	{
		get => ResultsNet.FromJson( ResultsJson );
		private set => ResultsJson = ResultsNet.ToJson( value );
	}

	public bool BlocksPlayerMovement => State is RoundState.RoleReveal or RoundState.Results or RoundState.AssigningRoles;
	public bool IsLobby => State is RoundState.WaitingForPlayers or RoundState.LobbyCountdown;
	public bool IsPlaying => State is RoundState.Preparation or RoundState.AirportOpen or RoundState.Boarding or RoundState.Chase;
	/// <summary>Passengers may join document/scanner queues only after prep ends.</summary>
	public bool IsSecurityOpen => State is RoundState.AirportOpen or RoundState.Boarding or RoundState.Chase;

	TimeSince _phaseTimer;
	float _phaseDuration;
	bool _mapBuilt;

	protected override void OnAwake()
	{
		Instance = this;
	}

	protected override async Task OnLoad()
	{
		if ( Scene.IsEditor ) return;
		if ( StartServer && !Networking.IsActive )
		{
			LoadingScreen.Title = "Opening NO FLY Lobby";
			await Task.DelayRealtimeSeconds( 0.1f );
			Networking.CreateLobby( new() { MaxPlayers = 16 } );
		}
	}

	protected override void OnStart()
	{
		Instance = this;
		EnsureMap();
		if ( Networking.IsHost )
			EnterState( RoundState.WaitingForPlayers, 0f );
	}

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost ) return;

		ConnectedPlayers = Scene.GetAllComponents<NoFlyPlayer>().Count( p => !p.IsBot );
		ReadyPlayers = Scene.GetAllComponents<NoFlyPlayer>().Count( p => !p.IsBot && p.IsReady );

		TickLobby();
		TickPhase();
		TickRoundSystems();
	}

	void EnsureMap()
	{
		if ( _mapBuilt ) return;
		var root = Scene.Directory.FindByName( "AirportRoot" ).FirstOrDefault();
		if ( !root.IsValid() )
		{
			root = new GameObject( true, "AirportRoot" );
			Airport = root.Components.Create<AirportBuilder>();
			Airport.Build();
		}
		else
		{
			Airport = root.Components.Get<AirportBuilder>();
		}
		_mapBuilt = true;
	}

	void TickLobby()
	{
		if ( State != RoundState.WaitingForPlayers && State != RoundState.LobbyCountdown )
			return;

		var humans = Scene.GetAllComponents<NoFlyPlayer>().Where( p => !p.IsBot ).ToList();
		if ( SinglePlayerMode && humans.Count >= 1 && humans.All( h => h.IsReady ) )
		{
			if ( State != RoundState.LobbyCountdown )
				EnterState( RoundState.LobbyCountdown, 3f );
			return;
		}

		var need = Settings.MinMultiplayerPlayers;
		if ( humans.Count >= need && ReadyPlayers >= Math.Min( need, humans.Count ) )
		{
			if ( State != RoundState.LobbyCountdown )
				EnterState( RoundState.LobbyCountdown, Settings.LobbyCountdownSeconds );
		}
		else if ( State == RoundState.LobbyCountdown && !SinglePlayerMode )
		{
			EnterState( RoundState.WaitingForPlayers, 0f );
			StatusMessage = $"Waiting for players ({ReadyPlayers}/{need} ready)";
		}
	}

	void TickPhase()
	{
		if ( _phaseDuration <= 0f ) return;
		PhaseTimeLeft = MathF.Max( 0f, _phaseDuration - _phaseTimer );
		if ( PhaseTimeLeft > 0f ) return;

		switch ( State )
		{
			case RoundState.LobbyCountdown:
				BeginRound();
				break;
			case RoundState.RoleReveal:
				BeginPreparation();
				break;
			case RoundState.Preparation:
				OpenSecurity();
				break;
			case RoundState.AirportOpen:
			case RoundState.Boarding:
			case RoundState.Chase:
				EndRound( Winner == WinSide.None ? EvaluateTimeoutWinner() : Winner );
				break;
			case RoundState.Results:
				ResetRound();
				break;
		}
	}

	void TickRoundSystems()
	{
		if ( !IsPlaying && State != RoundState.Preparation ) return;

		if ( State is RoundState.AirportOpen or RoundState.Boarding or RoundState.Chase )
			RoundElapsed += Time.Delta;

		FlightManager.UpdateFlights( this );
		QueueSystem.Tick( this );
		BotDirector.Tick( this );
		NpcDirector.Tick( this );

		if ( State == RoundState.AirportOpen && RoundElapsed >= Settings.BoardingStartsAtSeconds )
		{
			EnterState( RoundState.Boarding, Settings.RoundDurationSeconds - RoundElapsed );
			StatusMessage = "BOARDING has begun";
		}

		if ( ChaseActive && State != RoundState.Chase && State is RoundState.AirportOpen or RoundState.Boarding )
		{
			EnterState( RoundState.Chase, MathF.Max( 30f, Settings.RoundDurationSeconds - RoundElapsed ) );
			StatusMessage = "ALARM — Smuggler exposed!";
		}
	}

	public void EnterState( RoundState state, float duration )
	{
		State = state;
		_phaseDuration = duration;
		_phaseTimer = 0f;
		PhaseTimeLeft = duration;
		Log.Info( $"[NO FLY] State -> {state} ({duration:0}s)" );
	}

	void BeginRound()
	{
		EnterState( RoundState.AssigningRoles, 0f );
		Winner = WinSide.None;
		ChaseActive = false;
		SmugglerId = null;
		UndercoverId = null;
		Alerts = new();
		ResultsSummary = null;
		LastResults = null;
		RoundElapsed = 0f;

		Flights = FlightCatalog.GenerateFlights( Settings );
		RoleManager.AssignRoles( this );
		EnterState( RoundState.RoleReveal, Settings.RoleRevealSeconds );
		StatusMessage = "Role reveal";
	}

	/// <summary>
	/// 30s pre-open: staff walk to desks, passengers wait in the lobby,
	/// smuggler forges + hides. Security queues stay closed.
	/// </summary>
	void BeginPreparation()
	{
		QueueSystem.ClearAll();
		foreach ( var p in AllPlayers() )
		{
			p.FlowState = PassengerFlowState.EnteringAirport;
			TeleportToRoleSpawn( p );
		}

		EnterState( RoundState.Preparation, Settings.PreparationSeconds );
		StatusMessage = $"Security opens in {Settings.PreparationSeconds:0}s — staff to stations!";
	}

	/// <summary>Release passengers into document/scanner queues.</summary>
	void OpenSecurity()
	{
		EnterState( RoundState.AirportOpen, Settings.RoundDurationSeconds - Settings.PreparationSeconds );
		StatusMessage = "Security is OPEN — passengers may queue!";
		RoundElapsed = 0f;
		NpcSpawner.SpawnInitial( Settings.TargetNpcCount );
	}

	WinSide EvaluateTimeoutWinner()
	{
		var smuggler = FindPlayer( SmugglerId );
		if ( smuggler is { HasBoarded: true } ) return WinSide.Smuggler;
		if ( smuggler is { IsArrested: true } ) return WinSide.Tsa;
		return WinSide.Tsa;
	}

	public void EndRound( WinSide winner )
	{
		if ( !Networking.IsHost ) return;
		if ( State is RoundState.Results or RoundState.Resetting ) return;

		Winner = winner;
		LastResults = ResultsManager.Build( this );
		ResultsSummary = LastResults?.Headline ?? (winner == WinSide.Smuggler ? "SMUGGLER WINS" : "TSA WINS");
		EnterState( RoundState.Results, Settings.ResultsSeconds );
		StatusMessage = winner == WinSide.Smuggler ? "SMUGGLER ESCAPED!" : "TSA WINS!";
		Log.Info( $"[NO FLY] Round over — {StatusMessage}" );
	}

	public void ResetRound()
	{
		EnterState( RoundState.Resetting, 0f );
		foreach ( var p in Scene.GetAllComponents<NoFlyPlayer>().ToList() )
		{
			if ( p.IsBot )
			{
				p.GameObject.Destroy();
				continue;
			}
			p.ResetForRound();
			p.Role = RoleType.None;
			p.Team = TeamType.None;
			p.IsReady = false;
			TeleportToLobby( p );
		}

		NpcSpawner.ClearAll();
		QueueSystem.ClearAll();
		Alerts = new();
		ChaseActive = false;
		SinglePlayerMode = false;
		AlertsJson = null;
		ResultsJson = null;
		EnterState( RoundState.WaitingForPlayers, 0f );
		StatusMessage = "Lobby — ready up!";
	}

	public void TeleportToLobby( NoFlyPlayer player )
	{
		var spot = Airport?.GetSpawn( "lobby" ) ?? WorldPosition + Vector3.Up * 10f;
		player.WorldPosition = spot;
	}

	public void TeleportToRoleSpawn( NoFlyPlayer player )
	{
		// Staff start a short walk from their desk so the prep countdown feels like
		// "get to your station". Passengers land in the waiting lobby.
		string key;
		switch ( player.Role )
		{
			case RoleType.DocumentAgent:
				key = "docs_approach";
				break;
			case RoleType.ScannerAgent:
				key = "scanner_approach";
				break;
			case RoleType.SecurityOfficer:
				key = "security_approach";
				break;
			case RoleType.Smuggler:
				key = "wait_smuggler";
				break;
			default:
				key = $"wait_{Math.Abs( player.PlayerId?.GetHashCode() ?? 0 ) % 5}";
				break;
		}
		player.WorldPosition = Airport?.GetSpawn( key ) ?? WorldPosition;
	}

	public Vector3 GetStaffDesk( RoleType role ) => role switch
	{
		RoleType.DocumentAgent => Airport?.GetSpawn( "docs" ) ?? WorldPosition,
		RoleType.ScannerAgent => Airport?.GetSpawn( "scanner" ) ?? WorldPosition,
		RoleType.SecurityOfficer => Airport?.GetSpawn( "security" ) ?? WorldPosition,
		_ => Airport?.GetSpawn( "entrance" ) ?? WorldPosition
	};

	public NoFlyPlayer FindPlayer( string id ) =>
		Scene.GetAllComponents<NoFlyPlayer>().FirstOrDefault( p => p.PlayerId == id );

	public NoFlyPlayer FindLocalPlayer() =>
		Scene.GetAllComponents<NoFlyPlayer>().FirstOrDefault( p => !p.IsProxy && !p.IsBot );

	public IEnumerable<NoFlyPlayer> AllPlayers( bool includeBots = true ) =>
		Scene.GetAllComponents<NoFlyPlayer>().Where( p => includeBots || !p.IsBot );

	public void StartChase( NoFlyPlayer smuggler, string reason )
	{
		if ( !Networking.IsHost || smuggler is null ) return;
		smuggler.Exposed = true;
		smuggler.FlowState = PassengerFlowState.Escaping;
		ChaseActive = true;
		AddAlert( new SecurityAlert
		{
			Type = AlertType.Chase,
			Message = $"CHASE: {smuggler.DisplayName} — {reason}",
			TargetPlayerId = smuggler.PlayerId,
			Position = smuggler.WorldPosition
		} );
	}

	public void AddAlert( SecurityAlert alert )
	{
		if ( !Networking.IsHost ) return;
		alert.Id = Guid.NewGuid().ToString( "N" )[..8];
		alert.CreatedAt = RoundElapsed;
		var list = Alerts;
		list.Insert( 0, alert );
		if ( list.Count > 20 ) list.RemoveAt( list.Count - 1 );
		Alerts = list;
	}

	public void ResolveAlert( string alertId )
	{
		if ( !Networking.IsHost || string.IsNullOrEmpty( alertId ) ) return;
		var list = Alerts;
		var match = list.FirstOrDefault( a => a.Id == alertId );
		if ( match is null ) return;
		match.Resolved = true;
		Alerts = list;
	}

	public void OnActive( Connection channel )
	{
		SpawnHuman( channel );
	}

	public void OnDisconnected( Connection channel )
	{
		var player = Scene.GetAllComponents<NoFlyPlayer>()
			.FirstOrDefault( p => p.Network.Owner == channel );
		if ( !player.IsValid() ) return;

		var wasCritical = player.Role is RoleType.Smuggler or RoleType.DocumentAgent or RoleType.ScannerAgent or RoleType.SecurityOfficer;
		var role = player.Role;
		player.GameObject.Destroy();

		if ( IsPlaying && wasCritical )
			BotDirector.ReplaceRole( this, role );
	}

	void SpawnHuman( Connection channel )
	{
		var spawn = Airport?.GetSpawn( "lobby" ) ?? WorldTransform.Position + Vector3.Up * 8f;
		var go = new GameObject( true, $"Player - {channel.DisplayName}" );
		go.WorldPosition = spawn;
		var player = go.Components.Create<NoFlyPlayer>();
		player.DisplayName = channel.DisplayName;
		player.PlayerId = channel.Id.ToString()[..8];
		player.OutfitColor = Kit.RandomOutfit();
		player.AppearanceHasHat = Random.Shared.NextDouble() > 0.7;
		go.Components.Create<PlayerInteractor>();
		go.NetworkSpawn( channel );

		if ( IsPlaying )
		{
			player.IsSpectator = true;
			player.ObjectiveSummary = "Spectating — join next round";
		}
	}

	[Rpc.Host]
	public void RpcRequestSinglePlayer( RoleType role )
	{
		var local = FindLocalPlayer();
		if ( local is null ) return;
		SinglePlayerMode = true;
		SoloForcedRole = role;
		local.IsReady = true;
		StatusMessage = $"Solo as {RoleCatalog.Get( role ).DisplayName}";
	}

	[Rpc.Host]
	public void RpcForceStart()
	{
		if ( !Settings.DebugToolsEnabled ) return;
		BeginRound();
	}
}

public sealed class SecurityAlert
{
	public string Id { get; set; }
	public AlertType Type { get; set; }
	public string Message { get; set; }
	public string TargetPlayerId { get; set; }
	public string SourcePlayerId { get; set; }
	public Vector3 Position { get; set; }
	public float CreatedAt { get; set; }
	public bool Resolved { get; set; }
}

public sealed class RoundResults
{
	public string Headline { get; set; }
	public WinSide Winner { get; set; }
	public string SmugglerName { get; set; }
	public string UndercoverName { get; set; }
	public string ForgedField { get; set; }
	public string ContrabandHide { get; set; }
	public List<string> Lines { get; set; } = new();
	public List<string> Mvps { get; set; } = new();
}
