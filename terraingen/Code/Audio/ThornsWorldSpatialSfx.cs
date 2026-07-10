namespace Sandbox;

using Terraingen;
using Terraingen.Player;

public enum ThornsSpatialSfxCategory
{
	PlayerGunshot,
	NpcGunshot,
	FootstepRemote,
	WildlifeVocal,
	WaterAmbience,
	PlayerReload,
	PlayerMelee,
	PlayerToolStrike,
	PlayerInteraction,
	PlayerBuild
}

/// <summary>Shared local-listener falloff for world-position one-shots.</summary>
public static class ThornsWorldSpatialSfx
{
	public static bool TryGetLocalListenerEar( out Vector3 earWorld )
	{
		earWorld = default;

		var scene = Game.ActiveScene;
		if ( scene is not null && scene.IsValid )
		{
			var player = ThornsSceneObserver.FindLocalPlayerObject( scene );
			if ( player.IsValid() )
			{
				if ( ThornsLocalPlayer.TryGetAuthoritativeEye( player, out earWorld, out _ ) )
					return true;

				earWorld = player.WorldPosition;
				return true;
			}

			if ( scene.Camera.IsValid() )
			{
				earWorld = scene.Camera.WorldPosition;
				return true;
			}
		}

		return false;
	}

	public static float DistanceVolumeMultiplier( Vector3 worldEmit, ThornsSpatialSfxCategory category )
	{
		if ( !TryGetLocalListenerEar( out var ear ) )
			return 1f;

		var dist = Vector3.DistanceBetween( worldEmit, ear );
		CategoryCurve( category, out var minFull, out var silentBeyond );
		return ComputeDistanceFalloff( dist, minFull, silentBeyond );
	}

	/// <summary>Smooth 1→0 falloff between <paramref name="minFullDistance"/> and <paramref name="silentBeyondDistance"/>.</summary>
	public static float ComputeDistanceFalloff( float distance, float minFullDistance, float silentBeyondDistance ) =>
		SmoothInverseLerp( distance, minFullDistance, silentBeyondDistance );

	public static SoundHandle PlayWorldOneShot(
		string resourcePath,
		Vector3 worldEmit,
		ThornsSpatialSfxCategory category,
		float baseVolume = 1f )
	{
		if ( string.IsNullOrWhiteSpace( resourcePath ) || Application.IsDedicatedServer || Application.IsHeadless )
			return default;

		var volume = Math.Clamp( baseVolume, 0f, 4f ) * DistanceVolumeMultiplier( worldEmit, category );
		if ( volume <= 0.001f )
			return default;

		var h = Sound.Play( resourcePath.Trim(), worldEmit );
		if ( h.IsValid() )
		{
			h.SpacialBlend = 1f;
			h.Volume = volume;
		}

		return h;
	}

	/// <summary>One-shot attached to a moving source (recommended for footsteps, gunfire, vocals).</summary>
	public static SoundHandle PlayFollowingOnGameObject(
		GameObject source,
		string resourcePath,
		ThornsSpatialSfxCategory category,
		float baseVolume = 1f,
		Vector3 localOffset = default,
		float localOwnerSpacialBlend = 1f )
	{
		if ( source is null || !source.IsValid() || string.IsNullOrWhiteSpace( resourcePath )
		     || Application.IsDedicatedServer || Application.IsHeadless )
			return default;

		var emit = WorldPointFromLocalOffset( source, localOffset );
		var localOwner = ThornsLocalPlayer.IsLocalConnectionPlayerRoot( source );
		var volume = Math.Clamp( baseVolume, 0f, 4f );
		if ( !localOwner )
			volume *= DistanceVolumeMultiplier( emit, category );

		if ( volume <= 0.001f )
			return default;

		var h = Sound.Play( resourcePath.Trim(), emit );
		if ( !h.IsValid() )
			return default;

		h.Parent = source;
		h.FollowParent = true;
		h.SpacialBlend = localOwner ? Math.Clamp( localOwnerSpacialBlend, 0f, 1f ) : 1f;
		h.Volume = volume;
		return h;
	}

	public static Vector3 WorldPointFromLocalOffset( GameObject source, Vector3 localOffset ) =>
		localOffset == default
			? source.WorldPosition
			: source.WorldTransform.PointToWorld( localOffset );

	static void CategoryCurve( ThornsSpatialSfxCategory category, out float minFullDistance, out float silentBeyondDistance )
	{
		switch ( category )
		{
			case ThornsSpatialSfxCategory.PlayerGunshot:
				minFullDistance = 120f;
				silentBeyondDistance = 7200f;
				break;
			case ThornsSpatialSfxCategory.NpcGunshot:
				minFullDistance = 120f;
				silentBeyondDistance = 6400f;
				break;
			case ThornsSpatialSfxCategory.FootstepRemote:
				minFullDistance = 32f;
				silentBeyondDistance = 2600f;
				break;
			case ThornsSpatialSfxCategory.WildlifeVocal:
				minFullDistance = 90f;
				silentBeyondDistance = 5200f;
				break;
			case ThornsSpatialSfxCategory.WaterAmbience:
				minFullDistance = 100f;
				silentBeyondDistance = 2400f;
				break;
			case ThornsSpatialSfxCategory.PlayerReload:
				minFullDistance = 64f;
				silentBeyondDistance = 3600f;
				break;
			case ThornsSpatialSfxCategory.PlayerMelee:
			case ThornsSpatialSfxCategory.PlayerToolStrike:
				minFullDistance = 48f;
				silentBeyondDistance = 2200f;
				break;
			case ThornsSpatialSfxCategory.PlayerInteraction:
			case ThornsSpatialSfxCategory.PlayerBuild:
				minFullDistance = 56f;
				silentBeyondDistance = 2800f;
				break;
			default:
				minFullDistance = 64f;
				silentBeyondDistance = 4800f;
				break;
		}
	}

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
