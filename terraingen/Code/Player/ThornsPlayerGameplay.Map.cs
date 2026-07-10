namespace Terraingen.Player;

using Sandbox.Network;
using Terraingen.GameData;
using Terraingen.Multiplayer;
using Terraingen.UI;

/// <summary>Map snapshot sync (extracted module).</summary>
public sealed partial class ThornsPlayerGameplay
{
	public void RefreshMapSnapshot()
	{
		var map = ThornsMapWorldService.Instance?.BuildSnapshotFor( this, AccountKey ) ?? new ThornsMapSnapshotDto();
		PushMapSnapshotToOwner( map );
	}

	/// <summary>Client peer: rebuild world markers from local scene; keep host-synced waypoints from UI state.</summary>
	public void RefreshClientWorldMapSnapshot()
	{
		if ( !IsLocalPlayer() || ThornsMultiplayer.IsHostOrOffline )
			return;

		var prior = ThornsUiClientState.Snapshot?.Map;
		var map = ThornsMapWorldService.Instance?.BuildSnapshotFor( this, AccountKey ) ?? new ThornsMapSnapshotDto();
		ThornsMapWorldService.PreserveHostAuthoritativeMarkers( prior, map );
		ThornsMapWorldService.Instance?.PreserveUiWaypoints( map );
		ThornsUiClientState.ApplyPartialMap( map );
	}

	/// <summary>Host builds authoritative map (waypoints + world markers) and ships to pawn owner.</summary>
	public void PushMapSnapshotToOwner( ThornsMapSnapshotDto map = null )
	{
		map ??= ThornsMapWorldService.Instance?.BuildSnapshotFor( this, AccountKey ) ?? new ThornsMapSnapshotDto();

		if ( !Networking.IsActive )
			ThornsUiClientState.ApplyPartialMap( map );
		else if ( IsLocalPlayer() && ThornsMultiplayer.IsHostOrOffline )
			ThornsUiClientState.ApplyPartialMap( map );
		else
			RpcSyncMapJson( Json.Serialize( map ) );
	}

	public void RequestSetWaypointAtPlayer()
	{
		if ( !IsLocalPlayer() || string.IsNullOrEmpty( AccountKey ) )
			return;

		var pos = GameObject.WorldPosition;
		if ( Networking.IsActive && !Networking.IsHost )
			RpcSetMapWaypoint( pos.x, pos.y );
		else
		{
			ThornsMapWorldService.Instance?.HostSetWaypoint( AccountKey, pos.x, pos.y );
			PushMapSnapshotToOwner();
		}
	}

	public void RequestClearWaypoint()
	{
		if ( !IsLocalPlayer() || string.IsNullOrEmpty( AccountKey ) )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcClearMapWaypoint();
		else
		{
			ThornsMapWorldService.Instance?.HostClearWaypoint( AccountKey );
			PushMapSnapshotToOwner();
		}
	}

	[Rpc.Host]
	void RpcSetMapWaypoint( float worldX, float worldY )
	{
		if ( !ValidateCaller() )
			return;

		ThornsMapWorldService.Instance?.HostSetWaypoint( AccountKey, worldX, worldY );
		PushMapSnapshotToOwner();
	}

	[Rpc.Host]
	void RpcClearMapWaypoint()
	{
		if ( !ValidateCaller() )
			return;

		ThornsMapWorldService.Instance?.HostClearWaypoint( AccountKey );
		PushMapSnapshotToOwner();
	}

	[Rpc.Owner]
	void RpcSyncMapJson( string json )
	{
		if ( !ThornsNetAuthority.TryDeserializeJson( json, ThornsNetAuthority.DefaultOwnerJsonMaxBytes, out ThornsMapSnapshotDto map ) )
			return;

		ThornsUiClientState.ApplyPartialMap( map );
	}
}
