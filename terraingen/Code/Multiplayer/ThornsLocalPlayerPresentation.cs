namespace Terraingen.Multiplayer;

using Sandbox;
using Terraingen.Player;
using Terraingen.UI;
using Terraingen.UI.Core;

/// <summary>Local pawn presentation readiness — HUD, snapshot, mounted assets, boot gate.</summary>
public static class ThornsLocalPlayerPresentation
{
	public static bool IsHudReady()
	{
		var menuHost = ThornsMenuHost.Instance;
		if ( menuHost is null || !menuHost.IsValid || !menuHost.IsUiBuilt )
			return false;

		return menuHost.Panel is { IsValid: true }
		       && ThornsGameplayUiStyles.IsGameplayRootReady( menuHost.Panel );
	}

	public static bool IsSnapshotReady() => ThornsUiClientState.HasSnapshot;

	public static bool IsBootReady() => !ThornsWorldBootGate.BlocksLocalOwnerPresentation;

	public static bool IsFullyReady() =>
		IsBootReady()
		&& IsHudReady()
		&& IsSnapshotReady()
		&& !ThornsLocalHostSpawnCoordinator.IsDeferredPending;

	public static void TickAssetBootstrap()
	{
		var menuHost = ThornsMenuHost.Instance;
		if ( menuHost is { Panel.IsValid: true } && !ThornsGameplayUiStyles.IsGameplayRootReady( menuHost.Panel ) )
			ThornsGameplayUiStyles.LoadGameplayRoot( menuHost.Panel );

		if ( !ThornsIconCache.IsGameplayIconsWarmed )
			ThornsIconCache.WarmGameplayIcons();
	}

	public static void EnsureLocalReady( Scene scene, GameObject player )
	{
		if ( scene is null || !scene.IsValid || !player.IsValid() )
			return;

		TickAssetBootstrap();
		ThornsMenuHost.ForceGameplayState();

		var menuHost = ThornsMenuHost.Instance;
		if ( menuHost is not null && menuHost.IsValid )
			menuHost.EnsureUiReady();
		else
			ThornsGameplayUiHost.Instance?.EnsureScreenUiForDeferredHud();

		ThornsGameplayUiHost.RefreshScreenPanelCamera( scene );

		var gameplay = player.Components.Get<ThornsPlayerGameplay>();
		if ( gameplay.IsValid() && !ThornsUiClientState.HasSnapshot )
			gameplay.RefreshMenuSnapshot();

		if ( ThornsUiClientState.HasSnapshot )
			player.Components.Get<ThornsFpPresentation>()?.RefreshFromActiveHotbar();
	}
}
