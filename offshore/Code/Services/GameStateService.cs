namespace Offshore;

/// <summary>Exclusive phase machine — only one major gameplay/menu state at a time.</summary>
public sealed class GameStateService
{
	public GamePhase Phase { get; private set; } = GamePhase.MainMenu;
	public GamePhase ResumePhase { get; private set; } = GamePhase.Dock;
	public event Action<GamePhase, GamePhase> Changed;

	public bool IsMenu => Phase is GamePhase.MainMenu or GamePhase.Shop or GamePhase.Selling
		or GamePhase.FishLog or GamePhase.Objectives or GamePhase.Pause or GamePhase.Settings
		or GamePhase.CatchResult;

	public bool BlocksWorldInput => IsMenu || Phase is GamePhase.EmergencyTow or GamePhase.Boarding;

	public bool CanSail => Phase is GamePhase.Sailing;
	public bool CanCast => Phase is GamePhase.Sailing or GamePhase.Dock;
	public bool IsFishing => Phase is GamePhase.Casting or GamePhase.WaitingBite or GamePhase.Hooking or GamePhase.Reeling;

	public bool TryEnter( GamePhase next )
	{
		if ( !CanTransition( Phase, next ) )
			return false;

		var prev = Phase;
		if ( next == GamePhase.Pause )
			ResumePhase = prev is GamePhase.Pause or GamePhase.Settings ? ResumePhase : prev;
		if ( next == GamePhase.Settings && Phase != GamePhase.MainMenu )
			ResumePhase = Phase == GamePhase.Pause ? ResumePhase : Phase;

		Phase = next;
		Changed?.Invoke( prev, next );
		return true;
	}

	public void Force( GamePhase next )
	{
		var prev = Phase;
		Phase = next;
		Changed?.Invoke( prev, next );
	}

	public bool CloseOverlay()
	{
		return Phase switch
		{
			GamePhase.Shop or GamePhase.Selling or GamePhase.FishLog or GamePhase.Objectives
				or GamePhase.CatchResult => TryEnter( WasAboard() ? GamePhase.Sailing : GamePhase.Dock ),
			GamePhase.Settings when ResumePhase == GamePhase.MainMenu => TryEnter( GamePhase.MainMenu ),
			GamePhase.Settings => TryEnter( GamePhase.Pause ),
			GamePhase.Pause => TryEnter( ResumePhase ),
			_ => false
		};
	}

	bool WasAboard() => ResumePhase is GamePhase.Sailing or GamePhase.Casting or GamePhase.WaitingBite
		or GamePhase.Hooking or GamePhase.Reeling;

	static bool CanTransition( GamePhase from, GamePhase to )
	{
		if ( from == to )
			return true;

		// Hard locks
		if ( from == GamePhase.EmergencyTow && to is not (GamePhase.Dock or GamePhase.MainMenu) )
			return false;
		if ( from == GamePhase.CatchResult && to is GamePhase.Casting or GamePhase.WaitingBite or GamePhase.Shop )
			return false;
		if ( from == GamePhase.Boarding && to is GamePhase.Shop or GamePhase.Casting )
			return false;
		if ( from == GamePhase.Sailing && to == GamePhase.Shop )
			return false;
		if ( from == GamePhase.Sailing && to == GamePhase.Selling )
			return false;
		if ( from is GamePhase.Casting or GamePhase.WaitingBite or GamePhase.Hooking or GamePhase.Reeling
			&& to is GamePhase.Shop or GamePhase.Selling or GamePhase.Boarding )
			return false;
		if ( from == GamePhase.MainMenu && to is not (GamePhase.Dock or GamePhase.Settings or GamePhase.MainMenu) )
			return false;

		return true;
	}
}
