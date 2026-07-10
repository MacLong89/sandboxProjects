namespace ThinkDrink;

/// <summary>
/// Spawns and drives the lobby filler / practice bot. Host-only logic.
/// Auto-joins when a solo player is waiting; leaves when a second human arrives.
/// </summary>
public sealed class BotManager : Component
{
	public static BotManager Instance { get; private set; }

	[Sync( SyncFlags.FromHost )] public bool BotActive { get; set; }
	[Sync( SyncFlags.FromHost )] public bool WaitingForPlayers { get; set; }

	public ThinkDrinkPlayer Bot { get; private set; }

	private TimeUntil _autoJoinDelay;
	private bool _autoJoinPending;
	private TimeSince _phaseEntered;
	private bool _buzzScheduled;
	private bool _answerScheduled;
	private bool _stealScheduled;
	private float _botActionDelay;
	private TriviaQuestion _botQuestion;

	protected override void OnAwake()
	{
		Instance = this;
		GameEvents.PhaseChanged += OnPhaseChanged;
		GameEvents.QuestionShown += OnQuestionShown;
	}

	protected override void OnDestroy()
	{
		GameEvents.PhaseChanged -= OnPhaseChanged;
		GameEvents.QuestionShown -= OnQuestionShown;
		if ( Instance == this ) Instance = null;
	}

	public int GetHumanCount()
	{
		var n = 0;
		for ( var i = 0; i < ThinkDrinkPlayer.All.Count; i++ )
		{
			var p = ThinkDrinkPlayer.All[i];
			if ( p.IsBot ) continue;
			if ( p.IsConnected ) n++;
		}
		return n;
	}

	public void OnHumanJoined()
	{
		if ( !Networking.IsHost ) return;

		_autoJoinPending = false;
		WaitingForPlayers = GetHumanCount() == 1;

		if ( GetHumanCount() >= 2 && MatchManager.Instance?.Phase == MatchPhase.Lobby )
			RemoveBot( "A player joined — bot stepped out." );
	}

	public void OnHumanLeft()
	{
		if ( !Networking.IsHost ) return;
		WaitingForPlayers = GetHumanCount() == 1;
		if ( GetHumanCount() == 1 && MatchManager.Instance?.Phase == MatchPhase.Lobby )
			ScheduleAutoJoin();
	}

	public void OnReadyChanged()
	{
		if ( !Networking.IsHost ) return;
		if ( MatchManager.Instance?.Phase != MatchPhase.Lobby ) return;

		if ( GetHumanCount() == 1 && ThinkDrinkPlayer.AnyHumanReady() )
			EnsureBot( autoReady: true );
	}

	public void OnReturnedToLobby()
	{
		if ( !Networking.IsHost ) return;
		WaitingForPlayers = GetHumanCount() == 1;

		if ( GetHumanCount() >= 2 )
			RemoveBot();
		else if ( GetHumanCount() == 1 )
			ScheduleAutoJoin();
	}

	public void ForcePracticeBot()
	{
		if ( !Networking.IsHost ) return;
		if ( MatchManager.Instance?.Phase != MatchPhase.Lobby ) return;
		if ( GetHumanCount() != 1 ) return;

		EnsureBot( autoReady: true );
		WaitingForPlayers = true;
		GameEvents.RaisePlayerListChanged();
	}

	private void ScheduleAutoJoin()
	{
		if ( BotActive || GetHumanCount() != 1 ) return;
		if ( MatchManager.Instance?.Phase != MatchPhase.Lobby ) return;

		_autoJoinPending = true;
		_autoJoinDelay = GameConstants.BotAutoJoinDelaySeconds;
	}

	public void EnsureBot( bool autoReady )
	{
		if ( !Networking.IsHost ) return;
		if ( BotActive && Bot.IsValid() ) return;
		if ( GetHumanCount() != 1 ) return;

		var go = new GameObject( true, $"Player - {GameConstants.BotDisplayName}" );
		go.Tags.Add( "player" );
		go.Tags.Add( "bot" );

		var player = go.AddComponent<ThinkDrinkPlayer>();
		player.SetupAsBot( autoReady );

		go.NetworkMode = NetworkMode.Object;
		go.NetworkSpawn();
		go.Network.SetOrphanedMode( NetworkOrphaned.Host );

		Bot = player;
		BotActive = true;
		WaitingForPlayers = true;
		GameEvents.RaisePlayerListChanged();
		Log.Info( "Think & Drink: Practice Bot joined the lobby." );
	}

	public void RemoveBot( string reason = null )
	{
		if ( !Networking.IsHost || !BotActive ) return;

		ResetBotActions();

		if ( Bot.IsValid() )
			Bot.GameObject.Destroy();

		Bot = null;
		BotActive = false;

		if ( GetHumanCount() <= 1 )
			WaitingForPlayers = true;

		GameEvents.RaisePlayerListChanged();

		if ( !string.IsNullOrEmpty( reason ) )
			Log.Info( $"Think & Drink: {reason}" );
	}

	private void OnQuestionShown( TriviaQuestion question ) => _botQuestion = question;

	private void OnPhaseChanged( MatchPhase phase )
	{
		if ( !Networking.IsHost ) return;
		ResetBotActions();
		_phaseEntered = 0;

		if ( !BotActive || !Bot.IsValid() ) return;

		switch ( phase )
		{
			case MatchPhase.CreativeSubmit:
				ScheduleCreativeQuip();
				break;
			case MatchPhase.CreativeVote:
				ScheduleCreativeVote();
				break;
			case MatchPhase.BuzzIn:
				ScheduleBuzz();
				break;
			case MatchPhase.Answering when MatchManager.Instance?.ActiveModeUsesBuzzers == false:
				ScheduleAnswer( isSteal: false );
				break;
			case MatchPhase.Answering when Bot.SteamKey == MatchManager.Instance?.BuzzWinnerKey:
				ScheduleAnswer( isSteal: false );
				break;
			case MatchPhase.StealAttempt when Bot.SteamKey != MatchManager.Instance?.BuzzWinnerKey:
				ScheduleSteal();
				break;
		}
	}

	private bool _creativeQuipScheduled;
	private bool _creativeVoteScheduled;

	private void ScheduleCreativeQuip()
	{
		_creativeQuipScheduled = true;
		_botActionDelay = Random.Shared.Float( 4f, 12f );
	}

	private void ScheduleCreativeVote()
	{
		_creativeVoteScheduled = true;
		_botActionDelay = Random.Shared.Float( 3f, 10f );
	}

	private void ScheduleBuzz()
	{
		if ( _botQuestion is null ) return;
		if ( !TriviaBotService.ShouldAttemptBuzz( _botQuestion.Difficulty, Random.Shared ) ) return;

		_buzzScheduled = true;
		_botActionDelay = TriviaBotService.RollBuzzDelay( _botQuestion.Difficulty, Random.Shared );
	}

	private void ScheduleAnswer( bool isSteal )
	{
		if ( _botQuestion is null ) return;

		_answerScheduled = !isSteal;
		_stealScheduled = isSteal;
		_botActionDelay = TriviaBotService.RollAnswerDelay( _botQuestion.Difficulty, Random.Shared );
	}

	private void ScheduleSteal()
	{
		if ( _botQuestion is null ) return;
		if ( !TriviaBotService.ShouldAttemptSteal( _botQuestion.Difficulty, Random.Shared ) ) return;

		_stealScheduled = true;
		_botActionDelay = TriviaBotService.RollStealDelay( Random.Shared );
	}

	private void ResetBotActions()
	{
		_buzzScheduled = false;
		_answerScheduled = false;
		_stealScheduled = false;
		_creativeQuipScheduled = false;
		_creativeVoteScheduled = false;
		_botActionDelay = 0;
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy || !Networking.IsHost ) return;

		if ( _autoJoinPending && _autoJoinDelay )
		{
			_autoJoinPending = false;
			if ( GetHumanCount() == 1 && MatchManager.Instance?.Phase == MatchPhase.Lobby && !BotActive )
				EnsureBot( autoReady: true );
		}

		if ( !BotActive || !Bot.IsValid() ) return;

		var phase = MatchManager.Instance?.Phase ?? MatchPhase.Lobby;
		if ( phase is MatchPhase.Lobby or MatchPhase.PostMatch or MatchPhase.MatchEnd ) return;

		if ( _botActionDelay > 0 && _phaseEntered < _botActionDelay ) return;

		if ( _creativeQuipScheduled && phase == MatchPhase.CreativeSubmit )
		{
			_creativeQuipScheduled = false;
			var quip = MatchManager.Instance?.PickBotCreativeQuip() ?? CreativeBotService.PickQuip( Random.Shared );
			MatchManager.Instance?.TrySubmitCreativeQuip( Bot, quip );
			return;
		}

		if ( _creativeVoteScheduled && phase == MatchPhase.CreativeVote )
		{
			_creativeVoteScheduled = false;
			var letter = MatchManager.Instance?.PickBotCreativeVote( Bot.SteamKey ) ?? "A";
			MatchManager.Instance?.TrySubmitCreativeVote( Bot, letter );
			return;
		}

		if ( _buzzScheduled && phase == MatchPhase.BuzzIn )
		{
			_buzzScheduled = false;
			MatchManager.Instance?.TryRegisterBuzz( Bot );
			return;
		}

		if ( _answerScheduled && phase == MatchPhase.Answering && (MatchManager.Instance?.ActiveModeUsesBuzzers == false || Bot.SteamKey == MatchManager.Instance?.BuzzWinnerKey) )
		{
			_answerScheduled = false;
			var difficulty = MatchManager.Instance?.CurrentQuestionDifficulty ?? _botQuestion?.Difficulty ?? Difficulty.Medium;
			var correct = TriviaBotService.ShouldAnswerCorrectly( difficulty, Random.Shared );
			var answer = MatchManager.Instance?.PickBotAnswer( correct ) ?? TriviaBotService.PickAnswer( _botQuestion, correct, Random.Shared );
			MatchManager.Instance?.TrySubmitAnswer( Bot, answer, isSteal: false );
			return;
		}

		if ( _stealScheduled && phase == MatchPhase.StealAttempt && Bot.SteamKey != MatchManager.Instance?.BuzzWinnerKey )
		{
			_stealScheduled = false;
			var difficulty = MatchManager.Instance?.CurrentQuestionDifficulty ?? _botQuestion?.Difficulty ?? Difficulty.Medium;
			var correct = TriviaBotService.ShouldAnswerCorrectly( difficulty, Random.Shared );
			var answer = MatchManager.Instance?.PickBotAnswer( correct ) ?? TriviaBotService.PickAnswer( _botQuestion, correct, Random.Shared );
			MatchManager.Instance?.TrySubmitAnswer( Bot, answer, isSteal: true );
		}
	}
}
