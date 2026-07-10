namespace ThinkDrink;

/// <summary>
/// Host-authoritative match state machine. Owns phases, scoring, buzz logic, and win detection.
/// </summary>
public sealed class MatchManager : Component
{
	public static MatchManager Instance { get; private set; }

	[Sync( SyncFlags.FromHost )] public MatchPhase Phase { get; set; } = MatchPhase.Lobby;
	[Sync( SyncFlags.FromHost )] public string CurrentCategory { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public string QuestionText { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public string RevealedAnswer { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public string Explanation { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public string BuzzWinnerKey { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public string BuzzWinnerName { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public float PhaseTimer { get; set; }
	[Sync( SyncFlags.FromHost )] public int WinThreshold { get; set; } = GameConstants.DefaultWinThreshold;
	[Sync( SyncFlags.FromHost )] public int RoundNumber { get; set; }
	[Sync( SyncFlags.FromHost )] public bool StealEnabled { get; set; } = true;
	[Sync( SyncFlags.FromHost )] public bool PredictionsEnabled { get; set; } = true;
	[Sync( SyncFlags.FromHost )] public RandomEventType ActiveEvent { get; set; }
	[Sync( SyncFlags.FromHost )] public string EventBanner { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public string WinnerKey { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public string WinnerName { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public int DifficultyLevel { get; set; }
	[Sync( SyncFlags.FromHost )] public GameModeId ActiveGameModeId { get; set; } = GameModeId.TriviaShowdown;
	[Sync( SyncFlags.FromHost )] public string LastScorerName { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public int LastPointsAwarded { get; set; }
	[Sync( SyncFlags.FromHost )] public int LastScorerStreak { get; set; }
	[Sync( SyncFlags.FromHost )] public float LastAnswerResponseTime { get; set; }
	[Sync( SyncFlags.FromHost )] public bool ActiveModeUsesBuzzers { get; set; } = true;
	[Sync( SyncFlags.FromHost )] public bool ActiveModeScoresAllCorrectAnswers { get; set; }
	[Sync( SyncFlags.FromHost )] public bool UsesMultipleChoice { get; set; }
	[Sync( SyncFlags.FromHost )] public string McqA { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public string McqB { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public string McqC { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public string McqD { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public int PhaseSkipCount { get; set; }
	[Sync( SyncFlags.FromHost )] public int PhaseSkipRequired { get; set; }
	[Sync( SyncFlags.FromHost )] public bool IsCreativeMode { get; set; }
	[Sync( SyncFlags.FromHost )] public int CreativeSubmitCount { get; set; }
	[Sync( SyncFlags.FromHost )] public int CreativeSubmitRequired { get; set; }
	[Sync( SyncFlags.FromHost )] public string CreativeVotePack { get; set; } = "";

	// Host-only runtime (not replicated)
	private IGameMode _activeGameMode;
	private TriviaQuestion _currentQuestion;
	private GameModePrompt _roundPrompt;
	private readonly HashSet<string> _usedQuestionIds = new();
	private readonly HashSet<string> _phaseSkipVotes = new();
	private readonly HashSet<string> _featuredBonusGranted = new();
	private readonly Dictionary<string, int> _matchCorrect = new();
	private readonly Dictionary<string, int> _matchIncorrect = new();
	private readonly Dictionary<string, int> _matchBuzzWins = new();
	private readonly Dictionary<string, float> _matchFastest = new();
	private readonly Dictionary<string, int> _matchScore = new();
	private readonly Dictionary<string, int> _matchStreak = new();
	private readonly HashSet<string> _stealAttempted = new();
	private readonly HashSet<string> _predictionsProcessed = new();
	private readonly HashSet<string> _openRoundCorrect = new();
	private readonly Dictionary<string, string> _creativeSubmissions = new();
	private readonly Dictionary<string, string> _creativeVotes = new();
	private readonly Dictionary<string, string> _creativeAuthorByLetter = new();
	private readonly Dictionary<string, int> _creativeVoteCounts = new();
	private readonly HashSet<string> _usedCreativePromptIds = new();
	private string _creativePromptId = "";
	private MatchResult _lastResult;
	private TimeSince _answerPhaseStart;
	private bool _buzzLocked;
	private TimeSince _buzzLockedAt;
	private bool _roundResolved;

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
	}

	public MatchResult LastResult => _lastResult;
	public GameModeDefinition ActiveGameModeDefinition => GameModeRegistry.GetDefinition( ActiveGameModeId );
	public string ActiveModeLabel => _activeGameMode?.GetScreenLabel( Phase ) ?? ActiveGameModeDefinition.ShortName;
	public IReadOnlyList<CreativeVoteOption> CreativeVoteOptions => CreativeVoteCodec.Decode( CreativeVotePack );

	public void StartMatch( int winThreshold )
	{
		StartMatch( winThreshold, GameNightManager.Instance?.SelectedGameMode ?? GameModeId.TriviaShowdown );
	}

	public void StartMatch( int winThreshold, GameModeId mode )
	{
		if ( !Networking.IsHost ) return;

		_activeGameMode?.Cleanup();
		ActiveGameModeId = mode;
		_activeGameMode = GameModeRegistry.Create( mode );
		_activeGameMode.Initialize( this );
		IsCreativeMode = _activeGameMode.GetRoundSettings( 1, RandomEventType.None ).UsesCreativeFlow;

		WinThreshold = Math.Clamp( winThreshold, GameConstants.MinWinThreshold, GameConstants.MaxWinThreshold );
		var settings = _activeGameMode.GetRoundSettings( 1, RandomEventType.None );
		StealEnabled = settings.StealsEnabled;
		PredictionsEnabled = settings.PredictionsEnabled;
		ActiveModeUsesBuzzers = settings.UsesBuzzers;
		ActiveModeScoresAllCorrectAnswers = settings.ScoresAllCorrectAnswers;
		ActiveEvent = RandomEventType.None;
		RoundNumber = 0;
		WinnerKey = "";
		WinnerName = "";
		_usedQuestionIds.Clear();
		_matchCorrect.Clear();
		_matchIncorrect.Clear();
		_matchBuzzWins.Clear();
		_matchFastest.Clear();
		_matchScore.Clear();
		_matchStreak.Clear();
		LastScorerName = "";
		LastPointsAwarded = 0;
		LastScorerStreak = 0;
		_featuredBonusGranted.Clear();
		_lastResult = null;

		for ( var i = 0; i < ThinkDrinkPlayer.All.Count; i++ )
		{
			var p = ThinkDrinkPlayer.All[i];
			if ( !p.IsParticipant ) continue;
			p.ResetMatchScore();
			_matchScore[p.SteamKey] = 0;
		}

		StatsManager.Instance?.BeginMatch();
		SetPhase( MatchPhase.CategoryReveal );
		BeginNextRound();
	}

	private void BeginNextRound()
	{
		if ( !Networking.IsHost ) return;

		RoundNumber++;
		_activeGameMode ??= GameModeRegistry.Create( ActiveGameModeId );
		_activeGameMode.Initialize( this );
		_activeGameMode.StartRound( RoundNumber );
		_buzzLocked = false;
		_roundResolved = false;
		_stealAttempted.Clear();
		_predictionsProcessed.Clear();
		_openRoundCorrect.Clear();
		RevealedAnswer = "";
		Explanation = "";
		_roundPrompt = null;
		BuzzWinnerKey = "";
		BuzzWinnerName = "";

		for ( var i = 0; i < ThinkDrinkPlayer.All.Count; i++ )
			ThinkDrinkPlayer.All[i].ResetRoundState();

		_creativeSubmissions.Clear();
		_creativeVotes.Clear();
		_creativeAuthorByLetter.Clear();
		_creativeVoteCounts.Clear();
		CreativeVotePack = "";
		CreativeSubmitCount = 0;
		UsesMultipleChoice = false;
		McqA = McqB = McqC = McqD = "";

		var settings = _activeGameMode.GetRoundSettings( RoundNumber, ActiveEvent );
		StealEnabled = settings.StealsEnabled;
		PredictionsEnabled = settings.PredictionsEnabled;
		ActiveModeUsesBuzzers = settings.UsesBuzzers;
		ActiveModeScoresAllCorrectAnswers = settings.ScoresAllCorrectAnswers;
		IsCreativeMode = settings.UsesCreativeFlow;

		if ( IsCreativeMode && _activeGameMode is ICreativeGameMode creativeMode )
		{
			BeginCreativeRound( creativeMode, settings );
			return;
		}

		var category = PickCategoryForRound();
		CurrentCategory = category;
		var questionCount = QuestionManager.Instance?.Repository.GetAll().Count ?? 0;
		if ( questionCount > 0 && _usedQuestionIds.Count >= questionCount )
			_usedQuestionIds.Clear();

		_currentQuestion = QuestionManager.Instance?.PickQuestion( _usedQuestionIds, category, Random.Shared );

		if ( _currentQuestion is null )
		{
			Log.Warning( "Think & Drink: no questions available." );
			EndMatch( null );
			return;
		}

		_usedQuestionIds.Add( _currentQuestion.Id );

		_roundPrompt = _activeGameMode.BuildPrompt( _currentQuestion, RoundNumber, Random.Shared );
		CurrentCategory = string.IsNullOrWhiteSpace( _roundPrompt.Category ) ? _currentQuestion.Category : _roundPrompt.Category;
		QuestionText = _roundPrompt.Question;
		DifficultyLevel = (int)_currentQuestion.Difficulty;
		ApplyMcqFromPrompt();
		GameEvents.RaiseQuestionShown( _currentQuestion );

		if ( ActiveEvent == RandomEventType.LightningRound )
		{
			SetPhase( MatchPhase.QuestionReveal );
			PhaseTimer = settings.FirstQuestionRevealSeconds;
		}
		else
		{
			SetPhase( MatchPhase.CategoryReveal );
			PhaseTimer = RoundNumber == 1
				? settings.FirstCategoryRevealSeconds
				: settings.CategoryRevealSeconds;
			GameEvents.RaiseAudio( AudioEventId.CategoryReveal );
		}
	}

	void BeginCreativeRound( ICreativeGameMode creativeMode, GameModeRoundSettings settings )
	{
		_currentQuestion = null;
		_roundPrompt = creativeMode.PickPrompt( RoundNumber, Random.Shared, _usedCreativePromptIds );
		_creativePromptId = _roundPrompt.Explanation ?? "";
		if ( !string.IsNullOrEmpty( _creativePromptId ) )
			_usedCreativePromptIds.Add( _creativePromptId );

		CurrentCategory = string.IsNullOrWhiteSpace( _roundPrompt.Category ) ? DefinitionCategoryFallback() : _roundPrompt.Category;
		QuestionText = _roundPrompt.Question;
		DifficultyLevel = (int)Difficulty.Medium;
		RevealedAnswer = "";
		Explanation = "";

		SetPhase( MatchPhase.CategoryReveal );
		PhaseTimer = RoundNumber == 1
			? settings.FirstCategoryRevealSeconds
			: settings.CategoryRevealSeconds;
		GameEvents.RaiseAudio( AudioEventId.CategoryReveal );
	}

	static string DefinitionCategoryFallback() => "Creative";

	string PickCategoryForRound()
	{
		var previous = CurrentCategory;
		var category = QuestionSelectionService.PickCategory( GameConstants.Categories, Random.Shared );

		if ( ActiveEvent != RandomEventType.CategorySwap || string.IsNullOrEmpty( previous ) )
			return category;

		for ( var i = 0; i < 6 && category == previous; i++ )
			category = QuestionSelectionService.PickCategory( GameConstants.Categories, Random.Shared );

		return category;
	}

	private void EnterQuestionReveal()
	{
		var settings = _activeGameMode?.GetRoundSettings( RoundNumber, ActiveEvent ) ?? new GameModeRoundSettings();
		SetPhase( MatchPhase.QuestionReveal );
		PhaseTimer = ActiveEvent == RandomEventType.LightningRound
			? settings.QuestionRevealSeconds
			: RoundNumber == 1
				? settings.FirstQuestionRevealSeconds
				: settings.QuestionRevealSeconds;
		GameEvents.RaiseAudio( AudioEventId.RoundStart );
	}

	private void EnterBuzzPhase()
	{
		var settings = _activeGameMode?.GetRoundSettings( RoundNumber, ActiveEvent ) ?? new GameModeRoundSettings();
		if ( !settings.UsesBuzzers )
		{
			QuestionText = string.IsNullOrWhiteSpace( _roundPrompt?.AnswerQuestion )
				? QuestionText
				: _roundPrompt.AnswerQuestion;
			SetPhase( MatchPhase.Answering );
			PhaseTimer = settings.AnswerWindowSeconds;
			BuzzWinnerKey = "";
			BuzzWinnerName = "";
			_buzzLocked = true;
			_answerPhaseStart = 0;
			return;
		}

		QuestionText = string.IsNullOrWhiteSpace( _roundPrompt?.AnswerQuestion )
			? QuestionText
			: _roundPrompt.AnswerQuestion;
		SetPhase( MatchPhase.BuzzIn );
		PhaseTimer = settings.BuzzWindowSeconds;
		_buzzLocked = false;
	}

	public void TryRegisterBuzz( ThinkDrinkPlayer player )
	{
		if ( !Networking.IsHost ) return;
		if ( Phase != MatchPhase.BuzzIn || player is null ) return;
		if ( !player.IsParticipant ) return;

		if ( _buzzLocked )
		{
			GameEvents.RaiseBuzzRejected( player.SteamKey );
			if ( _buzzLockedAt.Relative <= GameConstants.NearMissBuzzSeconds )
				player.NotifyBuzzNearMiss();
			else
				player.NotifyBuzzTooSlow();
			return;
		}

		_buzzLocked = true;
		_buzzLockedAt = 0;
		BuzzWinnerKey = player.SteamKey;
		BuzzWinnerName = player.PlayerName;
		player.IsBuzzWinner = true;

		if ( !_matchBuzzWins.ContainsKey( player.SteamKey ) )
			_matchBuzzWins[player.SteamKey] = 0;
		_matchBuzzWins[player.SteamKey]++;

		ChallengeManager.Instance?.OnBuzzWin( player.SteamKey );
		StatsManager.Instance?.RecordBuzzWin( player.SteamKey );
		player.NotifyBuzzWinner();
		GameEvents.RaiseBuzzWinner( player.SteamKey );

		SetPhase( MatchPhase.Answering );
		PhaseTimer = (_activeGameMode?.GetRoundSettings( RoundNumber, ActiveEvent ) ?? new GameModeRoundSettings()).AnswerWindowSeconds;
		_answerPhaseStart = 0;
	}

	public void TrySubmitAnswer( ThinkDrinkPlayer player, string answer, bool isSteal )
	{
		if ( !Networking.IsHost || player is null || _roundResolved ) return;
		_activeGameMode ??= GameModeRegistry.Create( ActiveGameModeId );
		if ( !_activeGameMode.HandlePlayerInput( new GameModeInput { Player = player, Text = answer, IsSteal = isSteal } ) )
			return;

		if ( isSteal )
		{
			if ( Phase != MatchPhase.StealAttempt ) return;
			if ( player.SteamKey == BuzzWinnerKey ) return;
			if ( _stealAttempted.Contains( player.SteamKey ) ) return;
			if ( player.HasAnsweredThisRound ) return;

			_stealAttempted.Add( player.SteamKey );
			player.HasAnsweredThisRound = true;
			ResolveAnswer( player, answer, isSteal: true );
			return;
		}

		if ( Phase != MatchPhase.Answering ) return;
		if ( !ActiveModeUsesBuzzers )
		{
			SubmitOpenAnswer( player, answer );
			return;
		}

		if ( player.SteamKey != BuzzWinnerKey ) return;
		if ( player.HasAnsweredThisRound ) return;

		player.HasAnsweredThisRound = true;
		ResolveAnswer( player, answer, isSteal: false );
	}

	private void SubmitOpenAnswer( ThinkDrinkPlayer player, string answer )
	{
		if ( player is null || !player.IsParticipant ) return;
		if ( player.HasAnsweredThisRound ) return;
		if ( _currentQuestion is null ) return;

		player.HasAnsweredThisRound = true;
		var validation = _activeGameMode?.ValidateAnswer( answer, _roundPrompt, _currentQuestion, QuestionManager.Instance.Validator )
			?? QuestionManager.Instance.Validator.Validate( answer, _currentQuestion );
		var responseTime = _answerPhaseStart.Relative;
		if ( validation.IsCorrect )
		{
			_openRoundCorrect.Add( player.SteamKey );
			AwardCorrect( player, responseTime, isSteal: false, checkWin: !ActiveModeScoresAllCorrectAnswers );
			if ( !ActiveModeScoresAllCorrectAnswers )
			{
				FinishOpenAnswerRound();
				return;
			}
		}
		else
		{
			GameEvents.RaiseAnswerResolved( player.SteamKey, false );
			player.NotifyAnswerResult( false, isSteal: false );
			GameEvents.RaiseAudio( AudioEventId.Incorrect );
			StatsManager.Instance?.RecordIncorrect( player.SteamKey );
			_matchStreak[player.SteamKey] = 0;
		}

		if ( AllOpenAnswersSubmitted() )
			FinishOpenAnswerRound();
	}

	private bool AllOpenAnswersSubmitted()
	{
		for ( var i = 0; i < ThinkDrinkPlayer.All.Count; i++ )
		{
			var p = ThinkDrinkPlayer.All[i];
			if ( !p.IsParticipant ) continue;
			if ( !p.HasAnsweredThisRound )
				return false;
		}

		return true;
	}

	private void FinishOpenAnswerRound()
	{
		if ( _roundResolved ) return;
		ResolveOpenAnswerWinner();
		_roundResolved = true;
		RevealAndContinue();
	}

	public void TrySubmitCreativeQuip( ThinkDrinkPlayer player, string answer )
	{
		if ( !Networking.IsHost || player is null || !IsCreativeMode ) return;
		if ( Phase != MatchPhase.CreativeSubmit ) return;
		if ( !player.IsParticipant ) return;
		if ( _creativeSubmissions.ContainsKey( player.SteamKey ) ) return;

		var text = CreativeVoteCodec.SanitizeSubmission( answer );
		if ( string.IsNullOrWhiteSpace( text ) ) return;

		_creativeSubmissions[player.SteamKey] = text;
		player.HasAnsweredThisRound = true;
		CreativeSubmitCount = _creativeSubmissions.Count;

		player.NotifyToast( "Quip locked in!", true );
		GameEvents.RaiseAnswerResolved( player.SteamKey, true );

		if ( AllCreativeSubmissionsIn() )
			EnterCreativeVote();
	}

	public void TrySubmitCreativeVote( ThinkDrinkPlayer player, string letter )
	{
		if ( !Networking.IsHost || player is null || !IsCreativeMode ) return;
		if ( Phase != MatchPhase.CreativeVote ) return;
		if ( !player.IsParticipant ) return;
		if ( player.HasAnsweredThisRound ) return;
		if ( _creativeVotes.ContainsKey( player.SteamKey ) ) return;

		letter = letter?.Trim().ToUpperInvariant() ?? "";
		if ( !_creativeAuthorByLetter.TryGetValue( letter, out var authorKey ) ) return;
		if ( authorKey == player.SteamKey ) return;

		_creativeVotes[player.SteamKey] = authorKey;
		player.HasAnsweredThisRound = true;
		player.NotifyToast( "Vote locked in!", true );

		if ( AllCreativeVotesIn() )
			ResolveCreativeRound();
	}

	bool AllCreativeSubmissionsIn()
	{
		var required = CountCreativeParticipants();
		return required > 0 && CreativeSubmitCount >= required;
	}

	bool AllCreativeVotesIn()
	{
		var required = CountCreativeParticipants();
		return required > 0 && _creativeVotes.Count >= required;
	}

	int CountCreativeParticipants()
	{
		var n = 0;
		for ( var i = 0; i < ThinkDrinkPlayer.All.Count; i++ )
			if ( ThinkDrinkPlayer.All[i].IsParticipant )
				n++;
		return n;
	}

	void EnterCreativeSubmit()
	{
		var settings = _activeGameMode?.GetRoundSettings( RoundNumber, ActiveEvent ) ?? new GameModeRoundSettings();
		CreativeSubmitRequired = CountCreativeParticipants();
		CreativeSubmitCount = 0;
		_creativeSubmissions.Clear();
		_creativeVotes.Clear();
		_creativeAuthorByLetter.Clear();
		_creativeVoteCounts.Clear();
		CreativeVotePack = "";
		UsesMultipleChoice = false;

		for ( var i = 0; i < ThinkDrinkPlayer.All.Count; i++ )
			ThinkDrinkPlayer.All[i].ResetRoundState();

		SetPhase( MatchPhase.CreativeSubmit );
		PhaseTimer = settings.CreativeSubmitSeconds;
		_answerPhaseStart = 0;
	}

	void EnterCreativeVote()
	{
		if ( _creativeSubmissions.Count == 0 )
		{
			_roundResolved = true;
			RevealAndContinue();
			return;
		}

		var settings = _activeGameMode?.GetRoundSettings( RoundNumber, ActiveEvent ) ?? new GameModeRoundSettings();
		_creativeAuthorByLetter.Clear();
		_creativeVotes.Clear();
		_creativeVoteCounts.Clear();

		var letters = new[] { "A", "B", "C", "D", "E", "F", "G", "H" };
		var entries = _creativeSubmissions
			.OrderBy( kv => kv.Key )
			.Select( ( kv, index ) => (Letter: letters[Math.Min( index, letters.Length - 1 )], kv.Key, kv.Value) )
			.ToList();

		for ( var i = 0; i < entries.Count; i++ )
			_creativeAuthorByLetter[entries[i].Letter] = entries[i].Key;

		CreativeVotePack = CreativeVoteCodec.Encode( entries.Select( e => (e.Letter, e.Value) ) );
		ApplyMcqFromCreativeVote();

		for ( var i = 0; i < ThinkDrinkPlayer.All.Count; i++ )
		{
			var player = ThinkDrinkPlayer.All[i];
			player.ResetRoundState();
			player.CreativeOwnVoteLetter = GetCreativeAuthorLetter( player.SteamKey );
		}

		SetPhase( MatchPhase.CreativeVote );
		PhaseTimer = settings.CreativeVoteSeconds;
	}

	void ApplyMcqFromCreativeVote()
	{
		var options = CreativeVoteCodec.Decode( CreativeVotePack );
		UsesMultipleChoice = options.Count > 0;
		McqA = GetCreativeOptionText( options, 0 );
		McqB = GetCreativeOptionText( options, 1 );
		McqC = GetCreativeOptionText( options, 2 );
		McqD = GetCreativeOptionText( options, 3 );
	}

	static string GetCreativeOptionText( IReadOnlyList<CreativeVoteOption> options, int index ) =>
		index < options.Count ? options[index].Text : "";

	void ResolveCreativeRound()
	{
		if ( _roundResolved ) return;

		var settings = _activeGameMode?.GetRoundSettings( RoundNumber, ActiveEvent ) ?? new GameModeRoundSettings();
		var pointsPerVote = Math.Max( 1, settings.CreativePointsPerVote );
		_creativeVoteCounts.Clear();

		foreach ( var vote in _creativeVotes.Values )
		{
			if ( !_creativeVoteCounts.ContainsKey( vote ) )
				_creativeVoteCounts[vote] = 0;
			_creativeVoteCounts[vote]++;
		}

		ThinkDrinkPlayer topScorer = null;
		var topVotes = -1;

		foreach ( var kv in _creativeVoteCounts )
		{
			var player = ThinkDrinkPlayer.FindBySteamKey( kv.Key );
			if ( player is null || !player.IsParticipant ) continue;

			var points = kv.Value * pointsPerVote;
			player.MatchScore += points;
			_matchScore[player.SteamKey] = player.MatchScore;
			LastScorerName = player.PlayerName;
			LastPointsAwarded = points;
			LastAnswerResponseTime = 0;

			if ( kv.Value > topVotes )
			{
				topVotes = kv.Value;
				topScorer = player;
			}

			if ( points > 0 )
			{
				GameEvents.RaiseScoreAwarded( player.SteamKey, points, isSteal: false );
				player.NotifyToast( $"+{points} pts · {kv.Value} vote{(kv.Value == 1 ? "" : "s")}!", true );
			}

			if ( player.MatchScore >= WinThreshold )
				CheckWin( player );
		}

		var lines = new List<string>();
		foreach ( var submission in _creativeSubmissions.OrderByDescending( kv => _creativeVoteCounts.GetValueOrDefault( kv.Key ) ) )
		{
			var author = ThinkDrinkPlayer.FindBySteamKey( submission.Key );
			var name = author?.PlayerName ?? "Player";
			var votes = _creativeVoteCounts.GetValueOrDefault( submission.Key );
			lines.Add( $"{name} ({votes} vote{(votes == 1 ? "" : "s")}): \"{submission.Value}\"" );
		}

		RevealedAnswer = topScorer is not null && _creativeSubmissions.TryGetValue( topScorer.SteamKey, out var winningText )
			? $"\"{winningText}\" — {topScorer.PlayerName}"
			: "Round complete";

		Explanation = string.Join( "\n", lines );
		_roundResolved = true;
		_activeGameMode?.EndRound();
		SetPhase( MatchPhase.AnswerReveal );
		PhaseTimer = GameConstants.RoundRevealSeconds;
		GameEvents.RaiseAudio( AudioEventId.ScoreboardReveal );
	}

	public string GetCreativeAuthorLetter( string steamKey )
	{
		foreach ( var kv in _creativeAuthorByLetter )
		{
			if ( kv.Value == steamKey )
				return kv.Key;
		}

		return "";
	}

	public string PickBotCreativeQuip() => CreativeBotService.PickQuip( Random.Shared );

	public string PickBotCreativeVote( string botSteamKey )
	{
		var letters = _creativeAuthorByLetter.Keys.ToList();
		var ownLetter = GetCreativeAuthorLetter( botSteamKey );
		return CreativeBotService.PickVoteLetter( letters, ownLetter, Random.Shared );
	}

	public void TrySubmitPrediction( ThinkDrinkPlayer player, string answer )
	{
		if ( !Networking.IsHost || player is null || !PredictionsEnabled ) return;
		if ( Phase is not (MatchPhase.Answering or MatchPhase.StealAttempt or MatchPhase.BuzzIn) ) return;
		if ( player.SteamKey == BuzzWinnerKey ) return;
		if ( _predictionsProcessed.Contains( player.SteamKey ) ) return;
		if ( _currentQuestion is null ) return;

		_predictionsProcessed.Add( player.SteamKey );
		var result = _activeGameMode?.ValidateAnswer( answer, _roundPrompt, _currentQuestion, QuestionManager.Instance.Validator )
			?? QuestionManager.Instance.Validator.Validate( answer, _currentQuestion );

		GameEvents.RaisePredictionResolved( player.SteamKey, result.IsCorrect );
		player.NotifyPredictionResult( result.IsCorrect );
		if ( !result.IsCorrect ) return;

		var profile = PersistenceManager.Instance?.GetProfile( player.SteamKey );
		if ( profile is null ) return;

		XpService.ApplyXp( profile, GameConstants.PredictionXpBonus );
		PersistenceManager.Instance.SaveProfile( profile );
		player.RefreshLifetimeStats();
	}

	public string PickBotAnswer( bool correct )
	{
		if ( correct && _roundPrompt?.Accepted?.Count > 0 )
			return _roundPrompt.Accepted[0];

		if ( _roundPrompt?.Choices?.Count > 0 )
		{
			var wrongChoices = _roundPrompt.Choices
				.Where( choice => !IsCorrectMcqChoice( choice ) )
				.ToList();
			if ( wrongChoices.Count > 0 )
				return wrongChoices[Random.Shared.Next( wrongChoices.Count )].Letter;
		}

		if ( _roundPrompt?.Accepted?.Count > 0 )
		{
			var wrong = new[] { "A", "B", "C", "D", "pass", "unknown", "12", "red blue green" };
			for ( var i = 0; i < wrong.Length; i++ )
			{
				var candidate = wrong[Random.Shared.Next( wrong.Length )];
				var result = _activeGameMode?.ValidateAnswer( candidate, _roundPrompt, _currentQuestion, QuestionManager.Instance.Validator );
				if ( result?.IsCorrect != true )
					return candidate;
			}
		}

		return TriviaBotService.PickAnswer( _currentQuestion, correct, Random.Shared );
	}

	private bool IsCorrectMcqChoice( McqChoice choice )
	{
		if ( choice is null || _roundPrompt?.Accepted is null )
			return false;

		foreach ( var accepted in _roundPrompt.Accepted )
		{
			if ( string.Equals( choice.Letter, accepted, StringComparison.OrdinalIgnoreCase ) )
				return true;
			if ( string.Equals( choice.Text, accepted, StringComparison.OrdinalIgnoreCase ) )
				return true;
		}

		return false;
	}

	public Difficulty CurrentQuestionDifficulty => _currentQuestion?.Difficulty ?? Difficulty.Medium;

	private void ResolveAnswer( ThinkDrinkPlayer player, string answer, bool isSteal )
	{
		if ( _roundResolved || _currentQuestion is null ) return;

		var validation = _activeGameMode?.ValidateAnswer( answer, _roundPrompt, _currentQuestion, QuestionManager.Instance.Validator )
			?? QuestionManager.Instance.Validator.Validate( answer, _currentQuestion );
		var responseTime = _answerPhaseStart.Relative;

		if ( validation.IsCorrect )
		{
			_roundResolved = true;
			AwardCorrect( player, responseTime, isSteal );
			RevealAndContinue();
			return;
		}

		GameEvents.RaiseAnswerResolved( player.SteamKey, false );
		player.NotifyAnswerResult( false, isSteal );
		GameEvents.RaiseAudio( AudioEventId.Incorrect );
		StatsManager.Instance?.RecordIncorrect( player.SteamKey );
		_matchStreak[player.SteamKey] = 0;

		if ( !isSteal && StealEnabled && Phase == MatchPhase.Answering )
		{
			SetPhase( MatchPhase.StealAttempt );
			PhaseTimer = (_activeGameMode?.GetRoundSettings( RoundNumber, ActiveEvent ) ?? new GameModeRoundSettings()).StealWindowSeconds;
			GameEvents.RaiseAudio( AudioEventId.Steal );
			return;
		}

		if ( isSteal && AllStealsExhausted() )
		{
			_roundResolved = true;
			RevealAndContinue();
		}
		else if ( !isSteal && !StealEnabled )
		{
			_roundResolved = true;
			RevealAndContinue();
		}
	}

	private bool AllStealsExhausted()
	{
		for ( var i = 0; i < ThinkDrinkPlayer.All.Count; i++ )
		{
			var p = ThinkDrinkPlayer.All[i];
			if ( !p.IsConnected ) continue;
			if ( p.SteamKey == BuzzWinnerKey ) continue;
			if ( !_stealAttempted.Contains( p.SteamKey ) )
				return false;
		}
		return true;
	}

	private void AwardCorrect( ThinkDrinkPlayer player, float responseTime, bool isSteal, bool checkWin = true )
	{
		var points = (_activeGameMode ?? GameModeRegistry.Create( ActiveGameModeId ))
			.AwardPoints( _currentQuestion, ActiveEvent, isSteal, responseTime );

		player.MatchScore += points;
		_matchScore[player.SteamKey] = player.MatchScore;

		if ( !_matchStreak.ContainsKey( player.SteamKey ) )
			_matchStreak[player.SteamKey] = 0;
		_matchStreak[player.SteamKey]++;
		LastScorerName = player.PlayerName;
		LastPointsAwarded = points;
		LastScorerStreak = _matchStreak[player.SteamKey];
		LastAnswerResponseTime = responseTime;

		if ( LastScorerStreak >= 3 )
			GameEvents.RaiseAudio( AudioEventId.StreakBonus );

		if ( !_matchCorrect.ContainsKey( player.SteamKey ) )
			_matchCorrect[player.SteamKey] = 0;
		_matchCorrect[player.SteamKey]++;

		if ( responseTime < (_matchFastest.TryGetValue( player.SteamKey, out var prev ) ? prev : float.MaxValue) )
			_matchFastest[player.SteamKey] = responseTime;

		StatsManager.Instance?.RecordCorrect( player.SteamKey, _currentQuestion.Category, _currentQuestion.Difficulty, responseTime );
		ChallengeManager.Instance?.OnCorrectAnswer( player.SteamKey, _currentQuestion, !isSteal && player.SteamKey == BuzzWinnerKey );
		TryAwardFeaturedCategoryBonus( player );

		GameEvents.RaiseAnswerResolved( player.SteamKey, true );
		player.NotifyAnswerResult( true, isSteal );
		GameEvents.RaiseScoreAwarded( player.SteamKey, points, isSteal );
		GameEvents.RaiseAudio( isSteal ? AudioEventId.Steal : AudioEventId.Correct );

		if ( checkWin && (player.MatchScore >= WinThreshold || ActiveEvent == RandomEventType.SuddenDeath) )
			CheckWin( player );
	}

	private void ResolveOpenAnswerWinner()
	{
		if ( !string.IsNullOrEmpty( WinnerKey ) ) return;
		if ( ActiveEvent == RandomEventType.SuddenDeath )
		{
			var suddenWinner = ThinkDrinkPlayer.All
				.Where( p => p.IsParticipant && _openRoundCorrect.Contains( p.SteamKey ) )
				.OrderByDescending( p => p.MatchScore )
				.ThenBy( p => p.PlayerName )
				.FirstOrDefault();
			if ( suddenWinner is not null )
				CheckWin( suddenWinner );
			return;
		}

		var leader = ThinkDrinkPlayer.All
			.Where( p => p.IsParticipant && p.MatchScore >= WinThreshold )
			.OrderByDescending( p => p.MatchScore )
			.ThenBy( p => p.PlayerName )
			.FirstOrDefault();
		if ( leader is not null )
			CheckWin( leader );
	}

	private void EnterScoreboardReveal()
	{
		SetPhase( MatchPhase.ScoreboardReveal );
		PhaseTimer = (_activeGameMode?.GetRoundSettings( RoundNumber, ActiveEvent ) ?? new GameModeRoundSettings()).ScoreboardRevealSeconds;
		GameEvents.RaiseAudio( AudioEventId.ScoreboardReveal );
	}

	private void EnterMatchEnd()
	{
		SetPhase( MatchPhase.MatchEnd );
		PhaseTimer = 4f;
		GameEvents.RaiseAudio( AudioEventId.Win );
	}

	private void RevealAndContinue()
	{
		if ( _currentQuestion is null ) return;

		_activeGameMode?.EndRound();
		RevealedAnswer = _roundPrompt?.RevealedAnswer ?? _currentQuestion.Accepted.FirstOrDefault() ?? "";
		Explanation = _roundPrompt?.Explanation ?? _currentQuestion.Explanation ?? "";
		SetPhase( MatchPhase.AnswerReveal );
		PhaseTimer = GameConstants.RoundRevealSeconds;
	}

	private void CheckWin( ThinkDrinkPlayer player )
	{
		if ( player is null ) return;
		if ( player.MatchScore < WinThreshold && ActiveEvent != RandomEventType.SuddenDeath ) return;

		WinnerKey = player.SteamKey;
		WinnerName = player.PlayerName;
	}

	private void EndMatch( ThinkDrinkPlayer winner )
	{
		if ( winner is not null )
		{
			WinnerKey = winner.SteamKey;
			WinnerName = winner.PlayerName;
		}

		_lastResult = StatsManager.Instance?.FinalizeMatch( WinnerKey, RoundNumber ) ?? BuildFallbackResult();
		AchievementManager.Instance?.ProcessMatchResults( _lastResult );
		ChallengeManager.Instance?.ProcessMatchEnd( _lastResult );
		LeaderboardManager.Instance?.Rebuild();

		SetPhase( MatchPhase.PostMatch );
		PhaseTimer = GameConstants.PostMatchSeconds;
		GameEvents.RaiseMatchEnded( _lastResult );
	}

	private MatchResult BuildFallbackResult() => new()
	{
		WinnerSteamId = WinnerKey,
		WinnerName = WinnerName,
		RoundsPlayed = RoundNumber
	};

	public void OnPlayerDisconnected( Connection connection )
	{
		if ( !Networking.IsHost ) return;

		var player = ThinkDrinkPlayer.FindByConnection( connection );
		if ( player is not null )
			player.IsConnected = false;

		if ( Phase == MatchPhase.Lobby ) return;

		if ( GetActiveParticipantCount() < GameConstants.MinPlayers )
		{
			ReturnToLobby();
			return;
		}

		if ( player is not null && player.SteamKey == BuzzWinnerKey && Phase == MatchPhase.Answering )
		{
			_roundResolved = true;
			RevealAndContinue();
		}
	}

	private int GetActiveParticipantCount() => ThinkDrinkPlayer.CountParticipants();

	public void SkipPostMatch()
	{
		if ( !Networking.IsHost ) return;
		if ( Phase != MatchPhase.PostMatch ) return;
		PhaseTimer = 0;
	}

	public void TrySkipPhase( ThinkDrinkPlayer player )
	{
		if ( !Networking.IsHost || player is null ) return;
		if ( !IsSkippablePhase() ) return;
		if ( !player.IsParticipant ) return;
		if ( player.IsBot ) return;
		if ( !_phaseSkipVotes.Add( player.SteamKey ) ) return;

		PhaseSkipCount = _phaseSkipVotes.Count;
		if ( PhaseSkipCount < PhaseSkipRequired ) return;

		PhaseTimer = 0;
	}

	private static bool IsSkippablePhase( MatchPhase phase ) => phase switch
	{
		MatchPhase.CategoryReveal => true,
		MatchPhase.QuestionReveal => true,
		MatchPhase.CreativeSubmit => true,
		MatchPhase.CreativeVote => true,
		MatchPhase.AnswerReveal => true,
		MatchPhase.ScoreboardReveal => true,
		MatchPhase.MatchEnd => true,
		_ => false
	};

	private bool IsSkippablePhase() => IsSkippablePhase( Phase );

	private void ApplyMcqFromPrompt()
	{
		var choices = _roundPrompt?.Choices;
		if ( choices is null || choices.Count == 0 )
		{
			UsesMultipleChoice = false;
			McqA = McqB = McqC = McqD = "";
			return;
		}

		UsesMultipleChoice = true;
		McqA = GetMcqText( choices, "A" );
		McqB = GetMcqText( choices, "B" );
		McqC = GetMcqText( choices, "C" );
		McqD = GetMcqText( choices, "D" );
	}

	private static string GetMcqText( IReadOnlyList<McqChoice> choices, string letter )
	{
		for ( var i = 0; i < choices.Count; i++ )
		{
			if ( string.Equals( choices[i].Letter, letter, StringComparison.OrdinalIgnoreCase ) )
				return choices[i].Text;
		}

		return "";
	}

	public void ReturnToLobby()
	{
		if ( !Networking.IsHost ) return;

		ActiveEvent = RandomEventType.None;
		EventBanner = "";
		WinnerKey = "";
		WinnerName = "";
		_currentQuestion = null;
		_roundPrompt = null;
		_openRoundCorrect.Clear();
		_creativeSubmissions.Clear();
		_creativeVotes.Clear();
		CreativeVotePack = "";
		IsCreativeMode = false;
		_activeGameMode?.Cleanup();
		_activeGameMode = null;

		for ( var i = 0; i < ThinkDrinkPlayer.All.Count; i++ )
		{
			var p = ThinkDrinkPlayer.All[i];
			p.ResetMatchScore();
			p.ResetRoundState();
			p.IsSpectator = false;
			p.RefreshLifetimeStats();

			if ( p.IsBot )
			{
				p.IsReady = true;
				continue;
			}

			p.IsReady = GameSettings.Current.AutoReady;
		}

		LobbyManager.Instance?.ResetCountdown();
		SetPhase( MatchPhase.Lobby );
		BotManager.Instance?.OnReturnedToLobby();
		LobbyManager.Instance?.TryStartCountdown();
	}

	public void RequestRematch()
	{
		if ( !Networking.IsHost ) return;
		ReturnToLobby();
		LobbyManager.Instance?.StartRematchCountdown();
	}

	private void SetPhase( MatchPhase phase )
	{
		Phase = phase;
		_phaseSkipVotes.Clear();
		PhaseSkipCount = 0;
		PhaseSkipRequired = Math.Max( 1, GetActiveParticipantCount() );
		GameEvents.RaisePhaseChanged( phase );
	}

	private void TryAwardFeaturedCategoryBonus( ThinkDrinkPlayer player )
	{
		if ( player.IsBot || _currentQuestion is null ) return;
		if ( _currentQuestion.Category != GameConstants.GetFeaturedCategory() ) return;
		if ( !_featuredBonusGranted.Add( player.SteamKey ) ) return;

		var profile = PersistenceManager.Instance?.GetProfile( player.SteamKey );
		if ( profile is null ) return;

		XpService.ApplyXp( profile, GameConstants.WeeklyFeaturedCategoryXpBonus );
		PersistenceManager.Instance.SaveProfile( profile );
		player.RefreshLifetimeStats();
		player.NotifyToast( $"+{GameConstants.WeeklyFeaturedCategoryXpBonus} XP · Featured {GameConstants.GetFeaturedCategory()}!", true );
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy || !Networking.IsHost ) return;

		if ( PhaseTimer > 0 )
		{
			PhaseTimer -= Time.Delta;
			if ( PhaseTimer > 0 ) return;
			PhaseTimer = 0;
		}

		TickPhase();
	}

	private void TickPhase()
	{
		switch ( Phase )
		{
			case MatchPhase.Lobby:
				break;

			case MatchPhase.CategoryReveal:
				EnterQuestionReveal();
				break;

			case MatchPhase.QuestionReveal:
				if ( IsCreativeMode )
					EnterCreativeSubmit();
				else
					EnterBuzzPhase();
				break;

			case MatchPhase.CreativeSubmit:
				if ( !_roundResolved )
					EnterCreativeVote();
				break;

			case MatchPhase.CreativeVote:
				if ( !_roundResolved )
					ResolveCreativeRound();
				break;

			case MatchPhase.BuzzIn:
				if ( !_buzzLocked )
				{
					_roundResolved = true;
					RevealAndContinue();
				}
				break;

			case MatchPhase.Answering:
				if ( !_roundResolved )
				{
					if ( !ActiveModeUsesBuzzers )
					{
						FinishOpenAnswerRound();
						break;
					}

					var buzzPlayer = ThinkDrinkPlayer.FindBySteamKey( BuzzWinnerKey );
					if ( buzzPlayer is not null && !buzzPlayer.HasAnsweredThisRound )
						ResolveAnswer( buzzPlayer, "", isSteal: false );
				}
				break;

			case MatchPhase.StealAttempt:
				if ( !_roundResolved )
				{
					_roundResolved = true;
					RevealAndContinue();
				}
				break;

			case MatchPhase.AnswerReveal:
				if ( !string.IsNullOrEmpty( WinnerKey ) )
				{
					EnterMatchEnd();
					break;
				}

				EnterScoreboardReveal();
				break;

			case MatchPhase.ScoreboardReveal:
				if ( !string.IsNullOrEmpty( WinnerKey ) )
				{
					EnterMatchEnd();
					break;
				}

				TryRollRandomEvent();
				if ( Phase == MatchPhase.RandomEvent )
					break;

				BeginNextRound();
				break;

			case MatchPhase.RandomEvent:
				ActiveEvent = RandomEventType.None;
				EventBanner = "";
				if ( !string.IsNullOrEmpty( WinnerKey ) )
					EnterMatchEnd();
				else
					BeginNextRound();
				break;

			case MatchPhase.MatchEnd:
				EndMatch( ThinkDrinkPlayer.FindBySteamKey( WinnerKey ) );
				break;

			case MatchPhase.PostMatch:
				PersistenceManager.Instance?.SaveAll();
				ReturnToLobby();
				break;
		}
	}

	private void TryRollRandomEvent()
	{
		if ( !string.IsNullOrEmpty( WinnerKey ) ) return;
		if ( IsCreativeMode ) return;

		var evt = RandomEventManager.Instance?.Roll() ?? RandomEventType.None;
		if ( evt == RandomEventType.None )
			return;

		ActiveEvent = evt;
		EventBanner = RandomEventService.GetDisplayName( evt );
		SetPhase( MatchPhase.RandomEvent );
		PhaseTimer = 2.5f;
		GameEvents.RaiseRandomEvent( evt );
		GameEvents.RaiseAudio( AudioEventId.RandomEventStinger );
	}
}
