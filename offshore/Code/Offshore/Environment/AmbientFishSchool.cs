namespace Offshore;

/// <summary>
/// Swimming fish / schools (and a few seagulls) in the water.
/// Proximity to the bobber biases <see cref="FishSpawnSystem.Select"/> toward that species.
/// </summary>
public sealed class AmbientFishSchool : Component
{
	public static AmbientFishSchool Instance { get; private set; }

	private const float FishPlaneY = -24.5f; // near angler / bobber depth plane
	private const float ProximityRadius = 6f;
	private const float MaxProximityMul = 3.8f;
	private const float MinProximityMul = 1.35f;

	private readonly List<Swimmer> _swimmers = new();
	private readonly List<Seagull> _seagulls = new();
	private string _spawnedForLocation = "";

	private sealed class Swimmer
	{
		public GameObject Go;
		public SpriteRenderer Sprite;
		public string FishId;
		public float HomeX;
		public float HomeZ;
		public float PatrolHalf;
		public float Speed;
		public float Phase;
		public float BobAmp;
		public float SchoolOffsetX;
		public float SchoolOffsetZ;
		public bool FacingRight = true;
		public Vector2 Size;
	}

	private sealed class Seagull
	{
		public GameObject Go;
		public SpriteRenderer Sprite;
		public float HomeX;
		public float Phase;
		public float Speed;
		public float Altitude;
	}

	protected override void OnStart()
	{
		Instance = this;
		WorldPosition = Vector3.Zero;
		TryRespawn();
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
		ClearAll();
	}

	protected override void OnUpdate()
	{
		TryRespawn();
		TickSwimmers();
		TickSeagulls();
	}

	/// <summary>
	/// Species weight multipliers for fish near the bobber (1 = no change).
	/// Closer / denser schools → stronger bias.
	/// </summary>
	public Dictionary<string, float> BuildProximityMultipliers( Vector3 bobber, float radius = ProximityRadius )
	{
		var muls = new Dictionary<string, float>( StringComparer.OrdinalIgnoreCase );
		if ( radius <= 0.01f )
			return muls;

		foreach ( var s in _swimmers )
		{
			if ( s.Go is null || !s.Go.IsValid() )
				continue;

			var p = s.Go.WorldPosition;
			var dx = p.x - bobber.x;
			var dz = p.z - bobber.z;
			var dist = MathF.Sqrt( dx * dx + dz * dz );
			if ( dist > radius )
				continue;

			var t = Math.Clamp( dist / radius, 0f, 1f );
			var mul = MathX.Lerp( MaxProximityMul, MinProximityMul, t );
			if ( !muls.TryGetValue( s.FishId, out var cur ) || mul > cur )
				muls[s.FishId] = mul;
		}

		return muls;
	}

	public string DescribeNearby( Vector3 bobber, float radius = ProximityRadius )
	{
		var muls = BuildProximityMultipliers( bobber, radius );
		if ( muls.Count == 0 )
			return "";

		string bestId = null;
		var bestMul = 0f;
		foreach ( var kv in muls )
		{
			if ( kv.Value > bestMul )
			{
				bestMul = kv.Value;
				bestId = kv.Key;
			}
		}

		var def = FishCatalog.Get( bestId );
		return def is null ? "" : $"{def.DisplayName} nearby";
	}

	private void TryRespawn()
	{
		var loc = OffshoreGameController.Instance?.Progression?.CurrentLocationId ?? "old_dock";
		if ( string.Equals( loc, _spawnedForLocation, StringComparison.OrdinalIgnoreCase ) && _swimmers.Count > 0 )
			return;

		ClearAll();
		_spawnedForLocation = loc;
		SpawnForLocation( loc );
	}

	private void SpawnForLocation( string locationId )
	{
		var species = FishCatalog.ForLocation( locationId ).ToList();
		if ( species.Count == 0 )
			species = FishCatalog.All.Take( 4 ).ToList();

		// Schools of common fish + a few solos.
		var schoolCount = 0;
		foreach ( var fish in species )
		{
			if ( fish.Rarity <= FishRarity.Common && schoolCount < 3 )
			{
				SpawnSchool( fish, members: Game.Random.Int( 3, 5 ) );
				schoolCount++;
			}
			else if ( fish.Rarity <= FishRarity.Uncommon )
			{
				SpawnSolo( fish );
				if ( Game.Random.Float() < 0.55f )
					SpawnSolo( fish );
			}
		}

		// Fill if sparse.
		while ( _swimmers.Count < 10 && species.Count > 0 )
			SpawnSolo( species[Game.Random.Int( 0, species.Count - 1 )] );

		SpawnSeagulls( 3 );
		Log.Info( $"[Offshore] Ambient wildlife: {_swimmers.Count} fish, {_seagulls.Count} seagulls @ {locationId}" );
	}

	private void SpawnSchool( FishDefinition fish, int members )
	{
		var homeX = Game.Random.Float( OffshoreConstants.WaterMinX + 4f, OffshoreConstants.WaterMaxX * 0.55f );
		var homeZ = RestZ() + Game.Random.Float( -1.2f, 0.8f );
		var patrol = Game.Random.Float( 3.5f, 7f );
		var speed = Game.Random.Float( 1.1f, 2.0f ) * Math.Clamp( fish.Speed, 0.5f, 1.4f );
		var size = SizeFor( fish );

		for ( var i = 0; i < members; i++ )
		{
			var ang = (i / (float)members) * MathF.PI * 2f;
			var ox = MathF.Cos( ang ) * Game.Random.Float( 0.6f, 1.4f );
			var oz = MathF.Sin( ang ) * Game.Random.Float( 0.25f, 0.7f );
			AddSwimmer( fish, homeX, homeZ, patrol, speed, ox, oz, size );
		}
	}

	private void SpawnSolo( FishDefinition fish )
	{
		var homeX = Game.Random.Float( OffshoreConstants.WaterMinX + 3f, OffshoreConstants.WaterMaxX * 0.65f );
		var homeZ = RestZ() + Game.Random.Float( -2.0f, 1.0f );
		var patrol = Game.Random.Float( 4f, 10f );
		var speed = Game.Random.Float( 0.9f, 1.8f ) * Math.Clamp( fish.Speed, 0.5f, 1.4f );
		AddSwimmer( fish, homeX, homeZ, patrol, speed, 0f, 0f, SizeFor( fish ) );
	}

	private void AddSwimmer(
		FishDefinition fish,
		float homeX,
		float homeZ,
		float patrol,
		float speed,
		float ox,
		float oz,
		Vector2 size )
	{
		var path = string.IsNullOrWhiteSpace( fish.SpritePath ) ? OffshoreSprites.Paths.FishBluegill : fish.SpritePath;
		var sprite = OffshoreSprites.Spawn(
			GameObject,
			path,
			size,
			Vector3.Zero,
			$"AmbientFish_{fish.Id}" );
		sprite.AlphaCutoff = 0.04f;

		var s = new Swimmer
		{
			Go = sprite.GameObject,
			Sprite = sprite,
			FishId = fish.Id,
			HomeX = homeX,
			HomeZ = Math.Clamp( homeZ, OffshoreConstants.WaterMinZ + 2f, OffshoreConstants.PlayerStartZ - 0.8f ),
			PatrolHalf = patrol,
			Speed = speed,
			Phase = Game.Random.Float( 0f, MathF.PI * 2f ),
			BobAmp = Game.Random.Float( 0.15f, 0.45f ),
			SchoolOffsetX = ox,
			SchoolOffsetZ = oz,
			Size = size,
		};

		_swimmers.Add( s );
	}

	private void SpawnSeagulls( int count )
	{
		for ( var i = 0; i < count; i++ )
		{
			var sprite = OffshoreSprites.Spawn(
				GameObject,
				OffshoreSprites.Paths.Seagull,
				new Vector2( 1.1f, 0.85f ),
				Vector3.Zero,
				$"Seagull_{i}" );

			_seagulls.Add( new Seagull
			{
				Go = sprite.GameObject,
				Sprite = sprite,
				HomeX = Game.Random.Float( OffshoreConstants.WaterMinX + 6f, 40f ),
				Phase = Game.Random.Float( 0f, MathF.PI * 2f ),
				Speed = Game.Random.Float( 0.35f, 0.7f ),
				Altitude = Game.Random.Float( 4.5f, 8.5f ),
			} );
		}
	}

	private void TickSwimmers()
	{
		var t = Time.Now;
		foreach ( var s in _swimmers )
		{
			if ( s.Go is null || !s.Go.IsValid() )
				continue;

			var x = s.HomeX + MathF.Sin( t * s.Speed + s.Phase ) * s.PatrolHalf + s.SchoolOffsetX;
			var z = s.HomeZ + MathF.Sin( t * (s.Speed * 1.7f) + s.Phase * 1.3f ) * s.BobAmp + s.SchoolOffsetZ;
			var vx = MathF.Cos( t * s.Speed + s.Phase ) * s.PatrolHalf * s.Speed;

			s.FacingRight = vx >= 0f;
			s.Go.WorldPosition = new Vector3( x, FishPlaneY, z );

			var scale = s.Go.LocalScale;
			var sx = MathF.Abs( scale.x ) < 0.001f ? 1f : MathF.Abs( scale.x );
			// Sprites face right by default; flip when swimming left.
			s.Go.LocalScale = new Vector3( s.FacingRight ? sx : -sx, scale.y, scale.z );
			s.Sprite.Size = s.Size;
		}
	}

	private void TickSeagulls()
	{
		var t = Time.Now;
		foreach ( var g in _seagulls )
		{
			if ( g.Go is null || !g.Go.IsValid() )
				continue;

			var x = g.HomeX + MathF.Sin( t * g.Speed + g.Phase ) * 12f;
			var z = g.Altitude + MathF.Sin( t * g.Speed * 2.2f + g.Phase ) * 0.6f;
			var vx = MathF.Cos( t * g.Speed + g.Phase );
			g.Go.WorldPosition = new Vector3( x, FishPlaneY - 2f, z );

			var scale = g.Go.LocalScale;
			var sx = MathF.Abs( scale.x ) < 0.001f ? 1f : MathF.Abs( scale.x );
			g.Go.LocalScale = new Vector3( vx >= 0f ? sx : -sx, scale.y, scale.z );
		}
	}

	private void ClearAll()
	{
		foreach ( var s in _swimmers )
		{
			if ( s.Go is not null && s.Go.IsValid() )
				s.Go.Destroy();
		}
		_swimmers.Clear();

		foreach ( var g in _seagulls )
		{
			if ( g.Go is not null && g.Go.IsValid() )
				g.Go.Destroy();
		}
		_seagulls.Clear();
	}

	private static float RestZ() =>
		OffshoreConstants.PlayerStartZ - OffshoreConstants.BobberBelowPlayerZ;

	private static Vector2 SizeFor( FishDefinition fish )
	{
		var scale = fish.Rarity switch
		{
			FishRarity.Common => 1f,
			FishRarity.Uncommon => 1.15f,
			FishRarity.Rare => 1.35f,
			_ => 1.55f
		};
		return new Vector2( 1.15f * scale, 0.65f * scale );
	}
}
