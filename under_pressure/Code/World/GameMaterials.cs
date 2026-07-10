namespace UnderPressure;

/// <summary>
/// Lazily-loaded surface materials. Each is a grayscale detail texture on the standard
/// shader; the actual colour still comes from <see cref="ModelRenderer.Tint"/> (the shader
/// multiplies it in via g_flModelTintAmount), so the whole existing palette is preserved
/// while the textures add grain/pattern. Falls back to the flat dev material if a load
/// fails, so a missing asset never leaves an error surface.
/// </summary>
public static class GameMaterials
{
	private static Material Load( string name )
	{
		var mat = Material.Load( $"materials/up/{name}.vmat" );
		return mat ?? MeshPrimitives.Mat;
	}

	private static Material _grass, _concrete, _grime, _grimeFade, _wood, _bark, _shingles, _leaves, _metal, _horizon, _waterSpray;

	public static Material Grass => _grass ??= Load( "grass" );
	public static Material Concrete => _concrete ??= Load( "concrete" );
	public static Material Grime => _grime ??= Load( "grime" );

	/// <summary>Translucent grime used by dirt cells so they fade out as they're cleaned.</summary>
	public static Material GrimeFade => _grimeFade ??= Load( "grime_fade" );
	public static Material Wood => _wood ??= Load( "wood" );
	public static Material Bark => _bark ??= Load( "bark" );
	public static Material Shingles => _shingles ??= Load( "shingles" );
	public static Material Leaves => _leaves ??= Load( "leaves" );
	public static Material Metal => _metal ??= Load( "metal" );

	/// <summary>Soft unlit-style backdrop for the distant horizon ring.</summary>
	public static Material Horizon => _horizon ??= Load( "horizon" );

	/// <summary>Translucent glossy surface for pressure-washer spray droplets.</summary>
	public static Material WaterSpray => _waterSpray ??= Load( "water_spray" );
}
