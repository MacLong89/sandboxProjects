namespace NoFly;

public sealed class NpcPassenger : Component
{
	[Sync( SyncFlags.FromHost )] public string NpcId { get; set; }
	[Sync( SyncFlags.FromHost )] public PassengerFlowState FlowState { get; set; } = PassengerFlowState.EnteringAirport;
	[Sync( SyncFlags.FromHost )] public string Gate { get; set; }
	[Sync( SyncFlags.FromHost )] public bool HasInvalidDocument { get; set; }
	[Sync( SyncFlags.FromHost )] public bool HasWeirdLegalItem { get; set; }

	public DocumentInstance Document { get; set; }
	public BagInstance Bag { get; set; }

	CitizenAvatar _avatar;
	Color _outfit;
	float _speed = 110f;
	TimeSince _panic;
	GameObject _bagProp;

	protected override void OnStart()
	{
		if ( string.IsNullOrEmpty( NpcId ) )
			NpcId = Guid.NewGuid().ToString( "N" )[..8];
		_outfit = Kit.RandomOutfit();
		EnsureVisual();
		if ( Networking.IsHost )
		{
			Gate = Random.Shared.FromArray( FlightCatalog.Gates );
			HasInvalidDocument = Random.Shared.NextDouble() < 0.12;
			HasWeirdLegalItem = Random.Shared.NextDouble() < 0.18;
			Document = DocumentCatalog.CreateValid( NpcId );
			if ( HasInvalidDocument )
			{
				Document.ForgedField = DocumentFieldType.CountrySymbol;
				Document.OriginalValue = Document.Values[DocumentFieldType.CountrySymbol];
				Document.ForgedValue = "FISH";
				Document.Values[DocumentFieldType.CountrySymbol] = "FISH";
			}
			Bag = Random.Shared.NextDouble() < 0.14
				? LuggageCatalog.CreateSmugglerBag( NpcId )
				: LuggageCatalog.CreateCleanBag( NpcId );
			_speed = 90f + (float)Random.Shared.NextDouble() * 60f;
		}
	}

	protected override void OnUpdate()
	{
		EnsureVisual();
		_avatar?.Tick();
	}

	void EnsureVisual()
	{
		if ( !_avatar.IsValid() )
			_avatar = Components.GetOrCreate<CitizenAvatar>();
		_avatar.Ensure( _outfit );

		if ( _bagProp.IsValid() ) return;

		_bagProp = new GameObject( true, "CarryBag" );
		_bagProp.SetParent( GameObject );
		_bagProp.LocalPosition = new Vector3( 12f, 8f, 28f );
		_bagProp.LocalScale = new Vector3( 0.22f, 0.16f, 0.32f );
		var br = _bagProp.Components.Create<ModelRenderer>();
		br.Model = Model.Load( Kit.BoxModel );
		br.MaterialOverride = Material.Load( Kit.DefaultMaterial );
		br.Tint = Bag?.SuitcaseColor ?? Kit.RandomOutfit();
	}

	public void Tick( NoFlyGame game )
	{
		if ( !Networking.IsHost ) return;
		if ( game.ChaseActive && _panic < 0.1f )
		{
			_panic = 0;
			var flee = WorldPosition + new Vector3( RandRange( -200, 200 ), RandRange( -200, 200 ), 0 );
			MoveTo( flee, _speed * 1.6f );
			return;
		}

		switch ( FlowState )
		{
			case PassengerFlowState.EnteringAirport:
			case PassengerFlowState.GoingToDocuments:
				MoveTo( game.Airport.GetSpawn( "doc_queue_0" ), _speed );
				if ( Near( game.Airport.GetSpawn( "doc_queue_0" ) ) )
					FlowState = PassengerFlowState.DocumentQueue;
				break;
			case PassengerFlowState.DocumentQueue:
				if ( Random.Shared.NextDouble() < 0.02 )
				{
					FlowState = HasInvalidDocument && Random.Shared.NextDouble() < 0.5
						? PassengerFlowState.Detained
						: PassengerFlowState.GoingToScanner;
				}
				else
					MoveTo( game.Airport.GetSpawn( "doc_queue_0" ), _speed * 0.4f );
				break;
			case PassengerFlowState.GoingToScanner:
			case PassengerFlowState.ScannerQueue:
				MoveTo( game.Airport.GetSpawn( "scan_queue_0" ), _speed );
				// Wait on the belt for ScannerStation — don't skip past the desk.
				if ( Near( game.Airport.GetSpawn( "scan_queue_0" ) ) )
					FlowState = PassengerFlowState.ScannerQueue;
				break;
			case PassengerFlowState.BagInspection:
				MoveTo( game.Airport.GetSpawn( "scan_queue_0" ), _speed * 0.25f );
				break;
			case PassengerFlowState.InTerminal:
				if ( Random.Shared.NextDouble() < 0.4 )
					MoveTo( game.Airport.GetZone( "shop_food" ), _speed );
				else
					MoveTo( game.Airport.GetZone( "seating" ), _speed );
				if ( game.RoundElapsed > game.Settings.BoardingStartsAtSeconds - 40f )
					FlowState = PassengerFlowState.GoingToGate;
				break;
			case PassengerFlowState.GoingToGate:
				MoveTo( game.Airport.GetGateApproach( Gate ), _speed );
				if ( Near( game.Airport.GetGateApproach( Gate ) ) )
					FlowState = PassengerFlowState.Boarding;
				break;
			case PassengerFlowState.Boarding:
				MoveTo( game.Airport.GetBoardSpawn( Gate ), _speed );
				if ( Near( game.Airport.GetBoardSpawn( Gate ) ) )
					FlowState = PassengerFlowState.Boarded;
				break;
			case PassengerFlowState.Detained:
				MoveTo( game.Airport.GetSpawn( "holding" ), _speed );
				break;
		}
	}

	void MoveTo( Vector3 target, float speed )
	{
		var delta = (target - WorldPosition).WithZ( 0 );
		if ( delta.Length < 10f ) return;
		WorldPosition += delta.Normal * speed * Time.Delta;
		WorldRotation = Rotation.LookAt( delta.Normal );
	}

	bool Near( Vector3 target ) => Vector3.DistanceBetween( WorldPosition.WithZ( 0 ), target.WithZ( 0 ) ) < 40f;

	static float RandRange( float min, float max ) => min + (float)Random.Shared.NextDouble() * (max - min);
}

public static class NpcSpawner
{
	static readonly List<GameObject> Alive = new();

	public static void SpawnInitial( int count )
	{
		ClearAll();
		var game = NoFlyGame.Instance;
		if ( game?.Airport is null ) return;
		for ( var i = 0; i < count; i++ )
		{
			var go = new GameObject( true, $"NPC_{i}" );
			var offset = new Vector3( -80f + (float)Random.Shared.NextDouble() * 160f, -120f + (float)Random.Shared.NextDouble() * 240f, 0 );
			go.WorldPosition = game.Airport.GetSpawn( "entrance" ) + offset;
			go.Components.Create<NpcPassenger>();
			go.NetworkSpawn();
			Alive.Add( go );
		}
	}

	public static void ClearAll()
	{
		foreach ( var go in Alive.ToArray() )
		{
			if ( go.IsValid() ) go.Destroy();
		}
		Alive.Clear();
		foreach ( var npc in Game.ActiveScene.GetAllComponents<NpcPassenger>().ToList() )
			npc.GameObject.Destroy();
	}
}

public static class NpcDirector
{
	public static void Tick( NoFlyGame game )
	{
		if ( !Networking.IsHost ) return;
		foreach ( var npc in game.Scene.GetAllComponents<NpcPassenger>() )
			npc.Tick( game );

		// Occasional respawn trickle
		var count = game.Scene.GetAllComponents<NpcPassenger>().Count();
		if ( game.IsPlaying && count < game.Settings.TargetNpcCount && Random.Shared.NextDouble() < 0.01 )
		{
			var go = new GameObject( true, "NPC_extra" );
			go.WorldPosition = game.Airport.GetSpawn( "entrance" );
			go.Components.Create<NpcPassenger>();
			go.NetworkSpawn();
		}
	}
}
