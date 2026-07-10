namespace Sandbox;

/// <summary>
/// Non-spatial ambience bed — restarts when the clip ends (engine has no gapless loop on <see cref="SoundHandle"/>).
/// </summary>
[Title( "Thorns — World ambience" )]
[Category( "Thorns/World" )]
[Icon( "graphic_eq" )]
public sealed class ThornsWorldAmbience : Component
{
	[Property] public string AmbienceSoundPath { get; set; } = "sounds/ambience.sound";

	// Linear volume multiplier (0.5 = 50%) — inspector text from Property codegen (avoid /// on [Property]: SB2000).
	[Property] public float Volume { get; set; } = 0.5f;

	/// <summary>Runtime ducking (e.g. atmospheric music) — multiplied with <see cref="Volume"/>.</summary>
	public float RuntimeVolumeMultiplier { get; set; } = 1f;

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

		if ( string.IsNullOrWhiteSpace( AmbienceSoundPath ) )
		{
			StopInternal();
			return;
		}

		if ( _handle is { IsValid: true, IsPlaying: true } )
		{
			_handle.Volume = Volume * MathF.Max( 0f, RuntimeVolumeMultiplier );
			return;
		}

		var h = Sound.Play( AmbienceSoundPath.Trim(), Vector3.Zero );
		if ( !h.IsValid() )
			return;

		h.Volume = Volume * MathF.Max( 0f, RuntimeVolumeMultiplier );
		h.SpacialBlend = 0f;
		_handle = h;
	}

	void StopInternal()
	{
		var h = _handle;
		_handle = default;
		if ( h is { IsValid: true } )
			h.Stop( 0f );
	}
}
