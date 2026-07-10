namespace FinalOutpost;

/// <summary>Raw resources harvested from plots. Scrap remains the main currency (see PlayerWallet).</summary>
public enum ResourceKind
{
	None,
	Wood,
	Stone,
	Water,
	Specimens
}

public static class ResourceInfo
{
	public static readonly ResourceKind[] Harvestable = { ResourceKind.Wood, ResourceKind.Stone, ResourceKind.Water };
	public static readonly ResourceKind[] AllStockpiled = { ResourceKind.Wood, ResourceKind.Stone, ResourceKind.Water, ResourceKind.Specimens };

	public static string Name( ResourceKind k ) => k switch
	{
		ResourceKind.Wood => "Wood",
		ResourceKind.Stone => "Stone",
		ResourceKind.Water => "Water",
		ResourceKind.Specimens => "Specimens",
		_ => "None"
	};

	public static string Icon( ResourceKind k ) => k switch
	{
		ResourceKind.Wood => "park",
		ResourceKind.Stone => "landscape",
		ResourceKind.Water => "water_drop",
		ResourceKind.Specimens => "biotech",
		_ => "block"
	};

	public static Color Tint( ResourceKind k ) => k switch
	{
		ResourceKind.Wood => new Color( 0.36f, 0.6f, 0.28f ),
		ResourceKind.Stone => new Color( 0.58f, 0.58f, 0.62f ),
		ResourceKind.Water => new Color( 0.35f, 0.62f, 0.9f ),
		ResourceKind.Specimens => new Color( 0.72f, 0.45f, 0.85f ),
		_ => new Color( 0.5f, 0.5f, 0.5f )
	};

	public static ResourceKind Parse( string s ) =>
		Enum.TryParse<ResourceKind>( s, out var k ) ? k : ResourceKind.None;
}

/// <summary>Stores harvested resource amounts, persisted in <see cref="SaveData"/>.</summary>
public sealed class ResourceBank
{
	private readonly SaveData _save;

	public ResourceBank( SaveData save ) => _save = save;

	public event Action Changed;

	public double Get( ResourceKind kind )
	{
		if ( kind == ResourceKind.None ) return 0;
		return _save.Resources.TryGetValue( kind.ToString(), out var v ) ? v : 0;
	}

	public void Add( ResourceKind kind, double amount )
	{
		if ( kind == ResourceKind.None || amount <= 0 ) return;
		_save.Resources[kind.ToString()] = Get( kind ) + amount;
		Changed?.Invoke();
	}

	public bool TrySpend( ResourceKind kind, double amount )
	{
		if ( kind == ResourceKind.None || amount <= 0 ) return true;
		if ( Get( kind ) < amount ) return false;
		_save.Resources[kind.ToString()] = Get( kind ) - amount;
		Changed?.Invoke();
		return true;
	}

	/// <summary>Removes up to <paramref name="amount"/> of the most-stocked resource. Returns what was taken.</summary>
	public (ResourceKind kind, double taken) DrainRichest( double amount )
	{
		var best = ResourceKind.None;
		var bestQty = 0.0;
		foreach ( var k in ResourceInfo.Harvestable )
		{
			var q = Get( k );
			if ( q > bestQty ) { bestQty = q; best = k; }
		}

		if ( best == ResourceKind.None || bestQty <= 0 )
			return (ResourceKind.None, 0);

		var take = Math.Min( amount, bestQty );
		_save.Resources[best.ToString()] = bestQty - take;
		Changed?.Invoke();
		return (best, take);
	}
}
