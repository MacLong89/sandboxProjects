namespace ThinkDrink.UI;

/// <summary>Single owner of UI input — only the active context receives keybinds.</summary>
public static class UiInputManager
{
	public static UiInputContext ActiveContext { get; private set; } = UiInputContext.Gameplay;

	public static void SyncFromWindows( IReadOnlyCollection<UiWindowId> activeWindows )
	{
		var next = ResolveContext( activeWindows );
		if ( next == ActiveContext ) return;
		ActiveContext = next;
		UIManager.Bump();
	}

	public static void HandleInput()
	{
		if ( Input.Pressed( "Close" ) )
			UIManager.CloseTop();

		if ( ActiveContext is UiInputContext.Gameplay or UiInputContext.Menu or UiInputContext.DevTool )
		{
			if ( Input.Pressed( "Menu" ) )
				UIManager.Request( UiRequest.Toggle( UiWindowId.BoardTuner ) );
		}

		if ( UIManager.TooltipPending && UIManager.TooltipDelayElapsed )
			UIManager.ConfirmTooltip();
	}

	public static bool ContextAcceptsGameplayShortcuts =>
		ActiveContext is UiInputContext.Gameplay or UiInputContext.HudInteractive;

	static UiInputContext ResolveContext( IReadOnlyCollection<UiWindowId> activeWindows )
	{
		var best = UiInputContext.Gameplay;
		var bestLayer = UiLayerPriority.Hotbar;

		foreach ( var id in activeWindows )
		{
			var def = UiWindowRegistry.Get( id );
			if ( def.Id == UiWindowId.None ) continue;
			if ( def.Group == UiWindowGroup.TransientFeedback ) continue;
			if ( def.Layer < bestLayer ) continue;

			bestLayer = def.Layer;
			best = def.InputContext;
		}

		if ( UiState.LocalAnswerPromptActive || UiState.PredictionInputOpen )
			return UiInputContext.HudInteractive;

		var phase = MatchManager.Instance?.Phase ?? MatchPhase.Lobby;
		if ( best == UiInputContext.Gameplay && phase is MatchPhase.Lobby or MatchPhase.PostMatch )
			return UiInputContext.Menu;

		return best;
	}
}
