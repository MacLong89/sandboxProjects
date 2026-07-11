namespace FinalOutpost;

public enum UnitOrderKind
{
	None,
	Move,
	Gather,
	AttackMove
}

/// <summary>Northgard-style unit commands for Road to a Cure (day phase).</summary>
public sealed class UnitOrderController : Component
{
	public static UnitOrderController Instance { get; private set; }

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
	}

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

	private static bool TryAttackTarget( Vector3 ground, DefenderManager.DefenderUnit unit )
	{
		var combat = GameCore.Instance?.Combat;
		if ( combat is null ) return false;

		var zombie = combat.NearestZombie( ground, 120f );
		if ( zombie is null ) return false;

		unit.SetAttackOrder( zombie );
		return true;
	}
}
