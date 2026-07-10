namespace Fauna2;

/// <summary>One actionable guest insight for the stats / TAB menu.</summary>
public readonly struct GuestInsight
{
	public string Icon { get; init; }
	public string Title { get; init; }
	public string Detail { get; init; }
	/// <summary>Higher = show first for problems.</summary>
	public int Priority { get; init; }
	public bool IsPositive { get; init; }
}

/// <summary>One row on the local standings table.</summary>
public readonly struct LeaderboardRow
{
	public string Stat { get; init; }
	public string Value { get; init; }
	public string Note { get; init; }
}

/// <summary>
/// Aggregates zoo finances and guest feedback for the TAB stats dashboard.
/// </summary>
public static class ZooStatsReport
{
	private static int _insightCacheKey = int.MinValue;
	private static IReadOnlyList<GuestInsight> _harmsCache = Array.Empty<GuestInsight>();
	private static IReadOnlyList<GuestInsight> _wantsCache = Array.Empty<GuestInsight>();
	private static bool _harmsCacheValid;
	private static bool _wantsCacheValid;

	static int InsightCacheKey() => HashCode.Combine(
		HashCode.Combine(
			PlaceableRegistry.Count,
			PlaceableRegistry.RestaurantCount,
			PlaceableRegistry.RestroomCount,
			PlaceableRegistry.ShopCount,
			HabitatRegistry.Count,
			AnimalRegistry.Count ),
		HashCode.Combine(
			GuestSystem.Instance?.GuestCount ?? 0,
			(int)(GuestSystem.Instance?.Satisfaction ?? 0f),
			(int)(GuestSystem.Instance?.Cleanliness ?? 0f),
			(int)(GuestSystem.Instance?.ZooRating ?? 0f) ),
		HashCode.Combine(
			PathNetwork.HasEntrance,
			PathNetwork.HasGuestAccess,
			PathNetwork.GetConnectedPaths().Count ) );

	public static long LifetimeProfit =>
		(ZooState.Instance?.TotalEarned ?? 0) - (ZooState.Instance?.TotalSpent ?? 0);

	public static float IncomePerMinute => EconomySystem.Instance?.IncomePerMinute ?? 0f;
	public static float RevenuePerMinute => EconomySystem.Instance?.RevenuePerMinute ?? 0f;
	public static float ExpensePerMinute => EconomySystem.Instance?.ExpensePerMinute ?? 0f;

	public static float SatisfactionBreakdownHabitat =>
		(HabitatRegistry.Count > 0 ? HabitatRegistry.AverageScore() : 50f) * 0.30f;

	public static float SatisfactionBreakdownCleanliness =>
		(GuestSystem.Instance?.Cleanliness ?? 100f) * 0.20f;

	public static float SatisfactionBreakdownVariety
	{
		get
		{
			var variety = AnimalRegistry.DistinctSpeciesCount();
			return (variety / 5f).Clamp( 0f, 1f ) * 100f * 0.15f;
		}
	}

	public static float SatisfactionBreakdownAmenities =>
		GuestAmenities.SatisfactionScore( GuestSystem.Instance?.GuestCount ?? 0 ) * 0.35f;

	public static IReadOnlyList<GuestInsight> GetRatingHarms()
	{
		var key = InsightCacheKey();
		if ( _harmsCacheValid && key == _insightCacheKey )
			return _harmsCache;

		try
		{
			_harmsCache = BuildRatingHarms();
			_harmsCacheValid = true;
			_insightCacheKey = key;
			return _harmsCache;
		}
		catch ( Exception e )
		{
			Log.Warning( $"[Fauna2 Stats] GetRatingHarms failed — {e.Message}" );
			return Array.Empty<GuestInsight>();
		}
	}

	static List<GuestInsight> BuildRatingHarms()
	{
		var harms = new List<GuestInsight>();
		var guests = GuestSystem.Instance;
		var guestCount = guests?.GuestCount ?? 0;
		var (restCov, foodCov, shopCov) = GuestAmenities.Coverages( guestCount );

		if ( !PathNetwork.HasEntrance )
		{
			harms.Add( new GuestInsight
			{
				Icon = "door_front", Title = "No zoo entrance",
				Detail = "Visitors cannot enter without an entrance on the edge of your land.",
				Priority = 100, IsPositive = false,
			} );
		}
		else if ( !PathNetwork.HasGuestAccess )
		{
			harms.Add( new GuestInsight
			{
				Icon = "route", Title = "Paths not connected",
				Detail = "Lay path tiles from your entrance so guests can explore.",
				Priority = 95, IsPositive = false,
			} );
		}

		if ( guestCount > 0 )
		{
			if ( PlaceableRegistry.RestroomCount == 0 )
			{
				harms.Add( new GuestInsight
				{
					Icon = "wc", Title = "No restrooms",
					Detail = "Guest satisfaction is tanking — build a restroom next to your paths.",
					Priority = 90, IsPositive = false,
				} );
			}
			else if ( restCov < 50f )
			{
				harms.Add( new GuestInsight
				{
					Icon = "wc", Title = "Not enough restrooms",
					Detail = $"Restroom coverage is {restCov:0}% — add more for {guestCount} guests.",
					Priority = 70, IsPositive = false,
				} );
			}

			if ( PlaceableRegistry.RestaurantCount == 0 )
			{
				harms.Add( new GuestInsight
				{
					Icon = "restaurant", Title = "No restaurant",
					Detail = "Hungry guests leave unhappy — build a restaurant near paths.",
					Priority = 88, IsPositive = false,
				} );
			}
			else if ( foodCov < 50f )
			{
				harms.Add( new GuestInsight
				{
					Icon = "restaurant", Title = "Not enough food service",
					Detail = $"Restaurant coverage is {foodCov:0}% — add more dining for {guestCount} guests.",
					Priority = 68, IsPositive = false,
				} );
			}

			if ( PlaceableRegistry.ShopCount > 0 && shopCov < 75f )
			{
				harms.Add( new GuestInsight
				{
					Icon = "storefront", Title = "Not enough shops",
					Detail = $"Shop coverage is {shopCov:0}% — add more gift shops for {guestCount} guests.",
					Priority = 62, IsPositive = false,
				} );
			}
			else if ( guestCount >= 40 && PlaceableRegistry.ShopCount == 0 )
			{
				harms.Add( new GuestInsight
				{
					Icon = "storefront", Title = "No gift shops",
					Detail = "Souvenir shops earn bonus cash and keep bigger crowds happy.",
					Priority = 58, IsPositive = false,
				} );
			}
		}

		var cleanliness = guests?.Cleanliness ?? 100f;
		if ( cleanliness < 65f )
		{
			harms.Add( new GuestInsight
			{
				Icon = "cleaning_services", Title = "Zoo is getting dirty",
				Detail = $"Cleanliness is {cleanliness:0}% — crowds wear paths down over time.",
				Priority = 55, IsPositive = false,
			} );
		}

		if ( HabitatRegistry.Count == 0 )
		{
			harms.Add( new GuestInsight
			{
				Icon = "fence", Title = "No habitats",
				Detail = "Guests came to see animals — build habitats first.",
				Priority = 80, IsPositive = false,
			} );
		}
		else if ( HabitatRegistry.AverageScore() < 50f )
		{
			harms.Add( new GuestInsight
			{
				Icon = "park", Title = "Poor habitat quality",
				Detail = $"Average habitat score is {HabitatRegistry.AverageScore():0} — enrich and decorate habitats.",
				Priority = 50, IsPositive = false,
			} );
		}

		if ( AnimalRegistry.Count == 0 && guestCount > 0 )
		{
			harms.Add( new GuestInsight
			{
				Icon = "pets", Title = "No animals on display",
				Detail = "Adopt animals from the market so guests have something to see.",
				Priority = 85, IsPositive = false,
			} );
		}

		var species = AnimalRegistry.DistinctSpeciesCount();
		if ( species < 2 && guestCount >= 10 )
		{
			harms.Add( new GuestInsight
			{
				Icon = "diversity_3", Title = "Low species variety",
				Detail = "Guests want more than one species — expand your collection.",
				Priority = 45, IsPositive = false,
			} );
		}

		var satisfaction = guests?.Satisfaction ?? 100f;
		if ( satisfaction < 45f && guestCount > 0 )
		{
			harms.Add( new GuestInsight
			{
				Icon = "sentiment_dissatisfied", Title = "Guest satisfaction is low",
				Detail = $"Overall satisfaction is {satisfaction:0}% — fix the issues below to recover your rating.",
				Priority = 60, IsPositive = false,
			} );
		}

		return harms.OrderByDescending( h => h.Priority ).ToList();
	}

	public static IReadOnlyList<GuestInsight> GetGuestWants()
	{
		var key = InsightCacheKey();
		if ( _wantsCacheValid && key == _insightCacheKey )
			return _wantsCache;

		try
		{
			_wantsCache = BuildGuestWants();
			_wantsCacheValid = true;
			_insightCacheKey = key;
			return _wantsCache;
		}
		catch ( Exception e )
		{
			Log.Warning( $"[Fauna2 Stats] GetGuestWants failed — {e.Message}" );
			return Array.Empty<GuestInsight>();
		}
	}

	static List<GuestInsight> BuildGuestWants()
	{
		var wants = new List<GuestInsight>();
		var guestCount = GuestSystem.Instance?.GuestCount ?? 0;
		var species = AnimalRegistry.DistinctSpeciesCount();
		var connectedPaths = PathNetwork.GetConnectedPaths().Count;

		if ( species < 4 )
		{
			wants.Add( new GuestInsight
			{
				Icon = "menu_book", Title = "More species to discover",
				Detail = species == 0
					? "Adopt your first animal from the market (N)."
					: $"Guests enjoy variety — you have {species} species; aim for 4+.",
				Priority = 40, IsPositive = true,
			} );
		}

		if ( guestCount > 0 && connectedPaths < 8 )
		{
			wants.Add( new GuestInsight
			{
				Icon = "route", Title = "A longer walking route",
				Detail = "Extend your path network so guests spend more time (and money) in the zoo.",
				Priority = 35, IsPositive = true,
			} );
		}

		if ( PlaceableRegistry.TotalAppeal() < 20f && HabitatRegistry.Count > 0 )
		{
			wants.Add( new GuestInsight
			{
				Icon = "emoji_objects", Title = "Prettier surroundings",
				Detail = "Benches, fountains and flowers raise appeal and draw more visitors.",
				Priority = 30, IsPositive = true,
			} );
		}

		if ( guestCount >= 15 && HabitatRegistry.AverageScore() < 70f )
		{
			wants.Add( new GuestInsight
			{
				Icon = "grass", Title = "Better animal habitats",
				Detail = "Add enrichment, water and shelter inside habitats to impress guests.",
				Priority = 38, IsPositive = true,
			} );
		}

		if ( guestCount > 0 && GuestAmenities.HasMinimumAmenities )
		{
			var (rest, food, shop) = GuestAmenities.Coverages( guestCount );
			if ( rest >= 90f && food >= 90f && (PlaceableRegistry.ShopCount == 0 || shop >= 90f) )
			{
				wants.Add( new GuestInsight
				{
					Icon = "thumb_up", Title = "Amenities are keeping up",
					Detail = "Restrooms, dining and shops cover your current crowd well.",
					Priority = 20, IsPositive = true,
				} );
			}
		}

		if ( (GuestSystem.Instance?.ZooRating ?? 0f) >= 4f )
		{
			wants.Add( new GuestInsight
			{
				Icon = "star", Title = "Excellent zoo reputation",
				Detail = "Keep it up — high ratings attract more visitors and income.",
				Priority = 10, IsPositive = true,
			} );
		}

		if ( wants.Count == 0 )
		{
			wants.Add( new GuestInsight
			{
				Icon = "waving_hand", Title = "Ready for visitors",
				Detail = "Build habitats, connect paths, and adopt animals to grow your zoo.",
				Priority = 5, IsPositive = true,
			} );
		}

		return wants.OrderByDescending( w => w.Priority ).ToList();
	}

	public static void InvalidateInsightCache()
	{
		_harmsCacheValid = false;
		_wantsCacheValid = false;
		_insightCacheKey = int.MinValue;
	}

	public static IReadOnlyList<LeaderboardRow> GetLocalStandings()
	{
		var state = ZooState.Instance;
		var guests = GuestSystem.Instance;
		var social = SocialSystem.Instance;

		return new List<LeaderboardRow>
		{
			new() { Stat = "Zoo Rating", Value = $"{guests?.ZooRating ?? 0f:0.0} / 5", Note = "Guest satisfaction & variety" },
			new() { Stat = "Prestige", Value = $"{state?.Prestige ?? 0:n0}", Note = "Discoveries & milestones" },
			new() { Stat = "Peak Guests", Value = $"{guests?.PeakGuests ?? 0:n0}", Note = "Busiest moment" },
			new() { Stat = "Lifetime Revenue", Value = $"${state?.TotalEarned ?? 0:n0}", Note = "All guest spending" },
			new() { Stat = "Lifetime Profit", Value = $"${LifetimeProfit:n0}", Note = "Revenue minus costs" },
			new() { Stat = "Likes", Value = $"{social?.Likes.Count ?? 0:n0}", Note = "Community thumbs-up" },
			new() { Stat = "Total Visitors", Value = $"{social?.TotalVisitors ?? 0:n0}", Note = "Friends who joined" },
			new() { Stat = "Codex", Value = $"{CollectionSystem.Instance?.CompletionPercent ?? 0f:0}%", Note = "Species discovered" },
		};
	}
}
