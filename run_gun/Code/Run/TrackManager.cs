namespace RunGun;

/// <summary>
/// Owns everything that lives on the track: the ground/walls, gate rows, enemies, projectiles,
/// and the bullet pool. Runs the whole simulation each frame.
/// </summary>
public sealed class TrackManager : Component
{
	public static TrackManager Instance { get; private set; }

	private sealed class Bullet
	{
		public GameObject Go;
		public float X;
		public float Y;
		public float BaseDamage;
		public float Life;
		public int PierceRemaining;
		public bool Active;
	}

	private sealed class BossState
	{
		public Enemy Enemy;
		public float AttackTimer;
		public int Phase;
		public bool SweepLeft = true;
	}

	private readonly List<Gate> _gates = new();
	private readonly List<Hazard> _hazards = new();
	private readonly List<Enemy> _enemies = new();
	private readonly List<Enemy> _segmentEnemies = new();
	private readonly List<Projectile> _projectiles = new();
	private readonly List<Bullet> _bullets = new();
	private readonly Stack<Bullet> _bulletPool = new();
	private readonly List<BossState> _bosses = new();
	private readonly SectionPacing _pacing = new();

	private ModelRenderer _groundRenderer;
	private ModelRenderer _wallLeft;
	private ModelRenderer _wallRight;

	private bool _worldBuilt;
	private float _spawnCursor;
	private float _lastMeters;
	private int _nextBossMilestone = 1;
	private int _spawnSeed;

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
	}

	protected override void OnStart() => EnsureWorld();

	public void ResetRun( float startX )
	{
		EnsureWorld();
		ClearContent();
		_spawnCursor = startX + GameConstants.ContentStartX;
		_lastMeters = 0f;
		_nextBossMilestone = 1;
		_spawnSeed = Game.Random.Int( 0, 99999 );
		_pacing.Update( 0f );
		ApplyBiomeTint();
	}

	private void ClearContent()
	{
		foreach ( var g in _gates ) g?.GameObject?.Destroy();
		foreach ( var h in _hazards ) h?.GameObject?.Destroy();
		foreach ( var e in _enemies ) e?.GameObject?.Destroy();
		foreach ( var p in _projectiles ) p?.Kill();
		_gates.Clear();
		_hazards.Clear();
		_enemies.Clear();
		_projectiles.Clear();
		_bosses.Clear();

		foreach ( var b in _bullets ) RecycleBullet( b );
		_bullets.Clear();
	}

	public void Fire( Vector3 origin )
	{
		var core = GameCore.Instance;
		if ( core is null || !core.Run.Active ) return;

		var run = core.Run;
		var count = run.BulletsPerShot;
		var dmg = run.BulletDamage;

		// Spread lanes across (but never past) the crowd's width so the wall reads as fire
		// coming from the whole squad rather than a single point.
		var maxWidth = (GameConstants.LaneHalf - 10f) * 2f;
		var spacing = count <= 1 ? 0f : MathF.Min( GameConstants.SquadLaneSpacing, maxWidth / (count - 1) );
		var width = (count - 1) * spacing;

		for ( var i = 0; i < count; i++ )
		{
			var offY = count <= 1 ? 0f : -width * 0.5f + i * spacing;
			SpawnBullet( origin.x, origin.y + offY, origin.z, dmg, run.Pierce );
		}

		Sfx.Play( Sfx.Shoot );
	}

	public void SpawnProjectile( Vector3 origin, Vector3 direction, float damage, Color tint )
	{
		var go = new GameObject( true, "Projectile" );
		var projectile = go.Components.Create<Projectile>();
		projectile.Setup( origin, direction, damage, tint );
		_projectiles.Add( projectile );
	}

	public void SpawnEnemyDirect( EnemySpawnSpec spec )
	{
		var core = GameCore.Instance;
		if ( core is null ) return;
		CreateEnemy( spec, core.Daily );
	}

	protected override void OnUpdate()
	{
		var core = GameCore.Instance;
		if ( core is null || !core.Run.Active ) return;

		var player = core.Player;
		if ( !player.IsValid() ) return;

		var dt = Time.Delta;
		var pos = player.WorldPosition;
		var run = core.Run;

		run.Tick( dt );
		run.FurthestX = MathF.Max( run.FurthestX, pos.x );

		var meters = run.DistanceMeters;
		_pacing.Update( meters );
		if ( (int)meters / GameConstants.SectionCycleMeters != (int)_lastMeters / GameConstants.SectionCycleMeters )
			ApplyBiomeTint();

		AdvanceSpawns( pos.x, run, core );
		UpdateBullets( dt, pos.x, core );
		UpdateEnemies( dt, pos, run, core );
		UpdateProjectiles( dt, pos, run );
		UpdateBosses( dt, pos, run );
		UpdateGates( pos, run );
		UpdateHazards( pos, run, core );
		AccrueDistanceCoins( run, core.Upgrades );
		HandleMilestones( pos, run );
		DespawnBehind( pos.x );

		_lastMeters = meters;
		core.Missions.Update( run );
	}

	private void AdvanceSpawns( float playerX, RunState run, GameCore core )
	{
		while ( _spawnCursor < playerX + GameConstants.SpawnAhead )
		{
			var meters = MathF.Max( 0f, (_spawnCursor - run.StartX) / GameConstants.UnitsPerMeter );

			if ( meters >= _nextBossMilestone * GameConstants.BossIntervalMeters )
			{
				SpawnBoss( _spawnCursor, meters, core.Daily );
				_nextBossMilestone++;
			}
			else
			{
				SpawnGateRow( _spawnCursor, meters, core );

				var packChance = Difficulty.PackSpawnChance( meters );
				if ( _pacing.Current == RunSection.Breather )
					packChance *= 0.5f;

				if ( Game.Random.Float( 0f, 1f ) < packChance )
					SpawnEnemyPack( _spawnCursor + GameConstants.GateSpacing * 0.5f, meters, core.Daily );

				// Hazards sit between gate rows so dodging them is its own beat.
				if ( _pacing.Current != RunSection.Breather && Game.Random.Float( 0f, 1f ) < Difficulty.HazardChance( meters ) )
					SpawnHazardRow( _spawnCursor + GameConstants.GateSpacing * 0.75f, meters );
			}

			_spawnCursor += GameConstants.GateSpacing;
		}
	}

	private void SpawnGateRow( float x, float meters, GameCore core )
	{
		// Every row is a tension between getting stronger and staying alive: one side is
		// always offense, the other survival/economy. Which side is which is randomised.
		var offenseOnLeft = Game.Random.Float( 0f, 1f ) < 0.5f;
		var left = RollGate( meters, core, offense: offenseOnLeft );
		var right = RollGate( meters, core, offense: !offenseOnLeft );
		CreateGate( x, left, true );
		CreateGate( x, right, false );
	}

	// Quality stats sharpen each runner; support keeps you alive / rich. But the star of every
	// row is CREW — growing the crowd is both power and survival, so it dominates the pools.
	private static readonly BuildStat[] QualityPool =
		{ BuildStat.Damage, BuildStat.FireRate, BuildStat.CritChance, BuildStat.Pierce };
	private static readonly BuildStat[] SupportPool =
		{ BuildStat.Shield, BuildStat.CoinMult };

	private (BuildStat Stat, GateOp Op, float Value) RollGate( float meters, GameCore core, bool offense )
	{
		var luck = core.Upgrades.GateLuck;
		var daily = core.Daily.ActiveModifier;

		// Crowd gates are the hook. The offense side is almost always CREW; the support side is
		// crew often enough that most rows become the classic "+15 vs x2" pick.
		var squadChance = offense ? 0.72f : 0.45f;
		BuildStat stat;
		if ( daily == DailyModifier.CritGatesOnly && !offense )
			stat = Game.Random.Float( 0f, 1f ) < 0.5f ? BuildStat.Shield : BuildStat.CoinMult;
		else if ( Game.Random.Float( 0f, 1f ) < squadChance )
			stat = BuildStat.Squad;
		else if ( offense )
			stat = QualityPool[Game.Random.Int( 0, QualityPool.Length - 1 )];
		else
			stat = SupportPool[Game.Random.Int( 0, SupportPool.Length - 1 )];

		// Crew never multiplies — only additive +N — so the army grows linearly, not exponentially.
		// Multiply gates stay for the quality stats, which don't snowball the whole run.
		var multAllowed = stat is BuildStat.Damage or BuildStat.FireRate or BuildStat.CoinMult;
		var useMult = multAllowed && Game.Random.Float( 0f, 1f ) < 0.16f + luck * 0.06f;
		var op = useMult ? GateOp.Mult : GateOp.Add;

		var sectionMult = _pacing.GateBonusMult;
		var value = stat switch
		{
			BuildStat.Squad =>
				GameConstants.GateSquadAddMin
				+ Game.Random.Float( 0f, GameConstants.GateSquadAddMax - GameConstants.GateSquadAddMin )
				+ meters * GameConstants.GateSquadDistanceScale + luck * 0.5f,
			BuildStat.Damage => useMult
				? Game.Random.Float( 1.25f, 1.6f ) + luck * 0.05f
				: GameConstants.GateAddStartMin + Game.Random.Float( 0f, 3f ) + meters * 0.03f + luck,
			BuildStat.FireRate => useMult
				? Game.Random.Float( 1.2f, 1.45f )
				: 0.08f + luck * 0.02f,
			BuildStat.CritChance => 0.03f + luck * 0.01f,
			BuildStat.Pierce => 1f,
			BuildStat.Shield => 6f + meters * 0.04f,
			BuildStat.CoinMult => useMult ? Game.Random.Float( 1.15f, 1.35f ) : 0.08f + luck * 0.02f,
			_ => 1f,
		};

		if ( op == GateOp.Add ) value *= sectionMult;
		return (stat, op, value);
	}

	private void CreateGate( float x, (BuildStat Stat, GateOp Op, float Value) roll, bool leftSide )
	{
		var go = new GameObject( true, "Gate" );
		go.WorldPosition = new Vector3( x, 0f, 0f );
		var gate = go.Components.Create<Gate>();
		gate.Setup( roll.Stat, roll.Op, roll.Value, leftSide );
		_gates.Add( gate );
	}

	private void SpawnHazardRow( float x, float meters )
	{
		var laneMin = -GameConstants.LaneHalf;
		var laneMax = GameConstants.LaneHalf;
		var gap = Difficulty.HazardGap( meters );

		// Three layouts, all guaranteeing at least one strafe-able opening.
		switch ( Game.Random.Int( 0, 2 ) )
		{
			case 0: // Side wall — gap on the opposite side
				if ( Game.Random.Float( 0f, 1f ) < 0.5f )
					CreateHazard( x, laneMin, laneMax - gap );
				else
					CreateHazard( x, laneMin + gap, laneMax );
				break;

			case 1: // Center block — gaps on both edges
				var half = gap * 0.5f;
				CreateHazard( x, laneMin, -half );
				CreateHazard( x, half, laneMax );
				break;

			default: // Offset gap — single opening somewhere across the lane
				var gapStart = Game.Random.Float( laneMin + 20f, laneMax - gap - 20f );
				if ( gapStart - laneMin > 20f )
					CreateHazard( x, laneMin, gapStart );
				if ( laneMax - (gapStart + gap) > 20f )
					CreateHazard( x, gapStart + gap, laneMax );
				break;
		}
	}

	private void CreateHazard( float x, float minY, float maxY )
	{
		if ( maxY - minY < 24f ) return;
		var go = new GameObject( true, "Hazard" );
		go.WorldPosition = new Vector3( x, 0f, 0f );
		var hazard = go.Components.Create<Hazard>();
		hazard.Setup( minY, maxY );
		_hazards.Add( hazard );
	}

	private void SpawnEnemyPack( float x, float meters, DailyChallengeSystem daily )
	{
		var specs = EnemyFormation.Generate( x, meters, _pacing.Current, _spawnSeed + (int)x );
		var mult = _pacing.EnemyCountMult;
		// Only pile on extra bodies once the run has ramped up; keep the opening clean.
		var extra = mult > 1.2f && Difficulty.Ramp( meters ) > 0.4f ? 1 : 0;
		for ( var i = 0; i < specs.Count + extra; i++ )
		{
			var spec = i < specs.Count ? specs[i] : specs[Game.Random.Int( 0, specs.Count - 1 )];
			CreateEnemy( spec, daily );
		}
	}

	private void SpawnBoss( float x, float meters, DailyChallengeSystem daily )
	{
		var health = (GameConstants.EnemyBaseHealth + meters * GameConstants.EnemyHealthPerMeter) * GameConstants.BossHealthMult;
		if ( daily.ActiveModifier is DailyModifier.DoubleEnemyHp or DailyModifier.HardMode )
			health *= daily.ActiveModifier == DailyModifier.HardMode ? 1.6f : 2f;

		var spec = new EnemySpawnSpec { Type = EnemyType.Boss, X = x, Y = 0f, Health = health, Elite = true };
		var enemy = CreateEnemy( spec, daily );
		_bosses.Add( new BossState { Enemy = enemy, AttackTimer = GameConstants.BossAttackInterval } );
		Sfx.Play( Sfx.Boss );
	}

	private Enemy CreateEnemy( EnemySpawnSpec spec, DailyChallengeSystem daily )
	{
		var go = new GameObject( true, spec.Type == EnemyType.Boss ? "Boss" : "Enemy" );
		go.WorldPosition = new Vector3( spec.X, 0f, 0f );
		var enemy = go.Components.Create<Enemy>();
		enemy.Setup( spec, daily );
		_enemies.Add( enemy );
		return enemy;
	}

	private void SpawnBullet( float x, float y, float z, float damage, int pierce )
	{
		var b = _bulletPool.Count > 0 ? _bulletPool.Pop() : CreateBullet();
		b.X = x;
		b.Y = y;
		b.BaseDamage = damage;
		b.Life = GameConstants.BulletLife;
		b.PierceRemaining = pierce;
		b.Active = true;
		b.Go.Enabled = true;
		b.Go.WorldPosition = new Vector3( x, y, z );
		_bullets.Add( b );
	}

	private Bullet CreateBullet()
	{
		var go = new GameObject( true, "Bullet" );
		go.LocalScale = MeshPrimitives.BoxScale( new Vector3( 30f, 12f, 12f ) );
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = MeshPrimitives.Box;
		mr.MaterialOverride = MeshPrimitives.Mat;
		mr.Tint = new Color( 1f, 0.92f, 0.35f );
		return new Bullet { Go = go };
	}

	private void RecycleBullet( Bullet b )
	{
		if ( b?.Go is null ) return;
		b.Active = false;
		b.Go.Enabled = false;
		_bulletPool.Push( b );
	}

	private void UpdateBullets( float dt, float playerX, GameCore core )
	{
		var step = GameConstants.BulletSpeed * dt;

		for ( var i = _bullets.Count - 1; i >= 0; i-- )
		{
			var b = _bullets[i];
			var oldX = b.X;
			b.X += step;
			b.Life -= dt;

			var consumed = ProcessBulletSegment( b, oldX, b.X, core );

			if ( consumed || b.Life <= 0f || b.X > playerX + GameConstants.SpawnAhead )
			{
				RecycleBullet( b );
				_bullets.RemoveAt( i );
				continue;
			}

			b.Go.WorldPosition = new Vector3( b.X, b.Y, b.Go.WorldPosition.z );
		}

		CleanupDeadEnemies();
	}

	/// <summary>
	/// Advances a bullet across a segment. Gates it flies over on its lane half are pumped but
	/// do NOT block the shot, so enemies behind a gate are always killable. Enemies are hit
	/// nearest-first, respecting pierce. Returns true when the bullet should be removed.
	/// </summary>
	private bool ProcessBulletSegment( Bullet b, float oldX, float newX, GameCore core )
	{
		// Pass-through pump of any unapplied gate crossed on this half.
		foreach ( var g in _gates )
		{
			if ( g.Applied ) continue;
			if ( g.X <= oldX || g.X > newX ) continue;
			if ( !g.Contains( b.Y ) ) continue;
			g.Hit( core.Run );
			Sfx.Play( Sfx.GateHit );
		}

		_segmentEnemies.Clear();
		foreach ( var e in _enemies )
		{
			if ( e.Dead ) continue;
			if ( e.X <= oldX || e.X > newX ) continue;
			if ( MathF.Abs( b.Y - e.Y ) > GameConstants.EnemyRadius ) continue;
			_segmentEnemies.Add( e );
		}

		if ( _segmentEnemies.Count == 0 )
			return false;

		_segmentEnemies.Sort( ( l, r ) => l.X.CompareTo( r.X ) );

		foreach ( var e in _segmentEnemies )
		{
			if ( e.Dead ) continue;

			var fromFront = e.X > oldX;
			var damage = core.Run.ResolveDamage( b.BaseDamage, out var crit );
			if ( e.Hit( damage, fromFront, out var dealt ) )
			{
				var coins = e.CoinValue( core.Upgrades, core.Daily ) * core.Run.ComboMultiplier * core.Run.RunCoinMult;
				core.Run.Coins += coins;
				core.Run.OnKill( e );
				Sfx.Play( crit ? Sfx.Crit : Sfx.EnemyKill );
				if ( e.Type == EnemyType.Boss )
					core.Player?.ApplyCameraShake( GameConstants.CamShakeHurt * 1.3f );
				else if ( e.IsElite )
					core.Player?.ApplyCameraShake( 6f );
				if ( e.Type == EnemyType.Splitter )
					SpawnSplitterChildren( e );
			}
			else if ( crit )
			{
				Sfx.Play( Sfx.Crit );
			}

			VfxManager.Instance?.SpawnDamageNumber( e.WorldPosition, dealt, crit );

			if ( b.PierceRemaining > 0 )
			{
				b.PierceRemaining--;
				continue;
			}

			return true;
		}

		return false;
	}

	private void SpawnSplitterChildren( Enemy parent )
	{
		var core = GameCore.Instance;
		if ( core is null ) return;

		for ( var i = 0; i < 2; i++ )
		{
			CreateEnemy( new EnemySpawnSpec
			{
				Type = EnemyType.Swarm,
				X = parent.X + 20f,
				Y = parent.Y + (i == 0 ? -45f : 45f),
				Health = parent.MaxHealth * 0.35f,
			}, core.Daily );
		}
	}

	private void UpdateEnemies( float dt, Vector3 playerPos, RunState run, GameCore core )
	{
		var reach = GameConstants.EnemyRadius + GameConstants.PlayerRadius;
		var advanceMult = Difficulty.AdvanceSpeedMult( run.DistanceMeters );

		foreach ( var e in _enemies )
		{
			if ( e.Dead ) continue;

			e.Advance( dt, core.Daily, advanceMult );
			e.TrySpit( playerPos, this );

			if ( e.X <= playerPos.x + GameConstants.PlayerRadius && MathF.Abs( e.Y - playerPos.y ) < reach )
			{
				var cost = e.Type == EnemyType.Boss
					? GameConstants.SquadBossContactCost
					: Difficulty.ContactSquadCost( run.DistanceMeters );
				LoseCrew( run, core, cost, playerPos, GameConstants.CamShakeHurt );
				e.Hit( e.MaxHealth, true, out _ );
			}
		}

		CleanupDeadEnemies();
	}

	private void UpdateBosses( float dt, Vector3 playerPos, RunState run )
	{
		var core = GameCore.Instance;
		if ( core is null ) return;

		for ( var i = _bosses.Count - 1; i >= 0; i-- )
		{
			var boss = _bosses[i];
			if ( boss.Enemy.Dead )
			{
				_bosses.RemoveAt( i );
				continue;
			}

			boss.AttackTimer -= dt;
			if ( boss.AttackTimer > 0f ) continue;

			boss.AttackTimer = GameConstants.BossAttackInterval;
			boss.Phase = (boss.Phase + 1) % 3;

			switch ( boss.Phase )
			{
				case 0:
					boss.Enemy.TrySpit( playerPos, this );
					break;
				case 1:
					for ( var j = 0; j < 3; j++ )
					{
						SpawnEnemyDirect( new EnemySpawnSpec
						{
							Type = EnemyType.Swarm,
							X = boss.Enemy.X + 40f + j * 50f,
							Y = Game.Random.Float( -120f, 120f ),
							Health = boss.Enemy.MaxHealth * 0.08f,
						} );
					}
					break;
				default:
					var sweptLeft = boss.SweepLeft;
					boss.SweepLeft = !boss.SweepLeft;
					var inLane = sweptLeft ? playerPos.y < 0f : playerPos.y >= 0f;
					if ( inLane && MathF.Abs( playerPos.x - boss.Enemy.X ) < 120f )
						LoseCrew( run, core, GameConstants.SquadBossContactCost, playerPos, GameConstants.CamShakeHurt * 0.9f );
					break;
			}
		}
	}

	private void UpdateProjectiles( float dt, Vector3 playerPos, RunState run )
	{
		for ( var i = _projectiles.Count - 1; i >= 0; i-- )
		{
			var p = _projectiles[i];
			if ( p.Dead || !p.IsValid() )
			{
				_projectiles.RemoveAt( i );
				continue;
			}

			p.Tick( dt );

			if ( Vector3.DistanceBetween( p.WorldPosition, playerPos.WithZ( p.WorldPosition.z ) ) < GameConstants.PlayerRadius + 14f )
			{
				LoseCrew( run, GameCore.Instance, GameConstants.ProjectileSquadCost, playerPos, GameConstants.CamShakeHurt * 0.6f );
				p.Kill();
				_projectiles.RemoveAt( i );
			}
		}
	}

	private void UpdateGates( Vector3 playerPos, RunState run )
	{
		foreach ( var g in _gates )
		{
			if ( g.Applied || g.X > playerPos.x ) continue;

			g.Applied = true;

			if ( g.Contains( playerPos.y ) )
			{
				g.Apply( run );
				Sfx.Play( Sfx.GatePass );
			}

			g.GameObject.Enabled = false;
		}
	}

	private void UpdateHazards( Vector3 playerPos, RunState run, GameCore core )
	{
		foreach ( var h in _hazards )
		{
			if ( h.Triggered || h.X > playerPos.x ) continue;

			h.Triggered = true;

			if ( h.Contains( playerPos.y ) )
			{
				// A hazard rips a whole chunk of the crowd away — the big, felt punishment.
				LoseCrew( run, core, run.HazardSquadCost(), playerPos, GameConstants.CamShakeHurt * 1.6f );
			}
		}
	}

	private void AccrueDistanceCoins( RunState run, UpgradeSystem upgrades )
	{
		var meters = run.DistanceMeters;
		var delta = meters - _lastMeters;
		if ( delta > 0f )
			run.Coins += delta * GameConstants.DistanceCoinPerMeter * run.EffectiveCoinMult / upgrades.CoinMult;
	}

	private void HandleMilestones( Vector3 playerPos, RunState run )
	{
		if ( run.PendingMilestone <= 0 ) return;
		VfxManager.Instance?.SpawnMilestone( playerPos, $"{run.PendingMilestone}m!" );
		run.PendingMilestone = 0;
	}

	/// <summary>Applies a crowd loss and fires the feedback (shake, sound, floating "-N") when it lands.</summary>
	private void LoseCrew( RunState run, GameCore core, int cost, Vector3 pos, float shake )
	{
		var before = run.SquadInt;
		run.LoseSquad( cost );
		var lost = before - run.SquadInt;
		if ( lost <= 0 ) return;   // absorbed by shield or invulnerable during overdrive

		Sfx.Play( Sfx.Hurt );
		core?.Player?.ApplyCameraShake( shake );
		VfxManager.Instance?.SpawnCrewLoss( pos.WithZ( GameConstants.BodyHeight ), lost );
	}

	private void CleanupDeadEnemies()
	{
		for ( var i = _enemies.Count - 1; i >= 0; i-- )
		{
			if ( !_enemies[i].Dead ) continue;
			_enemies[i].GameObject?.Destroy();
			_enemies.RemoveAt( i );
		}
	}

	private void DespawnBehind( float playerX )
	{
		var cutoff = playerX - GameConstants.DespawnBehind;

		for ( var i = _gates.Count - 1; i >= 0; i-- )
		{
			if ( _gates[i].X >= cutoff ) continue;
			_gates[i].GameObject?.Destroy();
			_gates.RemoveAt( i );
		}

		for ( var i = _hazards.Count - 1; i >= 0; i-- )
		{
			if ( _hazards[i].X >= cutoff ) continue;
			_hazards[i].GameObject?.Destroy();
			_hazards.RemoveAt( i );
		}

		for ( var i = _enemies.Count - 1; i >= 0; i-- )
		{
			if ( _enemies[i].X >= cutoff ) continue;
			_enemies[i].GameObject?.Destroy();
			_enemies.RemoveAt( i );
		}
	}

	private void ApplyBiomeTint()
	{
		if ( !_worldBuilt ) return;
		var (ground, wall) = SectionPacing.BiomeColors( _pacing.BiomeIndex );
		if ( _groundRenderer.IsValid() ) _groundRenderer.Tint = ground;
		if ( _wallLeft.IsValid() ) _wallLeft.Tint = wall;
		if ( _wallRight.IsValid() ) _wallRight.Tint = wall;
	}

	private void EnsureWorld()
	{
		if ( _worldBuilt ) return;
		_worldBuilt = true;
		_groundRenderer = BuildGround();
		_wallLeft = BuildWall( GameConstants.LaneHalf + 20f );
		_wallRight = BuildWall( -(GameConstants.LaneHalf + 20f) );
	}

	private ModelRenderer BuildGround()
	{
		var go = new GameObject( true, "Ground" );
		go.WorldPosition = new Vector3( GameConstants.TrackLength * 0.5f - 1000f, 0f, 0f );
		go.LocalScale = MeshPrimitives.QuadScale( GameConstants.TrackLength, GameConstants.LaneHalf * 2f + 200f );
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = MeshPrimitives.Quad;
		mr.MaterialOverride = MeshPrimitives.Mat;
		mr.Tint = new Color( 0.32f, 0.34f, 0.38f );
		return mr;
	}

	private ModelRenderer BuildWall( float y )
	{
		var go = new GameObject( true, "Wall" );
		go.WorldPosition = new Vector3( GameConstants.TrackLength * 0.5f - 1000f, y, GameConstants.WallHeight * 0.5f );
		go.LocalScale = MeshPrimitives.BoxScale( new Vector3( GameConstants.TrackLength, 24f, GameConstants.WallHeight ) );
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = MeshPrimitives.Box;
		mr.MaterialOverride = MeshPrimitives.Mat;
		mr.Tint = new Color( 0.22f, 0.24f, 0.29f );
		return mr;
	}
}
