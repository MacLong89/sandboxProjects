namespace SceneLab;

/// <summary>
/// Five lot-safe suburban house archetypes.
/// Dimensions are absolute finals (already tuned) — <see cref="CityScale"/> derives the rest of the city from these.
/// </summary>
public enum HouseArchetype
{
	Cottage = 0,
	Ranch = 1,
	Colonial = 2,
	LBungalow = 3,
	Craftsman = 4,
}

/// <summary>Street-facing house. Local +X = front, +Y = along street.</summary>
public sealed class HouseSpec
{
	public HouseArchetype Archetype;
	public float Width;
	public float Depth;
	public float WallH;
	public float FoundationH = 14f;
	public float GarageWidth;
	public float GarageDepth;
	public float WingWidth;
	public float WingDepth;
	public Color Wall;
	public Color Trim;
	public Color Roof;
	public Color Floor;
	public Color Accent;
	public bool HasGarage;
	public bool HasWing;
	public bool HasPorch;
	public bool HasChimney;
	public bool WalkableUpper;
	/// <summary>+1 / −1: wing on +Y / −Y. Garage always opposite when both present.</summary>
	public int WingSign = 1;

	public float FootprintWidth => Width;
	public float FootprintDepth => Depth;

	public static HouseSpec Of( HouseArchetype archetype, int wingSign = 1 )
	{
		var sign = wingSign >= 0 ? 1 : -1;
		// Finals: previously base × √1.5 — keep the feeling the user locked in.
		return archetype switch
		{
			HouseArchetype.Cottage => new HouseSpec
			{
				Archetype = HouseArchetype.Cottage,
				Width = 318f,
				Depth = 269f,
				WallH = 171f,
				FoundationH = 15f,
				Wall = Palette.HouseSage,
				Trim = KitBox.Solid( Palette.HouseCream, 1.05f ),
				Roof = new Color( 0.38f, 0.32f, 0.28f ),
				Floor = new Color( 0.62f, 0.48f, 0.32f ),
				Accent = Palette.HouseDoor,
				HasGarage = false,
				HasWing = false,
				HasPorch = true,
				HasChimney = true,
				WingSign = sign,
			},
			HouseArchetype.Ranch => new HouseSpec
			{
				Archetype = HouseArchetype.Ranch,
				Width = 539f,
				Depth = 294f,
				WallH = 181f,
				GarageWidth = 171f,
				GarageDepth = 245f,
				Wall = Palette.HouseBlue,
				Trim = KitBox.Solid( Palette.HouseCream ),
				Roof = new Color( 0.32f, 0.34f, 0.38f ),
				Floor = new Color( 0.72f, 0.68f, 0.58f ),
				Accent = new Color( 0.2f, 0.35f, 0.55f ),
				HasGarage = true,
				HasWing = false,
				HasPorch = false,
				HasChimney = false,
				WingSign = sign,
			},
			HouseArchetype.Colonial => new HouseSpec
			{
				Archetype = HouseArchetype.Colonial,
				Width = 490f,
				Depth = 318f,
				WallH = 184f,
				GarageWidth = 147f,
				GarageDepth = 220f,
				Wall = Palette.HouseCream,
				Trim = new Color( 0.92f, 0.92f, 0.90f ),
				Roof = new Color( 0.28f, 0.22f, 0.20f ),
				Floor = new Color( 0.48f, 0.32f, 0.20f ),
				Accent = new Color( 0.45f, 0.12f, 0.12f ),
				HasGarage = true,
				HasWing = false,
				HasPorch = true,
				HasChimney = false,
				WalkableUpper = true,
				WingSign = sign,
			},
			HouseArchetype.LBungalow => new HouseSpec
			{
				Archetype = HouseArchetype.LBungalow,
				Width = 490f,
				Depth = 392f,
				WallH = 184f,
				WingWidth = 196f,
				WingDepth = 257f,
				Wall = Palette.HouseBrick,
				Trim = KitBox.Solid( Palette.HouseCream, 0.98f ),
				Roof = Palette.HouseRoof,
				Floor = new Color( 0.58f, 0.40f, 0.28f ),
				Accent = new Color( 0.55f, 0.30f, 0.22f ),
				HasGarage = false,
				HasWing = true,
				HasPorch = true,
				HasChimney = true,
				WingSign = sign,
			},
			_ => new HouseSpec // Craftsman (~CityScale.HouseFront / HouseDepth)
			{
				Archetype = HouseArchetype.Craftsman,
				Width = 563f,
				Depth = 367f,
				WallH = 193f,
				GarageWidth = 159f,
				GarageDepth = 233f,
				WingWidth = 159f,
				WingDepth = 196f,
				Wall = new Color( 0.78f, 0.70f, 0.55f ),
				Trim = Palette.WoodDark,
				Roof = new Color( 0.35f, 0.22f, 0.14f ),
				Floor = Palette.Wood,
				Accent = Palette.WoodDark,
				HasGarage = true,
				HasWing = true,
				HasPorch = true,
				HasChimney = true,
				WingSign = sign,
			},
		};
	}

	public static HouseSpec Roll( Random rng )
		=> Of( (HouseArchetype)rng.Next( 0, 5 ), rng.Next( 0, 2 ) == 0 ? -1 : 1 );

	public static HouseSpec Default => Of( HouseArchetype.Ranch );
}
