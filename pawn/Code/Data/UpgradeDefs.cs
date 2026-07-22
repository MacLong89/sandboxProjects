namespace PawnShop;

public enum UpgradeId
{
	DisplayWall,       // +4 display slots (extra wall shelf)
	DisplayFloor,      // +4 display slots (floor stands)
	Storage1,          // +8 backroom capacity
	Storage2,          // +12 backroom capacity
	Lighting,          // better buyer interest + inspection confidence
	SecurityCamera,    // reduces theft, may recover stolen goods
	AlarmSystem,       // reduces burglary/theft losses
	BetterCounter,     // faster service, +patience
	RepairBench,       // unlocks repairs
	AdvancedRepair,    // unlocks advanced repairs, better success chance
	ReferenceComputer, // faster/cheaper research
	CleanStore,        // reputation trickle each day
	AdSign,            // more customer traffic
	PremiumCase,       // +2 premium display slots, attracts wealthy buyers
}

public sealed class UpgradeDef
{
	public UpgradeId Id { get; init; }
	public string Name { get; init; }
	public string Description { get; init; }
	public int Cost { get; init; }
	public string Icon { get; init; } = "upgrade";
	/// <summary>Upgrade that must be owned first, if any.</summary>
	public UpgradeId? Requires { get; init; }
}

public static class UpgradeCatalog
{
	public static readonly List<UpgradeDef> All = new()
	{
		new UpgradeDef { Id = UpgradeId.DisplayWall, Name = "Extra Wall Shelving", Cost = 500, Icon = "shelves",
			Description = "+4 display slots on a new wall shelf." },
		new UpgradeDef { Id = UpgradeId.DisplayFloor, Name = "Floor Stands", Cost = 650, Icon = "storefront",
			Description = "+4 display slots on sturdy floor stands." },
		new UpgradeDef { Id = UpgradeId.Storage1, Name = "Backroom Racking", Cost = 350, Icon = "inventory",
			Description = "+8 backroom storage capacity." },
		new UpgradeDef { Id = UpgradeId.Storage2, Name = "Mezzanine Storage", Cost = 800, Icon = "warehouse", Requires = UpgradeId.Storage1,
			Description = "+12 more backroom storage capacity." },
		new UpgradeDef { Id = UpgradeId.Lighting, Name = "Gallery Lighting", Cost = 450, Icon = "light",
			Description = "Warm display lighting. Buyers browse longer and pay closer to sticker." },
		new UpgradeDef { Id = UpgradeId.SecurityCamera, Name = "Security Cameras", Cost = 550, Icon = "videocam",
			Description = "Halves theft risk and sometimes recovers stolen merchandise." },
		new UpgradeDef { Id = UpgradeId.AlarmSystem, Name = "Alarm System", Cost = 700, Icon = "notifications_active", Requires = UpgradeId.SecurityCamera,
			Description = "Nearly eliminates theft and protects against overnight burglary." },
		new UpgradeDef { Id = UpgradeId.BetterCounter, Name = "Walnut Service Counter", Cost = 600, Icon = "countertops",
			Description = "A proper counter. Customers wait 25% longer before losing patience." },
		new UpgradeDef { Id = UpgradeId.RepairBench, Name = "Repair Bench", Cost = 500, Icon = "handyman",
			Description = "Unlocks basic repairs in the backroom." },
		new UpgradeDef { Id = UpgradeId.AdvancedRepair, Name = "Specialist Tooling", Cost = 900, Icon = "precision_manufacturing", Requires = UpgradeId.RepairBench,
			Description = "Unlocks advanced repairs and improves repair success chance." },
		new UpgradeDef { Id = UpgradeId.ReferenceComputer, Name = "Reference Computer", Cost = 650, Icon = "computer",
			Description = "Research finishes same-day and costs half as much." },
		new UpgradeDef { Id = UpgradeId.CleanStore, Name = "Deep Clean & Paint", Cost = 400, Icon = "cleaning_services",
			Description = "A spotless store. Small reputation gain every day." },
		new UpgradeDef { Id = UpgradeId.AdSign, Name = "Neon Street Sign", Cost = 750, Icon = "campaign",
			Description = "BRASS BUCK PAWN in glowing letters. +35% customer traffic." },
		new UpgradeDef { Id = UpgradeId.PremiumCase, Name = "Premium Glass Case", Cost = 1000, Icon = "diamond", Requires = UpgradeId.Lighting,
			Description = "+2 premium display slots that attract wealthy buyers." },
	};

	private static Dictionary<UpgradeId, UpgradeDef> _byId;
	public static UpgradeDef Get( UpgradeId id )
	{
		_byId ??= All.ToDictionary( d => d.Id );
		return _byId[id];
	}
}
