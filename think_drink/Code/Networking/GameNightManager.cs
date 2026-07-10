namespace ThinkDrink;

/// <summary>Networked game-night shell: shared lobby settings and selected game mode.</summary>
public sealed class GameNightManager : Component
{
	public static GameNightManager Instance { get; private set; }

	[Sync( SyncFlags.FromHost )] public LobbyType LobbyType { get; set; } = LobbyType.Party;
	[Sync( SyncFlags.FromHost )] public TeamMode TeamMode { get; set; } = TeamMode.FreeForAll;
	[Sync( SyncFlags.FromHost )] public GameModeId SelectedGameMode { get; set; } = GameModeId.TriviaShowdown;
	[Sync( SyncFlags.FromHost )] public bool BotFill { get; set; } = true;

	public GameModeDefinition SelectedDefinition => GameModeRegistry.GetDefinition( SelectedGameMode );

	protected override void OnAwake() => Instance = this;

	protected override void OnStart()
	{
		if ( Networking.IsHost && !GameModeRegistry.IsSelectable( SelectedGameMode ) )
			SelectMode( GameModeId.TriviaShowdown );
	}

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
	}

	public void SelectMode( GameModeId mode )
	{
		if ( !Networking.IsHost ) return;
		if ( MatchManager.Instance?.Phase != MatchPhase.Lobby ) return;
		if ( !GameModeRegistry.IsSelectable( mode ) ) return;

		SelectedGameMode = mode;
		MatchManager.Instance.ActiveGameModeId = mode;
		GameEvents.RaiseGameModeChanged( mode );
		GameEvents.RaisePlayerListChanged();
	}
}
