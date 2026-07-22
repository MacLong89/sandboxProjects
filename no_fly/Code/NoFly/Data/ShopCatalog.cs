namespace NoFly;

public sealed class ShopItemDef
{
	public string Id { get; init; }
	public string Label { get; init; }
	public string Blurb { get; init; }
	public int Price { get; init; }
	public Color Tint { get; init; }
}

public static class ShopCatalog
{
	public static readonly ShopItemDef[] Snacks =
	{
		new() { Id = "soda", Label = "Sky Soda", Blurb = "Bubbly. Slightly warm.", Price = 4, Tint = new Color( 0.2f, 0.7f, 0.95f ) },
		new() { Id = "pretzel", Label = "Gate Pretzel", Blurb = "Salted. Questionably fresh.", Price = 5, Tint = new Color( 0.85f, 0.65f, 0.3f ) },
		new() { Id = "noodles", Label = "Cup Noodles", Blurb = "Airport classic.", Price = 6, Tint = new Color( 0.95f, 0.45f, 0.25f ) },
		new() { Id = "coffee", Label = "Tarmac Coffee", Blurb = "Strong enough to clear security.", Price = 5, Tint = new Color( 0.45f, 0.28f, 0.15f ) },
	};

	public static readonly ShopItemDef[] Gifts =
	{
		new() { Id = "magnet", Label = "Fridge Magnet", Blurb = "Says NO FLY on it.", Price = 8, Tint = new Color( 0.9f, 0.35f, 0.55f ) },
		new() { Id = "plush", Label = "Tiny Plane Plush", Blurb = "Squishable turbulence.", Price = 12, Tint = new Color( 0.55f, 0.75f, 0.95f ) },
		new() { Id = "mug", Label = "Security Mug", Blurb = "\"I cleared bag scan.\"", Price = 10, Tint = new Color( 0.7f, 0.85f, 0.4f ) },
		new() { Id = "keychain", Label = "Luggage Keychain", Blurb = "Will set off a scanner someday.", Price = 7, Tint = new Color( 0.95f, 0.8f, 0.25f ) },
	};

	public static string TitleFor( string zoneTag ) => zoneTag switch
	{
		"shop_food" => "Snack Stand",
		"shop_gift" => "Gift Shop",
		_ => "Shop"
	};

	public static string SubtitleFor( string zoneTag ) => zoneTag switch
	{
		"shop_food" => "Grab a drink or a bite before your flight.",
		"shop_gift" => "Overpriced souvenirs. Perfect.",
		_ => "Pick something up."
	};

	public static ShopItemDef[] ItemsFor( string zoneTag ) => zoneTag switch
	{
		"shop_food" => Snacks,
		"shop_gift" => Gifts,
		_ => Snacks
	};

	public static ShopItemDef Find( string zoneTag, string itemId ) =>
		ItemsFor( zoneTag ).FirstOrDefault( i => i.Id == itemId );
}
