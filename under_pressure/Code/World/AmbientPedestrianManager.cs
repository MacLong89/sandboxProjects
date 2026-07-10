namespace UnderPressure;

/// <summary>
/// Keeps a steady flow of background citizens walking through each job site.
/// Density scales with the map theme — busy downtown levels see far more foot traffic.
/// </summary>
public sealed class AmbientPedestrianManager : Component
{
	public static AmbientPedestrianManager Instance { get; private set; }

	private int _lastGeneration = -1;
	private GameObject _root;
	private readonly List<AmbientPedestrian> _active = new();
	private AmbientPedestrianDensity _density;
	private TimeUntil _nextSpawn;

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}

	protected override void OnUpdate()
	{
		var core = GameCore.Instance;
		if ( core is null )
			return;

		if ( core.Jobs.LoadGeneration != _lastGeneration )
		{
			_lastGeneration = core.Jobs.LoadGeneration;
			Rebuild( core );
		}

		if ( core.IsWorldFrozen )
			return;

		Prune( core );
		TickSpawns( core );
	}

	private void Rebuild( GameCore core )
	{
		_root?.Destroy();
		_active.Clear();

		var job = core.Jobs.Current;
		if ( job is null )
			return;

		_density = AmbientPedestrianDensity.For( job.Theme );
		_root = new GameObject( true, "AmbientPedestrians" );
		_nextSpawn = Game.Random.Float( _density.SpawnIntervalMin, _density.SpawnIntervalMax );

		for ( var i = 0; i < _density.InitialBurst; i++ )
			TrySpawn( core );
	}

	private void TickSpawns( GameCore core )
	{
		if ( _root is null || !_root.IsValid() )
			return;

		if ( _nextSpawn > 0f )
			return;

		TrySpawn( core );
		_nextSpawn = Game.Random.Float( _density.SpawnIntervalMin, _density.SpawnIntervalMax );
	}

	private void TrySpawn( GameCore core )
	{
		_active.RemoveAll( p => !p.IsValid() );
		if ( _active.Count >= _density.MaxActive )
			return;

		var job = core.Jobs.Current;
		if ( job is null || _root is null || !_root.IsValid() )
			return;

		if ( !TryPickRoute( job, core, out var start, out var end ) )
			return;

		var speed = Game.Random.Float( _density.WalkSpeedMin, _density.WalkSpeedMax );
		var tint = RandomSkinTint();
		var height = Game.Random.Float( 0.94f, 1.06f ) * GameConstants.CitizenHeightScale;

		var go = new GameObject( _root, true, "Pedestrian" );
		var ped = go.Components.Create<AmbientPedestrian>();
		ped.Init( start, end, speed, tint, height );
		_active.Add( ped );
	}

	private void Prune( GameCore core )
	{
		var job = core.Jobs.Current;
		if ( job is null )
			return;

		var roam = MathF.Max( job.GroundSize.x, job.GroundSize.y ) * 0.72f;
		for ( var i = _active.Count - 1; i >= 0; i-- )
		{
			var ped = _active[i];
			if ( !ped.IsValid() || ped.IsExpired( job.WorkCenter, roam ) )
			{
				ped.GameObject.Destroy();
				_active.RemoveAt( i );
			}
		}
	}

	private static bool TryPickRoute( JobDef job, GameCore core, out Vector3 start, out Vector3 end )
	{
		start = default;
		end = default;

		var center = job.WorkCenter;
		var half = job.GroundSize * 0.5f;
		var player = PressurePlayer.Instance?.WorldPosition ?? center;
		const float minPlayerDist = 180f;

		for ( var attempt = 0; attempt < 12; attempt++ )
		{
			var edge = Game.Random.Int( 0, 3 );
			start = EdgePoint( center, half, edge, inset: 0.92f );

			// Walk to the opposite edge, or along the long axis in urban lots.
			var destEdge = job.Theme is MapTheme.UrbanPlaza or MapTheme.Storefront or MapTheme.Alley
				? (edge + 2) % 4
				: Game.Random.Int( 0, 3 );
			end = EdgePoint( center, half, destEdge, inset: 0.88f );

			var path = (end - start).WithZ( 0f );
			if ( path.Length < 260f )
				continue;

			if ( (start - player).WithZ( 0f ).Length < minPlayerDist )
				continue;

			return true;
		}

		return false;
	}

	private static Vector3 EdgePoint( Vector3 center, Vector2 half, int edge, float inset )
	{
		var hx = half.x * inset;
		var hy = half.y * inset;
		var along = Game.Random.Float( -0.85f, 0.85f );

		return edge switch
		{
			0 => center + new Vector3( along * hx, hy, 0f ),
			1 => center + new Vector3( hx, along * hy, 0f ),
			2 => center + new Vector3( along * hx, -hy, 0f ),
			_ => center + new Vector3( -hx, along * hy, 0f ),
		};
	}

	private static Color RandomSkinTint()
	{
		var baseTone = Game.Random.Float( 0.72f, 0.94f );
		var warmth = Game.Random.Float( -0.06f, 0.08f );
		return new Color( baseTone + warmth, baseTone - 0.04f, baseTone - 0.12f, 1f );
	}
}
