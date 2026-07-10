namespace UnderPressure;

/// <summary>
/// A single pest. All behaviour is data-driven from its <see cref="EnemyDef"/>: it wanders
/// or hovers around its spawn point, periodically re-soils the player's cleaned cells, and
/// is defeated by scrubbing/spraying it with the correct tool. Assembled from tinted box
/// primitives so it fits the low-poly world with no authored models.
/// </summary>
public sealed class Enemy : Component
{
	private EnemyDef _def;
	private EnemyManager _manager;
	private Vector3 _origin;
	private float _health;
	private float _difficulty = 1f;

	private Vector3 _target;
	private float _resoilTimer;
	private float _attackTimer;
	private float _wobble;
	private bool _dead;
	private bool _attacksEnabled;
	private CitizenHumanoid _humanoid;
	private AnimalPestVisual _animal;
	private bool _usesCitizen;
	private bool _usesAnimalModel;
	private bool _built;
	private Vector3 _lastMovePos;

	public EnemyDef Def => _def;

	public void Init( EnemyDef def, Vector3 origin, EnemyManager manager, float difficulty = 1f )
	{
		_def = def;
		_manager = manager;
		_origin = origin;
		_difficulty = Math.Max( 1f, difficulty );
		_health = def.Health * _difficulty;
		_wobble = Game.Random.Float( 0f, 100f );
		_resoilTimer = Game.Random.Float( 0.5f, def.ResoilPeriod <= 0 ? 1f : def.ResoilPeriod );
		_attackTimer = Game.Random.Float( 0.5f, def.AttackPeriod <= 0 ? 2f : def.AttackPeriod );

		WorldPosition = RestPosition();
		WorldRotation = Rotation.FromYaw( Game.Random.Float( 0f, 360f ) );

		PickTarget();
		_lastMovePos = WorldPosition;
	}

	protected override void OnStart() => Build();

	private Vector3 RestPosition() => _origin + Vector3.Up * _def.HoverHeight;

	protected override void OnUpdate()
	{
		if ( _dead || _def is null )
			return;

		var core = GameCore.Instance;
		if ( core?.IsWorldFrozen == true )
			return;

		_wobble += Time.Delta;

		_attacksEnabled = core is not null
			&& core.Jobs.Index >= GameConstants.PestAttackUnlockJob
			&& _def.Attack != AttackStyle.None;

		Move();

		// Stop undoing work once the job is done and the player is free to leave.
		var jobDone = core?.AwaitingDeparture ?? false;
		if ( _def.ResoilPeriod > 0f && !jobDone )
		{
			_resoilTimer -= Time.Delta;
			if ( _resoilTimer <= 0f )
			{
				_resoilTimer = _def.ResoilPeriod;
				Resoil();
			}
		}

		if ( _attacksEnabled && !jobDone )
			TickAttack( core );

		TickCitizenAnimation();
	}

	private void TickCitizenAnimation()
	{
		if ( !_usesCitizen || _humanoid is null || !_humanoid.IsReady )
			return;

		var dt = Math.Max( Time.Delta, 0.001f );
		var velocity = (WorldPosition - _lastMovePos) / dt;
		_lastMovePos = WorldPosition;

		var running = _attacksEnabled && IsChasing() && velocity.WithZ( 0f ).Length > 20f;
		var wish = velocity.WithZ( 0f );
		if ( wish.Length < 2f && _attacksEnabled )
		{
			var player = PressurePlayer.Instance;
			if ( player is not null )
				wish = (player.WorldPosition - WorldPosition).WithZ( 0f ).Normal * _def.MoveSpeed;
		}

		_humanoid.TickLocomotion( wish, running );

		var lookAt = PressurePlayer.Instance?.WorldPosition;
		if ( lookAt is not null && (_attacksEnabled || wish.Length > 2f) )
			_humanoid.TickLookAt( lookAt.Value );
	}

	// --- Movement ---

	private void Move()
	{
		switch ( _def.Move )
		{
			case MoveStyle.Ground: MoveGround(); break;
			case MoveStyle.Hover: MoveHover(); break;
			default: MoveStatic(); break;
		}
	}

	private void MoveStatic()
	{
		var bob = MathF.Sin( _wobble * 1.4f ) * 4f * _def.Scale;
		WorldPosition = RestPosition() + Vector3.Up * bob;
	}

	private void MoveGround()
	{
		var pos = WorldPosition.WithZ( 0f );
		var flatTarget = EffectiveTarget().WithZ( 0f );
		var to = flatTarget - pos;

		if ( to.Length < 12f )
			PickTarget();
		else
		{
			var speed = _def.MoveSpeed * (_attacksEnabled && IsChasing() ? 1.25f : 1f);
			var step = to.Normal * speed * Time.Delta;
			pos += step;
			WorldRotation = Rotation.Lerp( WorldRotation, Rotation.LookAt( to.WithZ( 0f ).Normal ), Time.Delta * 6f );
		}

		var hop = MathF.Abs( MathF.Sin( _wobble * 6f ) ) * 6f * _def.Scale;
		WorldPosition = pos.WithZ( _origin.z + hop );
	}

	private void MoveHover()
	{
		var target = EffectiveTarget();
		var to = target - WorldPosition;
		if ( to.Length < 16f )
			PickTarget();
		else
		{
			var speed = _def.MoveSpeed * (_attacksEnabled && IsChasing() ? 1.3f : 1f);
			WorldPosition += to.Normal * speed * Time.Delta;
		}

		var bob = MathF.Sin( _wobble * 4f ) * 8f * _def.Scale;
		WorldPosition = WorldPosition.WithZ( _origin.z + _def.HoverHeight + bob );

		if ( to.Length > 1f )
			WorldRotation = Rotation.Lerp( WorldRotation, Rotation.LookAt( to.WithZ( 0f ).Normal ), Time.Delta * 5f );
	}

	private Vector3 EffectiveTarget()
	{
		if ( !_attacksEnabled || _def.ChaseRange <= 0f )
			return _target;

		var player = PressurePlayer.Instance;
		if ( player is null )
			return _target;

		var dist = WorldPosition.Distance( player.WorldPosition );
		return dist <= _def.ChaseRange ? player.WorldPosition + Vector3.Up * _def.HoverHeight : _target;
	}

	private bool IsChasing()
	{
		var player = PressurePlayer.Instance;
		if ( player is null || _def.ChaseRange <= 0f )
			return false;

		return WorldPosition.Distance( player.WorldPosition ) <= _def.ChaseRange;
	}

	private void PickTarget()
	{
		var r = _def.MoveRadius;
		var offset = new Vector3( Game.Random.Float( -r, r ), Game.Random.Float( -r, r ), 0f );
		_target = _origin + offset + Vector3.Up * _def.HoverHeight;
	}

	// --- Undoing the player's work ---

	private void Resoil()
	{
		var surface = NearestSurface();
		if ( surface is null )
			return;

		// Aim at the point on the panel closest to us so grime reappears where the pest is.
		var local = surface.WorldTransform.PointToLocal( WorldPosition );
		local.x = Math.Clamp( local.x, -surface.Width * 0.5f, surface.Width * 0.5f );
		local.y = Math.Clamp( local.y, -surface.Height * 0.5f, surface.Height * 0.5f );
		var point = surface.WorldTransform.PointToWorld( local.WithZ( 0f ) );

		surface.Resoil( point, _def.ResoilRadius * _def.Scale, _def.ResoilAmount );
	}

	private CleanableSurface NearestSurface()
	{
		CleanableSurface best = null;
		var bestDist = float.MaxValue;

		foreach ( var s in Scene.GetAllComponents<CleanableSurface>() )
		{
			var d = WorldPosition.DistanceSquared( s.WorldPosition );
			if ( d < bestDist )
			{
				bestDist = d;
				best = s;
			}
		}

		return best;
	}

	// --- Harassing the player ---

	private void TickAttack( GameCore core )
	{
		var player = PressurePlayer.Instance;
		if ( player is null )
			return;

		var dist = WorldPosition.Distance( player.WorldPosition );
		if ( dist > _def.AttackRange )
			return;

		_attackTimer -= Time.Delta;
		if ( _attackTimer > 0f )
			return;

		_attackTimer = Math.Max( 0.8f, _def.AttackPeriod / MathF.Sqrt( _difficulty ) );
		ExecuteAttack( core, player );
	}

	private void ExecuteAttack( GameCore core, PressurePlayer player )
	{
		var strength = _def.AttackStrength * _difficulty;
		var washer = PressureWasher.Instance;

		switch ( _def.Attack )
		{
			case AttackStyle.Sting:
				washer?.DrainWater( strength );
				washer?.DrainStamina( strength * 0.65f );
				player.Jostle( WorldPosition, strength * 0.35f );
				Sfx.PlayAt( Sfx.CleanTick, player.WorldPosition );
				core.RegisterPestHit(
					$"{_def.Name} stung you! −{(int)strength} water, −{(int)(strength * 0.65f)} stamina",
					_def.Name );
				break;

			case AttackStyle.Dive:
				ResoilAt( player.WorldPosition );
				washer?.DrainWater( strength * 0.5f );
				Sfx.PlayAt( Sfx.Footstep, player.WorldPosition );
				core.RegisterPestHit(
					$"{_def.Name} dive-bombed your work! −{(int)(strength * 0.5f)} water · spot resoiled",
					_def.Name );
				break;

			case AttackStyle.Rob:
				var stolen = core.Wallet.Steal( strength );
				Sfx.PlayAt( Sfx.Purchase, player.WorldPosition );
				if ( stolen > 0 )
					core.RegisterPestHit( $"{_def.Name} stole {GameConstants.FormatCash( stolen )}!", _def.Name );
				else
					core.RegisterPestHit( $"{_def.Name} tried to rob you — you're broke!", _def.Name );
				break;

			case AttackStyle.SprayFight:
				washer?.DrainWater( strength * 0.22f );
				washer?.DrainStamina( strength * 0.14f );
				player.Jostle( WorldPosition, strength );
				ResoilAt( player.WorldPosition );
				Sfx.PlayAt( Sfx.Spray, player.WorldPosition );
				core.RegisterPestHit(
					$"{_def.Name} blasted you! −{(int)(strength * 0.22f)} water · spot resoiled",
					_def.Name );
				break;
		}
	}

	private void ResoilAt( Vector3 worldPos )
	{
		var surface = NearestSurface();
		if ( surface is null )
			return;

		var local = surface.WorldTransform.PointToLocal( worldPos );
		local.x = Math.Clamp( local.x, -surface.Width * 0.5f, surface.Width * 0.5f );
		local.y = Math.Clamp( local.y, -surface.Height * 0.5f, surface.Height * 0.5f );
		var point = surface.WorldTransform.PointToWorld( local.WithZ( 0f ) );

		surface.Resoil( point, _def.ResoilRadius * _def.Scale * 0.85f, _def.ResoilAmount * 0.75f );
	}

	// --- Taking damage ---

	/// <summary>Apply tool contact. Returns true if this pest was actually damaged.</summary>
	public bool TryDamage( ToolType tool, float amount )
	{
		if ( _dead || _def is null || tool != _def.DamagedBy )
			return false;

		_health -= amount;
		if ( _health <= 0f )
			Defeat();

		return true;
	}

	private void Defeat()
	{
		if ( _dead )
			return;

		_dead = true;

		if ( _def.Family == EnemyFamily.Contract || _def.DamagedBy == ToolType.Gun )
			CleanableSurface.SplatterBloodAt( Scene, WorldPosition );

		Sfx.PlayAt( _def.DamagedBy == ToolType.Gun ? Sfx.Gunshot : Sfx.CleanTick, WorldPosition );
		_manager?.OnDefeated( this, _def, _origin );
		GameObject.Destroy();
	}

	// --- Visuals ---

	private void Build()
	{
		if ( _built || _def is null )
			return;

		_built = true;

		switch ( _def.VisualKind )
		{
			case PestVisualKind.Humanoid: BuildHumanoid(); break;
			default: BuildAnimal(); break;
		}

		AddCollider();
	}

	private void BuildHumanoid()
	{
		if ( TryBuildCitizen( GameConstants.CitizenHeightScale ) )
			return;

		switch ( _def.Kind )
		{
			case EnemyKind.StickerBandit: BuildBandit(); break;
			case EnemyKind.RivalWasher: BuildRivalWasherFallback(); break;
			case EnemyKind.ContractTarget: BuildContractTargetFallback(); break;
			case EnemyKind.ContractBodyguard: BuildContractBodyguardFallback(); break;
		}
	}

	private void BuildAnimal()
	{
		_animal = Components.GetOrCreate<AnimalPestVisual>();
		_usesAnimalModel = _animal.TrySetup( _def.ModelPath, _def.Scale, _def.Body );
		if ( _usesAnimalModel )
			return;

		switch ( _def.Kind )
		{
			case EnemyKind.Pigeon: BuildBird(); break;
			case EnemyKind.Wasp: BuildWasp(); break;
			case EnemyKind.OilLeech: BuildLeech(); break;
			case EnemyKind.Rat: BuildRat(); break;
			case EnemyKind.Raccoon: BuildRaccoon(); break;
			case EnemyKind.StrayDog: BuildStrayDog(); break;
		}
	}

	private void BuildRat()
	{
		Box( "Body", new Vector3( 0, 0, 10 ), new Vector3( 34, 18, 16 ), _def.Body );
		Box( "Head", new Vector3( 20, 0, 12 ), new Vector3( 16, 14, 14 ), _def.Body );
		Box( "Snout", new Vector3( 30, 0, 11 ), new Vector3( 10, 8, 8 ), _def.Accent );
		Box( "EarL", new Vector3( 18, -8, 20 ), new Vector3( 8, 4, 8 ), _def.Accent );
		Box( "EarR", new Vector3( 18, 8, 20 ), new Vector3( 8, 4, 8 ), _def.Accent );
		Box( "Tail", new Vector3( -24, 0, 12 ), new Vector3( 28, 4, 4 ), _def.Accent, new Angles( 0, 0, 12 ) );
	}

	private void BuildRaccoon()
	{
		Box( "Body", new Vector3( 0, 0, 18 ), new Vector3( 44, 28, 28 ), _def.Body );
		Box( "Head", new Vector3( 24, 0, 24 ), new Vector3( 22, 20, 20 ), _def.Body );
		Box( "Mask", new Vector3( 30, 0, 24 ), new Vector3( 14, 18, 14 ), _def.Accent );
		Box( "EarL", new Vector3( 22, -10, 34 ), new Vector3( 8, 6, 8 ), _def.Accent );
		Box( "EarR", new Vector3( 22, 10, 34 ), new Vector3( 8, 6, 8 ), _def.Accent );
		Box( "Tail", new Vector3( -28, 0, 16 ), new Vector3( 24, 10, 10 ), _def.Accent, new Angles( 0, 0, 18 ) );
	}

	private void BuildStrayDog()
	{
		Box( "Body", new Vector3( 0, 0, 28 ), new Vector3( 56, 30, 34 ), _def.Body );
		Box( "Chest", new Vector3( 16, 0, 24 ), new Vector3( 28, 26, 26 ), _def.Accent );
		Box( "Head", new Vector3( 34, 0, 38 ), new Vector3( 24, 22, 22 ), _def.Body );
		Box( "Snout", new Vector3( 48, 0, 34 ), new Vector3( 16, 12, 12 ), _def.Accent );
		Box( "EarL", new Vector3( 30, -10, 48 ), new Vector3( 10, 6, 14 ), _def.Accent, new Angles( 0, 0, -18 ) );
		Box( "EarR", new Vector3( 30, 10, 48 ), new Vector3( 10, 6, 14 ), _def.Accent, new Angles( 0, 0, 18 ) );
		Box( "LegFL", new Vector3( 18, -10, 8 ), new Vector3( 10, 10, 22 ), _def.Accent );
		Box( "LegFR", new Vector3( 18, 10, 8 ), new Vector3( 10, 10, 22 ), _def.Accent );
		Box( "LegBL", new Vector3( -18, -10, 8 ), new Vector3( 10, 10, 22 ), _def.Accent );
		Box( "LegBR", new Vector3( -18, 10, 8 ), new Vector3( 10, 10, 22 ), _def.Accent );
		Box( "Tail", new Vector3( -34, 0, 30 ), new Vector3( 22, 8, 8 ), _def.Body, new Angles( 0, 0, 24 ) );
	}

	private void AddCollider()
	{
		var s = _def.Scale;
		var col = Components.Create<BoxCollider>();
		if ( _usesCitizen )
		{
			var h = GameConstants.CitizenHeightScale;
			col.Center = new Vector3( 0, 0, 36f * h );
			col.Scale = new Vector3( 28f * h, 28f * h, 72f * h );
		}
		else if ( _usesAnimalModel )
		{
			col.Center = new Vector3( 0, 0, 24f * s );
			col.Scale = new Vector3( 36f * s, 36f * s, 48f * s );
		}
		else
		{
			col.Center = new Vector3( 0, 0, 30f * s );
			col.Scale = new Vector3( 60f * s, 60f * s, 60f * s );
		}

		col.Static = false;
	}

	private bool TryBuildCitizen( float heightScale = 1f )
	{
		_humanoid = Components.GetOrCreate<CitizenHumanoid>();
		_usesCitizen = _humanoid.TrySetup( heightScale );
		return _usesCitizen;
	}

	private GameObject Box( string name, Vector3 pos, Vector3 size, Color color, Angles rot = default )
		=> Scenery.Box( GameObject, name, pos * _def.Scale, size * _def.Scale, color, rot, GameMaterials.Grime );

	private void BuildBird()
	{
		Box( "Body", new Vector3( 0, 0, 16 ), new Vector3( 40, 22, 24 ), _def.Body );
		Box( "Neck", new Vector3( 14, 0, 30 ), new Vector3( 14, 14, 20 ), _def.Body );
		Box( "Head", new Vector3( 18, 0, 42 ), new Vector3( 18, 16, 16 ), _def.Body );
		Box( "Beak", new Vector3( 30, 0, 40 ), new Vector3( 12, 6, 6 ), _def.Accent );
		Box( "Tail", new Vector3( -24, 0, 18 ), new Vector3( 22, 16, 6 ), _def.Body, new Angles( -18, 0, 0 ) );
		Box( "WingL", new Vector3( -2, -12, 20 ), new Vector3( 26, 6, 16 ), _def.Body );
		Box( "WingR", new Vector3( -2, 12, 20 ), new Vector3( 26, 6, 16 ), _def.Body );
	}

	private void BuildWasp()
	{
		Box( "Abdomen", new Vector3( -14, 0, 0 ), new Vector3( 30, 20, 20 ), _def.Body );
		Box( "Stripe1", new Vector3( -20, 0, 0 ), new Vector3( 6, 22, 22 ), _def.Accent );
		Box( "Thorax", new Vector3( 6, 0, 2 ), new Vector3( 22, 18, 18 ), _def.Accent );
		Box( "Head", new Vector3( 22, 0, 4 ), new Vector3( 14, 14, 14 ), _def.Body );
		Box( "WingL", new Vector3( 0, -16, 12 ), new Vector3( 26, 6, 4 ), new Color( 0.85f, 0.9f, 0.95f ), new Angles( 0, 0, 18 ) );
		Box( "WingR", new Vector3( 0, 16, 12 ), new Vector3( 26, 6, 4 ), new Color( 0.85f, 0.9f, 0.95f ), new Angles( 0, 0, -18 ) );
		Box( "Stinger", new Vector3( -30, 0, 0 ), new Vector3( 12, 5, 5 ), _def.Accent );
	}

	private void BuildLeech()
	{
		Box( "Seg1", new Vector3( 20, 0, 12 ), new Vector3( 24, 26, 22 ), _def.Body );
		Box( "Seg2", new Vector3( 0, 0, 14 ), new Vector3( 30, 30, 26 ), _def.Body );
		Box( "Seg3", new Vector3( -22, 0, 10 ), new Vector3( 22, 24, 20 ), _def.Accent );
		Box( "Sheen", new Vector3( 4, 0, 26 ), new Vector3( 20, 18, 6 ), _def.Accent );
		Box( "EyeL", new Vector3( 30, -8, 16 ), new Vector3( 6, 6, 6 ), new Color( 0.9f, 0.85f, 0.2f ) );
		Box( "EyeR", new Vector3( 30, 8, 16 ), new Vector3( 6, 6, 6 ), new Color( 0.9f, 0.85f, 0.2f ) );
	}

	private void BuildBandit()
	{
		var skin = new Color( 0.8f, 0.62f, 0.48f );
		Box( "Legs", new Vector3( 0, 0, 16 ), new Vector3( 22, 26, 32 ), new Color( 0.15f, 0.16f, 0.2f ) );
		Box( "Torso", new Vector3( 0, 0, 46 ), new Vector3( 28, 32, 34 ), _def.Body );
		Box( "Head", new Vector3( 2, 0, 72 ), new Vector3( 20, 20, 20 ), skin );
		Box( "Cap", new Vector3( 2, 0, 84 ), new Vector3( 24, 24, 8 ), _def.Accent );
		Box( "Brim", new Vector3( 14, 0, 82 ), new Vector3( 12, 22, 4 ), _def.Accent );
		Box( "Stickers", new Vector3( 16, 0, 48 ), new Vector3( 8, 22, 22 ), _def.Accent, new Angles( 0, 0, 12 ) );
	}

	private void BuildRivalWasherFallback()
	{
		var skin = new Color( 0.78f, 0.58f, 0.44f );
		var denim = new Color( 0.18f, 0.22f, 0.34f );
		Box( "Legs", new Vector3( 0, 0, 16 ), new Vector3( 24, 28, 34 ), denim );
		Box( "Torso", new Vector3( 0, 0, 48 ), new Vector3( 30, 34, 36 ), _def.Body );
		Box( "Head", new Vector3( 2, 0, 74 ), new Vector3( 22, 22, 22 ), skin );
		Box( "Helmet", new Vector3( 2, 0, 86 ), new Vector3( 26, 26, 10 ), _def.Accent );
		Box( "Wand", new Vector3( 22, 10, 52 ), new Vector3( 34, 8, 8 ), new Color( 0.55f, 0.58f, 0.62f ), new Angles( 0, 0, -18 ) );
		Box( "Nozzle", new Vector3( 40, 10, 50 ), new Vector3( 10, 10, 10 ), new Color( 0.95f, 0.2f, 0.15f ) );
		Box( "Hose", new Vector3( -6, -8, 40 ), new Vector3( 8, 8, 28 ), new Color( 0.12f, 0.14f, 0.18f ), new Angles( 0, 0, 12 ) );
	}

	private void BuildContractTargetFallback()
	{
		var skin = new Color( 0.82f, 0.66f, 0.50f );
		var suit = _def.Body;
		var tie = _def.Accent;
		Box( "Legs", new Vector3( 0, 0, 16 ), new Vector3( 24, 28, 34 ), new Color( 0.12f, 0.13f, 0.16f ) );
		Box( "Torso", new Vector3( 0, 0, 48 ), new Vector3( 30, 34, 36 ), suit );
		Box( "Head", new Vector3( 2, 0, 74 ), new Vector3( 22, 22, 22 ), skin );
		Box( "Hair", new Vector3( 2, 0, 84 ), new Vector3( 24, 24, 10 ), new Color( 0.18f, 0.12f, 0.08f ) );
		Box( "Tie", new Vector3( 10, 0, 56 ), new Vector3( 6, 6, 24 ), tie, new Angles( 0, 0, 8 ) );
		Box( "Briefcase", new Vector3( 24, 12, 30 ), new Vector3( 28, 10, 20 ), tie );
	}

	private void BuildContractBodyguardFallback()
	{
		var skin = new Color( 0.78f, 0.58f, 0.44f );
		Box( "Legs", new Vector3( 0, 0, 16 ), new Vector3( 26, 30, 36 ), _def.Body );
		Box( "Torso", new Vector3( 0, 0, 50 ), new Vector3( 34, 36, 40 ), _def.Body );
		Box( "Head", new Vector3( 2, 0, 76 ), new Vector3( 24, 24, 24 ), skin );
		Box( "EarPiece", new Vector3( 14, -10, 76 ), new Vector3( 6, 6, 6 ), _def.Accent );
		Box( "ArmL", new Vector3( -20, -8, 48 ), new Vector3( 10, 10, 34 ), _def.Body, new Angles( 0, 0, 18 ) );
		Box( "Pistol", new Vector3( -28, -10, 40 ), new Vector3( 18, 6, 6 ), new Color( 0.18f, 0.20f, 0.24f ), new Angles( 0, 0, -12 ) );
	}
}
