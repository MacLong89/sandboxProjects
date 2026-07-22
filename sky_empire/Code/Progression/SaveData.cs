namespace SkyEmpire;

public enum QuestKind { EarnCash, CollectOrbs, GoldenOrbs, BuyThings }

public sealed class QuestData
{
	public QuestKind Kind { get; set; }
	public double Target { get; set; }
	public double Progress { get; set; }
	public bool Claimed { get; set; }

	public bool Complete => Progress >= Target;
	public float Fraction => Target <= 0 ? 0f : (float)Math.Clamp( Progress / Target, 0, 1 );

	public string Title => Kind switch
	{
		QuestKind.EarnCash => $"Earn {Balance.Fmt( Target )} cash",
		QuestKind.CollectOrbs => $"Collect {Target:0} orbs",
		QuestKind.GoldenOrbs => $"Collect {Target:0} golden orbs",
		_ => $"Buy {Target:0} island upgrades",
	};
}

public sealed class SaveData
{
	public int Version { get; set; } = 1;

	// Currencies
	public double Cash { get; set; }
	public int Gems { get; set; }
	public double LifetimeCash { get; set; }

	// Island
	public List<string> Purchased { get; set; } = new();
	public int Rebirths { get; set; }
	public int LifetimePurchases { get; set; }

	// Stats
	public long OrbsCollected { get; set; }
	public long GoldenOrbs { get; set; }

	// Boosts (unix expiry so Overdrive survives a relaunch)
	public long OverdriveUntilUnix { get; set; }

	// Retention
	public string DailyDate { get; set; } = "";
	public List<QuestData> DailyQuests { get; set; } = new();
	public int StreakCount { get; set; }
	public string LastLoginDay { get; set; } = "";
	public long LastSeenUnix { get; set; }
	public int ChestsClaimed { get; set; }

	// Onboarding
	public int MilestoneIndex { get; set; }
	public bool SeenWelcome { get; set; }
	public bool HideTutorialTips { get; set; }
	public List<string> TutorialTipsShown { get; set; } = new();
}
