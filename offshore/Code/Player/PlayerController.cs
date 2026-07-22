namespace Offshore;

public sealed class PlayerController
{
	public float DockX { get; set; } = 80f;
	public float Facing { get; private set; } = 1f;
	public string Anim { get; set; } = "idle";
	public bool OnBoat { get; set; }

	/// <summary>World X of the bait-shop front (left side of the combined shop+dock plate).</summary>
	public const float ShopX = -40f;
	public const float MinX = -200f;
	/// <summary>Walk far enough right to reach the moored boat.</summary>
	public const float MaxX = BoatController.DockX + 12f;

	public void TickDock( float dt, float move )
	{
		if ( OnBoat ) return;
		if ( Math.Abs( move ) > 0.01f )
		{
			Facing = Math.Sign( move );
			DockX = Math.Clamp( DockX + move * 110f * dt, MinX, MaxX );
			Anim = "walk";
		}
		else if ( Anim is "walk" or "idle" )
		{
			Anim = "idle";
		}
	}

	public InteractPrompt Prompt( GamePhase phase, bool hasCatch, bool nearBoat, bool nearShop, bool nearSell )
	{
		if ( phase == GamePhase.Sailing ) return InteractPrompt.DockBoat;
		if ( phase is GamePhase.WaitingBite ) return InteractPrompt.Hook;
		if ( phase is GamePhase.Reeling or GamePhase.Hooking ) return InteractPrompt.Reel;
		if ( phase is GamePhase.Casting ) return InteractPrompt.StopCast;
		if ( OnBoat ) return InteractPrompt.None;
		if ( nearShop ) return InteractPrompt.EnterShop;
		if ( nearBoat ) return InteractPrompt.BoardBoat;
		if ( nearSell && hasCatch ) return InteractPrompt.SellCatch;
		return InteractPrompt.Cast;
	}

	/// <summary>Left pier / shop porch — clear of the mid-dock cast zone and boat berth.</summary>
	public bool NearShop => !OnBoat && DockX <= ShopX + 90f;
	/// <summary>Standing near the berth — only meaningful when a boat is owned.</summary>
	public bool NearBoatBerth => !OnBoat && Math.Abs( DockX - BoatController.DockX ) < 90f;
}
