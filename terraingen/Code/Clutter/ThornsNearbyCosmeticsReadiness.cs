namespace Terraingen.Clutter;

using Terraingen.Foliage;
using Terraingen.Minerals;
using Terraingen.Player;
using Terraingen.UI;
using Terraingen.UI.Core;
using Terraingen.UI.Menu;

/// <summary>Holds local player on a loading overlay while nearby cosmetics stream in.</summary>
public static class ThornsNearbyCosmeticsReadiness
{
	const float MinHoldSeconds = 2f;
	const float MaxWaitSeconds = 10f;

	static bool _waiting;
	static double _startedRealtime;
	static GameObject _heldPlayer;

	public static bool IsWaiting => _waiting;

	public static void Begin( GameObject localPlayer )
	{
		_waiting = true;
		_startedRealtime = Time.Now;
		_heldPlayer = localPlayer;

		if ( localPlayer.IsValid() )
			ThornsPlayerLocomotion.SetOverlayInputBlocked( localPlayer, blocked: true );

		UpdateProgressMessage();
	}

	public static void Cancel()
	{
		if ( _heldPlayer.IsValid() )
			ThornsPlayerLocomotion.SetOverlayInputBlocked( _heldPlayer, blocked: false );

		_waiting = false;
		_heldPlayer = null;
	}

	public static bool TryComplete( Scene scene )
	{
		if ( !_waiting )
			return true;

		UpdateProgressMessage();

		var elapsed = Time.Now - _startedRealtime;
		if ( elapsed < MinHoldSeconds )
			return false;

		if ( elapsed >= MaxWaitSeconds || AreSystemsIdle( scene ) )
		{
			var reason = elapsed >= MaxWaitSeconds ? "timeout" : "idle";
			Log.Info( $"[Thorns Clutter] Spawn hold released ({reason}, {elapsed:F1}s)." );
			Cancel();
			return true;
		}

		return false;
	}

	static void UpdateProgressMessage()
	{
		if ( !_waiting )
			return;

		// Join handoff already released input — stream cosmetics without reopening the join overlay.
		if ( ThornsUiManager.ActiveContext == ThornsUiManager.UiContext.Gameplay )
			return;

		ThornsMenuJoinFlow.SetProgressMessage( DescribeBlockingWork() );
	}

	static string DescribeBlockingWork()
	{
		var scene = Game.ActiveScene;
		if ( scene is null || !scene.IsValid )
			return "Loading world around you...";

		var clutter = scene.GetAllComponents<ThornsClutterFoundation>().FirstOrDefault();
		if ( clutter is { IsValid: true, PendingChunkBuildCount: > 0 } )
			return "Loading ground cover...";

		if ( clutter is { IsValid: true, ActiveRevealCount: > 0 } )
			return "Settling ground cover...";

		var foliage = scene.GetAllComponents<ThornsFoliageFoundation>().FirstOrDefault();
		if ( foliage is { IsValid: true } && foliage.IsPopulatingNearPlayer )
			return "Growing nearby foliage...";

		var minerals = scene.GetAllComponents<ThornsMineralFoundation>().FirstOrDefault();
		if ( minerals is { IsValid: true } && minerals.IsPopulatingNearPlayer )
			return "Scattering nearby resources...";

		if ( minerals is { IsValid: true } && minerals.IsReadyForPlayerPocket && !minerals.HasPlayerPocket )
			return "Scattering nearby resources...";

		return "Loading world around you...";
	}

	static bool AreSystemsIdle( Scene scene )
	{
		if ( scene is null || !scene.IsValid )
			return true;

		var clutter = scene.GetAllComponents<ThornsClutterFoundation>().FirstOrDefault();
		if ( clutter is { IsValid: true } )
		{
			if ( clutter.PendingChunkBuildCount > 0 )
				return false;

			if ( !clutter.IsNearbyStreamingSettled )
				return false;
		}

		var grass = scene.GetAllComponents<ClientGrassRenderer>().FirstOrDefault();
		if ( grass is { IsValid: true, IsGpuStreamingActive: true } && grass.PendingTileCount > 0 )
			return false;

		var foliage = scene.GetAllComponents<ThornsFoliageFoundation>().FirstOrDefault();
		if ( foliage is { IsValid: true } && foliage.IsPopulatingNearPlayer )
			return false;

		var minerals = scene.GetAllComponents<ThornsMineralFoundation>().FirstOrDefault();
		if ( minerals is { IsValid: true } && minerals.IsPopulatingNearPlayer )
			return false;

		if ( minerals is { IsValid: true } && minerals.IsReadyForPlayerPocket && !minerals.HasPlayerPocket )
			return false;

		return true;
	}
}
