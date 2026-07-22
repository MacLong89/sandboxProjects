namespace CatchACritter;

/// <summary>
/// A wild critter. Host-owned: wandering and fleeing run on the host, the
/// transform syncs to everyone. Visuals are rebuilt locally from synced genes.
/// </summary>
public sealed class CritterAgent : Component
{
	[Sync( SyncFlags.FromHost )] public string SpeciesId { get; set; }
	[Sync( SyncFlags.FromHost )] public bool Shiny { get; set; }
	[Sync( SyncFlags.FromHost )] public float SizeRoll { get; set; } = 1f;
	[Sync( SyncFlags.FromHost )] public bool Caught { get; set; }

	public SpeciesDef Def => SpeciesCatalog.Get( SpeciesId );

	GameObject _body;
	TextRenderer _plate;
	string _builtFor;

	// Modeled species (scratch-v6 animals) play real gait sequences.
	SkinnedModelRenderer _modelRenderer;
	string _seqPrefix;
	string _seqPlaying;
	float _bodyBaseScale = 1f;
	Vector3 _lastGaitPos;
	float _gaitSpeed;

	// Host-side brain
	Vector3 _wanderTarget;
	TimeUntil _nextWander;
	TimeUntil _despawn;
	float _popScale = 1f;
	TimeSince _spawned;

	protected override void OnStart()
	{
		_spawned = 0;
		_nextWander = Game.Random.Float( 0.5f, 2f );
	}

	protected override void OnUpdate()
	{
		EnsureBody();
		AnimateLocal();

		if ( !Networking.IsHost ) return;

		if ( Caught )
		{
			if ( _despawn ) GameObject.Destroy();
			return;
		}

		TickBrain();
	}

	void EnsureBody()
	{
		if ( Def is null ) return;
		var key = $"{SpeciesId}:{Shiny}";
		if ( _builtFor == key && _body.IsValid() ) return;

		_body?.Destroy();
		_body = CritterBody.Build( GameObject, Def, Shiny, SizeRoll );
		_builtFor = key;

		// Modeled bodies carry a normalization scale that the pop-in animation
		// must preserve, plus a renderer we drive with gait sequences.
		_bodyBaseScale = _body.LocalScale.x;
		_modelRenderer = _body.Components.Get<SkinnedModelRenderer>();
		_seqPrefix = SpeciesCatalog.SkinFor( SpeciesId )?.SeqPrefix;
		_seqPlaying = null;
		_lastGaitPos = WorldPosition;

		if ( !_plate.IsValid() )
		{
			var plateGo = new GameObject( true, "Plate" );
			plateGo.SetParent( GameObject );
			plateGo.LocalPosition = Vector3.Up * (95f * Def.Size * SizeRoll + 26f);
			_plate = plateGo.Components.Create<TextRenderer>();
			_plate.Scale = 0.14f;
			_plate.FontSize = 32;
			_plate.Billboard = TextRenderer.BillboardMode.YOnly;
			_plate.HorizontalAlignment = TextRenderer.HAlignment.Center;
		}

		var rarityTag = Def.Rarity >= Rarity.Rare ? $" [{Def.Rarity}]" : "";
		_plate.Text = Shiny ? $"* SHINY {Def.Name} *" : $"{Def.Name}{rarityTag}";
		_plate.Color = Shiny ? CritterBody.ShinyGold : SpeciesCatalog.RarityColor( Def.Rarity );
	}

	void AnimateLocal()
	{
		if ( !_body.IsValid() || Def is null ) return;

		// Spawn pop-in and shrink when caught. Kit bodies also hop-bob; modeled
		// bodies have real gait animations instead.
		var grow = MathF.Min( 1f, _spawned * 3.5f );
		if ( Caught ) _popScale = MathF.Max( 0f, _popScale - Time.Delta * 5f );
		_body.LocalScale = Vector3.One * (_bodyBaseScale * grow * _popScale);

		if ( _modelRenderer.IsValid() )
		{
			_body.LocalPosition = Vector3.Zero;
			UpdateGait();
		}
		else
		{
			var bob = MathF.Abs( MathF.Sin( Time.Now * 5f + WorldPosition.x * 0.01f ) ) * 6f;
			_body.LocalPosition = Vector3.Up * bob * grow * _popScale;
		}

		// Nameplate only near the local player — keeps the screen calm.
		if ( _plate.IsValid() )
		{
			var local = CritterPlayer.Local;
			var show = !Caught && local.IsValid() && Vector3.DistanceBetween( local.WorldPosition, WorldPosition ) < 420f;
			_plate.GameObject.Enabled = show;
		}
	}

	/// <summary>
	/// Picks idle/walk/gallop from observed planar speed, so every client
	/// animates correctly off the synced transform alone.
	/// </summary>
	void UpdateGait()
	{
		var dt = MathF.Max( Time.Delta, 0.001f );
		var planar = (WorldPosition - _lastGaitPos).WithZ( 0 ).Length / dt;
		_lastGaitPos = WorldPosition;
		_gaitSpeed = MathX.Lerp( _gaitSpeed, MathF.Min( planar, 500f ), dt * 10f );

		// Wandering tops out around 82 u/s; fleeing starts at 120.
		var gait = _gaitSpeed > 100f ? "gallop" : _gaitSpeed > 10f ? "walk" : "idle";
		var seq = $"{_seqPrefix}_{gait}";
		if ( _seqPlaying == seq ) return;

		_seqPlaying = seq;
		_modelRenderer.Sequence.Name = seq;
		_modelRenderer.Sequence.Looping = true;
	}

	void TickBrain()
	{
		var def = Def;
		if ( def is null ) return;

		var biome = BiomeCatalog.Get( def.Biome );

		// Flee from the nearest fast-approaching player.
		CritterPlayer threat = null;
		float threatDist = def.FleeRadius;
		foreach ( var p in Scene.GetAllComponents<CritterPlayer>() )
		{
			if ( p.IsSneaking ) continue;
			var d = Vector3.DistanceBetween( p.WorldPosition, WorldPosition );
			if ( d < threatDist ) { threatDist = d; threat = p; }
		}

		Vector3 move;
		float speed;
		if ( threat.IsValid() )
		{
			var away = (WorldPosition - threat.WorldPosition).WithZ( 0 ).Normal;
			move = away;
			speed = def.FleeSpeed;
			_nextWander = 0.2f;
		}
		else
		{
			if ( _nextWander )
			{
				var ang = Game.Random.Float( 0f, MathF.Tau );
				var dist = Game.Random.Float( 0f, biome.Radius * 0.82f );
				_wanderTarget = new Vector3( biome.Center.x + MathF.Cos( ang ) * dist, biome.Center.y + MathF.Sin( ang ) * dist, 0f );
				_nextWander = Game.Random.Float( 2.5f, 6f );
			}

			var to = (_wanderTarget - WorldPosition).WithZ( 0 );
			if ( to.Length < 20f ) { return; }
			move = to.Normal;
			speed = 42f + (int)def.Rarity * 8f;
		}

		var next = WorldPosition + move * speed * Time.Delta;

		// Stay on the biome disc.
		var p2 = new Vector2( next.x, next.y );
		var off = p2 - biome.Center;
		if ( off.Length > biome.Radius - 40f )
		{
			off = off.Normal * (biome.Radius - 40f);
			next = new Vector3( biome.Center.x + off.x, biome.Center.y + off.y, next.z );
		}

		WorldPosition = next.WithZ( 2f );
		if ( move.Length > 0.1f )
			WorldRotation = Rotation.Lerp( WorldRotation, Rotation.LookAt( move.WithZ( 0 ) ), Time.Delta * 6f );
	}

	/// <summary>Client asks the host to catch this critter. Host arbitrates races.</summary>
	[Rpc.Host]
	public void RequestCatch( Guid catcherConnection, int netPower )
	{
		if ( Caught || Def is null ) return;
		if ( netPower < Def.RequiredNetPower ) return;

		Caught = true;
		ConfirmCatch( catcherConnection );

		var spawner = Scene.GetAllComponents<CritterSpawner>().FirstOrDefault();
		spawner?.OnCritterCaught( this );

		_despawn = 0.45f;
	}

	[Rpc.Broadcast]
	void ConfirmCatch( Guid catcherConnection )
	{
		var isMine = Connection.Local?.Id == catcherConnection;
		CatchEffects.CatchPop( WorldPosition, Def, Shiny, isMine );

		if ( isMine )
			PlayerProgress.Local?.OnCaught( Def, Shiny );
	}
}
