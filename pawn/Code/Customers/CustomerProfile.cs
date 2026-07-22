namespace PawnShop;

/// <summary>
/// Runtime identity + hidden negotiation values for one visiting customer.
/// Built fresh each visit (named customers persist via RelationshipData).
/// </summary>
public sealed class CustomerProfile
{
	public string Id { get; set; }
	public string Name { get; set; }
	public ArchetypeDef Archetype { get; set; }
	public NamedCustomerDef Named { get; set; }
	public CustomerIntent Intent { get; set; }

	/// <summary>Item they brought (Sell/Pawn intents).</summary>
	public ItemInstance Item { get; set; }
	/// <summary>Contract they're redeeming (Redeem intent).</summary>
	public PawnContract Contract { get; set; }

	// --- Seller negotiation (hidden) ---
	/// <summary>Their opening ask (sell) or requested loan (pawn).</summary>
	public int AskPrice { get; set; }
	/// <summary>Lowest they'll take right now (moves during negotiation).</summary>
	public int MinAccept { get; set; }
	/// <summary>The number they'd be happy with.</summary>
	public int IdealPrice { get; set; }

	// --- Buyer fields ---
	public int Budget { get; set; }
	public int TargetItemId { get; set; } = -1;
	public int BuyerOffer { get; set; }

	// --- Mood / social ---
	/// <summary>0..1 current mood (0 = furious).</summary>
	public float Mood { get; set; } = 0.7f;
	/// <summary>Seconds of patience left in the current interaction.</summary>
	public float Patience { get; set; } = 60f;
	/// <summary>Patience at the start of the current interaction (for UI bars).</summary>
	public float PatienceMax { get; set; } = 60f;
	public RelationshipData Relationship { get; set; }

	// --- Visuals ---
	public Color Shirt { get; set; }
	public Color Pants { get; set; }
	public Color Skin { get; set; }

	public bool IsNamed => Named is not null;

	public string MoodLabel => Mood switch
	{
		>= 0.75f => "Happy",
		>= 0.5f => "Neutral",
		>= 0.3f => "Annoyed",
		_ => "Angry",
	};

	public string MoodIcon => Mood switch
	{
		>= 0.75f => "sentiment_very_satisfied",
		>= 0.5f => "sentiment_neutral",
		>= 0.3f => "sentiment_dissatisfied",
		_ => "sentiment_very_dissatisfied",
	};

	/// <summary>Build a procedural walk-in customer.</summary>
	public static CustomerProfile Procedural( ArchetypeDef archetype )
	{
		return new CustomerProfile
		{
			Id = null,
			Name = NameGen.Random(),
			Archetype = archetype,
			Shirt = new ColorHsv( Game.Random.Float( 0f, 360f ), Game.Random.Float( 0.3f, 0.7f ), Game.Random.Float( 0.5f, 0.9f ) ),
			Pants = new Color( 0.2f, 0.22f, 0.3f ) * Game.Random.Float( 0.8f, 1.4f ),
			Skin = SkinTones[Game.Random.Int( 0, SkinTones.Length - 1 )],
		};
	}

	/// <summary>Build a visit from a recurring named customer.</summary>
	public static CustomerProfile FromNamed( NamedCustomerDef def, RelationshipData rel )
	{
		return new CustomerProfile
		{
			Id = def.Id,
			Name = def.Name,
			Named = def,
			Archetype = ArchetypeCatalog.Get( def.Archetype ),
			Relationship = rel,
			Shirt = def.ShirtColor,
			Pants = def.PantsColor,
			Skin = def.SkinColor,
		};
	}

	private static readonly Color[] SkinTones =
	{
		new( 0.95f, 0.80f, 0.66f ),
		new( 0.90f, 0.72f, 0.58f ),
		new( 0.85f, 0.65f, 0.50f ),
		new( 0.72f, 0.54f, 0.40f ),
		new( 0.58f, 0.42f, 0.30f ),
		new( 0.45f, 0.32f, 0.24f ),
	};
}
