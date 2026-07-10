namespace Sandbox;

/// <summary>
/// Formal rule catalog for procedural buildings. Bump <see cref="RulesetVersion"/> when hard rules change
/// so seeded layouts stay comparable across builds.
/// </summary>
public static class ThornsProcBuildingRules
{
	/// <summary>Increment when hard-rule semantics change (invalidates old seed comparisons).</summary>
	public const int RulesetVersion = 6;

	public const int MaxGenerationAttempts = 24;
	public const int MinQualityScoreToAccept = 42;

	public const int MinWalkableCellsGround = 4;
	public const int MinWalkableCellsPerUpperFloor = 3;

	/// <summary>Soft: rooms smaller than this are penalized.</summary>
	public const int MinPreferredRoomCells = 2;

	/// <summary>Soft: very large open areas without dividers are penalized.</summary>
	public const int MaxPreferredRoomCells = 14;

	/// <summary>Soft: exterior window chance target (spawn uses ~20%).</summary>
	public const float ExteriorWindowChanceTarget = 0.2f;

	public const float ExteriorWindowChanceMaxHard = 0.45f;

	/// <summary>Interior divider placement attempt rate before connectivity filter.</summary>
	public const float InteriorWallCandidateRate = 0.22f;

	public const int MaxInteriorWallsPerStory = 48;

	public enum RuleTier
	{
		Hard,
		Soft
	}

	public enum RuleCategory
	{
		Connectivity,
		MultiFloor,
		Room,
		WallWindow,
		Structural,
		Navigation,
		Gameplay,
		PropPlacement
	}

	public enum RuleId
	{
		// Hard — connectivity
		EveryWalkableReachableFromDoor,
		NoIsolatedFloors,
		DoorOpensOntoWalkableFloor,
		DoorNotOnRampShaft,

		// Hard — multi-floor
		LowerFloorHasRampWhenMultiStory,
		UpperFloorHasShaftAboveRamp,
		RampNotStackedOnShaft,
		VerticalAccessReachable,

		// Hard — structural
		NoFloatingUpperCells,
		UpperFloorSupportedByBelow,

		// Hard — rooms / navigation (with interior wall plan)
		InteriorWallsPreserveConnectivity,
		NoFullySealedRooms,

		// Hard — walls
		WindowsOnlyOnExterior,

		// Soft — rooms
		PreferVariedRoomSizes,
		AvoidTinyRooms,
		AvoidHugeEmptyRooms,

		// Soft — navigation / PvP
		LimitDeadEnds,
		LargeRoomsHaveMultipleExits,
		LimitLongSightlines,

		// Soft — props (post-placement)
		PropsOnReachableFloor,
		PropsDoNotBlockDoorRamp
	}

	public enum GenerationStage
	{
		FootprintSilhouette,
		UpperFloorsAndSupport,
		RampAssignment,
		InteriorWallPlan,
		DoorSelection,
		HardValidation,
		SoftQualityScore,
		PropPlacement,
		PostPropValidation
	}

	public readonly record struct RuleDefinition(
		RuleId Id,
		RuleTier Tier,
		RuleCategory Category,
		string Summary );

	public static IReadOnlyList<RuleDefinition> All { get; } = BuildAll();

	static List<RuleDefinition> BuildAll() =>
	[
		Hard( RuleId.EveryWalkableReachableFromDoor, RuleCategory.Connectivity,
			"Flood-fill from door must visit every walkable cell on every floor." ),
		Hard( RuleId.NoIsolatedFloors, RuleCategory.Connectivity,
			"No storey may contain walkable cells disconnected from the entrance component." ),
		Hard( RuleId.DoorOpensOntoWalkableFloor, RuleCategory.Connectivity,
			"Ground door interior cell must be walkable." ),
		Hard( RuleId.DoorNotOnRampShaft, RuleCategory.Connectivity,
			"Door cannot open on ramp or ramp-shaft tiles." ),

		Hard( RuleId.LowerFloorHasRampWhenMultiStory, RuleCategory.MultiFloor,
			"Buildings with 2+ floors must place a ramp on each storey below the top." ),
		Hard( RuleId.UpperFloorHasShaftAboveRamp, RuleCategory.MultiFloor,
			"Floor above each ramp must omit slabs on the shaft cell and the ascent headroom cell (two openings)." ),
		Hard( RuleId.RampNotStackedOnShaft, RuleCategory.MultiFloor,
			"No ramp on the storey above may occupy the shaft or headroom cells required by a ramp below." ),
		Hard( RuleId.VerticalAccessReachable, RuleCategory.MultiFloor,
			"Every upper-floor walkable cell must reach ground via ramps and landings." ),

		Hard( RuleId.NoFloatingUpperCells, RuleCategory.Structural,
			"No occupied upper cell without occupied cell directly below." ),
		Hard( RuleId.UpperFloorSupportedByBelow, RuleCategory.Structural,
			"Upper footprint must be subset of supported lower footprint." ),

		Hard( RuleId.InteriorWallsPreserveConnectivity, RuleCategory.Room,
			"Interior dividers must not disconnect any walkable tile from the door." ),
		Hard( RuleId.NoFullySealedRooms, RuleCategory.Room,
			"Each room region must have at least one opening to another region or exterior." ),

		Hard( RuleId.WindowsOnlyOnExterior, RuleCategory.WallWindow,
			"Windows may only spawn on perimeter edges (never on interior dividers)." ),

		Soft( RuleId.PreferVariedRoomSizes, RuleCategory.Room,
			"Reward mixed small/medium room sizes instead of one blob." ),
		Soft( RuleId.AvoidTinyRooms, RuleCategory.Room,
			"Penalize 1-cell closet rooms unless intentional loot niche." ),
		Soft( RuleId.AvoidHugeEmptyRooms, RuleCategory.Room,
			"Penalize single rooms larger than preferred max without dividers." ),

		Soft( RuleId.LimitDeadEnds, RuleCategory.Navigation,
			"Penalize degree-1 dead-end corridors (except small loot niches)." ),
		Soft( RuleId.LargeRoomsHaveMultipleExits, RuleCategory.Gameplay,
			"Rooms above size threshold should have 2+ connections." ),
		Soft( RuleId.LimitLongSightlines, RuleCategory.Gameplay,
			"Penalize very long straight walkable runs on ground floor." ),

		Soft( RuleId.PropsOnReachableFloor, RuleCategory.PropPlacement,
			"Loot/radio props only on flood-fill reachable tiles." ),
		Soft( RuleId.PropsDoNotBlockDoorRamp, RuleCategory.PropPlacement,
			"Props must not occupy door, ramp, or shaft cells." )
	];

	static RuleDefinition Hard( RuleId id, RuleCategory cat, string summary ) =>
		new( id, RuleTier.Hard, cat, summary );

	static RuleDefinition Soft( RuleId id, RuleCategory cat, string summary ) =>
		new( id, RuleTier.Soft, cat, summary );

	public static RuleCategory CategoryFor( RuleId id )
	{
		foreach ( var def in All )
		{
			if ( def.Id == id )
				return def.Category;
		}

		return RuleCategory.Structural;
	}
}
