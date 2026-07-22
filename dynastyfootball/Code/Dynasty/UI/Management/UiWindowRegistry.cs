namespace Dynasty.UI.Management;

/// <summary>
/// Static registry of all UI window metadata. Single source of truth for layering and behavior.
/// </summary>
public static class UiWindowRegistry
{
	static readonly Dictionary<UiWindowType, UiWindowDefinition> _definitions = Build();

	public static UiWindowDefinition Get( UiWindowType type )
		=> _definitions.TryGetValue( type, out var def ) ? def : null;

	public static string GetLayerId( UiWindowType type ) => Get( type )?.LayerId ?? $"window.{type}";

	static Dictionary<UiWindowType, UiWindowDefinition> Build() => new()
	{
		[UiWindowType.GameViewer] = new UiWindowDefinition
		{
			Type = UiWindowType.GameViewer,
			LayerId = UiModalIds.GameViewer,
			Priority = DynastyUiPriority.Fullscreen,
			Group = UiWindowGroup.FullscreenExperience,
			Region = UiScreenRegion.FullScreen,
			InputContext = DynastyUiInputContext.Modal,
			DismissOnBackdrop = true,
			AllowsTooltips = false
		},
		[UiWindowType.WeekSummary] = new UiWindowDefinition
		{
			Type = UiWindowType.WeekSummary,
			LayerId = UiModalIds.WeekSummary,
			Priority = DynastyUiPriority.Fullscreen,
			Group = UiWindowGroup.FullscreenExperience,
			Region = UiScreenRegion.CenterModal,
			InputContext = DynastyUiInputContext.Modal,
			DismissOnBackdrop = false
		},
		[UiWindowType.PostGameCelebration] = new UiWindowDefinition
		{
			Type = UiWindowType.PostGameCelebration,
			LayerId = UiModalIds.PostGameCelebration,
			Priority = DynastyUiPriority.Critical,
			Group = UiWindowGroup.CriticalTakeover,
			Region = UiScreenRegion.CenterModal,
			InputContext = DynastyUiInputContext.Critical,
			DismissOnEscape = false,
			DismissOnBackdrop = false,
			AllowsNotifications = false,
			AllowsTooltips = false
		},
		[UiWindowType.DraftCeremony] = new UiWindowDefinition
		{
			Type = UiWindowType.DraftCeremony,
			LayerId = UiModalIds.DraftCeremony,
			Priority = DynastyUiPriority.Critical,
			Group = UiWindowGroup.CriticalTakeover,
			Region = UiScreenRegion.CenterModal,
			InputContext = DynastyUiInputContext.Critical,
			DismissOnEscape = false,
			DismissOnBackdrop = false,
			AllowsNotifications = false,
			AllowsTooltips = false
		},
		[UiWindowType.FormationPicker] = new UiWindowDefinition
		{
			Type = UiWindowType.FormationPicker,
			LayerId = UiModalIds.FormationPicker,
			Priority = DynastyUiPriority.Screen,
			Group = UiWindowGroup.StandardDialog,
			Region = UiScreenRegion.CenterModal,
			InputContext = DynastyUiInputContext.Modal,
			DismissOnBackdrop = true
		},
		[UiWindowType.TeamReleaseConfirm] = new UiWindowDefinition
		{
			Type = UiWindowType.TeamReleaseConfirm,
			LayerId = UiModalIds.TeamCut,
			Priority = DynastyUiPriority.Confirmation,
			Group = UiWindowGroup.StandardDialog,
			Region = UiScreenRegion.CenterModal,
			InputContext = DynastyUiInputContext.Modal,
			DismissOnBackdrop = true
		},
		[UiWindowType.TeamExtendDialog] = new UiWindowDefinition
		{
			Type = UiWindowType.TeamExtendDialog,
			LayerId = UiModalIds.TeamExtend,
			Priority = DynastyUiPriority.Confirmation,
			Group = UiWindowGroup.StandardDialog,
			Region = UiScreenRegion.CenterModal,
			InputContext = DynastyUiInputContext.Modal,
			DismissOnBackdrop = true
		},
		[UiWindowType.TeamTradeDialog] = new UiWindowDefinition
		{
			Type = UiWindowType.TeamTradeDialog,
			LayerId = UiModalIds.TeamTrade,
			Priority = DynastyUiPriority.Confirmation,
			Group = UiWindowGroup.StandardDialog,
			Region = UiScreenRegion.CenterModal,
			InputContext = DynastyUiInputContext.Modal,
			DismissOnBackdrop = true
		},
		[UiWindowType.MainMenuConfirm] = new UiWindowDefinition
		{
			Type = UiWindowType.MainMenuConfirm,
			LayerId = UiModalIds.MainMenuConfirm,
			Priority = DynastyUiPriority.Confirmation,
			Group = UiWindowGroup.StandardDialog,
			Region = UiScreenRegion.CenterModal,
			InputContext = DynastyUiInputContext.Menu
		},
		[UiWindowType.AdvanceTimeMenu] = new UiWindowDefinition
		{
			Type = UiWindowType.AdvanceTimeMenu,
			LayerId = UiModalIds.AdvanceTimeMenu,
			Priority = DynastyUiPriority.Confirmation,
			Group = UiWindowGroup.StandardDialog,
			Region = UiScreenRegion.CenterModal,
			InputContext = DynastyUiInputContext.Modal,
			DismissOnBackdrop = true
		},
		[UiWindowType.TeamProfile] = new UiWindowDefinition
		{
			Type = UiWindowType.TeamProfile,
			LayerId = UiModalIds.TeamProfile,
			Priority = DynastyUiPriority.Screen,
			Group = UiWindowGroup.StandardDialog,
			Region = UiScreenRegion.CenterModal,
			InputContext = DynastyUiInputContext.Modal,
			DismissOnBackdrop = true
		},
		[UiWindowType.SessionSummary] = new UiWindowDefinition
		{
			Type = UiWindowType.SessionSummary,
			LayerId = UiModalIds.SessionSummary,
			Priority = DynastyUiPriority.Confirmation,
			Group = UiWindowGroup.StandardDialog,
			Region = UiScreenRegion.CenterModal,
			InputContext = DynastyUiInputContext.Modal,
			DismissOnBackdrop = false
		},
		[UiWindowType.FourthDownDecision] = new UiWindowDefinition
		{
			Type = UiWindowType.FourthDownDecision,
			LayerId = UiModalIds.FourthDownDecision,
			Priority = DynastyUiPriority.Confirmation,
			Group = UiWindowGroup.StandardDialog,
			Region = UiScreenRegion.CenterModal,
			InputContext = DynastyUiInputContext.Modal,
			DismissOnBackdrop = false
		},
		[UiWindowType.TutorialTip] = new UiWindowDefinition
		{
			Type = UiWindowType.TutorialTip,
			LayerId = "modal.tutorialTip",
			Priority = DynastyUiPriority.Confirmation,
			Group = UiWindowGroup.StandardDialog,
			Region = UiScreenRegion.CenterModal,
			InputContext = DynastyUiInputContext.Modal,
			DismissOnBackdrop = false,
			DismissOnEscape = false,
			IsModal = true,
			BlocksHudInput = true
		}
	};
}
