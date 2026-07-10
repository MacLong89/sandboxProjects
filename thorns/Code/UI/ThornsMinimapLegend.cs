using System;
using System.Collections.Generic;

namespace Sandbox;

/// <summary>World-map legend rows (swatch + label) aligned with minimap marker styling.</summary>
public static class ThornsMinimapLegend
{
	public enum SwatchShape
	{
		Circle,
		Square,
		DeathCross
	}

	public readonly record struct Entry( string Label, Color Color, SwatchShape Shape, float SizePx );

	static readonly ThornsProcBuildingType[] BuildingTypeDisplayOrder =
	[
		ThornsProcBuildingType.Skyscraper,
		ThornsProcBuildingType.ApartmentTower,
		ThornsProcBuildingType.OfficeBuilding,
		ThornsProcBuildingType.Apartment,
		ThornsProcBuildingType.Store,
		ThornsProcBuildingType.Factory,
		ThornsProcBuildingType.Warehouse,
		ThornsProcBuildingType.House,
		ThornsProcBuildingType.Cabin,
		ThornsProcBuildingType.Barn,
		ThornsProcBuildingType.Ruin,
		ThornsProcBuildingType.MilitaryComplex,
		ThornsProcBuildingType.RadioOutpost
	];

	public static IReadOnlyList<Entry> BuildEntries(
		bool useBuildingTypeColors,
		IReadOnlyList<ThornsPoiAuthority.PoiClientRecord> livePois = null )
	{
		var list = new List<Entry>( 32 );
		list.Add( new Entry( "You", new Color( 0.32f, 0.79f, 0.85f, 0.95f ), SwatchShape.Circle, 8f ) );
		list.Add( new Entry( "Last death", new Color( 0.86f, 0.15f, 0.15f, 0.98f ), SwatchShape.DeathCross, 14f ) );
		list.Add( new Entry( "Your bed (respawn)", new Color( 0.28f, 0.8f, 0.47f, 0.96f ), SwatchShape.Square, 10f ) );
		list.Add( new Entry( "Supply drop", new Color( 1f, 1f, 0f, 0.96f ), SwatchShape.Circle, 12f ) );
		list.Add( new Entry( "Boss wildlife", new Color( 0.96f, 0.12f, 0.08f, 0.98f ), SwatchShape.Circle, 10f ) );
		list.Add( new Entry( "Guild mate", new Color( 0.62f, 0.94f, 0.42f, 0.96f ), SwatchShape.Circle, 5f ) );

		var seenColors = new HashSet<uint>();

		if ( useBuildingTypeColors )
			AppendAllBuildingTypeEntries( list, seenColors );
		else
			AppendSettlementMaterialEntries( list, seenColors );

		AppendUniqueLivePoiEntries( list, livePois, seenColors, useBuildingTypeColors );

		return list;
	}

	static void AppendAllBuildingTypeEntries( List<Entry> list, HashSet<uint> seenColors )
	{
		var covered = new HashSet<ThornsProcBuildingType>();

		foreach ( var type in BuildingTypeDisplayOrder )
		{
			AddBuildingType( list, seenColors, type );
			covered.Add( type );
		}

		foreach ( ThornsProcBuildingType type in Enum.GetValues<ThornsProcBuildingType>() )
		{
			if ( covered.Contains( type ) )
				continue;

			AddBuildingType( list, seenColors, type );
		}
	}

	static void AppendSettlementMaterialEntries( List<Entry> list, HashSet<uint> seenColors )
	{
		AddColored( list, seenColors, "Main city — metal tier", new Color( 0.45f, 0.88f, 1f, 1f ) );
		AddColored( list, seenColors, "Main city — stone tier", new Color( 0.78f, 0.78f, 0.82f, 1f ) );
		AddColored( list, seenColors, "Main city — wood tier", new Color( 0.82f, 0.66f, 0.46f, 1f ) );
		AddColored( list, seenColors, "Town — metal tier", new Color( 0.55f, 0.62f, 0.7f, 0.95f ) );
		AddColored( list, seenColors, "Town — stone tier", new Color( 0.7f, 0.7f, 0.7f, 0.95f ) );
		AddColored( list, seenColors, "Town — wood tier", new Color( 0.7f, 0.55f, 0.38f, 0.95f ) );
		AddColored( list, seenColors, "Wilderness — metal tier", new Color( 0.58f, 0.64f, 0.72f, 0.9f ) );
		AddColored( list, seenColors, "Wilderness — stone tier", new Color( 0.72f, 0.72f, 0.72f, 0.9f ) );
		AddColored( list, seenColors, "Wilderness — wood tier", new Color( 0.72f, 0.56f, 0.4f, 0.9f ) );
	}

	static void AppendUniqueLivePoiEntries(
		List<Entry> list,
		IReadOnlyList<ThornsPoiAuthority.PoiClientRecord> livePois,
		HashSet<uint> seenColors,
		bool useBuildingTypeColors )
	{
		if ( livePois is null || livePois.Count == 0 )
			return;

		foreach ( var poi in livePois )
		{
			if ( poi.Rgba == 0 || !seenColors.Add( poi.Rgba ) )
				continue;

			if ( IsNonBuildingPoiKey( poi.Key ) )
				continue;

			var label = string.IsNullOrWhiteSpace( poi.Label ) ? "Building" : poi.Label.Trim();
			if ( useBuildingTypeColors && label.Contains( '(' ) )
				label = label.Split( '(' )[0].Trim();

			list.Add( new Entry(
				label,
				ThornsPoiAuthority.UnpackRgba( poi.Rgba ),
				SwatchShape.Circle,
				Math.Clamp( poi.BlipDiameterPx, 8f, 14f ) ) );
		}
	}

	static bool IsNonBuildingPoiKey( string key )
	{
		if ( string.IsNullOrWhiteSpace( key ) )
			return false;

		return key.Contains( "supply", StringComparison.OrdinalIgnoreCase )
		       || key.Contains( "player", StringComparison.OrdinalIgnoreCase );
	}

	static void AddBuildingType( List<Entry> list, HashSet<uint> seenColors, ThornsProcBuildingType type )
	{
		var color = ThornsProcBuildingTypeDebugColors.MinimapColor( type );
		AddColored( list, seenColors, BuildingTypeLegendLabel( type ), color );
	}

	static void AddColored(
		List<Entry> list,
		HashSet<uint> seenColors,
		string label,
		Color color,
		float sizePx = 10f )
	{
		var rgba = ThornsPoiAuthority.PackRgba( color );
		if ( !seenColors.Add( rgba ) )
			return;

		list.Add( new Entry( label, color, SwatchShape.Circle, sizePx ) );
	}

	static string BuildingTypeLegendLabel( ThornsProcBuildingType type ) =>
		type switch
		{
			ThornsProcBuildingType.ApartmentTower => "Apartment tower",
			ThornsProcBuildingType.OfficeBuilding => "Office building",
			ThornsProcBuildingType.MilitaryComplex => "Military complex",
			ThornsProcBuildingType.RadioOutpost => "Radio outpost",
			_ => type.ToString()
		};

	public static bool ResolveUseBuildingTypeColors( Scene scene )
	{
		if ( ThornsProcBuildingTypeDebugColors.UseTypeColors )
			return true;

		if ( scene is null || !scene.IsValid() )
			return true;

		foreach ( var terrain in scene.GetAllComponents<ThornsTerrainSystem>() )
		{
			if ( terrain.IsValid() )
				return terrain.DebugBuildingTypeColors;
		}

		return true;
	}
}
