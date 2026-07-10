namespace Terraingen.Economy;

/// <summary>World anchor for radio shop interaction — host validates distance to this transform.</summary>
public sealed class ThornsRadioStation : Component
{
	public static readonly Dictionary<Guid, ThornsRadioStation> ActiveById = new();

	[Property] public float InteractionRadius { get; set; } = 220f;

	[Sync( SyncFlags.FromHost )] public string StationIdSync { get; set; } = "";

	public Guid StationId => SyncGuidParse( StationIdSync );

	protected override void OnStart()
	{
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

			var coneOnly = Terraingen.World.ThornsWorldUseAim.HasInteriorRadioRootTag( s.GameObject );
			if ( !Terraingen.World.ThornsWorldUseAim.PawnLooksAtInteractableRoot(
				     pawnRoot, s.GameObject, s.InteractionRadius, coneOnly ) )
				continue;

			var d = (s.GameObject.WorldPosition - pawnRoot.WorldPosition).Length;
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

			var coneOnly = Terraingen.World.ThornsWorldUseAim.HasInteriorRadioRootTag( s.GameObject );
			if ( !Terraingen.World.ThornsWorldUseAim.PawnLooksAtInteractableRoot(
				     pawnRoot, s.GameObject, s.InteractionRadius, coneOnly ) )
				continue;

			var d = (s.GameObject.WorldPosition - pawnRoot.WorldPosition).Length;
			if ( d >= bestD )
				continue;

			bestD = d;
			pick = s;
		}

		return pick;
	}
}
