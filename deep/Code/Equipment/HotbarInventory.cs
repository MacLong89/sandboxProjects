namespace Deep;

/// <summary>Dive hotbar — 8 tool slots with charges, selected via 1-8 / mouse wheel.</summary>
public sealed class HotbarInventory
{
	private readonly int[] _charges = new int[ToolCatalog.SlotCount];
	private readonly TimeUntil[] _cooldownReady = new TimeUntil[ToolCatalog.SlotCount];

	public int SelectedIndex { get; private set; }
	public int SlotCount => ToolCatalog.SlotCount;

	public ToolDefinition SelectedTool => ToolCatalog.GetBySlot( SelectedIndex );

	public void RestockForDive()
	{
		for ( var i = 0; i < SlotCount; i++ )
		{
			var def = ToolCatalog.GetBySlot( i );
			_charges[i] = def.InfiniteCharges ? int.MaxValue : 0;
			_cooldownReady[i] = 0f;
		}

		SelectedIndex = 0;
	}

	public void LoadFromReserves( int[] reserves, LoadoutInventory loadout )
	{
		if ( reserves is null || reserves.Length < SlotCount || loadout is null )
		{
			RestockForDive();
			return;
		}

		for ( var i = 0; i < SlotCount; i++ )
		{
			var def = ToolCatalog.GetBySlot( i );
			if ( def.InfiniteCharges )
			{
				_charges[i] = int.MaxValue;
			}
			else if ( !loadout.IsUnlocked( i ) )
			{
				_charges[i] = 0;
			}
			else
			{
				_charges[i] = Math.Clamp( reserves[i], 0, 99 );
			}
			_cooldownReady[i] = 0f;
		}

		SelectedIndex = FindFirstSelectable( loadout );
	}

	private int FindFirstSelectable( LoadoutInventory loadout )
	{
		for ( var i = 0; i < SlotCount; i++ )
		{
			if ( loadout.IsUnlocked( i ) )
				return i;
		}
		return 0;
	}

	public void Select( int index )
	{
		if ( index < 0 || index >= SlotCount ) return;
		SelectedIndex = index;
	}

	public void SelectNext( LoadoutInventory loadout )
	{
		if ( loadout is null )
		{
			SelectedIndex = (SelectedIndex + 1) % SlotCount;
			return;
		}

		for ( var step = 1; step < SlotCount; step++ )
		{
			var next = (SelectedIndex + step) % SlotCount;
			if ( loadout.IsUnlocked( next ) )
			{
				SelectedIndex = next;
				return;
			}
		}
	}

	public void SelectPrev( LoadoutInventory loadout )
	{
		if ( loadout is null )
		{
			SelectedIndex = (SelectedIndex - 1 + SlotCount) % SlotCount;
			return;
		}

		for ( var step = 1; step < SlotCount; step++ )
		{
			var prev = (SelectedIndex - step + SlotCount) % SlotCount;
			if ( loadout.IsUnlocked( prev ) )
			{
				SelectedIndex = prev;
				return;
			}
		}
	}

	public int GetCharges( int slot )
	{
		if ( slot < 0 || slot >= SlotCount ) return 0;
		var def = ToolCatalog.GetBySlot( slot );
		if ( def.InfiniteCharges ) return -1;
		return _charges[slot];
	}

	public bool IsOnCooldown( int slot )
	{
		if ( slot < 0 || slot >= SlotCount ) return true;
		return !_cooldownReady[slot];
	}

	public float CooldownFraction( int slot )
	{
		if ( slot < 0 || slot >= SlotCount ) return 0f;
		var def = ToolCatalog.GetBySlot( slot );
		if ( def.CooldownSeconds <= 0.01f ) return 0f;
		if ( _cooldownReady[slot] ) return 0f;
		// TimeUntil doesn't expose remaining easily in all versions — treat as busy flag.
		return 1f;
	}

	public bool CanUseSelected()
	{
		var def = SelectedTool;
		if ( def is null ) return false;
		if ( IsOnCooldown( SelectedIndex ) ) return false;
		if ( def.InfiniteCharges ) return true;
		return _charges[SelectedIndex] > 0;
	}

	public bool TryConsumeSelected()
	{
		if ( !CanUseSelected() ) return false;

		var def = SelectedTool;
		if ( !def.InfiniteCharges )
			_charges[SelectedIndex] = Math.Max( 0, _charges[SelectedIndex] - 1 );

		_cooldownReady[SelectedIndex] = def.CooldownSeconds;
		return true;
	}

	public bool HasCharges( int slot )
	{
		var def = ToolCatalog.GetBySlot( slot );
		if ( def is null ) return false;
		if ( def.InfiniteCharges ) return true;
		return _charges[slot] > 0;
	}
}
