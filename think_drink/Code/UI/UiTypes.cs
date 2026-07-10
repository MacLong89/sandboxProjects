namespace ThinkDrink.UI;

/// <summary>Strict render hierarchy — higher values always render above lower values.</summary>
public enum UiLayerPriority
{
	Hotbar = 10,
	Hud = 20,
	Notification = 30,
	Tooltip = 40,
	JournalPanel = 50,
	Dialogue = 60,
	CharacterPanel = 70,
	FullscreenMenu = 80,
	Confirmation = 90,
	CriticalAlert = 100
}

public enum UiWindowId
{
	None,
	LobbyShell,
	MatchHudShell,
	PostMatchShell,
	Leaderboard,
	Profile,
	Achievements,
	Challenges,
	Settings,
	Onboarding,
	BoardTuner,
	FlashFeedback,
	LevelUpBanner,
	BuzzerPrompt
}

public enum UiWindowGroup
{
	None,
	Shell,
	OverlayPanel,
	DevTool,
	ForcedModal,
	TransientFeedback
}

public enum UiInputContext
{
	Gameplay,
	HudInteractive,
	Menu,
	Modal,
	DevTool,
	Blocked
}

public enum UiScreenRegion
{
	FullScreen,
	TopHud,
	BottomControls,
	CenterModal,
	TopNotification,
	CenterFeedback,
	BottomTooltip,
	DevCorner
}

public enum UiRequestAction
{
	Open,
	Close,
	Toggle,
	PushNotification
}

public enum UiNotificationKind
{
	Toast,
	Flash,
	LevelUp
}

public readonly struct UiRequest
{
	public UiRequest( UiWindowId window, UiRequestAction action = UiRequestAction.Open, string payload = "" )
	{
		Window = window;
		Action = action;
		Payload = payload;
	}

	public UiWindowId Window { get; }
	public UiRequestAction Action { get; }
	public string Payload { get; }

	public static UiRequest Open( UiWindowId window ) => new( window, UiRequestAction.Open );
	public static UiRequest Close( UiWindowId window ) => new( window, UiRequestAction.Close );
	public static UiRequest Toggle( UiWindowId window ) => new( window, UiRequestAction.Toggle );
}

public readonly struct UiWindowDefinition
{
	public UiWindowDefinition(
		UiWindowId id,
		UiLayerPriority layer,
		UiInputContext inputContext,
		UiScreenRegion region,
		UiWindowGroup group,
		bool isModal,
		bool blocksLowerInteraction,
		bool suppressesBackgroundHud,
		bool allowedDuringActiveRound )
	{
		Id = id;
		Layer = layer;
		InputContext = inputContext;
		Region = region;
		Group = group;
		IsModal = isModal;
		BlocksLowerInteraction = blocksLowerInteraction;
		SuppressesBackgroundHud = suppressesBackgroundHud;
		AllowedDuringActiveRound = allowedDuringActiveRound;
	}

	public UiWindowId Id { get; }
	public UiLayerPriority Layer { get; }
	public UiInputContext InputContext { get; }
	public UiScreenRegion Region { get; }
	public UiWindowGroup Group { get; }
	public bool IsModal { get; }
	public bool BlocksLowerInteraction { get; }
	public bool SuppressesBackgroundHud { get; }
	public bool AllowedDuringActiveRound { get; }
}

public readonly struct UiToastEntry
{
	public UiToastEntry( string message, bool positive, int count = 1 )
	{
		Message = message;
		Positive = positive;
		Count = count;
	}

	public string Message { get; }
	public bool Positive { get; }
	public int Count { get; }

	public string DisplayText => Count > 1 ? $"{Message} (×{Count})" : Message;
}

public readonly struct UiTooltipState
{
	public UiTooltipState( string text, bool visible )
	{
		Text = text;
		Visible = visible;
	}

	public string Text { get; }
	public bool Visible { get; }
}

public static class UiAnimation
{
	public const float FadeSeconds = 0.2f;
	public const float ScaleSeconds = 0.15f;
	public const float SlideSeconds = 0.25f;
}
