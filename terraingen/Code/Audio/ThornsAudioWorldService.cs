namespace Terraingen.Audio;

using Sandbox.Network;
using Terraingen.Animals;
using Terraingen.Core;
using Terraingen.Multiplayer;
using Terraingen.Player;

// Host-originated spatial SFX delivery for all peers (interest-filtered broadcast).
[Title( "Thorns Audio World" )]
[Category( "Thorns/Audio" )]
public sealed class ThornsAudioWorldService : Component
{
	public static ThornsAudioWorldService Instance { get; private set; }

	protected override void OnStart() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}

	public static void EnsureForScene( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return;

		if ( scene.GetAllComponents<ThornsAudioWorldService>().FirstOrDefault() is { } existing )
		{
			Instance ??= existing;
			return;
		}

		var go = scene.CreateObject();
		go.Name = "ThornsAudioWorld";
		var service = go.Components.Create<ThornsAudioWorldService>();
		Instance = service;
	}

	public static void BroadcastWorldOneShot(
		string resourcePath,
		Vector3 worldEmit,
		ThornsSpatialSfxCategory category,
		float baseVolume = 1f )
	{
		if ( string.IsNullOrWhiteSpace( resourcePath ) || Application.IsDedicatedServer || Application.IsHeadless )
			return;

		if ( !Networking.IsActive )
		{
			ThornsWorldSpatialSfx.PlayWorldOneShot( resourcePath, worldEmit, category, baseVolume );
			return;
		}

		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		var inst = ResolveInstance();
		if ( inst is null )
			return;

		var path = resourcePath.Trim();
		ThornsNetInterest.HostBroadcastNear(
			worldEmit,
			() => inst.RpcWorldOneShot( path, worldEmit, (int)category, baseVolume ) );
	}

	public static void BroadcastFollowing(
		Guid sourceObjectId,
		string resourcePath,
		Vector3 localOffset,
		ThornsSpatialSfxCategory category,
		float baseVolume,
		Vector3 interestPoint,
		float localOwnerSpacialBlend = 1f )
	{
		if ( sourceObjectId == Guid.Empty || string.IsNullOrWhiteSpace( resourcePath )
		     || Application.IsDedicatedServer || Application.IsHeadless )
			return;

		var path = resourcePath.Trim();

		if ( !Networking.IsActive )
		{
			var source = TryResolveSource( sourceObjectId );
			if ( source.IsValid() )
			{
				ThornsWorldSpatialSfx.PlayFollowingOnGameObject(
					source,
					path,
					category,
					baseVolume,
					localOffset,
					localOwnerSpacialBlend );
			}

			return;
		}

		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		var inst = ResolveInstance();
		if ( inst is null )
			return;

		ThornsNetInterest.HostBroadcastNear(
			interestPoint,
			() => inst.RpcFollowing(
				sourceObjectId,
				path,
				localOffset,
				(int)category,
				baseVolume,
				localOwnerSpacialBlend ) );
	}

	[Rpc.Broadcast]
	void RpcWorldOneShot( string resourcePath, Vector3 worldEmit, int category, float baseVolume )
	{
		if ( ThornsNetAuthority.RejectClientBroadcastOrigin() )
			return;

		ThornsWorldSpatialSfx.PlayWorldOneShot(
			resourcePath,
			worldEmit,
			(ThornsSpatialSfxCategory)category,
			baseVolume );
	}

	[Rpc.Broadcast]
	void RpcFollowing(
		Guid sourceObjectId,
		string resourcePath,
		Vector3 localOffset,
		int category,
		float baseVolume,
		float localOwnerSpacialBlend )
	{
		if ( ThornsNetAuthority.RejectClientBroadcastOrigin() )
			return;

		var source = TryResolveSource( sourceObjectId );
		if ( !source.IsValid() )
			return;

		ThornsWorldSpatialSfx.PlayFollowingOnGameObject(
			source,
			resourcePath,
			(ThornsSpatialSfxCategory)category,
			baseVolume,
			localOffset,
			localOwnerSpacialBlend );
	}

	static ThornsAudioWorldService ResolveInstance()
	{
		var inst = Instance;
		if ( inst is not null && inst.IsValid() )
			return inst;

		var scene = Game.ActiveScene;
		if ( scene is null || !scene.IsValid() )
			return null;

		EnsureForScene( scene );
		inst = Instance;
		return inst is not null && inst.IsValid() ? inst : null;
	}

	static GameObject TryResolveSource( Guid sourceObjectId )
	{
		if ( sourceObjectId == Guid.Empty )
			return null;

		var scene = Game.ActiveScene;
		if ( scene is null || !scene.IsValid() )
			return null;

		var player = ThornsPlayerRootCache.TryGetByObjectId( scene, sourceObjectId );
		if ( player.IsValid() )
			return player;

		var animal = ThornsAnimalManager.TryGetByObjectId( sourceObjectId );
		if ( animal.IsValid() )
			return animal.GameObject;

		foreach ( var obj in scene.GetAllObjects( true ) )
		{
			if ( obj.IsValid() && obj.Id == sourceObjectId )
				return obj;
		}

		return null;
	}
}
