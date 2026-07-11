namespace Terraingen.Multiplayer;

using Terraingen.Buildings;
using Terraingen.TerrainGen;

/// <summary>Streams host building placements to join clients — no client-side proc layout.</summary>
public sealed class ThornsWorldBuildingSyncRpc : Component
{
	const int PlacementsPerChunk = 40;
	const float RequestCooldownSeconds = 6f;

	static ThornsWorldBuildingSyncRpc _instance;

	ThornsTerrainBootstrap _bootstrap;
	HostBuildingPlacementRecord[] _hostPlacements = Array.Empty<HostBuildingPlacementRecord>();
	HostTownNodeRecord[] _hostTownNodes = Array.Empty<HostTownNodeRecord>();

	HostBuildingPlacementRecord[] _receivePlacements = Array.Empty<HostBuildingPlacementRecord>();
	HostTownNodeRecord[] _receiveTownNodes = Array.Empty<HostTownNodeRecord>();
	int _expectedPlacementChunks;
	int _receivedPlacementChunks;
	bool _clientApplied;
	float _lastClientRequest;

	protected override void OnAwake() => _instance = this;

	protected override void OnDestroy()
	{
		if ( _instance == this )
			_instance = null;
	}

	public void Bind( ThornsTerrainBootstrap bootstrap ) => _bootstrap = bootstrap;

	public static void NotifyHostGenerated( ThornsWorldBuildingGenerator generator )
	{
		if ( !Networking.IsHost || generator is null || !generator.IsValid() )
			return;

		if ( _instance is null || !_instance.IsValid() )
			return;

		_instance.HostPublishFrom( generator );
	}

	public static void RequestFromHostIfNeeded()
	{
		if ( !ThornsMultiplayer.IsRemoteJoinClient || !Networking.IsActive || Networking.IsHost )
			return;

		_instance?.ClientRequestIfNeeded();
	}

	void HostPublishFrom( ThornsWorldBuildingGenerator generator )
	{
		_hostPlacements = generator.ExportHostSyncPlacements();
		_hostTownNodes = generator.ExportHostSyncTownNodes();
		if ( _hostPlacements.Length == 0 )
			return;

		RpcBeginBuildingSync( _hostPlacements.Length, _hostTownNodes );

		var chunkCount = (_hostPlacements.Length + PlacementsPerChunk - 1) / PlacementsPerChunk;
		for ( var chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++ )
		{
			var start = chunkIndex * PlacementsPerChunk;
			var length = Math.Min( PlacementsPerChunk, _hostPlacements.Length - start );
			var slice = new HostBuildingPlacementRecord[length];
			Array.Copy( _hostPlacements, start, slice, 0, length );
			RpcSendBuildingSyncChunk( chunkIndex, slice );
		}
	}

	void ClientRequestIfNeeded()
	{
		if ( _clientApplied || !Networking.IsActive || Networking.IsHost )
			return;

		if ( Time.Now - _lastClientRequest < RequestCooldownSeconds )
			return;

		_lastClientRequest = Time.Now;
		RpcRequestBuildingSync();
	}

	[Rpc.Host]
	void RpcRequestBuildingSync()
	{
		if ( Rpc.Caller is null )
			return;

		if ( _hostPlacements.Length == 0 && _bootstrap is not null )
		{
			var generator = _bootstrap.GameObject.Components.Get<ThornsWorldBuildingGenerator>();
			if ( !generator.IsValid() )
				return;

			HostPublishFrom( generator );
			return;
		}

		if ( _hostPlacements.Length == 0 )
			return;

		RpcBeginBuildingSync( _hostPlacements.Length, _hostTownNodes );

		var chunkCount = (_hostPlacements.Length + PlacementsPerChunk - 1) / PlacementsPerChunk;
		for ( var chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++ )
		{
			var start = chunkIndex * PlacementsPerChunk;
			var length = Math.Min( PlacementsPerChunk, _hostPlacements.Length - start );
			var slice = new HostBuildingPlacementRecord[length];
			Array.Copy( _hostPlacements, start, slice, 0, length );
			RpcSendBuildingSyncChunk( chunkIndex, slice );
		}
	}

	[Rpc.Broadcast]
	void RpcBeginBuildingSync( int placementCount, HostTownNodeRecord[] townNodes )
	{
		if ( ThornsNetAuthority.RejectClientBroadcastOrigin() )
			return;

		if ( Networking.IsHost )
			return;

		_receiveTownNodes = townNodes ?? Array.Empty<HostTownNodeRecord>();
		_receivePlacements = placementCount > 0 ? new HostBuildingPlacementRecord[placementCount] : Array.Empty<HostBuildingPlacementRecord>();
		_expectedPlacementChunks = placementCount > 0
			? (placementCount + PlacementsPerChunk - 1) / PlacementsPerChunk
			: 0;
		_receivedPlacementChunks = 0;

		if ( _expectedPlacementChunks == 0 )
			TryApplyClientSync();
	}

	[Rpc.Broadcast]
	void RpcSendBuildingSyncChunk( int chunkIndex, HostBuildingPlacementRecord[] chunk )
	{
		if ( ThornsNetAuthority.RejectClientBroadcastOrigin() )
			return;

		if ( Networking.IsHost || chunk is null || _receivePlacements.Length == 0 )
			return;

		var start = chunkIndex * PlacementsPerChunk;
		if ( start >= _receivePlacements.Length )
			return;

		for ( var i = 0; i < chunk.Length; i++ )
		{
			var dst = start + i;
			if ( dst >= _receivePlacements.Length )
				break;

			_receivePlacements[dst] = chunk[i];
		}

		_receivedPlacementChunks++;
		if ( _receivedPlacementChunks < _expectedPlacementChunks )
			return;

		TryApplyClientSync();
	}

	void TryApplyClientSync()
	{
		if ( _clientApplied || _bootstrap is null || !_bootstrap.IsValid() )
			return;

		var terrain = _bootstrap.WorldTerrain;
		var config = _bootstrap.Config;
		if ( !terrain.IsValid() || config is null || _receivePlacements.Length == 0 )
			return;

		var generator = _bootstrap.GameObject.Components.Get<ThornsWorldBuildingGenerator>()
		                ?? _bootstrap.GameObject.Components.Create<ThornsWorldBuildingGenerator>();
		generator.ApplyHostSync( _receivePlacements, _receiveTownNodes, terrain, config );
		_clientApplied = true;
		Log.Info( $"[Thorns Buildings] Client applied host sync ({_receivePlacements.Length} building(s))." );
	}
}
