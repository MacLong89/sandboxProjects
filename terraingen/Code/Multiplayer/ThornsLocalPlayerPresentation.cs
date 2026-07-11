namespace Terraingen.Multiplayer;

using Sandbox;
using Terraingen.Player;
using Terraingen.TerrainGen;
using Terraingen.UI;
using Terraingen.UI.Core;
using Terraingen.UI.Menu;

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

	public static bool IsBootReady() =>
		ThornsWorldBootGate.IsLocalBootComplete
		|| ThornsTerrainBootstrap.Instance?.IsWorldApplied == true;

	public static bool IsFullyReady() =>
		IsBootReady()
		&& IsHudReady()
		&& IsSnapshotReady()
		&& !ThornsLocalHostSpawnCoordinator.IsDeferredPending;

	/// <summary>Enough to release join overlay and play — cosmetics may still stream in.</summary>
	public static bool IsMinimallyPlayable() =>
		IsBootReady() && IsHudReady() && IsSnapshotReady();

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
		{
			ThornsJoinFlowDebug.JoinWarn(
				$"EnsureLocalReady skipped — scene={( scene?.IsValid == true ? scene.Name : "invalid" )} player={( player.IsValid() ? player.Name : "invalid" )}" );
			return;
		}

		TickAssetBootstrap();
		ThornsMenuHost.ForceGameplayState();

		var menuHost = ThornsMenuHost.Instance;
		if ( menuHost is not null && menuHost.IsValid )
		{
			ThornsJoinFlowDebug.JoinInfo( $"EnsureLocalReady via menuHost.EnsureUiReady — {ThornsJoinFlowDebug.DescribeHud()}" );
			menuHost.EnsureUiReady();
		}
		else
		{
			ThornsJoinFlowDebug.JoinInfo(
				$"EnsureLocalReady menuHost missing — EnsureScreenUiForDeferredHud gameplayUiHost={( ThornsGameplayUiHost.Instance?.IsValid() == true )}" );
			ThornsGameplayUiHost.Instance?.EnsureScreenUiForDeferredHud();
		}

		ThornsGameplayUiHost.RefreshScreenPanelCamera( scene );

		var gameplay = player.Components.Get<ThornsPlayerGameplay>();
		if ( gameplay.IsValid() && !ThornsUiClientState.HasSnapshot )
		{
			ThornsJoinFlowDebug.JoinInfo( "EnsureLocalReady requesting menu snapshot (client/host refresh)." );
			gameplay.RefreshMenuSnapshot();
		}

		if ( ThornsUiClientState.HasSnapshot )
			player.Components.Get<ThornsFpPresentation>()?.RefreshFromActiveHotbar();

		ThornsJoinFlowDebug.JoinInfo(
			$"EnsureLocalReady done — boot={IsBootReady()} hud={IsHudReady()} snapshot={IsSnapshotReady()} {ThornsJoinFlowDebug.DescribeHud()}" );
	}
}
