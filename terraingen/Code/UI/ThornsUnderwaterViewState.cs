namespace Terraingen.UI;

using Terraingen.Player;
using Terraingen.TerrainGen;

/// <summary>Smoothed underwater camera tint for the local player.</summary>
public static class ThornsUnderwaterViewState
{
	const float SmoothSpeed = 7.5f;
	const float MaxOverlayOpacity = 0.44f;

	static float _blend;
	static int _revision;

	public static int Revision => _revision;

	public static float OverlayOpacity => _blend * MaxOverlayOpacity;

	public static void Reset()
	{
		_blend = 0f;
		_revision++;
	}

	public static void Tick( Scene scene, float deltaSeconds )
	{
		var target = 0f;

		if ( scene.IsValid() )
		{
			var player = ThornsPlayerGameplay.Local;
			if ( player.IsValid() && ThornsLocalPlayer.IsLocallyControlledPawn( player.GameObject ) )
			{
				if ( ThornsNaturalWaterBody.TrySample( scene, player.GameObject, out var state ) )
				{
					var eyeZ = player.GameObject.WorldPosition.z + ThornsPlayerFirstPersonRig.DefaultEyeOffsetZ;
					if ( ThornsLocalPlayer.TryGetAuthoritativeEye( player.GameObject, out var eyePos, out _ ) )
						eyeZ = eyePos.z;

					target = ThornsNaturalWaterBody.ComputeViewSubmergeBlend( state, eyeZ );
				}
			}
		}

		var step = Math.Clamp( SmoothSpeed * Math.Max( 0f, deltaSeconds ), 0f, 1f );
		var next = _blend + (target - _blend) * step;
		if ( MathF.Abs( next - _blend ) > 0.002f )
		{
			_blend = next;
			_revision++;
		}
		else if ( target <= 0.001f && _blend > 0.001f )
		{
			_blend = 0f;
			_revision++;
		}
	}
}
