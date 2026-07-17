namespace FinalOutpost;

/// <summary>
/// Recruited defenders patrol by day and auto-defend at night. Player focus/area commands
/// temporarily override their autonomous targeting; towers remain fully automatic.
/// </summary>
public sealed class DefenderManager : Component
{
	public static DefenderManager Instance { get; private set; }

	private readonly List<DefenderUnit> _units = new();

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
		Clear();
	}

	public IReadOnlyList<DefenderUnit> Units => _units;
	public int Count => _units.Count;

	public int CountOf( RecruitWeaponType type )
	{
		var n = 0;
		foreach ( var u in _units ) if ( u.Type == type ) n++;
		return n;
	}

	public int TrainLevelOf( RecruitWeaponType type )
	{
		var save = GameCore.Instance?.Save;
		if ( save is null ) return 0;
		return save.RecruitTrainLevels.TryGetValue( type.ToString(), out var lvl ) ? lvl : 0;
	}

	public double RecruitCost( RecruitWeaponType type )
	{
		var cost = RecruitWeapons.Get( type ).RecruitCost;
		var core = GameCore.Instance;
		if ( core?.IsCure == true )
			cost *= TeamBonuses.RecruitCostMult( core );
		return cost;
	}

	public double TrainCost( RecruitWeaponType type )
	{
		var def = RecruitWeapons.Get( type );
		return def.TrainBaseCost * Math.Pow( 1.5, TrainLevelOf( type ) );
	}

	/// <summary>Current per-shot damage for a gun type, including its training level.</summary>
	public float DamageOf( RecruitWeaponType type )
	{
		var def = RecruitWeapons.Get( type );
		var trainMult = 1f + (GameCore.Instance?.Upgrades?.DefenderTrainBonus ?? 0f);
		var dmg = def.Damage + TrainLevelOf( type ) * def.DamagePerTrain * trainMult;
		var core = GameCore.Instance;
		if ( core?.IsCure == true )
			dmg *= TeamBonuses.RecruitDamageMult( core );
		return dmg;
	}

	public static float MaxRecruitHealth()
	{
		var core = GameCore.Instance;
		return core?.IsCure == true
			? GameConstants.RecruitMaxHealth * TeamBonuses.RecruitHealthMult( core )
			: GameConstants.RecruitMaxHealth;
	}

	public float RangeOf( RecruitWeaponType type ) => RecruitWeapons.Get( type ).Range;

	public void RebuildFromSave() => RespawnAll();

	public void RefreshWeaponModels()
	{
		foreach ( var unit in _units )
			unit.Character?.RefreshWeaponModel();
	}

	public bool TryRecruit( RecruitWeaponType type )
	{
		var core = GameCore.Instance;
		var build = BuildManager.Instance;
		if ( core is null || core.Phase != GamePhase.Day ) return false;
		if ( build is null || build.BarracksCount <= 0 ) return false;
		if ( Count >= build.RecruitCapacity ) return false;
		if ( !NightUnlocks.IsRecruitUnlocked( core.Save, type ) ) return false;
		if ( !core.Wallet.TrySpend( RecruitCost( type ) ) ) return false;

		core.Save.Recruits.Add( type.ToString() );
		core.Save.RecruitHealth.Add( MaxRecruitHealth() );
		RespawnAll();
		Sfx.Play( Sfx.Purchase );
		core.CloseRecruit();
		core.SaveManagerTouch();
		return true;
	}

	public bool TryTrain( RecruitWeaponType type )
	{
		var core = GameCore.Instance;
		if ( core is null || core.Phase != GamePhase.Day ) return false;
		if ( CountOf( type ) <= 0 ) return false;
		if ( !core.Wallet.TrySpend( TrainCost( type ) ) ) return false;

		core.Save.RecruitTrainLevels[type.ToString()] = TrainLevelOf( type ) + 1;
		Sfx.Play( Sfx.Purchase );
		core.SaveManagerTouch();
		return true;
	}

	/// <summary>Dismiss a single recruit, refunding a fraction of its purchase cost.</summary>
	public bool Dismiss( DefenderUnit unit )
	{
		var core = GameCore.Instance;
		if ( core is null || unit is null ) return false;
		if ( !_units.Contains( unit ) ) return false;

		var idx = unit.SaveIndex;
		if ( idx < 0 || idx >= core.Save.Recruits.Count ) return false;

		core.Save.Recruits.RemoveAt( idx );
		if ( idx < core.Save.RecruitHealth.Count )
			core.Save.RecruitHealth.RemoveAt( idx );
		core.Wallet.Earn( RecruitCost( unit.Type ) * GameConstants.SellRefundFraction, applyIncomeScale: false );

		if ( BuildManager.Instance?.Selected is DefenderSelectable sel && sel.Wraps( unit ) )
			BuildManager.Instance.Deselect();

		RespawnAll();
		core.SaveManagerTouch();
		return true;
	}

	/// <summary>Damage a recruit. Returns true if they were killed.</summary>
	public bool DamageUnit( DefenderUnit unit, float amount )
	{
		if ( unit is null || !unit.IsAlive || amount <= 0f ) return false;

		unit.Health = MathF.Max( 0f, unit.Health - amount );
		SyncHealth( unit );

		if ( unit.Health > 0f ) return false;

		KillUnit( unit );
		return true;
	}

	public void HealInRadius( Vector3 center, float radius, float amount )
	{
		if ( amount <= 0f ) return;
		var r2 = radius * radius;

		foreach ( var u in _units )
		{
			if ( !u.IsAlive || u.Health >= u.MaxHealth ) continue;
			if ( (u.WorldPos - center).LengthSquared > r2 ) continue;

			u.Health = MathF.Min( u.MaxHealth, u.Health + amount );
			SyncHealth( u );
		}
	}

	public void HealAssignedToBarracks( PlacedBuilding barracks, float amount )
	{
		if ( amount <= 0f || barracks is null ) return;

		foreach ( var u in _units )
		{
			if ( !u.IsAlive || u.Health >= u.MaxHealth ) continue;
			if ( !IsAssignedToBarracks( u, barracks ) ) continue;

			u.Health = MathF.Min( u.MaxHealth, u.Health + amount );
			SyncHealth( u );
		}
	}

	/// <summary>Restore all living recruits assigned to a barracks to full HP (used after a night ends).</summary>
	public void FullHealAssignedToBarracks( PlacedBuilding barracks )
	{
		if ( barracks is null ) return;

		foreach ( var u in _units )
		{
			if ( !u.IsAlive ) continue;
			if ( !IsAssignedToBarracks( u, barracks ) ) continue;

			u.Health = u.MaxHealth;
			SyncHealth( u );
		}
	}

	/// <summary>Restore all living recruits within range to full HP.</summary>
	public void FullHealInRadius( Vector3 center, float radius )
	{
		var r2 = radius * radius;

		foreach ( var u in _units )
		{
			if ( !u.IsAlive ) continue;
			if ( (u.WorldPos - center).LengthSquared > r2 ) continue;

			u.Health = u.MaxHealth;
			SyncHealth( u );
		}
	}

	private void SyncHealth( DefenderUnit unit )
	{
		var save = GameCore.Instance?.Save;
		if ( save is null || unit.SaveIndex < 0 ) return;
		while ( save.RecruitHealth.Count <= unit.SaveIndex )
			save.RecruitHealth.Add( MaxRecruitHealth() );
		save.RecruitHealth[unit.SaveIndex] = unit.Health;
	}

	private void KillUnit( DefenderUnit unit )
	{
		var core = GameCore.Instance;
		if ( core is null ) return;

		DestructionFx.Burst( unit.WorldPos, 0.33f );

		if ( BuildManager.Instance?.Selected is DefenderSelectable sel && sel.Wraps( unit ) )
			BuildManager.Instance.Deselect();

		var idx = unit.SaveIndex;
		if ( idx >= 0 && idx < core.Save.Recruits.Count )
		{
			core.Save.Recruits.RemoveAt( idx );
			if ( idx < core.Save.RecruitHealth.Count )
				core.Save.RecruitHealth.RemoveAt( idx );
		}

		core.SaveManagerTouch();
		RespawnAll();
	}

	/// <summary>Daytime patrol — recruits stroll around the base while the player prepares.</summary>
	protected override void OnUpdate()
	{
		var core = GameCore.Instance;
		if ( core is null || core.Phase != GamePhase.Day ) return;

		var dt = Time.Delta;
		foreach ( var u in _units )
			u.TickDay( dt );
	}

	/// <summary>Nighttime combat — driven each frame by the <see cref="NightController"/>.</summary>
	public void TickCombat( CombatSystem combat, UpgradeSystem upgrades )
	{
		var dt = Time.Delta;
		foreach ( var u in _units )
		{
			if ( !u.IsAlive ) continue;
			var def = RecruitWeapons.Get( u.Type );
			var trainLevel = TrainLevelOf( u.Type );
			var trainMult = 1f + upgrades.DefenderTrainBonus;
			var perShot = def.Damage + trainLevel * def.DamagePerTrain * trainMult
				+ upgrades.TurretDamageBonus * 0.25f / MathF.Max( 1, def.Pellets );
			var range = def.Range + upgrades.TurretRangeBonus * 0.5f;

			u.TickNight( dt, combat, def, perShot, range );
		}
	}

	/// <summary>All living recruits chase and fire on one zombie. Towers are unaffected.</summary>
	public void OrderAllFocus( ZombieInstance target )
	{
		if ( target is null || target.Dead ) return;
		foreach ( var u in _units )
		{
			if ( !u.IsAlive ) continue;
			u.SetAttackOrder( target );
		}
	}

	/// <summary>All living recruits move to a point and engage zombies near it.</summary>
	public void OrderAllAreaAttack( Vector3 ground )
	{
		var target = ground.WithZ( 0f );
		var guard = GameConstants.ArenaHalf - 70f;
		if ( target.Length > guard )
			target = target.Normal * guard;

		foreach ( var u in _units )
		{
			if ( !u.IsAlive ) continue;
			u.SetAreaAttackOrder( target );
		}
	}

	public void ClearAllOrders()
	{
		foreach ( var u in _units )
			u.ClearOrder();
	}

	private static bool IsAssignedToBarracks( DefenderUnit unit, PlacedBuilding barracks ) =>
		BuildManager.Instance?.GetBarracksForRecruit( unit.SaveIndex ) == barracks;

	private void RespawnAll()
	{
		Clear();
		var save = GameCore.Instance?.Save;
		if ( save is null ) return;

		var total = save.Recruits.Count;
		for ( var i = 0; i < total; i++ )
		{
			var hp = i < save.RecruitHealth.Count ? save.RecruitHealth[i] : MaxRecruitHealth();
			SpawnUnit( RecruitWeapons.Parse( save.Recruits[i] ), i, total, hp );
		}
	}

	private void SpawnUnit( RecruitWeaponType type, int index, int total, float health )
	{
		var build = BuildManager.Instance;
		var barracks = build?.GetBarracksForRecruit( index );
		var roamCenter = Vector3.Zero;
		var roamRadius = GameConstants.PlotSize * 0.32f;
		var hasPlot = false;

		if ( barracks is not null && build.TryGetPlotForBuilding( barracks, out var plotX, out var plotY ) )
		{
			roamCenter = PlotGrid.CenterWorld( plotX, plotY );
			hasPlot = true;
		}

		Vector3 pos;
		if ( hasPlot )
		{
			var slot = index % GameConstants.RecruitsPerBarracks;
			var angle = slot / (float)GameConstants.RecruitsPerBarracks * MathF.PI * 2f;
			var offset = 48f + slot * 8f;
			pos = roamCenter + new Vector3( MathF.Cos( angle ) * offset, MathF.Sin( angle ) * offset, 0f );
			if ( BuildingCollision.BlocksUnit( pos )
			     && !BuildingCollision.TryFindClearPoint( roamCenter, 24f, roamRadius, out pos )
			     && !BuildingCollision.TryEscape( roamCenter, out pos ) )
				pos = roamCenter;
		}
		else
		{
			pos = FallbackSpawnPosition( index, total );
			roamCenter = pos;
			roamRadius = GameConstants.ArenaHalf * 0.35f;
		}

		var yaw = hasPlot
			? MathF.Atan2( barracks.Center.y - pos.y, barracks.Center.x - pos.x ) * (180f / MathF.PI)
			: MathF.Atan2( -pos.y, -pos.x ) * (180f / MathF.PI);

		var def = RecruitWeapons.Get( type );
		pos.z = OutpostTerrain.SampleHeight( pos.x, pos.y );

		var go = new GameObject( true, $"Defender_{index}_{type}" );
		go.WorldPosition = pos;
		go.WorldRotation = Rotation.FromYaw( yaw );

		var character = go.Components.Create<CharacterModel>();
		character.Setup( def.BodyTint, def.WorldModel, def.Hold, def.WeaponScale );

		var unit = new DefenderUnit
		{
			Go = go,
			Character = character,
			Type = type,
			Aim = Rotation.FromYaw( yaw ),
			SaveIndex = index,
			Health = MathF.Min( MaxRecruitHealth(), MathF.Max( 0f, health ) ),
			MaxHp = MaxRecruitHealth(),
			RoamCenter = roamCenter,
			RoamRadius = roamRadius,
			HasBarracksPlot = hasPlot
		};
		// Prime the pose so it's holding + aiming outward on the very first frame.
		character.Tick( Vector3.Zero, unit.Aim );
		_units.Add( unit );
	}

	private static Vector3 FallbackSpawnPosition( int index, int total )
	{
		var half = GameConstants.ArenaHalf - 100f;
		var angle = (index / (float)Math.Max( 1, total )) * MathF.PI * 2f;
		return new Vector3( MathF.Cos( angle ) * half * 0.85f, MathF.Sin( angle ) * half * 0.85f, 0f );
	}

	private void Clear()
	{
		foreach ( var u in _units ) u.Go?.Destroy();
		_units.Clear();
	}

	private static Vector3 ApplySpread( Vector3 forward, float degrees )
	{
		if ( degrees <= 0.01f )
			return forward;

		var yaw = Game.Random.Float( -degrees, degrees );
		var pitch = Game.Random.Float( -degrees, degrees );
		return (Rotation.LookAt( forward ) * Rotation.From( pitch, yaw, 0f )).Forward;
	}

	/// <summary>One recruited defender in the world. Logic driven by <see cref="DefenderManager"/>.</summary>
	public sealed class DefenderUnit
	{
		public int SaveIndex = -1;
		public float Health = GameConstants.RecruitMaxHealth;
		public float MaxHp = GameConstants.RecruitMaxHealth;
		public float MaxHealth => MaxHp;
		public bool IsAlive => Go.IsValid() && Health > 0f;

		public GameObject Go;
		public CharacterModel Character;
		public RecruitWeaponType Type;
		public Rotation Aim;
		public Vector3 RoamCenter;
		public float RoamRadius = GameConstants.PlotSize * 0.32f;
		public bool HasBarracksPlot;

		private float _fireTimer;
		private UnitLocomotion.WanderState _wander;
		private UnitLocomotion.SteerState _steer;
		private float _moveStuckTimer;
		private UnitOrderKind _orderKind = UnitOrderKind.None;
		private Vector3 _orderTarget;
		private ZombieInstance _attackTarget;

		public Vector3 WorldPos => Go.IsValid() ? Go.WorldPosition : Vector3.Zero;

		public void SetMoveOrder( Vector3 ground )
		{
			_orderKind = UnitOrderKind.Move;
			_orderTarget = ground.WithZ( 0f );
			_wander.Waypoint = _orderTarget;
			_wander.HasWaypoint = true;
			_wander.RepathTimer = 999f;
			_attackTarget = null;
		}

		public void SetAttackOrder( ZombieInstance target )
		{
			if ( target is null || target.Dead ) return;
			_orderKind = UnitOrderKind.AttackMove;
			_attackTarget = target;
			_orderTarget = target.Position.WithZ( 0f );
			_wander.RepathTimer = 0f;
		}

		public void SetAreaAttackOrder( Vector3 ground )
		{
			_orderKind = UnitOrderKind.AreaAttack;
			_orderTarget = ground.WithZ( 0f );
			_attackTarget = null;
			_wander.Waypoint = _orderTarget;
			_wander.HasWaypoint = true;
			_wander.RepathTimer = 0f;
		}

		public void ClearOrder()
		{
			_orderKind = UnitOrderKind.None;
			_attackTarget = null;
		}

		public void TickDay( float dt )
		{
			if ( !Go.IsValid() ) return;

			var core = GameCore.Instance;
			var speed = GameConstants.DefenderMoveSpeed * 0.55f;
			if ( core?.IsCure == true && TechTreeCatalog.IsUnlocked( core.Save, "tactics" ) )
				speed *= 1.35f;

			if ( _orderKind == UnitOrderKind.AttackMove && _attackTarget is not null && !_attackTarget.Dead )
			{
				TickDayAttack( dt, speed );
				return;
			}

			if ( _orderKind == UnitOrderKind.AreaAttack )
			{
				var areaDef = RecruitWeapons.Get( Type );
				var areaTrain = DefenderManager.Instance?.TrainLevelOf( Type ) ?? 0;
				var areaShot = areaDef.Damage + areaTrain * areaDef.DamagePerTrain;
				TickAreaHunt( dt, speed, GameCore.Instance?.Combat, areaDef, areaShot, areaDef.Range );
				return;
			}

			if ( _orderKind == UnitOrderKind.Move )
			{
				UnitLocomotion.MoveHumanoid( Go, _orderTarget, dt, speed, ref Aim, Character, ref _moveStuckTimer, ref _steer );
				if ( (_orderTarget - Go.WorldPosition).WithZ( 0f ).Length <= UnitLocomotion.ArrivalDistance )
					_orderKind = UnitOrderKind.None;
				return;
			}

			TickWander( dt, speed );
		}

		private void TickDayAttack( float dt, float speed )
		{
			var combat = GameCore.Instance?.Combat;
			if ( combat is null ) { _orderKind = UnitOrderKind.None; return; }

			var def = RecruitWeapons.Get( Type );
			var trainLevel = DefenderManager.Instance?.TrainLevelOf( Type ) ?? 0;
			var perShot = def.Damage + trainLevel * def.DamagePerTrain;
			var range = def.Range;

			var target = _attackTarget;
			if ( target is null || target.Dead ) { _orderKind = UnitOrderKind.None; _attackTarget = null; return; }

			EngageTarget( dt, combat, def, perShot, range, target, speed );
		}

		/// <summary>
		/// Recruits auto-defend at night unless the player gives a focus or area order.
		/// Explicit orders temporarily override autonomous target selection.
		/// </summary>
		public void TickNight( float dt, CombatSystem combat, RecruitWeaponDef def, float perShotDamage, float range )
		{
			if ( !Go.IsValid() || !IsAlive ) return;

			var speed = GameConstants.DefenderMoveSpeed;

			if ( _orderKind == UnitOrderKind.AttackMove )
			{
				if ( _attackTarget is null || _attackTarget.Dead )
				{
					_orderKind = UnitOrderKind.None;
					_attackTarget = null;
				}
				else
				{
					EngageTarget( dt, combat, def, perShotDamage, range, _attackTarget, speed );
					return;
				}
			}

			if ( _orderKind == UnitOrderKind.AreaAttack )
			{
				TickAreaHunt( dt, speed, combat, def, perShotDamage, range );
				return;
			}

			TickAutonomousNight( dt, combat, def, perShotDamage, range, speed );
		}

		private void TickAutonomousNight(
			float dt,
			CombatSystem combat,
			RecruitWeaponDef def,
			float perShotDamage,
			float range,
			float speed )
		{
			var pos = Go.WorldPosition;
			var losOrigin = pos + Vector3.Up * 52f;
			var target = combat.NearestEngageableZombie( pos, range, losOrigin );

			if ( target is not null )
			{
				EngageTarget( dt, combat, def, perShotDamage, range, target, speed );
				return;
			}

			var threat = combat.NearestZombie( pos, GameConstants.DefenderAcquireRange );
			if ( threat is not null )
			{
				var guard = GameConstants.ArenaHalf - 70f;
				var destination = threat.Position.WithZ( 0f );
				if ( destination.Length > guard )
					destination = destination.Normal * guard;

				UnitLocomotion.MoveHumanoid(
					Go, destination, dt, speed, ref Aim, Character, ref _moveStuckTimer, ref _steer );
				return;
			}

			TickWander( dt, speed * 0.55f );
		}

		private void TickAreaHunt( float dt, float speed, CombatSystem combat, RecruitWeaponDef def, float perShotDamage, float range )
		{
			if ( combat is null )
			{
				UnitLocomotion.MoveHumanoid( Go, _orderTarget, dt, speed, ref Aim, Character, ref _moveStuckTimer, ref _steer );
				return;
			}

			if ( _attackTarget is not null && !_attackTarget.Dead )
			{
				var toFocus = (_attackTarget.Position - _orderTarget).WithZ( 0f ).Length;
				if ( toFocus <= GameConstants.NightAreaEngageRadius )
				{
					EngageTarget( dt, combat, def, perShotDamage, range, _attackTarget, speed );
					return;
				}

				_attackTarget = null;
			}

			var nearPoint = combat.NearestZombie( _orderTarget, GameConstants.NightAreaEngageRadius );
			if ( nearPoint is not null && !nearPoint.Dead )
			{
				_attackTarget = nearPoint;
				EngageTarget( dt, combat, def, perShotDamage, range, nearPoint, speed );
				return;
			}

			if ( (_orderTarget - Go.WorldPosition).WithZ( 0f ).Length <= UnitLocomotion.ArrivalDistance )
			{
				_orderKind = UnitOrderKind.None;
				_attackTarget = null;
				TickAutonomousNight( dt, combat, def, perShotDamage, range, speed );
				return;
			}

			UnitLocomotion.MoveHumanoid( Go, _orderTarget, dt, speed, ref Aim, Character, ref _moveStuckTimer, ref _steer );
		}

		private void EngageTarget(
			float dt,
			CombatSystem combat,
			RecruitWeaponDef def,
			float perShotDamage,
			float range,
			ZombieInstance target,
			float moveSpeed )
		{
			var pos = Go.WorldPosition;
			var dist = (target.Position - pos).WithZ( 0f ).Length;
			var muzzle = Character?.MuzzleWorld( Aim ) ?? pos + Vector3.Up * 52f;

			if ( dist <= range && BuildingCollision.HasLineOfFire( muzzle, target.Position ) )
			{
				var to = (target.Position - muzzle).WithZ( 0f );
				if ( to.Length > 1f )
					Aim = Rotation.Slerp( Aim, Rotation.LookAt( to.Normal ), MathF.Min( 1f, dt * 16f ) );

				Go.WorldRotation = Aim;
				Character?.Tick( Vector3.Zero, Aim );

				_fireTimer -= dt;
				if ( _fireTimer <= 0f )
				{
					muzzle = Character?.MuzzleWorld( Aim ) ?? pos + Vector3.Up * 52f;
					if ( BuildingCollision.HasLineOfFire( muzzle, target.Position ) )
						FireVolley( combat, def, perShotDamage, muzzle );
					_fireTimer = def.FireInterval;
				}
				return;
			}

			UnitLocomotion.MoveHumanoid( Go, target.Position, dt, moveSpeed, ref Aim, Character, ref _moveStuckTimer, ref _steer );
		}

		private void FireVolley( CombatSystem combat, RecruitWeaponDef def, float perShotDamage, Vector3 muzzle )
		{
			for ( var p = 0; p < def.Pellets; p++ )
			{
				var dir = ApplySpread( Aim.Forward, def.SpreadDegrees );
				combat.FireBullet( muzzle, dir, perShotDamage, def.FireSound, def.TracerColor, playSound: p == 0 );
			}
		}

		private void TickWander( float dt, float speed )
		{
			if ( HasBarracksPlot )
			{
				UnitLocomotion.TickWander(
					ref _wander, Go, RoamCenter, 0f, RoamRadius, dt, speed, ref Aim, Character, RoamRadius );
				return;
			}

			UnitLocomotion.TickWander(
				ref _wander, Go, RoamCenter, RoamRadius * 0.5f, RoamRadius, dt, speed, ref Aim, Character );
		}
	}
}
