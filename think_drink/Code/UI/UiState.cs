namespace ThinkDrink.UI;

public enum UiScreen
{
	Lobby,
	Match,
	PostMatch,
	Leaderboard,
	Profile,
	Achievements,
	Challenges,
	Settings
}

public readonly struct ScorePop
{
	public ScorePop( string label, int points )
	{
		Label = label;
		Points = points;
	}

	public string Label { get; }
	public int Points { get; }
}

/// <summary>Client-side UI state — presentation data routed through <see cref="UIManager"/>.</summary>
public static class UiState
{
	public static UiScreen Overlay => UIManager.Overlay;
	public static int Revision => UIManager.Revision;
	public static string AnswerDraft { get; set; } = "";
	public static string PredictionDraft { get; set; } = "";
	public static string FlashMessage => UIManager.FlashMessage;
	public static bool FlashCorrect => UIManager.FlashCorrect;
	public static string ToastMessage => UIManager.Toasts.Count > 0 ? UIManager.Toasts[^1].Message : "";
	public static bool ToastPositive => UIManager.Toasts.Count > 0 && UIManager.Toasts[^1].Positive;
	public static string LevelUpMessage => UIManager.LevelUpMessage;
	public static int MatchStreak { get; private set; }
	public static bool LocalAnswerPromptActive => _answerPromptActive && !_answerPromptClear;
	public static bool PredictionInputOpen { get; private set; }
	public static bool HasSubmittedPredictionThisRound { get; private set; }
	public static bool BuzzerHoverActive { get; private set; }
	public static int InteractionPulse { get; private set; }
	public static IReadOnlyList<ScorePop> ScorePops => _scorePops;

	private static readonly List<ScorePop> _scorePops = new();
	private static TimeUntil _answerPromptClear;
	private static bool _answerPromptActive;

	public static void PulseInteraction() => InteractionPulse++;

	public static void Bump() => UIManager.Bump();

	public static void OpenOverlay( UiScreen screen ) => UIManager.OpenOverlay( screen );

	public static void CloseOverlay() => UIManager.CloseOverlay();

	public static void ToggleOverlay( UiScreen screen ) => UIManager.ToggleOverlay( screen );

	public static void ShowFlash( string message, bool correct ) => UIManager.ShowFlash( message, correct );

	public static void ShowToast( string message, bool positive = true ) => UIManager.ShowToast( message, positive );

	public static void ShowLevelUp( int level, string title ) => UIManager.ShowLevelUp( level, title );

	public static void SetMatchStreak( int streak )
	{
		if ( MatchStreak == streak ) return;
		MatchStreak = streak;
		Bump();
	}

	public static void AddScorePop( string label, int points )
	{
		_scorePops.Add( new ScorePop( label, points ) );
		if ( _scorePops.Count > 6 )
			_scorePops.RemoveAt( 0 );
		Bump();
	}

	public static void ShowLocalAnswerPrompt()
	{
		ClearAnswerDraft();
		CloseOverlay();
		_answerPromptClear = 1.5f;
		_answerPromptActive = true;
		Bump();
	}

	public static void SetBuzzerHover( bool active )
	{
		if ( BuzzerHoverActive == active ) return;
		BuzzerHoverActive = active;
		Bump();
	}

	public static void ClearAnswerDraft()
	{
		if ( AnswerDraft == "" ) return;
		AnswerDraft = "";
		Bump();
	}

	public static void ClearPredictionDraft()
	{
		if ( PredictionDraft == "" ) return;
		PredictionDraft = "";
		Bump();
	}

	public static void OpenPredictionInput()
	{
		if ( PredictionInputOpen ) return;
		PredictionInputOpen = true;
		Bump();
	}

	public static void MarkPredictionSubmitted()
	{
		HasSubmittedPredictionThisRound = true;
		PredictionInputOpen = false;
		Bump();
	}

	public static void ClearRoundDrafts()
	{
		var changed = AnswerDraft != "" || PredictionDraft != "" || _answerPromptActive || BuzzerHoverActive
			|| PredictionInputOpen || HasSubmittedPredictionThisRound;
		AnswerDraft = "";
		PredictionDraft = "";
		_answerPromptActive = false;
		PredictionInputOpen = false;
		HasSubmittedPredictionThisRound = false;
		BuzzerHoverActive = false;
		MatchStreak = 0;
		if ( changed )
			Bump();
	}

	public static void UpdateDraftTimers()
	{
		if ( _answerPromptActive && _answerPromptClear )
		{
			_answerPromptActive = false;
			Bump();
		}
	}

	public static void Update() => UIManager.Update();
}
