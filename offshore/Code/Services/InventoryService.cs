namespace Offshore;

public sealed class InventoryService
{
	public SaveData Save { get; private set; }

	public RodDefinition Rod => Catalog.RodById( Save.EquippedRod );
	public ReelDefinition Reel => Catalog.ReelById( Save.EquippedReel );
	public HookDefinition Hook => Catalog.HookById( Save.EquippedHook );
	public LineDefinition Line => Catalog.LineById( Save.EquippedLine );
	public BaitDefinition Bait => Catalog.BaitById( Save.EquippedBait );
	public BoatDefinition Boat => Catalog.BoatById( Save.EquippedBoat );
	public bool HasBoat => Save.OwnedBoats.Count > 0;

	public int BaitCount => Save.BaitCounts.TryGetValue( Save.EquippedBait ?? "", out var n ) ? n : 0;
	public int StorageUsed => Save.Storage.Count;
	public int StorageCapacity => Boat?.Storage ?? 6;
	public bool StorageFull => StorageUsed >= StorageCapacity;

	public void Bind( SaveData save )
	{
		Save = save;
		SyncHotbarSelection();
	}

	public int GetBait( string id ) => Save.BaitCounts.TryGetValue( id, out var n ) ? n : 0;

	public readonly record struct HotbarEntry( string Kind, string Id );

	/// <summary>0–7 index of the last picked hotbar slot (single selection highlight).</summary>
	public int SelectedHotbarIndex { get; private set; }

	/// <summary>
	/// Quick-swap bar: baits you carry + hooks you own (max 8).
	/// Stable catalog order so slots don't jump when you equip.
	/// Rod / reel / line / boat stay shop upgrades — not hotbar items.
	/// </summary>
	public List<HotbarEntry> GetHotbarEntries()
	{
		var list = new List<HotbarEntry>( 8 );

		foreach ( var bait in Catalog.Baits )
		{
			if ( list.Count >= 8 ) break;
			var qty = GetBait( bait.Id );
			if ( qty <= 0 && bait.Id != Save.EquippedBait ) continue;
			list.Add( new HotbarEntry( "bait", bait.Id ) );
		}

		foreach ( var hook in Catalog.Hooks )
		{
			if ( list.Count >= 8 ) break;
			if ( !Save.OwnedHooks.Contains( hook.Id ) ) continue;
			list.Add( new HotbarEntry( "hook", hook.Id ) );
		}

		if ( SelectedHotbarIndex >= list.Count )
			SelectedHotbarIndex = Math.Max( 0, list.Count - 1 );

		return list;
	}

	public string TryEquipHotbar( int index )
	{
		var entries = GetHotbarEntries();
		if ( index < 0 || index >= entries.Count )
			return null;

		var e = entries[index];
		if ( e.Kind == "bait" )
		{
			if ( GetBait( e.Id ) <= 0 && e.Id != Save.EquippedBait )
				return "No bait left.";
			SelectedHotbarIndex = index;
			if ( Save.EquippedBait == e.Id )
				return null;
			Save.EquippedBait = e.Id;
			return $"Bait: {Catalog.BaitById( e.Id )?.Name}";
		}

		if ( e.Kind == "hook" )
		{
			if ( !Save.OwnedHooks.Contains( e.Id ) )
				return "Hook not owned.";
			SelectedHotbarIndex = index;
			if ( Save.EquippedHook == e.Id )
				return null;
			Save.EquippedHook = e.Id;
			return $"Hook: {Catalog.HookById( e.Id )?.Name}";
		}

		return null;
	}

	/// <summary>Snap selection to whatever bait/hook is currently equipped (e.g. after load).</summary>
	public void SyncHotbarSelection()
	{
		var entries = GetHotbarEntries();
		for ( var i = 0; i < entries.Count; i++ )
		{
			var e = entries[i];
			if ( e.Kind == "bait" && e.Id == Save.EquippedBait )
			{
				SelectedHotbarIndex = i;
				return;
			}
		}
		for ( var i = 0; i < entries.Count; i++ )
		{
			var e = entries[i];
			if ( e.Kind == "hook" && e.Id == Save.EquippedHook )
			{
				SelectedHotbarIndex = i;
				return;
			}
		}
	}

	public bool ConsumeBait( int amount = 1 )
	{
		var id = Save.EquippedBait;
		if ( string.IsNullOrEmpty( id ) || GetBait( id ) < amount )
			return false;
		Save.BaitCounts[id] = GetBait( id ) - amount;
		if ( Save.BaitCounts[id] <= 0 )
			Save.BaitCounts.Remove( id );
		return true;
	}

	public bool TryAddCatch( CaughtFish fish, out string error )
	{
		error = null;
		if ( fish is null )
		{
			error = "No fish.";
			return false;
		}
		if ( StorageFull )
		{
			error = "Boat storage is full. Release this fish or replace one.";
			return false;
		}
		Save.Storage.Add( fish );
		return true;
	}

	public void ReplaceCatch( int index, CaughtFish fish )
	{
		if ( index < 0 || index >= Save.Storage.Count )
			Save.Storage.Add( fish );
		else
			Save.Storage[index] = fish;
	}

	public void ReleaseCatch( CaughtFish fish ) { }

	public void RecordCatch( CaughtFish fish )
	{
		Save.TotalCaught++;
		if ( !Save.FishLog.TryGetValue( fish.SpeciesId, out var entry ) )
		{
			entry = new FishLogEntry { SpeciesId = fish.SpeciesId };
			Save.FishLog[fish.SpeciesId] = entry;
			fish.NewSpecies = true;
		}
		entry.TimesCaught++;
		if ( fish.Length > entry.BestLength )
		{
			entry.BestLength = fish.Length;
			fish.PersonalBest = true;
		}
		if ( fish.Weight > entry.BestWeight )
		{
			entry.BestWeight = fish.Weight;
			fish.PersonalBest = true;
		}
		entry.BestValue = Math.Max( entry.BestValue, fish.Value );
	}

	public int SellAll()
	{
		var total = Save.Storage.Sum( f => f.Value );
		Save.Coins += total;
		Save.LifetimeCoins += total;
		Save.Storage.Clear();
		return total;
	}

	public int SellSelected( IEnumerable<int> indices )
	{
		var list = indices.Distinct().OrderByDescending( i => i ).ToList();
		var total = 0;
		foreach ( var i in list )
		{
			if ( i < 0 || i >= Save.Storage.Count )
				continue;
			total += Save.Storage[i].Value;
			Save.Storage.RemoveAt( i );
		}
		Save.Coins += total;
		Save.LifetimeCoins += total;
		return total;
	}
}
