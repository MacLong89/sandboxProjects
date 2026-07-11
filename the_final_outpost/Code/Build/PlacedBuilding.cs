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

	private readonly List<(ModelRenderer Renderer, Color Base)> _parts = new();
	private ModelRenderer _rubble;
	private GameObject _headRoot;
	private Vector3 _muzzleLocal = new( 30f, 0f, 0f );
	private float _fireTimer;
	private Rotation _aim = Rotation.Identity;

	public Vector3 Center => WorldPosition;
	public float HealthFraction => MaxHealth <= 0f ? 0f : Health / MaxHealth;

	public void Setup( BuildableId type, int cellX, int cellY, int level, float health, int placeOrder = 0 )
	{
		Type = type;
		CellX = cellX;
		CellY = cellY;
		PlaceOrder = placeOrder;
		Level = Math.Clamp( level, 1, Def.MaxLevel );

		var pos = BuildGrid.CellToWorld( cellX, cellY );
		WorldPosition = pos;

		BuildVisual();

		SetMaxHealth( Def.MaxHp( Level ), health );
	}

	public void Relocate( int cellX, int cellY )
	{
		CellX = cellX;
		CellY = cellY;
		WorldPosition = BuildGrid.CellToWorld( cellX, cellY );
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

		if ( IsDestroyed && IsDefense )
		{
			BuildManager.Instance?.HandleTowerDestroyed( this );
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
		PlaceOrder = PlaceOrder
	};

	public double SellRefund()
	{
		var refund = Def.PlaceCost * GameConstants.SellRefundFraction;
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
	public float SelectRadius => MathF.Max( Def.VisualSize.x, Def.VisualSize.y ) * 0.6f;
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
					Detail = SelectHelp.Cost( upCost ),
					Kind = SelectActionKind.Primary,
					Enabled = SelectHelp.CanAfford( upCost ),
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
					list.Add( new SelectAction
					{
						Label = "Repair",
						Detail = SelectHelp.Cost( repairCost ),
						Enabled = SelectHelp.IsDay && SelectHelp.CanAfford( repairCost ),
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
				Detail = $"+{SelectHelp.Cost( SellRefund() )}",
				Kind = SelectActionKind.Warn,
				Invoke = () => BuildManager.Instance?.TrySellBuilding( this )
			} );

			return list;
		}
	}

	// --- Visuals ---

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

		switch ( Type )
		{
			case BuildableId.GunTower:
				Part( cyl, stone, new Vector3( 0, 0, 10 ), new Vector3( 48, 48, 20 ), white, StoneFallback );
				Part( cyl, stone, new Vector3( 0, 0, 40 ), new Vector3( 38, 38, 40 ), new Color( 0.82f, 0.88f, 1f ), new Color( 0.5f, 0.58f, 0.72f ) );
				EnsureHeadRoot( 55f );
				HeadPart( box, stone, new Vector3( 0, 0, 62 ), 55f, new Vector3( 44, 44, 8 ), white, StoneFallback );
				HeadPart( box, wood, new Vector3( 20, 0, 60 ), 55f, new Vector3( 30, 10, 10 ), new Color( 0.35f, 0.37f, 0.4f ), new Color( 0.3f, 0.3f, 0.32f ) );
				_muzzleLocal = new Vector3( 35f, 0f, 5f );
				break;

			case BuildableId.CannonTower:
				Part( cyl, stone, new Vector3( 0, 0, 11 ), new Vector3( 56, 56, 22 ), white, StoneFallback );
				Part( box, wood, new Vector3( 0, 0, 31 ), new Vector3( 34, 34, 18 ), white, WoodFallback );
				EnsureHeadRoot( 31f );
				HeadPart( box, stone, new Vector3( 14, 0, 38 ), 31f, new Vector3( 44, 18, 18 ), new Color( 0.3f, 0.32f, 0.35f ), new Color( 0.28f, 0.3f, 0.33f ) );
				_muzzleLocal = new Vector3( 36f, 0f, 7f );
				break;

			case BuildableId.LongRangeTower:
				Part( cyl, stone, new Vector3( 0, 0, 12 ), new Vector3( 40, 40, 24 ), white, StoneFallback );
				EnsureHeadRoot( 24f );
				HeadPart( cyl, stone, new Vector3( 0, 0, 46 ), 24f, new Vector3( 28, 28, 44 ), new Color( 0.85f, 1f, 0.8f ), new Color( 0.5f, 0.7f, 0.45f ) );
				HeadPart( box, wood, new Vector3( 0, 0, 72 ), 24f, new Vector3( 40, 40, 8 ), white, WoodFallback );
				HeadPart( pyr, roof, new Vector3( 0, 0, 86 ), 24f, new Vector3( 46, 46, 20 ), white, RoofFallback );
				HeadPart( box, wood, new Vector3( 16, 0, 78 ), 24f, new Vector3( 28, 8, 8 ), new Color( 0.45f, 0.38f, 0.28f ), new Color( 0.38f, 0.32f, 0.24f ) );
				_muzzleLocal = new Vector3( 30f, 0f, 78f );
				break;

			case BuildableId.WallPiece:
			{
				// Match the starter perimeter walls: a single stone slab filling the cell so
				// adjacent placed walls tile into a seamless barrier. Same material + tint as WallSegment.
				var wallH = GameConstants.WallHeight;
				var wallTint = new Color( 0.55f, 0.58f, 0.62f );
				Part( box, stone, new Vector3( 0, 0, wallH * 0.5f ), new Vector3( 60, 60, wallH ), white, wallTint );
				break;
			}

			case BuildableId.Barracks:
				Part( box, wood, new Vector3( 0, 0, 20 ), new Vector3( 64, 64, 40 ), white, WoodFallback );
				Part( pyr, roof, new Vector3( 0, 0, 53 ), new Vector3( 74, 74, 26 ), white, RoofFallback );
				Part( box, stone, new Vector3( 32, 0, 14 ), new Vector3( 4, 26, 28 ), new Color( 0.5f, 0.4f, 0.3f ), new Color( 0.45f, 0.35f, 0.25f ) );
				break;

			case BuildableId.Lab:
				Part( box, stone, new Vector3( 0, 0, 24 ), new Vector3( 56, 56, 48 ), white, new Color( 0.65f, 0.72f, 0.82f ) );
				Part( box, stone, new Vector3( 0, 0, 58 ), new Vector3( 20, 20, 36 ), white, new Color( 0.45f, 0.85f, 0.95f ) );
				break;

			case BuildableId.Farm:
				Part( box, wood, new Vector3( 0, 0, 16 ), new Vector3( 58, 58, 32 ), white, new Color( 0.55f, 0.42f, 0.28f ) );
				Part( pyr, MeshPrimitives.Mat, new Vector3( -18, 12, 6 ), new Vector3( 28, 28, 16 ), white, new Color( 0.45f, 0.82f, 0.38f ) );
				Part( pyr, MeshPrimitives.Mat, new Vector3( 16, -14, 6 ), new Vector3( 24, 24, 14 ), white, new Color( 0.42f, 0.78f, 0.35f ) );
				break;

			case BuildableId.Factory:
				Part( box, stone, new Vector3( 0, 0, 22 ), new Vector3( 64, 64, 44 ), white, new Color( 0.62f, 0.62f, 0.66f ) );
				Part( cyl, stone, new Vector3( 24, 0, 52 ), new Vector3( 16, 16, 28 ), white, new Color( 0.45f, 0.45f, 0.48f ) );
				break;

			case BuildableId.Library:
				Part( box, wood, new Vector3( 0, 0, 24 ), new Vector3( 56, 56, 48 ), white, new Color( 0.55f, 0.72f, 0.95f ) );
				Part( pyr, roof, new Vector3( 0, 0, 56 ), new Vector3( 64, 64, 22 ), white, RoofFallback );
				break;

			case BuildableId.School:
				Part( box, wood, new Vector3( 0, 0, 26 ), new Vector3( 60, 60, 52 ), white, new Color( 0.62f, 0.78f, 0.55f ) );
				Part( pyr, roof, new Vector3( 0, 0, 60 ), new Vector3( 68, 68, 24 ), white, RoofFallback );
				break;

			case BuildableId.Hospital:
				Part( box, stone, new Vector3( 0, 0, 26 ), new Vector3( 62, 62, 52 ), white, new Color( 0.92f, 0.92f, 0.95f ) );
				Part( box, stone, new Vector3( 0, 0, 58 ), new Vector3( 18, 18, 32 ), white, new Color( 0.75f, 0.85f, 0.95f ) );
				break;

			case BuildableId.Shop:
				Part( box, wood, new Vector3( 0, 0, 20 ), new Vector3( 54, 54, 40 ), white, new Color( 0.85f, 0.62f, 0.32f ) );
				Part( pyr, roof, new Vector3( 0, 0, 48 ), new Vector3( 60, 60, 18 ), white, RoofFallback );
				break;
		}

		_rubble = Part( box, stone, new Vector3( 0, 0, 8 ), new Vector3( Def.VisualSize.x, Def.VisualSize.y, 16 ), new Color( 0.32f, 0.28f, 0.24f ), new Color( 0.25f, 0.2f, 0.18f ), track: false );
		_rubble.GameObject.Enabled = false;
	}

	private void RefreshVisual()
	{
		if ( IsDestroyed )
		{
			foreach ( var (mr, _) in _parts )
				if ( mr.IsValid() ) mr.GameObject.Enabled = false;
			if ( _rubble.IsValid() ) _rubble.GameObject.Enabled = true;
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
