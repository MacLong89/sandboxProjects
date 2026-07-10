namespace Sandbox;

/// <summary>Named arena surface types mapped to material slugs under <c>materials/</c>.</summary>
public enum AimboxArenaSurface
{
	Solid,
	Concrete,
	ConcreteDark,
	Asphalt,
	Gravel,
	Tile,
	CorrugatedMetal,
	Wood,
	BarnWood,
	Brick,
	StoneBrick,
	Metal,
	SheetMetal,
}

public static class AimboxArenaSurfaceExtensions
{
	public static string MaterialSlug( this AimboxArenaSurface surface ) => surface switch
	{
		AimboxArenaSurface.Concrete => "concrete",
		AimboxArenaSurface.ConcreteDark => "concrete_dark",
		AimboxArenaSurface.Asphalt => "concrete_dark",
		AimboxArenaSurface.Gravel => "stone_brick",
		AimboxArenaSurface.Tile => "concrete",
		AimboxArenaSurface.CorrugatedMetal => "sheet_metal",
		AimboxArenaSurface.Wood => "wood",
		AimboxArenaSurface.BarnWood => "barn_wood",
		AimboxArenaSurface.Brick => "brick",
		AimboxArenaSurface.StoneBrick => "stone_brick",
		AimboxArenaSurface.Metal => "metal",
		AimboxArenaSurface.SheetMetal => "sheet_metal",
		_ => null
	};

	public static bool HasMaterial( this AimboxArenaSurface surface ) =>
		surface != AimboxArenaSurface.Solid && !string.IsNullOrEmpty( surface.MaterialSlug() );
}
