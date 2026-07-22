namespace Offshore;

public sealed class FishDefinition
{
	public string Id;
	public string Name;
	public string Description;
	public string Sprite;
	public Rarity Rarity;
	public FishBehavior Behavior;
	public float MinDistance;
	public float MaxDistance;
	public float PreferredDistance;
	public float MinDepth;
	public float MaxDepth;
	public float PreferredLureDepth;
	public DayPhase PreferredTime;
	public WeatherType PreferredWeather;
	public float PreferredTemp = 18f;
	public float PreferredClarity = 0.7f;
	public string PreferredBait;
	public string SecondaryBait;
	public string PreferredSeabed = "sand";
	public string PreferredStructure = "open";
	public float SurfaceAffinity;
	public float MoonlightAffinity;
	public float WindAffinity;
	public float RainAffinity;
	public float HotspotAffinity = 1f;
	public float NoiseSensitivity = 0.5f;
	public int SchoolSize = 1;
	public float Aggression = 0.4f;
	public int BaseValue;
	public float MinLength;
	public float MaxLength;
	public float MinWeight;
	public float MaxWeight;
	public float Stamina = 40f;
	public float PullForce = 30f;
	public string FightPattern = "steady";
	public float HookDifficulty = 0.4f;
	public float LineVisibilitySensitivity = 0.5f;
}

public sealed class BaitDefinition
{
	public string Id;
	public string Name;
	public string Description;
	public string Icon;
	public int Price;
	public int BundleCount = 5;
	public float MinDepth;
	public float MaxDepth = 200f;
	public float ScentRange = 1f;
	public float AttractionSpeed = 1f;
	public float Durability = 1f;
	public float WeatherBonus;
	public float NightBonus;
	public float ClarityBonus;
	public float PredatorBonus;
	public float RareAffinity;
	public string[] PrimaryTargets = Array.Empty<string>();
	public string[] SecondaryTargets = Array.Empty<string>();
}

public sealed class RodDefinition
{
	public string Id;
	public string Name;
	public string Description;
	public string Icon;
	public int Price;
	public float CastDistance = 1f;
	public float CastPower = 1f;
	public float CastAccuracy = 1f;
	public float MaxTension = 60f;
	public float PullControl = 1f;
	public float TensionRecovery = 1f;
	public float StaminaDrain = 1f;
	public float MaxFishWeight = 20f;
	public float BendSpeed = 1f;
	public float HookPower = 1f;
	public float Responsiveness = 1f;
	public float WeatherStability = 1f;
}

public sealed class ReelDefinition
{
	public string Id;
	public string Name;
	public string Description;
	public string Icon;
	public int Price;
	public float ReelSpeed = 1f;
	public float ReelAccel = 1f;
	public float SlackRecovery = 1f;
	public float MaxDrag = 50f;
	public float StaminaDrain = 1f;
	public float TensionRecovery = 1f;
	public float RetrievalDepth = 1f;
	public float Smoothness = 1f;
}

public sealed class HookDefinition
{
	public string Id;
	public string Name;
	public string Description;
	public string Icon;
	public int Price;
	public float HookSuccess = 0.7f;
	public float HookWindow = 0.7f;
	public float EscapeChance = 0.25f;
	public float MaxFishSize = 20f;
	public float AggressiveRetention = 0.5f;
	public float SnagResistance = 0.5f;
	public float BaitRetention = 0.7f;
	public float SmallFishPenalty;
	public float LargeFishBonus;
}

public sealed class LineDefinition
{
	public string Id;
	public string Name;
	public string Description;
	public string Icon;
	public int Price;
	public float MaxTension = 55f;
	public float Abrasion = 0.5f;
	public float Stretch = 0.5f;
	public float CastDistance = 1f;
	public float Visibility = 0.6f;
	public float SlackRecovery = 1f;
	public float Sensitivity = 1f;
	public float SnagResistance = 0.5f;
	public float DeepCapability = 0.5f;
}

public sealed class BoatDefinition
{
	public string Id;
	public string Name;
	public string Description;
	public string Sprite;
	public int Price;
	public float TopSpeed;
	public float Acceleration;
	public float Braking;
	public float FuelCapacity;
	public float FuelEfficiency;
	public float MaxRange;
	public int Storage;
	public float MaxDepth;
	public float Stability;
	public float WaveResponse;
	public float WindResponse;
	public float CastAccuracy;
	public float CastDistanceMod;
	public float WeatherResistance;
	public float TowPenalty;
	public Vector2 PlayerAnchor = new( 8, 18 );
	public Vector2 BoardAnchor = new( -20, 0 );
	public Vector2 RodAnchor = new( 22, 22 );
	public Vector2 LureLaunch = new( 28, 8 );
	public Vector2 WakeAnchor = new( -30, 0 );
	public Vector2 EngineAnchor = new( -26, 4 );
	public Vector2 Size = new( 140, 70 );
}

public sealed class ObjectiveDefinition
{
	public string Id;
	public string Title;
	public string Description;
	public string EventKey;
	public int Target = 1;
}

public sealed class CaughtFish
{
	public string SpeciesId;
	public string SpeciesName;
	public Rarity Rarity;
	public float Length;
	public float Weight;
	public int Value;
	public float Quality = 1f;
	public bool PersonalBest;
	public bool NewSpecies;
}
