namespace Terraingen.Player;

using Sandbox.Network;
using Terraingen.GameData;
using Terraingen.Multiplayer;
using Terraingen.UI;
using Terraingen.Victory;

public sealed partial class ThornsPlayerGameplay
{
	ThornsVictorySnapshot _victory = new();

	public void HostRefreshVictorySnapshot()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		_victory = ThornsVictoryManager.Instance?.BuildSnapshotFor( AccountKey, _victory?.SelectedPathId ) ?? new ThornsVictorySnapshot();
	}

	public void HostPushVictorySnapshot()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		HostRefreshVictorySnapshot();
		PushVictoryToOwner();
		RefreshGuildFromWorld( pushVictory: false );
	}

	public void RequestVictoryUiRefresh()
	{
		if ( !IsLocalPlayer() )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcRequestVictoryUiRefresh();
		else
			HostPushVictorySnapshot();
	}

	[Rpc.Host]
	void RpcRequestVictoryUiRefresh()
	{
		if ( !ValidateCaller() )
			return;

		HostPushVictorySnapshot();
	}

	void PushVictoryToOwner()
	{
		if ( !IsValid )
			return;

		if ( Networking.IsActive && !IsLocalPlayer() )
		{
			if ( !CanPushOwnerRpcs() )
				return;

			RpcSyncVictoryJson( Json.Serialize( _victory ) );
		}
		else if ( IsLocalPlayer() )
			ThornsUiClientState.ApplyPartialVictory( _victory );
	}

	[Rpc.Owner]
	void RpcSyncVictoryJson( string json )
	{
		if ( ThornsNetAuthority.TryDeserializeJson( json, ThornsNetAuthority.DefaultOwnerJsonMaxBytes, out ThornsVictorySnapshot snap ) )
			ThornsUiClientState.ApplyPartialVictory( snap );
	}

	public void SetVictoryUiState( string selectedPathId )
	{
		if ( !IsLocalPlayer() )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
		{
			RpcSetVictoryPath( selectedPathId );
			if ( ThornsUiClientState.HasSnapshot && !string.IsNullOrWhiteSpace( selectedPathId ) )
				ThornsUiClientState.Snapshot.Victory.SelectedPathId = selectedPathId;
			UiRevisionBus.Publish( UiRevisionChannel.Victory );
			return;
		}

		HostApplyVictoryUiState( selectedPathId );
	}

	[Rpc.Host]
	void RpcSetVictoryPath( string selectedPathId )
	{
		if ( !ValidateCaller() )
			return;

		HostApplyVictoryUiState( selectedPathId );
	}

	void HostApplyVictoryUiState( string selectedPathId )
	{
		if ( !string.IsNullOrWhiteSpace( selectedPathId ) )
			_victory.SelectedPathId = selectedPathId;

		HostRefreshVictorySnapshot();
		PushVictoryToOwner();
		RefreshGuildFromWorld( pushVictory: false );
		UiRevisionBus.Publish( UiRevisionChannel.Victory );
	}
}
