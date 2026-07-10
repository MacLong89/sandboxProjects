namespace Sandbox;

public sealed record AimboxMapDefinition(
	string Id,
	AimboxArenaMap Map,
	string DisplayName,
	string AccentColor,
	AimboxMapLayout Layout,
	string RootName )
{
	public float WidthMeters => Layout.ArenaHalfWidth * 2f / AimboxMapDesignRules.UnitsPerMeter;
	public float DepthMeters => Layout.ArenaHalfLength * 2f / AimboxMapDesignRules.UnitsPerMeter;
	public int LayoutSignature => AimboxMapCatalog.GetLayoutSignature( Layout );
}

public static class AimboxMapCatalog
{
	public static IReadOnlyList<AimboxMapDefinition> All => BuildDefinitions();

	static IReadOnlyList<AimboxMapDefinition> BuildDefinitions() =>
	[
		Define( "yard", AimboxArenaMap.Yard, "YARD", "#7a9e4a", 100.8f, 81.6f, "Aimbox Yard Arena" ),
		Define( "docks", AimboxArenaMap.Docks, "DOCKS", "#4a7a9e", 105.6f, 81.6f, "Aimbox Docks Arena" ),
		Define( "vault", AimboxArenaMap.Vault, "VAULT", "#8a8a92", 96f, 76.8f, "Aimbox Vault Arena" ),
		Define( "junction", AimboxArenaMap.Junction, "JUNCTION", "#9e7a4a", 105.6f, 86.4f, "Aimbox Junction Arena" ),
		Define( "stack", AimboxArenaMap.Stack, "STACK", "#c45a3a", 100.8f, 81.6f, "Aimbox Stack Arena" ),
		Define( "canal", AimboxArenaMap.Canal, "CANAL", "#5a8a9a", 105.6f, 86.4f, "Aimbox Canal Arena" )
	];

	static AimboxMapDefinition Define(
		string id,
		AimboxArenaMap map,
		string displayName,
		string accentColor,
		float widthMeters,
		float depthMeters,
		string rootName ) =>
		new( id, map, displayName, accentColor, AimboxMapDesignRules.CreateLayout( widthMeters, depthMeters ), rootName );

	public static AimboxMapDefinition Get( AimboxArenaMap map ) =>
		All.FirstOrDefault( m => m.Map == map ) ?? All[0];

	public static AimboxMapDefinition Get( string mapId )
	{
		mapId = NormalizeMapId( mapId );
		return All.FirstOrDefault( m => m.Id == mapId ) ?? All[0];
	}

	public static string NormalizeMapId( string mapId )
	{
		if ( string.IsNullOrWhiteSpace( mapId ) )
			return All[0].Id;

		mapId = mapId.Trim().ToLowerInvariant();
		return mapId switch
		{
			"redline" or "skirmish" or "pollution" or "nuketown" or "cyber_yard" or "gridlock" or "crosswind" => All[0].Id,
			_ => All.Any( m => m.Id == mapId ) ? mapId : All[0].Id
		};
	}

	public static AimboxArenaMap MapFromId( string mapId ) => Get( mapId ).Map;

	public static string[] ArenaRootNames => All.Select( m => m.RootName ).ToArray();

	public static int GetLayoutSignature( AimboxMapLayout layout ) =>
		HashCode.Combine(
			layout.ArenaHalfWidth,
			layout.ArenaHalfLength,
			layout.SpawnInset,
			layout.SpawnSpreadY,
			layout.WallThickness );
}
