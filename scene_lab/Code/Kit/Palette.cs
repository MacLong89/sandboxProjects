namespace SceneLab;

/// <summary>Shared look-dev colors. Car defaults match docs/ref_sedan_goal.md.</summary>
public static class Palette
{
	public static readonly Color Asphalt = new( 0.22f, 0.22f, 0.24f );
	public static readonly Color LaneMark = new( 0.92f, 0.88f, 0.55f );
	public static readonly Color Sidewalk = new( 0.72f, 0.72f, 0.70f );
	public static readonly Color Curb = new( 0.62f, 0.62f, 0.60f );
	public static readonly Color Grass = new( 0.28f, 0.62f, 0.22f );
	public static readonly Color GrassDark = new( 0.22f, 0.52f, 0.18f );
	public static readonly Color Dirt = new( 0.45f, 0.34f, 0.22f );
	public static readonly Color Trunk = new( 0.55f, 0.34f, 0.16f );
	public static readonly Color LeafA = new( 0.32f, 0.72f, 0.18f );
	public static readonly Color LeafB = new( 0.24f, 0.60f, 0.14f );
	public static readonly Color HydrantRed = new( 0.82f, 0.14f, 0.12f );
	public static readonly Color HydrantCap = new( 0.75f, 0.75f, 0.78f );
	public static readonly Color GroundFill = new( 0.35f, 0.48f, 0.28f );

	/// <summary>Goal sedan body paint.</summary>
	public static readonly Color CarTan = new( 0.72f, 0.58f, 0.42f );
	public static readonly Color CarBlue = new( 0.22f, 0.45f, 0.78f );
	public static readonly Color CarRed = new( 0.78f, 0.22f, 0.18f );
	public static readonly Color CarTire = new( 0.10f, 0.10f, 0.11f );
	public static readonly Color CarHub = new( 0.55f, 0.55f, 0.58f );
	/// <summary>Dark opaque car insets (not translucent).</summary>
	public static readonly Color CarGlass = new( 0.14f, 0.14f, 0.16f );
	/// <summary>Building window panes — alpha drives see-through via kit_glass.</summary>
	public static readonly Color WindowGlass = new( 0.55f, 0.72f, 0.88f, 0.32f );
	public static readonly Color CarChrome = new( 0.35f, 0.35f, 0.38f );
	public static readonly Color CarGrille = new( 0.12f, 0.12f, 0.13f );
	public static readonly Color CarHeadlight = new( 0.95f, 0.95f, 0.98f );
	public static readonly Color CarTaillight = new( 0.90f, 0.12f, 0.10f );
	public static readonly Color CarPlate = new( 0.92f, 0.92f, 0.94f );

	public static readonly Color MetalGreen = new( 0.28f, 0.55f, 0.32f );
	public static readonly Color MetalDark = new( 0.16f, 0.16f, 0.18f );
	public static readonly Color Wood = new( 0.62f, 0.42f, 0.24f );
	public static readonly Color WoodDark = new( 0.42f, 0.28f, 0.16f );
	public static readonly Color Cushion = new( 0.45f, 0.48f, 0.52f );

	public static readonly Color HouseCream = new( 0.90f, 0.84f, 0.72f );
	public static readonly Color HouseBlue = new( 0.55f, 0.68f, 0.82f );
	public static readonly Color HouseBrick = new( 0.72f, 0.38f, 0.28f );
	public static readonly Color HouseSage = new( 0.62f, 0.72f, 0.58f );
	public static readonly Color HouseRoof = new( 0.42f, 0.28f, 0.22f );
	public static readonly Color HouseDoor = new( 0.45f, 0.28f, 0.16f );
	public static readonly Color HouseFoundation = new( 0.58f, 0.56f, 0.50f );
	public static readonly Color HouseFloor = new( 0.55f, 0.42f, 0.28f );
	public static readonly Color HouseCeiling = new( 0.92f, 0.90f, 0.86f );
}
