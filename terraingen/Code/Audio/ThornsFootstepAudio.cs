namespace Sandbox;

using Terraingen.Animals;
using Terraingen.Combat;
using Terraingen.Player;

[Title( "Thorns Footstep Audio" )]
[Category( "Thorns/Audio" )]
[Icon( "directions_walk" )]
public sealed class ThornsFootstepAudio : Component
{
	[Property] public string FootstepSoundPath { get; set; } = "sounds/footsteps.sound";
	[Property] public string FootstepGrassSoundPath { get; set; } = "sounds/footsteps_grass.sound";
	[Property] public float MinHorizontalSpeed { get; set; } = 38f;
	[Property] public float DistancePerStep { get; set; } = 125f;
	[Property] public float MinInterval { get; set; } = 0.14f;
	[Property] public float Volume { get; set; } = 0.55f;
	[Property] public float LocalOwnerSpacialBlend { get; set; } = 0.22f;

	PlayerController _controller;
	ThornsPlayerMountController _mount;
	float _distanceAccum;
	double _lastStepTime;
	SoundHandle _activeFootstep;

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying || Application.IsDedicatedServer || Application.IsHeadless )
			return;

		_controller ??= Components.Get<PlayerController>( FindMode.EverythingInSelf );
		if ( !_controller.IsValid() )
			return;

		_mount ??= Components.Get<ThornsPlayerMountController>( FindMode.EverythingInSelf );
		if ( _mount.IsValid() && _mount.IsMounted )
		{
			ResetStepState();
			return;
		}

		// Parented to a mount before mount sync replicates — avoid doubling with mount footsteps.
		if ( GameObject.Parent.IsValid()
		     && GameObject.Parent.Components.Get<ThornsAnimalBrain>( FindMode.EverythingInSelf ).IsValid() )
		{
			ResetStepState();
			return;
		}

		if ( _controller.IsSwimming || ThornsNaturalWaterBody.IsSwimming( Scene, GameObject ) )
		{
			ResetStepState();
			return;
		}

		if ( !IsGroundedForFootsteps() )
		{
			ResetStepState();
			return;
		}

		var velocity = _controller.Velocity;
		var horizontalSpeed = velocity.WithZ( 0f ).Length;
		if ( horizontalSpeed < MinHorizontalSpeed )
		{
			ResetStepState();
			return;
		}

		var dist = MathF.Max( 1f, ThornsFootstepSurface.ScaleDistancePerStep( DistancePerStep, GameObject ) );
		_distanceAccum += horizontalSpeed * Time.Delta;
		if ( _distanceAccum < dist )
			return;

		if ( MinInterval > 0f && _lastStepTime > 0 && Time.Now - _lastStepTime < MinInterval )
			return;

		_distanceAccum %= dist;
		_lastStepTime = Time.Now;
		PlayStep();
	}

	void PlayStep()
	{
		var path = ResolveFootstepSoundPath();
		if ( string.IsNullOrWhiteSpace( path ) )
			return;

		StopActiveFootstep( 0f );
		_activeFootstep = ThornsWorldSpatialSfx.PlayFollowingOnGameObject(
			GameObject,
			path.Trim(),
			ThornsSpatialSfxCategory.FootstepRemote,
			Volume,
			Vector3.Up * 4f,
			LocalOwnerSpacialBlend );
	}

	void ResetStepState()
	{
		StopActiveFootstep( 0.07f );
		_distanceAccum = 0f;
		_lastStepTime = 0;
	}

	bool IsGroundedForFootsteps()
	{
		var cc = Components.Get<CharacterController>( FindMode.EverythingInSelf );
		return !cc.IsValid() || cc.IsOnGround;
	}

	void StopActiveFootstep( float fadeSeconds )
	{
		var h = _activeFootstep;
		_activeFootstep = default;
		if ( !h.IsValid() )
			return;

		h.Stop( Math.Clamp( fadeSeconds, 0f, 0.35f ) );
	}

	string ResolveFootstepSoundPath()
	{
		if ( !string.IsNullOrWhiteSpace( FootstepGrassSoundPath ) && ThornsFootstepSurface.ShouldUseOutdoorGrassFootstep( GameObject ) )
			return FootstepGrassSoundPath;

		return FootstepSoundPath;
	}
}
