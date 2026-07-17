namespace DeepDive;

/// <summary>Persistent tool reserves between dives + shop purchases.</summary>
public sealed class LoadoutInventory
{
	private readonly int[] _reserves = new int[ToolCatalog.SlotCount];
	private readonly bool[] _unlocked = new bool[ToolCatalog.SlotCount];

	public LoadoutInventory()
	{
		ResetToDefaults();
	}

	public void ResetToDefaults()
	{
		for ( var i = 0; i < ToolCatalog.SlotCount; i++ )
		{
			var def = ToolCatalog.GetBySlot( i );
			_unlocked[i] = def.InfiniteCharges;
			_reserves[i] = def.InfiniteCharges ? int.MaxValue : 0;
		}
	}

	public bool IsUnlocked( int slot )
	{
		if ( slot < 0 || slot >= ToolCatalog.SlotCount ) return false;
		var def = ToolCatalog.GetBySlot( slot );
		if ( def.InfiniteCharges ) return true;
		return _unlocked[slot];
	}

	public void Unlock( int slot )
	{
		if ( slot < 0 || slot >= ToolCatalog.SlotCount ) return;
		var def = ToolCatalog.GetBySlot( slot );
		if ( def.InfiniteCharges ) return;
		_unlocked[slot] = true;
	}

	public int GetReserve( int slot )
	{
		if ( slot < 0 || slot >= ToolCatalog.SlotCount ) return 0;
		var def = ToolCatalog.GetBySlot( slot );
		if ( def.InfiniteCharges ) return -1;
		return _reserves[slot];
	}

	public void AddCharges( int slot, int amount )
	{
		if ( amount <= 0 || slot < 0 || slot >= ToolCatalog.SlotCount ) return;
		var def = ToolCatalog.GetBySlot( slot );
		if ( def.InfiniteCharges ) return;
		Unlock( slot );
		_reserves[slot] = Math.Min( 99, _reserves[slot] + amount );
	}

	/// <summary>Copy reserves into the dive hotbar.</summary>
	public void ApplyToHotbar( HotbarInventory hotbar )
	{
		if ( hotbar is null ) return;
		hotbar.LoadFromReserves( _reserves, this );
	}

	/// <summary>Write unused dive charges back into reserves.</summary>
	public void CaptureFromHotbar( HotbarInventory hotbar )
	{
		if ( hotbar is null ) return;
		for ( var i = 0; i < ToolCatalog.SlotCount; i++ )
		{
			var def = ToolCatalog.GetBySlot( i );
			if ( def.InfiniteCharges ) continue;
			if ( !IsUnlocked( i ) ) continue;
			var left = hotbar.GetCharges( i );
			if ( left < 0 ) continue;
			_reserves[i] = left;
		}
	}

	public Dictionary<string, int> ToSaveMap()
	{
		var map = new Dictionary<string, int>();
		for ( var i = 0; i < ToolCatalog.SlotCount; i++ )
		{
			var def = ToolCatalog.GetBySlot( i );
			if ( def.InfiniteCharges || !IsUnlocked( i ) ) continue;
			map[def.Id] = _reserves[i];
		}
		return map;
	}

	public List<string> ToUnlockedList()
	{
		var list = new List<string>();
		for ( var i = 0; i < ToolCatalog.SlotCount; i++ )
		{
			var def = ToolCatalog.GetBySlot( i );
			if ( def.InfiniteCharges || !IsUnlocked( i ) ) continue;
			list.Add( def.Id );
		}
		return list;
	}

	public void ApplySaveMap( Dictionary<string, int> map, List<string> unlocked )
	{
		ResetToDefaults();
		if ( unlocked is not null )
		{
			for ( var i = 0; i < ToolCatalog.SlotCount; i++ )
			{
				var def = ToolCatalog.GetBySlot( i );
				if ( def.InfiniteCharges ) continue;
				if ( unlocked.Contains( def.Id ) )
					Unlock( i );
			}
		}

		if ( map is not null )
		{
			for ( var i = 0; i < ToolCatalog.SlotCount; i++ )
			{
				var def = ToolCatalog.GetBySlot( i );
				if ( def.InfiniteCharges ) continue;
				if ( map.TryGetValue( def.Id, out var n ) && n > 0 )
				{
					Unlock( i );
					_reserves[i] = Math.Clamp( n, 0, 99 );
				}
			}
		}
	}
}
