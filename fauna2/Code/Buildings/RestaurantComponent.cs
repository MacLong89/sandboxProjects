namespace Fauna2;

/// <summary>
/// A restaurant or shop that passively stores guest spending until the player clicks to collect.
/// </summary>
public sealed class RestaurantComponent : Component
{
	[Sync( SyncFlags.FromHost )] public float Uncollected { get; set; }

	private TimeUntil _nextTick;
	private PlaceableComponent _placeable;

	protected override void OnStart() => _placeable = Components.Get<PlaceableComponent>();

	protected override void OnFixedUpdate()
	{
		if ( IsProxy || !_nextTick )
			return;

		_nextTick = 1f;

		_placeable ??= Components.Get<PlaceableComponent>();
		var rate = RestaurantRevenue.PerSecond( _placeable );
		if ( rate <= 0f )
			return;

		var max = RestaurantRevenue.MaxStored( _placeable?.Definition );
		Uncollected = Math.Min( max, Uncollected + rate );
	}

	[Rpc.Host]
	public void RequestCollect()
	{
		if ( !RpcAuthorization.IsOwnerCaller() ) return;

		_placeable ??= Components.Get<PlaceableComponent>();

		// AUDIT FIX B7: Host proximity — client WorldInteract already ranges, but
		// the RPC previously collected from any building on the map.
		if ( _placeable is not null
			&& RpcAuthorization.TryGetCallerFeet( out var feet )
			&& !CollectibleBuildingHelper.IsWithinCollectRange( feet, _placeable ) )
		{
			return;
		}

		var state = ZooState.Instance;

		var amount = (int)Uncollected;
		if ( amount <= 0 )
		{
			state?.Notify( "No earnings to collect yet — guests need paths and time to visit.", "storefront" );
			return;
		}

		Uncollected = 0f;

		if ( !state.IsValid() )
			return;

		var name = _placeable?.Definition?.DisplayName ?? "building";

		// AUDIT FIX B9: Pass isIncome:true so amenity collections count toward
		// TotalEarned / daily EarnIncome goals (ticket revenue already did).
		// Revert: state.AddMoney( amount ); if restaurant should stay "bonus pocket".
		state.AddMoney( amount, isIncome: true );
		state.Notify( $"Collected ${amount:n0} from {name}", "storefront" );
		// wasEconomyGain path used to RequestSave via GameEvents; isIncome skips that.
		SaveSystem.Instance?.RequestSave();
	}
}
