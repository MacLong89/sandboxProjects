using System;

namespace Sandbox;

/// <summary>Categories for distance falloff tuning of world-emitted one-shots (gunfire, footsteps, vocals).</summary>
public enum ThornsSpatialSfxCategory
{
	PlayerGunshot,
	NpcGunshot,
	FootstepRemote,
	WildlifeVocal
}

/// <summary>
/// World-position gameplay audio heard by the local player: full 3D panning (<see cref="SoundHandle.SpacialBlend"/> = 1)
/// and manual volume falloff from the local listener (pawn eye, or nothing if unavailable).
/// </summary>
public static class ThornsWorldSpatialSfx
{
	/// <summary>Hard cull for distant gunshot RPCs — planar distance from local listener (pawn eye or camera).</summary>
	public static bool LocalListenerWithinPlanarRadius( Vector3 worldEmit, float maxRadius )
	{
		if ( maxRadius <= 0f )
			return true;

		if ( !TryGetLocalListenerEar( out var ear ) )
			return true;

		var dx = worldEmit.x - ear.x;
		var dy = worldEmit.y - ear.y;
		return dx * dx + dy * dy <= maxRadius * maxRadius;
	}

	public static bool TryGetLocalListenerEar( out Vector3 earWorld )
	{
		earWorld = default;
		if ( ThornsPawn.Local is { IsValid: true } lp )
		{
			if ( ThornsCombatAuthority.TryGetAuthoritativeEye( lp.GameObject, out earWorld, out _ ) )
				return true;

			earWorld = lp.GameObject.WorldPosition;
			return true;
		}

		// Pawn not registered yet (join frame), spectator tooling, or menu — still pan/attenuate world SFX from the active view.
		var scene = Game.ActiveScene;
		if ( scene is not null && scene.IsValid() && scene.Camera.IsValid() )
		{
			earWorld = scene.Camera.WorldPosition;
			return true;
		}

		return false;
	}

	public static float DistanceVolumeMultiplier( Vector3 worldEmit, ThornsSpatialSfxCategory category )
	{
		if ( !TryGetLocalListenerEar( out var ear ) )
			return 1f;

		var dist = Vector3.DistanceBetween( worldEmit, ear );
		CategoryCurve( category, out var minFull, out var silentBeyond );
		return SmoothInverseLerp( dist, minFull, silentBeyond );
	}

	/// <summary>World point → offset from <paramref name="followRoot"/> (for <see cref="GameObject.PlaySound"/>).</summary>
	public static Vector3 WorldEmitToLocalOffset( GameObject followRoot, Vector3 worldEmit )
	{
		if ( !followRoot.IsValid() )
			return worldEmit;

		return followRoot.WorldRotation.Inverse * (worldEmit - followRoot.WorldPosition);
	}

	public static Vector3 LocalOffsetToWorldEmit( GameObject followRoot, Vector3 localOffset )
	{
		if ( !followRoot.IsValid() )
			return localOffset;

		return followRoot.WorldPosition + followRoot.WorldRotation * localOffset;
	}

	static float VolumeAtWorldEmit( Vector3 worldEmit, ThornsSpatialSfxCategory category, float baseVolume )
	{
		var atten = DistanceVolumeMultiplier( worldEmit, category );
		return Math.Clamp( baseVolume, 0f, 4f ) * atten;
	}

	/// <summary>Static world point — props, impacts, one-shots with no moving source.</summary>
	public static SoundHandle PlayWorldOneShot(
		string resourcePath,
		Vector3 worldEmit,
		ThornsSpatialSfxCategory category,
		float baseVolume = 1f )
	{
		if ( string.IsNullOrWhiteSpace( resourcePath ) )
			return default;

		if ( !TryGetLocalListenerEar( out _ ) )
			return default;

		var vol = VolumeAtWorldEmit( worldEmit, category, baseVolume );
		if ( vol <= 0.001f )
			return default;

		var h = Sound.Play( resourcePath.Trim(), worldEmit );
		if ( !h.IsValid )
			return h;

		h.SpacialBlend = 1f;
		h.Volume = vol;
		return h;
	}

	/// <summary>
	/// Moving source — <see cref="Sound.Play"/> at emit point, then <see cref="BindFollowingEmitter"/> so panning tracks the root while the clip plays.
	/// </summary>
	public static SoundHandle PlayWorldOneShotFollowing(
		GameObject followRoot,
		Vector3 localOffset,
		string resourcePath,
		ThornsSpatialSfxCategory category,
		float baseVolume = 1f )
	{
		if ( string.IsNullOrWhiteSpace( resourcePath ) || !followRoot.IsValid() )
			return default;

		if ( !TryGetLocalListenerEar( out _ ) )
			return default;

		var worldEmit = LocalOffsetToWorldEmit( followRoot, localOffset );
		var vol = VolumeAtWorldEmit( worldEmit, category, baseVolume );
		if ( vol <= 0.001f )
			return default;

		var h = Sound.Play( resourcePath.Trim(), worldEmit );
		if ( !h.IsValid )
			return h;

		BindFollowingEmitter( h, followRoot, localOffset );
		h.SpacialBlend = 1f;
		h.Volume = vol;
		return h;
	}

	/// <summary>When a clip was started at a fixed point, re-parent so it tracks a moving <paramref name="followRoot"/>.</summary>
	public static void BindFollowingEmitter(
		SoundHandle handle,
		GameObject followRoot,
		Vector3 localOffset )
	{
		if ( !handle.IsValid || !followRoot.IsValid() )
			return;

		handle.Parent = followRoot;
		handle.FollowParent = true;
		handle.Position = LocalOffsetToWorldEmit( followRoot, localOffset );
	}

	static void CategoryCurve( ThornsSpatialSfxCategory category, out float minFullDistance, out float silentBeyondDistance )
	{
		switch ( category )
		{
			case ThornsSpatialSfxCategory.PlayerGunshot:
				minFullDistance = 90f;
				silentBeyondDistance = 4800f;
				break;
			case ThornsSpatialSfxCategory.NpcGunshot:
				minFullDistance = 90f;
				silentBeyondDistance = 4200f;
				break;
			case ThornsSpatialSfxCategory.FootstepRemote:
				minFullDistance = 24f;
				silentBeyondDistance = 1500f;
				break;
			case ThornsSpatialSfxCategory.WildlifeVocal:
				minFullDistance = 70f;
				silentBeyondDistance = 3400f;
				break;
			default:
				minFullDistance = 50f;
				silentBeyondDistance = 3000f;
				break;
		}
	}

	/// <summary>1 inside <paramref name="minFull"/>, 0 at/after <paramref name="silentBeyond"/>, smoothstep between.</summary>
	static float SmoothInverseLerp( float distance, float minFull, float silentBeyond )
	{
		if ( silentBeyond <= minFull )
			return distance <= minFull ? 1f : 0f;

		if ( distance <= minFull )
			return 1f;
		if ( distance >= silentBeyond )
			return 0f;

		var t = (distance - minFull) / (silentBeyond - minFull);
		t = t * t * (3f - 2f * t);
		return 1f - t;
	}
}
