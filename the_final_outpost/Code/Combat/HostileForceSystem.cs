namespace FinalOutpost;

public enum HostileMission
{
	/// <summary>Defend a rival plot during a player assault.</summary>
	DefendPlot,
	/// <summary>Advance on the player's outpost.</summary>
	AssaultBase
}

/// <summary>Armed rival soldiers — shoot like recruits, fight player defenders / core / buildings.</summary>
public sealed class HostileForceSystem : Component
{
	public static HostileForceSystem Instance { get; private set; }

	readonly List<HostileUnit> _units = new();
	readonly List<HostileTower> _towers = new();

	int _spawnRemaining;
	float _spawnTimer;
	HostileMission _mission;
	int _assaultPlotX;
	int _assaultPlotY;
	int _breakersAssigned;
	float _retreatTimer;

	public IReadOnlyList<HostileUnit> Units => _units;
	public int AliveCount
	{
		get
		{
			var n = 0;
			foreach ( var u in _units ) if ( u.IsAlive ) n++;
			return n;
		}
	}
	public int SpawnRemaining => _spawnRemaining;
	public bool AnyActive => AliveCount > 0 || _spawnRemaining > 0;
	public HostileMission Mission => _mission;
	public bool IsAssault => _mission == HostileMission.DefendPlot;
	public int AssaultPlotX => _assaultPlotX;
	public int AssaultPlotY => _assaultPlotY;

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
		ClearAll();
	}

	public void ClearAll()
	{
		foreach ( var u in _units ) u.Go?.Destroy();
		_units.Clear();
		foreach ( var t in _towers ) t.Go?.Destroy();
		_towers.Clear();
		_spawnRemaining = 0;
		_spawnTimer = 0f;
		_breakersAssigned = 0;
		_retreatTimer = 0f;
	}

	/// <summary>Player invading a rival plot — spawn full garrison on that tile.</summary>
	public void BeginPlotAssault( int plotX, int plotY )
	{
		ClearAll();
		_mission = HostileMission.DefendPlot;
		_assaultPlotX = plotX;
		_assaultPlotY = plotY;
		_spawnRemaining = 0;

		var layout = RivalGarrison.Build( plotX, plotY );
		var center = PlotGrid.CenterWorld( plotX, plotY );
		var night = GameCore.Instance?.CombatProgressionNight ?? 1;

		foreach ( var slot in layout.Recruits )
			SpawnUnit( center + slot.LocalOffset, slot.Weapon, night, HostileMission.DefendPlot, wallBreaker: false );

		foreach ( var building in layout.Buildings )
		{
			if ( BuildableCatalog.Get( building.Id ).Role != BuildingRole.Defense )
				continue;
			SpawnTower( center + building.LocalOffset, building.Id, night );
		}
	}

	/// <summary>Rival soldiers march on the player's base (staggered spawns).</summary>
	public void BeginBaseAttack( int count )
	{
		ClearAll();
		_mission = HostileMission.AssaultBase;
		_spawnRemaining = Math.Max( 4, count );
		_spawnTimer = 0f;
		_breakersAssigned = 0;
		_retreatTimer = 0f;
	}

	public void Tick( float dt )
	{
		TickSpawns( dt );
		foreach ( var t in _towers )
			t.Tick( dt );
		foreach ( var u in _units )
		{
			if ( !u.IsAlive ) continue;
			u.Tick( dt );
		}
		TickRetreat( dt );
		CleanupDead();
	}

	void TickSpawns( float dt )
	{
		if ( _spawnRemaining <= 0 || _mission != HostileMission.AssaultBase ) return;
		_spawnTimer -= dt;
		if ( _spawnTimer > 0f ) return;
		_spawnTimer = GameConstants.SpawnStagger * 1.15f;
		_spawnRemaining--;

		var night = GameCore.Instance?.CombatProgressionNight ?? 1;
		var weapon = PickAttackWeapon( night, _spawnRemaining );
		var breaker = _breakersAssigned < GameConstants.HostileRaidMinBreakers;
		if ( breaker )
			_breakersAssigned++;
		SpawnUnit( PickPerimeterSpawn(), weapon, night, HostileMission.AssaultBase, wallBreaker: breaker );
	}

	/// <summary>
	/// If wall-breakers die before opening a breach, remaining raiders stuck outside flee
	/// after <see cref="GameConstants.HostileRaidRetreatDelay"/> seconds.
	/// </summary>
	void TickRetreat( float dt )
	{
		if ( _mission != HostileMission.AssaultBase ) return;
		if ( _spawnRemaining > 0 )
		{
			_retreatTimer = 0f;
			return;
		}

		var outpost = OutpostManager.Instance;
		if ( outpost is null )
		{
			_retreatTimer = 0f;
			return;
		}

		var corePos = outpost.CorePosition;
		var livingBreakers = 0;
		var trappedOutside = false;

		foreach ( var u in _units )
		{
			if ( !u.IsAlive ) continue;
			if ( u.IsWallBreaker )
				livingBreakers++;

			if ( !WallApproach.IsOutsideWalls( u.WorldPos, corePos ) )
				continue;

			if ( WallApproach.SideHasBreach( outpost.Walls, u.ApproachSide ) )
				continue;

			trappedOutside = true;
		}

		if ( livingBreakers > 0 || !trappedOutside )
		{
			_retreatTimer = 0f;
			return;
		}

		_retreatTimer += dt;
		if ( _retreatTimer < GameConstants.HostileRaidRetreatDelay )
			return;

		FleeOutsideRaiders();
	}

	void FleeOutsideRaiders()
	{
		var outpost = OutpostManager.Instance;
		var corePos = outpost?.CorePosition ?? Vector3.Zero;
		var fled = 0;

		foreach ( var u in _units )
		{
			if ( !u.IsAlive ) continue;
			if ( outpost is not null
			     && !WallApproach.IsOutsideWalls( u.WorldPos, corePos )
			     && !WallApproach.NeedsBreachEntry( u.WorldPos, corePos ) )
				continue;

			u.Health = 0f;
			fled++;
		}

		_retreatTimer = 0f;
		if ( fled > 0 )
			GameCore.Instance?.ShowToast( "Raiders retreated — they couldn't breach the walls." );
	}

	static RecruitWeaponType PickAttackWeapon( int night, int ordinal )
	{
		if ( night >= 8 && ordinal % 5 == 0 ) return RecruitWeaponType.Sniper;
		if ( night >= 5 && ordinal % 4 == 0 ) return RecruitWeaponType.Shotgun;
		if ( ordinal % 3 == 0 ) return RecruitWeaponType.Smg;
		if ( ordinal % 2 == 0 ) return RecruitWeaponType.AssaultRifle;
		return RecruitWeaponType.Pistol;
	}

	static Vector3 PickPerimeterSpawn()
	{
		var claimedHalf = PlotManager.Instance?.ClaimedHalfExtent ?? GameConstants.ArenaHalf;
		var half = MathF.Max( GameConstants.ArenaHalf, claimedHalf ) + GameConstants.SpawnMargin;
		half = MathF.Min( half, GameConstants.ActiveTerrainHalfExtent - GameConstants.U( 60f ) );
		var t = Game.Random.Float( -half, half );
		return Game.Random.Int( 0, 3 ) switch
		{
			0 => new Vector3( t, half, 0f ),
			1 => new Vector3( t, -half, 0f ),
			2 => new Vector3( half, t, 0f ),
			_ => new Vector3( -half, t, 0f ),
		};
	}

	void SpawnUnit( Vector3 pos, RecruitWeaponType weapon, int night, HostileMission mission, bool wallBreaker )
	{
		pos.z = OutpostTerrain.SampleHeight( pos.x, pos.y );
		var def = RecruitWeapons.Get( weapon );
		var tint = wallBreaker
			? new Color( 0.88f, 0.28f, 0.22f )
			: new Color( 0.78f, 0.32f, 0.34f );
		var go = new GameObject( true, wallBreaker ? $"HostileBreaker_{weapon}" : $"Hostile_{weapon}" );
		go.WorldPosition = pos;
		go.WorldRotation = Rotation.FromYaw( Game.Random.Float( 0f, 360f ) );

		var character = go.Components.Create<CharacterModel>();
		character.Setup( tint, def.WorldModel, def.Hold, def.WeaponScale );

		var hp = DefenderManager.MaxRecruitHealth() * (0.85f + night * 0.03f);
		var unit = new HostileUnit
		{
			Go = go,
			Character = character,
			Weapon = weapon,
			Health = hp,
			MaxHealth = hp,
			Mission = mission,
			IsWallBreaker = wallBreaker,
			ApproachSide = WallApproach.FromWorldPosition( pos, Vector3.Zero ),
			GuardCenter = mission == HostileMission.DefendPlot
				? PlotGrid.CenterWorld( _assaultPlotX, _assaultPlotY )
				: (OutpostManager.Instance?.CorePosition ?? Vector3.Zero),
			Aim = go.WorldRotation
		};
		HostileHitTarget.Attach( go, unit );
		_units.Add( unit );
	}

	void SpawnTower( Vector3 pos, BuildableId id, int night )
	{
		pos.z = OutpostTerrain.SampleHeight( pos.x, pos.y );
		var go = new GameObject( true, $"Hostile_{id}" );
		go.WorldPosition = pos;
		BuildingVisual.Build( go, id, pos, includeRubble: false );

		var def = BuildableCatalog.Get( id );
		// Support pads (Spotlight / Oil) are visual only for rivals — no phantom guns.
		if ( def.BaseDamage <= 0f || def.BaseRange <= 0f || def.FireInterval <= 0f )
			return;

		_towers.Add( new HostileTower
		{
			Go = go,
			Damage = def.BaseDamage * (0.9f + night * 0.04f),
			Range = MathF.Max( 200f, def.BaseRange ) * GameConstants.RangeScale,
			FireInterval = def.FireInterval
		} );
	}

	void CleanupDead()
	{
		for ( var i = _units.Count - 1; i >= 0; i-- )
		{
			var u = _units[i];
			if ( u.IsAlive ) continue;
			DestructionFx.Burst( u.WorldPos, 0.3f );
			u.Go?.Destroy();
			_units.RemoveAt( i );
		}
	}

	public bool DamageUnit( HostileUnit unit, float amount )
	{
		if ( unit is null || !unit.IsAlive || amount <= 0f ) return false;
		unit.Health = MathF.Max( 0f, unit.Health - amount );
		return !unit.IsAlive;
	}

	public HostileUnit Nearest( Vector3 from, float range )
	{
		HostileUnit best = null;
		var bestD = range;
		foreach ( var u in _units )
		{
			if ( !u.IsAlive ) continue;
			var d = (u.WorldPos - from).WithZ( 0f ).Length;
			if ( d >= bestD ) continue;
			bestD = d;
			best = u;
		}
		return best;
	}

	public bool TryHitAt( Vector3 pos, float damage, float radius )
	{
		var hit = false;
		foreach ( var u in _units )
		{
			if ( !u.IsAlive ) continue;
			if ( (u.WorldPos - pos).WithZ( 0f ).Length > radius ) continue;
			if ( DamageUnit( u, damage ) )
			{
				var core = GameCore.Instance;
				if ( core is not null )
				{
					core.Wallet.Earn( (GameConstants.ScrapPerKillBase * 0.65) * core.ScrapMultiplier + core.SalvageKillBonus );
					core.Save.TotalKills++;
				}
			}
			hit = true;
			break;
		}
		return hit;
	}

	/// <summary>Segment test so scaled bullet steps can't tunnel through hostiles.</summary>
	public bool TryHitSwept( Vector3 from, Vector3 to, float damage, float radius )
	{
		HostileUnit best = null;
		var bestT = float.MaxValue;
		var a = from.WithZ( 0f );
		var b = to.WithZ( 0f );
		var ab = b - a;
		var lenSq = ab.LengthSquared;

		foreach ( var u in _units )
		{
			if ( !u.IsAlive ) continue;
			var p = u.WorldPos.WithZ( 0f );
			float dist;
			float t;
			if ( lenSq < 0.0001f )
			{
				dist = (p - a).Length;
				t = 0f;
			}
			else
			{
				t = Math.Clamp( Vector3.Dot( p - a, ab ) / lenSq, 0f, 1f );
				dist = (p - (a + ab * t)).Length;
			}

			if ( dist > radius || t >= bestT ) continue;
			bestT = t;
			best = u;
		}

		if ( best is null ) return false;

		if ( DamageUnit( best, damage ) )
		{
			var core = GameCore.Instance;
			if ( core is not null )
			{
				core.Wallet.Earn( (GameConstants.ScrapPerKillBase * 0.65) * core.ScrapMultiplier + core.SalvageKillBonus );
				core.Save.TotalKills++;
			}
		}

		return true;
	}

	/// <summary>Order player recruits toward the assault plot.</summary>
	public void RallyPlayerRecruitsToAssault()
	{
		if ( _mission != HostileMission.DefendPlot ) return;
		var center = PlotGrid.CenterWorld( _assaultPlotX, _assaultPlotY );
		DefenderManager.Instance?.OrderAllAreaAttack( center );
	}
}

public sealed class HostileUnit
{
	public GameObject Go;
	public CharacterModel Character;
	public RecruitWeaponType Weapon;
	public float Health;
	public float MaxHealth;
	public HostileMission Mission;
	public bool IsWallBreaker;
	public WallApproachSide ApproachSide;
	public Vector3 GuardCenter;
	public Rotation Aim;
	public bool IsAlive => Go.IsValid() && Health > 0f;
	public Vector3 WorldPos => Go.IsValid() ? Go.WorldPosition : Vector3.Zero;

	float _fireTimer;
	float _bashTimer;
	UnitLocomotion.SteerState _steer;
	float _stuck;

	public void Tick( float dt )
	{
		if ( !IsAlive ) return;
		_fireTimer = MathF.Max( 0f, _fireTimer - dt );
		_bashTimer = MathF.Max( 0f, _bashTimer - dt );

		var def = RecruitWeapons.Get( Weapon );
		var range = def.Range;
		var speed = GameConstants.DefenderMoveSpeed * 0.9f;

		if ( Mission == HostileMission.AssaultBase && TickAssaultBreach( dt, speed, def ) )
			return;

		var targetPos = FindTarget( out var targetKind );
		if ( targetPos.HasValue )
		{
			var to = (targetPos.Value - WorldPos).WithZ( 0f );
			var dist = to.Length;
			if ( dist > 1f )
				Aim = Rotation.LookAt( to.Normal );

			var inRange = dist <= range;
			if ( inRange && _fireTimer <= 0f )
			{
				FireAt( targetPos.Value, def, targetKind );
				_fireTimer = def.FireInterval;
			}

			if ( Mission == HostileMission.AssaultBase && dist > range * 0.65f )
			{
				UnitLocomotion.MoveHumanoid( Go, targetPos.Value, dt, speed, ref Aim, Character, ref _stuck, ref _steer );
			}
			else if ( Mission == HostileMission.DefendPlot && dist > range )
			{
				UnitLocomotion.MoveHumanoid( Go, targetPos.Value, dt, speed, ref Aim, Character, ref _stuck, ref _steer );
			}
			else
			{
				Character?.Tick( Vector3.Zero, Aim );
			}
			return;
		}

		if ( Mission == HostileMission.DefendPlot )
		{
			var guard = GuardCenter.WithZ( 0f );
			if ( (WorldPos.WithZ( 0f ) - guard).Length > GameConstants.U( 90f ) )
				UnitLocomotion.MoveHumanoid( Go, guard, dt, speed * 0.6f, ref Aim, Character, ref _stuck, ref _steer );
			else
				Character?.Tick( Vector3.Zero, Aim );
		}
		else
		{
			UnitLocomotion.MoveHumanoid( Go, Vector3.Zero, dt, speed, ref Aim, Character, ref _stuck, ref _steer );
		}
	}

	/// <summary>
	/// Breach funnel for base raids — breakers smash a wall like zombies; others wait, then enter.
	/// Returns true when this tick was fully handled by breach logic.
	/// </summary>
	bool TickAssaultBreach( float dt, float speed, RecruitWeaponDef def )
	{
		var outpost = OutpostManager.Instance;
		if ( outpost is null || outpost.Walls.Count == 0 )
			return false;

		var corePos = outpost.CorePosition;
		var outside = WallApproach.IsOutsideWalls( WorldPos, corePos );
		var needsEntry = WallApproach.NeedsBreachEntry( WorldPos, corePos );
		var hasBreach = WallApproach.SideHasBreach( outpost.Walls, ApproachSide );

		// Already inside courtyard — normal combat.
		if ( !outside && !needsEntry )
			return false;

		if ( hasBreach )
		{
			var waypoint = WallApproach.InwardWaypoint( outpost.Walls, ApproachSide, corePos, near: WorldPos );
			if ( !WallApproach.PastBreachWaypoint( WorldPos, ApproachSide, waypoint ) )
			{
				UnitLocomotion.MoveHumanoid( Go, waypoint, dt, speed, ref Aim, Character, ref _stuck, ref _steer );

				// Opportunistic fire while funneling in.
				var targetPos = FindTarget( out var kind );
				if ( targetPos.HasValue )
				{
					var dist = (targetPos.Value - WorldPos).WithZ( 0f ).Length;
					if ( dist <= def.Range && _fireTimer <= 0f )
					{
						FireAt( targetPos.Value, def, kind );
						_fireTimer = def.FireInterval;
					}
				}

				return true;
			}

			return false;
		}

		// No breach yet — breakers smash; others rally outside the breach wall.
		var wall = WallApproach.GetBreachWall( outpost.Walls, ApproachSide );
		if ( wall is null )
			return false;

		var outward = WallApproach.OutwardNormal( ApproachSide );
		var stand = wall.Center + outward * (GameConstants.WallPathDepth * 0.5f + GameConstants.UnitCollisionRadius + 8f);
		var toWall = (wall.Center - WorldPos).WithZ( 0f );
		var distWall = toWall.Length;
		if ( distWall > 1f )
			Aim = Rotation.LookAt( toWall.Normal );

		if ( IsWallBreaker )
		{
			var melee = GameConstants.ZombieMeleeRange;
			if ( distWall > melee * 0.85f )
				UnitLocomotion.MoveHumanoid( Go, stand, dt, speed, ref Aim, Character, ref _stuck, ref _steer );
			else
				Character?.Tick( Vector3.Zero, Aim );

			var canBash = !wall.IsBroken
				&& (distWall <= melee * 1.4f || _stuck >= 0.35f);
			if ( canBash && _bashTimer <= 0f )
			{
				_bashTimer = GameConstants.HostileWallBashInterval;
				var dmg = MathF.Max( 14f, def.Damage * 1.35f );
				wall.Damage( dmg );
				CombatAudio.PlayImpact( CombatAudio.ImpactKind.Wall, wall.Key ?? "wall", "HostileBashWall" );
				_stuck = 0f;
			}

			return true;
		}

		// Non-breakers: hold near the breach site (don't circle the whole ring).
		var rally = stand + outward * GameConstants.CellSize * 0.35f;
		if ( (WorldPos - rally).WithZ( 0f ).Length > GameConstants.U( 70f ) )
			UnitLocomotion.MoveHumanoid( Go, rally, dt, speed * 0.85f, ref Aim, Character, ref _stuck, ref _steer );
		else
			Character?.Tick( Vector3.Zero, Aim );

		return true;
	}

	Vector3? FindTarget( out int kind )
	{
		kind = 0; // 0 defender, 1 building, 2 core
		var best = (Vector3?)null;
		var bestD = float.MaxValue;

		foreach ( var d in DefenderManager.Instance?.Units ?? Array.Empty<DefenderManager.DefenderUnit>() )
		{
			if ( !d.IsAlive ) continue;
			var dist = (d.WorldPos - WorldPos).WithZ( 0f ).Length;
			if ( dist >= bestD ) continue;
			bestD = dist;
			best = d.WorldPos;
			kind = 0;
		}

		var build = BuildManager.Instance;
		if ( build is not null )
		{
			foreach ( var b in build.Buildings )
			{
				if ( b.IsDestroyed ) continue;
				var dist = (b.Center - WorldPos).WithZ( 0f ).Length;
				if ( dist >= bestD ) continue;
				bestD = dist;
				best = b.Center;
				kind = 1;
			}
		}

		var outpost = OutpostManager.Instance;
		if ( outpost is not null && outpost.CoreHealth > 0f )
		{
			var corePos = outpost.CorePosition;
			var dist = (corePos - WorldPos).WithZ( 0f ).Length;
			if ( dist < bestD )
			{
				best = corePos + Vector3.Up * 40f;
				kind = 2;
			}
		}

		return best;
	}

	void FireAt( Vector3 target, RecruitWeaponDef def, int kind )
	{
		var muzzle = Character?.MuzzleWorld( Aim ) ?? WorldPos + Vector3.Up * 52f + Aim.Forward * 20f;
		var dir = (target - muzzle);
		if ( dir.Length < 1f ) dir = Aim.Forward;
		dir = dir.Normal;

		var dmg = def.Damage * 0.9f;
		var combat = CombatSystem.Instance;
		for ( var p = 0; p < Math.Max( 1, def.Pellets ); p++ )
		{
			var shotDir = ApplySpread( dir, def.SpreadDegrees );
			combat?.FireBullet( muzzle, shotDir, 0f, def.FireSound, def.TracerColor, playSound: p == 0 );
		}

		// Hitscan resolve against player side (bullets are visual — hostiles apply damage directly).
		ApplyHostileDamage( muzzle, dir, dmg, def.Range, kind );
		Character?.Tick( Vector3.Zero, Aim );
	}

	void ApplyHostileDamage( Vector3 origin, Vector3 dir, float damage, float range, int preferredKind )
	{
		var end = origin + dir * range;

		// Prefer nearest defender along the shot cone.
		DefenderManager.DefenderUnit hitDef = null;
		var hitT = range;
		foreach ( var d in DefenderManager.Instance?.Units ?? Array.Empty<DefenderManager.DefenderUnit>() )
		{
			if ( !d.IsAlive ) continue;
			var to = d.WorldPos + Vector3.Up * 40f - origin;
			var t = Vector3.Dot( to, dir );
			if ( t < 0f || t > hitT ) continue;
			var closest = origin + dir * t;
			if ( (closest - (d.WorldPos + Vector3.Up * 40f)).Length > 40f ) continue;
			hitT = t;
			hitDef = d;
		}

		if ( hitDef is not null )
		{
			DefenderManager.Instance?.DamageUnit( hitDef, damage );
			return;
		}

		if ( preferredKind == 1 )
		{
			PlacedBuilding hitB = null;
			hitT = range;
				foreach ( var b in BuildManager.Instance?.Buildings ?? (IReadOnlyCollection<PlacedBuilding>)Array.Empty<PlacedBuilding>() )
			{
				if ( b.IsDestroyed ) continue;
				var to = b.Center - origin;
				var t = Vector3.Dot( to, dir );
				if ( t < 0f || t > hitT ) continue;
				if ( (origin + dir * t - b.Center).Length > 50f ) continue;
				hitT = t;
				hitB = b;
			}
			if ( hitB is not null )
			{
				hitB.Damage( damage );
				return;
			}
		}

		var core = OutpostManager.Instance;
		if ( core is not null && core.CoreHealth > 0f )
		{
			var corePos = Vector3.Up * 40f;
			var to = corePos - origin;
			var t = Vector3.Dot( to, dir.Normal );
			if ( t > 0f && t < range && (origin + dir.Normal * t - corePos).Length < 70f )
				core.DamageCore( damage * 0.55f );
		}

		_ = end;
	}

	static Vector3 ApplySpread( Vector3 forward, float degrees )
	{
		if ( degrees <= 0.01f ) return forward;
		var yaw = Game.Random.Float( -degrees, degrees );
		var pitch = Game.Random.Float( -degrees, degrees );
		return (Rotation.LookAt( forward ) * Rotation.From( pitch, yaw, 0f )).Forward;
	}
}

public sealed class HostileTower
{
	public GameObject Go;
	public float Damage;
	public float Range;
	public float FireInterval;
	float _timer;

	public Vector3 WorldPos => Go.IsValid() ? Go.WorldPosition : Vector3.Zero;

	public void Tick( float dt )
	{
		if ( !Go.IsValid() ) return;
		_timer -= dt;
		if ( _timer > 0f ) return;

		var target = FindTarget();
		if ( target is null ) return;
		_timer = FireInterval;

		var muzzle = WorldPos + Vector3.Up * 20f;
		var dir = (target.Value - muzzle).Normal;
		CombatSystem.Instance?.FireBullet( muzzle, dir, 0f, Sfx.Shoot, new Color( 1f, 0.45f, 0.4f ) );

		foreach ( var d in DefenderManager.Instance?.Units ?? Array.Empty<DefenderManager.DefenderUnit>() )
		{
			if ( !d.IsAlive ) continue;
			if ( (d.WorldPos - WorldPos).WithZ( 0f ).Length > Range ) continue;
			DefenderManager.Instance?.DamageUnit( d, Damage );
			return;
		}
	}

	Vector3? FindTarget()
	{
		foreach ( var d in DefenderManager.Instance?.Units ?? Array.Empty<DefenderManager.DefenderUnit>() )
		{
			if ( !d.IsAlive ) continue;
			if ( (d.WorldPos - WorldPos).WithZ( 0f ).Length <= Range )
				return d.WorldPos + Vector3.Up * 40f;
		}
		return null;
	}
}
