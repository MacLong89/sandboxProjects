namespace FinalOutpost;

/// <summary>
/// Plot support pads: Spotlight range, Ammo fire rate, Hardpoint armor, Radio recruit buffs,
/// plus Oil Slick slows and Mine/Artillery splash helpers.
/// </summary>
public static class DefenseEffects
{
	public const float SpotlightRangeMult = 1.5f;
	/// <summary>Ammo Depot — towers/recruits fire this much faster (interval ÷ mult).</summary>
	public const float AmmoDepotFireRateMult = 1.25f;
	/// <summary>Hardpoint — structures take this fraction of incoming damage.</summary>
	public const float HardpointDamageTakenMult = 0.8f;
	/// <summary>Radio Mast — recruit move speed on the plot.</summary>
	public const float RadioMastMoveMult = 1.2f;
	/// <summary>Radio Mast — wider auto-acquire so recruits snap to threats sooner.</summary>
	public const float RadioMastAcquireMult = 1.25f;
	/// <summary>Oil Slick move multiplier while coated (0.4 = 60% slow).</summary>
	public const float OilSlowMult = 0.4f;
	public const float OilSlowLinger = 0.55f;

	public static bool PlotHasSupport( int plotX, int plotY, BuildableId id )
	{
		var build = BuildManager.Instance;
		if ( build is null ) return false;

		foreach ( var b in build.Buildings )
		{
			if ( b is null || b.IsDestroyed || b.Type != id )
				continue;
			if ( !build.TryGetPlotForBuilding( b, out var px, out var py ) )
				continue;
			if ( px == plotX && py == plotY )
				return true;
		}

		return false;
	}

	public static bool WorldHasSupport( Vector3 world, BuildableId id )
	{
		if ( !PlotGrid.WorldToPlot( world, out var px, out var py ) )
			return false;
		return PlotHasSupport( px, py, id );
	}

	public static bool PlotHasSpotlight( int plotX, int plotY ) =>
		PlotHasSupport( plotX, plotY, BuildableId.Spotlight );

	public static bool WorldHasSpotlight( Vector3 world ) =>
		WorldHasSupport( world, BuildableId.Spotlight );

	public static float RangeMultAt( Vector3 world ) =>
		WorldHasSpotlight( world ) ? SpotlightRangeMult : 1f;

	public static float RangeMultForBuilding( PlacedBuilding building )
	{
		if ( building is null ) return 1f;
		if ( BuildManager.Instance?.TryGetPlotForBuilding( building, out var px, out var py ) == true
		     && PlotHasSpotlight( px, py ) )
			return SpotlightRangeMult;
		return 1f;
	}

	public static float EffectiveTowerRange( PlacedBuilding building, UpgradeSystem upgrades )
	{
		if ( building is null ) return 0f;
		var baseRange = building.Def.Range( building.Level ) + (upgrades?.TurretRangeBonus ?? 0f);
		return baseRange * RangeMultForBuilding( building );
	}

	public static float FireIntervalMultAt( Vector3 world ) =>
		WorldHasSupport( world, BuildableId.AmmoDepot ) ? 1f / AmmoDepotFireRateMult : 1f;

	public static float FireIntervalMultForBuilding( PlacedBuilding building )
	{
		if ( building is null ) return 1f;
		if ( BuildManager.Instance?.TryGetPlotForBuilding( building, out var px, out var py ) == true
		     && PlotHasSupport( px, py, BuildableId.AmmoDepot ) )
			return 1f / AmmoDepotFireRateMult;
		return 1f;
	}

	public static float DamageTakenMultAt( Vector3 world ) =>
		WorldHasSupport( world, BuildableId.Hardpoint ) ? HardpointDamageTakenMult : 1f;

	public static float DamageTakenMultForBuilding( PlacedBuilding building )
	{
		if ( building is null ) return 1f;
		if ( BuildManager.Instance?.TryGetPlotForBuilding( building, out var px, out var py ) == true
		     && PlotHasSupport( px, py, BuildableId.Hardpoint ) )
			return HardpointDamageTakenMult;
		return 1f;
	}

	public static float RecruitMoveMultAt( Vector3 world ) =>
		WorldHasSupport( world, BuildableId.RadioMast ) ? RadioMastMoveMult : 1f;

	public static float RecruitAcquireMultAt( Vector3 world ) =>
		WorldHasSupport( world, BuildableId.RadioMast ) ? RadioMastAcquireMult : 1f;
}
