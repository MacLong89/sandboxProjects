namespace FinalOutpost;

/// <summary>A player-placed structure on the build grid, rendered as a stylized multi-part model.</summary>
public sealed class PlacedBuilding : Component, ISelectable
{
	public BuildableId Type { get; private set; }
	public BuildableDef Def => BuildableCatalog.Get( Type );
	public int CellX { get; private set; }
	public int CellY { get; private set; }
	/// <summary>Facing in 90° steps (0 = +X / east-west wall run).</summary>
	public int YawSteps { get; private set; }
	public int PlaceOrder { get; private set; }
	public int Level { get; private set; } = 1;
	public float Health { get; private set; }
	public float MaxHealth { get; private set; }
	public bool IsDestroyed => Health <= 0f;
	public bool IsDefense => Def.Role == BuildingRole.Defense;
	public bool IsSupport => Def.Role == BuildingRole.Support;

	/// <summary>
	/// AUDIT FIX M1: scrap paid at place time. Barracks cost escalates with owned count;
	/// SellRefund must use this or 2nd+ barracks under-refund.
	/// 0 = loaded from pre-v18 save → fall back to Def.PlaceCost.
	/// </summary>
	public double PaidPlaceCost { get; private set; }

	private readonly List<(ModelRenderer Renderer, Color Base)> _parts = new();
	private ModelRenderer _rubble;
	private GameObject _headRoot;
	private Vector3 _muzzleLocal = new( 30f, 0f, 0f );
	private float _fireTimer;
	private Rotation _aim = Rotation.Identity;

	public Vector3 Center => WorldPosition;
	public float HealthFraction => MaxHealth <= 0f ? 0f : Health / MaxHealth;

	float _wallMountZ;

	public void Setup( BuildableId type, int cellX, int cellY, int level, float health, int placeOrder = 0, double paidPlaceCost = 0, int yawSteps = 0 )
	{
		Type = type;
		CellX = cellX;
		CellY = cellY;
		PlaceOrder = placeOrder;
		PaidPlaceCost = paidPlaceCost;
		Level = Math.Clamp( level, 1, Def.MaxLevel );
		YawSteps = ((yawSteps % 4) + 4) % 4;

		var pos = TileOccupancy.WallMountWorldPosition( cellX, cellY );
		_wallMountZ = TileOccupancy.WallMountHeight( cellX, cellY );
		WorldPosition = pos;
		ApplyYaw();

		BuildVisual();

		SetMaxHealth( Def.MaxHp( Level ), health );
	}

	public void Relocate( int cellX, int cellY, int? yawSteps = null )
	{
		CellX = cellX;
		CellY = cellY;
		if ( yawSteps.HasValue )
			YawSteps = ((yawSteps.Value % 4) + 4) % 4;
		RefreshMountHeight( force: true );
		ApplyYaw();
	}

	public void SetYawSteps( int yawSteps )
	{
		YawSteps = ((yawSteps % 4) + 4) % 4;
		ApplyYaw();
	}

	void ApplyYaw()
	{
		if ( GameObject.IsValid() )
			WorldRotation = Rotation.FromYaw( YawSteps * 90f );
	}

	/// <summary>Sit on the wall when the cell has an intact perimeter segment; drop if the wall falls.</summary>
	public void RefreshMountHeight( bool force = false )
	{
		var target = TileOccupancy.WallMountWorldPosition( CellX, CellY );
		var mount = TileOccupancy.WallMountHeight( CellX, CellY );
		if ( !force
		     && MathF.Abs( mount - _wallMountZ ) < 0.5f
		     && (target.WithZ( 0f ) - WorldPosition.WithZ( 0f )).Length < 0.5f )
			return;

		_wallMountZ = mount;
		WorldPosition = target;
		ApplyYaw();
	}

	protected override void OnUpdate()
	{
		if ( (IsDefense || IsSupport) && !IsDestroyed )
			RefreshMountHeight();
	}

	public void SetHiddenForMove( bool hidden )
	{
		if ( GameObject.IsValid() )
			GameObject.Enabled = !hidden;
	}

	public void SetMaxHealth( float max, float? health = null )
	{
		MaxHealth = max;
		Health = health ?? max;
		RefreshVisual();
	}

	public void Damage( float amount )
	{
		if ( IsDestroyed ) return;
		amount *= DefenseEffects.DamageTakenMultForBuilding( this );
		Health = MathF.Max( 0f, Health - amount );

		if ( HasNightOneGunTowerProtection() && Health <= 0f )
			Health = 1f;

		if ( IsDestroyed )
		{
			DestructionFx.Burst( Center, IsDefense ? 1.25f : 1.1f );
			BuildManager.Instance?.HandleStructureDestroyed( this );
			return;
		}

		RefreshVisual();
	}

	/// <summary>First-night safety net — Survival starter gun towers cannot be destroyed on night 1.</summary>
	private bool HasNightOneGunTowerProtection()
	{
		if ( Type != BuildableId.GunTower ) return false;
		var core = GameCore.Instance;
		return core?.IsSurvival == true
			&& core.Phase == GamePhase.Night
			&& core.Save.CurrentNight == 1;
	}

	public void RepairToFull()
	{
		Health = MaxHealth;
		RefreshVisual();
	}

	public void Repair( float amount )
	{
		if ( IsDestroyed || amount <= 0f ) return;
		Health = MathF.Min( MaxHealth, Health + amount );
		RefreshVisual();
	}

	public void SetHealth( float hp )
	{
		if ( IsDestroyed ) return;
		Health = MathF.Min( MaxHealth, MathF.Max( 0f, hp ) );
		if ( IsDestroyed )
		{
			DestructionFx.Burst( Center, IsDefense ? 1.25f : 1.1f );
			BuildManager.Instance?.HandleStructureDestroyed( this );
			return;
		}

		RefreshVisual();
	}

	public bool TryUpgrade()
	{
		if ( Level >= Def.MaxLevel ) return false;
		Level++;
		SetMaxHealth( Def.MaxHp( Level ), MaxHealth );
		Health = MaxHealth;
		RebuildVisual();
		return true;
	}

	public double MissingRepairCost() => RepairCosts.EffectiveScrapCost( Def.RepairCost( MaxHealth - Health ) );

	public void TickCombat( CombatSystem combat, UpgradeSystem upgrades )
	{
		if ( IsDestroyed ) return;
		if ( !IsDefense && !IsSupport ) return;

		switch ( Type )
		{
			case BuildableId.Spotlight:
			case BuildableId.AmmoDepot:
			case BuildableId.Hardpoint:
			case BuildableId.RadioMast:
				return;
			case BuildableId.Minefield:
				TickMines( combat, upgrades );
				return;
			case BuildableId.OilSlick:
				TickOilSlick( combat, upgrades );
				return;
			case BuildableId.Artillery:
				TickArtillery( combat, upgrades );
				return;
		}

		if ( !IsDefense ) return;
		if ( !TryTrackTarget( combat, upgrades, out var muzzle, out var fireDir ) )
			return;

		_fireTimer -= Time.Delta;
		if ( _fireTimer > 0f ) return;

		var dmg = ScaledDamage( upgrades );
		var isCannon = Type == BuildableId.CannonTower;
		combat.FireBullet(
			muzzle,
			fireDir,
			dmg,
			Sfx.Turret,
			isCannon ? new Color( 1f, 0.48f, 0.18f ) : null,
			splashRadius: isCannon ? 85f * GameConstants.RangeScale : 0f,
			splashDamageMult: isCannon ? 0.45f : 0f );
		_fireTimer = Def.FireInterval * DefenseEffects.FireIntervalMultForBuilding( this );
	}

	float ScaledDamage( UpgradeSystem upgrades )
	{
		var dmg = Def.Damage( Level ) + (upgrades?.TurretDamageBonus ?? 0f);
		var core = GameCore.Instance;
		if ( core?.IsCure == true )
			dmg *= TeamBonuses.TurretDamageMult( core );
		return dmg;
	}

	void TickMines( CombatSystem combat, UpgradeSystem upgrades )
	{
		_fireTimer -= Time.Delta;
		if ( _fireTimer > 0f ) return;

		var trigger = DefenseEffects.EffectiveTowerRange( this, upgrades );
		if ( trigger <= 1f ) return;

		var origin = WorldPosition.WithZ( 0f );
		var armed = false;
		foreach ( var z in combat.Zombies )
		{
			if ( z.Dead ) continue;
			if ( (z.Position.WithZ( 0f ) - origin).Length <= trigger )
			{
				armed = true;
				break;
			}
		}

		if ( !armed ) return;

		var splash = MathF.Max( trigger * 1.65f, 140f * GameConstants.RangeScale );
		combat.SplashAt( origin, ScaledDamage( upgrades ), splash, Sfx.Turret );
		DestructionFx.Burst( origin, 0.55f );
		_fireTimer = Def.FireInterval * DefenseEffects.FireIntervalMultForBuilding( this );
	}

	void TickOilSlick( CombatSystem combat, UpgradeSystem upgrades )
	{
		_fireTimer -= Time.Delta;
		if ( _fireTimer > 0f ) return;
		_fireTimer = Def.FireInterval;

		var radius = DefenseEffects.EffectiveTowerRange( this, upgrades );
		if ( radius <= 1f ) return;

		var origin = WorldPosition.WithZ( 0f );
		foreach ( var z in combat.Zombies )
		{
			if ( z.Dead ) continue;
			if ( (z.Position.WithZ( 0f ) - origin).Length > radius ) continue;
			z.ApplySlow( DefenseEffects.OilSlowMult, DefenseEffects.OilSlowLinger );
		}
	}

	void TickArtillery( CombatSystem combat, UpgradeSystem upgrades )
	{
		if ( !TryTrackTarget( combat, upgrades, out var muzzle, out var fireDir, out var aimFeet ) )
			return;

		_fireTimer -= Time.Delta;
		if ( _fireTimer > 0f ) return;

		var dmg = ScaledDamage( upgrades );
		var splash = 220f * GameConstants.RangeScale;
		combat.FireBullet(
			muzzle,
			fireDir,
			dmg * 0.2f,
			Sfx.Turret,
			new Color( 1f, 0.55f, 0.2f ),
			splashRadius: 0f );
		combat.SplashAt( aimFeet, dmg, splash );
		DestructionFx.Burst( aimFeet, 1.35f );
		_fireTimer = Def.FireInterval * DefenseEffects.FireIntervalMultForBuilding( this );
	}

	private bool TryTrackTarget( CombatSystem combat, UpgradeSystem upgrades, out Vector3 muzzle, out Vector3 fireDir ) =>
		TryTrackTarget( combat, upgrades, out muzzle, out fireDir, out _ );

	private bool TryTrackTarget( CombatSystem combat, UpgradeSystem upgrades, out Vector3 muzzle, out Vector3 fireDir, out Vector3 aimFeet )
	{
		muzzle = default;
		fireDir = default;
		aimFeet = default;

		var range = DefenseEffects.EffectiveTowerRange( this, upgrades );
		var pivot = _headRoot.IsValid()
			? _headRoot.WorldPosition
			: WorldPosition + Vector3.Up * (Def.VisualSize.z * 0.55f);

		// No building LOS gate — after the world scale-up, cell occlusion made towers
		// wait until zombies were inside the short-range margin (~1 cell). Towers shoot
		// over walls/scaffolding; solid blockers are a poor fit for this loop.
		var target = combat.NearestZombie( WorldPosition, range );
		if ( target is not null )
		{
			aimFeet = target.Position;
		}
		else
		{
			var hostile = HostileForceSystem.Instance?.Nearest( WorldPosition, range );
			if ( hostile is null ) return false;
			aimFeet = hostile.WorldPos;
		}

		var aimAt = aimFeet + Vector3.Up * 40f;
		var toTarget = aimAt - pivot;
		if ( toTarget.Length < 1f ) return false;

		var flat = toTarget.WithZ( 0f );
		if ( flat.Length < 0.01f )
			flat = _aim.Forward.WithZ( 0f );
		if ( flat.Length < 0.01f )
			flat = Vector3.Forward;
		flat = flat.Normal;

		var desiredYaw = Rotation.LookAt( flat );
		_aim = desiredYaw;

		// World-space aim — building root may be rotated from placement yaw (R).
		if ( _headRoot.IsValid() )
			_headRoot.WorldRotation = Rotation.Slerp( _headRoot.WorldRotation, desiredYaw, MathF.Min( 1f, Time.Delta * 14f ) );

		// All defenses share a hard floor/ceiling on muzzle reach so shots always clear the
		// 1×1 cell rim — gun, cannon, and long-range alike. The old 0.3× CellSize ceiling
		// spawned rounds inside the tower volume and read as blocked vision.
		var cell = GameConstants.CellSize;
		var muzzleReach = Math.Clamp(
			MathF.Abs( _muzzleLocal.x ),
			BuildingVisual.DefenseMuzzleReach( cell ) * 0.9f,
			cell * 0.62f );
		var muzzleLift = Math.Clamp(
			MathF.Abs( _muzzleLocal.z ),
			BuildingVisual.DefenseMuzzleLift( cell ) * 0.5f,
			GameConstants.U( 28f ) );

		// Prefer the commanded aim direction (flat) over head.Forward — LookAt forward axis
		// can disagree with the +X barrel layout during slerp frames.
		muzzle = pivot + flat * muzzleReach + Vector3.Up * muzzleLift;
		fireDir = (aimAt - muzzle);
		if ( fireDir.Length < 1f )
			fireDir = flat;
		else
			fireDir = fireDir.Normal;

		return true;
	}

	public SavedBuilding ToSave() => new()
	{
		Type = Def.Key,
		CellX = CellX,
		CellY = CellY,
		Level = Level,
		Health = Health,
		PlaceOrder = PlaceOrder,
		PaidPlaceCost = PaidPlaceCost,
		YawSteps = YawSteps
	};

	public double SellRefund()
	{
		// AUDIT FIX M1: use recorded spend when present (escalating barracks).
		var placeCost = PaidPlaceCost > 0 ? PaidPlaceCost : Def.PlaceCost;
		var refund = placeCost * GameConstants.SellRefundFraction;
		for ( var i = 1; i < Level; i++ )
			refund += Def.UpgradeCost( i ) * GameConstants.SellRefundFraction;
		return refund;
	}

	// --- ISelectable ---
	public string Name => Def.Name;
	public string Icon => Def.Icon;
	public string Subtitle => Type switch
	{
		BuildableId.Spotlight => "Plot range buff",
		BuildableId.Minefield => "Proximity mines",
		BuildableId.OilSlick => "Slow field",
		BuildableId.Artillery => "Heavy splash",
		BuildableId.AmmoDepot => "Plot fire-rate buff",
		BuildableId.Hardpoint => "Plot armor buff",
		BuildableId.RadioMast => "Recruit coordination",
		_ => Def.Role switch
		{
			BuildingRole.Defense => "Defense tower",
			BuildingRole.Support => "Support pad",
			BuildingRole.Wall => "Barricade",
			_ => "Support building"
		}
	};
	public bool HasHealth => true;
	public Vector3 WorldPos => WorldPosition;
	public float SelectRadius =>
		MathF.Max( Def.VisualSize.x, Def.VisualSize.y )
		* (TileOccupancy.IsWallCell( CellX, CellY ) ? 0.85f : 0.6f);
	public bool IsAlive => !IsDestroyed && GameObject.IsValid();

	public IReadOnlyList<StatLine> Stats
	{
		get
		{
			var list = new List<StatLine> { new( "Level", $"{Level} / {Def.MaxLevel}" ) };
			var up = GameCore.Instance?.Upgrades;
			if ( Def.Role == BuildingRole.Support )
			{
				foreach ( var line in BuildableDef.SupportPlacementStats( Type, up ) )
					list.Add( line );
				if ( Type == BuildableId.Spotlight )
					list.Add( new StatLine( "Active", DefenseEffects.RangeMultForBuilding( this ) > 1.01f ? "Lighting plot" : "Ready" ) );
				else if ( Type == BuildableId.OilSlick )
					list.Add( new StatLine( "Range", $"{DefenseEffects.EffectiveTowerRange( this, up ):0}" ) );
			}
			else if ( Def.Role == BuildingRole.Defense )
			{
				var dmg = Def.Damage( Level ) + (up?.TurretDamageBonus ?? 0);
				var interval = Def.FireInterval * DefenseEffects.FireIntervalMultForBuilding( this );
				var dps = interval > 0 ? dmg / interval : 0f;
				list.Add( new StatLine( "Damage", $"{dmg:0}" ) );
				if ( Type is BuildableId.Minefield or BuildableId.Artillery or BuildableId.CannonTower )
					list.Add( new StatLine( "Splash", "Yes" ) );
				list.Add( new StatLine( "DPS", $"{dps:0}" ) );
				list.Add( new StatLine( "Range", $"{DefenseEffects.EffectiveTowerRange( this, up ):0}" ) );
				if ( Def.FireInterval > 0f )
					list.Add( new StatLine( Type == BuildableId.Minefield ? "Rearm" : "Fire Rate",
						Type == BuildableId.Minefield
							? $"{interval:0.0}s"
							: $"{1f / interval:0.0}/s" ) );
			}
			else if ( Type == BuildableId.Lab )
			{
				list.Add( new StatLine( "Research", $"{CureResearch.LabOutputPerSec( GameCore.Instance ):0.0}/s" ) );
				list.Add( new StatLine( "Lab Points", $"{GameCore.Instance?.Save.CureLabPoints ?? 0:0}" ) );
			}
			else if ( Def.Role == BuildingRole.Management )
			{
				var cap = GameConstants.MaxRecruitCapacity( BuildManager.Instance?.BarracksCount ?? 0 );
				list.Add( new StatLine( "Defenders", $"{DefenderManager.Instance?.Count ?? 0} / {cap}" ) );
				list.Add( new StatLine( "Squad Cap", $"{GameConstants.RecruitsPerBarracks} per Barracks" ) );
				list.Add( new StatLine( "Day Heal", $"{GameConstants.BarracksHealPerSec:0}/s" ) );
				list.Add( new StatLine( "Dawn Heal", "Full in range" ) );
				list.Add( new StatLine( "Heal Range", $"{GameConstants.BarracksHealRadius:0}" ) );
			}
			else if ( Def.Role == BuildingRole.Civic )
			{
				foreach ( var line in BuildableDef.CivicOutputStats( Type ) )
					list.Add( line );
			}
			return list;
		}
	}

	public IReadOnlyList<SelectAction> Actions
	{
		get
		{
			var list = new List<SelectAction>();

			if ( Level < Def.MaxLevel )
			{
				var upCost = Def.UpgradeCost( Level );
				list.Add( new SelectAction
				{
					Label = "Upgrade",
					Detail = SelectHelp.IsDay ? SelectHelp.Cost( upCost ) : "Day only",
					Kind = SelectActionKind.Primary,
					// AUDIT FIX H5: was afford-only — could upgrade mid-night.
					Enabled = SelectHelp.IsDay && SelectHelp.CanAfford( upCost ),
					Invoke = () => BuildManager.Instance?.TryUpgradeBuilding( this )
				} );
			}
			else
			{
				list.Add( new SelectAction { Label = "Max Level", Enabled = false, Invoke = () => { } } );
			}

			if ( Def.Role == BuildingRole.Management )
			{
				list.Add( new SelectAction
				{
					Label = "Manage Recruits",
					Invoke = () => GameCore.Instance?.OpenRecruit()
				} );
			}

			if ( Health < MaxHealth )
			{
				var rm = RepairManager.Instance;
				if ( rm?.IsScheduled( this ) == true )
				{
					list.Add( new SelectAction
					{
						Label = $"Repairing ({SelectHelp.Pct( rm.ProgressFor( this ) )}%)",
						Enabled = false,
						Invoke = () => { }
					} );
				}
				else
				{
					var repairCost = MissingRepairCost();
					var spend = SelectHelp.AffordableRepairSpend( repairCost );
					list.Add( new SelectAction
					{
						Label = spend > 0 && spend + 0.001 < repairCost ? "Partial Repair" : "Repair",
						Detail = SelectHelp.Cost( spend > 0 ? spend : repairCost ),
						Enabled = SelectHelp.CanStartRepair( repairCost ),
						Invoke = () => BuildManager.Instance?.TryRepairBuilding( this )
					} );
				}
			}

			list.Add( new SelectAction
			{
				Label = "Move",
				Detail = SelectHelp.IsDay ? "Relocate" : "Day only",
				Enabled = SelectHelp.IsDay && RepairManager.Instance?.IsScheduled( this ) != true,
				Invoke = () => BuildManager.Instance?.BeginMove( this )
			} );

			list.Add( new SelectAction
			{
				Label = "Sell",
				Detail = SelectHelp.IsDay ? $"+{SelectHelp.Cost( SellRefund() )}" : "Day only",
				Kind = SelectActionKind.Warn,
				// AUDIT FIX H5: Sell had no day gate (Move already did).
				Enabled = SelectHelp.IsDay,
				Invoke = () => BuildManager.Instance?.TrySellBuilding( this )
			} );

			return list;
		}
	}

	// --- Visuals ---

	void RebuildVisual()
	{
		if ( !GameObject.IsValid() ) return;

		foreach ( var child in GameObject.Children.ToArray() )
			child?.Destroy();

		BuildVisual();
		ApplyYaw();
		RefreshVisual();
	}

	private void BuildVisual()
	{
		_parts.Clear();
		_headRoot = null;
		_rubble = null;

		var built = BuildingVisual.Build( GameObject, Type, WorldPosition, includeRubble: true, level: Level );
		_parts.AddRange( built.Parts );
		_headRoot = built.HeadRoot;
		_muzzleLocal = built.MuzzleLocal;
		_rubble = built.Rubble;
	}

	private void RefreshVisual()
	{
		// Destroyed structures are removed immediately — never show rubble foundations.
		if ( IsDestroyed )
		{
			foreach ( var (mr, _) in _parts )
				if ( mr.IsValid() ) mr.GameObject.Enabled = false;
			if ( _rubble.IsValid() ) _rubble.GameObject.Enabled = false;
			if ( GameObject.IsValid() )
				GameObject.Enabled = false;
			return;
		}

		if ( _rubble.IsValid() ) _rubble.GameObject.Enabled = false;

		var t = HealthFraction;
		foreach ( var (mr, baseTint) in _parts )
		{
			if ( !mr.IsValid() ) continue;
			mr.GameObject.Enabled = true;
			mr.Tint = Color.Lerp( new Color( 0.6f, 0.2f, 0.15f ), baseTint, t );
		}
	}
}
