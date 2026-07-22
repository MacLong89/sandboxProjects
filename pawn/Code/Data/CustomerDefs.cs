namespace PawnShop;

public enum Archetype
{
	DesperateSeller,
	ToughNegotiator,
	CluelessSeller,
	Collector,
	Scammer,
	WealthyBuyer,
	BargainHunter,
	PawnRegular,
	Regular,
	SuspiciousSeller,
}

public enum CustomerIntent
{
	Sell,
	Pawn,
	Buy,
	Redeem,
}

/// <summary>Static behavior profile for a customer archetype.</summary>
public sealed class ArchetypeDef
{
	public Archetype Id { get; init; }
	public string Label { get; init; }
	/// <summary>0..1 — how long they tolerate waiting / negotiating.</summary>
	public float Patience { get; init; } = 0.5f;
	/// <summary>0..1 — how accurately they know their item's value.</summary>
	public float Knowledge { get; init; } = 0.5f;
	/// <summary>0..1 — how badly they need money right now.</summary>
	public float Desperation { get; init; } = 0.3f;
	/// <summary>0..1 — likelihood they're honest about the item.</summary>
	public float Honesty { get; init; } = 0.8f;
	/// <summary>Multiplier applied to counterfeit chance of the item they bring.</summary>
	public float CounterfeitMult { get; init; } = 1f;
	/// <summary>Chance the item they bring is stolen.</summary>
	public float StolenChance { get; init; } = 0.02f;
	/// <summary>How far above true value they open when selling (1.0 = at value).</summary>
	public float AskMult { get; init; } = 1.3f;
	/// <summary>Fraction of true value they will grudgingly accept.</summary>
	public float FloorMult { get; init; } = 0.65f;
	/// <summary>Buyer budget multiplier vs typical stock value.</summary>
	public float BudgetMult { get; init; } = 1f;
	/// <summary>Weights for what this archetype walks in wanting.</summary>
	public float SellWeight { get; init; } = 1f;
	public float PawnWeight { get; init; } = 0.3f;
	public float BuyWeight { get; init; } = 0.5f;
	public string Blurb { get; init; } = "";
}

public static class ArchetypeCatalog
{
	public static readonly List<ArchetypeDef> All = new()
	{
		new ArchetypeDef { Id = Archetype.DesperateSeller, Label = "Desperate Seller", Patience = 0.35f, Knowledge = 0.35f, Desperation = 0.9f, Honesty = 0.85f,
			AskMult = 1.05f, FloorMult = 0.45f, SellWeight = 1.6f, PawnWeight = 0.8f, BuyWeight = 0.05f,
			Blurb = "Needs cash today and will take a low offer — but insulting offers sting." },
		new ArchetypeDef { Id = Archetype.ToughNegotiator, Label = "Tough Negotiator", Patience = 0.9f, Knowledge = 0.85f, Desperation = 0.15f, Honesty = 0.85f,
			AskMult = 1.55f, FloorMult = 0.85f, SellWeight = 1.2f, PawnWeight = 0.4f, BuyWeight = 0.3f,
			Blurb = "Knows the price guide by heart and starts high." },
		new ArchetypeDef { Id = Archetype.CluelessSeller, Label = "Clueless Seller", Patience = 0.6f, Knowledge = 0.08f, Desperation = 0.4f, Honesty = 0.9f,
			AskMult = 1.0f, FloorMult = 0.4f, SellWeight = 1.5f, PawnWeight = 0.3f, BuyWeight = 0.2f,
			Blurb = "Found it in the attic. Could ask double — or half." },
		new ArchetypeDef { Id = Archetype.Collector, Label = "Collector", Patience = 0.75f, Knowledge = 0.9f, Desperation = 0.1f, Honesty = 0.9f,
			AskMult = 1.35f, FloorMult = 0.8f, BudgetMult = 1.8f, SellWeight = 0.4f, PawnWeight = 0.1f, BuyWeight = 1.8f,
			Blurb = "Hunts rare pieces and pays premiums for the right find." },
		new ArchetypeDef { Id = Archetype.Scammer, Label = "Scammer", Patience = 0.4f, Knowledge = 0.75f, Desperation = 0.5f, Honesty = 0.05f,
			CounterfeitMult = 4.5f, StolenChance = 0.10f, AskMult = 1.45f, FloorMult = 0.55f, SellWeight = 1.8f, PawnWeight = 0.4f, BuyWeight = 0.05f,
			Blurb = "Smooth story, fast talker, and merchandise that won't survive a UV light." },
		new ArchetypeDef { Id = Archetype.WealthyBuyer, Label = "Wealthy Buyer", Patience = 0.55f, Knowledge = 0.7f, Desperation = 0.05f, Honesty = 0.95f,
			BudgetMult = 3.0f, SellWeight = 0.1f, PawnWeight = 0.05f, BuyWeight = 2.2f,
			Blurb = "Wants the best piece in the store and expects it to be genuine." },
		new ArchetypeDef { Id = Archetype.BargainHunter, Label = "Bargain Hunter", Patience = 0.7f, Knowledge = 0.6f, Desperation = 0.2f, Honesty = 0.9f,
			BudgetMult = 0.6f, SellWeight = 0.3f, PawnWeight = 0.1f, BuyWeight = 1.8f,
			Blurb = "Will haggle over anything and loves a marked-down shelf." },
		new ArchetypeDef { Id = Archetype.PawnRegular, Label = "Pawn Regular", Patience = 0.65f, Knowledge = 0.5f, Desperation = 0.6f, Honesty = 0.85f,
			AskMult = 1.1f, FloorMult = 0.5f, SellWeight = 0.3f, PawnWeight = 2.2f, BuyWeight = 0.1f,
			Blurb = "Pawns the same watch every month and always comes back for it. Usually." },
		new ArchetypeDef { Id = Archetype.Regular, Label = "Regular", Patience = 0.7f, Knowledge = 0.45f, Desperation = 0.3f, Honesty = 0.9f,
			AskMult = 1.2f, FloorMult = 0.6f, SellWeight = 1f, PawnWeight = 0.5f, BuyWeight = 0.8f,
			Blurb = "A neighborhood face who remembers how you treated them." },
		new ArchetypeDef { Id = Archetype.SuspiciousSeller, Label = "Suspicious Seller", Patience = 0.3f, Knowledge = 0.55f, Desperation = 0.75f, Honesty = 0.25f,
			StolenChance = 0.55f, AskMult = 0.85f, FloorMult = 0.35f, SellWeight = 2f, PawnWeight = 0.2f, BuyWeight = 0.02f,
			Blurb = "Selling a very nice bike, very cheap, very fast, no questions please." },
	};

	private static Dictionary<Archetype, ArchetypeDef> _byId;
	public static ArchetypeDef Get( Archetype id )
	{
		_byId ??= All.ToDictionary( d => d.Id );
		return _byId[id];
	}
}

/// <summary>A recurring, named customer with a fixed look and personality.</summary>
public sealed class NamedCustomerDef
{
	public string Id { get; init; }
	public string Name { get; init; }
	public Archetype Archetype { get; init; }
	public ItemCategory[] Favorites { get; init; } = Array.Empty<ItemCategory>();
	public Color ShirtColor { get; init; } = Color.White;
	public Color PantsColor { get; init; } = new( 0.25f, 0.28f, 0.35f );
	public Color SkinColor { get; init; } = new( 0.85f, 0.65f, 0.50f );
	public string Quirk { get; init; } = "";
}

public static class NamedCustomers
{
	public static readonly List<NamedCustomerDef> All = new()
	{
		new NamedCustomerDef { Id = "marge", Name = "Marge Whitlow", Archetype = Archetype.CluelessSeller, Favorites = new[]{ ItemCategory.Antiques, ItemCategory.Art },
			ShirtColor = new Color( 0.85f, 0.55f, 0.70f ), SkinColor = new Color( 0.92f, 0.76f, 0.62f ), Quirk = "Everything belonged to her late husband, apparently." },
		new NamedCustomerDef { Id = "dex", Name = "Dex Marlowe", Archetype = Archetype.Scammer, Favorites = new[]{ ItemCategory.Watches, ItemCategory.Jewelry },
			ShirtColor = new Color( 0.20f, 0.20f, 0.24f ), SkinColor = new Color( 0.75f, 0.58f, 0.45f ), Quirk = "Always 'just passing through town'." },
		new NamedCustomerDef { Id = "rosa", Name = "Rosa Quintero", Archetype = Archetype.ToughNegotiator, Favorites = new[]{ ItemCategory.Instruments },
			ShirtColor = new Color( 0.75f, 0.25f, 0.25f ), SkinColor = new Color( 0.70f, 0.50f, 0.38f ), Quirk = "Played bass in three touring bands. Will tell you about all of them." },
		new NamedCustomerDef { Id = "ernie", Name = "Ernie Paget", Archetype = Archetype.PawnRegular, Favorites = new[]{ ItemCategory.Watches, ItemCategory.Tools },
			ShirtColor = new Color( 0.35f, 0.50f, 0.35f ), SkinColor = new Color( 0.88f, 0.70f, 0.55f ), Quirk = "Pawns his father's watch on the 1st, redeems it on payday." },
		new NamedCustomerDef { Id = "vivian", Name = "Vivian Ashcroft", Archetype = Archetype.WealthyBuyer, Favorites = new[]{ ItemCategory.Jewelry, ItemCategory.Art, ItemCategory.Antiques },
			ShirtColor = new Color( 0.92f, 0.88f, 0.80f ), SkinColor = new Color( 0.90f, 0.74f, 0.60f ), Quirk = "Refers to the shop as 'this charming little place'." },
		new NamedCustomerDef { Id = "toby", Name = "Toby Finch", Archetype = Archetype.BargainHunter, Favorites = new[]{ ItemCategory.Gaming, ItemCategory.Electronics },
			ShirtColor = new Color( 0.30f, 0.55f, 0.85f ), SkinColor = new Color( 0.95f, 0.80f, 0.66f ), Quirk = "Knows the online price of everything, quotes it constantly." },
		new NamedCustomerDef { Id = "august", Name = "August Reyes", Archetype = Archetype.Collector, Favorites = new[]{ ItemCategory.Collectibles, ItemCategory.Memorabilia },
			ShirtColor = new Color( 0.55f, 0.35f, 0.75f ), SkinColor = new Color( 0.72f, 0.54f, 0.40f ), Quirk = "Hunting one specific tin robot to complete a shelf." },
		new NamedCustomerDef { Id = "pearl", Name = "Pearl Okafor", Archetype = Archetype.Regular, Favorites = new[]{ ItemCategory.Cameras, ItemCategory.Art },
			ShirtColor = new Color( 0.95f, 0.65f, 0.20f ), SkinColor = new Color( 0.45f, 0.32f, 0.24f ), Quirk = "Brings photos of her grandkids. All forty of them." },
		new NamedCustomerDef { Id = "sid", Name = "Sid Kowalski", Archetype = Archetype.SuspiciousSeller, Favorites = new[]{ ItemCategory.Sports, ItemCategory.Electronics },
			ShirtColor = new Color( 0.45f, 0.45f, 0.45f ), SkinColor = new Color( 0.85f, 0.68f, 0.54f ), Quirk = "Checks the door twice a minute." },
		new NamedCustomerDef { Id = "flora", Name = "Flora Benetti", Archetype = Archetype.DesperateSeller, Favorites = new[]{ ItemCategory.Jewelry, ItemCategory.Appliances },
			ShirtColor = new Color( 0.65f, 0.80f, 0.60f ), SkinColor = new Color( 0.90f, 0.72f, 0.58f ), Quirk = "Rent is due Friday. It's always due Friday." },
		new NamedCustomerDef { Id = "hank", Name = "Hank Dooley", Archetype = Archetype.CluelessSeller, Favorites = new[]{ ItemCategory.Tools, ItemCategory.Sports },
			ShirtColor = new Color( 0.80f, 0.60f, 0.30f ), SkinColor = new Color( 0.92f, 0.74f, 0.58f ), Quirk = "Convinced everything in his garage is 'vintage'." },
		new NamedCustomerDef { Id = "nadia", Name = "Nadia Volkov", Archetype = Archetype.ToughNegotiator, Favorites = new[]{ ItemCategory.Watches, ItemCategory.Cameras },
			ShirtColor = new Color( 0.25f, 0.30f, 0.40f ), SkinColor = new Color( 0.93f, 0.78f, 0.65f ), Quirk = "Was an auction house appraiser. Never lets you forget it." },
		new NamedCustomerDef { Id = "chip", Name = "Chip Tanaka", Archetype = Archetype.Collector, Favorites = new[]{ ItemCategory.Gaming, ItemCategory.Collectibles },
			ShirtColor = new Color( 0.90f, 0.35f, 0.35f ), SkinColor = new Color( 0.90f, 0.74f, 0.58f ), Quirk = "Owns every Pixelforge console except one." },
		new NamedCustomerDef { Id = "gwen", Name = "Gwen Harlow", Archetype = Archetype.PawnRegular, Favorites = new[]{ ItemCategory.Instruments },
			ShirtColor = new Color( 0.60f, 0.30f, 0.55f ), SkinColor = new Color( 0.86f, 0.66f, 0.50f ), Quirk = "Pawns her trumpet between gigs. Guards it like a child." },
	};

	private static Dictionary<string, NamedCustomerDef> _byId;
	public static NamedCustomerDef Get( string id )
	{
		_byId ??= All.ToDictionary( d => d.Id );
		return id is not null && _byId.TryGetValue( id, out var d ) ? d : null;
	}
}

/// <summary>Name pools for procedurally generated walk-ins.</summary>
public static class NameGen
{
	private static readonly string[] First = { "Alma", "Benny", "Cora", "Dale", "Edie", "Frank", "Gilda", "Harv", "Iris", "Joel", "Kiki", "Lou", "Mabel", "Ned", "Opal", "Pete", "Queenie", "Ray", "Sadie", "Ted", "Una", "Vic", "Wanda", "Xavi", "Yola", "Zeke", "Milo", "June", "Otis", "Faye" };
	private static readonly string[] Last = { "Abbott", "Brill", "Cutter", "Dunn", "Ellery", "Fontaine", "Gruber", "Hale", "Ives", "Jasper", "Klein", "Lowry", "Munn", "Noble", "Otterby", "Prewitt", "Quill", "Rourke", "Slade", "Tully", "Ulmer", "Vance", "Webb", "Yates", "Zorn" };

	public static string Random() => $"{First[Game.Random.Int( 0, First.Length - 1 )]} {Last[Game.Random.Int( 0, Last.Length - 1 )]}";
}
