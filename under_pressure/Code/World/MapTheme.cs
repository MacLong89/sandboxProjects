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
	/// <summary>Dockside asphalt with dark harbor water beyond the pad.</summary>
	Waterfront,
	/// <summary>Snow-covered clearing ringed by frosted pines.</summary>
	Snowfield,
	/// <summary>Enclosed tunnel/bunker: dark shell, no sky.</summary>
	Underground,
	/// <summary>Enclosed room: lighter shell, no sky.</summary>
	Interior,
	/// <summary>High roof deck — dark void below, city silhouette in the distance.</summary>
	Rooftop,
	/// <summary>Night highway: dark asphalt, sparse tree line.</summary>
	Highway,
	/// <summary>Concrete dam crest with reservoir water beyond.</summary>
	Dam,
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
		MapTheme.Waterfront => new()
		{
			FieldColor = new Color( 0.42f, 0.44f, 0.47f ),
			FieldMaterial = GameMaterials.Concrete,
			TransitionColor = new Color( 0.48f, 0.50f, 0.52f ),
			TransitionMaterial = GameMaterials.Concrete,
			HorizonSky = new Color( 0.56f, 0.62f, 0.70f ),
			HorizonGround = new Color( 0.30f, 0.34f, 0.40f ),
		},
		MapTheme.Snowfield => new()
		{
			FieldColor = new Color( 0.90f, 0.93f, 0.97f ),
			FieldMaterial = GameMaterials.Concrete,
			TransitionColor = new Color( 0.85f, 0.89f, 0.94f ),
			TransitionMaterial = GameMaterials.Concrete,
			HorizonSky = new Color( 0.74f, 0.80f, 0.90f ),
			HorizonGround = new Color( 0.80f, 0.85f, 0.92f ),
		},
		MapTheme.Underground => new()
		{
			FieldColor = new Color( 0.14f, 0.14f, 0.16f ),
			FieldMaterial = GameMaterials.Concrete,
			TransitionColor = new Color( 0.18f, 0.18f, 0.20f ),
			TransitionMaterial = GameMaterials.Concrete,
			HorizonSky = new Color( 0.08f, 0.08f, 0.10f ),
			HorizonGround = new Color( 0.12f, 0.12f, 0.14f ),
		},
		MapTheme.Interior => new()
		{
			FieldColor = new Color( 0.34f, 0.34f, 0.37f ),
			FieldMaterial = GameMaterials.Concrete,
			TransitionColor = new Color( 0.38f, 0.38f, 0.41f ),
			TransitionMaterial = GameMaterials.Concrete,
			HorizonSky = new Color( 0.22f, 0.22f, 0.26f ),
			HorizonGround = new Color( 0.26f, 0.26f, 0.30f ),
		},
		MapTheme.Rooftop => new()
		{
			FieldColor = new Color( 0.10f, 0.11f, 0.14f ),
			FieldMaterial = GameMaterials.Concrete,
			TransitionColor = new Color( 0.14f, 0.15f, 0.18f ),
			TransitionMaterial = GameMaterials.Concrete,
			HorizonSky = new Color( 0.36f, 0.38f, 0.48f ),
			HorizonGround = new Color( 0.16f, 0.17f, 0.22f ),
		},
		MapTheme.Highway => new()
		{
			FieldColor = new Color( 0.20f, 0.21f, 0.23f ),
			FieldMaterial = GameMaterials.Concrete,
			TransitionColor = new Color( 0.24f, 0.25f, 0.27f ),
			TransitionMaterial = GameMaterials.Concrete,
			HorizonSky = new Color( 0.18f, 0.20f, 0.30f ),
			HorizonGround = new Color( 0.10f, 0.12f, 0.16f ),
		},
		MapTheme.Dam => new()
		{
			FieldColor = new Color( 0.52f, 0.52f, 0.54f ),
			FieldMaterial = GameMaterials.Concrete,
			TransitionColor = new Color( 0.46f, 0.47f, 0.50f ),
			TransitionMaterial = GameMaterials.Concrete,
			HorizonSky = new Color( 0.42f, 0.52f, 0.58f ),
			HorizonGround = new Color( 0.28f, 0.34f, 0.38f ),
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
