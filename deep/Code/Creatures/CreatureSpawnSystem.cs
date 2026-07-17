namespace Deep;

public sealed class CreatureSpawnSystem : Component
{
	private readonly List<GameObject> _spawned = new();
	private readonly Random _rng = new( 9091 );

	public void RespawnAll( BalanceConfig balance )
	{
		Clear();
		var bed = SeabedTerrain.Instance;
		if ( bed is null ) return;

		foreach ( var def in CreatureCatalog.All.Where( c => c.Disposition == CreatureDisposition.Ambient ) )
		{
			var count = def.Id == "reef_fish" ? 6 : 4;
			for ( var i = 0; i < count; i++ )
			{
				var maxD = MathF.Min( def.MaxDepth, balance.MaxOceanDepthMeters - 5f );
				if ( def.MinDepth >= maxD ) continue;

				var depth = Lerp( def.MinDepth, maxD, (float)_rng.NextDouble() );
				var x = bed.SampleX( (float)_rng.NextDouble() );
				var z = balance.WorldZFromDepth( depth );
				var floor = bed.FloorZAtX( x ) + bed.SwimClearance + def.SpriteWorldHeight * 0.5f;
				if ( z < floor ) z = floor + 2f;

				var origin = new Vector3( x, 0.12f, z );
				var go = new GameObject( true, $"Ambient_{def.Id}_{i}" );
				go.WorldPosition = origin;

				var ambient = go.Components.Create<AmbientCreature>();
				ambient.Setup( def );

				var motion = go.Components.Create<SwimMotion>();
				motion.Configure( origin,
					ampX: 3f + (float)_rng.NextDouble() * 4f,
					ampZ: 1.2f + (float)_rng.NextDouble() * 1.5f,
					speed: 0.45f + (float)_rng.NextDouble() * 0.5f,
					phase: i * 0.8f );

				_spawned.Add( go );
			}
		}
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

	private static float Lerp( float a, float b, float t ) => a + (b - a) * t;
}
