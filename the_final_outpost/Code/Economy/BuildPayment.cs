namespace FinalOutpost;

/// <summary>
/// Building payments. In Cure, the scrap price can be paid with Scrap
/// or with Wood+Stone at 1:1 (prefer the material you have more of).
/// </summary>
public static class BuildPayment
{
	public static double MaterialsValue( GameCore core )
	{
		if ( core?.Resources is null ) return 0;
		return core.Resources.Get( ResourceKind.Wood ) + core.Resources.Get( ResourceKind.Stone );
	}

	public static bool CanAfford( GameCore core, double scrapCost )
	{
		if ( core is null || scrapCost <= 0 ) return true;
		if ( core.Wallet.Scrap >= scrapCost ) return true;
		return core.IsCure && MaterialsValue( core ) >= scrapCost;
	}

	public static bool TryPay( GameCore core, double scrapCost )
	{
		if ( core is null || scrapCost <= 0 ) return true;

		if ( core.Wallet.TrySpend( scrapCost ) )
			return true;

		if ( !core.IsCure || MaterialsValue( core ) < scrapCost )
			return false;

		var need = scrapCost;
		var wood = core.Resources.Get( ResourceKind.Wood );
		var stone = core.Resources.Get( ResourceKind.Stone );

		// Prefer spending from the larger stockpile; fall back to the other.
		if ( wood >= stone )
		{
			var takeWood = Math.Min( wood, need );
			if ( takeWood > 0 )
			{
				core.Resources.TrySpend( ResourceKind.Wood, takeWood );
				need -= takeWood;
			}

			if ( need > 0.001 && !core.Resources.TrySpend( ResourceKind.Stone, need ) )
				return false;
		}
		else
		{
			var takeStone = Math.Min( stone, need );
			if ( takeStone > 0 )
			{
				core.Resources.TrySpend( ResourceKind.Stone, takeStone );
				need -= takeStone;
			}

			if ( need > 0.001 && !core.Resources.TrySpend( ResourceKind.Wood, need ) )
				return false;
		}

		return true;
	}

	public static string ShortfallToast( GameCore core, double scrapCost )
	{
		if ( core?.IsCure == true )
			return $"Need {scrapCost:0} scrap or {scrapCost:0} wood/stone";
		return $"Need {scrapCost:0} scrap";
	}
}
