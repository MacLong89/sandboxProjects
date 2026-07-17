namespace DeepDive;

public sealed class HaulItemRecord
{
	public string CollectibleId { get; init; }
	public string DisplayName { get; init; }
	public CollectibleRarity Rarity { get; init; }
	public float Value { get; init; }
	public int CapacityCost { get; init; }
}

/// <summary>Temporary dive haul — cleared on sell or failure.</summary>
public sealed class HaulInventory
{
	private readonly List<HaulItemRecord> _items = new();

	public IReadOnlyList<HaulItemRecord> Items => _items;
	public int CapacityUsed { get; private set; }
	public int MaxCapacity { get; private set; } = 8;
	public int CapacityBonus { get; private set; }
	public int EffectiveMaxCapacity => MaxCapacity + CapacityBonus;
	public int ItemCount => _items.Count;
	public float TotalValue { get; private set; }
	public bool IsFull => CapacityUsed >= EffectiveMaxCapacity;

	public void Reset( int maxCapacity )
	{
		_items.Clear();
		CapacityUsed = 0;
		TotalValue = 0f;
		CapacityBonus = 0;
		MaxCapacity = Math.Max( 1, maxCapacity );
	}

	public void SetCapacityBonus( int bonus )
	{
		CapacityBonus = Math.Max( 0, bonus );
	}

	public bool CanFit( CollectibleDefinition def )
	{
		if ( def is null ) return false;
		return CapacityUsed + Math.Max( 1, def.CapacityCost ) <= EffectiveMaxCapacity;
	}

	public bool TryAdd( CollectibleDefinition def )
	{
		if ( def is null || !CanFit( def ) )
			return false;

		var cost = Math.Max( 1, def.CapacityCost );
		_items.Add( new HaulItemRecord
		{
			CollectibleId = def.Id,
			DisplayName = def.DisplayName,
			Rarity = def.Rarity,
			Value = def.BaseValue,
			CapacityCost = cost
		} );
		CapacityUsed += cost;
		TotalValue += def.BaseValue;
		return true;
	}

	public void Clear()
	{
		_items.Clear();
		CapacityUsed = 0;
		TotalValue = 0f;
	}
}
