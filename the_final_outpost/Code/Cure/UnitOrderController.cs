namespace FinalOutpost;

public enum UnitOrderKind
{
	None,
	Move,
	Gather,
	AttackMove,
	/// <summary>Night / day area hunt: move to a point and engage nearby zombies.</summary>
	AreaAttack
}

/// <summary>
/// Unit commands: Cure day orders for selected units, plus Survival/Cure night orders
/// that send all recruits to focus a zombie or clear an area. Towers stay fully automatic.
/// </summary>
public sealed class UnitOrderController : Component
{
	public static UnitOrderController Instance { get; private set; }
	public bool NightCommandMode { get; private set; }
	public bool CanAcceptNightClick => NightCommandMode && _armDelayFrames <= 0;

	protected override void OnAwake() => Instance = this;

	private int _armDelayFrames;

	protected override void OnUpdate()
	{
		if ( _armDelayFrames > 0 )
			_armDelayFrames--;

		if ( GameCore.Instance?.Phase != GamePhase.Night )
			CancelNightCommandMode();
	}

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
	}

	public void ToggleNightCommandMode()
	{
		var core = GameCore.Instance;
		if ( RecruitTakeoverController.Instance?.IsPossessing == true )
		{
			CancelNightCommandMode();
			return;
		}

		if ( core?.Phase != GamePhase.Night || (DefenderManager.Instance?.Count ?? 0) <= 0 )
		{
			CancelNightCommandMode();
			return;
		}

		NightCommandMode = !NightCommandMode;
		_armDelayFrames = NightCommandMode ? 2 : 0;
	}

	public void CancelNightCommandMode()
	{
		NightCommandMode = false;
		_armDelayFrames = 0;
	}

	/// <summary>Cure day: order the currently selected recruit/worker.</summary>
	public bool TryIssueOrder( Vector3 ground, ISelectable selected )
	{
		var core = GameCore.Instance;
		if ( core is null || !core.IsCure || core.Phase != GamePhase.Day ) return false;

		if ( selected is DefenderSelectable ds && ds.TryGetUnit() is { } defender )
		{
			if ( TryAttackTarget( ground, defender ) ) return true;
			defender.SetMoveOrder( ground );
			BuildManager.Instance?.Select( selected );
			return true;
		}

		if ( selected is WorkerSelectable ws && ws.TryGetUnit() is { } worker )
		{
			if ( PlotGrid.WorldToPlot( ground, out var px, out var py )
			     && PlotManager.Instance?.IsOwned( px, py ) == true
			     && !PlotGrid.IsHome( px, py )
			     && PlotGrid.HarvestResourceAt( px, py ) != ResourceKind.None )
			{
				worker.SetGatherOrder( px, py );
				BuildManager.Instance?.Select( selected );
				return true;
			}

			worker.SetMoveOrder( ground );
			BuildManager.Instance?.Select( selected );
			return true;
		}

		return false;
	}

	/// <summary>
	/// Night: all living recruits focus the nearest zombie under the cursor, or area-attack
	/// the clicked ground. Returns false only when there are no recruits to command.
	/// </summary>
	public bool TryIssueNightRecruitOrders( Vector3 ground )
	{
		var core = GameCore.Instance;
		if ( core is null || core.Phase != GamePhase.Night || !CanAcceptNightClick ) return false;
		if ( RecruitTakeoverController.Instance?.IsPossessing == true ) return false;

		var defenders = DefenderManager.Instance;
		if ( defenders is null || defenders.Count == 0 )
		{
			CancelNightCommandMode();
			return false;
		}

		var zombie = core.Combat?.NearestZombie( ground, GameConstants.NightFocusPickRadius );
		if ( zombie is not null && !zombie.Dead )
		{
			defenders.OrderAllFocus( zombie );
			CancelNightCommandMode();
			return true;
		}

		defenders.OrderAllAreaAttack( ground );
		CancelNightCommandMode();
		return true;
	}

	private static bool TryAttackTarget( Vector3 ground, DefenderManager.DefenderUnit unit )
	{
		var combat = GameCore.Instance?.Combat;
		if ( combat is null ) return false;

		var zombie = combat.NearestZombie( ground, GameConstants.NightFocusPickRadius );
		if ( zombie is null ) return false;

		unit.SetAttackOrder( zombie );
		return true;
	}
}
