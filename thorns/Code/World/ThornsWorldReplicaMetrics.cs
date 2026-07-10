namespace Sandbox;

/// <summary>Host + client observability for terrain/POI replica payloads and rebuild cost (F1 developer panel).</summary>
public static class ThornsWorldReplicaMetrics
{
	public static int LastTerrainPayloadBytes { get; set; }
	public static int LastTerrainDecodedPayloadBytes { get; set; }
	public static double LastTerrainDecodeMs { get; set; }
	public static double LastTerrainRebuildMs { get; set; }
	public static int TerrainRebuildCount { get; set; }
	public static int LastPoiPayloadBytes { get; set; }
	public static double LastPoiParseMs { get; set; }
	public static int PoiDatasetRebuildCount { get; set; }
	public static int PoiDatasetClientHydrateCount { get; set; }
	public static int TerrainSpecDescriptorVersion { get; set; }
	public static long TerrainSpecContentHash { get; set; }
	public static int PoiDescriptorVersion { get; set; }
	public static long PoiContentDescriptorHash { get; set; }
	public static bool LastClientTerrainHashMatched { get; set; } = true;
	public static string LastTerrainClientRebuildReason { get; set; } = "";
	public static string LastPoiClientHydrateReason { get; set; } = "";
}
