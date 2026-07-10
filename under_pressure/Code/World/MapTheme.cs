namespace UnderPressure;

/// <summary>Visual environment wrapped around each job's work site.</summary>
public enum MapTheme
{
	Suburban,
	UrbanPlaza,
	GasStation,
	Alley,
	Backyard,
	Storefront,
	Industrial,
	ParkingGarage,
}

/// <summary>Terrain and horizon palette for a <see cref="MapTheme"/>.</summary>
public readonly struct MapThemeInfo
{
	public Color FieldColor { get; init; }
	public Material FieldMaterial { get; init; }
	public Color TransitionColor { get; init; }
	public Material TransitionMaterial { get; init; }
	public Color HorizonSky { get; init; }
	public Color HorizonGround { get; init; }
}

public static class MapThemes
{
	public static MapThemeInfo Get( MapTheme theme ) => theme switch
	{
		MapTheme.UrbanPlaza => new()
		{
			FieldColor = new Color( 0.58f, 0.52f, 0.40f ),
			FieldMaterial = GameMaterials.Concrete,
			TransitionColor = new Color( 0.66f, 0.58f, 0.44f ),
			TransitionMaterial = GameMaterials.Concrete,
			HorizonSky = new Color( 0.94f, 0.72f, 0.40f ),
			HorizonGround = new Color( 0.52f, 0.46f, 0.34f ),
		},
		MapTheme.GasStation => new()
		{
			FieldColor = new Color( 0.48f, 0.46f, 0.42f ),
			FieldMaterial = GameMaterials.Concrete,
			TransitionColor = new Color( 0.56f, 0.52f, 0.46f ),
			TransitionMaterial = GameMaterials.Concrete,
			HorizonSky = new Color( 0.96f, 0.74f, 0.38f ),
			HorizonGround = new Color( 0.54f, 0.50f, 0.42f ),
		},
		MapTheme.Alley => new()
		{
			FieldColor = new Color( 0.52f, 0.48f, 0.44f ),
			FieldMaterial = GameMaterials.Concrete,
			TransitionColor = new Color( 0.58f, 0.52f, 0.46f ),
			TransitionMaterial = GameMaterials.Concrete,
			HorizonSky = new Color( 0.82f, 0.62f, 0.44f ),
			HorizonGround = new Color( 0.44f, 0.40f, 0.36f ),
		},
		MapTheme.Backyard => new()
		{
			FieldColor = new Color( 0.42f, 0.84f, 0.10f ),
			FieldMaterial = GameMaterials.Grass,
			TransitionColor = new Color( 0.46f, 0.88f, 0.12f ),
			TransitionMaterial = GameMaterials.Grass,
			HorizonSky = new Color( 0.98f, 0.76f, 0.36f ),
			HorizonGround = new Color( 0.38f, 0.72f, 0.10f ),
		},
		MapTheme.Storefront => new()
		{
			FieldColor = new Color( 0.54f, 0.50f, 0.44f ),
			FieldMaterial = GameMaterials.Concrete,
			TransitionColor = new Color( 0.62f, 0.56f, 0.48f ),
			TransitionMaterial = GameMaterials.Concrete,
			HorizonSky = new Color( 0.92f, 0.70f, 0.38f ),
			HorizonGround = new Color( 0.48f, 0.44f, 0.38f ),
		},
		MapTheme.Industrial => new()
		{
			FieldColor = new Color( 0.50f, 0.46f, 0.42f ),
			FieldMaterial = GameMaterials.Concrete,
			TransitionColor = new Color( 0.56f, 0.50f, 0.44f ),
			TransitionMaterial = GameMaterials.Concrete,
			HorizonSky = new Color( 0.78f, 0.60f, 0.42f ),
			HorizonGround = new Color( 0.42f, 0.38f, 0.34f ),
		},
		MapTheme.ParkingGarage => new()
		{
			FieldColor = new Color( 0.40f, 0.40f, 0.42f ),
			FieldMaterial = GameMaterials.Concrete,
			TransitionColor = new Color( 0.46f, 0.46f, 0.48f ),
			TransitionMaterial = GameMaterials.Concrete,
			HorizonSky = new Color( 0.62f, 0.58f, 0.54f ),
			HorizonGround = new Color( 0.36f, 0.36f, 0.38f ),
		},
		_ => new()
		{
			FieldColor = new Color( 0.42f, 0.84f, 0.10f ),
			FieldMaterial = GameMaterials.Grass,
			TransitionColor = new Color( 0.46f, 0.88f, 0.12f ),
			TransitionMaterial = GameMaterials.Grass,
			HorizonSky = new Color( 0.98f, 0.76f, 0.36f ),
			HorizonGround = new Color( 0.36f, 0.68f, 0.10f ),
		},
	};
}
