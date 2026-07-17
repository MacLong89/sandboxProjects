namespace Offshore;

/// <summary>
/// Validates and applies session-state transitions. Idempotent: same-state requests succeed.
/// Pause is handled separately so gameplay states remain restoreable.
/// </summary>
public sealed class OffshoreStateMachine
{
	public FishingSessionState State { get; private set; } = FishingSessionState.Boot;
	public FishingSessionState StateBeforePause { get; private set; } = FishingSessionState.DockIdle;

	public bool IsMenuOpen =>
		State is FishingSessionState.UpgradeMenu
			or FishingSessionState.EquipmentMenu
			or FishingSessionState.BoatMenu
			or FishingSessionState.JournalMenu
			or FishingSessionState.LocationSelection
			or FishingSessionState.Selling
			or FishingSessionState.Tournament
			or FishingSessionState.Traveling;

	public bool IsFishingActive =>
		State is FishingSessionState.AimingCast
			or FishingSessionState.ChargingCast
			or FishingSessionState.Casting
			or FishingSessionState.HookInWater
			or FishingSessionState.WaitingForBite
			or FishingSessionState.BiteWindow
			or FishingSessionState.FishHooked
			or FishingSessionState.Reeling;

	public bool BlocksGameplayInput =>
		State is FishingSessionState.Paused || IsMenuOpen;

	public bool TrySet( FishingSessionState next )
	{
		if ( State == FishingSessionState.Paused && next != FishingSessionState.Paused )
			return false;

		if ( State == next )
			return true;

		if ( !IsAllowed( State, next ) )
		{
			Log.Warning( $"[Offshore] Blocked state {State} → {next}" );
			return false;
		}

		State = next;
		return true;
	}

	public bool TryEnterPause()
	{
		if ( State == FishingSessionState.Paused )
			return true;

		if ( State == FishingSessionState.Casting )
			return false;

		// Allow pause during bite/reel; block only while the cast projectile is in flight.
		if ( State is FishingSessionState.Boot or FishingSessionState.Loading )
			return false;

		StateBeforePause = State;
		State = FishingSessionState.Paused;
		return true;
	}

	public bool TryExitPause()
	{
		if ( State != FishingSessionState.Paused )
			return false;

		State = StateBeforePause;
		return true;
	}

	/// <summary>Bypass transition table for menu open/close and outcome → sell.</summary>
	public bool ForceSet( FishingSessionState next )
	{
		if ( State == FishingSessionState.Paused && next != FishingSessionState.Paused )
			return false;

		State = next;
		return true;
	}

	private static bool IsAllowed( FishingSessionState from, FishingSessionState to ) =>
		(from, to) switch
		{
			(FishingSessionState.Boot, FishingSessionState.Loading) => true,
			(FishingSessionState.Boot, FishingSessionState.DockIdle) => true,
			(FishingSessionState.Loading, FishingSessionState.DockIdle) => true,

			(FishingSessionState.DockIdle, FishingSessionState.AimingCast) => true,
			(FishingSessionState.AimingCast, FishingSessionState.ChargingCast) => true,
			(FishingSessionState.AimingCast, FishingSessionState.DockIdle) => true,
			(FishingSessionState.ChargingCast, FishingSessionState.AimingCast) => true,
			(FishingSessionState.ChargingCast, FishingSessionState.Casting) => true,
			(FishingSessionState.Casting, FishingSessionState.HookInWater) => true,
			(FishingSessionState.Casting, FishingSessionState.CastFailed) => true,
			(FishingSessionState.HookInWater, FishingSessionState.WaitingForBite) => true,
			(FishingSessionState.HookInWater, FishingSessionState.AimingCast) => true,
			(FishingSessionState.CastFailed, FishingSessionState.AimingCast) => true,
			(FishingSessionState.CastFailed, FishingSessionState.DockIdle) => true,

			(FishingSessionState.WaitingForBite, FishingSessionState.BiteWindow) => true,
			(FishingSessionState.WaitingForBite, FishingSessionState.AimingCast) => true,
			(FishingSessionState.BiteWindow, FishingSessionState.FishHooked) => true,
			(FishingSessionState.BiteWindow, FishingSessionState.Reeling) => true,
			(FishingSessionState.BiteWindow, FishingSessionState.FishEscaped) => true,
			(FishingSessionState.FishHooked, FishingSessionState.Reeling) => true,
			(FishingSessionState.FishHooked, FishingSessionState.FishEscaped) => true,
			(FishingSessionState.Reeling, FishingSessionState.CatchSuccess) => true,
			(FishingSessionState.Reeling, FishingSessionState.FishEscaped) => true,
			(FishingSessionState.AimingCast, FishingSessionState.FishingFromBoat) => true,
			(FishingSessionState.FishingFromBoat, FishingSessionState.AimingCast) => true,
			(FishingSessionState.FishingFromBoat, FishingSessionState.ChargingCast) => true,
			(FishingSessionState.CatchSuccess, FishingSessionState.AimingCast) => true,
			(FishingSessionState.CatchSuccess, FishingSessionState.CoolerFull) => true,
			(FishingSessionState.CatchSuccess, FishingSessionState.Selling) => true,
			(FishingSessionState.FishEscaped, FishingSessionState.AimingCast) => true,
			(FishingSessionState.CoolerFull, FishingSessionState.AimingCast) => true,
			(FishingSessionState.CoolerFull, FishingSessionState.Selling) => true,
			(FishingSessionState.CoolerFull, FishingSessionState.UpgradeMenu) => true,

			(FishingSessionState.DockIdle, FishingSessionState.UpgradeMenu) => true,
			(FishingSessionState.DockIdle, FishingSessionState.EquipmentMenu) => true,
			(FishingSessionState.DockIdle, FishingSessionState.BoatMenu) => true,
			(FishingSessionState.DockIdle, FishingSessionState.JournalMenu) => true,
			(FishingSessionState.DockIdle, FishingSessionState.LocationSelection) => true,
			(FishingSessionState.DockIdle, FishingSessionState.Selling) => true,
			(FishingSessionState.DockIdle, FishingSessionState.Tournament) => true,
			(FishingSessionState.AimingCast, FishingSessionState.UpgradeMenu) => true,
			(FishingSessionState.AimingCast, FishingSessionState.EquipmentMenu) => true,
			(FishingSessionState.AimingCast, FishingSessionState.BoatMenu) => true,
			(FishingSessionState.AimingCast, FishingSessionState.JournalMenu) => true,
			(FishingSessionState.AimingCast, FishingSessionState.LocationSelection) => true,
			(FishingSessionState.AimingCast, FishingSessionState.Selling) => true,
			(FishingSessionState.AimingCast, FishingSessionState.Tournament) => true,

			(FishingSessionState.UpgradeMenu, FishingSessionState.DockIdle) => true,
			(FishingSessionState.UpgradeMenu, FishingSessionState.AimingCast) => true,
			(FishingSessionState.UpgradeMenu, FishingSessionState.Selling) => true,
			(FishingSessionState.UpgradeMenu, FishingSessionState.BoatMenu) => true,
			(FishingSessionState.UpgradeMenu, FishingSessionState.LocationSelection) => true,
			(FishingSessionState.UpgradeMenu, FishingSessionState.JournalMenu) => true,
			(FishingSessionState.UpgradeMenu, FishingSessionState.EquipmentMenu) => true,
			(FishingSessionState.UpgradeMenu, FishingSessionState.Tournament) => true,
			(FishingSessionState.EquipmentMenu, FishingSessionState.DockIdle) => true,
			(FishingSessionState.EquipmentMenu, FishingSessionState.AimingCast) => true,
			(FishingSessionState.EquipmentMenu, FishingSessionState.UpgradeMenu) => true,
			(FishingSessionState.BoatMenu, FishingSessionState.DockIdle) => true,
			(FishingSessionState.BoatMenu, FishingSessionState.AimingCast) => true,
			(FishingSessionState.BoatMenu, FishingSessionState.UpgradeMenu) => true,
			(FishingSessionState.JournalMenu, FishingSessionState.DockIdle) => true,
			(FishingSessionState.JournalMenu, FishingSessionState.AimingCast) => true,
			(FishingSessionState.JournalMenu, FishingSessionState.UpgradeMenu) => true,
			(FishingSessionState.LocationSelection, FishingSessionState.DockIdle) => true,
			(FishingSessionState.LocationSelection, FishingSessionState.AimingCast) => true,
			(FishingSessionState.LocationSelection, FishingSessionState.UpgradeMenu) => true,
			(FishingSessionState.LocationSelection, FishingSessionState.Traveling) => true,
			(FishingSessionState.Traveling, FishingSessionState.AimingCast) => true,
			(FishingSessionState.Selling, FishingSessionState.DockIdle) => true,
			(FishingSessionState.Selling, FishingSessionState.AimingCast) => true,
			(FishingSessionState.Selling, FishingSessionState.UpgradeMenu) => true,
			(FishingSessionState.Tournament, FishingSessionState.DockIdle) => true,
			(FishingSessionState.Tournament, FishingSessionState.AimingCast) => true,
			(FishingSessionState.Tournament, FishingSessionState.UpgradeMenu) => true,

			_ => false
		};
}
