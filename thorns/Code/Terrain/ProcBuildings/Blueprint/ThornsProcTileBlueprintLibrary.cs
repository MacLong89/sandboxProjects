using System.Linq;

namespace Sandbox;

/// <summary>Authoritative tile blueprints (rows listed north → south, parsed to y=0 = south).</summary>
public static class ThornsProcTileBlueprintLibrary
{
	static readonly Dictionary<ThornsProcBuildingType, ThornsProcTileBlueprint> _byType = BuildAll();
	static readonly Dictionary<ThornsProcBuildingType, ThornsProcTileBlueprint> _organicCompact = BuildOrganicCompact();

	public static ThornsProcTileBlueprint Get( ThornsProcBuildingType type ) =>
		_byType.TryGetValue( type, out var bp ) ? bp : _byType[ThornsProcBuildingType.House];

	/// <summary>Smaller footprints for tall landmarks during organic map scatter (same story height).</summary>
	public static ThornsProcTileBlueprint GetForOrganicPlacement( ThornsProcBuildingType type ) =>
		_organicCompact.TryGetValue( type, out var compact ) ? compact : Get( type );

	public static bool TryGet( ThornsProcBuildingType type, out ThornsProcTileBlueprint blueprint ) =>
		_byType.TryGetValue( type, out blueprint );

	static Dictionary<ThornsProcBuildingType, ThornsProcTileBlueprint> BuildAll() =>
		new()
		{
			[ThornsProcBuildingType.House] = BuildHouse(),
			[ThornsProcBuildingType.Ruin] = BuildRuin(),
			[ThornsProcBuildingType.Warehouse] = BuildWarehouse(),
			[ThornsProcBuildingType.MilitaryComplex] = BuildMilitaryComplex(),
			[ThornsProcBuildingType.Cabin] = BuildCabin(),
			[ThornsProcBuildingType.Store] = BuildStore(),
			[ThornsProcBuildingType.Apartment] = BuildApartment(),
			[ThornsProcBuildingType.Factory] = BuildFactory(),
			[ThornsProcBuildingType.Barn] = BuildBarn(),
			[ThornsProcBuildingType.RadioOutpost] = BuildRadioOutpost(),
			[ThornsProcBuildingType.ApartmentTower] = BuildApartmentTower(),
			[ThornsProcBuildingType.Skyscraper] = BuildSkyscraper(),
			[ThornsProcBuildingType.OfficeBuilding] = BuildOfficeBuilding()
		};

	static ThornsProcTileBlueprint BuildHouse() => new()
	{
		Type = ThornsProcBuildingType.House,
		DisplayName = "House",
		WindowChance = 0.35f,
		Layers =
		[
			ThornsProcTileBlueprintParser.ParseLayer(
				"[F:Window_W] [F] [F] [F:Window_E]",
				"[F] [F:R_N] [F] [F]",
				"[F] [F] [F] [F]",
				"[F:Door_S] [F] [F] [F]" ),
			ThornsProcTileBlueprintParser.ParseLayer(
				"[F] [F] [F] [F]",
				"[F] [F] [F] [F]",
				"[F] [F] [F] [F]",
				"[F] [F] [F] [F]" )
		]
	};

	static ThornsProcTileBlueprint BuildRuin() => new()
	{
		Type = ThornsProcBuildingType.Ruin,
		DisplayName = "Ruin",
		WindowChance = 0.1f,
		Layers =
		[
			ThornsProcTileBlueprintParser.ParseLayer(
				"[F] [. ] [F] [F]",
				"[F] [F:R_N] [. ] [F]",
				"[F] [. ] [F] [F]",
				"[F:Door_S] [F] [. ] [F]" ),
			ThornsProcTileBlueprintParser.ParseLayer(
				"[F] [O] [O] [F]",
				"[F] [O] [. ] [F]",
				"[.] [F] [F] [. ]",
				"[F] [. ] [F] [F]" )
		]
	};

	static ThornsProcTileBlueprint BuildWarehouse() => new()
	{
		Type = ThornsProcBuildingType.Warehouse,
		DisplayName = "Warehouse",
		WindowChance = 0.08f,
		Layers =
		[
			ThornsProcTileBlueprintParser.ParseLayer(
				"[F:Door_S] [F] [F] [F] [F] [F:Door_S]",
				"[F] [F] [F] [F] [F] [F]",
				"[F] [F:R_N] [F] [F:R_N] [F] [F]",
				"[F:Door_N] [F] [F] [F] [F] [F:Door_N]" ),
			ThornsProcTileBlueprintParser.ParseLayer(
				"[F] [F] [F] [F] [F] [F]",
				"[F] [O] [O] [. ] [O] [F]",
				"[F] [O] [. ] [O] [O] [F]",
				"[F] [F] [F] [F] [F] [F]" )
		]
	};

	static ThornsProcTileBlueprint BuildMilitaryComplex() => new()
	{
		Type = ThornsProcBuildingType.MilitaryComplex,
		DisplayName = "Military Complex",
		WindowChance = 0.06f,
		Layers =
		[
			ThornsProcTileBlueprintParser.ParseLayer(
				"[F:Door_S] [F] [F] [. ] [. ] [F] [F] [F:Door_S]",
				"[F] [. ] [F] [. ] [. ] [F] [. ] [F]",
				"[F] [F] [F] [. ] [. ] [F] [F] [F]",
				"[.] [. ] [. ] [. ] [. ] [. ] [. ] [.]",
				"[.] [. ] [. ] [. ] [. ] [. ] [. ] [.]",
				"[F] [F] [F] [. ] [. ] [F] [F] [F]",
				"[F] [. ] [F] [. ] [. ] [F] [. ] [F]",
				"[F:Door_N] [F] [F] [. ] [. ] [F] [F] [F:Door_N]" )
		]
	};

	static ThornsProcTileBlueprint BuildCabin() => new()
	{
		Type = ThornsProcBuildingType.Cabin,
		DisplayName = "Cabin",
		WindowChance = 0.18f,
		Layers =
		[
			ThornsProcTileBlueprintParser.ParseLayer(
				"[F:Window_W] [F] [F:Window_E]",
				"[F] [F] [F]",
				"[F:Door_S] [F] [F]" )
		]
	};

	static ThornsProcTileBlueprint BuildStore() => new()
	{
		Type = ThornsProcBuildingType.Store,
		DisplayName = "Store",
		WindowChance = 0.5f,
		PreferFrontWindows = true,
		Layers =
		[
			ThornsProcTileBlueprintParser.ParseLayer(
				"[F:Window_N] [F:Window_N] [F:Window_N] [F:Window_N] [F:Window_N]",
				"[F] [F] [F] [F] [F]",
				"[F] [F] [F] [F] [F]",
				"[F:Door_S] [F] [F] [F] [F:Door_S]" )
		]
	};

	static ThornsProcTileBlueprint BuildApartment() => new()
	{
		Type = ThornsProcBuildingType.Apartment,
		DisplayName = "Apartment",
		WindowChance = 0.38f,
		Layers =
		[
			ThornsProcTileBlueprintParser.ParseLayer(
				"[F] [F] [F] [F] [F]",
				"[F] [F:R_N] [F] [F:R_N] [F]",
				"[F] [F] [F] [F] [F]",
				"[F:Door_S] [F] [F] [F] [F:Door_S]" ),
			ThornsProcTileBlueprintParser.ParseLayer(
				"[F] [O] [O] [O] [F]",
				"[F] [O] [F] [O] [O]",
				"[F] [F] [F] [F] [F]",
				"[F] [F] [F] [F] [F]" ),
			ThornsProcTileBlueprintParser.ParseLayer(
				"[F] [F] [F] [F] [F]",
				"[F] [F] [F] [F] [F]",
				"[F] [F] [F] [F] [F]",
				"[F] [F] [F] [F] [F]" )
		]
	};

	static ThornsProcTileBlueprint BuildFactory() => new()
	{
		Type = ThornsProcBuildingType.Factory,
		DisplayName = "Factory",
		WindowChance = 0.08f,
		Layers =
		[
			ThornsProcTileBlueprintParser.ParseLayer(
				"[F:Door_S] [F] [F] [F] [F] [F] [F:Door_S]",
				"[F] [F:R_N] [F] [F:R_N] [F] [F] [F]",
				"[F] [F] [F] [F] [F] [F]",
				"[F] [F] [F] [F] [F] [F]",
				"[F:Door_N] [F] [F] [F] [F] [F] [F:Door_N]" ),
			ThornsProcTileBlueprintParser.ParseLayer(
				"[F] [O] [O] [O] [F] [O] [O]",
				"[F] [O] [. ] [O] [O] [. ] [F]",
				"[F] [. ] [. ] [. ] [. ] [. ] [F]",
				"[F] [F] [F] [F] [F] [F] [F]",
				"[F] [F] [F] [F] [F] [F] [F]" ),
			ThornsProcTileBlueprintParser.ParseLayer(
				"[F] [F] [F] [F] [F] [F] [F]",
				"[F] [F] [F] [F] [F] [F] [F]",
				"[F] [F] [F] [F] [F] [F] [F]",
				"[F] [F] [F] [F] [F] [F] [F]",
				"[F] [F] [F] [F] [F] [F] [F]" )
		]
	};

	static ThornsProcTileBlueprint BuildBarn() => new()
	{
		Type = ThornsProcBuildingType.Barn,
		DisplayName = "Barn",
		WindowChance = 0.05f,
		Layers =
		[
			ThornsProcTileBlueprintParser.ParseLayer(
				"[F:Door_S] [F] [F] [F] [F:Door_S]",
				"[F] [F:R_N] [F] [F] [F]",
				"[F] [F] [F] [F] [F]",
				"[F:Door_N] [F] [F] [F] [F:Door_N]" ),
			ThornsProcTileBlueprintParser.ParseLayer(
				"[F] [O] [O] [F] [F]",
				"[F] [O] [. ] [. ] [F]",
				"[F] [. ] [. ] [. ] [F]",
				"[F] [F] [F] [F] [F]" )
		]
	};

	static ThornsProcTileBlueprint BuildRadioOutpost() => new()
	{
		Type = ThornsProcBuildingType.RadioOutpost,
		DisplayName = "Radio Outpost",
		WindowChance = 0.25f,
		Layers =
		[
			ThornsProcTileBlueprintParser.ParseLayer(
				"[F] [F:R_N] [F] [F]",
				"[F] [F] [F] [F]",
				"[F] [F] [F] [F]",
				"[F:Door_S] [F] [F] [F:Door_S]" ),
			ThornsProcTileBlueprintParser.ParseLayer(
				"[F] [O] [O] [F]",
				"[F] [O] [F] [F]",
				"[F] [F] [F] [F]",
				"[F] [F] [F] [F]" )
		]
	};

	static ThornsProcTileBlueprint BuildApartmentTower() => new()
	{
		Type = ThornsProcBuildingType.ApartmentTower,
		DisplayName = "Apartment Tower",
		WindowChance = 0.36f,
		AllowDamagedVariant = true,
		Layers =
		[
			ThornsProcTileBlueprintParser.ParseLayer(
				"[F] [F] [F] [F] [F]",
				"[F] [F:R_N] [F] [F:R_N] [F]",
				"[F] [F] [F] [F] [F]",
				"[F] [F] [F] [F] [F]",
				"[F:Door_S] [F] [F] [F] [F:Door_S]" ),
			Layer5x5DualRampLanding(),
			Layer5x5DualRampLanding(),
			Layer5x5DualRampLanding(),
			Layer5x5AllFloors( 5 )
		]
	};

	static ThornsProcTileBlueprint BuildSkyscraper() => new()
	{
		Type = ThornsProcBuildingType.Skyscraper,
		DisplayName = "Skyscraper",
		WindowChance = 0.24f,
		AllowDamagedVariant = true,
		Layers = BuildSkyscraperLayers()
	};

	static ThornsProcTileBlueprint BuildOfficeBuilding() => new()
	{
		Type = ThornsProcBuildingType.OfficeBuilding,
		DisplayName = "Office Building",
		WindowChance = 0.28f,
		AllowDamagedVariant = true,
		Layers =
		[
			ThornsProcTileBlueprintParser.ParseLayer(
				"[F:Door_S] [F] [F] [F] [F] [F:Door_S]",
				"[F] [F:R_N] [F] [F] [F] [F]",
				"[F] [F] [F] [F] [F] [F]",
				"[F] [F] [F] [F] [F] [F]",
				"[F:Door_N] [F] [F] [F] [F] [F:Door_N]" ),
			Layer6x5SingleRampLanding(),
			Layer6x5SingleRampLanding(),
			Layer6x5AllFloors( 5 )
		]
	};

	static IReadOnlyList<ThornsProcTileLayer> BuildSkyscraperLayers()
	{
		var layers = new List<ThornsProcTileLayer>( 8 )
		{
			ThornsProcTileBlueprintParser.ParseLayer(
				"[F:Door_S] [F] [F] [F] [F] [F:Door_S]",
				"[F] [F:R_N] [F] [F:R_N] [F] [F]",
				"[F] [F] [F] [F] [F] [F]",
				"[F] [F] [F] [F] [F] [F]",
				"[F] [F] [F] [F] [F] [F]",
				"[F:Door_N] [F] [F] [F] [F] [F:Door_N]" )
		};

		for ( var i = 0; i < 6; i++ )
			layers.Add( Layer6x6DualRampLanding() );

		layers.Add( Layer6x6AllFloors() );
		return layers;
	}

	static ThornsProcTileLayer Layer5x5AllFloors( int rows ) =>
		ThornsProcTileBlueprintParser.ParseLayer( Enumerable.Repeat( "[F] [F] [F] [F] [F]", rows ).ToArray() );

	static ThornsProcTileLayer Layer5x5DualRampLanding() =>
		ThornsProcTileBlueprintParser.ParseLayer(
			"[F] [O] [O] [O] [F]",
			"[F] [O] [F] [O] [O]",
			"[F] [F] [F] [F] [F]",
			"[F] [F] [F] [F] [F]",
			"[F] [F] [F] [F] [F]" );

	static ThornsProcTileLayer Layer6x6DualRampLanding() =>
		ThornsProcTileBlueprintParser.ParseLayer(
			"[F] [O] [O] [O] [O] [F]",
			"[F] [O] [F] [F] [O] [O]",
			"[F] [F] [F] [F] [F] [F]",
			"[F] [F] [F] [F] [F] [F]",
			"[F] [F] [F] [F] [F] [F]",
			"[F] [F] [F] [F] [F] [F]" );

	static ThornsProcTileLayer Layer6x6AllFloors() =>
		ThornsProcTileBlueprintParser.ParseLayer(
			"[F] [F] [F] [F] [F] [F]",
			"[F] [F] [F] [F] [F] [F]",
			"[F] [F] [F] [F] [F] [F]",
			"[F] [F] [F] [F] [F] [F]",
			"[F] [F] [F] [F] [F] [F]",
			"[F] [F] [F] [F] [F] [F]" );

	static ThornsProcTileLayer Layer6x5SingleRampLanding() =>
		ThornsProcTileBlueprintParser.ParseLayer(
			"[F] [O] [O] [F] [F] [F]",
			"[F] [O] [F] [F] [F] [F]",
			"[F] [F] [F] [F] [F] [F]",
			"[F] [F] [F] [F] [F] [F]",
			"[F] [F] [F] [F] [F] [F]" );

	static ThornsProcTileLayer Layer6x5AllFloors( int rows ) =>
		ThornsProcTileBlueprintParser.ParseLayer( Enumerable.Repeat( "[F] [F] [F] [F] [F] [F]", rows ).ToArray() );

	static Dictionary<ThornsProcBuildingType, ThornsProcTileBlueprint> BuildOrganicCompact() =>
		new()
		{
			[ThornsProcBuildingType.ApartmentTower] = BuildApartmentTowerOrganic(),
			[ThornsProcBuildingType.Skyscraper] = BuildSkyscraperOrganic(),
			[ThornsProcBuildingType.OfficeBuilding] = BuildOfficeBuildingOrganic()
		};

	static ThornsProcTileBlueprint BuildApartmentTowerOrganic() => new()
	{
		Type = ThornsProcBuildingType.ApartmentTower,
		DisplayName = "Apartment Tower",
		WindowChance = 0.36f,
		AllowDamagedVariant = true,
		Layers =
		[
			ThornsProcTileBlueprintParser.ParseLayer(
				"[F] [F] [F] [F]",
				"[F] [F:R_N] [F:R_N] [F]",
				"[F] [F] [F] [F]",
				"[F:Door_S] [F] [F] [F:Door_S]" ),
			Layer4x4DualRampLanding(),
			Layer4x4DualRampLanding(),
			Layer4x4DualRampLanding(),
			Layer4x4AllFloors( 5 )
		]
	};

	static ThornsProcTileBlueprint BuildSkyscraperOrganic() => new()
	{
		Type = ThornsProcBuildingType.Skyscraper,
		DisplayName = "Skyscraper",
		WindowChance = 0.24f,
		AllowDamagedVariant = true,
		Layers = BuildSkyscraperOrganicLayers()
	};

	static IReadOnlyList<ThornsProcTileLayer> BuildSkyscraperOrganicLayers()
	{
		var layers = new List<ThornsProcTileLayer>( 8 )
		{
			ThornsProcTileBlueprintParser.ParseLayer(
				"[F:Door_S] [F] [F] [F:Door_S]",
				"[F] [F:R_N] [F:R_N] [F]",
				"[F] [F] [F] [F]",
				"[F:Door_N] [F] [F] [F:Door_N]" )
		};

		for ( var i = 0; i < 6; i++ )
			layers.Add( Layer4x4DualRampLanding() );

		layers.Add( Layer4x4AllFloors( 1 ) );
		return layers;
	}

	static ThornsProcTileBlueprint BuildOfficeBuildingOrganic() => new()
	{
		Type = ThornsProcBuildingType.OfficeBuilding,
		DisplayName = "Office Building",
		WindowChance = 0.28f,
		AllowDamagedVariant = true,
		Layers =
		[
			ThornsProcTileBlueprintParser.ParseLayer(
				"[F:Door_S] [F] [F] [F:Door_S]",
				"[F] [F:R_N] [F] [F]",
				"[F] [F] [F] [F]",
				"[F:Door_N] [F] [F] [F:Door_N]" ),
			Layer4x4SingleRampLanding(),
			Layer4x4SingleRampLanding(),
			Layer4x4AllFloors( 5 )
		]
	};

	static ThornsProcTileLayer Layer4x4AllFloors( int rows ) =>
		ThornsProcTileBlueprintParser.ParseLayer( Enumerable.Repeat( "[F] [F] [F] [F]", rows ).ToArray() );

	static ThornsProcTileLayer Layer4x4DualRampLanding() =>
		ThornsProcTileBlueprintParser.ParseLayer(
			"[F] [O] [O] [F]",
			"[F] [O] [F] [O]",
			"[F] [F] [F] [F]",
			"[F] [F] [F] [F]" );

	static ThornsProcTileLayer Layer4x4SingleRampLanding() =>
		ThornsProcTileBlueprintParser.ParseLayer(
			"[F] [O] [O] [F]",
			"[F] [O] [F] [F]",
			"[F] [F] [F] [F]",
			"[F] [F] [F] [F]" );
}
