namespace Sandbox;

using Terraingen.Combat;
using Terraingen.Player;
using Terraingen.TerrainGen;

/// <summary>Local owner: shoreline/wading water ambience while near the world water sheet.</summary>
[Title( "Thorns Player Water Proximity Audio" )]
[Category( "Thorns/Audio" )]
[Icon( "waves" )]
public sealed class ThornsPlayerWaterProximityAudio : Component
{
	public const string DefaultWaterSoundPath = "sounds/water.sound";

	[Property] public string WaterSoundPath { get; set; } = DefaultWaterSoundPath;
	[Property] public float MaxVolume { get; set; } = 0.42f;

	SoundHandle _handle;

	protected override void OnDestroy()
	{
		StopInternal();
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying || Application.IsDedicatedServer || Application.IsHeadless )
		{
			StopInternal();
			return;
		}

		if ( !ThornsLocalPlayer.IsLocalConnectionOwner( this ) )
			return;

		var health = Components.Get<ThornsPlayerHealth>();
		if ( health is not null && health.IsValid() && ( health.IsDeadState || !health.IsAlive ) )
		{
			StopInternal();
			return;
		}

		if ( string.IsNullOrWhiteSpace( WaterSoundPath ) )
		{
			StopInternal();
			return;
		}

		if ( !ThornsNaturalWaterDrink.TryGetWaterAmbienceState( Scene, GameObject, out _, out var blend ) )
		{
			StopInternal();
			return;
		}

		var vol = MaxVolume * blend;
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
}
