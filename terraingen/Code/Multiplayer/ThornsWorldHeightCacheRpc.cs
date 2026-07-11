namespace Terraingen.Multiplayer;

using Terraingen.TerrainGen;

/// <summary>Streams height cache from host to joining clients.</summary>
public sealed class ThornsWorldHeightCacheRpc : Component
{
	const int FloatsPerChunk = 32768;
	const float RequestCooldownSeconds = 8f;

	ThornsTerrainBootstrap _bootstrap;
	readonly Dictionary<Guid, float> _lastRequestByConnectionId = new();
	HeightmapField _receiveField;
	int _receiveWidth;
	int _receiveHeight;
	int _expectedChunks;
	int _receivedChunks;

	public void Bind( ThornsTerrainBootstrap bootstrap ) => _bootstrap = bootstrap;

	public void HostSetSourceField( HeightmapField field )
	{
		if ( field is null || _bootstrap?.Config is null )
			return;

		ThornsTerrainHeightCache.Save( _bootstrap.Config, field );
	}

	public void ClientRequestIfNeeded()
	{
		if ( !Networking.IsActive || Networking.IsHost || _bootstrap is null )
			return;

		if ( ThornsMultiplayer.IsRemoteJoinClient )
		{
			RpcRequestHeightCache();
			return;
		}

		if ( ThornsTerrainHeightCache.TryLoad( _bootstrap.Config, out _ ) )
			return;

		RpcRequestHeightCache();
	}

	[Rpc.Host]
	void RpcRequestHeightCache()
	{
		if ( Rpc.Caller is null )
			return;

		var now = Time.Now;
		if ( _lastRequestByConnectionId.TryGetValue( Rpc.Caller.Id, out var lastRequest )
		     && now - lastRequest < RequestCooldownSeconds )
			return;

		_lastRequestByConnectionId[Rpc.Caller.Id] = now;

		var config = _bootstrap?.Config;
		if ( config is null )
			return;

		if ( !ThornsTerrainHeightCache.TryLoad( config, out var field ) )
		{
			Log.Warning( "[Thorns World] Client requested height cache but host has none yet." );
			return;
		}

		var total = field.Heights.Length;
		var chunkCount = (total + FloatsPerChunk - 1) / FloatsPerChunk;
		RpcBeginHeightCache( chunkCount, field.Width, field.Height );

		for ( var i = 0; i < chunkCount; i++ )
		{
			var start = i * FloatsPerChunk;
			var length = Math.Min( FloatsPerChunk, total - start );
			var slice = new float[length];
			Array.Copy( field.Heights, start, slice, 0, length );
			RpcSendHeightCacheChunk( i, slice );
		}
	}

	[Rpc.Broadcast]
	void RpcBeginHeightCache( int chunkCount, int width, int height )
	{
		if ( ThornsNetAuthority.RejectClientBroadcastOrigin() )
			return;

		if ( Networking.IsHost )
			return;

		_receiveWidth = width;
		_receiveHeight = height;
		_expectedChunks = chunkCount;
		_receivedChunks = 0;
		_receiveField = new HeightmapField( width, height );
	}

	[Rpc.Broadcast]
	void RpcSendHeightCacheChunk( int chunkIndex, float[] data )
	{
		if ( ThornsNetAuthority.RejectClientBroadcastOrigin() )
			return;

		if ( Networking.IsHost || _receiveField is null || data is null )
			return;

		var start = chunkIndex * FloatsPerChunk;
		if ( start >= _receiveField.Heights.Length )
			return;

		var copyLen = Math.Min( data.Length, _receiveField.Heights.Length - start );
		Array.Copy( data, 0, _receiveField.Heights, start, copyLen );
		_receivedChunks++;

		if ( _receivedChunks < _expectedChunks )
			return;

		ThornsWorldSession.TryReadFromLobby();
		ThornsWorldSession.ApplyConfig( _bootstrap.Config );

		if ( _bootstrap.Config.WorldSeed != ThornsWorldSession.WorldSeed )
		{
			Log.Warning( $"[Thorns World] Ignoring height cache — seed mismatch client={_bootstrap.Config.WorldSeed} lobby={ThornsWorldSession.WorldSeed}." );
			_receiveField = null;
			return;
		}

		ThornsTerrainHeightCache.Save( _bootstrap.Config, _receiveField );
		_bootstrap.ApplyCachedField( _receiveField );
		ThornsWorldBuildingSyncRpc.RequestFromHostIfNeeded();
		Log.Info( "[Thorns World] Client assembled height cache from host." );
	}
}
