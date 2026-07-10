namespace Sandbox;

[Title( "Thorns World Ambience" )]
[Category( "Thorns/Audio" )]
[Icon( "graphic_eq" )]
public sealed class ThornsWorldAmbience : Component
{
	[Property] public string AmbienceSoundPath { get; set; } = "sounds/ambience.sound";
	[Property] public float Volume { get; set; } = 0.5f;

	public float RuntimeVolumeMultiplier { get; set; } = 1f;

	SoundHandle _handle;

	protected override void OnDestroy()
	{
		StopInternal();
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying || Application.IsDedicatedServer || Application.IsHeadless || string.IsNullOrWhiteSpace( AmbienceSoundPath ) )
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
