namespace UnderPressure;

/// <summary>How many ambient citizens to keep around a job, keyed by environment.</summary>
public readonly struct AmbientPedestrianDensity
{
	public int MaxActive { get; init; }
	public int InitialBurst { get; init; }
	public float SpawnIntervalMin { get; init; }
	public float SpawnIntervalMax { get; init; }
	public float WalkSpeedMin { get; init; }
	public float WalkSpeedMax { get; init; }

	public static AmbientPedestrianDensity For( MapTheme theme ) => theme switch
	{
		MapTheme.UrbanPlaza => new()
		{
			MaxActive = 10,
			InitialBurst = 4,
			SpawnIntervalMin = 2.8f,
			SpawnIntervalMax = 6.5f,
			WalkSpeedMin = 42f,
			WalkSpeedMax = 58f,
		},
		MapTheme.Storefront => new()
		{
			MaxActive = 9,
			InitialBurst = 3,
			SpawnIntervalMin = 3.2f,
			SpawnIntervalMax = 7f,
			WalkSpeedMin = 40f,
			WalkSpeedMax = 56f,
		},
		MapTheme.Alley => new()
		{
			MaxActive = 7,
			InitialBurst = 3,
			SpawnIntervalMin = 3.8f,
			SpawnIntervalMax = 8f,
			WalkSpeedMin = 44f,
			WalkSpeedMax = 62f,
		},
		MapTheme.ParkingGarage => new()
		{
			MaxActive = 6,
			InitialBurst = 2,
			SpawnIntervalMin = 4.5f,
			SpawnIntervalMax = 9f,
			WalkSpeedMin = 38f,
			WalkSpeedMax = 52f,
		},
		MapTheme.GasStation => new()
		{
			MaxActive = 5,
			InitialBurst = 2,
			SpawnIntervalMin = 5f,
			SpawnIntervalMax = 10f,
			WalkSpeedMin = 40f,
			WalkSpeedMax = 54f,
		},
		MapTheme.Industrial => new()
		{
			MaxActive = 4,
			InitialBurst = 1,
			SpawnIntervalMin = 7f,
			SpawnIntervalMax = 13f,
			WalkSpeedMin = 36f,
			WalkSpeedMax = 48f,
		},
		MapTheme.Suburban => new()
		{
			MaxActive = 3,
			InitialBurst = 1,
			SpawnIntervalMin = 9f,
			SpawnIntervalMax = 16f,
			WalkSpeedMin = 34f,
			WalkSpeedMax = 46f,
		},
		MapTheme.Backyard => new()
		{
			MaxActive = 2,
			InitialBurst = 0,
			SpawnIntervalMin = 14f,
			SpawnIntervalMax = 24f,
			WalkSpeedMin = 30f,
			WalkSpeedMax = 42f,
		},
		_ => new()
		{
			MaxActive = 3,
			InitialBurst = 1,
			SpawnIntervalMin = 9f,
			SpawnIntervalMax = 16f,
			WalkSpeedMin = 34f,
			WalkSpeedMax = 46f,
		},
	};
}
