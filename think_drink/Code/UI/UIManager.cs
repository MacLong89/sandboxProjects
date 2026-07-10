namespace ThinkDrink.UI;

/// <summary>
/// Central UI authority — every open/close, layer, focus, notification, and input decision flows here.
/// </summary>
public static class UIManager
{
	public const int MaxVisibleToasts = 3;
	public const float ToastDuration = 2f;
	public const float FlashDuration = 1.2f;
	public const float LevelUpDuration = 2.5f;
	public const float TooltipDelaySeconds = 0.35f;

	public static int Revision { get; private set; }
	public static UiScreen Overlay { get; private set; } = UiScreen.Lobby;
	public static UiInputContext InputContext => UiInputManager.ActiveContext;
	public static string FlashMessage { get; private set; } = "";
	public static bool FlashCorrect { get; private set; }
	public static string LevelUpMessage { get; private set; } = "";
	public static UiTooltipState Tooltip { get; private set; }
	public static IReadOnlyList<UiToastEntry> Toasts => _toastView;
	public static IReadOnlyCollection<UiWindowId> ActiveWindows => _activeWindows;

	public static bool ShowBoardTuner => IsWindowOpen( UiWindowId.BoardTuner );
	public static bool OnboardingActive => IsWindowOpen( UiWindowId.Onboarding );
	public static bool TooltipPending => _tooltipPending;
	public static bool TooltipDelayElapsed => _tooltipDelay;

	private static readonly HashSet<UiWindowId> _activeWindows = new();
	private static readonly Stack<UiWindowId> _focusStack = new();
	private static readonly List<UiToastEntry> _toastView = new();
	private static readonly List<(UiToastEntry Entry, TimeUntil Expires)> _toasts = new();
	private static readonly Queue<(UiNotificationKind Kind, string Message, bool Positive, int Level, string Title)> _deferredNotifications = new();

	private static TimeUntil _flashClear;
	private static bool _flashActive;
	private static TimeUntil _levelUpClear;
	private static string _pendingTooltipText = "";
	private static TimeUntil _tooltipDelay;
	private static bool _tooltipPending;

	public static void Bump() => Revision++;

	public static int GetLayerZIndex( UiLayerPriority layer ) => (int)layer;

	public static bool IsWindowOpen( UiWindowId window ) => _activeWindows.Contains( window );

	public static bool IsOverlayOpen => Overlay != UiScreen.Lobby;

	public static bool HasModalOpen
	{
		get
		{
			foreach ( var id in _activeWindows )
			{
				var def = UiWindowRegistry.Get( id );
				if ( def.IsModal && def.BlocksLowerInteraction )
					return true;
			}

			return false;
		}
	}

	public static bool BlocksWorldInput =>
		HasModalOpen || ShowBoardTuner || UiState.LocalAnswerPromptActive;

	public static bool BlocksGameplayInput =>
		OnboardingActive || IsOverlayOpen || ShowBoardTuner;

	public static bool ShouldDimBackground => HasModalOpen;

	public static bool ShouldSuppressHudChrome => UiGameplayPolicy.ShouldSuppressHudChrome;

	public static bool Request( UiRequest request )
	{
		return request.Action switch
		{
			UiRequestAction.Open => TryOpen( request.Window ),
			UiRequestAction.Close => TryClose( request.Window ),
			UiRequestAction.Toggle => IsWindowOpen( request.Window ) ? TryClose( request.Window ) : TryOpen( request.Window ),
			_ => false
		};
	}

	public static bool TryOpen( UiWindowId window )
	{
		if ( window == UiWindowId.None ) return false;
		if ( IsWindowOpen( window ) ) return false;
		if ( !UiGameplayPolicy.AllowsWindow( window ) ) return false;
		if ( !UiCompatibility.CanOpen( window, _activeWindows ) )
			CloseConflicts( window );

		if ( !UiCompatibility.CanOpen( window, _activeWindows ) )
			return false;

		RegisterWindow( window );
		Bump();
		return true;
	}

	public static bool TryClose( UiWindowId window )
	{
		if ( !IsWindowOpen( window ) ) return false;
		UnregisterWindow( window );
		Bump();
		return true;
	}

	public static void OpenOverlay( UiScreen screen )
	{
		var window = UiWindowRegistry.FromScreen( screen );
		if ( window == UiWindowId.None ) return;
		Request( UiRequest.Open( window ) );
	}

	public static void CloseOverlay() => TryClose( UiWindowRegistry.FromScreen( Overlay ) );

	public static void ToggleOverlay( UiScreen screen )
	{
		var window = UiWindowRegistry.FromScreen( screen );
		if ( window == UiWindowId.None ) return;
		Request( UiRequest.Toggle( window ) );
	}

	public static bool CanOpenOverlay( UiScreen screen )
	{
		var window = UiWindowRegistry.FromScreen( screen );
		return window != UiWindowId.None && UiGameplayPolicy.AllowsWindow( window );
	}

	public static void SetOnboardingActive( bool active )
	{
		if ( active )
			Request( UiRequest.Open( UiWindowId.Onboarding ) );
		else
			TryClose( UiWindowId.Onboarding );
	}

	public static void CloseTop()
	{
		if ( OnboardingActive ) return;

		if ( IsOverlayOpen )
		{
			CloseOverlay();
			return;
		}

		if ( ShowBoardTuner )
			TryClose( UiWindowId.BoardTuner );
	}

	public static void CloseAllTransient()
	{
		CloseAllInGroup( UiWindowGroup.OverlayPanel );
		TryClose( UiWindowId.BoardTuner );
		ClearNotifications();
		HideTooltip();
	}

	public static void OnMatchPhaseChanged( MatchPhase phase )
	{
		if ( phase is not MatchPhase.Lobby and not MatchPhase.PostMatch )
		{
			CloseAllInGroup( UiWindowGroup.OverlayPanel );
			TryClose( UiWindowId.BoardTuner );
		}

		FlushDeferredNotifications();
	}

	public static void ShowFlash( string message, bool correct )
	{
		if ( string.IsNullOrWhiteSpace( message ) ) return;

		FlashMessage = message;
		FlashCorrect = correct;
		_flashClear = FlashDuration;
		_flashActive = true;
		RegisterWindow( UiWindowId.FlashFeedback );
		Bump();
	}

	public static void ShowToast( string message, bool positive = true )
	{
		if ( string.IsNullOrWhiteSpace( message ) ) return;

		for ( var i = 0; i < _toasts.Count; i++ )
		{
			var existing = _toasts[i].Entry;
			if ( existing.Message != message || existing.Positive != positive ) continue;

			_toasts[i] = (new UiToastEntry( message, positive, existing.Count + 1 ), ToastDuration);
			RebuildToastView();
			Bump();
			return;
		}

		while ( _toasts.Count >= MaxVisibleToasts )
			_toasts.RemoveAt( 0 );

		_toasts.Add( (new UiToastEntry( message, positive ), ToastDuration) );
		RebuildToastView();
		Bump();
	}

	public static void ShowLevelUp( int level, string title )
	{
		if ( UiGameplayPolicy.ShouldDeferNotification( UiNotificationKind.LevelUp ) )
		{
			_deferredNotifications.Enqueue( (UiNotificationKind.LevelUp, "", false, level, title) );
			return;
		}

		ApplyLevelUp( level, title );
	}

	public static void QueueTooltip( string text )
	{
		if ( string.IsNullOrWhiteSpace( text ) )
		{
			HideTooltip();
			return;
		}

		if ( _pendingTooltipText == text && (Tooltip.Visible || _tooltipPending) )
			return;

		_pendingTooltipText = text;
		_tooltipPending = true;
		_tooltipDelay = TooltipDelaySeconds;
	}

	public static void HideTooltip()
	{
		_pendingTooltipText = "";
		_tooltipPending = false;
		if ( !Tooltip.Visible && Tooltip.Text == "" ) return;

		Tooltip = new UiTooltipState( "", false );
		Bump();
	}

	public static void ConfirmTooltip()
	{
		if ( !_tooltipPending || string.IsNullOrWhiteSpace( _pendingTooltipText ) )
			return;

		_tooltipPending = false;
		Tooltip = new UiTooltipState( _pendingTooltipText, true );
		Bump();
	}

	public static void ClearNotifications()
	{
		var changed = _flashActive || _toasts.Count > 0 || !string.IsNullOrEmpty( LevelUpMessage );
		FlashMessage = "";
		_flashActive = false;
		LevelUpMessage = "";
		_toasts.Clear();
		_activeWindows.Remove( UiWindowId.FlashFeedback );
		_activeWindows.Remove( UiWindowId.LevelUpBanner );
		RebuildToastView();
		if ( changed ) Bump();
	}

	public static void Update()
	{
		ExpireNotifications();
		UiState.UpdateDraftTimers();
		UiInputManager.SyncFromWindows( _activeWindows );
	}

	public static void HandleInput() => UiInputManager.HandleInput();

	public static bool WantsMouseCursor()
	{
		if ( UiState.LocalAnswerPromptActive || UiState.PredictionInputOpen ) return true;
		if ( InputContext is UiInputContext.Modal or UiInputContext.Menu or UiInputContext.DevTool ) return true;

		var phase = MatchManager.Instance?.Phase ?? MatchPhase.Lobby;
		if ( phase == MatchPhase.PostMatch ) return true;

		if ( phase == MatchPhase.Lobby )
		{
			if ( LobbyManager.Instance?.CountdownActive == true )
				return false;
			return true;
		}

		return WantsInMatchUiCursor( phase );
	}

	public static bool WantsMovement()
	{
		if ( UiState.LocalAnswerPromptActive || UiState.PredictionInputOpen ) return false;
		if ( HasModalOpen ) return false;

		var phase = MatchManager.Instance?.Phase ?? MatchPhase.Lobby;
		if ( phase is MatchPhase.Lobby or MatchPhase.PostMatch )
		{
			if ( phase == MatchPhase.Lobby && LobbyManager.Instance?.CountdownActive == true )
				return true;
			return !IsOverlayOpen;
		}

		return !WantsInMatchUiCursor( phase );
	}

	public static void OpenLeaderboard() => Request( UiRequest.Open( UiWindowId.Leaderboard ) );
	public static void OpenProfile() => Request( UiRequest.Open( UiWindowId.Profile ) );
	public static void OpenAchievements() => Request( UiRequest.Open( UiWindowId.Achievements ) );
	public static void OpenChallenges() => Request( UiRequest.Open( UiWindowId.Challenges ) );
	public static void OpenSettings() => Request( UiRequest.Open( UiWindowId.Settings ) );
	public static void Close() => CloseTop();

	static void RegisterWindow( UiWindowId window )
	{
		var def = UiWindowRegistry.Get( window );
		if ( def.Group == UiWindowGroup.OverlayPanel )
			CloseAllInGroup( UiWindowGroup.OverlayPanel );

		_activeWindows.Add( window );

		if ( def.Group is UiWindowGroup.OverlayPanel or UiWindowGroup.DevTool or UiWindowGroup.ForcedModal )
			_focusStack.Push( window );

		if ( def.Group == UiWindowGroup.OverlayPanel )
			Overlay = UiWindowRegistry.ToScreen( window );

		UiInputManager.SyncFromWindows( _activeWindows );
	}

	static void UnregisterWindow( UiWindowId window )
	{
		if ( !_activeWindows.Remove( window ) ) return;

		if ( UiWindowRegistry.FromScreen( Overlay ) == window )
			Overlay = UiScreen.Lobby;

		RemoveFromFocusStack( window );
		UiInputManager.SyncFromWindows( _activeWindows );
	}

	static void CloseConflicts( UiWindowId target )
	{
		foreach ( var conflict in UiCompatibility.GetConflicts( target, _activeWindows ).ToList() )
			TryClose( conflict );
	}

	static void CloseAllInGroup( UiWindowGroup group )
	{
		foreach ( var id in _activeWindows.ToList() )
		{
			if ( UiWindowRegistry.Get( id ).Group == group )
				TryClose( id );
		}
	}

	static void RemoveFromFocusStack( UiWindowId window )
	{
		if ( _focusStack.Count == 0 ) return;

		if ( _focusStack.Contains( window ) )
		{
			var items = _focusStack.Where( w => w != window ).Reverse().ToList();
			_focusStack.Clear();
			foreach ( var item in items )
				_focusStack.Push( item );
		}
	}

	static void ApplyLevelUp( int level, string title )
	{
		var message = $"LEVEL {level} — {title}";
		if ( LevelUpMessage == message ) return;

		LevelUpMessage = message;
		_levelUpClear = LevelUpDuration;
		RegisterWindow( UiWindowId.LevelUpBanner );
		GameEvents.RaiseAudio( AudioEventId.RankUp );
		Bump();
	}

	static void FlushDeferredNotifications()
	{
		if ( UiGameplayPolicy.IsActiveRoundPhase ) return;

		while ( _deferredNotifications.Count > 0 )
		{
			var item = _deferredNotifications.Dequeue();
			if ( item.Kind == UiNotificationKind.LevelUp )
				ApplyLevelUp( item.Level, item.Title );
		}
	}

	static void ExpireNotifications()
	{
		var changed = false;

		if ( _flashActive && _flashClear )
		{
			FlashMessage = "";
			_flashActive = false;
			_activeWindows.Remove( UiWindowId.FlashFeedback );
			changed = true;
		}

		if ( !string.IsNullOrEmpty( LevelUpMessage ) && _levelUpClear )
		{
			LevelUpMessage = "";
			_activeWindows.Remove( UiWindowId.LevelUpBanner );
			changed = true;
		}

		if ( _toasts.Count > 0 )
		{
			for ( var i = _toasts.Count - 1; i >= 0; i-- )
			{
				if ( _toasts[i].Expires )
				{
					_toasts.RemoveAt( i );
					changed = true;
				}
			}
		}

		if ( changed )
		{
			RebuildToastView();
			Bump();
		}
	}

	static void RebuildToastView()
	{
		_toastView.Clear();
		for ( var i = 0; i < _toasts.Count; i++ )
			_toastView.Add( _toasts[i].Entry );
	}

	static bool WantsInMatchUiCursor( MatchPhase phase )
	{
		var local = ThinkDrinkPlayer.Local;
		if ( local is null || !local.IsParticipant || local.IsSpectator )
			return false;

		var mm = MatchManager.Instance;
		if ( mm is null ) return false;

		var usesBuzzers = mm.ActiveModeUsesBuzzers;
		var isBuzzWinner = local.SteamKey == mm.BuzzWinnerKey;

		if ( phase == MatchPhase.Answering )
		{
			if ( !usesBuzzers && !local.HasAnsweredThisRound )
				return true;
			if ( isBuzzWinner )
				return true;
		}

		if ( phase == MatchPhase.StealAttempt && !isBuzzWinner )
			return true;

		if ( phase is MatchPhase.CreativeSubmit or MatchPhase.CreativeVote )
			return true;

		if ( WantsPredictionUi( mm, local, phase, isBuzzWinner ) )
			return true;

		if ( phase == MatchPhase.BuzzIn && usesBuzzers )
			return true;

		return false;
	}

	static bool WantsPredictionUi( MatchManager mm, ThinkDrinkPlayer local, MatchPhase phase, bool isBuzzWinner )
	{
		if ( !mm.PredictionsEnabled || !mm.ActiveModeUsesBuzzers ) return false;
		if ( isBuzzWinner || UiState.HasSubmittedPredictionThisRound ) return false;

		return phase is MatchPhase.QuestionReveal or MatchPhase.BuzzIn or MatchPhase.Answering or MatchPhase.StealAttempt;
	}
}
