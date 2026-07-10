namespace Fauna2;

/// <summary>Projected steady-state cash flow from adopting one more animal.</summary>
public readonly struct AdoptionEconomics
{
	public float NetPerMinute { get; init; }
	public float RevenueDelta { get; init; }
	public float CostDelta { get; init; }
	public int GuestDelta { get; init; }
	public bool HasGuestAccess { get; init; }

	public string NetLabel
	{
		get
		{
			var net = NetPerMinute >= 0f ? $"+${NetPerMinute:0}" : $"−${Math.Abs( NetPerMinute ):0}";
			return $"{net}/min";
		}
	}

	public string DetailLabel
	{
		get
		{
			if ( !HasGuestAccess )
				return "Upkeep only until guests can enter";

			if ( GuestDelta > 0 )
				return $"~{GuestDelta} more guests · +${RevenueDelta:0} revenue";

			if ( GuestDelta < 0 )
				return $"{GuestDelta} guests · +${RevenueDelta:0} revenue";

			return $"No guest change · +${RevenueDelta:0} revenue";
		}
	}
}

/// <summary>
/// Estimates how adopting an animal shifts zoo cash flow, mirroring appeal,
/// guest targets, and operating costs used by the live economy.
/// </summary>
public static class AnimalEconomics
{
	public static AdoptionEconomics ProjectAdoption( AnimalDefinition def )
	{
		if ( def is null ) return default;

		var animalCost = GameConstants.OperatingCostPerAnimalPerMinute;

		if ( !PathNetwork.HasGuestAccess )
		{
			return new AdoptionEconomics
			{
				NetPerMinute = -animalCost,
				CostDelta = animalCost,
				HasGuestAccess = false,
			};
		}

		var guests = GuestSystem.Instance;
		var state = ZooState.Instance;
		var currentAppeal = guests?.Appeal ?? 0f;
		var appealDelta = EstimateAppealContribution( def, state );
		var satisfaction = ProjectSatisfaction( def, guests );
		var satisfactionFactor = 0.35f + satisfaction / 100f * 0.75f;
		var guestCap = PlotSystem.Instance?.GuestCap ?? int.MaxValue;

		var currentTarget = TargetGuests( currentAppeal, satisfactionFactor, guestCap );
		var newTarget = TargetGuests( currentAppeal + appealDelta, satisfactionFactor, guestCap );
		var guestDelta = newTarget - currentTarget;

		var revenueDelta = GuestRevenue.PerMinute( newTarget, satisfaction ) - GuestRevenue.PerMinute( currentTarget, satisfaction );
		var guestCostDelta = guestDelta * GameConstants.OperatingCostPerGuestPerMinute;
		var costDelta = animalCost + guestCostDelta;

		return new AdoptionEconomics
		{
			NetPerMinute = revenueDelta - costDelta,
			RevenueDelta = revenueDelta,
			CostDelta = costDelta,
			GuestDelta = guestDelta,
			HasGuestAccess = true,
		};
	}

	private static int TargetGuests( float appeal, float satisfactionFactor, int guestCap )
	{
		var target = (int)(appeal * 1.05f * satisfactionFactor);
		return Math.Min( target, guestCap );
	}

	private static float EstimateAppealContribution( AnimalDefinition def, ZooState state )
	{
		var appeal = def.GuestAppeal;
		appeal *= 0.5f + 70f / 200f;

		if ( state.IsValid() && def.Biome == state.StarterBiome )
			appeal *= 1f + state.NativeGuestAppealBonus;

		if ( IsNewSpecies( def ) )
			appeal += GameConstants.VarietyAppealPerSpecies;

		if ( def.Rarity >= AnimalRarity.Rare )
			appeal += 5f;

		return appeal;
	}

	private static float ProjectSatisfaction( AnimalDefinition def, GuestSystem guests )
	{
		if ( guests is null ) return 75f;

		var variety = AnimalRegistry.DistinctSpeciesCount();
		if ( IsNewSpecies( def ) )
			variety++;

		var habitatQuality = HabitatRegistry.Count > 0 ? HabitatRegistry.AverageScore() : 50f;
		var varietyFactor = (variety / 5f).Clamp( 0f, 1f ) * 100f;
		var amenityFactor = GuestAmenities.SatisfactionScore( guests.GuestCount );

		return (
			habitatQuality * 0.30f +
			guests.Cleanliness * 0.20f +
			varietyFactor * 0.15f +
			amenityFactor * 0.35f
		).Clamp( 0f, 100f );
	}

	private static bool IsNewSpecies( AnimalDefinition def )
	{
		var id = Defs.IdOf( def );
		return !AnimalRegistry.All.Any( a => a.DefinitionId == id );
	}
}
