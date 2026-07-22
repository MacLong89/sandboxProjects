namespace PawnShop;

/// <summary>
/// A discoverable flaw (or positive trait) that can live on an item instance.
/// Hidden defects need the listed tool to be found; visible ones only need eyes.
/// </summary>
public sealed class DefectDef
{
	public string Id { get; init; }
	public string Name { get; init; }
	public string Description { get; init; }
	/// <summary>Fraction of value removed (negative values are value-ADDING traits).</summary>
	public float ValuePenalty { get; init; }
	/// <summary>Tool required to discover it. Eyes = visible at a glance.</summary>
	public InspectTool RevealTool { get; init; } = InspectTool.Eyes;
	/// <summary>True when this trait is proof the item is counterfeit.</summary>
	public bool CounterfeitSign { get; init; }
	/// <summary>True when this trait suggests the item may be stolen.</summary>
	public bool StolenSign { get; init; }
	/// <summary>Repairing the item removes this defect.</summary>
	public bool Repairable { get; init; }
	public string Icon { get; init; } = "report";

	public bool IsPositive => ValuePenalty < 0f;
}

/// <summary>All defect / trait definitions, keyed by id.</summary>
public static class DefectCatalog
{
	public static readonly List<DefectDef> All = new()
	{
		// --- Visible damage (eyes) ---
		new DefectDef { Id = "crack", Name = "Hairline Crack", Description = "A thin crack runs across the surface.", ValuePenalty = 0.18f, Repairable = true, Icon = "broken_image" },
		new DefectDef { Id = "scratches", Name = "Deep Scratches", Description = "Heavy scratching across the finish.", ValuePenalty = 0.12f, Repairable = true, Icon = "texture" },
		new DefectDef { Id = "missing_screw", Name = "Missing Screws", Description = "Several fasteners are missing.", ValuePenalty = 0.08f, Repairable = true, Icon = "handyman" },
		new DefectDef { Id = "dent", Name = "Dented Body", Description = "The casing has taken a serious knock.", ValuePenalty = 0.14f, Repairable = true, Icon = "compress" },
		new DefectDef { Id = "missing_part", Name = "Missing Accessory", Description = "The charger / strap / case is gone.", ValuePenalty = 0.10f, Repairable = true, Icon = "extension_off" },
		new DefectDef { Id = "rust", Name = "Rust Spots", Description = "Orange rust is blooming on the metal.", ValuePenalty = 0.15f, Repairable = true, Icon = "water_drop" },
		new DefectDef { Id = "mold", Name = "Mold Smell", Description = "Something in here has been damp for a long time.", ValuePenalty = 0.12f, Repairable = false, Icon = "air" },
		new DefectDef { Id = "worn_grip", Name = "Worn Grip", Description = "The handle is worn smooth from use.", ValuePenalty = 0.07f, Repairable = false, Icon = "back_hand" },

		// --- Hidden (magnifier) ---
		new DefectDef { Id = "fine_crack", Name = "Fine Fracture", Description = "A fracture only visible under magnification.", ValuePenalty = 0.16f, RevealTool = InspectTool.Magnifier, Repairable = true, Icon = "search" },
		new DefectDef { Id = "repaired_seam", Name = "Repaired Seam", Description = "Someone glued this back together — carefully.", ValuePenalty = 0.14f, RevealTool = InspectTool.Magnifier, Icon = "healing" },
		new DefectDef { Id = "fake_print", Name = "Blurry Logo Print", Description = "The brand mark is smudged and off-center. Fake.", ValuePenalty = 0f, RevealTool = InspectTool.Magnifier, CounterfeitSign = true, Icon = "verified_user" },
		new DefectDef { Id = "wrong_font", Name = "Wrong Serial Font", Description = "This serial number uses the wrong typeface. Fake.", ValuePenalty = 0f, RevealTool = InspectTool.Magnifier, CounterfeitSign = true, Icon = "pin" },
		new DefectDef { Id = "maker_mark", Name = "Rare Maker Mark", Description = "A tiny stamp from a famous workshop!", ValuePenalty = -0.30f, RevealTool = InspectTool.Magnifier, Icon = "star" },

		// --- Hidden (electronics tester) ---
		new DefectDef { Id = "dead_battery", Name = "Dead Battery", Description = "Won't hold any charge at all.", ValuePenalty = 0.15f, RevealTool = InspectTool.ElectronicsTester, Repairable = true, Icon = "battery_alert" },
		new DefectDef { Id = "water_damage", Name = "Water Damage", Description = "Corrosion inside — this has been swimming.", ValuePenalty = 0.30f, RevealTool = InspectTool.ElectronicsTester, Repairable = true, Icon = "water" },
		new DefectDef { Id = "board_fault", Name = "Faulty Board", Description = "Intermittent fault on the main board.", ValuePenalty = 0.25f, RevealTool = InspectTool.ElectronicsTester, Repairable = true, Icon = "memory" },
		new DefectDef { Id = "gutted", Name = "Gutted Shell", Description = "The insides have been stripped out. It's an empty shell.", ValuePenalty = 0f, RevealTool = InspectTool.ElectronicsTester, CounterfeitSign = true, Icon = "inbox" },

		// --- Hidden (metal tester) ---
		new DefectDef { Id = "plated", Name = "Gold Plated Only", Description = "A thin plating over base metal — not solid gold.", ValuePenalty = 0f, RevealTool = InspectTool.MetalTester, CounterfeitSign = true, Icon = "layers" },
		new DefectDef { Id = "wrong_alloy", Name = "Wrong Alloy", Description = "The metal composition doesn't match the maker's spec.", ValuePenalty = 0f, RevealTool = InspectTool.MetalTester, CounterfeitSign = true, Icon = "science" },
		new DefectDef { Id = "pure_metal", Name = "Higher Purity", Description = "Purer metal than the stamp claims. Worth more!", ValuePenalty = -0.20f, RevealTool = InspectTool.MetalTester, Icon = "auto_awesome" },

		// --- Hidden (gem tester) ---
		new DefectDef { Id = "glass_gem", Name = "Glass Stone", Description = "That sparkling gem is cut glass.", ValuePenalty = 0f, RevealTool = InspectTool.GemTester, CounterfeitSign = true, Icon = "diamond" },
		new DefectDef { Id = "lab_gem", Name = "Lab-Grown Stone", Description = "Synthetic stone — real, but worth much less.", ValuePenalty = 0.35f, RevealTool = InspectTool.GemTester, Icon = "biotech" },
		new DefectDef { Id = "flawless_gem", Name = "Flawless Stone", Description = "An exceptionally clean stone. A real find.", ValuePenalty = -0.25f, RevealTool = InspectTool.GemTester, Icon = "diamond" },

		// --- Hidden (UV light) ---
		new DefectDef { Id = "uv_repair", Name = "Hidden Restoration", Description = "Under UV, whole sections glow — heavy restoration.", ValuePenalty = 0.20f, RevealTool = InspectTool.UvLight, Icon = "flashlight_on" },
		new DefectDef { Id = "altered_label", Name = "Altered Label", Description = "The label has been chemically altered.", ValuePenalty = 0f, RevealTool = InspectTool.UvLight, CounterfeitSign = true, Icon = "label_off" },
		new DefectDef { Id = "security_mark", Name = "Security Marking", Description = "Someone's postcode is etched on it. That's an ownership mark.", ValuePenalty = 0f, RevealTool = InspectTool.UvLight, StolenSign = true, Icon = "policy" },
		new DefectDef { Id = "uv_signature", Name = "Hidden Signature", Description = "A hidden artist's signature glows under UV!", ValuePenalty = -0.35f, RevealTool = InspectTool.UvLight, Icon = "draw" },

		// --- Hidden (database) ---
		new DefectDef { Id = "scratched_serial", Name = "Scratched Serial", Description = "The serial number has been ground off.", ValuePenalty = 0.10f, RevealTool = InspectTool.Eyes, StolenSign = true, Icon = "tag" },
		new DefectDef { Id = "flagged_serial", Name = "Flagged Serial", Description = "This serial shows up on the stolen goods register.", ValuePenalty = 0f, RevealTool = InspectTool.Database, StolenSign = true, Icon = "gpp_bad" },
		new DefectDef { Id = "recall_model", Name = "Recalled Model", Description = "This model was recalled — hard to resell.", ValuePenalty = 0.22f, RevealTool = InspectTool.Database, Icon = "campaign" },
		new DefectDef { Id = "rare_variant", Name = "Rare Variant", Description = "A limited production run. Collectors will pay a premium.", ValuePenalty = -0.40f, RevealTool = InspectTool.Database, Icon = "workspace_premium" },
	};

	private static Dictionary<string, DefectDef> _byId;
	public static DefectDef Get( string id )
	{
		_byId ??= All.ToDictionary( d => d.Id );
		return id is not null && _byId.TryGetValue( id, out var d ) ? d : null;
	}
}
