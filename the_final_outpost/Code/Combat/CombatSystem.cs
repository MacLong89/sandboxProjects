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
		public float SplashRadius;
		public float SplashDamageMult;
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
		DestructionFx.Clear();
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

		// Leave death bursts alone — they self-expire. Clearing here erased last-kill FX
		// the same frame OnNightSurvived wiped combat.
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
		ZombieHitTarget.Attach( go, z, typeDef.Scale );
		z.RefreshLabel();
		_zombies.Add( z );
		return z;
	}

	public ZombieInstance NearestZombie( Vector3 from, float range ) =>
		NearestZombieInternal( from, range, losOrigin: null );

	/// <summary>Nearest live zombie in range with unobstructed line of fire from <paramref name="losOrigin"/>.</summary>
	public ZombieInstance NearestEngageableZombie(
		Vector3 from,
		float range,
		Vector3 losOrigin,
		int ignoreCellX = int.MinValue,
		int ignoreCellY = int.MinValue ) =>
		NearestZombieInternal( from, range, losOrigin, ignoreCellX, ignoreCellY );

	private ZombieInstance NearestZombieInternal(
		Vector3 from,
		float range,
		Vector3? losOrigin,
		int ignoreCellX = int.MinValue,
		int ignoreCellY = int.MinValue )
	{
		ZombieInstance best = null;
		var bestD = range * range;
		from = from.WithZ( 0f );

		foreach ( var z in _zombies )
		{
			if ( z.Dead ) continue;
			var d = (z.Position.WithZ( 0f ) - from).LengthSquared;
			if ( d >= bestD ) continue;

			if ( losOrigin.HasValue
			     && !BuildingCollision.HasLineOfFire( losOrigin.Value, z.Position, ignoreCellX: ignoreCellX, ignoreCellY: ignoreCellY ) )
				continue;

			bestD = d;
			best = z;
		}

		return best;
	}

	public void FireBullet(
		Vector3 origin,
		Vector3 dir,
		float damage,
		string fireSound = null,
		Color? tracerColor = null,
		bool playSound = true,
		float splashRadius = 0f,
		float splashDamageMult = 0f )
	{
		var b = _pool.Count > 0 ? _pool.Pop() : CreateBullet();
		b.Pos = origin;
		b.Dir = dir.Normal;
		b.Damage = damage;
		b.SplashRadius = MathF.Max( 0f, splashRadius );
		b.SplashDamageMult = MathF.Max( 0f, splashDamageMult );
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

	/// <summary>
	/// World blast vs zombies only (mines / artillery). Never hits recruits, buildings, or walls.
	/// </summary>
	public void SplashAt( Vector3 origin, float damage, float radius, string fireSound = null )
	{
		if ( damage <= 0f || radius <= 0f ) return;

		var core = GameCore.Instance;
		var night = core?.CombatProgressionNight ?? 1;
		origin = origin.WithZ( 0f );

		foreach ( var z in _zombies )
		{
			if ( z.Dead ) continue;
			if ( (z.Position.WithZ( 0f ) - origin).Length > radius ) continue;
			if ( z.Hit( damage ) && core is not null )
				GrantKillRewards( z, core, night );
		}

		if ( !string.IsNullOrEmpty( fireSound ) )
			CombatAudio.PlayGunfire( fireSound, "SplashAt" );
		else
			Sfx.Play( Sfx.Turret );
	}

	protected override void OnUpdate()
	{
		DestructionFx.Tick( Time.Delta );

		var core = GameCore.Instance;
		if ( core is null || core.Phase != GamePhase.Night ) return;

		var dt = Time.Delta;
		UpdateZombies( dt, core );
		UpdateBullets( dt, core );
		CleanupDead();
	}

	private enum TargetKind { Wall, Building, Defender, Core }

	/// <summary>
	/// Perimeter walls are solid by default. We used to set IgnorePerimeterWalls whenever
	/// the approach side had a breach while the agent was near the ring — that made EVERY
	/// intact wall on the ring non-collidable (phase-through). Broken segments already
	/// UnmarkWall their cells, so the gap is walkable without ignoring neighbors.
	/// Jumpers only ignore walls while <see cref="ZombieInstance.IsVaulting"/>.
	/// </summary>
	private static BuildingCollision.PathAllow PathAllowFor( ZombieInstance z, ZombieTarget target )
	{
		var building = target.Kind == TargetKind.Building ? target.Building : null;
		var wall = target.Kind == TargetKind.Wall ? target.Wall : null;
		var towardCore = target.Kind == TargetKind.Core || z.TypeDef?.BeeLinesCore == true;
		var scale = z.TypeDef?.Scale ?? 1f;
		var pathRadius = GameConstants.ZombiePathRadius * Math.Clamp( scale, 0.55f, 1.85f );

		var ignoreWalls = z.IsVaulting;

		return BuildingCollision.PathAllow.ForZombieTarget(
			ignoreWalls,
			building,
			wall,
			towardCore,
			ignoreBuildings: z.TypeDef?.BeeLinesCore == true,
			pathRadiusOverride: pathRadius );
	}

	/// <summary>One-frame slide step toward a goal. Caps escape nudges — never warps.</summary>
	private static void WalkZombieToward(
		ZombieInstance z,
		Vector3 goal,
		float dt,
		BuildingCollision.PathAllow allow,
		out float moved,
		out Vector3 walkDir )
	{
		var agent = ZombieAgentTag( z );
		var pos = z.Position;
		var to = (goal - pos).WithZ( 0f );
		walkDir = to.Length > 0.001f ? to / to.Length : z.Go.WorldRotation.Forward;
		var step = z.EffectiveSpeed * dt;
		if ( to.Length <= 1f || step <= 0.001f )
		{
			moved = 0f;
			return;
		}

		// If standing inside a blocker, step toward free space (one step max).
		if ( BuildingCollision.BlocksUnit( pos, ignorePerimeterWalls: allow.IgnorePerimeterWalls, forZombieMelee: true, allow: allow )
		     && BuildingCollision.TryEscape( pos, out var clear, GameConstants.ZombiePathRadius, allow.IgnorePerimeterWalls, forZombieMelee: true, allow ) )
		{
			PathDebug.Warn( agent, "inside-blocker",
				$"pos={PathDebug.Fmt( pos )}{PathDebug.Cell( pos )} → escape={PathDebug.Fmt( clear )}" );
			var escape = (clear - pos).WithZ( 0f );
			if ( escape.Length > 0.001f )
				goal = pos + escape.Normal * MathF.Min( escape.Length, step );
		}

		var next = BuildingCollision.ResolveZombieStep( pos, goal, step, allow.IgnorePerimeterWalls, allow );
		moved = (next - pos).WithZ( 0f ).Length;

		// When blocked straight-on, bias sideways so agents walk around cell edges.
		// Near the command-post exterior, prefer radial-out + tangent so corners don't pin them.
		if ( moved < step * GameConstants.ZombieStuckMoveThreshold )
		{
			if ( z.StrafeSign == 0 )
				z.StrafeSign = Game.Random.Int( 0, 1 ) == 0 ? -1 : 1;

			var strafeOk = false;
			if ( OutpostManager.Instance is { } outpost )
			{
				var core = outpost.CorePosition.WithZ( 0f );
				var half = BuildGrid.CommandPostZombieCollisionFootprint * 0.5f;
				var dx = MathF.Abs( pos.x - core.x );
				var dy = MathF.Abs( pos.y - core.y );
				var nearCore =
					dx < half.x + GameConstants.CellSize
					&& dy < half.y + GameConstants.CellSize;
				if ( nearCore )
				{
					var radial = (pos - core).WithZ( 0f );
					if ( radial.Length > 0.001f )
					{
						var outN = radial.Normal;
						var tang = new Vector3( -outN.y, outN.x, 0f ) * z.StrafeSign;
						var push = MathF.Max( step * 3.5f, GameConstants.CellSize * 0.4f );
						var pushGoal = pos + outN * push + tang * push + walkDir * (step * 0.5f);
						var pushNext = BuildingCollision.ResolveZombieStep(
							pos, pushGoal, step, allow.IgnorePerimeterWalls, allow );
						var pushMoved = (pushNext - pos).WithZ( 0f ).Length;
						if ( pushMoved >= step * GameConstants.ZombieStuckMoveThreshold )
						{
							PathDebug.Event( agent, "core-strafe-ok",
								$"sign={z.StrafeSign} moved={pushMoved:0.0} goal={PathDebug.Fmt( goal )}{PathDebug.Cell( goal )}" );
							next = pushNext;
							moved = pushMoved;
							z.StrafeSign = 0;
							strafeOk = true;
						}
					}
				}
			}

			if ( !strafeOk )
			{
				var perp = new Vector3( -walkDir.y, walkDir.x, 0f ) * z.StrafeSign;
				var sideGoal = pos + perp * MathF.Max( step * 4f, GameConstants.CellSize * 0.35f ) + walkDir * step;
				var sideNext = BuildingCollision.ResolveZombieStep( pos, sideGoal, step, allow.IgnorePerimeterWalls, allow );
				var sideMoved = (sideNext - pos).WithZ( 0f ).Length;
				if ( sideMoved >= step * GameConstants.ZombieStuckMoveThreshold )
				{
					PathDebug.Event( agent, "strafe-ok",
						$"sign={z.StrafeSign} moved={sideMoved:0.0} goal={PathDebug.Fmt( goal )}{PathDebug.Cell( goal )}" );
					next = sideNext;
					moved = sideMoved;
					z.StrafeSign = 0;
				}
				else
				{
					PathDebug.Event( agent, "strafe-fail",
						$"sign={z.StrafeSign} straight={moved:0.00}/{step:0.00} pos={PathDebug.Fmt( pos )}{PathDebug.Cell( pos )} goal={PathDebug.Fmt( goal )}{PathDebug.Cell( goal )}" );
					z.StrafeSign = -z.StrafeSign;
				}
			}
		}
		else
		{
			z.StrafeSign = 0;
		}

		next.z = pos.z + (OutpostTerrain.SampleHeight( next.x, next.y ) - pos.z) * MathF.Min( 1f, dt * 10f );
		z.Go.WorldPosition = next;
	}

	private static string ZombieAgentTag( ZombieInstance z )
	{
		var type = z.TypeDef?.Name ?? "Zombie";
		var id = z.Go.IsValid() ? z.Go.Id.ToString()[..8] : "dead";
		return $"{type}#{id}";
	}

	private static void EscapeZombieIfTrapped( ZombieInstance z, BuildingCollision.PathAllow allow = default )
	{
		if ( z?.Go is null || !z.Go.IsValid() ) return;

		var pos = z.Position;
		if ( !BuildingCollision.BlocksUnit( pos, ignorePerimeterWalls: allow.IgnorePerimeterWalls, forZombieMelee: true, allow: allow ) )
			return;

		if ( !BuildingCollision.TryEscape( pos, out var clear, GameConstants.ZombiePathRadius, allow.IgnorePerimeterWalls, forZombieMelee: true, allow ) )
		{
			PathDebug.Warn( ZombieAgentTag( z ), "trapped-no-escape",
				$"pos={PathDebug.Fmt( pos )}{PathDebug.Cell( pos )}" );
			return;
		}

		// Prefer stepping into courtyard, not sliding along the interior wall face —
		// but NEVER pull an outside agent inward through solid timber.
		if ( OutpostManager.Instance is { } outpost
		     && !WallApproach.IsOutsideWalls( pos, outpost.CorePosition ) )
		{
			var radius = allow.PathRadiusOverride > 0.01f ? allow.PathRadiusOverride : GameConstants.ZombiePathRadius;
			if ( WallApproach.OverlapsPerimeterPath( pos, outpost.CorePosition, radius ) )
			{
				var inward = WallApproach.ClampInsideCourtyard( pos, outpost.CorePosition, radius );
				if ( !BuildingCollision.BlocksUnit( inward, ignorePerimeterWalls: allow.IgnorePerimeterWalls, forZombieMelee: true, allow: allow ) )
					clear = inward;
				else
					clear = WallApproach.ClampInsideCourtyard( clear, outpost.CorePosition, radius );
			}
		}

		var delta = (clear - pos).WithZ( 0f );
		var maxStep = MathF.Max( GameConstants.ZombiePathRadius * 2f, z.EffectiveSpeed * 0.08f );
		if ( delta.Length > maxStep )
			clear = pos + delta.Normal * maxStep;

		PathDebug.Warn( ZombieAgentTag( z ), "escape-nudge",
			$"from={PathDebug.Fmt( pos )}{PathDebug.Cell( pos )} to={PathDebug.Fmt( clear )}{PathDebug.Cell( clear )} d={delta.Length:0.0}" );
		clear.z = OutpostTerrain.SampleHeight( clear.x, clear.y );
		z.Go.WorldPosition = clear;
	}

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
		var night = core.CombatProgressionNight;

		foreach ( var z in _zombies )
		{
			if ( z.Dead ) continue;
			z.TickSlow( dt );

			var target = FindTarget( z, z.Position, corePos, outpost, build, defenders );
			var allow = PathAllowFor( z, target );
			EscapeZombieIfTrapped( z, allow );

			// Wall vault: jumpers climb over solid timber. Must run before seek so they
			// never get IgnorePerimeterWalls from the old "breach nearby" path.
			if ( TickWallVault( z, dt, corePos, outpost, allow ) )
				continue;

			var pos = z.Position;
			var pathRadius = allow.PathRadiusOverride > 0.01f
				? allow.PathRadiusOverride
				: GameConstants.ZombiePathRadius;
			var (engagePoint, attackDist, attackRadius) = ResolveEngagement( pos, target, corePos, allow );
			engagePoint = BuildingCollision.EnsureClearStand(
				engagePoint,
				pos,
				pathRadius,
				allow.IgnorePerimeterWalls,
				forZombieMelee: true,
				allow );
			// Home-ring targets only — satellite-plot towers must keep their outside stand points.
			var homeTarget = TargetIsInsideHomeRing( target, corePos );
			if ( homeTarget && target.Kind is TargetKind.Building or TargetKind.Core )
				engagePoint = WallApproach.ClampInsideCourtyard( engagePoint, corePos, pathRadius );

			TryDisengage( z, attackDist, attackRadius );
			TryEngage( z, attackDist, attackRadius );

			var faceTarget = FaceTarget( pos, target, corePos, z.Go.WorldRotation );
			var facing = Rotation.Slerp( z.Go.WorldRotation, faceTarget, MathF.Min( 1f, dt * 8f ) );
			z.Go.WorldRotation = facing;

			var agent = ZombieAgentTag( z );
			var targetLabel = target.Kind switch
			{
				TargetKind.Wall => $"wall:{target.Wall?.Key}",
				TargetKind.Building => $"bldg:{target.Building?.Def.Name}",
				TargetKind.Defender => "defender",
				TargetKind.Core => "core",
				_ => target.Kind.ToString()
			};

			if ( z.IsEngaged )
			{
				PathDebug.Event( agent, "engaged",
					$"target={targetLabel} dist={attackDist:0.0} radius={attackRadius:0.0} pos={PathDebug.Fmt( pos )}{PathDebug.Cell( pos )}" );
				TickEngagedZombie( z, pos, engagePoint, attackDist, attackRadius, target, outpost, build, defenders, dt, core, night, facing, corePos, allow );
				continue;
			}

			// Destinations: funnel through a home breach only when the target is inside the
			// home ring. Outer-plot structures are approached directly — never drag the horde
			// into the courtyard first (that caused pass-by + invisible-wall softlocks).
			var moveGoal = engagePoint;
			var usedBreach = false;
			var usedDetour = false;
			var usedClearRing = false;
			if ( homeTarget
			     && z.TypeDef?.CanJumpWalls != true
			     && WallApproach.SideHasBreach( outpost.Walls, z.ApproachSide )
			     && target.Kind != TargetKind.Wall
			     && WallApproach.NeedsBreachEntry( pos, corePos ) )
			{
				var waypoint = WallApproach.InwardWaypoint( outpost.Walls, z.ApproachSide, corePos, near: pos );
				if ( !WallApproach.PastBreachWaypoint( pos, z.ApproachSide, waypoint )
				     && (waypoint - pos).WithZ( 0f ).Length > GameConstants.CellSize * 0.35f )
				{
					moveGoal = waypoint;
					usedBreach = true;
				}
			}

			// Clear-ring: ONLY when already inside and hunting a home-ring target.
			if ( !usedBreach
			     && homeTarget
			     && !WallApproach.IsOutsideWalls( pos, corePos )
			     && target.Kind is TargetKind.Building or TargetKind.Core
			     && WallApproach.OverlapsPerimeterPath( pos, corePos, pathRadius )
			     && attackDist > attackRadius + GameConstants.CellSize )
			{
				var clearPos = WallApproach.ClampInsideCourtyard( pos, corePos, pathRadius );
				if ( (clearPos - pos).WithZ( 0f ).Length > 6f )
				{
					moveGoal = clearPos;
					usedClearRing = true;
				}
			}

			if ( !usedBreach && !usedClearRing )
			{
				var goalReach = (moveGoal - pos).WithZ( 0f ).Length;
				var arrivedEarly = goalReach < 10f && attackDist > attackRadius + 20f;

				if ( arrivedEarly && target.Kind == TargetKind.Wall && target.Wall is not null )
				{
					var center = target.Wall.Center;
					var size = target.Wall.ZombieCollisionFootprint;
					moveGoal = BuildingCollision.ApproachPointForWallMounted( pos, center, size, allow );
					if ( (moveGoal - pos).WithZ( 0f ).Length < 6f )
					{
						var face = BuildingCollision.ClosestPointOnFootprint( pos, center, size );
						var outward = WallApproach.OutwardNormal( WallApproach.FromWorldPosition( center, corePos ) );
						moveGoal = face + outward * (GameConstants.ZombiePathRadius + GameConstants.ZombieApproachStandoff);
					}
				}
				else if ( (target.Kind == TargetKind.Building || target.Kind == TargetKind.Core)
				          && (arrivedEarly || BuildingCollision.SegmentHitsBlocker( pos, moveGoal, allow )
				              || z.MoveStuckTimer > 0.35f || z.HasDetour) )
				{
					var finalAim = TargetAimPoint( target, corePos );

					// Stick to a skirt waypoint until past HQ — recomputing every frame
					// flipped corners and pinned agents on the L-edge.
					if ( z.HasDetour )
					{
						if ( BuildingCollision.HasClearedCoreSkirt( pos, z.DetourGoal, finalAim, corePos ) )
						{
							z.HasDetour = false;
							PathDebug.Event( agent, "detour-clear",
								$"pos={PathDebug.Fmt( pos )}{PathDebug.Cell( pos )} aim={PathDebug.Fmt( finalAim )}" );
						}
						else
						{
							moveGoal = homeTarget
								? WallApproach.ClampInsideCourtyard( z.DetourGoal, corePos, pathRadius )
								: z.DetourGoal;
							usedDetour = true;
						}
					}

					if ( !usedDetour
					     && (arrivedEarly || BuildingCollision.SegmentHitsBlocker( pos, moveGoal, allow )
					         || z.MoveStuckTimer > 0.35f) )
					{
						// Never skip perimeter walls on detour — that let agents slide through timber.
						if ( BuildingCollision.TryDetourWaypoint(
							     pos,
							     finalAim,
							     allow,
							     skipPerimeterWalls: false,
							     out var detour )
						     && (detour - pos).WithZ( 0f ).Length > 14f )
						{
							z.HasDetour = true;
							z.DetourGoal = detour;
							moveGoal = homeTarget
								? WallApproach.ClampInsideCourtyard( detour, corePos, pathRadius )
								: detour;
							usedDetour = true;
						}
					}
				}

				if ( homeTarget && target.Kind is TargetKind.Building or TargetKind.Core )
					moveGoal = WallApproach.ClampInsideCourtyard( moveGoal, corePos, pathRadius );
			}

			if ( usedBreach )
			{
				z.HasDetour = false;
				PathDebug.Event( agent, "breach-waypoint",
					$"side={z.ApproachSide} via={PathDebug.Fmt( moveGoal )}{PathDebug.Cell( moveGoal )} target={targetLabel}" );
			}
			else if ( usedClearRing )
				PathDebug.Event( agent, "clear-ring",
					$"via={PathDebug.Fmt( moveGoal )}{PathDebug.Cell( moveGoal )} target={targetLabel}" );
			else if ( usedDetour )
				PathDebug.Event( agent, "detour",
					$"via={PathDebug.Fmt( moveGoal )}{PathDebug.Cell( moveGoal )} target={targetLabel} dist={attackDist:0.0}" );
			else
				PathDebug.Event( agent, "seek",
					$"target={targetLabel} dist={attackDist:0.0} goal={PathDebug.Fmt( moveGoal )}{PathDebug.Cell( moveGoal )} pos={PathDebug.Fmt( pos )}{PathDebug.Cell( pos )}" );

			z.Character?.SetZombieEngaged( false );
			WalkZombieToward( z, moveGoal, dt, allow, out var moved, out var walkDir );
			pos = z.Position;
			ApplyHordeSeparation( z, ref pos, dt, allow.IgnorePerimeterWalls, attackDist, allow );
			z.Go.WorldPosition = pos;

			var step = z.EffectiveSpeed * dt;
			if ( moved < step * GameConstants.ZombieStuckMoveThreshold )
			{
				z.MoveStuckTimer += dt;
				z.TotalStuckTimer += dt;
				if ( z.MoveStuckTimer > 0.4f )
					PathDebug.Warn( agent, "stuck",
						$"t={z.TotalStuckTimer:0.0}s moved={moved:0.00}/{step:0.00} target={targetLabel} pos={PathDebug.Fmt( pos )}{PathDebug.Cell( pos )} goal={PathDebug.Fmt( moveGoal )}{PathDebug.Cell( moveGoal )}" );
			}
			else
			{
				z.MoveStuckTimer = 0f;
				z.TotalStuckTimer = MathF.Max( 0f, z.TotalStuckTimer - dt * 2f );
			}

			z.Character?.TickZombie( walkDir * z.EffectiveSpeed, facing );

			(engagePoint, attackDist, attackRadius) = ResolveEngagement( pos, target, corePos, allow );
			TryEngage( z, attackDist, attackRadius );

			if ( z.IsEngaged )
				TickEngagedZombie( z, pos, engagePoint, attackDist, attackRadius, target, outpost, build, defenders, dt, core, night, facing, corePos, allow );
			else if ( z.TotalStuckTimer >= GameConstants.ZombieStuckFailsafeDelay )
			{
				PathDebug.Warn( agent, "failsafe-kill",
					$"stuck={z.TotalStuckTimer:0.0}s target={targetLabel} pos={PathDebug.Fmt( pos )}{PathDebug.Cell( pos )}" );
				KillStuckZombie( z, core, night );
			}
		}

		if ( outpost.CoreHealth <= 0f )
			core.OnCoreDestroyed();
	}

	/// <summary>
	/// Jumpers vault over the perimeter with a parabolic arc + citizen jump anim.
	/// Returns true when this zombie consumed the frame (caller should continue).
	/// </summary>
	private static bool TickWallVault(
		ZombieInstance z,
		float dt,
		Vector3 corePos,
		OutpostManager outpost,
		BuildingCollision.PathAllow allow )
	{
		if ( z.TypeDef?.CanJumpWalls != true )
			return false;

		var agent = ZombieAgentTag( z );
		var side = z.ApproachSide;

		// --- Already airborne: advance arc ---
		if ( z.VaultPhase == ZombieInstance.WallVaultPhase.Airborne )
		{
			z.VaultT += dt / MathF.Max( 0.15f, z.VaultDuration );
			var t = Math.Clamp( z.VaultT, 0f, 1f );

			var from = z.VaultFrom;
			var to = z.VaultTo;
			var flat = Vector3.Lerp( from.WithZ( 0f ), to.WithZ( 0f ), t );
			var startZ = from.z;
			var endZ = OutpostTerrain.SampleHeight( to.x, to.y );
			var apex = MathF.Max( startZ, endZ ) + GameConstants.WallHeight + GameConstants.ZombieWallJumpApexExtra;

			// Smooth rise to apex then fall (half / half).
			float height;
			if ( t < 0.5f )
			{
				var u = t * 2f;
				u = u * u * (3f - 2f * u);
				height = startZ + (apex - startZ) * u;
			}
			else
			{
				var u = (t - 0.5f) * 2f;
				u = u * u * (3f - 2f * u);
				height = apex + (endZ - apex) * u;
			}

			var next = flat.WithZ( height );
			z.Go.WorldPosition = next;

			var faceDir = (to - from).WithZ( 0f );
			if ( faceDir.Length > 1f )
				z.Go.WorldRotation = Rotation.Slerp( z.Go.WorldRotation, Rotation.LookAt( faceDir.Normal ), MathF.Min( 1f, dt * 10f ) );

			z.Character?.SetZombieEngaged( false );
			z.Character?.SetWallVaulting( true, triggerJump: !z.VaultJumpTriggered );
			z.VaultJumpTriggered = true;
			z.Character?.TickZombie( faceDir.Normal * z.EffectiveSpeed * 1.2f, z.Go.WorldRotation );

			if ( t >= 1f )
			{
				var land = to.WithZ( endZ );
				// Snap fully inside if we landed near timber.
				land = WallApproach.ClampInsideCourtyard( land, corePos );
				land.z = OutpostTerrain.SampleHeight( land.x, land.y );
				z.Go.WorldPosition = land;
				z.VaultPhase = ZombieInstance.WallVaultPhase.None;
				z.VaultT = 0f;
				z.VaultJumpTriggered = false;
				z.Character?.SetWallVaulting( false );
				z.MoveStuckTimer = 0f;
				z.TotalStuckTimer = 0f;
				PathDebug.Event( agent, "vault-land", $"side={side} at={PathDebug.Fmt( land )}" );
			}

			return true;
		}

		// --- Need to vault? Only when still OUTSIDE the ring (never re-vault from courtyard). ---
		var pos = z.Position;
		if ( !WallApproach.IsOutsideWalls( pos, corePos ) )
		{
			z.VaultPhase = ZombieInstance.WallVaultPhase.None;
			z.Character?.SetWallVaulting( false );
			return false;
		}

		// Walk up to takeoff, then launch.
		var takeoff = WallApproach.VaultTakeoff( pos, side );
		var landing = WallApproach.VaultLanding( pos, corePos, side );
		var toTakeoff = (takeoff - pos).WithZ( 0f );
		var takeoffDist = toTakeoff.Length;

		if ( takeoffDist > GameConstants.CellSize * 0.45f )
		{
			z.VaultPhase = ZombieInstance.WallVaultPhase.Approach;
			var facing = toTakeoff.Length > 0.1f
				? Rotation.LookAt( toTakeoff.Normal )
				: z.Go.WorldRotation;
			z.Go.WorldRotation = Rotation.Slerp( z.Go.WorldRotation, facing, MathF.Min( 1f, dt * 8f ) );
			z.Character?.SetZombieEngaged( false );
			z.Character?.SetWallVaulting( false );
			WalkZombieToward( z, takeoff, dt, allow, out _, out var walkDir );
			z.Character?.TickZombie( walkDir * z.EffectiveSpeed, z.Go.WorldRotation );
			PathDebug.Event( agent, "vault-approach",
				$"side={side} takeoff={PathDebug.Fmt( takeoff )} dist={takeoffDist:0.0}" );
			return true;
		}

		// Launch.
		var scale = z.TypeDef?.Scale ?? 1f;
		z.VaultFrom = pos.WithZ( OutpostTerrain.SampleHeight( pos.x, pos.y ) );
		z.VaultTo = landing.WithZ( OutpostTerrain.SampleHeight( landing.x, landing.y ) );
		z.VaultDuration = GameConstants.ZombieWallJumpDuration * Math.Clamp( scale, 0.75f, 1.6f );
		z.VaultT = 0f;
		z.VaultPhase = ZombieInstance.WallVaultPhase.Airborne;
		z.VaultJumpTriggered = false;
		z.IsEngaged = false;
		PathDebug.Event( agent, "vault-launch",
			$"side={side} from={PathDebug.Fmt( z.VaultFrom )} to={PathDebug.Fmt( z.VaultTo )} dur={z.VaultDuration:0.00}" );
		return true;
	}

	private void TickEngagedZombie(
		ZombieInstance z,
		Vector3 pos,
		Vector3 engagePoint,
		float attackDist,
		float attackRadius,
		ZombieTarget target,
		OutpostManager outpost,
		BuildManager build,
		DefenderManager defenders,
		float dt,
		GameCore core,
		int night,
		Rotation facing,
		Vector3 corePos,
		BuildingCollision.PathAllow allow )
	{
		CreepTowardEngagePoint( z, z.Position, engagePoint, attackDist, attackRadius, dt, allow );
		(_, attackDist, attackRadius) = ResolveEngagement( z.Position, target, corePos, allow );
		var didSwing = AttackTarget( z, target, attackDist, attackRadius, outpost, build, defenders, dt, core, night );
		z.Character?.SetZombieEngaged( true );
		z.Character?.TickZombie( facing.Forward * 24f, facing );
		if ( didSwing )
			z.Character?.TriggerMeleeSwing();
		z.MoveStuckTimer = 0f;
		z.TotalStuckTimer = 0f;
		z.HasDetour = false;
	}

	private static void TryDisengage( ZombieInstance z, float attackDist, float attackRadius )
	{
		if ( z.IsEngaged && attackDist > MeleeReach( attackRadius, GameConstants.ZombieEngageExitBuffer ) )
			z.IsEngaged = false;
	}

	private static float MeleeReach( float attackRadius, float stuckSlack = 0f ) =>
		attackRadius
		+ GameConstants.ZombieAttackRangeSlack
		+ GameConstants.ZombiePathRadius
		+ stuckSlack;

	private static float StuckEngageSlack( ZombieInstance z ) =>
		z.MoveStuckTimer > 0.6f ? GameConstants.ZombieStuckEngageSlack : 0f;

	private static void TryEngage( ZombieInstance z, float attackDist, float attackRadius )
	{
		if ( z.IsEngaged ) return;

		if ( attackDist <= MeleeReach( attackRadius, StuckEngageSlack( z ) ) )
			z.IsEngaged = true;
	}

	private void KillStuckZombie( ZombieInstance z, GameCore core, int night, bool grantRewards = true, bool suppressSplit = false )
	{
		if ( z.Dead ) return;

		z.Dead = true;
		// AUDIT FIX M6: failsafe path must not leave SplitCount active for CleanupDead.
		if ( suppressSplit )
			z.SuppressSplitOnDeath = true;
		if ( grantRewards )
			GrantKillRewards( z, core, night );
	}

	/// <summary>
	/// Guarantees a stalled round can finish. Kills remaining zombies without scrap payouts
	/// so pathing softlocks don't farm the wallet.
	/// AUDIT FIX M6: also suppresses Splitter re-spawn on cleanup.
	/// </summary>
	public void FailsafeClearRemainingZombies( GameCore core )
	{
		var night = core.CombatProgressionNight;
		foreach ( var z in _zombies )
			KillStuckZombie( z, core, night, grantRewards: false, suppressSplit: true );
	}

	public void ApplyTakeoverHit( ZombieInstance z, float damage )
	{
		if ( z is null || z.Dead || damage <= 0f ) return;
		var core = GameCore.Instance;
		if ( z.Hit( damage ) && core is not null )
			GrantKillRewards( z, core, core.CombatProgressionNight );
	}

	private static void GrantKillRewards( ZombieInstance z, GameCore core, int night )
	{
		CombatAudio.PlayZombieKill( "ZombieKill" );
		var scrap = (GameConstants.ScrapPerKillBase + night * GameConstants.ScrapPerKillPerNight)
			* core.ScrapMultiplier
			+ core.SalvageKillBonus;
		core.Wallet.Earn( scrap );
		core.Save.TotalKills++;
		ZombieBestiary.RecordKill( core.Save, z.TypeDef?.Kind ?? ZombieKind.Walker );
	}

	private static Vector3 TargetAimPoint( ZombieTarget target, Vector3 corePos )
	{
		switch ( target.Kind )
		{
			case TargetKind.Building when target.Building is not null:
				return target.Building.Center;
			case TargetKind.Defender when target.Defender is not null:
				return target.Defender.WorldPos;
			case TargetKind.Wall when target.Wall is not null:
				return target.Wall.Center;
			default:
				return target.Position == Vector3.Zero ? corePos : target.Position;
		}
	}

	private static Rotation FaceTarget( Vector3 pos, ZombieTarget target, Vector3 corePos, Rotation fallback )
	{
		var to = (TargetAimPoint( target, corePos ) - pos).WithZ( 0f );
		if ( to.Length < 0.5f )
			return fallback;

		return Rotation.LookAt( to.Normal );
	}

	private static void CreepTowardEngagePoint(
		ZombieInstance z,
		Vector3 pos,
		Vector3 engagePoint,
		float attackDist,
		float attackRadius,
		float dt,
		BuildingCollision.PathAllow allow )
	{
		// Once inside engage slack, stand and hit — don't micro-walk into blocked tower tiles.
		if ( attackDist <= MeleeReach( attackRadius, StuckEngageSlack( z ) ) ) return;

		var to = (engagePoint - pos).WithZ( 0f );
		if ( to.Length < 0.5f ) return;

		var creep = MathF.Min( to.Length, z.EffectiveSpeed * dt * 0.5f );
		var next = BuildingCollision.ResolveZombieStep(
			pos,
			pos + to.Normal * creep,
			creep,
			allow.IgnorePerimeterWalls,
			allow );
		next.z = pos.z + (OutpostTerrain.SampleHeight( next.x, next.y ) - pos.z) * MathF.Min( 1f, dt * 10f );
		z.Go.WorldPosition = next;
	}

	private void ApplyHordeSeparation(
		ZombieInstance z,
		ref Vector3 pos,
		float dt,
		bool jumpWalls,
		float attackDist,
		BuildingCollision.PathAllow allow )
	{
		if ( attackDist < GameConstants.ZombieMeleeRange * 2.2f )
			return;

		var minDist = GameConstants.ZombieSeparationRadius;
		var push = Vector3.Zero;
		foreach ( var other in _zombies )
		{
			if ( other == z || other.Dead ) continue;

			var delta = (pos - other.Position).WithZ( 0f );
			var dist = delta.Length;
			if ( dist >= minDist || dist < 0.001f ) continue;

			push += delta / dist * (minDist - dist);
		}

		if ( push.Length < 0.01f ) return;

		var step = MathF.Min( push.Length, z.EffectiveSpeed * dt * 0.22f );
		var desired = pos + push.Normal * step;
		var next = BuildingCollision.ResolveZombieStep( pos, desired, step, jumpWalls, allow );
		var moved = (next - pos).WithZ( 0f ).Length;
		if ( moved < step * GameConstants.ZombieStuckMoveThreshold )
		{
			// Project onto free axes so wall faces don't fully cancel separation.
			var best = next;
			var bestLen = moved;
			if ( MathF.Abs( push.x ) > 0.001f )
			{
				var axisX = BuildingCollision.ResolveZombieStep(
					pos,
					pos + new Vector3( MathF.Sign( push.x ) * step, 0f, 0f ),
					step,
					jumpWalls,
					allow );
				var mx = (axisX - pos).WithZ( 0f ).Length;
				if ( mx > bestLen )
				{
					best = axisX;
					bestLen = mx;
				}
			}

			if ( MathF.Abs( push.y ) > 0.001f )
			{
				var axisY = BuildingCollision.ResolveZombieStep(
					pos,
					pos + new Vector3( 0f, MathF.Sign( push.y ) * step, 0f ),
					step,
					jumpWalls,
					allow );
				var my = (axisY - pos).WithZ( 0f ).Length;
				if ( my > bestLen )
					best = axisY;
			}

			next = best;
		}

		pos = next.WithZ( pos.z );
	}

	private static (Vector3 engagePoint, float attackDist, float attackRadius) ResolveEngagement(
		Vector3 pos,
		ZombieTarget target,
		Vector3 corePos,
		BuildingCollision.PathAllow allow = default )
	{
		switch ( target.Kind )
		{
			case TargetKind.Building when target.Building is not null:
			{
				var building = target.Building;
				var center = building.Center;
				var size = building.Def.ZombieCollisionFootprint;
				var engage = TileOccupancy.IsWallCell( building.CellX, building.CellY )
					? BuildingCollision.ApproachPointForWallMounted( pos, center, size, allow )
					: BuildingCollision.ApproachPointForZombie( pos, center, size, allow );
				return (
					engage,
					BuildingCollision.DistToFootprintSurface( pos, center, size ),
					GameConstants.ZombieMeleeRange );
			}
			case TargetKind.Core:
			{
				var footprint = BuildGrid.CommandPostZombieCollisionFootprint;
				return (
					BuildingCollision.ApproachPointForZombie( pos, corePos, footprint, allow ),
					BuildingCollision.DistToFootprintSurface( pos, corePos, footprint ),
					GameConstants.ZombieMeleeRange );
			}
			case TargetKind.Defender when target.Defender is not null:
			{
				var center = target.Defender.WorldPos;
				var footprint = new Vector3(
					GameConstants.ZombiePathRadius * 2.4f,
					GameConstants.ZombiePathRadius * 2.4f,
					0f );
				return (
					BuildingCollision.ApproachPointForZombie( pos, center, footprint, allow ),
					BuildingCollision.DistToFootprintSurface( pos, center, footprint ),
					GameConstants.ZombieMeleeRange );
			}
			case TargetKind.Wall when target.Wall is not null:
			{
				var center = target.Wall.Center;
				var size = target.Wall.ZombieCollisionFootprint;
				// Perimeter walls: approach on the outward face (not ClosestPoint which drifts sideways).
				return (
					BuildingCollision.ApproachPointForWallMounted( pos, center, size, allow ),
					BuildingCollision.DistToFootprintSurface( pos, center, size ),
					GameConstants.ZombieMeleeRange );
			}
			default:
				return (
					target.Position,
					(target.Position - pos).WithZ( 0f ).Length,
					target.AttackRadius );
		}
	}

	/// <summary>
	/// True when the hunt target sits inside/on the home perimeter ring.
	/// False for structures on claimed outer plots — those are approached from outside.
	/// </summary>
	static bool TargetIsInsideHomeRing( ZombieTarget target, Vector3 corePos ) =>
		target.Kind switch
		{
			TargetKind.Core => true,
			TargetKind.Wall => true,
			TargetKind.Building when target.Building is not null =>
				!WallApproach.IsOutsideWalls( target.Building.Center, corePos ),
			TargetKind.Defender when target.Defender is not null =>
				!WallApproach.IsOutsideWalls( target.Defender.WorldPos, corePos ),
			_ => !WallApproach.IsOutsideWalls( target.Position, corePos )
		};

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
			float d;
			if ( building is not null )
				d = BuildingCollision.DistToFootprintSurface( pos, building.Center, building.Def.ZombieCollisionFootprint );
			else if ( wall is not null )
				d = BuildingCollision.DistToFootprintSurface( pos, wall.Center, wall.ZombieCollisionFootprint );
			else if ( defender is not null )
			{
				var fp = new Vector3(
					GameConstants.ZombiePathRadius * 2.4f,
					GameConstants.ZombiePathRadius * 2.4f,
					0f );
				d = BuildingCollision.DistToFootprintSurface( pos, defender.WorldPos, fp );
			}
			else if ( kind == TargetKind.Core )
				d = BuildingCollision.DistToFootprintSurface( pos, corePos, BuildGrid.CommandPostZombieCollisionFootprint );
			else
				d = (position - pos).WithZ( 0f ).Length;

			// Prefer buildings/defenders over walls when both are in melee reach — avoids
			// staring at a wall face while a tower a few units behind goes unhit.
			if ( wall is not null && d > GameConstants.ZombieMeleeRange * 0.35f )
				d += 18f;

			if ( d * d >= bestDist ) return;
			bestDist = d * d;
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

		// Buildings, command post & recruits — nearest wins (HQ no longer last-resort only).
		if ( outpost.CoreHealth > 0f )
		{
			var coreDist = BuildingCollision.DistToFootprintSurface(
				pos, corePos, BuildGrid.CommandPostZombieCollisionFootprint );
			if ( coreDist <= GameConstants.ZombieSeekRadius )
			{
				Consider(
					TargetKind.Core,
					corePos,
					GameConstants.CoreSize * 0.6f + GameConstants.ZombieRadius );
			}
		}

		if ( build is not null )
		{
			foreach ( var b in build.Buildings )
			{
				if ( b.IsDestroyed ) continue;
				if ( BuildingCollision.DistToFootprintSurface( pos, b.Center, b.Def.ZombieCollisionFootprint ) > GameConstants.ZombieSeekRadius )
					continue;
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

		// Already locked onto an outer-plot structure — don't divert to the home wall / HQ.
		if ( hasBest && !TargetIsInsideHomeRing( best, corePos ) )
			return best;

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

		if ( hasBest )
			return best;

		// Nothing in seek range — still push toward the command post.
		return new ZombieTarget
		{
			Kind = TargetKind.Core,
			Position = corePos,
			AttackRadius = GameConstants.CoreSize * 0.6f + GameConstants.ZombieRadius
		};
	}

	private bool AttackTarget(
		ZombieInstance z,
		ZombieTarget target,
		float attackDist,
		float attackRadius,
		OutpostManager outpost,
		BuildManager build,
		DefenderManager defenders,
		float dt,
		GameCore core,
		int night )
	{
		z.AttackTimer -= dt;
		if ( z.AttackTimer > 0f ) return false;

		// Match engage slack so force-engaged zombies can actually hit instead of creeping forever.
		if ( attackDist > MeleeReach( attackRadius, z.IsEngaged ? StuckEngageSlack( z ) : 0f ) )
			return false;

		z.AttackTimer = GameConstants.ZombieAttackInterval;

		switch ( target.Kind )
		{
			case TargetKind.Core:
				if ( z.TypeDef?.CoreExplosionDamage > 0f )
				{
					outpost.DamageCore( z.TypeDef.CoreExplosionDamage + z.Damage );
					z.Dead = true;
					CombatAudio.PlayImpact( CombatAudio.ImpactKind.BomberExplode, "core", "ZombieBomberExplode" );
					return true;
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

		return true;
	}

	private static string DefenderKey( DefenderManager.DefenderUnit defender ) =>
		defender.SaveIndex >= 0 ? $"recruit:{defender.SaveIndex}" : $"recruit:{defender.Go.Id}";

	private void UpdateBullets( float dt, GameCore core )
	{
		var step = GameConstants.BulletSpeed * dt;
		// World scale raised BulletSpeed with TileScale, but hit radii stayed citizen-sized.
		// Point samples then tunneled through targets (~180u/frame at 60fps vs ~42u radius).
		var hitRadius = GameConstants.ZombieRadius + 10f;

		for ( var i = _activeBullets.Count - 1; i >= 0; i-- )
		{
			var b = _activeBullets[i];
			var prev = b.Pos;
			b.Pos += b.Dir * step;
			b.Life -= dt;

			var hit = ResolveHitSwept( b, core, prev, b.Pos, hitRadius );

			if ( hit || b.Life <= 0f )
			{
				Recycle( b );
				_activeBullets.RemoveAt( i );
				continue;
			}

			b.Go.WorldPosition = b.Pos;
		}
	}

	private bool ResolveHitSwept( Bullet b, GameCore core, Vector3 from, Vector3 to, float hitRadius )
	{
		var a = new Vector2( from.x, from.y );
		var c = new Vector2( to.x, to.y );

		foreach ( var z in _zombies )
		{
			if ( z.Dead ) continue;

			var p = new Vector2( z.Position.x, z.Position.y );
			if ( DistancePointToSegment( p, a, c ) > hitRadius )
				continue;

			if ( z.Hit( b.Damage ) )
				GrantKillRewards( z, core, core.CombatProgressionNight );

			if ( b.SplashRadius > 0f && b.SplashDamageMult > 0f )
			{
				var impact = p;
				foreach ( var nearby in _zombies )
				{
					if ( nearby == z || nearby.Dead ) continue;
					var nearbyPos = new Vector2( nearby.Position.x, nearby.Position.y );
					if ( (nearbyPos - impact).Length > b.SplashRadius ) continue;
					if ( nearby.Hit( b.Damage * b.SplashDamageMult ) )
						GrantKillRewards( nearby, core, core.CombatProgressionNight );
				}
			}

			return true;
		}

		if ( b.Damage > 0f && HostileForceSystem.Instance is { } hostiles )
		{
			if ( hostiles.TryHitSwept( from, to, b.Damage, hitRadius + 2f ) )
				return true;
		}

		return false;
	}

	static float DistancePointToSegment( Vector2 p, Vector2 a, Vector2 b )
	{
		var ab = b - a;
		var lenSq = ab.LengthSquared;
		if ( lenSq < 0.0001f )
			return (p - a).Length;

		var t = Math.Clamp( Vector2.Dot( p - a, ab ) / lenSq, 0f, 1f );
		var closest = a + ab * t;
		return (p - closest).Length;
	}

	private void CleanupDead()
	{
		for ( var i = _zombies.Count - 1; i >= 0; i-- )
		{
			var z = _zombies[i];
			if ( !z.Dead ) continue;

			// AUDIT FIX M6: failsafe / forced clears set SuppressSplitOnDeath.
			if ( z.TypeDef?.SplitCount > 0 && !z.SuppressSplitOnDeath )
				SpawnSplitters( z );

			var scale = (z.TypeDef?.Scale ?? 1f) * 0.33f;
			var deathPos = z.Position;
			DestructionFx.Burst( deathPos, scale );
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
