namespace NoFly;

public sealed class DocumentFieldOption
{
	public string Id { get; init; }
	public string Label { get; init; }
	public string DisplayValue { get; init; }
	public DiscrepancyDifficulty Difficulty { get; init; }
	public bool IsCorrect { get; init; }
}

public sealed class DocumentTemplate
{
	public string Id { get; init; }
	public string CountryName { get; init; }
	public string CountryCode { get; init; }
	public Color AccentColor { get; init; }
	public string DefaultName { get; init; }
	public string DefaultDate { get; init; }
	public string DefaultNumber { get; init; }
	public string DefaultSymbol { get; init; }
	public string DefaultSeal { get; init; }
	public string DefaultDestination { get; init; }
	public string DefaultPattern { get; init; }
	public string DefaultPhoto { get; init; }
}

public sealed class DocumentInstance
{
	public string OwnerId { get; set; }
	public string TemplateId { get; set; }
	public Dictionary<DocumentFieldType, string> Values { get; set; } = new();
	public DocumentFieldType? ForgedField { get; set; }
	public string OriginalValue { get; set; }
	public string ForgedValue { get; set; }
	public DiscrepancyDifficulty Difficulty { get; set; }
	public bool IsForged => ForgedField.HasValue;
}

public static class DocumentCatalog
{
	public static readonly List<DocumentTemplate> Templates = new()
	{
		new()
		{
			Id = "talora",
			CountryName = "Talora",
			CountryCode = "TAL",
			AccentColor = new Color( 0.2f, 0.55f, 0.95f ),
			DefaultName = "MIRA SOLEN",
			DefaultDate = "14.08.2028",
			DefaultNumber = "TL-48219",
			DefaultSymbol = "BIRD",
			DefaultSeal = "STAR SEAL",
			DefaultDestination = "GATE C3",
			DefaultPattern = "WAVES",
			DefaultPhoto = "PHOTO A"
		},
		new()
		{
			Id = "brindle",
			CountryName = "Brindle",
			CountryCode = "BRN",
			AccentColor = new Color( 0.95f, 0.45f, 0.2f ),
			DefaultName = "JON PARKER",
			DefaultDate = "03.11.2027",
			DefaultNumber = "BR-90331",
			DefaultSymbol = "LEAF",
			DefaultSeal = "CIRCLE SEAL",
			DefaultDestination = "GATE B2",
			DefaultPattern = "DOTS",
			DefaultPhoto = "PHOTO B"
		},
		new()
		{
			Id = "kelvine",
			CountryName = "Kelvine",
			CountryCode = "KEL",
			AccentColor = new Color( 0.25f, 0.8f, 0.45f ),
			DefaultName = "SASHA RIN",
			DefaultDate = "22.01.2029",
			DefaultNumber = "KV-11704",
			DefaultSymbol = "SUN",
			DefaultSeal = "HEX SEAL",
			DefaultDestination = "GATE A1",
			DefaultPattern = "STRIPES",
			DefaultPhoto = "PHOTO C"
		},
		new()
		{
			Id = "nuvora",
			CountryName = "Nuvora",
			CountryCode = "NUV",
			AccentColor = new Color( 0.75f, 0.35f, 0.85f ),
			DefaultName = "ELLI VANCE",
			DefaultDate = "09.06.2028",
			DefaultNumber = "NV-55082",
			DefaultSymbol = "MOON",
			DefaultSeal = "DIAMOND SEAL",
			DefaultDestination = "GATE C3",
			DefaultPattern = "GRID",
			DefaultPhoto = "PHOTO D"
		}
	};

	public static readonly Dictionary<DocumentFieldType, List<DocumentFieldOption>> Options = new()
	{
		[DocumentFieldType.Photo] = new()
		{
			new() { Id = "photo_a", Label = "Portrait A", DisplayValue = "PHOTO A", Difficulty = DiscrepancyDifficulty.Easy, IsCorrect = true },
			new() { Id = "photo_b", Label = "Portrait B", DisplayValue = "PHOTO B", Difficulty = DiscrepancyDifficulty.Easy },
			new() { Id = "photo_close", Label = "Almost Same", DisplayValue = "PHOTO A~", Difficulty = DiscrepancyDifficulty.Medium },
			new() { Id = "photo_none", Label = "Missing Photo", DisplayValue = "NO PHOTO", Difficulty = DiscrepancyDifficulty.Easy }
		},
		[DocumentFieldType.Name] = new()
		{
			new() { Id = "name_ok", Label = "Correct", DisplayValue = "", Difficulty = DiscrepancyDifficulty.Easy, IsCorrect = true },
			new() { Id = "name_letter", Label = "One Letter Off", DisplayValue = "LETTER", Difficulty = DiscrepancyDifficulty.Medium },
			new() { Id = "name_swap", Label = "Swapped Letters", DisplayValue = "SWAP", Difficulty = DiscrepancyDifficulty.Medium },
			new() { Id = "name_wrong", Label = "Wrong Name", DisplayValue = "WRONG", Difficulty = DiscrepancyDifficulty.Easy }
		},
		[DocumentFieldType.Date] = new()
		{
			new() { Id = "date_ok", Label = "Correct", DisplayValue = "", Difficulty = DiscrepancyDifficulty.Easy, IsCorrect = true },
			new() { Id = "date_digit", Label = "One Digit Changed", DisplayValue = "DIGIT", Difficulty = DiscrepancyDifficulty.Medium },
			new() { Id = "date_rev", Label = "Reversed Digits", DisplayValue = "REVERSE", Difficulty = DiscrepancyDifficulty.Hard },
			new() { Id = "date_bad", Label = "Clearly Wrong", DisplayValue = "99.99.9999", Difficulty = DiscrepancyDifficulty.Easy }
		},
		[DocumentFieldType.PassportNumber] = new()
		{
			new() { Id = "num_ok", Label = "Correct", DisplayValue = "", Difficulty = DiscrepancyDifficulty.Easy, IsCorrect = true },
			new() { Id = "num_digit", Label = "One Digit Off", DisplayValue = "DIGIT", Difficulty = DiscrepancyDifficulty.Medium },
			new() { Id = "num_rev", Label = "Digits Reversed", DisplayValue = "REVERSE", Difficulty = DiscrepancyDifficulty.Hard },
			new() { Id = "num_bad", Label = "Wrong Prefix", DisplayValue = "XX-00000", Difficulty = DiscrepancyDifficulty.Easy }
		},
		[DocumentFieldType.CountrySymbol] = new()
		{
			new() { Id = "sym_bird", Label = "Bird", DisplayValue = "BIRD", Difficulty = DiscrepancyDifficulty.Easy, IsCorrect = true },
			new() { Id = "sym_fish", Label = "Fish", DisplayValue = "FISH", Difficulty = DiscrepancyDifficulty.Easy },
			new() { Id = "sym_leaf", Label = "Leaf", DisplayValue = "LEAF", Difficulty = DiscrepancyDifficulty.Medium },
			new() { Id = "sym_sun", Label = "Sun", DisplayValue = "SUN", Difficulty = DiscrepancyDifficulty.Easy }
		},
		[DocumentFieldType.SecuritySeal] = new()
		{
			new() { Id = "seal_star", Label = "Star Seal", DisplayValue = "STAR SEAL", Difficulty = DiscrepancyDifficulty.Easy, IsCorrect = true },
			new() { Id = "seal_circle", Label = "Circle Seal", DisplayValue = "CIRCLE SEAL", Difficulty = DiscrepancyDifficulty.Easy },
			new() { Id = "seal_hex", Label = "Hex Seal", DisplayValue = "HEX SEAL", Difficulty = DiscrepancyDifficulty.Medium },
			new() { Id = "seal_similar", Label = "Almost Star", DisplayValue = "STAR SEAL*", Difficulty = DiscrepancyDifficulty.Hard }
		},
		[DocumentFieldType.Destination] = new()
		{
			new() { Id = "dest_ok", Label = "Correct Gate", DisplayValue = "", Difficulty = DiscrepancyDifficulty.Easy, IsCorrect = true },
			new() { Id = "dest_a1", Label = "Gate A1", DisplayValue = "GATE A1", Difficulty = DiscrepancyDifficulty.Easy },
			new() { Id = "dest_b2", Label = "Gate B2", DisplayValue = "GATE B2", Difficulty = DiscrepancyDifficulty.Easy },
			new() { Id = "dest_c3", Label = "Gate C3", DisplayValue = "GATE C3", Difficulty = DiscrepancyDifficulty.Medium }
		},
		[DocumentFieldType.BackgroundPattern] = new()
		{
			new() { Id = "pat_waves", Label = "Waves", DisplayValue = "WAVES", Difficulty = DiscrepancyDifficulty.Easy, IsCorrect = true },
			new() { Id = "pat_dots", Label = "Dots", DisplayValue = "DOTS", Difficulty = DiscrepancyDifficulty.Easy },
			new() { Id = "pat_grid", Label = "Grid", DisplayValue = "GRID", Difficulty = DiscrepancyDifficulty.Medium },
			new() { Id = "pat_none", Label = "Missing Pattern", DisplayValue = "NONE", Difficulty = DiscrepancyDifficulty.Easy }
		}
	};

	public static DocumentInstance CreateValid( string ownerId, string templateId = null )
	{
		var template = Templates.FirstOrDefault( t => t.Id == templateId ) ?? Random.Shared.FromList( Templates );
		var doc = new DocumentInstance
		{
			OwnerId = ownerId,
			TemplateId = template.Id,
			Difficulty = DiscrepancyDifficulty.Easy
		};
		doc.Values[DocumentFieldType.Photo] = template.DefaultPhoto;
		doc.Values[DocumentFieldType.Name] = template.DefaultName;
		doc.Values[DocumentFieldType.Date] = template.DefaultDate;
		doc.Values[DocumentFieldType.PassportNumber] = template.DefaultNumber;
		doc.Values[DocumentFieldType.CountrySymbol] = template.DefaultSymbol;
		doc.Values[DocumentFieldType.SecuritySeal] = template.DefaultSeal;
		doc.Values[DocumentFieldType.Destination] = template.DefaultDestination;
		doc.Values[DocumentFieldType.BackgroundPattern] = template.DefaultPattern;
		return doc;
	}

	public static DocumentTemplate GetTemplate( string id ) => Templates.FirstOrDefault( t => t.Id == id ) ?? Templates[0];

	public static string ApplyNameForge( string original, string mode )
	{
		if ( string.IsNullOrEmpty( original ) || original.Length < 2 )
			return "WRONG NAME";

		return mode switch
		{
			"LETTER" => original[..^1] + (original[^1] == 'A' ? 'E' : 'A'),
			"SWAP" => original.Length < 2 ? original : $"{original[1]}{original[0]}{original[2..]}",
			"WRONG" => original.Contains( ' ' ) ? "ALEX RIVER" : "NOBODY",
			_ => original
		};
	}

	public static string ApplyDateForge( string original, string mode )
	{
		if ( mode == "99.99.9999" ) return mode;
		if ( string.IsNullOrEmpty( original ) || original.Length < 4 ) return "01.01.2000";
		var digits = original.Where( char.IsDigit ).ToArray();
		if ( digits.Length < 2 ) return original;
		if ( mode == "REVERSE" )
		{
			var chars = original.ToCharArray();
			var idxs = chars.Select( ( c, i ) => (c, i) ).Where( x => char.IsDigit( x.c ) ).Take( 2 ).ToArray();
			if ( idxs.Length == 2 )
			{
				(chars[idxs[0].i], chars[idxs[1].i]) = (chars[idxs[1].i], chars[idxs[0].i]);
			}
			return new string( chars );
		}
		// DIGIT
		{
			var chars = original.ToCharArray();
			for ( var i = chars.Length - 1; i >= 0; i-- )
			{
				if ( !char.IsDigit( chars[i] ) ) continue;
				chars[i] = chars[i] == '9' ? '0' : (char)(chars[i] + 1);
				break;
			}
			return new string( chars );
		}
	}

	public static string ApplyNumberForge( string original, string mode )
	{
		if ( mode == "XX-00000" ) return mode;
		if ( string.IsNullOrEmpty( original ) ) return "XX-00000";
		if ( mode == "REVERSE" )
		{
			var parts = original.Split( '-' );
			if ( parts.Length == 2 && parts[1].Length >= 2 )
			{
				var arr = parts[1].ToCharArray();
				(arr[0], arr[^1]) = (arr[^1], arr[0]);
				return $"{parts[0]}-{new string( arr )}";
			}
		}
		var chars = original.ToCharArray();
		for ( var i = chars.Length - 1; i >= 0; i-- )
		{
			if ( !char.IsDigit( chars[i] ) ) continue;
			chars[i] = chars[i] == '9' ? '1' : (char)(chars[i] + 1);
			break;
		}
		return new string( chars );
	}
}
