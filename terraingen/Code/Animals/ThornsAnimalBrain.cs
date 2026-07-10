namespace Terraingen.Animals;

using Sandbox.Network;
using Terraingen.AI;
using Terraingen.Combat;
using Terraingen.GameData;
using Terraingen.Multiplayer;
using Terraingen.Player;
using Terraingen.TerrainGen;
using Terraingen.World;

/// <summary>Server-authoritative animal AI and combat.</summary>
[Title( "Thorns Animal Brain" )]
[Category( "Thorns/Animals" )]
public sealed partial class ThornsAnimalBrain : Component
{
	[Sync( SyncFlags.FromHost )] public ushort SpeciesId { get; private set; }
	[Sync( SyncFlags.FromHost )] public ThornsAnimalState AiState { get; private set; } = ThornsAnimalState.Wander;
	[Sync( SyncFlags.FromHost )] public float CurrentHealth { get; private set; }
	[Sync( SyncFlags.FromHost )] public bool IsDead { get; private set; }
	[Sync( SyncFlags.FromHost )] public float ReplicatedMoveSpeed { get; private set; }
	[Sync( SyncFlags.FromHost )] public bool IsAwaitingTame { get; private set; }
	[Sync( SyncFlags.FromHost )] public bool IsTamed { get; private set; }
	[Sync( SyncFlags.FromHost )] public string TamedOwnerAccountKey { get; private set; } = "";
	[Sync( SyncFlags.FromHost )] public string TamedDisplayName { get; private set; } = "";
	[Sync( SyncFlags.FromHost )] public Guid MountedRiderId { get; private set; }
	[Sync( SyncFlags.FromHost )] public bool IsTamedFollowSprinting { get; private set; }
	[Sync( SyncFlags.FromHost )] public bool IsBloomed { get; private set; }
	[Sync( SyncFlags.FromHost )] public int BreedTier { get; private set; } = 1;
	[Sync( SyncFlags.FromHost )] public bool IsCrossbreed { get; private set; }
	[Sync( SyncFlags.FromHost )] public bool IsMutatedBreed { get; private set; }
	[Sync( SyncFlags.FromHost )] public string GeneticSpeciesIdsCsv { get; private set; } = "";
	[Sync( SyncFlags.FromHost )] public string GeneticTraitIdsCsv { get; private set; } = "";
	[Sync( SyncFlags.FromHost )] public uint StrikeSerial { get; private set; }
	[Sync( SyncFlags.FromHost )] public float StrikeAnimPlaybackRate { get; private set; } = 1f;

	public bool IsMounted => MountedRiderId != Guid.Empty;
	public long BreedCooldownUntilUtcTicks => _breedCooldownUntilUtcTicks;
	public bool IsOnBreedCooldown => ThornsTameCatalog.IsOnBreedCooldown( _breedCooldownUntilUtcTicks );

	public float NextDetectTime { get; internal set; }
	public float DetectStaggerSeconds { get; internal set; }
	public ThornsNpcLodTier LodTier { get; internal set; }

	const float WanderSpeedFraction = 0.5f;
	const float RunningSpeedMultiplier = 1.5f;
	const float MeleeDamageMultiplier = 1.5f;
	const float HumanoidMeleeReachBonus = 28f;
	const float TamedChaseStopReachFraction = 0.88f;
	const float PredatorPostKillWanderSeconds = 30f;

	NavMeshAgent _agent;
	ThornsAnimalCorpse _corpse;
	ThornsAnimalSpeciesData _species;

	float _spawnHealth;
	float _spawnDamage;
	float _spawnSpeed;
	float _breedDetectionRange;
	long _breedCooldownUntilUtcTicks;
	float _bodyRadius;
	float _corpseDespawnDelaySeconds = ThornsAnimalManager.CorpseFallbackLifetimeSeconds;

	GameObject _target;
	GameObject _retaliationTarget;
	GameObject _recentAttacker;
	double _recentAttackerUntilRealtime;
	Vector3 _huntStartPosition;
	ThornsAnimalMixedMode _mixedMode = ThornsAnimalMixedMode.None;
	GameObject _mixedThreat;

	Vector3 _mountedWishDir;
	Vector2 _mountedMoveInput;
	float _mountedVerticalVelocity;
	readonly List<Collider> _disabledRiderCollidersWhileMounted = new();

	GameObject _cachedTamedOwner;
	string _cachedTamedOwnerKey = "";
	bool _followPausedNearOwner;
	TimeUntil _nextTamedOwnerLookup;
	GameObject _cachedMountedRider;
	Guid _cachedMountedRiderId;
	TimeUntil _nextMountedRiderLookup;

	TimeUntil _attackCooldownReady;
	TimeSince _deathTime;

	double _strikeEndsAt;
	float _cachedAttackAnimDuration;

	double _nextWanderAt;
	double _nextChaseAt;
	double _idleUntilAt;
	double _huntLockedUntil;

	GameObject _ownerMarkedTarget;
	double _ownerMarkedUntil;
	GameObject _ownerThreat;
	double _ownerThreatUntil;

	Vector3 _fleeDirection = Vector3.Forward;
	uint _herdGroupId;
	internal uint HerdGroupId => _herdGroupId;

	public GameObject Target => _target;
	public ThornsAnimalSpeciesData Species => _species;
	public float MaxHealth => _spawnHealth > 0f ? _spawnHealth : CurrentHealth;
	public float SpawnDamage => _spawnDamage;
	public float SpawnSpeed => _spawnSpeed;
	public float DetectionRangeForUi => _breedDetectionRange > 0f ? _breedDetectionRange : _species?.DetectionRange ?? 1200f;

	internal float GetBodyRadius()
	{
		if ( _bodyRadius <= 0f )
			CacheBodyRadius();

		return _bodyRadius;
	}

	internal float GetBodyHeight()
	{
		var mesh = Components.Get<SkinnedModelRenderer>();
		return ThornsAnimalHitbox.GetBodyHeight( mesh?.Model, GameObject.WorldScale.x );
	}

	void CacheBodyRadius()
	{
		var mesh = Components.Get<SkinnedModelRenderer>();
		_bodyRadius = ThornsAnimalHitbox.GetPlanarRadius( mesh?.Model, GameObject.WorldScale.x );
		SyncAgentRadius();
	}

	void SyncAgentRadius()
	{
		if ( !_agent.IsValid() )
			return;

		_agent.Radius = Math.Clamp( GetBodyRadius() * 0.75f, ThornsAnimalManager.BaseAgentRadius * GameObject.WorldScale.x, 28f );
	}

	internal void HostAssignHerdGroup( uint herdGroupId ) => _herdGroupId = herdGroupId;

	internal void HostInitialize( ThornsAnimalSpeciesData species )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || species is null )
			return;

		_species = species;
		SpeciesId = species.SpeciesId;
		_spawnHealth = ThornsAnimalWorldUtil.RollVariation( species.BaseHealth );
		_spawnDamage = ThornsAnimalWorldUtil.RollVariation( species.BaseDamage );
		_spawnSpeed = ThornsAnimalWorldUtil.RollVariation( species.BaseSpeed );
		CurrentHealth = _spawnHealth;
		ResetMoveSpeedRamp();
		BreedTier = Math.Clamp( species.TameTier, 1, 5 );
		GeneticSpeciesIdsCsv = species.SpeciesId.ToString();
		GeneticTraitIdsCsv = string.Join( ",", Terraingen.GameData.ThornsTameCatalog.GetTraitsForSpecies( species.Key ).Select( t => t.Id ) );

		_agent = Components.Get<NavMeshAgent>();
		_corpse = Components.Get<ThornsAnimalCorpse>();
		DetectStaggerSeconds = Game.Random.Float( 0f, species.DetectionInterval );
		NextDetectTime = Time.Now + DetectStaggerSeconds;
		ScheduleNextWanderDestination();
		SetAiState( ThornsAnimalState.Wander, "initialize" );
		_attackCooldownReady = 0;
		CacheBodyRadius();
		SyncAgentMoveSpeed();
		EnsureMotor();
	}

	public bool HostApplyBloomMutation( float healthMultiplier = 5f, float damageMultiplier = 1.5f )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || IsDead || IsTamed || IsAwaitingTame )
			return false;

		if ( IsBloomed )
			return false;

		IsBloomed = true;
		_spawnHealth = Math.Max( 1f, _spawnHealth * Math.Max( 1f, healthMultiplier ) );
		_spawnDamage = Math.Max( _spawnDamage, _spawnDamage * Math.Max( 1f, damageMultiplier ) );
		CurrentHealth = _spawnHealth;
		_huntLockedUntil = 0;
		ApplyBloomTint();
		LogAi( $"Bloomed mutation applied HP={_spawnHealth:F0} DMG={_spawnDamage:F1}" );
		return true;
	}

	void ApplyBloomTint()
	{
		var renderer = Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );
		if ( renderer.IsValid() )
			renderer.Tint = new Color( 0.35f, 0.62f, 1f );
	}

	public void HostStartBreedCooldown()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		_breedCooldownUntilUtcTicks = DateTime.UtcNow.AddSeconds( ThornsTameCatalog.BreedCooldownSeconds ).Ticks;
	}

	public void HostApplyBredTame(
		string ownerAccountKey,
		string displayName,
		int tier,
		float maxHealth,
		float damage,
		float speed,
		float detectionRange,
		IEnumerable<ushort> geneticSpeciesIds,
		IEnumerable<string> traitIds,
		bool isCrossbreed,
		bool isMutated )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || string.IsNullOrWhiteSpace( ownerAccountKey ) )
			return;

		IsAwaitingTame = false;
		IsTamed = true;
		TamedOwnerAccountKey = ownerAccountKey;
		TamedDisplayName = string.IsNullOrWhiteSpace( displayName ) ? _species?.DisplayName ?? "Tame" : displayName.Trim();
		if ( TamedDisplayName.Length > 32 )
			TamedDisplayName = TamedDisplayName[..32];

		BreedTier = Math.Clamp( tier, 1, 5 );
		IsCrossbreed = isCrossbreed;
		IsMutatedBreed = isMutated;
		GeneticSpeciesIdsCsv = NormalizeSpeciesCsv( geneticSpeciesIds );
		GeneticTraitIdsCsv = NormalizeTraitCsv( traitIds );
		_spawnHealth = Math.Max( 1f, maxHealth );
		_spawnDamage = Math.Max( 0f, damage );
		_spawnSpeed = Math.Max( 60f, speed );
		_breedDetectionRange = Math.Max( 0f, detectionRange );
		CurrentHealth = _spawnHealth;
		_huntLockedUntil = 0;
		ClearEncounter();
		SetAiState( ThornsAnimalState.Idle, "bred_tame" );
		_idleUntilAt = Time.Now + 1.5f;
		SyncAgentMoveSpeed();
		HostRefreshModelVisual();
	}

	/// <summary>Host-only: swap mesh/collider for crossbreed hybrids (or parent fallback).</summary>
	public void HostRefreshModelVisual()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !GameObject.IsValid() )
			return;

		if ( !ThornsAnimalSpeciesRegistry.TryGet( SpeciesId, out var bodySpecies ) )
			return;

		var visual = ThornsAnimalModelResolve.ResolveForBrain( this );
		ThornsAnimalModelResolve.ApplyToGameObject( GameObject, visual, bodySpecies );
		_cachedAttackAnimDuration = 0f;
	}

	static string NormalizeSpeciesCsv( IEnumerable<ushort> ids ) =>
		string.Join( ",", (ids ?? Array.Empty<ushort>()).Where( id => id > 0 ).Distinct().OrderBy( id => id ) );

	static string NormalizeTraitCsv( IEnumerable<string> traitIds ) =>
		string.Join( ",", (traitIds ?? Array.Empty<string>())
			.Select( id => id?.Trim().ToLowerInvariant() ?? "" )
			.Where( id => !string.IsNullOrWhiteSpace( id ) )
			.Distinct()
			.OrderBy( id => id ) );

	void ScheduleNextWanderDestination()
	{
		_nextWanderAt = Time.Now + Game.Random.Float( _species.WanderIntervalMin, _species.WanderIntervalMax );
	}

	void ScheduleNextChaseDestination()
	{
		var interval = _species.ChaseDestinationInterval;
		if ( _target.IsValid() && FlatDistanceTo( _target ) < 420f )
			interval *= 0.45f;

		_nextChaseAt = Time.Now + interval;
	}

	protected override void OnStart()
	{
		_ = Components.Get<ThornsWildlifeVocalization>() ?? Components.Create<ThornsWildlifeVocalization>();
		_ = Components.Get<ThornsNpcFootstepAudio>() ?? Components.Create<ThornsNpcFootstepAudio>();
		HostEnsureVisualLod();
		_agent ??= Components.Get<NavMeshAgent>();
		_corpse ??= Components.Get<ThornsAnimalCorpse>();
		if ( !ThornsAnimalSpeciesRegistry.TryGet( SpeciesId, out _species ) )
			ThornsAnimalSpeciesRegistry.TryGet( SpeciesId, out _species );

		if ( ThornsMultiplayer.IsHostOrOffline )
		{
			ThornsAnimalManager.Register( this );
		}
		else if ( _agent.IsValid() )
			_agent.Enabled = false;

		if ( !ThornsMultiplayer.IsHostOrOffline || IsDead )
			return;

		SyncAgentToGround();
		CacheBodyRadius();
		SyncAgentMoveSpeed();
		if ( AiState == ThornsAnimalState.Wander )
			TryMoveToWanderPoint();
	}

	void SyncAgentToGround()
	{
		if ( !_agent.IsValid() )
			return;

		var scene = Scene;
		var terrain = ThornsTerrainCache.Resolve( scene );
		if ( !ThornsAnimalWorldUtil.TrySnapToTerrain( terrain, GameObject.WorldPosition, out var snapped ) )
			return;

		var config = ThornsAnimalWorldUtil.ResolveTerrainConfig( scene );
		if ( config is not null && ThornsAnimalWorldUtil.IsUnderSeaLevel( scene, terrain, config, snapped ) )
		{
			TryMoveToWanderPoint();
			return;
		}

		GameObject.WorldPosition = snapped;
		SyncAgentToPosition( snapped );
	}

	protected override void OnUpdate()
	{
		if ( !GameObject.IsValid() || !IsValid )
			return;

		if ( IsBloomed )
			ApplyBloomTint();

		// Host simulation is driven by ThornsAnimalManager staggered scheduler.
	}

	static bool IsLiveComponent<T>( T component ) where T : Component
		=> component is not null && component.IsValid();

	void DrawDebugOverlay()
	{
		if ( _species is null && !ThornsAnimalSpeciesRegistry.TryGet( SpeciesId, out _species ) )
			return;

		var tameTag = IsTamed ? " tamed" : IsAwaitingTame ? " tame?" : "";
		var moveMode = MotorMode.ToString();
		var navTag = _agent.IsValid() && _agent.Enabled && _agent.IsNavigating ? "+path" : "";
		var label =
			$"{_species.DisplayName} | {AiState}{tameTag} | {moveMode}{navTag} | LOD {LodTier} | HP {CurrentHealth:F0}";
		var origin = GameObject.WorldPosition + Vector3.Up * 96f;
		DebugOverlay.Text( origin, label, duration: 0.1f );
		DebugOverlay.Text( origin - Vector3.Up * 14f, MovementDebugSummary, duration: 0.1f );

		if ( IsActiveLocomotionSample )
			DebugOverlay.Text( origin - Vector3.Up * 28f, BuildSpeedDebugLine(), duration: 0.1f );

		if ( HasMoveIntent )
		{
			EnsureMotor();
			DebugOverlay.Line( GameObject.WorldPosition, _motor.IntentDestination, Color.Yellow, 0.1f );
		}
	}

	protected override void OnDestroy()
	{
		ThornsAnimalManager.Unregister( this );
	}

	internal void HostDetect( IReadOnlyList<ThornsAnimalBrain> animals, IReadOnlyList<GameObject> players )
	{
		if ( IsDead || _species is null )
			return;

		if ( IsAwaitingTame )
			return;

		if ( IsTamed )
		{
			HostCompanionDetect( animals );
			return;
		}

		if ( TryPrioritizeSelfDefenseTarget() )
			return;

		if ( _species.BehaviorType == ThornsAnimalBehaviorType.Predator && Time.Now < _huntLockedUntil )
			return;

		if ( AiState is ThornsAnimalState.Chase or ThornsAnimalState.Attack or ThornsAnimalState.Flee )
		{
			if ( _target.IsValid() && IsTargetStillValid( _target ) )
				return;
		}

		var best = FindBestTarget( animals, players );
		if ( !best.IsValid() )
			return;

		if ( ThornsAnimalWorldUtil.IsPlayerObject( best ) )
		{
			NotifyPlayerEncounter( best );

			if ( IsBloomed && _spawnDamage > 0f )
			{
				BeginHunt( best, alertPack: true );
				return;
			}

			BeginPlayerEncounter( best );
			return;
		}

		if ( best.Components.Get<ThornsAnimalBrain>( FindMode.EverythingInSelfAndParent ) is { IsValid: true } preyBrain )
		{
			var preyLabel = ThornsAnimalSpeciesRegistry.TryGet( preyBrain.SpeciesId, out var preySpecies )
				? preySpecies.DisplayName
				: preyBrain.SpeciesId.ToString();
			LogCombat( $"Detected animal '{preyLabel}' - responding." );
			BeginWildlifeEncounter( best, preyBrain.SpeciesId );
			return;
		}

		if ( best.Components.Get<ThornsBanditBrain>( FindMode.EverythingInSelfAndParent ) is { IsValid: true } )
		{
			LogCombat( $"Detected bandit '{best.Name}' - responding." );
			BeginWildlifeEncounter( best, isBandit: true );
			return;
		}

	}

	void TickStateMachine()
	{
		switch ( AiState )
		{
			case ThornsAnimalState.Idle:
				TickIdle();
				break;
			case ThornsAnimalState.Wander:
				TickWander();
				break;
			case ThornsAnimalState.Chase:
				TickChase();
				break;
			case ThornsAnimalState.Attack:
				TickAttack();
				break;
			case ThornsAnimalState.Flee:
				TickFlee();
				break;
		}
	}

	void TickIdle()
	{
		if ( IsAwaitingTame )
		{
			StopMotor();
			return;
		}

		if ( IsTamed )
		{
			if ( ShouldTickTamedFollow() )
				TickTamedFollowMovement();
			else
				StopMotor();

			return;
		}

		StopMotor();
		if ( Time.Now < _idleUntilAt )
			return;

		EnterWander();
	}

	void TickWander()
	{
		if ( IsAwaitingTame )
		{
			StopMotor();
			return;
		}

		if ( ShouldTickTamedFollow() )
		{
			TickTamedFollowMovement();
			return;
		}

		if ( HasReachedMoveGoal() )
		{
			if ( _species.BehaviorType == ThornsAnimalBehaviorType.Predator && Time.Now < _huntLockedUntil )
			{
				ScheduleNextWanderDestination();
				TryMoveToWanderPoint();
				return;
			}

			SetAiState( ThornsAnimalState.Idle, "wander_goal_reached" );
			_idleUntilAt = Time.Now + Game.Random.Float( _species.IdlePauseMin, _species.IdlePauseMax );
			StopMotor();
			return;
		}

		if ( Time.Now < _nextWanderAt )
			return;

		ScheduleNextWanderDestination();
		if ( !HasMoveIntent )
			TryMoveToWanderPoint();
	}

	void TickChase()
	{
		if ( !ValidateActiveTarget( checkChaseDistance: true ) )
			return;

		if ( _target.IsValid() )
			FaceTarget( _target );

		if ( CanBeginAttack( _target ) )
		{
			StopMotor();
			if ( _attackCooldownReady )
				BeginStrike();
			return;
		}

		if ( _nextChaseAt > Time.Now )
			return;

		ScheduleNextChaseDestination();
		SetMotorIntent( ResolveChaseApproachPoint( _target ) );
	}

	void TickAttack()
	{
		if ( !ValidateActiveTarget( checkChaseDistance: true ) )
			return;

		StopMotor();

		if ( _target.IsValid() )
			FaceTarget( _target );

		if ( Time.Now < _strikeEndsAt )
			return;

		EndStrikeResumeChase();
	}

	void BeginStrike()
	{
		var attackRange = ResolveEffectiveAttackRange();
		var damage = ResolveEffectiveSpawnDamage();
		if ( _species is null || attackRange <= 0f || damage <= 0f )
			return;

		if ( !_attackCooldownReady || !_target.IsValid() || !CanBeginAttack( _target ) )
			return;

		var strikeDuration = ResolveStrikeTiming( out var playbackRate );
		StrikeAnimPlaybackRate = playbackRate;
		StrikeSerial++;

		SetAiState( ThornsAnimalState.Attack, "strike" );
		StopMotor();
		FaceTarget( _target );
		HostApplyAttack( _target );
		_attackCooldownReady = _species.AttackCooldown;
		_strikeEndsAt = Time.Now + strikeDuration;
	}

	void EndStrikeResumeChase()
	{
		_strikeEndsAt = 0;

		if ( !_target.IsValid() )
			return;

		SetAiState( ThornsAnimalState.Chase, "strike_done" );
		_nextChaseAt = 0;
		if ( CanBeginAttack( _target ) )
			StopMotor();
		else
			SetMotorIntent( ResolveChaseApproachPoint( _target ) );
	}

	float ResolveStrikeTiming( out float playbackRate )
	{
		var animDuration = ResolveAttackAnimDuration();
		var cooldown = Math.Max( _species?.AttackCooldown ?? 1f, 0.05f );

		if ( animDuration <= cooldown )
		{
			playbackRate = 1f;
			return animDuration;
		}

		playbackRate = Math.Clamp( animDuration / cooldown, 1f, 1.5f );
		return animDuration / playbackRate;
	}

	float ResolveAttackAnimDuration()
	{
		if ( _cachedAttackAnimDuration > 0.05f )
			return _cachedAttackAnimDuration;

		var fallback = 0.75f;
		var renderer = Components.Get<SkinnedModelRenderer>();
		if ( !renderer.IsValid() || _species is null )
		{
			_cachedAttackAnimDuration = fallback;
			return fallback;
		}

		var prefix = ThornsAnimalModelResolve.ResolveForBrain( this ).AnimPrefix;
		if ( string.IsNullOrWhiteSpace( prefix ) )
			prefix = _species.AnimPrefix;

		var attackSequence = $"{prefix}_attack";
		var previous = renderer.Sequence.Name;
		renderer.Sequence.Name = attackSequence;
		var duration = renderer.Sequence.Duration;
		renderer.Sequence.Name = string.IsNullOrEmpty( previous ) ? attackSequence : previous;

		_cachedAttackAnimDuration = duration > 0.05f ? duration : fallback;
		return _cachedAttackAnimDuration;
	}

	void TickFlee()
	{
		if ( !_target.IsValid() )
		{
			ClearEncounter();
			EnterWander();
			return;
		}

		if ( (GameObject.WorldPosition - _target.WorldPosition).Length >= _species.FleeSafeDistanceOrDefault )
		{
			ClearEncounter();
			EnterWander();
			return;
		}

		if ( _nextChaseAt > Time.Now )
			return;

		ScheduleNextChaseDestination();
		SetMotorIntent( ResolveFleePoint( _target ) );
	}

	void FaceTarget( GameObject target )
	{
		if ( !target.IsValid() )
			return;

		var flat = target.WorldPosition - GameObject.WorldPosition;
		flat.z = 0f;
		if ( flat.LengthSquared < 0.01f )
			return;

		var face = Rotation.LookAt( flat.Normal );
		GameObject.WorldRotation = Rotation.Slerp( GameObject.WorldRotation, face, Time.Delta * 10f );
	}

	bool CanBeginAttack( GameObject target )
	{
		if ( IsTamed )
			return IsWithinAttackStickRange( target );

		return IsWithinAttackRange( target );
	}

	float FlatDistanceTo( GameObject target )
	{
		if ( !target.IsValid() )
			return float.MaxValue;

		return GameObject.WorldPosition.WithZ( 0f ).Distance( target.WorldPosition.WithZ( 0f ) );
	}

	void BeginHunt( GameObject prey, bool alertPack = true, bool retaliation = false )
	{
		_target = prey;
		_retaliationTarget = retaliation ? prey : null;
		_huntStartPosition = GameObject.WorldPosition;
		SetAiState( ThornsAnimalState.Chase, "hunt" );
		_nextChaseAt = 0;
		SetMotorIntent( ResolveChaseApproachPoint( prey ) );

		if ( prey.Components.Get<ThornsAnimalBrain>( FindMode.EverythingInSelfAndParent ) is { IsValid: true } preyBrain )
		{
			var preyLabel = ThornsAnimalSpeciesRegistry.TryGet( preyBrain.SpeciesId, out var preySpecies )
				? preySpecies.DisplayName
				: preyBrain.SpeciesId.ToString();
			LogCombat(
				$"BeginHunt prey={preyLabel} dist={FlatDistanceTo( prey ):F0} " +
				$"retaliation={retaliation} pack={alertPack && _species.HuntsInGroups}" );
		}

		if ( alertPack && _species.HuntsInGroups )
			ThornsAnimalManager.NotifyPackHunt( this, prey );
	}

	internal void HostJoinPackHunt( GameObject prey )
	{
		if ( IsTamed || IsAwaitingTame )
			return;

		if ( IsDead || _species is null || !_species.HuntsInGroups || !prey.IsValid() )
			return;

		if ( Time.Now < _huntLockedUntil )
			return;

		if ( !IsTargetStillValid( prey ) )
			return;

		if ( AiState is ThornsAnimalState.Chase or ThornsAnimalState.Attack && _target == prey )
			return;

		if ( AiState is ThornsAnimalState.Chase or ThornsAnimalState.Attack or ThornsAnimalState.Flee )
			return;

		BeginHunt( prey, alertPack: false );
	}

	internal void HostJoinHerdFlee( GameObject threat )
	{
		if ( IsTamed || IsAwaitingTame )
			return;

		if ( IsDead || _herdGroupId == 0 || !threat.IsValid() )
			return;

		if ( AiState is ThornsAnimalState.Flee && _target == threat )
			return;

		if ( AiState is ThornsAnimalState.Chase or ThornsAnimalState.Attack )
			return;

		BeginFlee( threat, alertHerd: false );
	}

	void BeginPlayerEncounter( GameObject player )
	{
		var fightChance = _species.ResolvePlayerFightChance();
		if ( fightChance <= 0f )
		{
			BeginFlee( player );
			return;
		}

		if ( fightChance >= 1f || Game.Random.Float( 0f, 1f ) < fightChance )
		{
			BeginHunt( player, alertPack: _species.HuntsInGroups );
			return;
		}

		BeginFlee( player );
	}

	void BeginFlee( GameObject threat, bool alertHerd = true )
	{
		_target = threat;
		SetAiState( ThornsAnimalState.Flee, "flee" );
		_nextChaseAt = 0;
		SetMotorIntent( ResolveFleePoint( threat ) );

		if ( alertHerd )
			AlertHerdIfApplicable( threat );
	}

	void AlertHerdIfApplicable( GameObject threat )
	{
		if ( _herdGroupId == 0 || _species is null || _species.SocialMode != ThornsAnimalSocialMode.Herd )
			return;

		ThornsAnimalManager.NotifyHerdFlee( this, threat );
	}

	void BeginMixedEncounter( GameObject threat )
	{
		if ( _mixedThreat != threat )
		{
			_mixedThreat = threat;
			_mixedMode = Game.Random.Float( 0f, 1f ) < 0.5f ? ThornsAnimalMixedMode.Flee : ThornsAnimalMixedMode.Fight;
		}

		_target = threat;
		if ( _mixedMode == ThornsAnimalMixedMode.Flee )
		{
			SetAiState( ThornsAnimalState.Flee, "mixed_flee" );
			_nextChaseAt = 0;
			SetMotorIntent( ResolveFleePoint( threat ) );
			AlertHerdIfApplicable( threat );

			return;
		}

		_huntStartPosition = GameObject.WorldPosition;
		SetAiState( ThornsAnimalState.Chase, "mixed_fight" );
		_nextChaseAt = 0;
		SetMotorIntent( ResolveChaseApproachPoint( threat ) );
	}

	void EnterWander()
	{
		SetAiState( ThornsAnimalState.Wander, "wander" );
		ScheduleNextWanderDestination();
		TryMoveToWanderPoint();
	}

	void ClearEncounter()
	{
		_target = null;
		_retaliationTarget = null;
		_recentAttacker = null;
		_recentAttackerUntilRealtime = 0;
		_mixedThreat = null;
		_mixedMode = ThornsAnimalMixedMode.None;
		_strikeEndsAt = 0;
	}

	bool ValidateActiveTarget( bool checkChaseDistance )
	{
		if ( _species is null )
			return false;

		if ( !_target.IsValid() || !IsTargetStillValid( _target ) )
		{
			ClearEncounter();
			EnterWander();
			return false;
		}

		if ( checkChaseDistance && AiState is ThornsAnimalState.Chase or ThornsAnimalState.Attack )
		{
			var preyLeash = (_target.WorldPosition - _huntStartPosition).WithZ( 0f ).Length;
			if ( preyLeash > _species.MaxChaseDistance )
			{
				ClearEncounter();
				EnterWander();
				return false;
			}
		}

		if ( _species.BehaviorType == ThornsAnimalBehaviorType.Mixed && _mixedMode != ThornsAnimalMixedMode.None )
		{
			var threatDistance = (GameObject.WorldPosition - _target.WorldPosition).Length;
			if ( threatDistance >= _species.FleeSafeDistanceOrDefault && _mixedMode == ThornsAnimalMixedMode.Fight )
			{
				ClearEncounter();
				EnterWander();
				return false;
			}
		}

		return true;
	}

	GameObject FindBestTarget( IReadOnlyList<ThornsAnimalBrain> animals, IReadOnlyList<GameObject> players )
	{
		var origin = GameObject.WorldPosition;
		var range = _species.DetectionRange;
		GameObject best = null;
		var bestDist = float.MaxValue;
		var predatorPrefersPrey = _species.BehaviorType == ThornsAnimalBehaviorType.Predator;

		for ( var i = 0; i < players.Count; i++ )
		{
			var player = players[i];
			if ( !IsDetectablePlayer( player ) )
				continue;

			if ( IsTamed && ResolveTamedOwner() == player )
				continue;

			var dist = (player.WorldPosition - origin).Length;
			if ( dist > range || dist >= bestDist )
				continue;

			best = player;
			bestDist = dist;
		}

		for ( var i = 0; i < animals.Count; i++ )
		{
			var other = animals[i];
			if ( other == this || other.IsDead || !other.GameObject.IsValid() )
				continue;

			if ( other.IsTamed )
				continue;

			if ( !ShouldDetectAnimal( other ) )
				continue;

			var dist = (other.GameObject.WorldPosition - origin).Length;
			if ( dist > range )
				continue;

			var pickDist = predatorPrefersPrey ? dist * 0.82f : dist;
			if ( pickDist >= bestDist )
				continue;

			best = other.GameObject;
			bestDist = pickDist;
		}

		if ( ShouldDetectBandits() )
		{
			foreach ( var bandit in ThornsBanditPopulation.HostBrainsReadOnly )
			{
				if ( !bandit.IsValid() || bandit.IsDead )
					continue;

				if ( !IsDetectableBandit( bandit ) )
					continue;

				var dist = (bandit.GameObject.WorldPosition - origin).Length;
				if ( dist > range || dist >= bestDist )
					continue;

				best = bandit.GameObject;
				bestDist = dist;
			}
		}

		return best;
	}

	bool IsDetectablePlayer( GameObject player )
	{
		if ( !player.IsValid() )
			return false;

		if ( ThornsAnimalManager.ShouldIgnorePlayers( _species ) )
			return false;

		if ( !ThornsAnimalWorldUtil.IsPlayerThreat( _species, player ) )
			return false;

		return _species.ResolvePlayerFightChance() < 1f
		       || ThornsAnimalWorldUtil.CanPredatorAttackPlayer( _species );
	}

	bool ShouldDetectBandits() =>
		_species.BehaviorType is ThornsAnimalBehaviorType.Prey
			or ThornsAnimalBehaviorType.Mixed
			or ThornsAnimalBehaviorType.Predator;

	bool ShouldDetectAnimal( ThornsAnimalBrain other )
	{
		if ( other is null || !other.IsValid() || other.IsDead )
			return false;

		if ( !IsTamed && !other.IsTamed && other.SpeciesId == SpeciesId )
			return false;

		if ( !ThornsAnimalSpeciesRegistry.TryGet( other.SpeciesId, out var otherSpecies ) )
			return false;

		return _species.BehaviorType switch
		{
			ThornsAnimalBehaviorType.Prey =>
				_species.IsThreatSpecies( other.SpeciesId )
				|| otherSpecies.BehaviorType == ThornsAnimalBehaviorType.Predator,
			ThornsAnimalBehaviorType.Predator => ShouldHuntSpecies( other.SpeciesId ),
			ThornsAnimalBehaviorType.Mixed =>
				_species.IsThreatSpecies( other.SpeciesId ) || ShouldHuntSpecies( other.SpeciesId ),
			_ => false
		};
	}

	bool ShouldHuntSpecies( ushort speciesId ) =>
		_species.IsPreyTarget( speciesId ) || _species.CanAttackSpecies( speciesId );

	void BeginWildlifeEncounter( GameObject target, ushort otherSpeciesId = 0, bool isBandit = false )
	{
		if ( !target.IsValid() || _species is null )
			return;

		if ( isBandit )
		{
			switch ( _species.BehaviorType )
			{
				case ThornsAnimalBehaviorType.Prey:
					BeginFlee( target );
					break;
				case ThornsAnimalBehaviorType.Mixed:
				case ThornsAnimalBehaviorType.Predator:
					BeginMixedEncounter( target );
					break;
			}

			return;
		}

		switch ( _species.BehaviorType )
		{
			case ThornsAnimalBehaviorType.Prey:
				BeginFlee( target );
				break;
			case ThornsAnimalBehaviorType.Mixed:
				if ( _species.IsThreatSpecies( otherSpeciesId ) )
					BeginMixedEncounter( target );
				else if ( ShouldHuntSpecies( otherSpeciesId ) )
					BeginHunt( target, alertPack: false );
				break;
			case ThornsAnimalBehaviorType.Predator:
				if ( ShouldHuntSpecies( otherSpeciesId ) )
					BeginHunt( target, alertPack: true );
				break;
		}
	}

	bool IsDetectableAnimal( ThornsAnimalBrain other ) => ShouldDetectAnimal( other );

	bool IsDetectableBandit( ThornsBanditBrain bandit )
	{
		if ( !bandit.IsValid() || bandit.IsDead )
			return false;

		return true;
	}

	bool IsTargetStillValid( GameObject target )
	{
		if ( !target.IsValid() || IsFriendlyTameTarget( target ) )
			return false;

		var playerHealth = target.Components.Get<ThornsPlayerHealth>( FindMode.EverythingInSelfAndDescendants );
		if ( IsLiveComponent( playerHealth ) )
			return playerHealth.IsAlive;

		var bandit = target.Components.Get<ThornsBanditBrain>( FindMode.EverythingInSelfAndParent );
		if ( bandit.IsValid() )
			return !bandit.IsDead;

		var otherBrain = target.Components.Get<ThornsAnimalBrain>( FindMode.EverythingInSelfAndParent );
		return IsLiveComponent( otherBrain ) && !otherBrain.IsDead && !otherBrain.IsAwaitingTame;
	}

	bool IsFriendlyTameTarget( GameObject target )
	{
		if ( !IsTamed || !target.IsValid() )
			return false;

		var otherBrain = target.Components.Get<ThornsAnimalBrain>( FindMode.EverythingInSelfAndParent );
		return IsLiveComponent( otherBrain ) && otherBrain.IsTamed
		                                    && !string.IsNullOrEmpty( TamedOwnerAccountKey )
		                                    && otherBrain.TamedOwnerAccountKey == TamedOwnerAccountKey;
	}

	static bool IsAwaitingTameTarget( GameObject target )
	{
		if ( !target.IsValid() )
			return false;

		var brain = target.Components.Get<ThornsAnimalBrain>( FindMode.EverythingInSelfAndParent );
		return IsLiveComponent( brain ) && brain.IsAwaitingTame;
	}

	float ResolveEffectiveAttackRange()
	{
		if ( _species is null )
			return 0f;

		if ( !IsTamed )
			return _species.AttackRange;

		var baseline = ResolveCompanionCombatBaseline();
		return Math.Max( _species.AttackRange, baseline.AttackRange );
	}

	float ResolveEffectiveSpawnDamage()
	{
		if ( !IsTamed )
			return _spawnDamage;

		return Math.Max( _spawnDamage, ResolveCompanionCombatBaseline().Damage );
	}

	(float Damage, float AttackRange) ResolveCompanionCombatBaseline()
	{
		var tier = Math.Clamp( BreedTier > 0 ? BreedTier : _species?.TameTier ?? 1, 1, 5 );
		return tier switch
		{
			1 => (10f, 78f),
			2 => (14f, 88f),
			3 => (18f, 98f),
			4 => (22f, 108f),
			_ => (26f, 118f)
		};
	}

	float ResolveMeleeReachTo( GameObject target )
	{
		var attackRange = ResolveEffectiveAttackRange();
		if ( attackRange <= 0f || !target.IsValid() )
			return 0f;

		var selfFront = GetBodyRadius() * 0.48f;
		var biteReach = attackRange * 0.82f;

		var otherBrain = target.Components.Get<ThornsAnimalBrain>( FindMode.EverythingInSelfAndParent );
		if ( IsLiveComponent( otherBrain ) )
		{
			return biteReach
			       + selfFront
			       + otherBrain.GetBodyRadius() * 0.48f
			       + ThornsAnimalSeparation.SeparationPadding * 0.35f;
		}

		return biteReach + selfFront + HumanoidMeleeReachBonus;
	}

	bool IsWithinAttackRange( GameObject target )
		=> target.IsValid() && FlatDistanceTo( target ) <= ResolveMeleeReachTo( target );

	bool IsWithinAttackStickRange( GameObject target )
		=> target.IsValid() && FlatDistanceTo( target ) <= ResolveMeleeReachTo( target ) * 1.14f;

	internal bool ShouldHoldCombatPosition()
		=> AiState is ThornsAnimalState.Chase or ThornsAnimalState.Attack
		   && _target.IsValid()
		   && CanBeginAttack( _target );

	void HostApplyAttack( GameObject target )
	{
		var damage = ResolveMeleeDamage();
		if ( damage <= 0f || !target.IsValid() )
		{
			LogCombat( $"HostApplyAttack skipped — damage={damage:F1} targetValid={target.IsValid()}" );
			return;
		}

		target = ThornsBanditUtil.ResolveHostileChaseRoot( target );
		if ( !target.IsValid() )
		{
			LogCombat( "HostApplyAttack skipped — ResolveHostileChaseRoot returned invalid target." );
			return;
		}

		var attackerFaction = IsTamed ? ThornsCombatFactions.FactionKind.TamedAnimal : ThornsCombatFactions.FactionKind.Wildlife;

		var playerHealth = target.Components.Get<ThornsPlayerHealth>( FindMode.EverythingInSelfAndDescendants );
		if ( IsLiveComponent( playerHealth ) )
		{
			if ( IsTamed )
			{
				var owner = ResolveTamedOwner();
				var targetRoot = target.Root;
				if ( !owner.IsValid() || target == owner || (targetRoot.IsValid() && targetRoot == owner) )
					return;

				var companionResult = ThornsCombatDamage.HostApplyDamage( GameObject, target, new ThornsCombatDamage.DamageInfo
				{
					Amount = damage,
					AttackerRoot = GameObject,
					VictimRoot = target,
					DamageTypeId = "melee",
					VictimKind = ThornsCombatDamage.VictimKind.Player,
					AttackerFaction = attackerFaction,
					VictimFaction = ThornsCombatFactions.FactionKind.Player,
					WeaponId = "melee",
					HitPosition = target.WorldPosition + Vector3.Up * 48f
				} );
				if ( companionResult.Applied )
					LogAi( $"Attack player {target.Name} for {damage:F1} (companion)" );
				if ( companionResult.Killed )
					EnterPostKillWander();

				return;
			}

			if ( ThornsAnimalWorldUtil.CanPredatorAttackPlayer( _species )
			          || _species.BehaviorType == ThornsAnimalBehaviorType.Mixed
			          || target == _retaliationTarget )
			{
				var playerResult = ThornsCombatDamage.HostApplyDamage( GameObject, target, new ThornsCombatDamage.DamageInfo
				{
					Amount = damage,
					AttackerRoot = GameObject,
					VictimRoot = target,
					DamageTypeId = "melee",
					VictimKind = ThornsCombatDamage.VictimKind.Player,
					AttackerFaction = ThornsCombatFactions.FactionKind.Wildlife,
					VictimFaction = ThornsCombatFactions.FactionKind.Player,
					WeaponId = "melee",
					HitPosition = target.WorldPosition + Vector3.Up * 48f
				} );
				if ( playerResult.Applied )
				{
					LogAi( $"Attack player {target.Name} for {damage:F1}" );
					LogCombat( $"Hit player {target.Name} for {damage:F1}" );
				}
				else
				{
					LogCombat( $"HostApplyDamage rejected player {target.Name} dmg={damage:F1}" );
				}

				if ( playerResult.Killed )
					EnterPostKillWander();
			}

			return;
		}

		var banditBrain = target.Components.Get<ThornsBanditBrain>( FindMode.EverythingInSelfAndParent );
		if ( banditBrain.IsValid() && !banditBrain.IsDead )
		{
			var banditHitPosition = banditBrain.GameObject.WorldPosition + Vector3.Up * 48f;
			var banditHitNormal = (banditBrain.GameObject.WorldPosition - GameObject.WorldPosition).WithZ( 0.25f ).Normal;
			var banditResult = ThornsCombatDamage.HostApplyDamage( GameObject, banditBrain.GameObject, new ThornsCombatDamage.DamageInfo
			{
				Amount = damage,
				AttackerRoot = GameObject,
				VictimRoot = banditBrain.GameObject,
				VictimKind = ThornsCombatDamage.VictimKind.Npc,
				AttackerFaction = attackerFaction,
				VictimFaction = ThornsCombatFactions.FactionKind.Bandit,
				DamageTypeId = "melee",
				WeaponId = "melee",
				HitPosition = banditHitPosition,
				HitNormal = banditHitNormal
			} );
			if ( banditResult.Applied )
			{
				LogAi( $"Attack bandit {target.Name} for {damage:F1}" );
				ThornsBanditCommunication.HostBroadcastWildlifeAttackAlert(
					banditBrain,
					GameObject,
					banditHitPosition );
			}

			if ( banditResult.Killed )
				EnterPostKillWander();

			return;
		}

		ThornsBanditCommunication.HostRegisterAnimalAttack( GameObject.WorldPosition );

		var otherBrain = target.Components.Get<ThornsAnimalBrain>( FindMode.EverythingInSelfAndParent );
		if ( !IsLiveComponent( otherBrain ) || otherBrain.IsDead )
		{
			LogCombat( $"HostApplyAttack skipped — no live animal brain on '{target.Name}'." );
			return;
		}

		if ( IsTamed && otherBrain.IsAwaitingTame )
		{
			LogCombat( "HostApplyAttack skipped — tamed attacker will not hit awaiting-tame prey." );
			return;
		}

		if ( !IsTamed && !CanMeleeAnimalTarget( otherBrain, out var rejectReason ) )
		{
			LogCombat( $"HostApplyAttack blocked — {rejectReason}" );
			return;
		}

		var hitPosition = otherBrain.GameObject.WorldPosition + Vector3.Up * otherBrain.GetBodyRadius() * 0.55f;
		var hitNormal = (otherBrain.GameObject.WorldPosition - GameObject.WorldPosition).WithZ( 0.25f ).Normal;
		var animalHit = new ThornsCombatDamage.DamageInfo
		{
			Amount = damage,
			AttackerRoot = GameObject,
			VictimRoot = otherBrain.GameObject,
			VictimKind = ThornsCombatDamage.VictimKind.Animal,
			AttackerFaction = attackerFaction,
			VictimFaction = otherBrain.IsTamed ? ThornsCombatFactions.FactionKind.TamedAnimal : ThornsCombatFactions.FactionKind.Wildlife,
			DamageTypeId = "melee",
			WeaponId = "melee",
			HitPosition = hitPosition,
			HitNormal = hitNormal
		};
		var animalResult = ThornsCombatDamage.HostApplyDamage( GameObject, otherBrain.GameObject, animalHit );
		if ( animalResult.Applied )
		{
			LogAi( $"Attack animal {target.Name} for {damage:F1}" );
			LogCombat(
				$"Hit {otherBrain.GameObject.Name} for {damage:F1} HP {otherBrain.CurrentHealth:F0}/{otherBrain.MaxHealth:F0}" );
		}
		else
		{
			LogCombat(
				$"HostApplyDamage rejected victim={otherBrain.GameObject.Name} dmg={damage:F1} " +
				$"attackerFaction={attackerFaction} victimFaction={animalHit.VictimFaction}" );
		}

		if ( animalResult.Killed )
			EnterPostKillWander();
	}

	static bool IsSameCombatTarget( GameObject a, GameObject b )
	{
		if ( !a.IsValid() || !b.IsValid() )
			return false;

		if ( a == b )
			return true;

		var aRoot = ThornsBanditUtil.ResolveHostileChaseRoot( a );
		var bRoot = ThornsBanditUtil.ResolveHostileChaseRoot( b );
		return aRoot.IsValid() && bRoot == aRoot;
	}

	float ResolveMeleeDamage() => ResolveEffectiveSpawnDamage() * MeleeDamageMultiplier;

	bool CanMeleeAnimalTarget( ThornsAnimalBrain otherBrain, out string rejectReason )
	{
		rejectReason = "";
		if ( otherBrain is null || !otherBrain.IsValid() || otherBrain.IsDead )
		{
			rejectReason = "victim dead or invalid";
			return false;
		}

		if ( otherBrain.IsAwaitingTame )
		{
			rejectReason = "victim awaiting tame";
			return false;
		}

		if ( ThornsAnimalCombatRules.ShouldIgnoreDamage( otherBrain, GameObject ) )
		{
			rejectReason = "tame-owner protection";
			return false;
		}

		if ( !IsTamed && !otherBrain.IsTamed && otherBrain.SpeciesId == SpeciesId )
		{
			rejectReason = "same-species wild animal";
			return false;
		}

		if ( !IsTamed && !otherBrain.IsTamed && _species is not null )
		{
			var hasAttackList = _species.CanAttackSpeciesIds is { Length: > 0 };
			if ( hasAttackList && !_species.CanAttackSpecies( otherBrain.SpeciesId ) )
			{
				rejectReason = "not a valid prey species";
				return false;
			}
		}

		return true;
	}

	void EnterPostKillWander()
	{
		if ( IsTamed )
		{
			HostGrantTameExperience( ThornsTameProgression.XpPerKill );
			ClearEncounter();
			SetAiState( ThornsAnimalState.Idle, "companion_kill_pause" );
			_idleUntilAt = Time.Now + 1.25f;
			return;
		}

		if ( _species.BehaviorType != ThornsAnimalBehaviorType.Predator )
			return;

		HostEnterPostKillWander();
		if ( _species.HuntsInGroups )
			ThornsAnimalManager.NotifyPackPostKillWander( this );
	}

	internal void HostEnterPostKillWander()
	{
		if ( IsDead || _species?.BehaviorType != ThornsAnimalBehaviorType.Predator )
			return;

		_huntLockedUntil = Time.Now + PredatorPostKillWanderSeconds;
		ClearEncounter();
		EnterWander();
	}

	public bool HostTakeDamage( float amount, GameObject attackerRoot )
	{
		var receiver = ThornsAnimalDamageReceiver.EnsureOn( this );
		if ( !receiver.IsValid )
			return false;

		var result = receiver.HostApplyDamage( attackerRoot, new ThornsCombatDamage.DamageInfo
		{
			Amount = amount,
			AttackerRoot = attackerRoot,
			VictimRoot = GameObject,
			DamageTypeId = "legacy",
			VictimKind = ThornsCombatDamage.VictimKind.Animal,
			AttackerFaction = ThornsCombatFactions.ResolveFaction( attackerRoot ),
			VictimFaction = IsTamed ? ThornsCombatFactions.FactionKind.TamedAnimal : ThornsCombatFactions.FactionKind.Wildlife
		} );
		return result.Killed;
	}

	internal bool HostApplyDamageFromPipeline( float amount, GameObject attackerRoot, in ThornsCombatDamage.DamageInfo info )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || IsDead )
			return false;

		var nonLethalKnockout = ShouldNonLethallyKnockoutFromAttacker( attackerRoot );
		if ( nonLethalKnockout )
			amount = ThornsAnimalTaming.ClampDamageForKnockout( amount, CurrentHealth, _spawnHealth );

		if ( amount <= 0f )
		{
			TryEnterAwaitingTameIfLow( attackerRoot );
			return false;
		}

		var before = CurrentHealth;
		CurrentHealth = Math.Max( 0f, CurrentHealth - amount );
		var attackerName = attackerRoot.IsValid() ? attackerRoot.Name : "unknown";
		if ( CurrentHealth > 0f )
		{
			LogCombat( $"Damage {amount:F1} from {attackerName} -> HP {before:F0}->{CurrentHealth:F0}" );
			HostRegisterRecentAttacker( attackerRoot );
			TryEnterAwaitingTameIfLow( attackerRoot );
			if ( !IsAwaitingTame && !IsTamed )
				HostRetaliateAgainst( attackerRoot );
			else if ( IsTamed )
				HostAlertOwnerThreat( attackerRoot );

			_ = info;
			return false;
		}

		if ( nonLethalKnockout )
		{
			CurrentHealth = _spawnHealth * ThornsAnimalTaming.LowHealthFraction;
			LogCombat( $"Knocked out by tamed {attackerName} (would have been lethal {amount:F1})" );
			HostRegisterRecentAttacker( attackerRoot );
			TryEnterAwaitingTameIfLow( attackerRoot );
			return false;
		}

		LogCombat( $"Killed by {attackerName} (final hit {amount:F1})" );
		HostDie( attackerRoot );
		return true;
	}

	bool ShouldNonLethallyKnockoutFromAttacker( GameObject attackerRoot )
	{
		if ( IsTamed || IsAwaitingTame || !IsTamedAnimalAttacker( attackerRoot ) )
			return false;

		return true;
	}

	static bool IsTamedAnimalAttacker( GameObject attackerRoot )
	{
		if ( !attackerRoot.IsValid() )
			return false;

		var brain = attackerRoot.Components.Get<ThornsAnimalBrain>( FindMode.EverythingInSelfAndParent );
		return IsLiveComponent( brain ) && brain.IsTamed;
	}

	void TryEnterAwaitingTameIfLow( GameObject attackerRoot )
	{
		if ( IsTamed || IsAwaitingTame || IsDead || _spawnHealth <= 0f )
			return;

		// Wildlife brawls should end in a corpse, not a tameable knockout.
		if ( IsWildWildlifeAttacker( attackerRoot ) )
			return;

		if ( CurrentHealth / _spawnHealth > ThornsAnimalTaming.LowHealthFraction )
			return;

		HostEnterAwaitingTame();
	}

	static bool IsWildWildlifeAttacker( GameObject attackerRoot )
	{
		if ( !attackerRoot.IsValid() )
			return false;

		var brain = attackerRoot.Components.Get<ThornsAnimalBrain>( FindMode.EverythingInSelfAndParent );
		return IsLiveComponent( brain ) && !brain.IsTamed;
	}

	internal void HostEnterAwaitingTame()
	{
		if ( IsDead || IsTamed || IsAwaitingTame )
			return;

		IsAwaitingTame = true;
		_huntLockedUntil = double.MaxValue;
		ClearEncounter();
		StopMotor();

		SetAiState( ThornsAnimalState.Idle, "awaiting_tame" );
		_idleUntilAt = double.MaxValue;
		LogAi( $"Awaiting tame at {CurrentHealth:F0}/{_spawnHealth:F0} HP" );
		ThornsAnimalCompanion.NotifyStopAttacking( GameObject );
	}

	public bool HostTryTame( GameObject playerRoot, Connection owner )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !IsAwaitingTame || IsTamed || IsDead || !playerRoot.IsValid() )
			return false;

		var accountKey = ResolveTameOwnerAccountKey( playerRoot, owner );
		if ( string.IsNullOrEmpty( accountKey ) )
			return false;

		if ( playerRoot.WorldPosition.Distance( GameObject.WorldPosition ) > ThornsAnimalTaming.UseMaxRange * 1.15f )
			return false;

		IsAwaitingTame = false;
		IsTamed = true;
		TamedOwnerAccountKey = accountKey;
		TamedDisplayName = _species?.DisplayName ?? "Tame";
		_huntLockedUntil = 0;
		ClearEncounter();
		_ownerMarkedTarget = null;
		_ownerThreat = null;
		LogAi( $"Tamed by {playerRoot.Name} (owner={accountKey})" );
		var gameplay = playerRoot.Components.Get<Terraingen.Player.ThornsPlayerGameplay>();
		gameplay?.HostEnsureProgressInitialized();
		Terraingen.GameData.ThornsMilestoneTracker.OnTamed( gameplay );
		gameplay?.HostPushTameFeedToOwner(
			_species?.Key ?? "",
			_species?.DisplayName ?? "Tame",
			Math.Clamp( _species?.TameTier ?? 1, 1, 4 ) );
		gameplay?.HostRebuildTamesFromWorld();
		ThornsAnimalCompanion.NotifyOwnerTamedAnimal( GameObject, accountKey );
		HostBeginCompanionOnTame( playerRoot );
		ThornsWorldPersistence.Instance?.TryHostSaveNow();
		return true;
	}

	static string ResolveTameOwnerAccountKey( GameObject playerRoot, Connection owner )
	{
		if ( !playerRoot.IsValid() )
			return "";

		playerRoot.Components.Get<ThornsPlayerSession>()?.HostEnsurePersistenceKey( owner );
		var gameplay = playerRoot.Components.Get<Terraingen.Player.ThornsPlayerGameplay>();
		gameplay?.HostEnsurePersistenceAccountKey();

		if ( gameplay.IsValid() && !string.IsNullOrEmpty( gameplay.AccountKey ) )
			return gameplay.AccountKey;

		var fromConnection = ThornsPersistenceIdentity.GetStableAccountKey( owner );
		if ( !string.IsNullOrEmpty( fromConnection ) )
			return fromConnection;

		return ThornsPersistenceIdentity.GetStableAccountKey( playerRoot );
	}

	void HostBeginCompanionOnTame( GameObject ownerRoot )
	{
		ThornsTameCommandHost.HostRegisterCommand( GameObject.Id, GameData.ThornsTameCommand.Follow );
		InvalidateTamedOwnerCache();
		Terraingen.Core.ThornsPlayerRootCache.Refresh( Scene );
		_followPausedNearOwner = false;
		_nextChaseAt = 0;
		HostApplyTameCommand( GameData.ThornsTameCommand.Follow );

		if ( ownerRoot.IsValid() )
		{
			var slot = ResolveTrailFollowSlot( ownerRoot );
			SetMotorIntent( slot );
		}
	}

	internal void HostRestoreTamedState( ThornsPersistentTameDto dto )
	{
		if ( dto is null || string.IsNullOrEmpty( dto.OwnerAccountKey ) )
			return;

		IsAwaitingTame = false;
		IsTamed = true;
		TamedOwnerAccountKey = dto.OwnerAccountKey;
		if ( ThornsAnimalSpeciesRegistry.TryGet( dto.SpeciesId, out var species ) )
			TamedDisplayName = string.IsNullOrWhiteSpace( dto.DisplayName ) ? species.DisplayName : dto.DisplayName.Trim();
		else
			TamedDisplayName = dto.DisplayName ?? "";
		_spawnHealth = dto.MaxHealth > 0f ? dto.MaxHealth : _spawnHealth;
		_spawnDamage = dto.Attack > 0f ? dto.Attack : _spawnDamage;
		_spawnSpeed = dto.MoveSpeed > 0f ? dto.MoveSpeed : _spawnSpeed;
		_breedDetectionRange = Math.Max( 0f, dto.DetectionRange );
		BreedTier = Math.Clamp( dto.BreedTier > 0 ? dto.BreedTier : _species?.TameTier ?? 1, 1, 5 );
		IsCrossbreed = dto.IsCrossbreed;
		IsMutatedBreed = dto.IsMutated;
		GeneticSpeciesIdsCsv = string.IsNullOrWhiteSpace( dto.GeneticSpeciesIdsCsv )
			? SpeciesId.ToString()
			: dto.GeneticSpeciesIdsCsv;
		GeneticTraitIdsCsv = string.IsNullOrWhiteSpace( dto.GeneticTraitIdsCsv ) && _species is not null
			? string.Join( ",", Terraingen.GameData.ThornsTameCatalog.GetTraitsForSpecies( _species.Key ).Select( t => t.Id ) )
			: dto.GeneticTraitIdsCsv ?? "";
		CurrentHealth = Math.Clamp( dto.CurrentHealth, 1f, _spawnHealth );
		HostRestoreTameProgression(
			dto.TameLevel,
			dto.TameExperience,
			dto.UnspentStatPoints,
			dto.StatStrength,
			dto.StatDefense,
			dto.StatStamina,
			dto.StatAgility,
			dto.StatIntelligence );
		_breedCooldownUntilUtcTicks = Math.Max( 0, dto.BreedCooldownUntilUtcTicks );
		_huntLockedUntil = 0;
		ClearEncounter();
		SetAiState( ThornsAnimalState.Idle, "tamed_restored" );
		_idleUntilAt = Time.Now + 1.5f;
		LogAi( $"Restored tame HP {CurrentHealth:F0}/{_spawnHealth:F0}" );
		if ( IsCrossbreed )
			HostRefreshModelVisual();
	}

	internal void HostAlertOwnerMarkedTarget( GameObject victim )
	{
		if ( !IsTamed || IsDead || !victim.IsValid() || victim == GameObject )
			return;
		if ( IsPassiveTameCommand() )
			return;

		if ( victim.Components.Get<ThornsAnimalBrain>( FindMode.EverythingInSelfAndParent ) is { } victimBrain
		     && (victimBrain.IsTamed && victimBrain.TamedOwnerAccountKey == TamedOwnerAccountKey
		         || victimBrain.IsAwaitingTame) )
			return;

		victim = ThornsBanditUtil.ResolveHostileChaseRoot( victim );
		_ownerMarkedTarget = victim;
		_ownerMarkedUntil = Time.Now + ThornsAnimalTaming.OwnerMarkSeconds;
		BeginCompanionHunt( victim );
	}

	internal void HostAlertOwnerThreat( GameObject attacker )
	{
		if ( !IsTamed || IsDead || !attacker.IsValid() || attacker == GameObject )
			return;
		if ( IsPassiveTameCommand() )
			return;

		var owner = ResolveTamedOwner();
		attacker = ThornsBanditUtil.ResolveHostileChaseRoot( attacker );
		var attackerRoot = attacker.Root;
		if ( owner.IsValid() && (attacker == owner || (attackerRoot.IsValid() && attackerRoot == owner)) )
			return;

		_ownerThreat = attacker;
		_ownerThreatUntil = Time.Now + ThornsAnimalTaming.OwnerThreatSeconds;
		BeginCompanionHunt( attacker );
	}

	void HostCompanionDetect( IReadOnlyList<ThornsAnimalBrain> animals )
	{
		if ( IsMounted )
			return;
		if ( IsPassiveTameCommand() )
			return;

		var owner = ResolveTamedOwner();
		if ( !owner.IsValid() )
			return;

		if ( Time.Now < _ownerThreatUntil && _ownerThreat.IsValid() && IsTargetStillValid( _ownerThreat ) )
		{
			if ( AiState is ThornsAnimalState.Idle or ThornsAnimalState.Wander )
				BeginCompanionHunt( _ownerThreat );

			return;
		}

		if ( Time.Now < _ownerMarkedUntil && _ownerMarkedTarget.IsValid() && IsTargetStillValid( _ownerMarkedTarget ) )
		{
			if ( AiState is ThornsAnimalState.Idle or ThornsAnimalState.Wander )
				BeginCompanionHunt( _ownerMarkedTarget );

			return;
		}

		for ( var i = 0; i < animals.Count; i++ )
		{
			var other = animals[i];
			if ( other == this || !other.IsValid() || other.IsDead || other.IsTamed )
				continue;

			if ( other.Target != owner )
				continue;

			if ( other.AiState is not ThornsAnimalState.Chase and not ThornsAnimalState.Attack )
				continue;

			BeginCompanionHunt( other.GameObject );
			return;
		}

		foreach ( var bandit in ThornsBanditPopulation.HostBrainsReadOnly )
		{
			if ( !bandit.IsValid() || bandit.IsDead )
				continue;

			var banditTarget = bandit.HostCombatTarget;
			if ( !banditTarget.IsValid() )
				continue;

			if ( banditTarget != owner && banditTarget.Root != owner )
				continue;

			if ( bandit.State is not (ThornsBanditAiState.Combat or ThornsBanditAiState.Chase or ThornsBanditAiState.Reposition) )
				continue;

			BeginCompanionHunt( bandit.GameObject );
			return;
		}

		TryCompanionAggressiveDetect( animals, owner );
	}

	void TryCompanionAggressiveDetect( IReadOnlyList<ThornsAnimalBrain> animals, GameObject owner )
	{
		if ( ThornsTameCommandHost.GetCommand( GameObject.Id ) != ThornsTameCommand.Attack )
			return;

		if ( AiState is ThornsAnimalState.Chase or ThornsAnimalState.Attack )
			return;

		var origin = GameObject.WorldPosition.WithZ( 0f );
		var detectRange = MathF.Max(
			_species?.DetectionRange ?? 0f,
			MathF.Max( ResolveEffectiveAttackRange() * 6f, 420f ) );
		var detectRangeSq = detectRange * detectRange;
		GameObject best = null;
		var bestDistSq = float.MaxValue;

		for ( var i = 0; i < animals.Count; i++ )
		{
			var other = animals[i];
			if ( other == this || !other.IsValid() || other.IsDead || other.IsTamed || other.IsAwaitingTame )
				continue;

			var delta = other.GameObject.WorldPosition.WithZ( 0f ) - origin;
			var distSq = delta.LengthSquared;
			if ( distSq > detectRangeSq || distSq >= bestDistSq )
				continue;

			best = other.GameObject;
			bestDistSq = distSq;
		}

		foreach ( var bandit in ThornsBanditPopulation.HostBrainsReadOnly )
		{
			if ( !bandit.IsValid() || bandit.IsDead )
				continue;

			var delta = bandit.GameObject.WorldPosition.WithZ( 0f ) - origin;
			var distSq = delta.LengthSquared;
			if ( distSq > detectRangeSq || distSq >= bestDistSq )
				continue;

			best = bandit.GameObject;
			bestDistSq = distSq;
		}

		if ( best.IsValid() )
			BeginCompanionHunt( best );
	}

	internal void HostStopEngaging( GameObject formerTarget )
	{
		if ( !formerTarget.IsValid() )
			return;

		if ( _ownerMarkedTarget == formerTarget )
		{
			_ownerMarkedTarget = null;
			_ownerMarkedUntil = 0;
		}

		if ( _ownerThreat == formerTarget )
		{
			_ownerThreat = null;
			_ownerThreatUntil = 0;
		}

		if ( _target != formerTarget && _retaliationTarget != formerTarget )
			return;

		ClearEncounter();
		StopMotor();

		if ( IsTamed )
		{
			SetAiState( ThornsAnimalState.Idle, "ally_tamed" );
			_idleUntilAt = Time.Now + 1.25f;
		}
		else if ( !IsDead )
		{
			EnterWander();
		}
	}

	void BeginCompanionHunt( GameObject foe )
	{
		foe = ThornsBanditUtil.ResolveHostileChaseRoot( foe );
		if ( !foe.IsValid() || IsFriendlyTameTarget( foe ) || IsAwaitingTameTarget( foe ) || !IsTargetStillValid( foe ) )
			return;

		_huntLockedUntil = 0;
		_target = foe;
		_retaliationTarget = foe;
		_huntStartPosition = GameObject.WorldPosition;
		SetAiState( ThornsAnimalState.Chase, "companion_hunt" );
		_nextChaseAt = 0;
		SetMotorIntent( ResolveChaseApproachPoint( foe ) );
	}

	void TickTamedFollowMovement()
	{
		var owner = ResolveTamedOwner();
		if ( !owner.IsValid() )
		{
			SetTamedFollowSprinting( false );
			return;
		}

		var dist = FlatDistanceTo( owner );
		if ( _followPausedNearOwner )
		{
			if ( dist > ThornsAnimalTaming.FollowResumeDistance )
				_followPausedNearOwner = false;
			else
			{
				SetTamedFollowSprinting( false );
				StopMotor();

				if ( AiState == ThornsAnimalState.Wander )
				{
					SetAiState( ThornsAnimalState.Idle, "tamed_near_owner" );
					_idleUntilAt = Time.Now + 1.5f;
				}

				FaceTarget( owner );
				return;
			}
		}

		if ( dist <= ThornsAnimalTaming.FollowOwnerDistance )
		{
			_followPausedNearOwner = true;
			SetTamedFollowSprinting( false );
			StopMotor();

			if ( AiState == ThornsAnimalState.Wander )
			{
				SetAiState( ThornsAnimalState.Idle, "tamed_near_owner" );
				_idleUntilAt = Time.Now + 1.5f;
			}

			FaceTarget( owner );
			return;
		}

		var shouldCatchUp = dist >= ThornsAnimalTaming.FollowCatchUpDistance;
		var needsDestination = !HasMoveIntent || HasReachedMoveGoal();

		if ( AiState != ThornsAnimalState.Wander )
			SetAiState( ThornsAnimalState.Wander, "tamed_follow" );

		SetTamedFollowSprinting( shouldCatchUp );

		if ( needsDestination )
		{
			_nextChaseAt = Time.Now + ( shouldCatchUp ? _species.ChaseDestinationInterval : 0.35f );
			SetMotorIntent( ResolveTrailFollowSlot( owner ) );
			return;
		}

		if ( _nextChaseAt > Time.Now )
			return;

		_nextChaseAt = Time.Now + ( shouldCatchUp ? _species.ChaseDestinationInterval : 0.35f );
		SetMotorIntent( ResolveTrailFollowSlot( owner ) );
	}

	internal bool ShouldTickTamedFollow()
	{
		return IsTamed
		       && !IsMounted
		       && ThornsTameCommandHost.GetCommand( GameObject.Id ) == ThornsTameCommand.Follow
		       && AiState is not (ThornsAnimalState.Chase or ThornsAnimalState.Attack or ThornsAnimalState.Flee);
	}

	bool IsPassiveTameCommand()
	{
		return IsTamed && ThornsTameCommandHost.GetCommand( GameObject.Id ) == ThornsTameCommand.Passive;
	}

	void SetTamedFollowSprinting( bool sprint )
	{
		if ( IsTamedFollowSprinting == sprint )
			return;

		IsTamedFollowSprinting = sprint;
		SyncAgentMoveSpeed();
	}

	GameObject ResolveTamedOwner()
	{
		if ( !IsTamed || string.IsNullOrEmpty( TamedOwnerAccountKey ) || Scene is null )
			return null;

		if ( _cachedTamedOwnerKey == TamedOwnerAccountKey && _cachedTamedOwner.IsValid() )
			return _cachedTamedOwner;

		if ( !_nextTamedOwnerLookup && _cachedTamedOwnerKey == TamedOwnerAccountKey )
			return _cachedTamedOwner;

		_nextTamedOwnerLookup = 0.35f;
		_cachedTamedOwnerKey = TamedOwnerAccountKey;
		_cachedTamedOwner = ThornsAnimalManager.TryGetPlayerByAccountKey( Scene, TamedOwnerAccountKey );
		if ( !_cachedTamedOwner.IsValid() )
			_cachedTamedOwner = TryResolveOwnerByGameplayAccountKey();
		return _cachedTamedOwner;
	}

	GameObject TryResolveOwnerByGameplayAccountKey()
	{
		if ( string.IsNullOrEmpty( TamedOwnerAccountKey ) || Scene is null || !Scene.IsValid() )
			return null;

		foreach ( var gameplay in Scene.GetAllComponents<Terraingen.Player.ThornsPlayerGameplay>() )
		{
			if ( !gameplay.IsValid() || !gameplay.GameObject.IsValid() )
				continue;

			if ( gameplay.AccountKey == TamedOwnerAccountKey )
				return gameplay.GameObject;
		}

		return null;
	}

	void HostRegisterRecentAttacker( GameObject attackerRoot )
	{
		if ( !attackerRoot.IsValid() || attackerRoot == GameObject )
			return;

		_recentAttacker = ThornsBanditUtil.ResolveHostileChaseRoot( attackerRoot );
		_recentAttackerUntilRealtime = Time.Now + 8.0;
	}

	bool TryPrioritizeSelfDefenseTarget()
	{
		if ( IsTamed || IsAwaitingTame || _species is null )
			return false;

		if ( Time.Now >= _recentAttackerUntilRealtime || !_recentAttacker.IsValid() )
			return false;

		if ( !IsTargetStillValid( _recentAttacker ) )
			return false;

		if ( AiState is ThornsAnimalState.Chase or ThornsAnimalState.Attack or ThornsAnimalState.Flee
		     && _target == _recentAttacker )
			return true;

		HostRetaliateAgainst( _recentAttacker );
		return true;
	}

	void HostRetaliateAgainst( GameObject attackerRoot )
	{
		if ( IsAwaitingTame )
			return;

		if ( !TryResolveRetaliationTarget( attackerRoot, out var attacker ) )
			return;

		var isBandit = attacker.Components.Get<ThornsBanditBrain>( FindMode.EverythingInSelfAndParent ) is { IsValid: true } b
		               && !b.IsDead;

		switch ( _species?.BehaviorType )
		{
			case ThornsAnimalBehaviorType.Predator:
				_huntLockedUntil = 0;
				BeginHunt( attacker, alertPack: !isBandit, retaliation: true );
				break;
			case ThornsAnimalBehaviorType.Mixed:
				BeginMixedEncounter( attacker );
				break;
			case ThornsAnimalBehaviorType.Prey:
				BeginFlee( attacker );
				break;
		}
	}

	bool TryResolveRetaliationTarget( GameObject attackerRoot, out GameObject attacker )
	{
		attacker = null;
		if ( !attackerRoot.IsValid() || attackerRoot == GameObject )
			return false;

		var attackerBrain = attackerRoot.Components.Get<ThornsAnimalBrain>( FindMode.EverythingInSelfAndParent );
		if ( IsLiveComponent( attackerBrain ) && attackerBrain != this && !attackerBrain.IsDead )
		{
			attacker = attackerBrain.GameObject;
			return true;
		}

		var banditBrain = attackerRoot.Components.Get<ThornsBanditBrain>( FindMode.EverythingInSelfAndParent );
		if ( banditBrain.IsValid() && !banditBrain.IsDead )
		{
			attacker = banditBrain.GameObject;
			return true;
		}

		var playerHealth = attackerRoot.Components.Get<ThornsPlayerHealth>( FindMode.EverythingInSelfAndDescendants );
		if ( IsLiveComponent( playerHealth ) && playerHealth.IsAlive )
		{
			attacker = attackerRoot;
			return true;
		}

		return false;
	}

	void HostDie( GameObject attacker )
	{
		_ = attacker;
		HostReleaseMountedRider();
		var wasTamed = IsTamed;
		IsDead = true;
		ThornsAnimalManager.NotifyAnimalDied( this );
		IsAwaitingTame = false;
		IsTamed = false;
		TamedOwnerAccountKey = "";
		CurrentHealth = 0f;
		SetAiState( ThornsAnimalState.Dead, "death" );
		_corpseDespawnDelaySeconds = ResolveCorpseDespawnDelaySeconds();
		_deathTime = 0f;
		_target = null;
		_retaliationTarget = null;
		StopMotor();
		if ( _agent.IsValid() )
			_agent.Enabled = false;

		if ( _corpse.IsValid() )
		{
			_corpse.Enabled = true;
			_corpse.CanInteract = true;
		}

		if ( wasTamed )
			ThornsWorldPersistence.Instance?.TryHostSaveNow();

		if ( ThornsMultiplayer.IsHostOrOffline )
		{
			var (items, title) = ThornsEnemyLootTables.BuildAnimalLoot( this );
			ThornsDeathCrateWorldService.Instance?.HostTrySpawnEnemyLootCrate(
				GameObject.WorldPosition,
				items,
				title );
		}
	}

	float ResolveCorpseDespawnDelaySeconds()
	{
		var renderer = Components.Get<SkinnedModelRenderer>();
		if ( !renderer.IsValid() )
			return ThornsAnimalManager.CorpseFallbackLifetimeSeconds;

		renderer.UseAnimGraph = false;
		renderer.Sequence.Name = ResolveAnimName();
		renderer.Sequence.Looping = false;

		var duration = renderer.Sequence.Duration;
		if ( duration <= 0.05f )
			return ThornsAnimalManager.CorpseFallbackLifetimeSeconds;

		return Math.Clamp(
			duration + ThornsAnimalManager.CorpseDespawnBufferSeconds,
			ThornsAnimalManager.CorpseMinLifetimeSeconds,
			ThornsAnimalManager.CorpseMaxLifetimeSeconds );
	}

	internal bool HostShouldDespawnCorpse()
	{
		return IsDead && _deathTime >= _corpseDespawnDelaySeconds;
	}

	void TryMoveToWanderPoint()
	{
		if ( !TryPickWanderPoint( out var point ) )
			return;

		SetMotorIntent( point );
	}

	bool HasReachedMoveGoal()
	{
		EnsureMotor();
		return _motor.HasReachedGoal();
	}

	internal bool TryPickWanderPoint( out Vector3 point )
	{
		point = default;
		var scene = Scene;
		if ( scene is null || !scene.IsValid() )
			return false;

		var terrain = ThornsTerrainCache.Resolve( scene );
		var config = ThornsAnimalWorldUtil.ResolveTerrainConfig( scene );

		var bodyRadius = GetBodyRadius();

		for ( var attempt = 0; attempt < 8; attempt++ )
		{
			var offset = Vector3.Random.WithZ( 0f ).Normal * Game.Random.Float( 64f, _species.WanderRadius );
			var candidate = GameObject.WorldPosition + offset;
			if ( !ThornsAnimalWorldUtil.TrySnapToTerrain( terrain, candidate, out var snapped ) )
				continue;

			if ( config is not null && ThornsAnimalWorldUtil.IsUnderSeaLevel( scene, terrain, config, snapped ) )
				continue;

			if ( ThornsAnimalWorldUtil.IsBlockedByBuildingFootprint( snapped, bodyRadius ) )
				continue;

			if ( ThornsAnimalWorldUtil.TryEstimateSlopeDegrees( terrain, snapped, out var slope )
			     && slope > _species.MaxWanderSlopeDegrees )
				continue;

			if ( ThornsAnimalWorldUtil.TryGetDryNavPoint( scene, snapped, out var nav ) )
			{
				point = nav;
				return true;
			}

			point = snapped;
			return true;
		}

		return false;
	}

	void UpdateReplicatedMoveSpeed()
	{
		if ( AiState == ThornsAnimalState.Mounted )
		{
			ReplicatedMoveSpeed = GetMoveSpeed();
			return;
		}

		if ( AiState is ThornsAnimalState.Dead or ThornsAnimalState.Idle )
		{
			ReplicatedMoveSpeed = 0f;
			return;
		}

		EnsureMotor();

		if ( HasMoveIntent
		     && MotorMode is ThornsAnimalMotorMode.DirectWalking
			     or ThornsAnimalMotorMode.Sidestepping
			     or ThornsAnimalMotorMode.Recovering )
		{
			ReplicatedMoveSpeed = _motor.LastFramePlanarSpeed;
			return;
		}

		if ( _agent.IsValid() && _agent.Enabled && _agent.IsNavigating )
			ReplicatedMoveSpeed = _agent.Velocity.WithZ( 0f ).Length;
		else
			ReplicatedMoveSpeed = 0f;
	}

	internal string DebugTargetName()
	{
		return _target.IsValid() ? _target.Name : "—";
	}

	void SetAiState( ThornsAnimalState state, string reason )
	{
		if ( AiState == state )
			return;

		AiState = state;
		if ( state is not ThornsAnimalState.Wander )
			IsTamedFollowSprinting = false;

		SyncAgentMoveSpeed();
		_ = reason;
	}

	internal bool IsRunningLocomotion =>
		AiState is ThornsAnimalState.Chase or ThornsAnimalState.Flee
		|| (AiState == ThornsAnimalState.Wander && IsTamedFollowSprinting);

	float ResolveRunningSpeedMultiplier()
	{
		return ThornsAnimalDebug.ResolveSprintMultiplier( _species );
	}

	string ResolveAnimName()
	{
		if ( _species is null )
			return "—";

		var prefix = _species.AnimPrefix;
		return AiState switch
		{
			ThornsAnimalState.Idle => $"{prefix}_idle",
			ThornsAnimalState.Wander when IsTamedFollowSprinting => $"{prefix}_run",
			ThornsAnimalState.Wander => $"{prefix}_walk",
			ThornsAnimalState.Chase => $"{prefix}_run",
			ThornsAnimalState.Flee => $"{prefix}_run",
			ThornsAnimalState.Attack => $"{prefix}_attack",
			ThornsAnimalState.Mounted => _mountedWishDir.WithZ( 0f ).Length > 0.05f ? $"{prefix}_run" : $"{prefix}_idle",
			ThornsAnimalState.Dead => $"{prefix}_death",
			_ => $"{prefix}_idle",
		};
	}

	void NotifyPlayerEncounter( GameObject player )
	{
		if ( !player.IsValid() || _species is null )
			return;

		var gameplay = player.Components.Get<ThornsPlayerGameplay>( FindMode.EverythingInSelfAndDescendants );
		if ( !IsLiveComponent( gameplay ) )
			return;

		gameplay.HostMarkDiscovery( ThornsDefinitionRegistry.DiscoveryIdForCreature( _species.Key ) );
	}

	void LogAi( string message )
	{
		if ( !ThornsAnimalDebug.BehaviorLog )
			return;

		LogAnimal( message );
	}

	void LogCombat( string message )
	{
		if ( !ThornsAnimalDebug.CombatLog && !ThornsAnimalDebug.BehaviorLog )
			return;

		LogAnimal( $"Combat: {message}" );
	}

	void LogAnimal( string message )
	{
		var name = _species?.DisplayName ?? $"species:{SpeciesId}";
		Log.Info( $"[Thorns Animals][{name}] {message}" );
	}

	public bool HostTryMount( GameObject rider )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !IsTamed || IsDead || IsMounted || !rider.IsValid() )
			return false;

		if ( !ThornsAnimalMounting.IsMountableSpecies( this ) )
			return false;

		if ( rider.WorldPosition.Distance( GameObject.WorldPosition ) > ThornsAnimalMounting.MountMaxRange * 1.15f )
			return false;

		MountedRiderId = rider.Id;
		_mountedWishDir = Vector3.Zero;
		_mountedMoveInput = Vector2.Zero;
		_mountedVerticalVelocity = 0f;
		SetMountRiderCollisionIgnored( rider, ignored: true );
		ClearEncounter();
		_ownerMarkedTarget = null;
		_ownerThreat = null;
		SetAiState( ThornsAnimalState.Mounted, "mounted" );
		StopMotor();
		LogAi( $"Mounted by {rider.Name}" );
		return true;
	}

	public bool HostTryDismount( GameObject rider )
	{
		if ( !IsMounted || !rider.IsValid() || rider.Id != MountedRiderId )
			return false;

		MountedRiderId = Guid.Empty;
		_mountedWishDir = Vector3.Zero;
		_mountedMoveInput = Vector2.Zero;
		_mountedVerticalVelocity = 0f;
		SetMountRiderCollisionIgnored( rider, ignored: false );
		SetAiState( ThornsAnimalState.Idle, "dismounted" );
		_idleUntilAt = Time.Now + 0.5f;
		LogAi( $"Dismounted by {rider.Name}" );
		return true;
	}

	public void HostApplyRiderMoveInput( GameObject rider, Vector2 moveInput, bool jumpPressed )
	{
		if ( !IsMounted || !rider.IsValid() || rider.Id != MountedRiderId )
			return;

		_mountedMoveInput = moveInput;

		if ( !jumpPressed || _mountedVerticalVelocity > 0f )
			return;

		var controller = rider.Components.Get<PlayerController>( FindMode.EverythingInSelf );
		if ( !controller.IsValid() )
			return;

		var terrain = ThornsTerrainCache.Resolve( Scene );
		if ( !TryGetMountedGroundZ( terrain, GameObject.WorldPosition, out var groundZ ) )
			return;

		if ( GameObject.WorldPosition.z <= groundZ + ThornsAnimalMounting.MountGroundStickInches )
			_mountedVerticalVelocity = ThornsAnimalMounting.MountJumpSpeed;
	}

	void TickMountedMovement()
	{
		var rider = ResolveMountedRider();
		if ( !rider.IsValid() )
		{
			HostReleaseMountedRider();
			return;
		}

		var riderHealth = rider.Components.Get<Terraingen.Combat.ThornsPlayerHealth>( FindMode.EverythingInSelf );
		if ( IsLiveComponent( riderHealth ) && !riderHealth.IsAlive )
		{
			HostReleaseMountedRider();
			return;
		}

		var terrain = ThornsTerrainCache.Resolve( Scene );
		var pos = GameObject.WorldPosition;
		var grounded = TryGetMountedGroundZ( terrain, pos, out var groundZ );
		var airborne = _mountedVerticalVelocity != 0f || (grounded && pos.z > groundZ + ThornsAnimalMounting.MountGroundStickInches + 2f );

		if ( airborne )
		{
			pos.z += _mountedVerticalVelocity * Time.Delta;
			_mountedVerticalVelocity -= ThornsAnimalMounting.MountGravity * Time.Delta;

			if ( grounded && pos.z <= groundZ )
			{
				pos.z = groundZ;
				_mountedVerticalVelocity = 0f;
				airborne = false;
			}
		}
		else if ( grounded )
		{
			pos.z = groundZ;
			_mountedVerticalVelocity = 0f;
		}

		var input = _mountedMoveInput;
		var turnInput = input.x;
		if ( MathF.Abs( turnInput ) > 0.05f )
		{
			var yaw = turnInput * ThornsAnimalMounting.MountTurnDegreesPerSecond * Time.Delta;
			GameObject.WorldRotation *= Rotation.FromYaw( yaw );
		}

		var forward = GameObject.WorldRotation.Forward.WithZ( 0f );
		if ( forward.LengthSquared < 1e-4f )
			forward = Vector3.Forward;
		else
			forward = forward.Normal;

		var moveAmount = 0f;
		if ( input.y > 0.05f )
			moveAmount = input.y;
		else if ( input.y < -0.05f )
			moveAmount = input.y;

		_mountedWishDir = forward * moveAmount;

		if ( MathF.Abs( moveAmount ) > 0.05f )
		{
			var beforeMove = pos;
			pos += forward * (moveAmount * ResolveMountedMoveSpeed( rider ) * Time.Delta);
			pos = ClampMountedPlanar( terrain, pos );

			if ( ThornsAiSolidMovementBlocker.SegmentHitsStructure(
				     Scene,
				     GameObject,
				     beforeMove,
				     pos,
				     GetBodyRadius(),
				     ThornsAnimalManager.BaseAgentHeight * MathF.Max( GameObject.WorldScale.x, 0.1f ),
				     out _ ) )
			{
				pos = beforeMove;
				_mountedWishDir = Vector3.Zero;
			}

			if ( !airborne && TryGetMountedGroundZ( terrain, pos, out groundZ ) )
				pos.z = groundZ;
		}
		else
		{
			_mountedWishDir = Vector3.Zero;
		}

		GameObject.WorldPosition = pos;
	}

	float ResolveMountedMoveSpeed( GameObject rider )
	{
		if ( rider.IsValid() )
		{
			var controller = rider.Components.Get<PlayerController>( FindMode.EverythingInSelf );
			if ( controller.IsValid() && controller.RunSpeed > 0f )
				return controller.RunSpeed * ThornsAnimalMounting.MountedPlayerSprintSpeedMultiplier;
		}

		return _spawnSpeed;
	}

	static Vector3 ClampMountedPlanar( Terrain terrain, Vector3 pos )
	{
		if ( !terrain.IsValid() )
			return pos;

		return ThornsTerrainSurface.ClampToTerrainBounds( terrain, pos );
	}

	static bool TryGetMountedGroundZ( Terrain terrain, Vector3 near, out float groundZ )
	{
		groundZ = near.z;
		if ( !ThornsAnimalWorldUtil.TrySnapToTerrain( terrain, near, out var snapped ) )
			return false;

		groundZ = snapped.z;
		return true;
	}

	void SetMountRiderCollisionIgnored( GameObject rider, bool ignored )
	{
		if ( !rider.IsValid() )
			return;

		if ( ignored )
		{
			_disabledRiderCollidersWhileMounted.Clear();
			foreach ( var collider in rider.Components.GetAll<Collider>( FindMode.EverythingInSelfAndDescendants ) )
			{
				if ( collider is null || !collider.IsValid() || !collider.Enabled )
					continue;

				collider.Enabled = false;
				_disabledRiderCollidersWhileMounted.Add( collider );
			}

			return;
		}

		foreach ( var collider in _disabledRiderCollidersWhileMounted )
		{
			if ( collider is not null && collider.IsValid() )
				collider.Enabled = true;
		}

		_disabledRiderCollidersWhileMounted.Clear();
	}

	GameObject ResolveMountedRider()
	{
		if ( !IsMounted || Scene is null )
			return null;

		if ( _cachedMountedRiderId == MountedRiderId && _cachedMountedRider.IsValid() )
			return _cachedMountedRider;

		if ( !_nextMountedRiderLookup && _cachedMountedRiderId == MountedRiderId )
			return _cachedMountedRider;

		_nextMountedRiderLookup = 0.25f;
		_cachedMountedRiderId = MountedRiderId;
		_cachedMountedRider = ThornsAnimalManager.TryGetPlayerByObjectId( Scene, MountedRiderId );
		return _cachedMountedRider;
	}

	void HostReleaseMountedRider()
	{
		if ( !IsMounted )
			return;

		var rider = ResolveMountedRider();
		MountedRiderId = Guid.Empty;
		_mountedWishDir = Vector3.Zero;
		_mountedMoveInput = Vector2.Zero;
		_mountedVerticalVelocity = 0f;
		SetMountRiderCollisionIgnored( rider, ignored: false );
		if ( !IsDead )
			SetAiState( ThornsAnimalState.Idle, "rider_released" );

		if ( rider.IsValid() )
		{
			var mount = rider.Components.Get<Terraingen.Combat.ThornsPlayerMountController>();
			if ( mount.IsValid() )
			{
				var riderHealth = rider.Components.Get<Terraingen.Combat.ThornsPlayerHealth>( FindMode.EverythingInSelf );
				if ( IsLiveComponent( riderHealth ) && !riderHealth.IsAlive )
					mount.CleanupPresentationAfterDeathOrRespawn();
				else
					mount.HostClearMountPresentation();
			}
		}
	}

	/// <summary>Host-only feed from Tames UI — restores HP up to spawn max.</summary>
	public bool HostFeed( float amount )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || IsDead || !IsTamed || amount <= 0f )
			return false;

		var max = _spawnHealth > 0f ? _spawnHealth : MaxHealth;
		if ( CurrentHealth >= max - 0.01f )
			return false;

		var before = CurrentHealth;
		CurrentHealth = Math.Min( max, CurrentHealth + amount );
		LogAi( $"Fed +{amount:F0} HP -> {before:F0}->{CurrentHealth:F0}" );
		return CurrentHealth > before;
	}

	/// <summary>Host-only tame command from Tames UI.</summary>
	public void HostSetTamedDisplayName( string displayName )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !IsTamed )
			return;

		var fallback = _species?.DisplayName ?? "Tame";
		TamedDisplayName = string.IsNullOrWhiteSpace( displayName ) ? fallback : displayName.Trim();
		if ( TamedDisplayName.Length > 32 )
			TamedDisplayName = TamedDisplayName[..32];
	}

	public void HostApplyTameCommand( GameData.ThornsTameCommand command )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !IsTamed || IsDead || IsMounted )
			return;

		switch ( command )
		{
			case GameData.ThornsTameCommand.Follow:
				_followPausedNearOwner = false;
				_nextChaseAt = 0;
				SetAiState( ThornsAnimalState.Wander, "tame_command_follow" );
				break;
			case GameData.ThornsTameCommand.Stay:
				_followPausedNearOwner = false;
				SetTamedFollowSprinting( false );
				SetAiState( ThornsAnimalState.Idle, "tame_command_stay" );
				break;
			case GameData.ThornsTameCommand.Guard:
				SetTamedFollowSprinting( false );
				SetAiState( ThornsAnimalState.Idle, "tame_command_guard" );
				break;
			case GameData.ThornsTameCommand.Passive:
				_ownerMarkedTarget = null;
				_ownerMarkedUntil = 0;
				_ownerThreat = null;
				_ownerThreatUntil = 0;
				_target = null;
				_retaliationTarget = null;
				ClearEncounter();
				SetTamedFollowSprinting( false );
				SetAiState( ThornsAnimalState.Idle, "tame_command_passive" );
				break;
			case GameData.ThornsTameCommand.Attack:
				SetTamedFollowSprinting( false );
				SetAiState( ThornsAnimalState.Idle, "tame_command_attack" );
				break;
		}
	}

	/// <summary>Host-only: teleport beside the tamed owner (~2 m).</summary>
	public void HostSummonNearOwner( int ringIndex = 0 ) =>
		ThornsTameSummonUtil.HostSummonNearOwner( this, ringIndex );

	internal void InvalidateTamedOwnerCache()
	{
		_cachedTamedOwner = null;
		_cachedTamedOwnerKey = null;
		_nextTamedOwnerLookup = 0f;
	}

	internal GameObject ResolveTamedOwnerForSummon()
	{
		if ( !IsTamed || string.IsNullOrEmpty( TamedOwnerAccountKey ) || Scene is null )
			return null;

		_cachedTamedOwnerKey = TamedOwnerAccountKey;
		_cachedTamedOwner = ThornsAnimalManager.TryGetPlayerByAccountKey( Scene, TamedOwnerAccountKey );
		return _cachedTamedOwner;
	}

	internal void ApplySummonTeleport( GameObject owner, Vector3 pos )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !owner.IsValid() )
			return;

		GameObject.WorldPosition = pos;
		LodTier = ThornsNpcLodTier.Full;

		var face = owner.WorldPosition - pos;
		face.z = 0f;
		if ( face.LengthSquared > 1f )
			GameObject.WorldRotation = Rotation.LookAt( face.Normal );

		_target = null;
		_retaliationTarget = null;
		StopMotor();
		SetTamedFollowSprinting( false );
		SetAiState( ThornsAnimalState.Idle, "tame_summon" );
		_idleUntilAt = Time.Now + 0.75f;
	}

	void HostEnsureVisualLod()
	{
		if ( Components.Get<ThornsNpcVisualLod>() is null )
			Components.Create<ThornsNpcVisualLod>();
	}
}
