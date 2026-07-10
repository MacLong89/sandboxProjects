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

	public static Material Grass => _grass ??= Load( "materials/fo_grass.vmat" );
	public static Material Stone => _stone ??= Load( "materials/fo_stone.vmat" );
	public static Material Wood => _wood ??= Load( "materials/fo_wood.vmat" );
	public static Material Roof => _roof ??= Load( "materials/fo_roof.vmat" );

	private static Material Load( string path )
	{
		try
		{
			var mat = Material.Load( path );
			if ( mat is not null && mat.IsValid() )
				return mat;
		}
		catch ( Exception e )
		{
			Log.Warning( $"[FinalOutpost] Material '{path}' failed to load: {e.Message}" );
		}

		return MeshPrimitives.Mat;
	}
}
