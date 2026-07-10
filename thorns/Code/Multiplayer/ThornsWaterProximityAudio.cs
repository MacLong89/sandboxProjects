namespace Sandbox;

/// <summary>
/// Local owner: sea-level water ambience when the pawn is at/below the resolved water plane (swimming) or slightly above it (shore / wading).
/// Uses <see cref="ThornsTerrainSystem.TryResolveWaterPlaneWorldZ"/> — same plane as the water mesh and <see cref="ThornsPawnMovement"/> swim logic.
/// </summary>
[Title( "Thorns — Water proximity audio" )]
[Category( "Thorns" )]
[Icon( "waves" )]
[Order( 48 )]
public sealed class ThornsWaterProximityAudio : Component
{
	public const string DefaultWaterSoundPath = "sounds/water.sound";

	[Property] public string WaterSoundPath { get; set; } = DefaultWaterSoundPath;

	/// <summary>Maximum linear volume when at/below water Z (scaled by <see cref="IntensityMul"/>).</summary>
	[Property] public float MaxVolume { get; set; } = 0.42f;

	/// <summary>
	/// World units above the water plane where the bed fully fades out (player standing on dry ground uphill from the shore).
	/// </summary>
	[Property] public float ShoreFadeRangeWorldZ { get; set; } = 320f;

	/// <summary>Extra scale for swim depth (optional; keeps wading shore sound from being as loud as deep water).</summary>
	[Property] public float IntensityMul { get; set; } = 1f;

	SoundHandle _handle;

	protected override void OnDestroy()
	{
		StopInternal();
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying )
		{
			StopInternal();
			return;
		}

		if ( !ThornsPawn.IsLocalConnectionOwner( this ) )
			return;

		var health = Components.Get<ThornsHealth>();
		if ( health.IsValid() && ( health.IsDeadState || !health.IsAlive ) )
		{
			StopInternal();
			return;
		}

		if ( AmbiencePausedByModalUi() )
		{
			StopInternal();
			return;
		}

		if ( string.IsNullOrWhiteSpace( WaterSoundPath ) )
		{
			StopInternal();
			return;
		}

		if ( !ThornsTerrainSystem.TryResolveWaterPlaneWorldZ( GameObject.Scene, out var waterZ ) )
		{
			StopInternal();
			return;
		}

		var pz = GameObject.WorldPosition.z;
		var deltaAboveWater = pz - waterZ;
		var shore = MathF.Max( 8f, ShoreFadeRangeWorldZ );

		float t;
		if ( deltaAboveWater <= 0f )
			t = 1f;
		else if ( deltaAboveWater >= shore )
			t = 0f;
		else
			t = 1f - deltaAboveWater / shore;

		t = Math.Clamp( t * IntensityMul, 0f, 1f );
		var vol = MaxVolume * t;

		if ( vol < 0.02f )
		{
			StopInternal();
			return;
		}

		if ( _handle is not { IsValid: true, IsPlaying: true } )
		{
			var h = Sound.Play( WaterSoundPath.Trim(), Vector3.Zero );
			if ( !h.IsValid() )
				return;

			h.SpacialBlend = 0f;
			_handle = h;
		}

		_handle.Volume = vol;
	}

	void StopInternal()
	{
		var h = _handle;
		_handle = default;
		if ( h is { IsValid: true } )
			h.Stop( 0f );
	}

	bool AmbiencePausedByModalUi()
	{
		var shell = Components.Get<ThornsGameShell>();
		if ( shell.IsValid() && shell.Enabled && shell.BlocksGameplayShellOverlay )
			return true;

		var hud = Components.Get<ThornsDebugHudHost>();
		if ( hud.IsValid() && ( hud.ShowFullInventory || hud.ShowDebugOverlay || hud.ShowRadioShop ) )
			return true;

		return false;
	}
}
