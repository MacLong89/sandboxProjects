namespace Deep;

public sealed class CollectibleSpawnSystem : Component
{
	private readonly List<GameObject> _spawned = new();
	private readonly Random _rng = new( 1337 );

	public IEnumerable<CollectiblePickup> ActivePickups
	{
		get
		{
			foreach ( var go in _spawned )
			{
				if ( go is null || !go.IsValid() ) continue;
				var c = go.Components.Get<CollectiblePickup>();
				if ( c is not null && c.IsValid() && !c.Collected )
					yield return c;
			}
		}
	}

	public void RespawnAll( BalanceConfig balance )
	{
		Clear();

		var bed = SeabedTerrain.Instance;
		var count = balance.CollectibleSpawnCount;
		for ( var i = 0; i < count; i++ )
		{
			var def = PickWeighted( balance );
			if ( def is null ) continue;

			Vector3 pos;
			if ( bed is not null )
				pos = SampleInColumn( bed, balance, def );
			else
			{
				var depth = Lerp( def.MinDepth, MathF.Min( def.MaxDepth, balance.MaxOceanDepthMeters - 2f ), (float)_rng.NextDouble() );
				var x = Lerp( -balance.HorizontalHalfWidth + 3f, balance.HorizontalHalfWidth - 3f, (float)_rng.NextDouble() );
				pos = new Vector3( x, 0f, balance.WorldZFromDepth( depth ) );
			}

			var go = new GameObject( true, $"Loot_{def.Id}_{i}" );
			go.WorldPosition = pos;
			var pickup = go.Components.Create<CollectiblePickup>();
			pickup.Setup( def );

			if ( def.IsSwimming )
			{
				var motion = go.Components.Create<SwimMotion>();
				motion.Configure( pos,
					ampX: 4f + (float)_rng.NextDouble() * 3f,
					ampZ: 1.4f + (float)_rng.NextDouble() * 1.4f,
					speed: 0.7f + (float)_rng.NextDouble() * 0.5f,
					phase: i * 0.55f );
			}

			_spawned.Add( go );
		}
	}

	private Vector3 SampleInColumn( SeabedTerrain bed, BalanceConfig balance, CollectibleDefinition def )
	{
		var maxDepth = MathF.Min( def.MaxDepth, balance.MaxOceanDepthMeters - 2f );
		var minDepth = MathF.Min( def.MinDepth, maxDepth );
		var depth = Lerp( minDepth, maxDepth, (float)_rng.NextDouble() );
		var x = bed.SampleX( (float)_rng.NextDouble() );
		var height = def.SpriteWorldHeight;

		if ( def.IsSwimming )
		{
			var z = balance.WorldZFromDepth( depth );
			var floor = bed.FloorZAtX( x ) + bed.SwimClearance + height;
			if ( z < floor ) z = floor + 1f;
			return new Vector3( x, 0.1f, z );
		}

		// Salvage rests on the seafloor like Terraria ground loot.
		return bed.GroundSprite( x, height, 0.05f );
	}

	public void Clear()
	{
		foreach ( var go in _spawned )
		{
			if ( go.IsValid() )
				go.Destroy();
		}
		_spawned.Clear();
	}

	private CollectibleDefinition PickWeighted( BalanceConfig balance )
	{
		var candidates = CollectibleCatalog.All
			.Where( c => c.MinDepth < balance.MaxOceanDepthMeters )
			.ToList();
		if ( candidates.Count == 0 )
			return null;

		var total = candidates.Sum( c => c.SpawnWeight );
		var roll = (float)_rng.NextDouble() * total;
		foreach ( var c in candidates )
		{
			roll -= c.SpawnWeight;
			if ( roll <= 0f )
				return c;
		}

		return candidates[^1];
	}

	private static float Lerp( float a, float b, float t ) => a + (b - a) * t;
}
