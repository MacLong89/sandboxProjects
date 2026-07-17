namespace OffshoreFishing.Core;

public sealed class GameState
{
	public int SchemaVersion { get; set; } = SaveSchema.CurrentVersion;
	public long Seed { get; set; }
	public double PlayedSeconds { get; set; }
	public DateTimeOffset LastSavedUtc { get; set; } = DateTimeOffset.UtcNow;
	public GameMode Mode { get; set; } = GameMode.Dock;
	public int Gold { get; set; }
	public int Day { get; set; } = 1;
	public float TimeOfDayHours { get; set; } = 8.5f;
	public float WindKts { get; set; } = 5f;
	public string WeatherId { get; set; } = "sunny";

	public string CurrentZoneId { get; set; } = "harbor";
	public float BoatDistanceM { get; set; }
	public float BoatDepthM { get; set; }
	public bool OnBoat { get; set; }
	public float DockPlayerX { get; set; } = 120f;

	public string EquippedRodId { get; set; } = "rod_starter";
	public string EquippedSpoolId { get; set; } = "spool_basic";
	public string EquippedHookId { get; set; } = "hook_basic";
	public string EquippedBaitId { get; set; } = "bait_worms";
	public string OwnedBoatId { get; set; } = "boat_skiff";

	public List<string> OwnedItemIds { get; set; } = new();
	public Dictionary<string, int> Inventory { get; set; } = new();
	public List<CaughtFish> Hold { get; set; } = new();
	public List<string> UnlockedZoneIds { get; set; } = new() { "harbor" };
	public List<string> OwnedHiredBoatIds { get; set; } = new();
	public Dictionary<string, double> HiredBoatTripTimers { get; set; } = new();

	public Dictionary<string, FishLogEntry> FishLog { get; set; } = new();
	public List<string> CompletedObjectiveIds { get; set; } = new();
	public string ActiveObjectiveId { get; set; }
	public int ActiveObjectiveProgress { get; set; }

	public int TotalCatches { get; set; }
	public int TotalGoldEarned { get; set; }
	public float FarthestDistanceM { get; set; }
	public bool TutorialFirstCatchDone { get; set; }
	public bool TutorialFirstSaleDone { get; set; }
	public bool EndingReached { get; set; }

	public FishingSession Fishing { get; set; } = new();
	public SettingsState Settings { get; set; } = new();

	public int CountItem( string id ) => Inventory.TryGetValue( id, out var n ) ? n : 0;

	public void AddItem( string id, int count = 1 )
	{
		if ( !OwnedItemIds.Contains( id ) )
			OwnedItemIds.Add( id );
		Inventory[id] = CountItem( id ) + count;
	}

	public bool TryConsumeItem( string id, int count = 1 )
	{
		var have = CountItem( id );
		if ( have < count ) return false;
		Inventory[id] = have - count;
		if ( Inventory[id] <= 0 ) Inventory.Remove( id );
		return true;
	}
}

public sealed class SettingsState
{
	public float MasterVolume { get; set; } = 1f;
	public float MusicVolume { get; set; } = 0.7f;
	public float SfxVolume { get; set; } = 1f;
	public bool ShowTutorials { get; set; } = true;
}

public sealed class CaughtFish
{
	public string InstanceId { get; set; }
	public string FishId { get; set; }
	public float SizeCm { get; set; }
	public float WeightKg { get; set; }
	public Rarity Rarity { get; set; }
	public float Quality { get; set; }
	public int Worth { get; set; }
	public string ZoneId { get; set; }
	public DateTimeOffset CaughtAtUtc { get; set; }
}

public sealed class FishLogEntry
{
	public string FishId { get; set; }
	public int TimesCaught { get; set; }
	public float BestCm { get; set; }
	public float BestKg { get; set; }
	public int BestWorth { get; set; }
	public Rarity BestRarity { get; set; }
	public DateTimeOffset FirstCaughtUtc { get; set; }
}

public sealed class FishingSession
{
	public FishingPhase Phase { get; set; } = FishingPhase.Idle;
	public float AimAngle { get; set; } = -0.6f;
	public float CastCharge { get; set; }
	public float HookX { get; set; }
	public float HookDepthM { get; set; }
	public float LineTension { get; set; } = 0.5f;
	public float SafeZoneCenter { get; set; } = 0.5f;
	public float SafeZoneWidth { get; set; } = 0.28f;
	public float ReelProgress { get; set; }
	public float BiteTimer { get; set; }
	public float BiteWindowRemaining { get; set; }
	public float FightTimer { get; set; }
	public float FishStamina { get; set; }
	public string PendingFishId { get; set; }
	public CaughtFish PendingCatch { get; set; }
	public string StatusText { get; set; } = "Ready to cast";
	public bool ReelingHeld { get; set; }
}
