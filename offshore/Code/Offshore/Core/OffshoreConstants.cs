namespace Offshore;

/// <summary>Central Stage 2 tuning. Balance lives here so cast code stays free of magic numbers.</summary>
public static class OffshoreConstants
{
	/// <summary>
	/// Side-view world (s and box is Z-up). Screen mapping we use for gameplay/art:
	/// Screen X → World X (left ↔ right), Screen Y → World Z (down ↔ up),
	/// Screen depth → World Y (toward camera = smaller Y). See <see cref="ScreenAxes"/>.
	/// </summary>
	public const float DockEdgeX = 0f;
	public const float RodTipX = 1.5f;
	public const float RodTipZ = 2.4f;

	// Player / boat locomotion (X = along shore↔sea, Z = screen vertical)
	public const float PlayerWalkSpeed = 7.5f;
	public const float BoatMoveSpeed = 9f;
	public const float PlayerStartX = 12.2f; // pier tip on world dock
	/// <summary>Deck Z for the angler (~5px higher than prior).</summary>
	public const float PlayerStartZ = -0.69f;
	/// <summary>Empty boat moored at the right end of the dock hub (into the water).</summary>
	public const float BoatMooringX = 17.0f;
	/// <summary>Empty boat waterline Z. ~10px lower than prior so hull sits in the water.</summary>
	public const float BoatMooringZ = -2.1f;
	/// <summary>On-foot board zone covering the pier tip → moored hull.</summary>
	public const float BoatZoneMinX = 11.5f;
	public const float BoatZoneMaxX = 20.5f;
	public const float BoatInteractRadius = 5.0f;
	/// <summary>Drive-up disembark band — boat alongside the dock / pier tip.</summary>
	public const float DockExitZoneMinX = 9.0f;
	public const float DockExitZoneMaxX = 20.5f;
	/// <summary>On-dock fishing limits (no boat boarded).</summary>
	public const float DockFishingMaxDepth = 4.5f;
	public const float DockFishingMaxFishSize = 1.35f;
	public const float PlayerMinX = -40f;
	public const float PlayerMaxX = 95f;
	public const float PlayerMinZ = 1.6f;
	public const float PlayerMaxZ = 4.2f;
	public const float RodTipOffsetX = 0.85f;
	public const float RodTipOffsetY = -0.05f;
	public const float RodTipOffsetZ = 0.75f;
	/// <summary>World Y of angler (closer to camera than ocean plate so he draws in front).</summary>
	public const float FisherPlaneY = -25f; // CamDistance 72 − depth 47
	public const float PlayerSpriteWidth = 2.25f;
	public const float PlayerSpriteHeight = 2.25f;
	/// <summary>Shared boat world height (dock moored + boarded). Width from that boat's empty PNG aspect.</summary>
	public const float BoatWorldHeight = 3.6f;
	/// <summary>Fisherman sit offset on the boat (world Z up from boat center).</summary>
	public const float BoatSeatOffsetZ = 0.55f;
	/// <summary>Pull fisherman slightly toward camera so he draws in front of the hull.</summary>
	public const float BoatSeatOffsetY = -0.2f;

	// Water starts just past the dock edge and opens to the right
	public const float WaterMinX = 3f;
	public const float WaterMaxX = 95f;
	public const float WaterSurfaceZ = 0f;
	public const float WaterMinZ = -28f;
	public const float WaterMaxZ = 0.15f;
	/// <summary>How thick the water slab is along camera axis (Y).</summary>
	public const float WaterSlabDepthY = 4f;

	// Cast feel
	public const float MinAimDegrees = -5f;
	public const float MaxAimDegrees = 70f;
	public const float DefaultAimDegrees = 35f;
	public const float AimSpeedDegrees = 55f;
	public const float ChargeRate = 0.85f;
	public const float MinChargeToCast = 0.08f;
	public const float MinCastDistance = 8f;
	public const float MaxCastDistance = 28f;
	public const float CastFlightSeconds = 0.55f;
	/// <summary>~40px below the angler deck (PlayerStartZ) when the bobber rests in water.</summary>
	public const float BobberBelowPlayerZ = 3.55f;
	/// <summary>Legacy name — kept for balance config; landing uses PlayerStartZ − BobberBelowPlayerZ.</summary>
	public const float HookSubmerge = BobberBelowPlayerZ;
	public const float ArcPeakScale = 0.35f;
	public const float CastTimeoutSeconds = 2.5f;

	// Reset timing
	public const float LandedHoldSeconds = 1.4f;
	public const float FailedHoldSeconds = 0.9f;

	// Camera sits on -Y looking toward +Y so world +X appears on screen-right
	// (dock on the LEFT, open water on the RIGHT).
	public const float CamDistance = 72f;
	public const float CamFollowSpeed = 6.5f;
	/// <summary>World X of the angler/dock edge used as the left frame anchor.</summary>
	public const float CamDockAnchorX = 1.2f;
	/// <summary>Mario-style: keep a little ocean ahead of the player when idle/moving right.</summary>
	public const float CamIdleLookAheadX = 5f;
	public const float CamBaseZ = 1.0f;
	public const float CamLookBias = 0.45f;
	public const float CamFocusMinX = -45f;
	public const float CamFocusMaxX = 90f;
	public const float CamFocusMinZ = -4f;
	public const float CamFocusMaxZ = 8f;
	public const float CamFov = 56f;
	public const float CamZNear = 1f;
	public const float CamZFar = 500f;

	// Layered scene art (sky/ocean follow camera; dock is a world side-scroller prop)
	public const float BackdropWidth = 95f;
	public const float BackdropHeight = 52f;
	public const float BackdropDepthY = 22f;
	public const float DockHubWidth = 31.25f; // ~0.661 × view half-width at dock depth
	public const float DockHubHeight = 20.82f; // 1024×682 aspect
	public const float DockHubWorldX = 2.0f;
	public const float DockHubWorldZ = -0.14f; // raised ~40px with fisherman
	/// <summary>World Y of dock (behind ocean plate so waves cover pilings).</summary>
	public const float DockPlaneY = -22f; // CamDistance 72 − depth 50
	/// <summary>Left half of the dock hub (bait shop) — walk here and press E.</summary>
	public const float ShopZoneMinX = -18f;
	public const float ShopZoneMaxX = 10f;
	public const float SeafloorWidth = 36f;
	public const float SeafloorHeight = 18f;
	public const float SeafloorCenterX = 8f;
	public const float SeafloorCenterZ = -8.5f;

	// Colors
	public static Color DockWood => new( 0.45f, 0.28f, 0.14f );
	public static Color DockPost => new( 0.32f, 0.2f, 0.1f );
	public static Color WaterDeep => new( 0.07f, 0.28f, 0.38f, 1f );
	public static Color WaterShallow => new( 0.22f, 0.52f, 0.58f, 1f );
	public static Color Seabed => new( 0.18f, 0.16f, 0.12f, 1f );
	public static Color HookColor => new( 0.85f, 0.85f, 0.9f );
	public static Color LineColor => new( 0.95f, 0.95f, 0.98f );
	public static Color AimPreviewColor => new( 1f, 1f, 1f, 0.35f );
	public static Color SkyTint => new( 0.95f, 0.72f, 0.55f );

	public static bool IsCastFlowState( FishingSessionState state ) =>
		state is FishingSessionState.DockIdle
			or FishingSessionState.AimingCast
			or FishingSessionState.ChargingCast
			or FishingSessionState.Casting
			or FishingSessionState.HookInWater
			or FishingSessionState.CastFailed;
}
