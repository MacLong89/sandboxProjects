namespace Offshore;

public enum GamePhase
{
	MainMenu,
	Dock,
	Shop,
	Selling,
	Boarding,
	Sailing,
	Casting,
	WaitingBite,
	Hooking,
	Reeling,
	CatchResult,
	FishLog,
	Objectives,
	Pause,
	Settings,
	EmergencyTow
}

public enum Rarity
{
	Common,
	Uncommon,
	Rare,
	Epic,
	Legendary
}

public enum FishBehavior
{
	Schooling,
	Solitary,
	Territorial,
	BottomFeeder,
	SurfaceFeeder,
	AmbushPredator,
	ActivePredator,
	Skittish,
	Aggressive,
	Nocturnal,
	DeepWater,
	StructureOriented
}

public enum WeatherType
{
	Clear,
	PartlyCloudy,
	Cloudy,
	Overcast,
	LightFog,
	HeavyFog,
	LightRain,
	HeavyRain,
	Windy,
	Thunderstorm
}

public enum DayPhase
{
	PreDawn,
	Sunrise,
	Morning,
	Midday,
	Afternoon,
	GoldenHour,
	Sunset,
	Dusk,
	Night
}

public enum ShopTab
{
	Bait,
	Rods,
	Hooks,
	Reels,
	Lines,
	Boats
}

public enum InteractPrompt
{
	None,
	EnterShop,
	BoardBoat,
	DockFish,
	DockBoat,
	SellCatch,
	Refuel,
	Cast,
	Hook,
	Reel,
	StopCast,
	OpenMap
}
