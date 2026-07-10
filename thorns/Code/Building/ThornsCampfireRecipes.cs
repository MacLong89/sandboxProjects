namespace Sandbox;

/// <summary>Campfire processing: one input unit → output unit every <see cref="ThornsCampfire.ProcessSecondsPerItem"/> (fuel separate).</summary>
public static class ThornsCampfireRecipes
{
	public static bool TryGetOutputForInput( string inputItemId, out string outputItemId, out int outputQuantity )
	{
		outputItemId = "";
		outputQuantity = 1;
		if ( string.IsNullOrWhiteSpace( inputItemId ) )
			return false;

		switch ( inputItemId.Trim().ToLowerInvariant() )
		{
			case "metal_ore":
				outputItemId = "metal";
				return true;
			case "raw_meat":
				outputItemId = "cooked_meat";
				return true;
			case "dirty_water":
				outputItemId = "water";
				return true;
			default:
				return false;
		}
	}

	public static bool IsValidFuelItemId( string itemId ) =>
		string.Equals( itemId?.Trim(), "wood", StringComparison.OrdinalIgnoreCase );

	public static bool IsRawInputProcessable( string itemId ) =>
		TryGetOutputForInput( itemId ?? "", out _, out _ );
}
