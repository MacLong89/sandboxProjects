namespace NoFly;

public sealed class BagItemDef
{
	public string Id { get; init; }
	public string Label { get; init; }
	public string Icon { get; init; }
	public bool IsLegalUnusual { get; init; }
	public Color Color { get; init; }
}

public sealed class ContrabandDef
{
	public string Id { get; init; }
	public string Label { get; init; }
	public string Icon { get; init; }
	public string Category { get; init; }
	public Color Color { get; init; }
}

public sealed class SuitcaseSlot
{
	public string ItemId { get; init; }
	public Vector2 Position { get; init; }
	public int Layer { get; init; }
	public bool CanHideBehind { get; init; } = true;
}

public sealed class SuitcaseLayoutDef
{
	public string Id { get; init; }
	public string Label { get; init; }
	public Color SuitcaseColor { get; init; }
	public List<SuitcaseSlot> Slots { get; init; } = new();
}

public sealed class BagInstance
{
	public string OwnerId { get; set; }
	public string LayoutId { get; set; }
	public string BagNumber { get; set; }
	public string ContrabandId { get; set; }
	public string HiddenBehindItemId { get; set; }
	public Vector2 ContrabandOffset { get; set; }
	public bool HasContraband => !string.IsNullOrEmpty( ContrabandId );
	public Color SuitcaseColor { get; set; } = Color.White;
}

public static class LuggageCatalog
{
	public static readonly List<BagItemDef> LegalItems = new()
	{
		new() { Id = "shirt", Label = "Shirt", Icon = "checkroom", Color = new Color( 0.4f, 0.7f, 1f ) },
		new() { Id = "shoe", Label = "Shoe", Icon = "steps", Color = new Color( 0.55f, 0.35f, 0.2f ) },
		new() { Id = "book", Label = "Book", Icon = "menu_book", Color = new Color( 0.85f, 0.45f, 0.3f ) },
		new() { Id = "laptop", Label = "Laptop", Icon = "laptop", Color = new Color( 0.5f, 0.55f, 0.6f ) },
		new() { Id = "toothbrush", Label = "Toothbrush", Icon = "clean_hands", Color = new Color( 0.9f, 0.9f, 1f ) },
		new() { Id = "camera", Label = "Camera", Icon = "photo_camera", Color = new Color( 0.3f, 0.3f, 0.35f ) },
		new() { Id = "headphones", Label = "Headphones", Icon = "headphones", Color = new Color( 0.2f, 0.2f, 0.25f ) },
		new() { Id = "bottle", Label = "Water Bottle", Icon = "water_drop", Color = new Color( 0.4f, 0.8f, 1f ) },
		new() { Id = "teddy", Label = "Stuffed Animal", Icon = "cruelty_free", Color = new Color( 0.95f, 0.75f, 0.4f ), IsLegalUnusual = true },
		new() { Id = "toiletry", Label = "Toiletry Bag", Icon = "soap", Color = new Color( 0.7f, 0.85f, 0.7f ) },
		new() { Id = "hat", Label = "Hat", Icon = "cottage", Color = new Color( 0.9f, 0.3f, 0.4f ) },
		new() { Id = "souvenir", Label = "Souvenir", Icon = "emoji_events", Color = new Color( 0.95f, 0.8f, 0.2f ), IsLegalUnusual = true },
		new() { Id = "socks", Label = "Socks", Icon = "texture", Color = new Color( 0.6f, 0.7f, 0.9f ) },
		new() { Id = "snacks", Label = "Snacks", Icon = "cookie", Color = new Color( 0.95f, 0.65f, 0.25f ) },
		new() { Id = "umbrella", Label = "Umbrella", Icon = "umbrella", Color = new Color( 0.35f, 0.45f, 0.85f ), IsLegalUnusual = true },
		new() { Id = "scarf", Label = "Scarf", Icon = "dry_cleaning", Color = new Color( 0.85f, 0.35f, 0.55f ) }
	};

	public static readonly List<ContrabandDef> Contraband = new()
	{
		new() { Id = "knife", Label = "Knife", Icon = "content_cut", Category = "weapon", Color = new Color( 0.75f, 0.75f, 0.8f ) },
		new() { Id = "package", Label = "Suspicious Package", Icon = "inventory_2", Category = "package", Color = new Color( 0.7f, 0.55f, 0.3f ) },
		new() { Id = "usb", Label = "Classified USB", Icon = "usb", Category = "electronic", Color = new Color( 0.3f, 0.85f, 0.5f ) },
		new() { Id = "jewel", Label = "Stolen Jewel", Icon = "diamond", Category = "valuables", Color = new Color( 0.4f, 0.9f, 1f ) },
		new() { Id = "gadget", Label = "Strange Device", Icon = "memory", Category = "electronic", Color = new Color( 0.95f, 0.4f, 0.3f ) },
		new() { Id = "forgedocs", Label = "Forged Papers", Icon = "description", Category = "documents", Color = new Color( 0.95f, 0.95f, 0.85f ) },
		new() { Id = "vial", Label = "Mystery Vial", Icon = "science", Category = "chemical", Color = new Color( 0.55f, 0.95f, 0.35f ) },
		new() { Id = "idol", Label = "Cursed Idol", Icon = "temple_buddhist", Category = "artifact", Color = new Color( 0.85f, 0.65f, 0.2f ) }
	};

	public static readonly List<SuitcaseLayoutDef> Layouts = BuildLayouts();

	static List<SuitcaseLayoutDef> BuildLayouts()
	{
		var layouts = new List<SuitcaseLayoutDef>();
		var palettes = new[]
		{
			new Color( 0.25f, 0.45f, 0.95f ),
			new Color( 0.95f, 0.35f, 0.35f ),
			new Color( 0.3f, 0.75f, 0.45f ),
			new Color( 0.95f, 0.7f, 0.2f ),
			new Color( 0.65f, 0.35f, 0.85f ),
			new Color( 0.2f, 0.2f, 0.25f ),
			new Color( 0.95f, 0.55f, 0.75f ),
			new Color( 0.45f, 0.75f, 0.95f )
		};

		string[][] packs =
		{
			new[] { "shirt", "shoe", "book", "laptop", "toothbrush", "camera" },
			new[] { "headphones", "bottle", "teddy", "toiletry", "hat", "souvenir", "socks" },
			new[] { "shirt", "snacks", "umbrella", "scarf", "book", "shoe", "camera" },
			new[] { "laptop", "headphones", "toothbrush", "hat", "bottle", "socks" },
			new[] { "teddy", "shirt", "shoe", "souvenir", "toiletry", "book", "scarf" },
			new[] { "camera", "laptop", "snacks", "umbrella", "shoe", "hat", "bottle", "socks" },
			new[] { "book", "shirt", "headphones", "teddy", "toothbrush", "souvenir" },
			new[] { "toiletry", "scarf", "shoe", "laptop", "camera", "hat", "snacks" }
		};

		for ( var i = 0; i < packs.Length; i++ )
		{
			var layout = new SuitcaseLayoutDef
			{
				Id = $"layout_{i + 1}",
				Label = $"Suitcase {i + 1}",
				SuitcaseColor = palettes[i]
			};

			var items = packs[i];
			for ( var s = 0; s < items.Length; s++ )
			{
				var col = s % 3;
				var row = s / 3;
				layout.Slots.Add( new SuitcaseSlot
				{
					ItemId = items[s],
					Position = new Vector2( 18 + col * 28, 18 + row * 28 ),
					Layer = s,
					CanHideBehind = true
				} );
			}

			layouts.Add( layout );
		}

		return layouts;
	}

	public static BagItemDef GetItem( string id ) => LegalItems.FirstOrDefault( i => i.Id == id ) ?? LegalItems[0];
	public static ContrabandDef GetContraband( string id ) => Contraband.FirstOrDefault( c => c.Id == id ) ?? Contraband[0];
	public static SuitcaseLayoutDef GetLayout( string id ) => Layouts.FirstOrDefault( l => l.Id == id ) ?? Layouts[0];

	public static BagInstance CreateCleanBag( string ownerId )
	{
		var layout = Random.Shared.FromList( Layouts );
		return new BagInstance
		{
			OwnerId = ownerId,
			LayoutId = layout.Id,
			BagNumber = Random.Shared.Next( 100, 999 ).ToString(),
			SuitcaseColor = layout.SuitcaseColor
		};
	}

	public static BagInstance CreateSmugglerBag( string ownerId, string contrabandId = null )
	{
		var bag = CreateCleanBag( ownerId );
		bag.ContrabandId = contrabandId ?? Random.Shared.FromList( Contraband ).Id;
		var layout = GetLayout( bag.LayoutId );
		var hide = layout.Slots.Where( s => s.CanHideBehind ).Select( s => s.ItemId ).ToList();
		if ( hide.Count > 0 )
			bag.HiddenBehindItemId = Random.Shared.FromList( hide );
		return bag;
	}
}
