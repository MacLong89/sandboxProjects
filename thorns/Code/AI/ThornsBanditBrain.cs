namespace Sandbox;

/// <summary>
/// Humanoid bandit controller — modular state machine (Scavenger / City / Airdrop defenders).
/// Runs only on host — clients never simulate decisions (§13 server authority).
/// </summary>
[Title( "Thorns — Bandit Brain" )]
[Category( "Thorns/AI" )]
[Icon( "smart_toy" )]
public sealed partial class ThornsBanditBrain : Component
{
	[Property] public float AggroRadius { get; set; } = ThornsBanditCombat.HumanNpcMaxEngagementRangeWorld;
	[Property] public float LoseRadius { get; set; } = ThornsBanditCombat.HumanNpcMaxEngagementRangeWorld * 1.15f;
	[Property] public float AttackRange { get; set; } = ThornsBanditCombat.HumanNpcMaxEngagementRangeWorld;
	[Property] public float WanderRadius { get; set; } = 550f;

	// Wanderers: no leash — goals sample around current position (see PickNewWanderGoal). Inspector text from [Property] codegen (avoid /// here: SB2000).
	[Property] public bool AnchorWanderGoalsToCurrentPosition { get; set; }

	// Leashed patrol + chase clamp to LeashAnchorWorld (airdrop guards / city defenders).
	[Property] public bool UseLeashAnchor { get; set; }

	[Property] public Vector3 LeashAnchorWorld { get; set; }

	[Property] public float LeashRadius { get; set; } = 420f;

	[Property] public float WanderSpeed { get; set; } = 150f;
	[Property] public float ChaseSpeed { get; set; } = 270f;

	[Property] public float AttackThinkSeconds { get; set; } = 0.09f;

	[Property] public float ChaseThinkSeconds { get; set; } = 0.12f;
	[Property] public float WanderThinkSeconds { get; set; } = 0.35f;
	[Property] public float IdleThinkSeconds { get; set; } = 1.1f;

	[Property] public float DormantThinkSeconds { get; set; } = 2.4f;

	/// <summary>When true, killing blow grants <see cref="ThornsXpBalance.WildlifeKillReward"/> instead of <see cref="ThornsXpBalance.BanditKillReward"/>.</summary>
	[Property] public bool AwardWildlifeKillXp { get; set; }

	/// <summary>When true, host spawns a small loot crate (ammo / meds / rare weapon) on death.</summary>
	[Property] public bool SpawnGuardLootCrateOnDeath { get; set; }

	ThornsBanditAiState _state = ThornsBanditAiState.Roam;
	Vector3 _spawnWorld;
	Vector3 _wanderGoal;
	GameObject _target;
	double _nextStateChoiceTime;
	double _thinkAccum;
	readonly double _thinkJitter;

	static readonly List<GameObject> AggroSpatialScratch = new();

	const float BanditPeerSeparationGapUnits = 12f;
	const float BanditPeerSeparationSpeedPerOverlapUnit = 10f;
	const float BanditPeerSeparationMaxPlanarSpeed = 240f;
	const float BanditChaseStandoffGapUnits = 28f;

	public ThornsBanditAiState State => _state;

	public ThornsBanditBrain()
	{
		_thinkJitter = Random.Shared.NextDouble() * 0.35;
	}

	protected override void OnStart()
	{
		_spawnWorld = UseLeashAnchor ? LeashAnchorWorld : GameObject.WorldPosition;
		_stateMachineContext ??= new ThornsBanditBrainContext { Brain = this };
		_stateMachineContext.SpawnPosition = _spawnWorld;
		_stateMachineContext.HomePosition = _spawnWorld;
		PickNewWanderGoal();
		_nextStateChoiceTime = Time.Now + Random.Shared.NextDouble() * 2.0;

		ThornsPopulationDirector.HostRegisterBandit( this );
	}

	/// <summary>Called from <see cref="ThornsNpcHumanBanditSpawn"/> after type/archetype properties are applied.</summary>
	public void HostCompleteSpawnSetup( ThornsNpcHumanBanditSpawn.Config cfg )
	{
		BanditType = cfg.BanditType;
		if ( cfg.Archetype is not null )
			ApplyArchetypeConfig( cfg.Archetype );

		_stateMachineContext ??= new ThornsBanditBrainContext { Brain = this };
		_stateMachineContext.SpawnPosition = _spawnWorld;
		_stateMachineContext.HomePosition = UseLeashAnchor ? LeashAnchorWorld : _spawnWorld;

		if ( _stateMachine is null )
		{
			InitStateMachine();
			return;
		}

		var initial = BanditType switch
		{
			ThornsBanditType.Scavenger => ThornsBanditAiState.Roam,
			_ => ThornsBanditAiState.Patrol,
		};
		_stateMachine.TryTransition( _stateMachineContext, initial, "spawn-archetype" );
	}

	protected override void OnDestroy()
	{
		ThornsPopulationDirector.HostUnregisterBandit( this );
	}

	Vector3 FlatAnchor()
	{
		return UseLeashAnchor ? LeashAnchorWorld.WithZ( 0 ) : _spawnWorld.WithZ( 0 );
	}

	Vector3 ClampPlanarToLeash( Vector3 goalWorld )
	{
		if ( !UseLeashAnchor || LeashRadius < 4f )
			return goalWorld;

		var a = FlatAnchor();
		var g = goalWorld.WithZ( 0 );
		var delta = g - a;
		var maxR = LeashRadius;
		if ( delta.LengthSquared <= maxR * maxR )
			return goalWorld;

		var dir = delta.Normal;
		var clampedFlat = a + dir * maxR;
		return clampedFlat.WithZ( goalWorld.z );
	}

	protected override void OnUpdate()
	{
		var authoritative = !Networking.IsActive || Networking.IsHost;
		if ( !authoritative )
			return;

		if ( _stateMachine is null )
			InitStateMachine();

		var selfHp = Components.Get<ThornsHealth>();
		if ( selfHp.IsValid() && ( !selfHp.IsAlive || selfHp.IsDeadState ) )
		{
			SetState( ThornsBanditAiState.Dead );
			Components.Get<ThornsBanditMotor>()?.HostSetWishWorld( Vector3.Zero );
			return;
		}

		// Smooth aim-facing while firing (avoid per-frame LOS — <see cref="IsTargetInteresting"/> traces; brain tick still validates combat).
		if ( _state == ThornsBanditAiState.Attack && CanFaceAttackTargetThisFrame() )
			HostFaceTarget( _target );

		var director = ThornsBanditDirector.Instance;
		if ( director is null )
		{
			Log.Warning(
				"[Thorns AI] ThornsBanditDirector missing — bandit cannot refresh player cache cheaply. Add one to the scene." );
			return;
		}

		var interval = CurrentThinkIntervalSeconds( director );
		_thinkAccum += Time.Delta;
		if ( _thinkAccum + _thinkJitter < interval )
			return;

		_thinkAccum = 0;
		var selfFlat = GameObject.WorldPosition.WithZ( 0 );
		StateMachineTickActive( director, selfFlat );
	}

	float CurrentThinkIntervalSeconds( ThornsBanditDirector director )
	{
		var flat = GameObject.WorldPosition.WithZ( 0 );
		var nearest = HostNearestCombatInterestDistSq( director, flat, LoseRadius );

		if ( nearest > LoseRadius * LoseRadius )
			return DormantThinkSeconds;

		return _state switch
		{
			ThornsBanditAiState.Attack => AttackThinkSeconds,
			ThornsBanditAiState.Chase => ChaseThinkSeconds,
			ThornsBanditAiState.SeekCover => ChaseThinkSeconds,
			ThornsBanditAiState.Investigate => WanderThinkSeconds * 0.85f,
			ThornsBanditAiState.Alert => WanderThinkSeconds,
			ThornsBanditAiState.Search => WanderThinkSeconds,
			ThornsBanditAiState.Roam => WanderThinkSeconds,
			ThornsBanditAiState.Patrol => WanderThinkSeconds,
			ThornsBanditAiState.Flee => ChaseThinkSeconds,
			ThornsBanditAiState.ReturnHome => WanderThinkSeconds,
			_ => IdleThinkSeconds
		};
	}

	bool IsTargetInteresting( GameObject targetRoot )
	{
		if ( !targetRoot.IsValid() )
			return false;

		var hp = targetRoot.Components.Get<ThornsHealth>();
		if ( hp.IsValid() && ( !hp.IsAlive || hp.IsDeadState ) )
			return false;

		if ( targetRoot.Components.Get<ThornsPawn>( FindMode.EnabledInSelf ).IsValid()
		     && ThornsWildlifeMountRules.PawnIsMounted( targetRoot ) )
			return false;

		var dsq = DistanceFlatSq( targetRoot );
		if ( dsq > AggroRadius * AggroRadius )
			return false;

		return ThornsBanditPerception.HasClearLos( GameObject, targetRoot, AggroRadius );
	}

	/// <summary>Host: shot or bitten by a player or wild/tamed animal — chase and return fire.</summary>
	public void HostNotifyDamagedByHostile( GameObject attackerRoot )
	{
		if ( !Networking.IsHost || attackerRoot is null || !attackerRoot.IsValid() )
			return;

		var atk = ThornsTameHostIntel.HostResolveCombatChaseRoot( attackerRoot );
		if ( !atk.IsValid() || atk == GameObject )
			return;

		var atkWild = atk.Components.GetInAncestorsOrSelf<ThornsWildlifeIdentity>( true );
		var atkPawn = atk.Components.GetInAncestorsOrSelf<ThornsPawn>( true );
		if ( !atkWild.IsValid() && !atkPawn.IsValid() )
			return;

		if ( atkPawn.IsValid() && ThornsWildlifeMountRules.PawnIsMounted( atkPawn.GameObject ) )
			return;

		var hp = atk.Components.GetInAncestorsOrSelf<ThornsHealth>( true );
		if ( hp.IsValid() && ( !hp.IsAlive || hp.IsDeadState ) )
			return;

		_target = atk;
		HostNotifyRecentDamager( atk );

		SyncContextFromBrainFields();
		_stateMachineContext.CurrentTarget = atk;
		_stateMachineContext.LastKnownTargetPosition = atk.WorldPosition;
		_stateMachineContext.AlertLevel = 3;

		if ( _stateMachine is not null && _stateMachine.TryTransition( _stateMachineContext, ThornsBanditAiState.Alert, "damaged" ) )
		{
			SyncBrainFieldsFromContext();
			return;
		}

		var dist = DistanceFlat( atk );
		if ( dist <= AttackRange
		     && ThornsBanditPerception.HasClearLos( GameObject, atk, AggroRadius * 1.1f ) )
			SetState( ThornsBanditAiState.Attack );
		else
			SetState( ThornsBanditAiState.Chase );
	}

	static float HostNearestCombatInterestDistSq( ThornsBanditDirector director, Vector3 selfFlat, float maxDistanceWorld )
	{
		var best = director.HostNearestAlivePlayerDistSqWithin( selfFlat, maxDistanceWorld );
		var r2 = maxDistanceWorld * maxDistanceWorld;
		var n = 0;

		foreach ( var brain in ThornsPopulationDirector.HostWildlifeBrainsReadOnly )
		{
			if ( n++ > 48 )
				break;

			if ( !brain.IsValid() )
				continue;

			var id = brain.Components.Get<ThornsWildlifeIdentity>();
			if ( !id.IsValid() || id.HostIsTamed )
				continue;

			var whp = brain.Components.Get<ThornsHealth>();
			if ( whp.IsValid() && ( !whp.IsAlive || whp.IsDeadState ) )
				continue;

			var d = (brain.GameObject.WorldPosition.WithZ( 0 ) - selfFlat).LengthSquared;
			if ( d < best )
				best = d;
		}

		return best > r2 ? float.MaxValue : best;
	}

	void TryAcquireTarget( ThornsBanditDirector director, Vector3 selfFlat )
	{
		director.HostQueryPlayersNearPlanar( selfFlat, AggroRadius, AggroSpatialScratch );

		GameObject best = default;
		var bestDsq = float.MaxValue;
		var aggroR2 = AggroRadius * AggroRadius;

		foreach ( var p in AggroSpatialScratch )
		{
			if ( !p.IsValid() )
				continue;

			var hp = p.Components.Get<ThornsHealth>();
			if ( hp.IsValid() && ( !hp.IsAlive || hp.IsDeadState ) )
				continue;

			if ( ThornsWildlifeMountRules.PawnIsMounted( p ) )
				continue;

			var dsq = ( p.WorldPosition.WithZ( 0 ) - selfFlat ).LengthSquared;
			if ( dsq > aggroR2 || dsq >= bestDsq )
				continue;

			if ( !ThornsBanditPerception.HasClearLos( GameObject, p, AggroRadius ) )
				continue;

			best = p;
			bestDsq = dsq;
		}

		var wildlifeN = 0;
		foreach ( var brain in ThornsPopulationDirector.HostWildlifeBrainsReadOnly )
		{
			if ( wildlifeN++ > 48 )
				break;

			if ( !brain.IsValid() )
				continue;

			var id = brain.Components.Get<ThornsWildlifeIdentity>();
			if ( !id.IsValid() || id.HostIsTamed )
				continue;

			var root = brain.GameObject;
			var whp = root.Components.Get<ThornsHealth>();
			if ( whp.IsValid() && ( !whp.IsAlive || whp.IsDeadState ) )
				continue;

			var dsq = ( root.WorldPosition.WithZ( 0 ) - selfFlat ).LengthSquared;
			if ( dsq > aggroR2 || dsq >= bestDsq )
				continue;

			if ( !ThornsBanditPerception.HasClearLos( GameObject, root, AggroRadius ) )
				continue;

			best = root;
			bestDsq = dsq;
		}

		if ( best.IsValid() )
			_target = best;
	}

	float DistanceFlat( GameObject other )
	{
		return MathF.Sqrt( DistanceFlatSq( other ) );
	}

	float DistanceFlatSq( GameObject other )
	{
		var a = GameObject.WorldPosition.WithZ( 0 );
		var b = other.WorldPosition.WithZ( 0 );
		return ( b - a ).LengthSquared;
	}

	/// <summary>Cheap gate for every-frame <see cref="HostFaceTarget"/> — no LOS ray (that stays on the AI think interval).</summary>
	bool CanFaceAttackTargetThisFrame()
	{
		if ( !_target.IsValid() )
			return false;

		var hp = _target.Components.Get<ThornsHealth>();
		if ( hp.IsValid() && ( !hp.IsAlive || hp.IsDeadState ) )
			return false;

		return DistanceFlatSq( _target ) <= LoseRadius * LoseRadius;
	}

	void HostMoveTowardFlat( Vector3 goalWorld, float speed, GameObject chaseTargetRoot = null )
	{
		var self = GameObject.WorldPosition.WithZ( 0 );
		var tgt = goalWorld.WithZ( 0 );
		var delta = tgt - self;
		var dist = delta.Length;

		if ( chaseTargetRoot.IsValid() )
		{
			var holdDist = Math.Clamp(
				AttackRange * 0.82f,
				HostGetChaseStandoffPlanarDistance( chaseTargetRoot ),
				AttackRange );
			if ( dist <= holdDist )
			{
				Components.Get<ThornsBanditMotor>()?.HostSetWishWorld( Vector3.Zero );
				if ( dist > 1f )
				{
					var face = delta.Normal;
					GameObject.WorldRotation = Rotation.LookAt( face );
				}

				return;
			}

			var slowOuter = holdDist * 1.35f;
			if ( dist < slowOuter )
			{
				var t = Math.Clamp( (dist - holdDist) / Math.Max( 12f, slowOuter - holdDist ), 0.2f, 1f );
				speed *= t;
			}
		}

		if ( dist < 4f )
		{
			Components.Get<ThornsBanditMotor>()?.HostSetWishWorld( Vector3.Zero );
			return;
		}

		if ( !chaseTargetRoot.IsValid() )
		{
			const float arrivalDist = 55f;
			var slowOuter = arrivalDist * 2.2f;
			if ( dist < slowOuter )
			{
				var t = Math.Clamp(
					( dist - arrivalDist * 0.4f ) / MathF.Max( 15f, slowOuter - arrivalDist * 0.4f ),
					0.15f,
					1f );
				speed *= t;
			}

			if ( UseLeashAnchor && LeashRadius > 4f )
			{
				var anchor = FlatAnchor();
				var fromAnchor = self - anchor;
				var distFromAnchor = fromAnchor.Length;
				var maxR = LeashRadius;
				if ( distFromAnchor > maxR && fromAnchor.LengthSquared > 4f )
				{
					tgt = anchor + fromAnchor.Normal * ( maxR * 0.93f );
					delta = tgt - self;
					dist = delta.Length;
					if ( dist < 4f )
					{
						Components.Get<ThornsBanditMotor>()?.HostSetWishWorld( Vector3.Zero );
						return;
					}
				}
				else if ( distFromAnchor > maxR * 0.91f )
				{
					var band = maxR * 0.09f;
					var edgeT = ( distFromAnchor - maxR * 0.91f ) / MathF.Max( 1f, band );
					speed *= 1f - 0.75f * Math.Clamp( edgeT, 0f, 1f );
				}
			}
		}

		var dir = delta / dist;
		GameObject.WorldRotation = Rotation.LookAt( dir );
		Components.Get<ThornsBanditMotor>()?.HostSetWishWorld( dir * speed );
	}

	public float HostGetSelfPlanarCollisionRadius()
	{
		var cc = Components.Get<CharacterController>();
		return cc.IsValid() ? Math.Max( 8f, cc.Radius ) : 20f;
	}

	static float HostGetTargetPlanarCollisionRadius( GameObject targetRoot )
	{
		if ( !targetRoot.IsValid() )
			return 20f;

		var cc = targetRoot.Components.GetInAncestorsOrSelf<CharacterController>( true );
		if ( cc.IsValid() )
			return Math.Max( 8f, cc.Radius );

		return 20f;
	}

	float HostGetChaseStandoffPlanarDistance( GameObject targetRoot ) =>
		HostGetSelfPlanarCollisionRadius() + HostGetTargetPlanarCollisionRadius( targetRoot ) + BanditChaseStandoffGapUnits;

	/// <summary>Planar push so bandits do not merge when sharing leash paths or attack slots.</summary>
	public bool HostTryGetBanditPeerSeparationWish( out Vector3 separationPlanar )
	{
		separationPlanar = Vector3.Zero;
		if ( !Networking.IsHost )
			return false;

		var flat = GameObject.WorldPosition.WithZ( 0 );
		var selfR = HostGetSelfPlanarCollisionRadius();

		foreach ( var other in ThornsPopulationDirector.HostBanditBrainsReadOnly )
		{
			if ( !other.IsValid() || other == this )
				continue;

			var otherHp = other.Components.Get<ThornsHealth>();
			if ( otherHp.IsValid() && ( !otherHp.IsAlive || otherHp.IsDeadState ) )
				continue;

			var otherFlat = other.GameObject.WorldPosition.WithZ( 0 );
			var delta = flat - otherFlat;
			var dist = delta.Length;
			var minDist = selfR + other.HostGetSelfPlanarCollisionRadius() + BanditPeerSeparationGapUnits;
			if ( dist >= minDist - 0.5f )
				continue;

			Vector3 away;
			if ( dist > 1e-3f )
				away = delta / dist;
			else
			{
				var jitter = new Vector3(
					(float)Math.Cos( ( GameObject.Id.GetHashCode() ^ other.GameObject.Id.GetHashCode() ) * 0.61 ),
					(float)Math.Sin( ( GameObject.Id.GetHashCode() ^ other.GameObject.Id.GetHashCode() ) * 0.61 ),
					0f );
				away = jitter.LengthSquared > 1e-4f ? jitter.Normal : Vector3.Right;
			}

			var overlap = minDist - Math.Max( dist, 0f );
			var push = Math.Min(
				BanditPeerSeparationMaxPlanarSpeed,
				overlap * BanditPeerSeparationSpeedPerOverlapUnit );
			separationPlanar += away * push;
		}

		if ( separationPlanar.LengthSquared < 4f )
			return false;

		if ( separationPlanar.Length > BanditPeerSeparationMaxPlanarSpeed )
			separationPlanar = separationPlanar.Normal * BanditPeerSeparationMaxPlanarSpeed;

		return true;
	}

	/// <summary>Host spawn helper — nudge position until no planar overlap with living bandit capsules.</summary>
	public static bool HostTryResolveSpawnClearOfBanditPeers( ref Vector3 worldPos, float extraGapUnits = 14f )
	{
		if ( !Networking.IsHost )
			return true;

		const int maxAttempts = 14;
		var flat = worldPos.WithZ( 0 );

		for ( var attempt = 0; attempt < maxAttempts; attempt++ )
		{
			if ( !HostPlanarOverlapsBanditPeer( flat, extraGapUnits, out var pushAway, out var minDist ) )
			{
				worldPos = flat.WithZ( worldPos.z );
				return true;
			}

			if ( pushAway.LengthSquared > 1e-4f )
				flat += pushAway.Normal * (minDist + extraGapUnits * 0.35f);
			else
			{
				var ang = Random.Shared.NextSingle() * MathF.PI * 2f;
				flat += new Vector3( MathF.Cos( ang ), MathF.Sin( ang ), 0f ) * (minDist + extraGapUnits);
			}
		}

		worldPos = flat.WithZ( worldPos.z );
		return !HostPlanarOverlapsBanditPeer( flat, extraGapUnits * 0.5f, out _, out _ );
	}

	static bool HostPlanarOverlapsBanditPeer(
		Vector3 flat,
		float extraGapUnits,
		out Vector3 pushAway,
		out float requiredSeparation )
	{
		pushAway = Vector3.Zero;
		requiredSeparation = 0f;
		var selfR = 20f;

		foreach ( var other in ThornsPopulationDirector.HostBanditBrainsReadOnly )
		{
			if ( !other.IsValid() )
				continue;

			var otherHp = other.Components.Get<ThornsHealth>();
			if ( otherHp.IsValid() && ( !otherHp.IsAlive || otherHp.IsDeadState ) )
				continue;

			var otherFlat = other.GameObject.WorldPosition.WithZ( 0 );
			var delta = flat - otherFlat;
			var dist = delta.Length;
			var minDist = selfR + other.HostGetSelfPlanarCollisionRadius() + extraGapUnits;
			if ( dist >= minDist )
				continue;

			pushAway = delta;
			requiredSeparation = Math.Max( requiredSeparation, minDist - dist );
			return true;
		}

		return false;
	}

	internal void HostFaceTarget( GameObject targetRoot )
	{
		if ( !ThornsCombatAuthority.TryGetAuthoritativeEye( GameObject, out var eye, out _ ) )
			eye = GameObject.WorldPosition + Vector3.Up * 64f;

		var aim = ThornsBanditPerception.ResolvePreferredAimWorldPoint( targetRoot );
		var dir = ( aim - eye ).Normal;
		if ( dir.LengthSquared < 1e-6f )
			return;

		// Root yaw toward target on X/Y — use a small epsilon (was incorrectly `> 9f`, never true for unit vectors).
		var flat = dir.WithZ( 0 );
		if ( flat.LengthSquared > 1e-10f )
			GameObject.WorldRotation = Rotation.LookAt( flat.Normal );

		var view = ThornsCombatAuthority.FindChild( GameObject, "View" );
		if ( view.IsValid() )
			view.WorldRotation = Rotation.LookAt( dir );
	}

	void PickNewWanderGoal()
	{
		if ( UseLeashAnchor && LeashRadius > 4f )
		{
			var yaw = Random.Shared.NextSingle() * 360f;
			var rad = LeashRadius * MathF.Sqrt( Random.Shared.NextSingle() );
			// Bias goals off the absolute rim so patrol does not hug the leash edge.
			if ( rad > LeashRadius * 0.9f )
				rad = LeashRadius * ( 0.55f + 0.35f * Random.Shared.NextSingle() );

			var flat = FlatAnchor() + Rotation.FromYaw( yaw ).Forward * rad;
			_wanderGoal = flat.WithZ( GameObject.WorldPosition.z );
			return;
		}

		var y = Random.Shared.NextSingle() * 360f;
		var r = WanderRadius * MathF.Sqrt( Random.Shared.NextSingle() );
		var wanderCenter = AnchorWanderGoalsToCurrentPosition ? GameObject.WorldPosition : _spawnWorld;
		_wanderGoal = wanderCenter + Rotation.FromYaw( y ).Forward * r;
	}

	void SetState( ThornsBanditAiState s )
	{
		if ( _state == s )
			return;

		if ( _stateMachine is not null && _stateMachineContext is not null )
		{
			SyncContextFromBrainFields();
			if ( _stateMachine.TryTransition( _stateMachineContext, s ) )
				SyncBrainFieldsFromContext();
			return;
		}

		_state = s;
	}
}
