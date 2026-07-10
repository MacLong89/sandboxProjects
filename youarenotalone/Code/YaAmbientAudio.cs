namespace Sandbox;

/// <summary>Looping background ambience (2D / UI sound event). Clip should be set to loop on import; default volume is half the previous level (~25% of full).</summary>
[Title( "YouAreNotAlone — Ambient audio" )]
[Category( "YouAreNotAlone" )]
[Icon( "graphic_eq" )]
[Order( 5 )]
public sealed class YaAmbientAudio : Component
{
	/// <summary>Sound event resource path (e.g. under <c>Assets/sounds/</c>).</summary>
	[Property] public string AmbientSoundPath { get; set; } = "sounds/ambient_sound.sound";

	/// <summary>Playback loudness multiplier (0.25 after turning down 50% from the prior 0.5 default).</summary>
	[Property] public float Volume { get; set; } = 0.25f;

	SoundHandle _ambient;

	protected override void OnStart()
	{
		if ( Scene.IsEditor || !Game.IsPlaying )
			return;
		if ( string.IsNullOrWhiteSpace( AmbientSoundPath ) )
			return;

		var h = Sound.Play( AmbientSoundPath.Trim() );
		if ( h is { IsValid: true } sh )
		{
			sh.Volume = Math.Clamp( Volume, 0f, 2f );
			_ambient = sh;
		}
	}

	protected override void OnDestroy()
	{
		var a = _ambient;
		_ambient = null;
		if ( a is { IsValid: true, IsPlaying: true } )
			a.Stop( 0f );
	}
}
