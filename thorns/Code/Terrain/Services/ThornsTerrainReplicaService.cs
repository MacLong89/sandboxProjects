namespace Sandbox;

/// <summary>Terrain net spec encoding and client replica sync payload.</summary>
public static class ThornsTerrainReplicaService
{
	public static void PushSpecToChunk( GameObject chunkRoot, ThornsTerrainNetSpec spec )
	{
		if ( !chunkRoot.IsValid() )
			return;

		var chunk = chunkRoot.Components.Get<ThornsTerrainChunk>();
		if ( !chunk.IsValid() )
			return;

		var bytes = ThornsTerrainReplicaBinaryV1.Encode( spec );
		var hash = unchecked((long)ThornsTerrainReplicaBinaryV1.Fnv1a64( bytes ) );
		chunk.TerrainSpecDescriptorVersion = ThornsTerrainReplicaBinaryV1.FormatVersion;
		chunk.SyncSpecPayloadV1Base64 = Convert.ToBase64String( bytes );
		chunk.TerrainSpecContentHash = hash;
		chunk.SyncSpecJson = "";
		ThornsWorldReplicaMetrics.LastTerrainDecodedPayloadBytes = bytes.Length;
		ThornsWorldReplicaMetrics.LastTerrainPayloadBytes = chunk.SyncSpecPayloadV1Base64.Length;
		ThornsWorldReplicaMetrics.TerrainSpecDescriptorVersion = chunk.TerrainSpecDescriptorVersion;
		ThornsWorldReplicaMetrics.TerrainSpecContentHash = hash;
		ThornsHeightmapBakeCache.BindContentHash( hash );
		Log.Info(
			$"[Thorns] Terrain replica v1 pushed rawBytes={bytes.Length} base64Len={chunk.SyncSpecPayloadV1Base64.Length} hash={hash:X}" );
		ThornsReplicationDiagnostics.WarnIfLargeSyncString(
			nameof(ThornsTerrainChunk) + ".SyncSpecPayloadV1Base64",
			chunk.SyncSpecPayloadV1Base64.Length );
	}
}
