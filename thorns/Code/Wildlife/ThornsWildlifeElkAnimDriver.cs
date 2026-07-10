using System.Threading.Tasks;

namespace Sandbox;

/// <summary>
/// Elk: compiled sequences on <c>models/elk/elk.vmdl</c> via <see cref="SkinnedModelRenderer.Sequence"/> (<see cref="SkinnedModelRenderer.UseAnimGraph"/> false).
/// Clips: <c>elk_idle</c>, <c>elk_walk</c>, <c>elk_run</c>, <c>elk_attack</c>, <c>elk_death</c>.
/// </summary>
[Title( "Thorns — Elk anim" )]
[Category( "Thorns/Wildlife" )]
[Icon( "animation" )]
[Order( 19 )]
public sealed class ThornsWildlifeElkAnimDriver : Component
{
	[Property] public string IdleSequenceName { get; set; } = "elk_idle";
	[Property] public string WalkSequenceName { get; set; } = "elk_walk";
	[Property] public string RunSequenceName { get; set; } = "elk_run";
	[Property] public string AttackSequenceName { get; set; } = "elk_attack";
	[Property] public string DeathSequenceName { get; set; } = "elk_death";

	[Property] public float IdleSpeedCutoff { get; set; } = 14f;
	[Property] public float WalkRunSpeedCutoff { get; set; } = 175f;
	[Property] public float AttackFallbackDurationSeconds { get; set; } = 0.55f;
	[Property] public bool LogAnimationDebug { get; set; }

	Vector3 _lastWorldPos;
	double _lastSampleTime;
	float _lastPlanarSpeed;
	string _lastLoopSequence = "";
	double _attackLockedUntilRealtime;
	int _lastMeleeStrikeSerial;
	bool _deathPlayed;
	bool _sceneReady;
	double _nextPresentationTick;

	const float ClientAnimPresentationHz = 15f;

	protected override void OnStart()
	{
		_lastWorldPos = GameObject.WorldPosition;
		_lastSampleTime = Time.Now;
		_ = TryBindSceneAndPrimeIdleAsync();
	}

	async Task TryBindSceneAndPrimeIdleAsync()
	{
		var waitUntil = Time.Now + 3f;
		while ( Time.Now < waitUntil && GameObject.IsValid() )
		{
			var skin = Components.Get<SkinnedModelRenderer>();
			if ( skin.IsValid() && skin.SceneObject is SceneModel )
			{
				skin.UseAnimGraph = false;
				PlaySequence( skin, IdleSequenceName, locomotionLoop: true );
				_lastLoopSequence = IdleSequenceName;
				_sceneReady = true;
				LogAnim(
					$"primed Sequence.Name='{IdleSequenceName}' UseAnimGraph=false seqDuration={skin.Sequence.Duration:0.###}" );
				return;
			}

			await Task.DelayRealtimeSeconds( 0.05f );
		}

		_sceneReady = true;
		LogAnim( "warning: SceneModel not ready within 3s — animations may stay idle until next clip change" );
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying )
			return;

		if ( ThornsWildlifeAnimDistanceLod.ShouldSkipClientAnimUpdate( GameObject ) )
			return;

		SamplePlanarSpeed();

		var skin = Components.Get<SkinnedModelRenderer>();
		if ( !skin.IsValid() || skin.SceneObject is not SceneModel )
			return;

		if ( !_sceneReady )
			return;

		var hp = Components.Get<ThornsHealth>();
		if ( hp.IsValid() && ( !hp.IsAlive || hp.IsDeadState ) )
		{
			if ( !_deathPlayed )
			{
				PlaySequence( skin, DeathSequenceName, locomotionLoop: false );
				LogAnim( $"death seq='{DeathSequenceName}' duration={skin.Sequence.Duration:0.###}" );
				_deathPlayed = true;
			}

			return;
		}

		var sync = Components.Get<ThornsWildlifeAnimSync>();
		if ( !sync.IsValid() )
			return;

		if ( ThornsWildlifeMotor.IsInTamingStun( GameObject ) )
		{
			TryPlayLocomotionLoop( skin, IdleSequenceName, "taming_stun" );
			return;
		}

		if ( Time.Now < _nextPresentationTick )
			return;

		_nextPresentationTick = Time.Now + (1f / ClientAnimPresentationHz);

		var ai = (ThornsWildlifeAiState)sync.AiStateOrdinal;
		_ = ai;
		var effectiveSpeed = ThornsWildlifeLocomotionAnimUtil.SampleEffectivePlanarSpeed( GameObject, _lastPlanarSpeed );
		_ = effectiveSpeed;

		if ( sync.MeleeStrikeSerial != _lastMeleeStrikeSerial )
		{
			_lastMeleeStrikeSerial = sync.MeleeStrikeSerial;
			if ( Time.Now - sync.LastMeleeStrikeTime <= Math.Max( 0.2f, AttackFallbackDurationSeconds ) )
			{
				PlaySequence( skin, AttackSequenceName, locomotionLoop: false );
				var d = skin.Sequence.Duration;
				var len = d > 0.05f ? d : AttackFallbackDurationSeconds;
				_attackLockedUntilRealtime = Time.Now + len;
				_lastLoopSequence = "";
				LogAnim(
					$"attack event={sync.MeleeStrikeSerial} ai={ai} seq='{AttackSequenceName}' seqDuration={d:0.###} lockSeconds={len:0.###}" );
				return;
			}
		}

		if ( Time.Now < _attackLockedUntilRealtime )
			return;

		ThornsAnimalLocomotionAnim locomotionAnim;
		if ( Enum.IsDefined( typeof(ThornsAnimalLocomotionAnim), sync.LocomotionAnimOrdinal ) )
			locomotionAnim = (ThornsAnimalLocomotionAnim)sync.LocomotionAnimOrdinal;
		else
		{
			locomotionAnim = ThornsWildlifeLocomotionAnimSelector.ResolveForAiState(
				ai,
				ThornsWildlifeLocomotionAnimUtil.SampleEffectivePlanarSpeed( GameObject, _lastPlanarSpeed ),
				isDead: false,
				IdleSpeedCutoff,
				WalkRunSpeedCutoff );
		}

		var sequence = ThornsWildlifeLocomotionAnimSelector.PickSequenceName(
			locomotionAnim,
			IdleSequenceName,
			WalkSequenceName,
			RunSequenceName,
			AttackSequenceName,
			DeathSequenceName );

		TryPlayLocomotionLoop( skin, sequence, locomotionAnim.ToString() );
	}

	void TryPlayLocomotionLoop( SkinnedModelRenderer skin, string sequenceName, string aiLabel )
	{
		if ( sequenceName == _lastLoopSequence )
			return;

		var prev = _lastLoopSequence;
		_lastLoopSequence = sequenceName;
		PlaySequence( skin, sequenceName, locomotionLoop: true );
		var effective = ThornsWildlifeLocomotionAnimUtil.SampleEffectivePlanarSpeed( GameObject, _lastPlanarSpeed );
		LogAnim(
			$"locomotion ai={aiLabel} seq '{prev}' -> '{sequenceName}' effectiveSpeed={effective:0.#} (idle<{IdleSpeedCutoff} run≥{WalkRunSpeedCutoff}) seqDuration={skin.Sequence.Duration:0.###}" );
	}

	static void PlaySequence( SkinnedModelRenderer skin, string sequenceName, bool locomotionLoop )
	{
		skin.UseAnimGraph = false;
		skin.Sequence.Name = sequenceName;
		if ( locomotionLoop )
			skin.Sequence.Looping = true;
	}

	void LogAnim( string message )
	{
		if ( !LogAnimationDebug )
			return;

		Log.Info( $"[Thorns][ElkAnim] {GameObject.Name}: {message}" );
	}

	void SamplePlanarSpeed()
	{
		var now = Time.Now;
		var p = GameObject.WorldPosition;
		var dt = now - _lastSampleTime;
		if ( dt < 1e-4d )
			return;

		dt = Math.Clamp( dt, 0.001, 0.12 );

		var delta = (p - _lastWorldPos).WithZ( 0 );
		if ( delta.LengthSquared > 4000000f )
		{
			_lastWorldPos = p;
			_lastSampleTime = now;
			_lastPlanarSpeed = 0f;
			return;
		}

		_lastPlanarSpeed = (float)(delta.Length / dt);
		_lastWorldPos = p;
		_lastSampleTime = now;
	}
}
