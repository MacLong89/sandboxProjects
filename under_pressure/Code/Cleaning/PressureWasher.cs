namespace UnderPressure;

/// <summary>
/// The core verb. Hold attack to spray: raycast from the eye, erase dirt on whatever
/// cleanable surface is hit, and drain the water tank. A bright impact blob sells the hit.
/// </summary>
public sealed class PressureWasher : Component
{
	public static PressureWasher Instance { get; private set; }

	public float Water { get; private set; }
	public float WaterMax { get; private set; } = GameConstants.TankBase;
	public float WaterFraction => WaterMax <= 0 ? 0f : Water / WaterMax;

	// Hand tools burn stamina instead of water (see ToolDef.UsesStamina).
	public float Stamina { get; private set; }
	public float StaminaMax { get; private set; } = GameConstants.StaminaBase;
	public float StaminaFraction => StaminaMax <= 0 ? 0f : Stamina / StaminaMax;

	public bool IsSpraying { get; private set; }

	/// <summary>The pest currently under the crosshair, if any — for the HUD "use this tool" cue.</summary>
	public EnemyDef AimedEnemy { get; private set; }

	// Spray: nozzle-matched circular droplets + mist spheres.
	private const int StreamCoreSegments = 20;
	private const int StreamOuterSegments = 12;
	private const int SplashDroplets = 14;
	private static readonly Color CoreTint = new( 0.82f, 0.95f, 1f, 0.82f );
	private static readonly Color OuterTint = new( 0.45f, 0.82f, 1f, 0.35f );
	private static readonly Color SplashTint = new( 0.58f, 0.88f, 1f, 0.55f );

	private GameObject[] _core;
	private GameObject[] _outer;
	private GameObject[] _splash;
	private ModelRenderer[] _coreR;
	private ModelRenderer[] _outerR;
	private ModelRenderer[] _splashR;
	private float[] _corePhase, _coreOffset;
	private float[] _outerPhase, _outerOffset, _outerLane;
	private float[] _splashPhase, _splashRadius, _splashSize;
	private bool _poolsReady;

	private float _refillDelay;
	private float _staminaDelay;
	private float _tickCooldown;

	// Looping "in use" sound for the equipped tool: plays while M1 is held, re-fires when the
	// clip finishes (so it loops), restarts on a tool swap, and cuts off on release.
	private SoundHandle _toolSound;
	private ToolType _toolSoundType;

	protected override void OnAwake()
	{
		Instance = this;
		Water = WaterMax;
		Stamina = StaminaMax;
		EnsureDropletPools();
	}

	private void EnsureDropletPools()
	{
		if ( _poolsReady )
			return;

		_core = new GameObject[StreamCoreSegments];
		_coreR = new ModelRenderer[StreamCoreSegments];
		_corePhase = new float[StreamCoreSegments];
		_coreOffset = new float[StreamCoreSegments];
		for ( int i = 0; i < StreamCoreSegments; i++ )
		{
			(_core[i], _coreR[i]) = CreateSprayPart( "SprayCore", MeshPrimitives.Sphere, GameMaterials.WaterSpray );
			_corePhase[i] = (i + 0.5f) / StreamCoreSegments;
			_coreOffset[i] = Game.Random.Float( -0.08f, 0.08f );
		}

		_outer = new GameObject[StreamOuterSegments];
		_outerR = new ModelRenderer[StreamOuterSegments];
		_outerPhase = new float[StreamOuterSegments];
		_outerOffset = new float[StreamOuterSegments];
		_outerLane = new float[StreamOuterSegments];
		for ( int i = 0; i < StreamOuterSegments; i++ )
		{
			(_outer[i], _outerR[i]) = CreateSprayPart( "SprayOuter", MeshPrimitives.Sphere, GameMaterials.WaterSpray );
			_outerPhase[i] = Game.Random.Float( 0.2f, 1f );
			_outerOffset[i] = Game.Random.Float( 0.4f, 1f );
			_outerLane[i] = Game.Random.Float( 0f, 1f );
		}

		_splash = new GameObject[SplashDroplets];
		_splashR = new ModelRenderer[SplashDroplets];
		_splashPhase = new float[SplashDroplets];
		_splashRadius = new float[SplashDroplets];
		_splashSize = new float[SplashDroplets];
		for ( int i = 0; i < SplashDroplets; i++ )
		{
			(_splash[i], _splashR[i]) = CreateSprayPart( "SplashDroplet", MeshPrimitives.Sphere, GameMaterials.WaterSpray );
			_splashPhase[i] = Game.Random.Float( 0f, 1f );
			_splashRadius[i] = Game.Random.Float( 0.2f, 1f );
			_splashSize[i] = Game.Random.Float( 0.9f, 2.2f );
		}

		_poolsReady = true;
	}

	protected override void OnStart()
	{
		EnsureDropletPools();
	}

	private static (GameObject Go, ModelRenderer Renderer) CreateSprayPart( string name, Model model, Material material )
	{
		var go = new GameObject( true, name );
		var r = go.Components.Create<ModelRenderer>();
		r.Model = model;
		r.MaterialOverride = material;
		go.Enabled = false;
		return (go, r);
	}

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
		StopToolSound();
		DestroyPool( _core );
		DestroyPool( _outer );
		DestroyPool( _splash );
	}

	/// <summary>Start (or loop-restart) the equipped tool's use-sound while M1 is held.</summary>
	private void TickToolSound( ToolDef tool )
	{
		// Swapping tools mid-hold: kill the old loop so the new tool's sound takes over.
		if ( _toolSound is { IsValid: true } && _toolSoundType != tool.Type )
			StopToolSound();

		// (Re)fire when nothing is playing — covers first press and looping past clip length.
		if ( _toolSound is not { IsValid: true, IsPlaying: true } )
		{
			_toolSound = Sfx.PlayHandle( tool.UseSound );
			_toolSoundType = tool.Type;
		}
	}

	private void StopToolSound()
	{
		_toolSound?.Stop( 0.05f );
		_toolSound = null;
	}

	private static void DestroyPool( GameObject[] pool )
	{
		if ( pool is null ) return;
		foreach ( var go in pool )
			go?.Destroy();
	}

	protected override void OnUpdate()
	{
		var core = GameCore.Instance;
		var player = PressurePlayer.Instance;
		if ( core is null || player is null )
			return;

		WaterMax = core.Upgrades.TankMax;
		Water = Math.Min( Water, WaterMax );
		StaminaMax = core.Upgrades.StaminaMax;
		Stamina = Math.Min( Stamina, StaminaMax );

		var tool = core.Tools.EquippedDef;

		// Don't act while a menu is up. (The van uses right-click, so left-click can still
		// clean even when the crosshair is on the van.)
		var blocked = core.IsUiBlocking;

		UpdateAim( player, blocked );
		var hasCharge = tool.UsesWater ? Water > 0f
			: tool.UsesStamina ? Stamina > 0f
			: true;
		var wantsUse = !blocked && Input.Down( "Attack1" ) && hasCharge;
		IsSpraying = wantsUse;
		_tickCooldown = Math.Max( 0f, _tickCooldown - Time.Delta );

		if ( wantsUse )
		{
			// Every tool has a looping use-sound that plays while held (see TickToolSound).
			TickToolSound( tool );

			if ( tool.UsesWater )
			{
				Water = Math.Max( 0f, Water - GameConstants.TankDrainPerSecond * Time.Delta );
				_refillDelay = GameConstants.TankRefillDelay;
			}
			else if ( tool.UsesStamina )
			{
				Stamina = Math.Max( 0f, Stamina - GameConstants.StaminaDrainPerSecond * Time.Delta );
				_staminaDelay = GameConstants.StaminaRefillDelay;
			}

			DoUse( core, player, tool );
		}
		else
		{
			StopToolSound();
			HideStream();
			HideSplash();
		}

		// Recover whichever meter isn't actively being spent this frame.
		if ( !(wantsUse && tool.UsesWater) )
		{
			_refillDelay = Math.Max( 0f, _refillDelay - Time.Delta );
			if ( _refillDelay <= 0f && Water < WaterMax )
				Water = Math.Min( WaterMax, Water + GameConstants.TankRefillPerSecond * Time.Delta );
		}
		if ( !(wantsUse && tool.UsesStamina) )
		{
			_staminaDelay = Math.Max( 0f, _staminaDelay - Time.Delta );
			if ( _staminaDelay <= 0f && Stamina < StaminaMax )
				Stamina = Math.Min( StaminaMax, Stamina + GameConstants.StaminaRefillPerSecond * Time.Delta );
		}
	}

	private void DoUse( GameCore core, PressurePlayer player, ToolDef tool )
	{
		var eye = player.EyePosition;
		var fwd = player.EyeForward;
		var nozzle = player.NozzlePosition;

		// Each tool's reach comes from its own upgrades (the washer scales with Hose Reach;
		// hand tools keep their fixed close range).
		var range = core.Upgrades.RangeFor( tool );

		// Trace against everything (ignoring the player) so the jet stops on any solid —
		// wall, fence, window — and reaches full range through open air/sky. Cleaning only
		// happens when the surface's required tool matches the one we're holding.
		var tr = Scene.Trace
			.Ray( eye, eye + fwd * range )
			.IgnoreGameObjectHierarchy( player.GameObject )
			.Run();

		var end = tr.Hit ? tr.EndPosition : eye + fwd * range;

		// The pressure washer sprays a stream of droplets; hand tools spray nothing.
		if ( tool.UsesWater )
			ShowStream( nozzle, end );
		else
			HideStream();

		// Radius and power are per-tool now: the washer reads its Nozzle/Pressure upgrades,
		// the scrub brush and squeegee read their own.
		var brush = core.Upgrades.RadiusFor( tool );
		var power = core.Upgrades.PowerFor( tool );

		if ( tool.Type == ToolType.PressureWasher && core.Jobs.Index == GameConstants.Level1JobIndex )
		{
			power *= GameConstants.Level1WasherPowerMultiplier;
			brush *= GameConstants.Level1WasherRadiusMultiplier;
		}

		if ( tool.Type == ToolType.Gun )
		{
			HideStream();
			HideSplash();

			if ( tr.Hit )
			{
				var enemy = FindEnemy( tr.GameObject );
				if ( enemy is not null )
					enemy.TryDamage( tool.Type, power * Time.Delta * 2.5f );

				if ( _tickCooldown <= 0f )
				{
					Sfx.Play( Sfx.Gunshot, 0.55f );
					_tickCooldown = 0.22f;
				}
			}

			return;
		}

		if ( tr.Hit )
		{
			// Pests block the surface behind them — hit the pest first with the right tool.
			var enemy = FindEnemy( tr.GameObject );
			if ( enemy is not null )
			{
				enemy.TryDamage( tool.Type, power * Time.Delta );
			}
			else
			{
				var surface = tr.GameObject?.Components.Get<CleanableSurface>();
				if ( surface is not null )
				{
					var amount = power * Time.Delta;
					var completed = surface.CleanAt( tr.EndPosition, brush, amount, tool.SquareBrush, tool.Type );

					if ( completed > 0 && _tickCooldown <= 0f )
					{
						Sfx.Play( Sfx.CleanTick, Sfx.CleanTickVolume );
						_tickCooldown = 0.08f;
					}
				}
			}

			if ( tool.UsesWater )
				ShowSplash( tr.EndPosition, tr.Normal, brush );
			else
				HideSplash();
		}
		else
		{
			HideSplash();
		}
	}

	/// <summary>Cheap per-frame crosshair trace so the HUD can name the pest you're looking at
	/// and tell you which tool defeats it.</summary>
	private void UpdateAim( PressurePlayer player, bool blocked )
	{
		if ( blocked )
		{
			AimedEnemy = null;
			return;
		}

		var eye = player.EyePosition;
		var fwd = player.EyeForward;
		var tr = Scene.Trace
			.Ray( eye, eye + fwd * 600f )
			.IgnoreGameObjectHierarchy( player.GameObject )
			.Run();

		AimedEnemy = tr.Hit ? FindEnemy( tr.GameObject )?.Def : null;
	}

	/// <summary>Find the Enemy component on the hit object or any of its parents.</summary>
	private static Enemy FindEnemy( GameObject go )
	{
		while ( go.IsValid() )
		{
			var enemy = go.Components.Get<Enemy>();
			if ( enemy is not null )
				return enemy;

			go = go.Parent;
		}

		return null;
	}

	/// <summary>Circular jet droplets sized to the wand nozzle, fanning out downrange.</summary>
	private void ShowStream( Vector3 from, Vector3 to )
	{
		EnsureDropletPools();
		if ( _core is null || _outer is null )
			return;

		var delta = to - from;
		var length = delta.Length;
		if ( length < 1f )
		{
			HideStream();
			return;
		}

		var dir = delta / length;
		Basis( dir, out var right, out var up );
		GetNozzleCrossSection( out var nozzleY, out var nozzleZ );

		var now = Time.Now;
		var baseDiam = (nozzleY + nozzleZ) * 0.5f;

		for ( int i = 0; i < _core.Length; i++ )
		{
			var d = _core[i];
			var r = _coreR[i];
			if ( !d.IsValid() || r is null ) continue;

			var t = (_corePhase[i] + now * 4.5f) % 1f;
			var fan = t * t;
			var diam = baseDiam * (1f + fan * 0.45f);
			var lateral = fan * 1.4f * _coreOffset[i];

			d.WorldPosition = from + dir * (length * t)
				+ right * lateral
				+ up * (_coreOffset[i] * fan * 0.7f);
			d.LocalScale = MeshPrimitives.SphereScale( diam );
			r.Tint = CoreTint.WithAlpha( 0.62f + 0.3f * (1f - t) );
			d.Enabled = true;
		}

		for ( int i = 0; i < _outer.Length; i++ )
		{
			var d = _outer[i];
			var r = _outerR[i];
			if ( !d.IsValid() || r is null ) continue;

			var t = (_outerPhase[i] + now * 3.6f) % 1f;
			if ( t < 0.18f )
			{
				d.Enabled = false;
				continue;
			}

			var fan = (t - 0.18f) / 0.82f;
			var spread = fan * (3f + length * 0.012f) * _outerOffset[i];
			var lane = _outerLane[i] * MathF.Tau;
			var diam = MathF.Max( 1.1f, baseDiam * (0.38f + fan * 0.55f) );

			d.WorldPosition = from + dir * (length * t)
				+ right * (MathF.Cos( lane ) * spread)
				+ up * (MathF.Sin( lane ) * spread);
			d.LocalScale = MeshPrimitives.SphereScale( diam );
			r.Tint = OuterTint.WithAlpha( 0.12f + 0.22f * (1f - fan) );
			d.Enabled = true;
		}
	}

	/// <summary>Matches <see cref="WandView"/> nozzle tip proportions by upgrade tier.</summary>
	private static void GetNozzleCrossSection( out float crossY, out float crossZ )
	{
		var lvl = GameCore.Instance?.Upgrades.Level( UpgradeId.Nozzle ) ?? 0;
		var tier = lvl <= 0 ? 0 : lvl < 4 ? 1 : lvl < 9 ? 2 : 3;
		crossY = 3f + tier * 0.5f;
		crossZ = 3f + tier * 0.5f;
	}

	/// <summary>Ricochet droplets off the surface where the jet lands.</summary>
	private void ShowSplash( Vector3 pos, Vector3 normal, float radius )
	{
		EnsureDropletPools();
		if ( _splash is null || normal.Length < 0.01f )
		{
			HideSplash();
			return;
		}

		Basis( normal, out var right, out var up );

		var now = Time.Now;
		var reach = radius * 2.2f + 12f;
		for ( int i = 0; i < _splash.Length; i++ )
		{
			var d = _splash[i];
			var r = _splashR[i];
			if ( !d.IsValid() || r is null ) continue;

			var t = (_splashPhase[i] + now * (3.5f + _splashRadius[i] * 2f)) % 1f;

			var az = _splashPhase[i] * MathF.Tau;
			var tangent = right * MathF.Cos( az ) + up * MathF.Sin( az );
			var launch = (normal * (0.7f + 0.5f * _splashRadius[i]) + tangent * (0.8f + 0.6f * _splashRadius[i])).Normal;

			var travel = reach * t;
			var drop = reach * 0.75f * t * t;

			d.WorldPosition = pos + launch * travel - Vector3.Up * drop;
			d.LocalScale = MeshPrimitives.SphereScale( _splashSize[i] * (1f - 0.45f * t) );
			r.Tint = SplashTint.WithAlpha( 0.55f * (1f - t * 0.85f) );
			d.Enabled = true;
		}
	}

	private void HideStream()
	{
		Hide( _core );
		Hide( _outer );
	}
	private void HideSplash() => Hide( _splash );

	private static void Hide( GameObject[] pool )
	{
		if ( pool is null ) return;
		foreach ( var go in pool )
			if ( go.IsValid() && go.Enabled )
				go.Enabled = false;
	}

	/// <summary>Two axes perpendicular to <paramref name="dir"/>, for scattering droplets around it.</summary>
	private static void Basis( Vector3 dir, out Vector3 right, out Vector3 up )
	{
		right = Vector3.Cross( dir, Vector3.Up );
		if ( right.Length < 0.01f )
			right = Vector3.Cross( dir, Vector3.Forward );
		right = right.Normal;
		up = Vector3.Cross( right, dir ).Normal;
	}

	public void ResetTank()
	{
		WaterMax = GameCore.Instance?.Upgrades.TankMax ?? GameConstants.TankBase;
		Water = WaterMax;
		StaminaMax = GameCore.Instance?.Upgrades.StaminaMax ?? GameConstants.StaminaBase;
		Stamina = StaminaMax;
	}

	public void DrainWater( float amount )
	{
		if ( amount <= 0f ) return;
		Water = Math.Max( 0f, Water - amount );
	}

	public void DrainStamina( float amount )
	{
		if ( amount <= 0f ) return;
		Stamina = Math.Max( 0f, Stamina - amount );
	}
}
