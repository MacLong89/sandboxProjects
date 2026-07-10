namespace Sandbox;

using Terraingen.AI;
using Terraingen.Animals;
using Terraingen.Combat;
[Title( "Thorns NPC Footstep Audio" )]
[Category( "Thorns/Audio" )]
[Icon( "directions_walk" )]
public sealed class ThornsNpcFootstepAudio : Component
{
	[Property] public string FootstepSoundPath { get; set; } = "sounds/footsteps.sound";
	[Property] public string FootstepGrassSoundPath { get; set; } = "sounds/footsteps_grass.sound";
	[Property] public float MinHorizontalSpeed { get; set; } = 30f;
	[Property] public float WalkDistancePerStep { get; set; } = 125f;
	[Property] public float RunDistancePerStep { get; set; } = 88f;
	[Property] public float WalkMinInterval { get; set; } = 0.14f;
	[Property] public float RunMinInterval { get; set; } = 0.1f;
	[Property] public float RunSpeedThreshold { get; set; } = 95f;
	[Property] public float WalkVolume { get; set; } = 0.58f;
	[Property] public float RunVolume { get; set; } = 0.68f;
	[Property] public bool RequireGrounded { get; set; } = true;

	[Property, Group( "Mounted" )] public float MountedWalkDistancePerStep { get; set; } = 260f;
	[Property, Group( "Mounted" )] public float MountedRunDistancePerStep { get; set; } = 420f;
	[Property, Group( "Mounted" )] public float MountedWalkMinInterval { get; set; } = 0.24f;
	[Property, Group( "Mounted" )] public float MountedRunMinInterval { get; set; } = 0.2f;

	float _distanceAccum;
	double _lastStepTime;
	Vector3 _lastPos;
	bool _hasLastPos;
	ThornsAnimalBrain _animal;

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying || Application.IsDedicatedServer || Application.IsHeadless )
			return;

		// Human players use ThornsFootstepAudio — avoid double one-shots on remote pawns.
		if ( Components.Get<PlayerController>( FindMode.EverythingInSelf ).IsValid() )
			return;

		if ( IsDeadNpc() )
		{
			ResetStepState();
			return;
		}

		if ( RequireGrounded && !IsGrounded() )
		{
			ResetStepState();
			return;
		}

		var planarSpeed = ResolvePlanarSpeed();
		if ( planarSpeed < MinHorizontalSpeed )
		{
			ResetStepState();
			return;
		}

		var running = planarSpeed >= RunSpeedThreshold;
		var mounted = IsMountedAnimal();
		var baseDist = running
			? (mounted ? MountedRunDistancePerStep : RunDistancePerStep)
			: (mounted ? MountedWalkDistancePerStep : WalkDistancePerStep);
		var distPerStep = MathF.Max( mounted ? 120f : 48f, ThornsFootstepSurface.ScaleDistancePerStep( baseDist, GameObject ) );
		var minInterval = running
			? (mounted ? MountedRunMinInterval : RunMinInterval)
			: (mounted ? MountedWalkMinInterval : WalkMinInterval);

		_distanceAccum += planarSpeed * Time.Delta;
		if ( _distanceAccum < distPerStep )
			return;

		if ( minInterval > 0f && _lastStepTime > 0 && Time.Now - _lastStepTime < minInterval )
			return;

		_distanceAccum %= distPerStep;
		_lastStepTime = Time.Now;
		PlayStep( running );
	}

	bool IsDeadNpc()
	{
		var animal = Components.Get<ThornsAnimalBrain>();
		if ( animal.IsValid() )
			return animal.IsDead;

		var hp = Components.Get<ThornsBanditHealth>();
		return hp.IsValid() && ( !hp.IsAlive || hp.IsDeadState );
	}

	float ResolvePlanarSpeed()
	{
		var cc = Components.Get<CharacterController>();
		if ( cc.IsValid() )
		{
			var ccSpeed = cc.Velocity.WithZ( 0f ).Length;
			if ( ccSpeed >= MinHorizontalSpeed * 0.5f )
				return ccSpeed;
		}

		var animal = Components.Get<ThornsAnimalBrain>();
		if ( animal.IsValid() && !animal.IsDead )
		{
			var replicated = animal.ReplicatedMoveSpeed;
			if ( replicated >= MinHorizontalSpeed * 0.5f )
				return replicated;
		}

		var pos = GameObject.WorldPosition;
		if ( !_hasLastPos )
		{
			_lastPos = pos;
			_hasLastPos = true;
			return 0f;
		}

		var dt = MathF.Max( Time.Delta, 0.0001f );
		var deltaSpeed = (pos - _lastPos).WithZ( 0f ).Length / dt;
		_lastPos = pos;
		return deltaSpeed;
	}

	bool IsGrounded()
	{
		var cc = Components.Get<CharacterController>();
		if ( cc.IsValid() )
			return cc.IsOnGround;

		return IsNearTerrainOrGround();
	}

	bool IsNearTerrainOrGround()
	{
		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid )
			return true;

		var origin = GameObject.WorldPosition + Vector3.Up * 24f;
		var tr = scene.Trace.Ray( origin, origin + Vector3.Down * 72f )
			.WithoutTags( "player", "trigger" )
			.Run();

		return tr.Hit;
	}

	void PlayStep( bool running )
	{
		var path = ResolveFootstepSoundPath();
		if ( string.IsNullOrWhiteSpace( path ) )
			return;

		var volume = running ? RunVolume : WalkVolume;
		ThornsWorldSpatialSfx.PlayFollowingOnGameObject(
			GameObject,
			path,
			ThornsSpatialSfxCategory.FootstepRemote,
			volume,
			Vector3.Up * 4f );
	}

	string ResolveFootstepSoundPath()
	{
		if ( !string.IsNullOrWhiteSpace( FootstepGrassSoundPath ) && ThornsFootstepSurface.ShouldUseOutdoorGrassFootstep( GameObject ) )
			return FootstepGrassSoundPath;

		return FootstepSoundPath;
	}

	void ResetStepState()
	{
		_distanceAccum = 0f;
		_lastStepTime = 0;
	}

	bool IsMountedAnimal()
	{
		_animal ??= Components.Get<ThornsAnimalBrain>();
		return _animal.IsValid() && _animal.IsMounted;
	}
}
