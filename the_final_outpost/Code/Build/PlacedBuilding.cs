namespace FinalOutpost;

/// <summary>A player-placed structure on the build grid, rendered as a stylized multi-part model.</summary>
public sealed class PlacedBuilding : Component, ISelectable
{
	public BuildableId Type { get; private set; }
	public BuildableDef Def => BuildableCatalog.Get( Type );
	public int CellX { get; private set; }
	public int CellY { get; private set; }
	public int PlaceOrder { get; private set; }
	public int Level { get; private set; } = 1;
	public float Health { get; private set; }
	public float MaxHealth { get; private set; }
	public bool IsDestroyed => Health <= 0f;
	public bool IsDefense => Def.Role == BuildingRole.Defense;

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

	public void Setup( BuildableId type, int cellX, int cellY, int level, float health, int placeOrder = 0, double paidPlaceCost = 0 )
	{
		Type = type;
		CellX = cellX;
		CellY = cellY;
		PlaceOrder = placeOrder;
		PaidPlaceCost = paidPlaceCost;
		Level = Math.Clamp( level, 1, Def.MaxLevel );

		var pos = TileOccupancy.WallMountWorldPosition( cellX, cellY );
		_wallMountZ = TileOccupancy.WallMountHeight( cellX, cellY );
		WorldPosition = pos;

		BuildVisual();

		SetMaxHealth( Def.MaxHp( Level ), health );
	}

	public void Relocate( int cellX, int cellY )
	{
		CellX = cellX;
		CellY = cellY;
		RefreshMountHeight( force: true );
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
	}

	protected override void OnUpdate()
	{
		if ( IsDefense && !IsDestroyed )
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

	/// <summary>First-night safety net — starter gun towers cannot be destroyed on night 1.</summary>
	private bool HasNightOneGunTowerProtection()
	{
		if ( Type != BuildableId.GunTower ) return false;
		var core = GameCore.Instance;
		return core?.Phase == GamePhase.Night && core.Save.CurrentNight == 1;
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
		return true;
	}

	public double MissingRepairCost() => Def.RepairCost( MaxHealth - Health );

	public void TickCombat( CombatSystem combat, UpgradeSystem upgrades )
	{
		if ( !IsDefense || IsDestroyed ) return;

		if ( !TryTrackTarget( combat, upgrades, out var muzzle, out var fireDir ) )
			return;

		_fireTimer -= Time.Delta;
		if ( _fireTimer > 0f ) return;

		var dmg = Def.Damage( Level ) + upgrades.TurretDamageBonus;
		var core = GameCore.Instance;
		if ( core?.IsCure == true )
			dmg *= TeamBonuses.TurretDamageMult( core );
		combat.FireBullet( muzzle, fireDir, dmg, Sfx.Turret );
		_fireTimer = Def.FireInterval;
	}

	private bool TryTrackTarget( CombatSystem combat, UpgradeSystem upgrades, out Vector3 muzzle, out Vector3 fireDir )
	{
		muzzle = default;
		fireDir = default;

		var range = Def.Range( Level ) + upgrades.TurretRangeBonus;
		var pivot = _headRoot.IsValid() ? _headRoot.WorldPosition : WorldPosition + Vector3.Up * (Def.VisualSize.z * 0.55f);
		var target = combat.NearestEngageableZombie( WorldPosition, range, pivot );
		if ( target is null ) return false;
		var toTarget = (target.Position - pivot).WithZ( 0f );
		if ( toTarget.Length < 1f ) return false;

		var desired = Rotation.LookAt( toTarget.Normal );
		_aim = desired;

		if ( _headRoot.IsValid() )
		{
			_headRoot.LocalRotation = Rotation.Slerp( _headRoot.LocalRotation, desired, MathF.Min( 1f, Time.Delta * 14f ) );
			fireDir = _headRoot.WorldRotation.Forward;
			muzzle = _headRoot.WorldPosition + _headRoot.WorldRotation * _muzzleLocal;
		}
		else
		{
			fireDir = desired.Forward;
			muzzle = pivot + fireDir * _muzzleLocal.x + Vector3.Up * _muzzleLocal.z;
		}

		if ( !BuildingCollision.HasLineOfFire( muzzle, target.Position ) )
			return false;

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
		PaidPlaceCost = PaidPlaceCost
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
	public string Subtitle => Def.Role switch
	{
		BuildingRole.Defense => "Defense tower",
		BuildingRole.Wall => "Barricade",
		_ => "Support building"
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
			if ( Def.Role == BuildingRole.Defense )
			{
				var dmg = Def.Damage( Level ) + (up?.TurretDamageBonus ?? 0);
				var dps = Def.FireInterval > 0 ? dmg / Def.FireInterval : 0f;
				list.Add( new StatLine( "Damage", $"{dmg:0}" ) );
				list.Add( new StatLine( "DPS", $"{dps:0}" ) );
				list.Add( new StatLine( "Range", $"{Def.Range( Level ) + (up?.TurretRangeBonus ?? 0):0}" ) );
				list.Add( new StatLine( "Fire Rate", $"{(Def.FireInterval > 0 ? 1f / Def.FireInterval : 0):0.0}/s" ) );
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

	/// <summary>Orient placeable wall pieces like perimeter segments (long along the ring, thin through it).</summary>
	Vector3 WallPieceFootprint( float cell, float wallH )
	{
		var side = WallApproach.FromWorldPosition( WorldPosition, Vector3.Zero );
		return side is WallApproachSide.North or WallApproachSide.South
			? new Vector3( cell, cell * 0.55f, wallH )
			: new Vector3( cell * 0.55f, cell, wallH );
	}

	private ModelRenderer Part( Model model, Material mat, Vector3 localPos, Vector3 size, Color textured, Color fallback, GameObject parent = null, bool track = true )
	{
		var useTexture = mat is not null && mat.IsValid() && mat != MeshPrimitives.Mat;
		var tint = useTexture ? textured : fallback;

		var go = new GameObject( parent ?? GameObject, true, "Part" );
		go.LocalPosition = localPos;
		go.LocalScale = MeshPrimitives.ScaleFor( model, size );

		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = model;
		mr.MaterialOverride = mat;
		mr.Tint = tint;

		if ( track )
			_parts.Add( (mr, tint) );

		return mr;
	}

	private void EnsureHeadRoot( float pivotZ )
	{
		_headRoot = new GameObject( GameObject, true, "TurretHead" );
		_headRoot.LocalPosition = new Vector3( 0f, 0f, pivotZ );
	}

	private void HeadPart( Model model, Material mat, Vector3 worldLocalPos, float pivotZ, Vector3 size, Color textured, Color fallback )
	{
		var local = new Vector3( worldLocalPos.x, worldLocalPos.y, worldLocalPos.z - pivotZ );
		Part( model, mat, local, size, textured, fallback, _headRoot );
	}

	private static readonly Color StoneFallback = new( 0.62f, 0.62f, 0.64f );
	private static readonly Color WoodFallback = new( 0.5f, 0.36f, 0.22f );
	private static readonly Color RoofFallback = new( 0.78f, 0.28f, 0.16f );

	private void BuildVisual()
	{
		var box = MeshPrimitives.Box;
		var cyl = MeshPrimitives.Cylinder;
		var pyr = MeshPrimitives.Pyramid;
		var stone = StylizedMaterials.Stone;
		var wood = StylizedMaterials.Wood;
		var roof = StylizedMaterials.Roof;

		var white = Color.White;
		// Every placeable is 1×1 — primary XY footprint always fills the cell.
		var c = GameConstants.CellSize;

		switch ( Type )
		{
			case BuildableId.GunTower:
				Part( cyl, stone, new Vector3( 0, 0, c * 0.17f ), new Vector3( c, c, c * 0.33f ), white, StoneFallback );
				Part( cyl, stone, new Vector3( 0, 0, c * 0.67f ), new Vector3( c * 0.79f, c * 0.79f, c * 0.67f ), new Color( 0.82f, 0.88f, 1f ), new Color( 0.5f, 0.58f, 0.72f ) );
				EnsureHeadRoot( c * 0.92f );
				HeadPart( box, stone, new Vector3( 0, 0, c * 1.03f ), c * 0.92f, new Vector3( c * 0.92f, c * 0.92f, c * 0.13f ), white, StoneFallback );
				HeadPart( box, wood, new Vector3( c * 0.33f, 0, c ), c * 0.92f, new Vector3( c * 0.5f, c * 0.17f, c * 0.17f ), new Color( 0.35f, 0.37f, 0.4f ), new Color( 0.3f, 0.3f, 0.32f ) );
				_muzzleLocal = new Vector3( c * 0.58f, 0f, c * 0.08f );
				break;

			case BuildableId.CannonTower:
				Part( cyl, stone, new Vector3( 0, 0, c * 0.18f ), new Vector3( c, c, c * 0.37f ), white, StoneFallback );
				Part( box, wood, new Vector3( 0, 0, c * 0.52f ), new Vector3( c * 0.57f, c * 0.57f, c * 0.3f ), white, WoodFallback );
				EnsureHeadRoot( c * 0.52f );
				HeadPart( box, stone, new Vector3( c * 0.23f, 0, c * 0.63f ), c * 0.52f, new Vector3( c * 0.73f, c * 0.3f, c * 0.3f ), new Color( 0.3f, 0.32f, 0.35f ), new Color( 0.28f, 0.3f, 0.33f ) );
				_muzzleLocal = new Vector3( c * 0.6f, 0f, c * 0.12f );
				break;

			case BuildableId.LongRangeTower:
				Part( cyl, stone, new Vector3( 0, 0, c * 0.2f ), new Vector3( c, c, c * 0.4f ), white, StoneFallback );
				EnsureHeadRoot( c * 0.4f );
				HeadPart( cyl, stone, new Vector3( 0, 0, c * 0.77f ), c * 0.4f, new Vector3( c * 0.7f, c * 0.7f, c * 0.73f ), new Color( 0.85f, 1f, 0.8f ), new Color( 0.5f, 0.7f, 0.45f ) );
				HeadPart( box, wood, new Vector3( 0, 0, c * 1.2f ), c * 0.4f, new Vector3( c, c, c * 0.13f ), white, WoodFallback );
				HeadPart( pyr, roof, new Vector3( 0, 0, c * 1.43f ), c * 0.4f, new Vector3( c * 1.08f, c * 1.08f, c * 0.33f ), white, RoofFallback );
				HeadPart( box, wood, new Vector3( c * 0.27f, 0, c * 1.3f ), c * 0.4f, new Vector3( c * 0.47f, c * 0.13f, c * 0.13f ), new Color( 0.45f, 0.38f, 0.28f ), new Color( 0.38f, 0.32f, 0.24f ) );
				_muzzleLocal = new Vector3( c * 0.5f, 0f, c * 1.3f );
				break;

			case BuildableId.WallPiece:
			{
				var wallH = GameConstants.WallHeight;
				var frame = new GameObject( GameObject, true, "WallFrame" );
				frame.LocalPosition = new Vector3( 0f, 0f, wallH * 0.5f );
				var footprint = WallPieceFootprint( c, wallH );
				WallScaffoldVisual.Build(
					frame,
					footprint,
					( mr, tint ) => _parts.Add( (mr, tint) ),
					WorldPosition );
				break;
			}

			case BuildableId.Barracks:
				Part( box, wood, new Vector3( 0, 0, c * 0.33f ), new Vector3( c, c, c * 0.67f ), white, WoodFallback );
				Part( pyr, roof, new Vector3( 0, 0, c * 0.88f ), new Vector3( c * 1.12f, c * 1.12f, c * 0.43f ), white, RoofFallback );
				Part( box, stone, new Vector3( c * 0.5f, 0, c * 0.23f ), new Vector3( c * 0.07f, c * 0.43f, c * 0.47f ), new Color( 0.5f, 0.4f, 0.3f ), new Color( 0.45f, 0.35f, 0.25f ) );
				break;

			case BuildableId.Lab:
				Part( box, stone, new Vector3( 0, 0, c * 0.4f ), new Vector3( c, c, c * 0.8f ), white, new Color( 0.65f, 0.72f, 0.82f ) );
				Part( box, stone, new Vector3( 0, 0, c * 0.97f ), new Vector3( c * 0.33f, c * 0.33f, c * 0.6f ), white, new Color( 0.45f, 0.85f, 0.95f ) );
				break;

			case BuildableId.Farm:
				Part( box, wood, new Vector3( 0, 0, c * 0.27f ), new Vector3( c, c, c * 0.53f ), white, new Color( 0.55f, 0.42f, 0.28f ) );
				Part( pyr, MeshPrimitives.Mat, new Vector3( -c * 0.3f, c * 0.2f, c * 0.1f ), new Vector3( c * 0.47f, c * 0.47f, c * 0.27f ), white, new Color( 0.45f, 0.82f, 0.38f ) );
				Part( pyr, MeshPrimitives.Mat, new Vector3( c * 0.27f, -c * 0.23f, c * 0.1f ), new Vector3( c * 0.4f, c * 0.4f, c * 0.23f ), white, new Color( 0.42f, 0.78f, 0.35f ) );
				break;

			case BuildableId.Factory:
				Part( box, stone, new Vector3( 0, 0, c * 0.37f ), new Vector3( c, c, c * 0.73f ), white, new Color( 0.62f, 0.62f, 0.66f ) );
				Part( cyl, stone, new Vector3( c * 0.4f, 0, c * 0.87f ), new Vector3( c * 0.27f, c * 0.27f, c * 0.47f ), white, new Color( 0.45f, 0.45f, 0.48f ) );
				break;

			case BuildableId.Library:
				Part( box, wood, new Vector3( 0, 0, c * 0.4f ), new Vector3( c, c, c * 0.8f ), white, new Color( 0.55f, 0.72f, 0.95f ) );
				Part( pyr, roof, new Vector3( 0, 0, c * 0.93f ), new Vector3( c * 1.08f, c * 1.08f, c * 0.37f ), white, RoofFallback );
				break;

			case BuildableId.School:
				Part( box, wood, new Vector3( 0, 0, c * 0.43f ), new Vector3( c, c, c * 0.87f ), white, new Color( 0.62f, 0.78f, 0.55f ) );
				Part( pyr, roof, new Vector3( 0, 0, c ), new Vector3( c * 1.13f, c * 1.13f, c * 0.4f ), white, RoofFallback );
				break;

			case BuildableId.Hospital:
				Part( box, stone, new Vector3( 0, 0, c * 0.43f ), new Vector3( c, c, c * 0.87f ), white, new Color( 0.92f, 0.92f, 0.95f ) );
				Part( box, stone, new Vector3( 0, 0, c * 0.97f ), new Vector3( c * 0.3f, c * 0.3f, c * 0.53f ), white, new Color( 0.75f, 0.85f, 0.95f ) );
				break;

			case BuildableId.Shop:
				Part( box, wood, new Vector3( 0, 0, c * 0.33f ), new Vector3( c, c, c * 0.67f ), white, new Color( 0.85f, 0.62f, 0.32f ) );
				Part( pyr, roof, new Vector3( 0, 0, c * 0.8f ), new Vector3( c * 1.08f, c * 1.08f, c * 0.3f ), white, RoofFallback );
				break;
		}

		_rubble = Part( box, stone, new Vector3( 0, 0, 8 ), new Vector3( c, c, 16 ), new Color( 0.32f, 0.28f, 0.24f ), new Color( 0.25f, 0.2f, 0.18f ), track: false );
		_rubble.GameObject.Enabled = false;
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
