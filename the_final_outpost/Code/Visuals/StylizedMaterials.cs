namespace FinalOutpost;

/// <summary>
/// Loads the Clash-of-Clans-style textured materials, falling back to the flat default
/// material (driven by renderer tint) if an asset fails to load. This keeps the game
/// looking colorful even if a texture material is missing.
/// </summary>
public static class StylizedMaterials
{
	private static Material _grass;
	private static Material _stone;
	private static Material _wood;
	private static Material _roof;
	private static Material _brick;
	private static Material _metal;
	private static Material _plaster;
	private static Material _thatch;
	private static Material _crops;
	private static Material _awning;
	private static Material _slate;

	public static Material Grass => _grass ??= Load( "materials/fo_grass.vmat" );
	public static Material Stone => _stone ??= Load( "materials/fo_stone.vmat" );
	public static Material Wood => _wood ??= Load( "materials/fo_wood.vmat" );
	public static Material Roof => _roof ??= Load( "materials/fo_roof.vmat" );
	public static Material Brick => _brick ??= Load( "materials/fo_brick.vmat" );
	public static Material Metal => _metal ??= Load( "materials/fo_metal.vmat" );
	public static Material Plaster => _plaster ??= Load( "materials/fo_plaster.vmat" );
	public static Material Thatch => _thatch ??= Load( "materials/fo_thatch.vmat" );
	public static Material Crops => _crops ??= Load( "materials/fo_crops.vmat" );
	public static Material Awning => _awning ??= Load( "materials/fo_awning.vmat" );
	public static Material Slate => _slate ??= Load( "materials/fo_slate.vmat" );

	private static Material Load( string path ) =>
		AssetSafe.Material( path, MeshPrimitives.Mat ) ?? MeshPrimitives.Mat;
}
