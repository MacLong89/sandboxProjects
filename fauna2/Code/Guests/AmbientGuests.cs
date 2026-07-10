namespace Fauna2;



/// <summary>
/// Purely cosmetic guest sprites, simulated locally from the synced guest count.
/// </summary>

public sealed class AmbientGuests : Component

{

	public static int DebugPoolCount { get; private set; }

	public static int DebugMovingCount { get; private set; }

	public static int DebugConnectedPaths { get; private set; }



	private enum GuestState

	{

		Wandering,

		Leaving,

	}



	private sealed class Guest

	{

		public GameObject Object;

		public SpriteRenderer Sprite;

		public PlaceableComponent Path;

		public Vector3 Target;

		public float Speed;

		public GuestState State;

	}



	private readonly List<Guest> _pool = new();

	private TimeUntil _nextAdjust;

	private int _movingThisTick;



	/// <summary>When a path tile is laid under a guest, send them walking so the new ground shows.</summary>
	public static void NudgeFromPathTile( Vector3 pathCenter )
	{
		var scene = Game.ActiveScene;
		if ( scene is null ) return;

		foreach ( var system in scene.GetAllComponents<AmbientGuests>() )
			system.NudgeGuestsFromPathTile( pathCenter );
	}

	private void NudgeGuestsFromPathTile( Vector3 pathCenter )
	{
		var radius = GameConstants.TileSize * 0.55f;
		var pathPos = pathCenter.WithZ( 0 );
		var path = PlaceableRegistry.NearestPath( pathCenter, radius );

		foreach ( var guest in _pool )
		{
			if ( !guest.Object.IsValid() ) continue;

			var feet = guest.Object.WorldPosition.WithZ( 0 );
			if ( feet.Distance( pathPos ) > radius ) continue;

			if ( path.IsValid() )
				guest.Path = path;

			PickNextPathTarget( guest );
		}
	}



	protected override void OnFixedUpdate()

	{

		if ( GameManager.Instance is null || !GameManager.Instance.GameStarted )

			return;



		var clock = DebugStats.StartTimer();



		if ( _nextAdjust )

		{

			_nextAdjust = 2.5f;

			AdjustPopulation();

		}



		_movingThisTick = 0;

		MoveGuests();



		DebugPoolCount = _pool.Count;

		DebugMovingCount = _movingThisTick;

		DebugConnectedPaths = PathNetwork.GetConnectedPaths().Count;



		DebugStats.StopTimer( "AmbientGuests", clock );

	}



	private void AdjustPopulation()

	{

		if ( !PathNetwork.HasGuestAccess )

		{

			ClearPool();

			return;

		}



		var guestCount = GuestSystem.Instance?.GuestCount ?? 0;

		if ( guestCount <= 0 )

		{

			ClearPool();

			return;

		}



		var desired = Math.Clamp( guestCount / GameConstants.GuestsPerAmbientVisual, 1, GameConstants.MaxAmbientGuestVisuals );

		var camPos = Scene.Camera.IsValid() ? Scene.Camera.WorldPosition : Vector3.Zero;



		while ( _pool.Count < desired )

		{

			var guest = CreateGuest();

			if ( guest is null ) break;

			_pool.Add( guest );

		}



		var surplus = _pool.Count - desired;

		for ( var i = 0; i < surplus; i++ )

			BeginRemoveGuest( camPos );

	}



	private void BeginRemoveGuest( Vector3 camPos )

	{

		var guest = PickGuestToRemove( camPos );

		if ( guest is null )

			return;



		if ( guest.State == GuestState.Leaving || guest.Object.WorldPosition.Distance( camPos ) > 900f )

		{

			_pool.Remove( guest );

			RemoveGuest( guest );

			return;

		}



		guest.State = GuestState.Leaving;

		guest.Target = ExitPoint();

	}



	private Guest PickGuestToRemove( Vector3 camPos )

	{

		Guest best = null;

		var bestScore = float.MinValue;



		foreach ( var guest in _pool )

		{

			if ( !guest.Object.IsValid() ) continue;



			var dist = guest.Object.WorldPosition.Distance( camPos );

			var score = dist + (guest.State == GuestState.Leaving ? 500f : 0f);

			if ( score > bestScore )

			{

				bestScore = score;

				best = guest;

			}

		}



		return best;

	}



	private void MoveGuests()

	{

		for ( var i = _pool.Count - 1; i >= 0; i-- )

		{

			var guest = _pool[i];

			if ( !guest.Object.IsValid() )

			{

				_pool.RemoveAt( i );

				continue;

			}



			TickGuest( guest );



			if ( guest.State == GuestState.Leaving

				&& guest.Object.WorldPosition.Distance( ExitPoint() ) < 28f )

			{

				RemoveGuest( guest );

				_pool.RemoveAt( i );

			}

		}

	}



	private void TickGuest( Guest guest )

	{

		var pos = guest.Object.WorldPosition;



		if ( guest.State == GuestState.Leaving )

			guest.Target = ExitPoint();



		var flatDelta = (guest.Target - pos).WithZ( 0 );

		var dist = flatDelta.Length;



		if ( dist >= 6f )

		{

			var step = flatDelta.Normal * guest.Speed * Time.Delta;

			if ( step.Length > dist )

				step = flatDelta;



			var newPos = (pos + step).WithZ( PathWalkHeight() );



			guest.Path = PathNetwork.NearestConnectedPath( newPos, 200f ) ?? guest.Path;

			guest.Object.WorldPosition = newPos;

			_movingThisTick++;

		}

		else if ( guest.State == GuestState.Wandering )

		{

			PickNextPathTarget( guest );

		}



		if ( guest.Path is null || !PathNetwork.IsConnectedPath( guest.Path ) )
			RelocateGuestToPath( guest );
	}



	private Guest CreateGuest()

	{

		var path = PickRandomPath();

		if ( path is null )

			return null;



		var start = RandomPointOnPath( path, Vector3.Zero, 0f );



		var go = new GameObject( GameObject, true, "Ambient Guest" );
		go.Tags.Add( "guest" );

		go.WorldPosition = start.WithZ( PathWalkHeight() );



		var variantIndex = Game.Random.Int( 0, SuppliedSpriteManifest.GuestVariantSpritePaths.Length - 1 );
		var guestSize = GameConstants.Tiles( GameConstants.PlayerSpriteTiles * 0.85f );
		var renderer = WorldSprites.Spawn(
			go,
			PixelArt.GuestSpriteResource( variantIndex ),
			guestSize,
			Vector3.Zero,
			"Sprite",
			layer: WorldSprites.GuestLayer,
			dynamicDepthSort: true,
			sourcePixels: PixelArt.SuppliedSpriteSourcePixels,
			movementRoot: go,
			walkAnimator: true );


		var guest = new Guest

		{

			Object = go,

			Sprite = renderer,

			Path = path,

			Speed = Game.Random.Float( 70f, 110f ),

			State = GuestState.Wandering,

		};



		PickNextPathTarget( guest );

		return guest;

	}



	private void PickNextPathTarget( Guest guest )

	{

		if ( guest.Path is null || !PathNetwork.IsConnectedPath( guest.Path ) )

		{

			RelocateGuestToPath( guest );

			return;

		}



		var neighbors = GetAdjacentPaths( guest.Path );

		if ( neighbors.Count > 0 )

		{

			var nextPath = neighbors[Game.Random.Int( 0, neighbors.Count - 1 )];

			guest.Path = nextPath;

			guest.Target = PathDestination( nextPath );

			return;

		}



		guest.Target = RandomPointOnPath( guest.Path, guest.Object.WorldPosition, 48f )

			.WithZ( PathWalkHeight() );

	}



	private void RelocateGuestToPath( Guest guest )

	{

		var path = PickRandomPath();

		if ( path is null )

		{

			_pool.Remove( guest );

			RemoveGuest( guest );

			return;

		}



		guest.Path = path;

		var spawn = RandomPointOnPath( path, Vector3.Zero, 0f ).WithZ( PathWalkHeight() );

		guest.Object.WorldPosition = spawn;

		PickNextPathTarget( guest );

	}



	private static List<PlaceableComponent> GetAdjacentPaths( PlaceableComponent current )

	{

		return PathNetwork.GetConnectedPaths()

			.Where( p => p != current && PathNetwork.AreAdjacent( current, p ) )

			.ToList();

	}



	private static PlaceableComponent PickRandomPath()

	{

		var paths = PathNetwork.GetConnectedPaths().ToList();

		if ( paths.Count == 0 ) return null;

		return paths[Game.Random.Int( 0, paths.Count - 1 )];

	}



	private static Vector3 PathDestination( PlaceableComponent path )

	{

		var center = path.GameObject.WorldPosition;

		var jitter = new Vector3(

			Game.Random.Float( -20f, 20f ),

			Game.Random.Float( -20f, 20f ),

			0 );

		return (center + jitter).WithZ( PathWalkHeight() );

	}



	private static Vector3 RandomPointOnPath( PlaceableComponent path, Vector3 avoid, float minDistance )

	{

		var footprint = path.Definition?.Footprint ?? new Vector2( 128, 128 );

		var inset = footprint * 0.35f;

		var center = path.GameObject.WorldPosition;

		var rot = path.GameObject.WorldRotation;



		for ( var attempt = 0; attempt < 8; attempt++ )

		{

			var offset = new Vector3(

				Game.Random.Float( -inset.x, inset.x ),

				Game.Random.Float( -inset.y, inset.y ),

				0 );



			var point = center + rot * offset;

			if ( minDistance <= 0f || avoid.WithZ( 0 ).Distance( point.WithZ( 0 ) ) >= minDistance )

				return point;

		}



		return center + rot.Forward * inset.x * 0.5f;

	}



	private static float PathWalkHeight() => 2f;



	private static Vector3 ExitPoint()

	{

		var entrance = PathNetwork.Entrance;

		if ( !entrance.IsValid() )

			return Vector3.Zero;



		var pos = entrance.GameObject.WorldPosition;

		var path = PathNetwork.GetConnectedPaths()

			.OrderBy( p => p.GameObject.WorldPosition.Distance( pos ) )

			.FirstOrDefault();



		if ( path is not null )

		{

			var inward = (path.GameObject.WorldPosition - pos).WithZ( 0 ).Normal;

			return (pos + inward * 36f).WithZ( PathWalkHeight() );

		}



		return (pos + entrance.GameObject.WorldRotation.Forward * 40f).WithZ( PathWalkHeight() );

	}



	private static void RemoveGuest( Guest guest )

	{

		guest.Object?.Destroy();

	}



	private void ClearPool()

	{

		foreach ( var guest in _pool )

			RemoveGuest( guest );

		_pool.Clear();

	}



	protected override void OnDestroy() => ClearPool();

}

