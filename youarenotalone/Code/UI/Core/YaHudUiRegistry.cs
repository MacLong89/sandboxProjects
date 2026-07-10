using Sandbox.UI;

namespace Sandbox;

/// <summary>Registers every HUD surface with the local <see cref="YaUiManager"/>.</summary>
public static class YaHudUiRegistry
{
	public static void RegisterAll( YaPlayerHud hud, YaUiManager manager )
	{
		manager.SetModalScrim( hud.ModalScrimRoot );

		Register( manager, YaUiSurfaceId.CriticalDeathOverlay, hud.DeathOverlayPanel, YaUiLayer.Critical,
			ctx => ctx.ShowDeathOverlay, modal: true, requiresMouse: true, managesOpacity: true );

		Register( manager, YaUiSurfaceId.FullscreenRoundVictory, hud.RoundVictoryRoot, YaUiLayer.Fullscreen,
			ctx => ctx.ShowRoundVictory, modal: true );

		Register( manager, YaUiSurfaceId.FullscreenPracticeChoice, hud.PracticeChoiceRoot, YaUiLayer.Fullscreen,
			ctx => ctx.ShowPracticeChoice, modal: true, requiresMouse: true );

		Register( manager, YaUiSurfaceId.FullscreenControlsTutorial, hud.ControlsTutorialPanel, YaUiLayer.Fullscreen,
			ctx => ctx.ShowControlsTutorial, modal: true, requiresMouse: true );

		Register( manager, YaUiSurfaceId.FullscreenScoreboard, hud.ScoreboardRoot, YaUiLayer.Fullscreen,
			ctx => ctx.ShowScoreboard, modal: true, requiresMouse: true );

		Register( manager, YaUiSurfaceId.HudTopObjective, hud.TopObjectiveRoot, YaUiLayer.Hud,
			ctx => ctx.ShowHudTopObjective );

		Register( manager, YaUiSurfaceId.HudTopLeftHints, hud.TopLeftHintsRoot, YaUiLayer.Hud,
			ctx => ctx.ShowHudTopLeftHints );

		Register( manager, YaUiSurfaceId.HudCombat, hud.CombatHudRoot, YaUiLayer.Hud,
			ctx => ctx.ShowHudCombat,
			onVisibilityChanged: visible => hud.SetCombatDockVisible( visible ) );

		Register( manager, YaUiSurfaceId.HudCrosshair, hud.CrosshairRoot, YaUiLayer.Hud,
			ctx => ctx.ShowCrosshair, managesOpacity: false );

		Register( manager, YaUiSurfaceId.PassiveParanoia, hud.ParanoiaOverlayRoot, YaUiLayer.PassiveOverlay,
			ctx => ctx.ShowParanoiaOverlays, managesOpacity: false );

		Register( manager, YaUiSurfaceId.PassiveDamage, hud.DamageFeedbackRoot, YaUiLayer.PassiveOverlay,
			ctx => ctx.ShowDamageFeedback, managesOpacity: false );

		Register( manager, YaUiSurfaceId.NotificationEventFeed, hud.KillFeedPanel, YaUiLayer.Notification,
			ctx => ctx.ShowEventFeed );

		Register( manager, YaUiSurfaceId.NotificationRoundStart, hud.RoundStartAnnouncementRoot, YaUiLayer.Notification,
			ctx => ctx.ShowRoundStartAnnouncement );

		Register( manager, YaUiSurfaceId.NotificationLobbyHint, hud.LobbySoloHintWrap, YaUiLayer.Notification,
			ctx => ctx.ShowLobbySoloHint );

		Register( manager, YaUiSurfaceId.NotificationFloatingStack, hud.FloatingMessageRoot, YaUiLayer.Notification,
			ctx => ctx.ShowFloatingMessages );
	}

	static void Register(
		YaUiManager manager,
		YaUiSurfaceId id,
		Panel panel,
		YaUiLayer layer,
		Func<YaUiFrameContext, bool> wantsVisible,
		bool modal = false,
		bool requiresMouse = false,
		bool managesOpacity = true,
		Action<bool> onVisibilityChanged = null )
	{
		manager.Register( new YaUiSurfaceBinding
		{
			Request = new YaUiRequest
			{
				Surface = id,
				Layer = layer,
				Modal = modal,
				InputContext = YaUiCompatibility.InputContextFor( id ),
				RequiresMouse = requiresMouse,
				SuppressWhenCombat = id is YaUiSurfaceId.NotificationLobbyHint or YaUiSurfaceId.NotificationRoundStart
			},
			Panel = panel,
			WantsVisible = wantsVisible,
			OnVisibilityChanged = onVisibilityChanged,
			ManagesOpacity = managesOpacity
		} );
	}
}
