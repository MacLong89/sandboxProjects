namespace Sandbox;

using System.Collections.Generic;

/// <summary>Deterministic rotating subset of <see cref="ThornsRadioShopCatalog"/> for the current epoch (host time).</summary>
public static class ThornsRadioShopRotation
{
	public const float RotationPeriodSeconds = 300f;

	public const int OfferSlotCount = 10;

	public static long CurrentEpochIndexHost() =>
		(long)Math.Floor( Time.Now / Math.Max( 0.5f, RotationPeriodSeconds ) );

	/// <summary>Stable offers for this epoch (same on host for all validations).</summary>
	public static ThornsRadioShopCatalog.ThornsShopStockEntry[] HostBuildOffersForEpoch( long epoch )
	{
		var pool = ThornsRadioShopCatalog.StockPool;
		if ( pool.Length == 0 )
			return Array.Empty<ThornsRadioShopCatalog.ThornsShopStockEntry>();

		var rng = new Random( HashEpoch( epoch ) );
		var result = new ThornsRadioShopCatalog.ThornsShopStockEntry[OfferSlotCount];
		if ( pool.Length >= OfferSlotCount )
		{
			var picks = new List<int>( pool.Length );
			for ( var i = 0; i < pool.Length; i++ )
				picks.Add( i );
			for ( var i = picks.Count - 1; i > 0; i-- )
			{
				var j = rng.Next( i + 1 );
				(picks[i], picks[j]) = (picks[j], picks[i]);
			}

			for ( var k = 0; k < OfferSlotCount; k++ )
				result[k] = pool[picks[k]];
		}
		else
		{
			for ( var i = 0; i < OfferSlotCount; i++ )
				result[i] = pool[rng.Next( 0, pool.Length )];
		}

		return result;
	}

	static int HashEpoch( long epoch )
	{
		unchecked
		{
			var h = (int)(epoch & 0x7FFFFFFF);
			h ^= h << 13;
			h ^= h >> 7;
			h ^= h << 17;
			return h == 0 ? 1337 : h;
		}
	}
}
