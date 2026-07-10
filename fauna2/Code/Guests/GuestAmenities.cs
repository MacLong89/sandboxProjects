namespace Fauna2;

/// <summary>
/// Guest restrooms, restaurants and shops — required once visitors arrive.
/// Satisfaction is capped by the scarcest amenity relative to current guest count.
/// </summary>
public static class GuestAmenities
{
	public static int RestroomCount => PlaceableRegistry.RestroomCount;
	public static int RestaurantCount => PlaceableRegistry.RestaurantCount;
	public static int ShopCount => PlaceableRegistry.ShopCount;

	public static bool HasMinimumAmenities => RestroomCount > 0 && RestaurantCount > 0;

	public static int TotalRestaurantCapacity() => PlaceableRegistry.RestaurantGuestCapacity;

	public static int TotalShopCapacity() => PlaceableRegistry.ShopGuestCapacity;

	public static float Coverage( int guestCount, int facilityCount, int guestsPerFacility )
	{
		if ( guestCount <= 0 ) return 100f;
		if ( facilityCount <= 0 ) return 0f;

		var capacity = facilityCount * guestsPerFacility;
		return (capacity / (float)guestCount * 100f).Clamp( 0f, 100f );
	}

	public static float CoverageFromCapacity( int guestCount, int totalCapacity )
	{
		if ( guestCount <= 0 ) return 100f;
		if ( totalCapacity <= 0 ) return 0f;

		return (totalCapacity / (float)guestCount * 100f).Clamp( 0f, 100f );
	}

	public static (float Restroom, float Restaurant, float Shop) Coverages( int guestCount ) =>
	(
		Coverage( guestCount, RestroomCount, GameConstants.GuestsPerRestroom ),
		CoverageFromCapacity( guestCount, TotalRestaurantCapacity() ),
		CoverageFromCapacity( guestCount, TotalShopCapacity() )
	);

	/// <summary>0–100 score blended into aggregate guest satisfaction.</summary>
	public static float SatisfactionScore( int guestCount )
	{
		if ( guestCount <= 0 || !PathNetwork.HasGuestAccess )
			return 100f;

		if ( RestroomCount == 0 || RestaurantCount == 0 )
			return 12f;

		var (restroom, restaurant, shop) = Coverages( guestCount );
		var score = Math.Min( AmenityCoverageScore( restroom ), AmenityCoverageScore( restaurant ) );

		if ( ShopCount > 0 )
			score = Math.Min( score, AmenityCoverageScore( shop ) );

		return score;
	}

	/// <summary>Partial amenity coverage still keeps guests reasonably happy.</summary>
	static float AmenityCoverageScore( float coveragePercent )
	{
		if ( coveragePercent <= 0f )
			return 0f;

		if ( coveragePercent >= 100f )
			return 100f;

		return 40f + coveragePercent * 0.6f;
	}
}
