namespace CatchACritter;

/// <summary>A net tier. Power gates which critters can be caught at all.</summary>
public sealed record NetDef( int Power, string Id, string Name, string Blurb, double Cost, float Radius, float Cooldown, string ColorHex )
{
	public Color Color => Color.Parse( ColorHex ) ?? Color.White;
}

public static class NetCatalog
{
	public static readonly NetDef[] All =
	{
		new( 0, "twig", "Twig Net", "A stick, some string, a dream.", 0, 85f, 0.85f, "#9c7b4f" ),
		new( 1, "garden", "Garden Net", "Borrowed from grandma. Thanks grandma.", 320, 95f, 0.8f, "#6fae4e" ),
		new( 2, "brass", "Brass Snapper", "Snaps shut with satisfying clack.", 2_400, 105f, 0.74f, "#c9963a" ),
		new( 3, "swift", "Swift Weave", "Woven from very fast spider silk.", 11_000, 115f, 0.66f, "#5ac3d0" ),
		new( 4, "storm", "Storm Catcher", "Slightly crackles. Critters respect that.", 48_000, 128f, 0.6f, "#7f8fd0" ),
		new( 5, "gloom", "Gloom Weave", "Woven in the dark, for the dark.", 210_000, 140f, 0.55f, "#6c5a80" ),
		new( 6, "frost", "Frostbite Net", "Cold hands, warm heart, full backpack.", 900_000, 152f, 0.5f, "#9fd0e8" ),
		new( 7, "ember", "Ember Maw", "Do not store near haystacks.", 3_800_000, 165f, 0.45f, "#e86a3a" ),
		new( 8, "royal", "Royal Gilded Net", "For distinguished critter connoisseurs.", 16_000_000, 178f, 0.41f, "#f2c53a" ),
		new( 9, "void", "Void Scoop", "Catches critters from adjacent realities.", 70_000_000, 192f, 0.37f, "#3d3566" ),
		new( 10, "celestial", "Celestial Lasso", "Braided from starlight and patience.", 320_000_000, 208f, 0.33f, "#bfe8ff" ),
		new( 11, "myth", "Mythcaller", "The critters catch themselves out of respect.", 1_500_000_000, 226f, 0.28f, "#c37fe8" ),
	};

	public static NetDef Get( int power ) => All[Math.Clamp( power, 0, All.Length - 1 )];
}

public enum TalentBranch { Hunter, Fortune, Keeper }

public sealed record TalentDef( string Id, string Name, string Blurb, TalentBranch Branch, int MaxRank, int Tier )
{
	public int CostPerRank => 1 + Tier / 2;
}

public static class TalentCatalog
{
	public static readonly TalentDef[] All =
	{
		// Hunter — catch harder, faster
		new( "h_radius", "Wide Sweep", "+6% net radius per rank.", TalentBranch.Hunter, 5, 0 ),
		new( "h_speed", "Fleet Foot", "+5% move speed per rank.", TalentBranch.Hunter, 5, 0 ),
		new( "h_cooldown", "Quick Hands", "-5% swing cooldown per rank.", TalentBranch.Hunter, 5, 1 ),
		new( "h_sneak", "Ghost Step", "Critters notice you 12% later per rank.", TalentBranch.Hunter, 4, 1 ),
		new( "h_double", "Double Scoop", "+7% chance per rank to catch twice the critter.", TalentBranch.Hunter, 4, 2 ),
		new( "h_magnet", "Critter Magnet", "Catch pulls critters from 25% further per rank.", TalentBranch.Hunter, 3, 3 ),

		// Fortune — luck and money
		new( "f_sell", "Silver Tongue", "+8% sell price per rank.", TalentBranch.Fortune, 5, 0 ),
		new( "f_luck", "Clover Charm", "+10% rare spawn luck per rank.", TalentBranch.Fortune, 5, 0 ),
		new( "f_shiny", "Shiny Sense", "+15% shiny odds per rank.", TalentBranch.Fortune, 5, 1 ),
		new( "f_gems", "Gem Digger", "+12% gem drop chance per rank.", TalentBranch.Fortune, 4, 1 ),
		new( "f_streaksave", "Lucky Calendar", "Daily streak survives 1 missed day per rank.", TalentBranch.Fortune, 2, 2 ),
		new( "f_jackpot", "Jackpot", "0.5% chance per rank a catch pays 100x.", TalentBranch.Fortune, 3, 3 ),

		// Keeper — sanctuary, breeding, followers
		new( "k_slots", "Cozy Pens", "+2 sanctuary slots per rank.", TalentBranch.Keeper, 5, 0 ),
		new( "k_income", "Happy Critters", "+10% sanctuary income per rank.", TalentBranch.Keeper, 5, 0 ),
		new( "k_egg", "Warm Nests", "-8% egg hatch time per rank.", TalentBranch.Keeper, 5, 1 ),
		new( "k_followers", "Pack Leader", "+1 follower slot per rank.", TalentBranch.Keeper, 2, 1 ),
		new( "k_followbuff", "Kindred Spirits", "Follower buffs +15% stronger per rank.", TalentBranch.Keeper, 4, 2 ),
		new( "k_offline", "Night Shift", "+10% offline earnings per rank.", TalentBranch.Keeper, 4, 3 ),
	};

	static Dictionary<string, TalentDef> _byId;
	public static TalentDef Get( string id )
	{
		_byId ??= All.ToDictionary( t => t.Id );
		return _byId.GetValueOrDefault( id );
	}
}
