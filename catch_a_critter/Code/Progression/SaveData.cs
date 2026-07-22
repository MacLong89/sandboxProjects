namespace CatchACritter;

public sealed class OwnedCritter
{
	public string Id { get; set; } = Guid.NewGuid().ToString( "N" )[..8];
	public string SpeciesId { get; set; }
	public bool Shiny { get; set; }
	public int Generation { get; set; }

	public SpeciesDef Def => SpeciesCatalog.Get( SpeciesId );
	public double IncomePerSecond
	{
		get
		{
			var def = Def;
			if ( def is null ) return 0;
			var v = def.BaseValue * Balance.SanctuaryIncomeShare;
			if ( Shiny ) v *= 3.0;
			return v * (1.0 + Generation * 0.15);
		}
	}
}

public sealed class BackpackItem
{
	public string SpeciesId { get; set; }
	public bool Shiny { get; set; }
}

public sealed class EggData
{
	public string SpeciesId { get; set; }
	public bool ShinyParent { get; set; }
	public int Generation { get; set; }
	public long StartUnix { get; set; }
	public double DurationSeconds { get; set; }

	public double SecondsLeft => Math.Max( 0, StartUnix + DurationSeconds - DateTimeOffset.UtcNow.ToUnixTimeSeconds() );
	public bool Ready => SecondsLeft <= 0;
}

public sealed class CodexEntry
{
	public int Caught { get; set; }
	public int ShinyCaught { get; set; }
	public int Bred { get; set; }
}

public enum QuestKind { CatchAny, CatchBiome, CatchRare, SellCoins, HatchEgg }

public sealed class QuestData
{
	public QuestKind Kind { get; set; }
	public int Param { get; set; }       // biome index / rarity index
	public double Target { get; set; }
	public double Progress { get; set; }
	public bool Claimed { get; set; }

	public bool Complete => Progress >= Target;
	public float Fraction => Target <= 0 ? 0f : (float)Math.Clamp( Progress / Target, 0, 1 );

	public string Title => Kind switch
	{
		QuestKind.CatchAny => $"Catch {Target:0} critters",
		QuestKind.CatchBiome => $"Catch {Target:0} in {BiomeCatalog.Get( (Biome)Param ).Name}",
		QuestKind.CatchRare => $"Catch {Target:0} {(Rarity)Param}+ critters",
		QuestKind.SellCoins => $"Earn {Balance.Fmt( Target )} coins selling",
		_ => "Hatch an egg",
	};
}

public sealed class SaveData
{
	public int Version { get; set; } = 1;

	// Currencies
	public double Coins { get; set; }
	public int Gems { get; set; }
	public double LifetimeCoins { get; set; }

	// Gear
	public int NetPower { get; set; }
	public int SpeedLevel { get; set; }
	public int BackpackLevel { get; set; }
	public int LuckLevel { get; set; }

	// World
	public List<string> UnlockedZones { get; set; } = new() { "Meadow" };

	// Inventory
	public List<BackpackItem> Backpack { get; set; } = new();
	public List<OwnedCritter> Sanctuary { get; set; } = new();
	public List<string> FollowerIds { get; set; } = new();
	public List<EggData> Eggs { get; set; } = new();

	// Collection
	public Dictionary<string, CodexEntry> Codex { get; set; } = new();
	public int LifetimeCatches { get; set; }
	public int LifetimeShinies { get; set; }
	public int SellCount { get; set; }

	// Prestige
	public int Crowns { get; set; }
	public int TalentPoints { get; set; }
	public Dictionary<string, int> Talents { get; set; } = new();

	// Retention
	public string DailyDate { get; set; } = "";
	public List<QuestData> DailyQuests { get; set; } = new();
	public int StreakCount { get; set; }
	public string LastLoginDay { get; set; } = "";
	public long LastSeenUnix { get; set; }

	// Onboarding
	public int MilestoneIndex { get; set; }
	public bool SeenWelcome { get; set; }
	public bool HideTutorialTips { get; set; }
	public List<string> TutorialTipsShown { get; set; } = new();
}
