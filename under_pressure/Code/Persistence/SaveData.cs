namespace UnderPressure;

/// <summary>
/// Serializable snapshot of all persistent player progress. Bump <see cref="Version"/>
/// and migrate in <see cref="SaveManager"/> when the schema changes.
/// </summary>
public sealed class SaveData
{
	/// <summary>Current schema version. Loads older than this are wiped (see SaveManager).</summary>
	public const int CurrentVersion = 6;

	public int Version { get; set; } = CurrentVersion;

	public double Cash { get; set; }
	public double LifetimeEarned { get; set; }
	public double RunEarned { get; set; }

	public int JobIndex { get; set; }
	public int PrestigeLevel { get; set; }

	/// <summary>Upgrade id -> purchased level.</summary>
	public Dictionary<string, int> Upgrades { get; set; } = new();

	/// <summary>The tool the player currently has equipped (see ToolType).</summary>
	public string EquippedTool { get; set; } = "PressureWasher";

	/// <summary>Tools the player has bought from the van shop (see ToolType names). Starter tools
	/// are always owned regardless of this list.</summary>
	public List<string> OwnedTools { get; set; } = new() { "PressureWasher" };

	public long LastPlayedUnix { get; set; }
	public int DailyStreak { get; set; }
	public string LastDailyDate { get; set; } = "";

	/// <summary>True once local lifetime earnings have been pushed to cloud stats.</summary>
	public bool LeaderboardMigrated { get; set; }

	/// <summary>The fixer NPC has delivered the level-3 briefing conversation.</summary>
	public bool HitmanBriefingSeen { get; set; }

	/// <summary>Unlocks the classified gun in the van and contract targets on late jobs.</summary>
	public bool HitmanContractUnlocked { get; set; }

	/// <summary>Discovery ids the player has uncovered (persists across jobs).</summary>
	public List<string> DiscoveredSecrets { get; set; } = new();

	/// <summary>Experienced players can hide first-run coach tips (H toggles).</summary>
	public bool HideTutorialTips { get; set; }
	public List<string> TutorialTipsShown { get; set; } = new();

	/// <summary>Tutorial gates — player blasted at least one grime cell.</summary>
	public bool HasCleanedSomeDirt { get; set; }

	/// <summary>Tutorial gates — player opened the van locker or shop once.</summary>
	public bool HasOpenedVanOrShop { get; set; }

	/// <summary>Tutorial gates — pests defeated across all jobs.</summary>
	public int PestsKilled { get; set; }

	public bool HasDiscovery( string id ) => !string.IsNullOrWhiteSpace( id ) && DiscoveredSecrets.Contains( id );

	public void MarkDiscovery( string id )
	{
		if ( string.IsNullOrWhiteSpace( id ) || DiscoveredSecrets.Contains( id ) )
			return;
		DiscoveredSecrets.Add( id );
	}

	public int GetUpgrade( string id ) => Upgrades.TryGetValue( id, out var lvl ) ? lvl : 0;
	public void SetUpgrade( string id, int level ) => Upgrades[id] = level;
}
