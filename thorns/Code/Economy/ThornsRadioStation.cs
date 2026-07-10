namespace Sandbox;

/// <summary>World anchor for radio shop interaction — host validates distance to this transform (THORNS_EVERYTHING_DOCUMENT §radio).</summary>
[Title( "Thorns — Radio station" )]
[Category( "Thorns" )]
[Icon( "radio" )]
[Order( 60 )]
public sealed class ThornsRadioStation : Component
{
	public static readonly Dictionary<Guid, ThornsRadioStation> ActiveById = new();

	[Property] public float InteractionRadius { get; set; } = ThornsBuildingVisuals.PlaceableInteractionUseRange;

	[Sync( SyncFlags.FromHost )] public string StationIdSync { get; set; } = "";

	/// <summary>Stable id for RPC payloads (assigned once on host).</summary>
	public Guid StationId => SyncGuidParse( StationIdSync );

	protected override void OnStart()
	{
		// Offline / listen-host: assign id here. Pure clients receive host-authored StationIdSync via sync.
		if ( string.IsNullOrWhiteSpace( StationIdSync ) && ( !Networking.IsActive || Networking.IsHost ) )
			StationIdSync = Guid.NewGuid().ToString( "D" );

		TryRegisterLookup();
	}

	void TryRegisterLookup()
	{
		var id = StationId;
		if ( id == Guid.Empty )
			return;

		ActiveById[id] = this;
	}

	protected override void OnDestroy()
	{
		var id = StationId;
		if ( id != Guid.Empty && ActiveById.TryGetValue( id, out var s ) && s == this )
			ActiveById.Remove( id );
	}

	static Guid SyncGuidParse( string s ) =>
		string.IsNullOrWhiteSpace( s ) ? Guid.Empty : (Guid.TryParse( s, out var g ) ? g : Guid.Empty);

	public bool HostIsInRange( Vector3 pawnWorldPosition )
	{
		if ( InteractionRadius <= 0f )
			return false;

		return (GameObject.WorldPosition - pawnWorldPosition).Length <= InteractionRadius;
	}

	public static ThornsRadioStation FindNearest( Scene scene, Vector3 fromWorld, float maxDistance )
	{
		ThornsRadioStation best = default;
		var bestD = maxDistance;

		if ( ActiveById.Count > 0 )
		{
			foreach ( var s in ActiveById.Values )
			{
				if ( !s.IsValid() || s.StationId == Guid.Empty )
					continue;

				var d = (s.GameObject.WorldPosition - fromWorld).Length;
				if ( d < bestD )
				{
					bestD = d;
					best = s;
				}
			}

			return best;
		}

		if ( scene is null || !scene.IsValid() )
			return default;

		foreach ( var s in scene.GetAllComponents<ThornsRadioStation>() )
		{
			if ( !s.IsValid() || s.StationId == Guid.Empty )
				continue;

			var d = (s.GameObject.WorldPosition - fromWorld).Length;
			if ( d < bestD )
			{
				bestD = d;
				best = s;
			}
		}

		return best;
	}

	/// <summary>Among stations in range, pick the nearest one the local pawn is actually looking at (matches <see cref="ThornsRadioShopInteractor"/> Use).</summary>
	public static ThornsRadioStation FindBestUnderAimForPawn( Scene scene, GameObject pawnRoot, float maxSearchHoriz )
	{
		_ = maxSearchHoriz;
		if ( pawnRoot is null || !pawnRoot.IsValid() )
			return default;

		if ( ActiveById.Count > 0 )
			return FindBestUnderAimFromRegistry( pawnRoot );

		if ( scene is null || !scene.IsValid() )
			return default;

		ThornsRadioStation pick = default;
		var bestD = float.PositiveInfinity;
		foreach ( var s in scene.GetAllComponents<ThornsRadioStation>() )
		{
			if ( !s.IsValid() || s.StationId == Guid.Empty )
				continue;

			if ( !s.HostIsInRange( pawnRoot.WorldPosition ) )
				continue;

			var coneOnly = ThornsWorldUseAim.HasInteriorRadioRootTag( s.GameObject );
			if ( !ThornsWorldUseAim.PawnLooksAtInteractableRoot( pawnRoot, s.GameObject, s.InteractionRadius, coneOnly ) )
				continue;

			var d = ( s.GameObject.WorldPosition - pawnRoot.WorldPosition ).Length;
			if ( d >= bestD )
				continue;

			bestD = d;
			pick = s;
		}

		return pick;
	}

	static ThornsRadioStation FindBestUnderAimFromRegistry( GameObject pawnRoot )
	{
		ThornsRadioStation pick = default;
		var bestD = float.PositiveInfinity;

		foreach ( var s in ActiveById.Values )
		{
			if ( !s.IsValid() || s.StationId == Guid.Empty )
				continue;

			if ( !s.HostIsInRange( pawnRoot.WorldPosition ) )
				continue;

			var coneOnly = ThornsWorldUseAim.HasInteriorRadioRootTag( s.GameObject );
			if ( !ThornsWorldUseAim.PawnLooksAtInteractableRoot( pawnRoot, s.GameObject, s.InteractionRadius, coneOnly ) )
				continue;

			var d = ( s.GameObject.WorldPosition - pawnRoot.WorldPosition ).Length;
			if ( d >= bestD )
				continue;

			bestD = d;
			pick = s;
		}

		return pick;
	}
}
