namespace PawnShop;

/// <summary>Pawn loan contracts: issuing, redemption, extension, and defaults.</summary>
public sealed class PawnSystem
{
	private readonly SaveData _save;
	private readonly InventorySystem _inventory;
	private readonly EconomySystem _economy;
	private readonly RelationshipSystem _relationships;

	public PawnSystem( SaveData save, InventorySystem inventory, EconomySystem economy, RelationshipSystem relationships )
	{
		_save = save;
		_inventory = inventory;
		_economy = economy;
		_relationships = relationships;
	}

	public IEnumerable<PawnContract> Contracts => _save.PawnContracts;
	public int Count => _save.PawnContracts.Count;

	public PawnContract ForItem( int itemId ) => _save.PawnContracts.FirstOrDefault( c => c.ItemId == itemId );

	/// <summary>Player-facing risk estimate; deliberately fuzzy.</summary>
	public static string RiskLabel( float likelihood )
	{
		// Fuzz so the label can't be reverse-engineered exactly.
		var fuzzed = likelihood + Game.Random.Float( -0.12f, 0.12f );
		return fuzzed switch
		{
			>= 0.65f => "Low Risk",
			>= 0.4f => "Moderate Risk",
			_ => "High Risk",
		};
	}

	/// <summary>Compute hidden repayment likelihood for a customer + loan.</summary>
	public float ComputeRepayLikelihood( CustomerProfile customer, ItemInstance item, int loan, GameManager game )
	{
		var value = ItemValue.TrueValue( item, game );
		var ratio = value > 0 ? (float)loan / value : 1f;

		var likelihood = 0.75f;
		likelihood -= ratio * 0.35f;                       // over-loaned items get abandoned
		likelihood -= customer.Archetype.Desperation * 0.2f;
		likelihood += customer.Relationship is { PawnsRedeemed: > 0 } rel ? Math.Min( 0.2f, rel.PawnsRedeemed * 0.05f ) : 0f;
		likelihood -= customer.Relationship?.PawnsDefaulted * 0.08f ?? 0f;
		if ( customer.Archetype.Id == Archetype.PawnRegular ) likelihood += 0.2f;
		if ( customer.Archetype.Id == Archetype.Scammer ) likelihood -= 0.35f;
		likelihood += Game.Random.Float( -0.08f, 0.08f );

		return Math.Clamp( likelihood, 0.05f, 0.95f );
	}

	/// <summary>Issue a pawn loan: pay out cash, store the item, register the contract.</summary>
	public PawnContract Issue( CustomerProfile customer, ItemInstance item, int principal, float fee, GameManager game )
	{
		_economy.Spend( principal );
		_economy.Ledger.PawnsIssued++;
		_economy.Ledger.PawnSpend += principal;

		_inventory.AddPawned( item, _save.Day );

		var contract = new PawnContract
		{
			ItemId = item.Id,
			CustomerId = customer.Id,
			CustomerName = customer.Name,
			Principal = principal,
			Fee = fee,
			StartDay = _save.Day,
			DueDay = _save.Day + GameConstants.PawnTermDays,
			RepayLikelihood = ComputeRepayLikelihood( customer, item, principal, game ),
		};
		_save.PawnContracts.Add( contract );
		return contract;
	}

	/// <summary>Customer pays back loan + fee and takes the item home.</summary>
	public bool Redeem( PawnContract contract )
	{
		var item = _inventory.Get( contract.ItemId );
		if ( item is null )
		{
			// Storage bug fallback: refund the customer relationship, void the contract.
			Log.Warning( $"[PawnShop] Pawned item {contract.ItemId} missing — contract voided gracefully." );
			_save.PawnContracts.Remove( contract );
			return false;
		}

		_economy.Earn( contract.RedemptionAmount );
		_economy.Ledger.Redemptions++;
		_economy.Ledger.RedemptionRevenue += contract.RedemptionAmount;
		_economy.RecordDeal( item.Name, contract.RedemptionAmount - contract.Principal );

		_inventory.Remove( item );
		_save.PawnContracts.Remove( contract );
		_relationships.RecordPawnOutcome( contract.CustomerId, contract.CustomerName, true );
		return true;
	}

	/// <summary>Extend the deadline (goodwill, small trust gain).</summary>
	public void Extend( PawnContract contract )
	{
		contract.DueDay += GameConstants.PawnExtensionDays;
		contract.Extended = true;
	}

	/// <summary>
	/// Advance contracts at day end. Expired contracts default: the item becomes
	/// store property (moved to backroom, purchase price = principal).
	/// Returns names of defaulted items for the summary.
	/// </summary>
	public List<string> ProcessDayEnd()
	{
		var defaulted = new List<string>();

		foreach ( var contract in _save.PawnContracts.ToList() )
		{
			if ( _save.Day < contract.DueDay )
				continue;

			// Grace: contract holder may still show up on the due day itself; default the day after.
			if ( _save.Day == contract.DueDay )
				continue;

			var item = _inventory.Get( contract.ItemId );
			_save.PawnContracts.Remove( contract );
			_relationships.RecordPawnOutcome( contract.CustomerId, contract.CustomerName, false );

			if ( item is not null )
			{
				// Ownership transfers — even if the backroom is over capacity we accept it
				// (defaults can overflow storage; the player just can't buy more until cleared).
				item.Location = ItemLocation.Backroom;
				item.PurchasePrice = contract.Principal;
				item.DayAcquired = _save.Day;
				defaulted.Add( item.Name );
			}
		}

		return defaulted;
	}

	/// <summary>Contracts due today or tomorrow (for the morning report).</summary>
	public IEnumerable<PawnContract> DueSoon => _save.PawnContracts.Where( c => c.DueDay - _save.Day <= 1 );
}
