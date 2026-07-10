namespace FinalOutpost;

/// <summary>
/// Bullet pool, zombie registry, hit resolution and kill payouts.
/// Towers and defenders fire through here.
/// </summary>
public sealed class CombatSystem : Component
{
	public static CombatSystem Instance { get; private set; }

	private sealed class Bullet
	{
		public GameObject Go;
		public ModelRenderer Renderer;
		public Vector3 Pos;
		public Vector3 Dir;
		public float Damage;
		public float Life;
		public bool Active;
	}

	private static readonly Color DefaultTracer = new( 1f, 0.9f, 0.35f );

	private readonly List<ZombieInstance> _zombies = new();
	private readonly List<Bullet> _activeBullets = new();
	private readonly Stack<Bullet> _pool = new();

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
		ClearAll();
	}

	public IReadOnlyList<ZombieInstance> Zombies => _zombies;
	public int AliveCount
	{
		get
		{
			var n = 0;
			foreach ( var z in _zombies ) if ( !z.Dead ) n++;
			return n;
		}
	}

	public void ClearAll()
	{
		foreach ( var z in _zombies ) z.Go?.Destroy();
		_zombies.Clear();

		foreach ( var b in _activeBullets ) Recycle( b );
		_activeBullets.Clear();

		CombatAudio.Reset();
	}

	public ZombieInstance SpawnZombie( Vector3 pos, int night, ZombieTypeDef typeDef )
	{
		typeDef ??= ZombieCatalog.Get( ZombieKind.Walker );

		var save = GameCore.Instance?.Save;
		if ( save is not null )
			ZombieBestiary.MarkEncountered( save, typeDef.Kind );

		var hp = GameConstants.ZombieHp( night, typeDef.HpMult );
		var dmg = (GameConstants.ZombieBaseDamage + night * GameConstants.ZombieDamagePerNight) * typeDef.DamageMult;
		var spd = (GameConstants.ZombieBaseSpeed + night * GameConstants.ZombieSpeedPerNight) * typeDef.SpeedMult;

		return SpawnZombieInternal( pos, hp, dmg, spd, typeDef );
	}

	private ZombieInstance SpawnZombieInternal( Vector3 pos, float health, float damage, float speed, ZombieTypeDef typeDef )
	{
		var go = new GameObject( true, $"Zombie_{typeDef.Kind}" );
		go.WorldPosition = pos.WithZ( OutpostTerrain.SampleHeight( pos.x, pos.y ) );
		if ( typeDef.Scale != 1f )
			go.LocalScale *= typeDef.Scale;

		var character = go.Components.Create<CharacterModel>();
		character.SetupZombie( typeDef );

		var labelGo = new GameObject( go, true, "Label" );
		labelGo.LocalPosition = new Vector3( 0f, 0f, 68f * typeDef.Scale + 8f );
		var wp = labelGo.Components.Create<Sandbox.WorldPanel>();
		wp.PanelSize = new Vector2( 48f, 24f );
		wp.RenderScale = 0.55f;
		wp.LookAtCamera = true;
		wp.InteractionRange = 0f;
		wp.RenderOptions.Game = true;
		var label = labelGo.Components.Create<UI.WorldLabel>();

		var z = new ZombieInstance
		{
			Go = go,
			Character = character,
			Label = label,
			TypeDef = typeDef,
			Health = health,
			MaxHealth = health,
			Damage = damage,
			Speed = speed,
			ApproachSide = WallApproach.FromWorldPosition( pos, OutpostManager.Instance?.CorePosition ?? Vector3.Zero )
		};
		z.RefreshLabel();
		_zombies.Add( z );
		return z;
	}

	public ZombieInstance NearestZombie( Vector3 from, float range )
	{
		ZombieInstance best = null;
		var bestD = range * range;

		foreach ( var z in _zombies )
		{
			if ( z.Dead ) continue;
			var d = (z.Position - from).LengthSquared;
			if ( d < bestD ) { bestD = d; best = z; }
		}

		return best;
	}

	public void FireBullet( Vector3 origin, Vector3 dir, float damage, string fireSound = null, Color? tracerColor = null, bool playSound = true )
	{
		var b = _pool.Count > 0 ? _pool.Pop() : CreateBullet();
		b.Pos = origin;
		b.Dir = dir.Normal;
		b.Damage = damage;
		b.Life = GameConstants.BulletLife;
		b.Active = true;
		b.Go.Enabled = true;
		b.Go.WorldPosition = origin;
		b.Go.WorldRotation = Rotation.LookAt( b.Dir );
		if ( b.Renderer.IsValid() )
			b.Renderer.Tint = tracerColor ?? DefaultTracer;
		_activeBullets.Add( b );

		if ( playSound )
		{
			var snd = fireSound ?? Sfx.Shoot;
			CombatAudio.PlayGunfire( snd, $"FireBullet ({System.IO.Path.GetFileName( snd )})" );
		}
	}

	protected override void OnUpdate()
	{
		var core = GameCore.Instance;
		if ( core is null || core.Phase != GamePhase.Night ) return;

		var dt = Time.Delta;
		UpdateZombies( dt, core );
		UpdateBullets( dt, core );
		CleanupDead();
	}

	private enum TargetKind { Wall, Building, Defender, Core }

	private readonly struct ZombieTarget
	{
		public TargetKind Kind { get; init; }
		public Vector3 Position { get; init; }
		public WallSegment Wall { get; init; }
		public PlacedBuilding Building { get; init; }
		public DefenderManager.DefenderUnit Defender { get; init; }
		public float AttackRadius { get; init; }
	}

	private void UpdateZombies( float dt, GameCore core )
	{
		var outpost = OutpostManager.Instance;
		if ( outpost is null ) return;

		var build = BuildManager.Instance;
		var defenders = DefenderManager.Instance;
		var corePos = outpost.CorePosition;
		var night = core.Save.CurrentNight;

		foreach ( var z in _zombies )
		{
			if ( z.Dead ) continue;

			var pos = z.Position;
			var target = FindTarget( z, pos, corePos, outpost, build, defenders );

			var moveGoal = target.Position;
			if ( z.TypeDef?.CanJumpWalls != true
				&& WallApproach.SideHasBreach( outpost.Walls, z.ApproachSide )
				&& target.Kind != TargetKind.Wall
				&& WallApproach.IsOutsideWalls( pos, corePos ) )
			{
				var waypoint = WallApproach.InwardWaypoint( outpost.Walls, z.ApproachSide, corePos );
				waypoint.z = pos.z;
				if ( (waypoint - pos).WithZ( 0f ).Length > 55f )
					moveGoal = waypoint;
			}

			var toMove = (moveGoal - pos).WithZ( 0f );
			var distMove = toMove.Length;
			var distAttack = (target.Position - pos).WithZ( 0f ).Length;
			var facing = distMove > 1f ? Rotation.LookAt( toMove.Normal ) : z.Go.WorldRotation;
			z.Go.WorldRotation = facing;

			if ( distAttack <= target.AttackRadius )
			{
				AttackTarget( z, target, outpost, build, defenders, dt, core, night );
				z.Character?.TickZombie( Vector3.Zero, facing );
				continue;
			}

			if ( distMove > 1f )
			{
				var dir = toMove / distMove;
				var next = pos + dir * z.Speed * dt;
				next.z = OutpostTerrain.SampleHeight( next.x, next.y );
				z.Go.WorldPosition = next;
				z.Character?.TickZombie( dir * z.Speed, facing );
			}
			else
			{
				z.Character?.TickZombie( Vector3.Zero, facing );
			}
		}

		if ( outpost.CoreHealth <= 0f )
			core.OnCoreDestroyed();
	}

	private static ZombieTarget FindTarget(
		ZombieInstance z,
		Vector3 pos,
		Vector3 corePos,
		OutpostManager outpost,
		BuildManager build,
		DefenderManager defenders )
	{
		if ( z.TypeDef?.BeeLinesCore == true )
		{
			return new ZombieTarget
			{
				Kind = TargetKind.Core,
				Position = corePos,
				AttackRadius = GameConstants.CoreSize * 0.6f + GameConstants.ZombieRadius
			};
		}

		var bestDist = float.MaxValue;
		ZombieTarget best = default;
		var hasBest = false;

		void Consider( TargetKind kind, Vector3 position, float attackRadius,
			WallSegment wall = null, PlacedBuilding building = null, DefenderManager.DefenderUnit defender = null )
		{
			var d = (position - pos).WithZ( 0f ).LengthSquared;
			if ( d >= bestDist ) return;
			bestDist = d;
			best = new ZombieTarget
			{
				Kind = kind,
				Position = position,
				Wall = wall,
				Building = building,
				Defender = defender,
				AttackRadius = attackRadius
			};
			hasBest = true;
		}

		if ( z.TypeDef?.CanJumpWalls != true )
		{
			var side = z.ApproachSide;
			if ( !WallApproach.SideHasBreach( outpost.Walls, side ) )
			{
				var breachWall = WallApproach.GetBreachWall( outpost.Walls, side );
				if ( breachWall is not null && (breachWall.Center - pos).Length <= GameConstants.ZombieSeekRadius )
					Consider( TargetKind.Wall, breachWall.Center, GameConstants.ZombieMeleeRange, wall: breachWall );
			}
		}

		if ( build is not null )
		{
			foreach ( var b in build.Buildings )
			{
				if ( b.IsDestroyed ) continue;
				if ( (b.Center - pos).Length > GameConstants.ZombieSeekRadius ) continue;
				Consider( TargetKind.Building, b.Center, GameConstants.ZombieMeleeRange, building: b );
			}
		}

		if ( defenders is not null )
		{
			foreach ( var d in defenders.Units )
			{
				if ( !d.IsAlive ) continue;
				if ( (d.WorldPos - pos).Length > GameConstants.ZombieSeekRadius ) continue;
				Consider( TargetKind.Defender, d.WorldPos, GameConstants.ZombieMeleeRange, defender: d );
			}
		}

		if ( hasBest )
			return best;

		return new ZombieTarget
		{
			Kind = TargetKind.Core,
			Position = corePos,
			AttackRadius = GameConstants.CoreSize * 0.6f + GameConstants.ZombieRadius
		};
	}

	private void AttackTarget(
		ZombieInstance z,
		ZombieTarget target,
		OutpostManager outpost,
		BuildManager build,
		DefenderManager defenders,
		float dt,
		GameCore core,
		int night )
	{
		z.AttackTimer -= dt;
		if ( z.AttackTimer > 0f ) return;

		z.AttackTimer = GameConstants.ZombieAttackInterval;

		switch ( target.Kind )
		{
			case TargetKind.Core:
				if ( z.TypeDef?.CoreExplosionDamage > 0f )
				{
					outpost.DamageCore( z.TypeDef.CoreExplosionDamage + z.Damage );
					z.Dead = true;
					CombatAudio.PlayImpact( CombatAudio.ImpactKind.BomberExplode, "core", "ZombieBomberExplode" );
					return;
				}

				outpost.DamageCore( z.Damage );
				CombatAudio.PlayImpact( CombatAudio.ImpactKind.Core, "core", "ZombieHitCore" );
				break;

			case TargetKind.Wall when target.Wall is not null && !target.Wall.IsBroken:
				target.Wall.Damage( z.Damage );
				CombatAudio.PlayImpact( CombatAudio.ImpactKind.Wall, target.Wall.Key ?? "wall", "ZombieHitWall" );
				break;

			case TargetKind.Building when target.Building is not null && !target.Building.IsDestroyed:
				target.Building.Damage( z.Damage );
				CombatAudio.PlayImpact(
					CombatAudio.ImpactKind.Building,
					$"{target.Building.CellX},{target.Building.CellY}",
					"ZombieHitBuilding" );
				break;

			case TargetKind.Defender when target.Defender is not null && target.Defender.IsAlive:
				if ( defenders?.DamageUnit( target.Defender, z.Damage ) == true )
				{
					CombatAudio.PlayImpact(
						CombatAudio.ImpactKind.RecruitKill,
						DefenderKey( target.Defender ),
						"ZombieKillRecruit" );
				}
				else
				{
					CombatAudio.PlayImpact(
						CombatAudio.ImpactKind.RecruitHit,
						DefenderKey( target.Defender ),
						"ZombieHitRecruit" );
				}

				break;
		}
	}

	private static string DefenderKey( DefenderManager.DefenderUnit defender ) =>
		defender.SaveIndex >= 0 ? $"recruit:{defender.SaveIndex}" : $"recruit:{defender.Go.Id}";

	private void UpdateBullets( float dt, GameCore core )
	{
		var step = GameConstants.BulletSpeed * dt;

		for ( var i = _activeBullets.Count - 1; i >= 0; i-- )
		{
			var b = _activeBullets[i];
			b.Pos += b.Dir * step;
			b.Life -= dt;

			var hit = ResolveHit( b, core );

			if ( hit || b.Life <= 0f )
			{
				Recycle( b );
				_activeBullets.RemoveAt( i );
				continue;
			}

			b.Go.WorldPosition = b.Pos;
		}
	}

	private bool ResolveHit( Bullet b, GameCore core )
	{
		foreach ( var z in _zombies )
		{
			if ( z.Dead ) continue;

			var p = z.Position;
			if ( (new Vector2( p.x, p.y ) - new Vector2( b.Pos.x, b.Pos.y )).Length > GameConstants.ZombieRadius + 10f )
				continue;

			if ( z.Hit( b.Damage ) )
			{
				CombatAudio.PlayZombieKill( "ZombieKill" );
				ComboSystem.Instance?.RegisterKill();
				var comboMult = ComboSystem.Instance?.Multiplier ?? 1f;

				var night = core.Save.CurrentNight;
				var scrap = (GameConstants.ScrapPerKillBase + night * GameConstants.ScrapPerKillPerNight)
					* core.ScrapMultiplier * comboMult;
				core.Wallet.Earn( scrap );
				core.Save.TotalKills++;
				ZombieBestiary.RecordKill( core.Save, z.TypeDef?.Kind ?? ZombieKind.Walker );
			}

			return true;
		}

		return false;
	}

	private void CleanupDead()
	{
		for ( var i = _zombies.Count - 1; i >= 0; i-- )
		{
			var z = _zombies[i];
			if ( !z.Dead ) continue;

			if ( z.TypeDef?.SplitCount > 0 )
				SpawnSplitters( z );

			z.Go?.Destroy();
			_zombies.RemoveAt( i );
		}
	}

	private void SpawnSplitters( ZombieInstance parent )
	{
		var walker = ZombieCatalog.Get( ZombieKind.Walker );
		var count = parent.TypeDef.SplitCount;
		for ( var i = 0; i < count; i++ )
		{
			var offset = Game.Random.Float( -40f, 40f );
			var pos = parent.Position + new Vector3( offset, offset * 0.5f, 0f );
			SpawnZombieInternal(
				pos,
				parent.MaxHealth * 0.35f,
				parent.Damage * 0.65f,
				parent.Speed * 1.05f,
				walker );
		}
	}

	private Bullet CreateBullet()
	{
		var go = new GameObject( true, "Bullet" );
		go.LocalScale = MeshPrimitives.BoxScale( new Vector3( 10f, 3.5f, 3.5f ) );
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = MeshPrimitives.Box;
		mr.MaterialOverride = MeshPrimitives.Mat;
		mr.Tint = DefaultTracer;
		return new Bullet { Go = go, Renderer = mr };
	}

	private void Recycle( Bullet b )
	{
		b.Active = false;
		b.Go.Enabled = false;
		_pool.Push( b );
	}
}
