namespace FinalOutpost;

/// <summary>
/// Recruited defenders that patrol the base by day and auto-fire during the Night. Each recruit
/// carries one of five gun types (see <see cref="RecruitWeapons"/>) with its own stats, visuals and
/// animations. Players can buy recruits of a chosen type, or train a type to raise its damage.
/// Individual recruits are selectable in the world via <see cref="DefenderSelectable"/>.
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
		private Vector3 _waypoint;
		private bool _hasWaypoint;
		private float _repathTimer;

		public Vector3 WorldPos => Go.IsValid() ? Go.WorldPosition : Vector3.Zero;

		public void TickDay( float dt )
		{
			if ( !Go.IsValid() ) return;
			Wander( dt, GameConstants.DefenderMoveSpeed * 0.55f );
		}

		public void TickNight( float dt, CombatSystem combat, RecruitWeaponDef def, float perShotDamage, float range )
		{
			if ( !Go.IsValid() || !IsAlive ) return;

			var pos = Go.WorldPosition;
			var target = combat.NearestZombie( pos, range );

			if ( target is not null )
			{
				// In range: hold ground, face the target and fire.
				var muzzle = Character?.MuzzleWorld( Aim ) ?? pos + Vector3.Up * 52f;
				var to = (target.Position - muzzle).WithZ( 0f );
				if ( to.Length > 1f )
					Aim = Rotation.LookAt( to.Normal );

				Go.WorldRotation = Aim;
				Character?.Tick( Vector3.Zero, Aim );

				_fireTimer -= dt;
				if ( _fireTimer <= 0f )
				{
					FireVolley( combat, def, perShotDamage, muzzle );
					_fireTimer = def.FireInterval;
				}
				return;
			}

			// Nothing in weapon range — advance toward the nearest threat, else keep patrolling.
			var threat = combat.NearestZombie( pos, GameConstants.DefenderAcquireRange );
			if ( threat is not null )
			{
				var guard = GameConstants.ArenaHalf - 70f;
				var tp = threat.Position.WithZ( 0f );
				if ( tp.Length > guard )
					tp = tp.Normal * guard;
				MoveToward( tp, dt, GameConstants.DefenderMoveSpeed );
			}
			else
			{
				Wander( dt, GameConstants.DefenderMoveSpeed * 0.55f );
			}
		}

		private void FireVolley( CombatSystem combat, RecruitWeaponDef def, float perShotDamage, Vector3 muzzle )
		{
			for ( var p = 0; p < def.Pellets; p++ )
			{
				var dir = ApplySpread( Aim.Forward, def.SpreadDegrees );
				combat.FireBullet( muzzle, dir, perShotDamage, def.FireSound, def.TracerColor, playSound: p == 0 );
			}
		}

		private void Wander( float dt, float speed )
		{
			var pos = Go.WorldPosition;
			_repathTimer -= dt;

			if ( !_hasWaypoint || _repathTimer <= 0f
			     || (_waypoint - pos).WithZ( 0f ).Length <= GameConstants.DefenderHomeDeadzone )
				PickWaypoint();

			MoveToward( _waypoint, dt, speed, leashToPlot: HasBarracksPlot );
		}

		private void PickWaypoint()
		{
			if ( HasBarracksPlot )
			{
				var angle = Game.Random.Float( 0f, MathF.PI * 2f );
				var r = Game.Random.Float( 0f, RoamRadius );
				_waypoint = RoamCenter + new Vector3( MathF.Cos( angle ) * r, MathF.Sin( angle ) * r, 0f );
			}
			else
			{
				var angle = Game.Random.Float( 0f, MathF.PI * 2f );
				var r = Game.Random.Float( RoamRadius * 0.5f, RoamRadius );
				_waypoint = RoamCenter + new Vector3( MathF.Cos( angle ) * r, MathF.Sin( angle ) * r, 0f );
			}

			_hasWaypoint = true;
			_repathTimer = Game.Random.Float( 2.5f, 5.5f );
		}

		private void MoveToward( Vector3 targetXY, float dt, float speed, bool leashToPlot = false )
		{
			var pos = Go.WorldPosition;
			var to = (targetXY - pos).WithZ( 0f );
			var dist = to.Length;

			if ( dist < 1f )
			{
				Character?.Tick( Vector3.Zero, Aim );
				return;
			}

			var dir = to / dist;
			Aim = Rotation.LookAt( dir );

			var step = MathF.Min( dist, speed * dt );
			var next = pos + dir * step;

			if ( leashToPlot && HasBarracksPlot )
			{
				var fromCenter = (next - RoamCenter).WithZ( 0f );
				if ( fromCenter.Length > RoamRadius )
					next = RoamCenter + fromCenter.Normal * RoamRadius;
			}

			next.z = OutpostTerrain.SampleHeight( next.x, next.y );
			Go.WorldPosition = next;
			Go.WorldRotation = Aim;

			Character?.Tick( dir * speed, Aim );
		}
	}
}
