namespace DeepDive;

public sealed class HazardSpawnSystem : Component
{
	private readonly List<GameObject> _spawned = new();
	private readonly Random _rng = new( 4242 );

	public IEnumerable<HazardContact> ActiveHazards
	{
		get
		{
			foreach ( var go in _spawned )
			{
				if ( go is null || !go.IsValid() ) continue;
				var h = go.Components.Get<HazardContact>();
				if ( h is not null && h.IsValid() )
					yield return h;
			}
		}
	}

	public void RespawnAll( BalanceConfig balance )
	{
		Clear();

		var bed = SeabedTerrain.Instance;
		if ( bed is null )
			return;

		// Schools / packs in the open column — not glued to a diagonal wall.
		SpawnPack( bed, balance, HazardKind.Jellyfish, count: 5, minDepth: 15f, maxDepth: 80f,
			spriteH: 2.8f, damageMul: 0.7f, ampX: 5f, ampZ: 2.2f, speed: 0.55f, hitR: 2.0f );
		SpawnPack( bed, balance, HazardKind.Puffer, count: 4, minDepth: 30f, maxDepth: 160f,
			spriteH: 2.4f, damageMul: 0.9f, ampX: 3.5f, ampZ: 1.4f, speed: 0.4f, hitR: 2.1f );
		SpawnPack( bed, balance, HazardKind.Mine, count: 6, minDepth: 50f, maxDepth: balance.MaxOceanDepthMeters - 20f,
			spriteH: 2.2f, damageMul: 1.35f, ampX: 1.2f, ampZ: 1.4f, speed: 0.3f, hitR: 2.3f );
		SpawnPack( bed, balance, HazardKind.Angler, count: 3, minDepth: 110f, maxDepth: 250f,
			spriteH: 3.6f, damageMul: 1.15f, ampX: 6f, ampZ: 2.5f, speed: 0.65f, hitR: 2.6f );
		SpawnPack( bed, balance, HazardKind.Angler, count: 3, minDepth: 260f, maxDepth: balance.MaxOceanDepthMeters - 10f,
			spriteH: 4.0f, damageMul: 1.4f, ampX: 7f, ampZ: 3f, speed: 0.7f, hitR: 2.8f );
		SpawnPack( bed, balance, HazardKind.Mine, count: 4, minDepth: 220f, maxDepth: balance.MaxOceanDepthMeters - 15f,
			spriteH: 2.4f, damageMul: 1.5f, ampX: 1f, ampZ: 1.2f, speed: 0.25f, hitR: 2.4f );
	}

	private void SpawnPack(
		SeabedTerrain bed,
		BalanceConfig balance,
		HazardKind kind,
		int count,
		float minDepth,
		float maxDepth,
		float spriteH,
		float damageMul,
		float ampX,
		float ampZ,
		float speed,
		float hitR )
	{
		for ( var i = 0; i < count; i++ )
		{
			var depth = Lerp( minDepth, maxDepth, (float)_rng.NextDouble() );
			var x = bed.SampleX( (float)_rng.NextDouble() );
			var z = balance.WorldZFromDepth( depth );

			// Keep a little air above the seafloor so packs patrol the column, not inside rocks.
			var floor = bed.FloorZAtX( x ) + bed.SwimClearance + spriteH * 0.5f;
			if ( z < floor ) z = floor + (float)_rng.NextDouble() * 6f;

			var origin = new Vector3( x, 0.15f, z );
			var go = new GameObject( true, $"{kind}_{i}" );
			go.WorldPosition = origin;

			var hazard = go.Components.Create<HazardContact>();
			hazard.Setup( kind, balance.HazardDamage * damageMul, spriteH, hitR );

			var motion = go.Components.Create<SwimMotion>();
			motion.Configure( origin, ampX, ampZ, speed, phase: i * 0.7f );
			motion.FaceMotion = kind != HazardKind.Mine;

			_spawned.Add( go );
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
