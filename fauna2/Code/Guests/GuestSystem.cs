namespace Fauna2;

/// <summary>
/// Aggregate guest simulation — no per-guest agents. The host periodically
/// derives total appeal from animals, habitats and decorations, then eases the
/// guest count toward it. Satisfaction, cleanliness and the star rating all
/// fall out of the same cheap pass, which scales to "thousands of guests"
/// for free. Visual-only ambient guests are spawned locally by AmbientGuests.
/// </summary>
public sealed class GuestSystem : Component
{
	public static GuestSystem Instance { get; private set; }

	[Sync( SyncFlags.FromHost )] public int GuestCount { get; set; }
	[Sync( SyncFlags.FromHost )] public int PeakGuests { get; set; }
	[Sync( SyncFlags.FromHost )] public float Satisfaction { get; set; } = 75f;
	[Sync( SyncFlags.FromHost )] public float Cleanliness { get; set; } = 100f;
	[Sync( SyncFlags.FromHost )] public float Appeal { get; set; }
	/// <summary>How well restrooms cover current guests (0–100).</summary>
	[Sync( SyncFlags.FromHost )] public float RestroomCoverage { get; set; } = 100f;
	/// <summary>How well restaurants cover current guests (0–100).</summary>
	[Sync( SyncFlags.FromHost )] public float RestaurantCoverage { get; set; } = 100f;
	/// <summary>How well shops cover current guests (0–100).</summary>
	[Sync( SyncFlags.FromHost )] public float ShopCoverage { get; set; } = 100f;
	/// <summary>Star rating, 0–5.</summary>
	[Sync( SyncFlags.FromHost )] public float ZooRating { get; set; }

	private TimeUntil _nextTick;

	protected override void OnAwake() => Instance = this;
	protected override void OnDestroy() { if ( Instance == this ) Instance = null; }

	protected override void OnFixedUpdate()
	{
		if ( IsProxy || !_nextTick ) return;
		_nextTick = GameConstants.GuestTickInterval;

		var clock = DebugStats.StartTimer();
		Tick();
		DebugStats.StopTimer( "Guests", clock );
	}

	private void Tick()
	{
		// ── Appeal ──────────────────────────────────────────
		var animalAppeal = 0f;
		var rareBonus = 0f;

		foreach ( var animal in AnimalRegistry.All )
		{
			animalAppeal += animal.EffectiveAppeal;

			if ( animal.Variant is not null )
				rareBonus += 10f;
			if ( animal.Definition?.Rarity >= AnimalRarity.Rare )
				rareBonus += 5f;
		}

		var variety = AnimalRegistry.DistinctSpeciesCount();
		var varietyBonus = variety * GameConstants.VarietyAppealPerSpecies * GameConstants.GamePaceMultiplier;
		var decorAppeal = PlaceableRegistry.TotalAppeal();
		var weather = WeatherSeasonSystem.Instance;
		var eventSystem = SanctuaryEventSystem.Instance;

		Appeal = animalAppeal + varietyBonus + decorAppeal + rareBonus
			+ PlaceableRegistry.TotalEducation() * 0.65f
			+ (StaffSystem.Instance?.GuideGuestBonus ?? 0f)
			+ (eventSystem?.GuestAppealBonus ?? 0f)
			+ (ZooState.Instance?.GuestAppealModifier ?? 0f);

		if ( weather is not null )
			Appeal += BiomeIdentity.GuestAppealBonus( ZooState.Instance?.StarterBiome ?? Biome.Grassland, weather.Season, weather.Weather );

		// ── Satisfaction ────────────────────────────────────
		var habitatQuality = HabitatRegistry.Count > 0 ? HabitatRegistry.AverageScore() : 50f;
		var varietyFactor = (variety / 5f).Clamp( 0f, 1f ) * 100f;
		(RestroomCoverage, RestaurantCoverage, ShopCoverage) = GuestAmenities.Coverages( GuestCount );
		var amenityFactor = GuestAmenities.SatisfactionScore( GuestCount );
		var comfort = PlaceableRegistry.TotalComfort().Clamp( 0f, 30f );

		var targetSatisfaction =
			habitatQuality * 0.30f +
			Cleanliness * 0.20f +
			varietyFactor * 0.15f +
			amenityFactor * 0.35f +
			comfort +
			(ResearchSystem.Instance?.GuestComfortBonus ?? 0f);

		Satisfaction = Satisfaction.LerpTo( targetSatisfaction, 0.15f ).Clamp( 0f, 100f );

		// ── Guest count ─────────────────────────────────────
		var satisfactionFactor = 0.35f + Satisfaction / 100f * 0.75f;
		var target = PathNetwork.HasGuestAccess
			? (int)(Appeal * 1.05f * satisfactionFactor)
			: 0;
		target = (int)(target * (weather?.GuestModifier ?? 1f) * (FranchiseSystem.Instance?.LegacyGuestMultiplier ?? 1f));
		target = Math.Min( target, PlotSystem.Instance?.GuestCap ?? target );

		var delta = (target - GuestCount) * GameConstants.GuestLerpRate * 2f * GameConstants.GamePaceMultiplier;
		var noise = target > 0 ? Game.Random.Float( -1f, 1f ) : 0f; // no phantom guests in an empty zoo
		GuestCount = Math.Max( 0, GuestCount + (int)MathF.Round( delta + noise ) );
		PeakGuests = Math.Max( PeakGuests, GuestCount );

		// ── Cleanliness (abstracted maintenance) ────────────
		var dirt = GuestCount * 0.004f * GameConstants.GuestTickInterval;
		var recovery = (1.2f + (StaffSystem.Instance?.CleanerRecoveryBonus ?? 0f)) * GameConstants.GuestTickInterval;
		Cleanliness = (Cleanliness - dirt + recovery).Clamp( 40f, 100f );

		// ── Star rating ─────────────────────────────────────
		var ratingScore =
			Satisfaction / 100f * 2.0f +
			(variety / 8f).Clamp( 0f, 1f ) * 1.5f +
			(habitatQuality / 100f) * 1.0f +
			((ZooState.Instance?.Prestige ?? 0) / 150f).Clamp( 0f, 1f ) * 0.5f;

		ZooRating = ZooRating.LerpTo( ratingScore.Clamp( 0f, 5f ), 0.2f );
	}
}
