namespace DeepDive;

/// <summary>Idempotent phase transitions with guards.</summary>
public sealed class DeepDiveGameStateMachine
{
	public GamePhase Phase { get; private set; } = GamePhase.Boot;
	public bool DiveEnded { get; private set; }
	public GamePhase PhaseBeforePause { get; private set; } = GamePhase.SurfaceIdle;

	public bool IsUiBlocking => Phase is
		GamePhase.Paused or
		GamePhase.DiveSuccess or
		GamePhase.DiveFailed or
		GamePhase.UpgradeMenu or
		GamePhase.EquipmentMenu or
		GamePhase.DiverHub or
		GamePhase.JournalMenu or
		GamePhase.Selling or
		GamePhase.PreparingDive or
		GamePhase.ReturningToSurface;

	public bool CanStartDive => Phase == GamePhase.SurfaceIdle;
	public bool IsDivingActive => Phase == GamePhase.Diving;
	public bool IsSurfaceCursor => Phase is GamePhase.SurfaceIdle or GamePhase.DiverHub or GamePhase.UpgradeMenu or GamePhase.JournalMenu;

	public void EnterBoot() => Force( GamePhase.Boot );
	public void EnterSurfaceIdle() => Force( GamePhase.SurfaceIdle );

	public bool TryPrepareDive()
	{
		if ( !CanStartDive )
			return false;

		Phase = GamePhase.PreparingDive;
		DiveEnded = false;
		return true;
	}

	public bool TryBeginDiving()
	{
		if ( Phase != GamePhase.PreparingDive && Phase != GamePhase.SurfaceIdle )
			return false;

		Phase = GamePhase.Diving;
		DiveEnded = false;
		return true;
	}

	public bool TryCompleteSuccess()
	{
		if ( Phase != GamePhase.Diving || DiveEnded )
			return false;

		DiveEnded = true;
		Phase = GamePhase.DiveSuccess;
		return true;
	}

	public bool TryFail()
	{
		if ( Phase != GamePhase.Diving || DiveEnded )
			return false;

		DiveEnded = true;
		Phase = GamePhase.DiveFailed;
		return true;
	}

	public bool TryPause()
	{
		if ( Phase != GamePhase.Diving )
			return false;

		PhaseBeforePause = Phase;
		Phase = GamePhase.Paused;
		return true;
	}

	public bool TryResume()
	{
		if ( Phase != GamePhase.Paused )
			return false;

		Phase = PhaseBeforePause;
		return true;
	}

	public bool TryOpenDiverHub()
	{
		if ( Phase != GamePhase.SurfaceIdle )
			return false;
		Phase = GamePhase.DiverHub;
		return true;
	}

	public bool TryOpenUpgradeMenu()
	{
		// Legacy shortcut — open the unified Diver Hub.
		return TryOpenDiverHub();
	}

	public bool TryOpenJournal()
	{
		if ( Phase != GamePhase.SurfaceIdle )
			return false;
		Phase = GamePhase.JournalMenu;
		return true;
	}

	public bool TryCloseMenuToSurface()
	{
		if ( Phase is not (GamePhase.UpgradeMenu or GamePhase.JournalMenu or GamePhase.EquipmentMenu or GamePhase.DiverHub or GamePhase.Selling) )
			return false;
		Phase = GamePhase.SurfaceIdle;
		return true;
	}

	public bool TryReturnToSurfaceIdle()
	{
		if ( Phase is not (GamePhase.DiveSuccess or GamePhase.DiveFailed or GamePhase.ReturningToSurface or GamePhase.Paused) )
		{
			if ( Phase == GamePhase.SurfaceIdle )
				return true;
			return false;
		}

		DiveEnded = false;
		Phase = GamePhase.SurfaceIdle;
		return true;
	}

	private void Force( GamePhase phase )
	{
		Phase = phase;
		if ( phase == GamePhase.SurfaceIdle || phase == GamePhase.Boot )
			DiveEnded = false;
	}
}
