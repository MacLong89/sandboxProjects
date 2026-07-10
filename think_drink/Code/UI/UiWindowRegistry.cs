namespace ThinkDrink.UI;

/// <summary>Data-driven window catalog — single source of truth for layer, input, and exclusivity metadata.</summary>
public static class UiWindowRegistry
{
	static readonly Dictionary<UiWindowId, UiWindowDefinition> _definitions = BuildDefinitions();

	public static UiWindowDefinition Get( UiWindowId id ) =>
		_definitions.TryGetValue( id, out var def ) ? def : default;

	public static IEnumerable<UiWindowDefinition> All => _definitions.Values;

	public static UiWindowId FromScreen( UiScreen screen ) => screen switch
	{
		UiScreen.Leaderboard => UiWindowId.Leaderboard,
		UiScreen.Profile => UiWindowId.Profile,
		UiScreen.Achievements => UiWindowId.Achievements,
		UiScreen.Challenges => UiWindowId.Challenges,
		UiScreen.Settings => UiWindowId.Settings,
		_ => UiWindowId.None
	};

	public static UiScreen ToScreen( UiWindowId id ) => id switch
	{
		UiWindowId.Leaderboard => UiScreen.Leaderboard,
		UiWindowId.Profile => UiScreen.Profile,
		UiWindowId.Achievements => UiScreen.Achievements,
		UiWindowId.Challenges => UiScreen.Challenges,
		UiWindowId.Settings => UiScreen.Settings,
		_ => UiScreen.Lobby
	};

	static Dictionary<UiWindowId, UiWindowDefinition> BuildDefinitions()
	{
		return new Dictionary<UiWindowId, UiWindowDefinition>
		{
			[UiWindowId.LobbyShell] = Def( UiWindowId.LobbyShell, UiLayerPriority.Hud, UiInputContext.Menu, UiScreenRegion.FullScreen, UiWindowGroup.Shell, false, false, false, false ),
			[UiWindowId.MatchHudShell] = Def( UiWindowId.MatchHudShell, UiLayerPriority.Hud, UiInputContext.Gameplay, UiScreenRegion.FullScreen, UiWindowGroup.Shell, false, false, false, true ),
			[UiWindowId.PostMatchShell] = Def( UiWindowId.PostMatchShell, UiLayerPriority.FullscreenMenu, UiInputContext.Menu, UiScreenRegion.FullScreen, UiWindowGroup.Shell, true, true, true, false ),

			[UiWindowId.Leaderboard] = Panel( UiWindowId.Leaderboard ),
			[UiWindowId.Profile] = Panel( UiWindowId.Profile ),
			[UiWindowId.Achievements] = Panel( UiWindowId.Achievements ),
			[UiWindowId.Challenges] = Panel( UiWindowId.Challenges ),
			[UiWindowId.Settings] = Panel( UiWindowId.Settings ),

			[UiWindowId.Onboarding] = Def( UiWindowId.Onboarding, UiLayerPriority.CriticalAlert, UiInputContext.Modal, UiScreenRegion.CenterModal, UiWindowGroup.ForcedModal, true, true, true, false ),
			[UiWindowId.BoardTuner] = Def( UiWindowId.BoardTuner, UiLayerPriority.Dialogue, UiInputContext.DevTool, UiScreenRegion.DevCorner, UiWindowGroup.DevTool, false, false, false, false ),

			[UiWindowId.FlashFeedback] = Def( UiWindowId.FlashFeedback, UiLayerPriority.CriticalAlert, UiInputContext.Gameplay, UiScreenRegion.CenterFeedback, UiWindowGroup.TransientFeedback, false, false, false, true ),
			[UiWindowId.LevelUpBanner] = Def( UiWindowId.LevelUpBanner, UiLayerPriority.Notification, UiInputContext.Gameplay, UiScreenRegion.TopNotification, UiWindowGroup.TransientFeedback, false, false, false, false ),
			[UiWindowId.BuzzerPrompt] = Def( UiWindowId.BuzzerPrompt, UiLayerPriority.Hud, UiInputContext.Gameplay, UiScreenRegion.CenterFeedback, UiWindowGroup.TransientFeedback, false, false, false, true )
		};
	}

	static UiWindowDefinition Panel( UiWindowId id ) =>
		Def( id, UiLayerPriority.JournalPanel, UiInputContext.Modal, UiScreenRegion.CenterModal, UiWindowGroup.OverlayPanel, true, true, true, false );

	static UiWindowDefinition Def(
		UiWindowId id,
		UiLayerPriority layer,
		UiInputContext inputContext,
		UiScreenRegion region,
		UiWindowGroup group,
		bool isModal,
		bool blocksLowerInteraction,
		bool suppressesBackgroundHud,
		bool allowedDuringActiveRound ) =>
		new( id, layer, inputContext, region, group, isModal, blocksLowerInteraction, suppressesBackgroundHud, allowedDuringActiveRound );
}
