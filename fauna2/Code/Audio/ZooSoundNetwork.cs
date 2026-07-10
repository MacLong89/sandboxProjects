namespace Fauna2;

/// <summary>Networked SFX broadcast — lives on ZooCore so Rpc reaches every client.</summary>
public sealed class ZooSoundNetwork : Component
{
	public static ZooSoundNetwork Instance { get; private set; }

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
	}

	public static void PlayForAll( string stem, float volume = 0.5f )
	{
		ZooSoundEffects.Play2D( stem, volume );

		if ( !Networking.IsActive || !Networking.IsHost )
			return;

		var network = Instance;
		if ( !network.IsValid() )
			return;

		network.BroadcastSfx2D( stem, volume );
	}

	public static void PlayPlaceForAll() => PlayForAll( "place", ZooSoundEffects.PlaceVolume );

	public static void Play3DForAll( string stem, Vector3 position, float volume = 0.5f, float maxDistance = 6000f )
	{
		ZooSoundEffects.Play3D( stem, position, volume, maxDistance );

		if ( !Networking.IsActive || !Networking.IsHost )
			return;

		var network = Instance;
		if ( !network.IsValid() )
			return;

		network.BroadcastSfx3D( stem, position, volume, maxDistance );
	}

	[Rpc.Broadcast]
	private void BroadcastSfx2D( string stem, float volume )
	{
		if ( Networking.IsHost ) return;
		ZooSoundEffects.Play2D( stem, volume );
	}

	[Rpc.Broadcast]
	private void BroadcastSfx3D( string stem, Vector3 position, float volume, float maxDistance )
	{
		if ( Networking.IsHost ) return;
		ZooSoundEffects.Play3D( stem, position, volume, maxDistance );
	}
}
