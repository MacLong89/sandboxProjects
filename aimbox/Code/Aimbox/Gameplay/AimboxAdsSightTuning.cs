namespace Sandbox;

/// <summary>ADS presentation — physical optics (viewmodel stays up, anim drives alignment).</summary>
public static class AimboxAdsSightTuning
{
	/// <summary>Reference hip FOV used when converting magnification to vertical ADS FOV.</summary>
	public const float HipFieldOfView = 80f;

	/// <summary>Iron-sight ADS vertical FOV — baseline for red-dot optics and magnification math.</summary>
	public const float IronSightAdsFov = 20f;

	/// <summary>Holo ADS zoom multiplier vs iron sights (1.2×).</summary>
	public const float HoloSightIronMagnification = 1.2f;

	/// <summary>Classic ranged scope zoom multiplier vs iron sights (4×).</summary>
	public const float RangedScopeIronMagnification = 4f;

	/// <summary>Raised RMR / red-dot ADS — same zoom as iron sights.</summary>
	public static float RedDotAdsFov => IronSightAdsFov;

	/// <summary>Holo sight ADS — 1.2× iron sight zoom.</summary>
	public static float HoloSightAdsFov => VerticalFovFromIronSightMagnification( HoloSightIronMagnification );

	/// <summary>Main-camera vertical FOV while classic sniper scope is active (4× iron).</summary>
	public static float SniperScopeViewFov => VerticalFovFromIronSightMagnification( RangedScopeIronMagnification );

	/// <summary>Classic scope aperture radius in vmin (screen circle the player sees through).</summary>
	public const float SniperClassicScopeApertureRadiusVmin = 42f;

	/// <summary>Legacy — main camera used to stay wide while PiP carried zoom; classic scope zooms the main eye.</summary>
	public const float SniperMainCameraAdsFov = 72f;

	/// <summary>ADS blend when scope-in finishes — classic overlay/zoom waits for the viewmodel motion (matches gun builder).</summary>
	public const float SniperClassicScopeEnterBlend = 0.98f;

	/// <summary>ADS blend when scope-out restores hip presentation (ring drops at halfway un-ADS).</summary>
	public const float SniperClassicScopeExitBlend = 0.5f;

	/// <summary>Legacy alias — use <see cref="SniperClassicScopeExitBlend"/> for asymmetric scope timing.</summary>
	public const float SniperClassicScopeBlend = SniperClassicScopeExitBlend;

	/// <summary>Extra hold after reaching enter blend before the scope ring appears.</summary>
	public const float SniperClassicScopeOverlayDelaySeconds = 0.25f;

	/// <summary>Legacy alias used by look sensitivity / debug.</summary>
	public static float SniperScopedFov => SniperScopeViewFov;

	/// <summary>Legacy — holo magnification was previously measured vs hip; now use <see cref="HoloSightIronMagnification"/>.</summary>
	public const float HoloSightAdsMagnification = HoloSightIronMagnification;

	/// <summary>Legacy — ranged magnification was previously measured vs hip; now use <see cref="RangedScopeIronMagnification"/>.</summary>
	public const float RangedScopeViewMagnification = RangedScopeIronMagnification;

	/// <summary>Below this ADS blend, viewmodel uses overlay pass (hip fire).</summary>
	public const float WorldPassAdsBlend = 0.2f;

	/// <summary>Legacy PiP threshold — classic scope uses <see cref="SniperClassicScopeBlend"/> instead.</summary>
	public const float SniperScopePipBlend = SniperClassicScopeBlend;

	/// <summary>Physical lens PiP is disabled — ranged sights use the classic black-ring overlay.</summary>
	public const bool RangedSightScopePipEnabled = false;

	/// <summary>Final panel radius multiplier for bolt-on ranged sight PiP.</summary>
	public const float RangedSightScopePipRadiusScale = 2.587f;

	/// <summary>Projected ring size for bolt-on sight_ranged (M700 uses <see cref="SniperScopePipProjectionHalfExtent"/>).</summary>
	public const float RangedSightScopePipProjectionHalfExtent = 4.25f;

	/// <summary>Panel-space nudge for bolt-on ranged sight PiP (positive X = right, positive Y = down).</summary>
	public const float RangedSightScopePipCenterXOffsetPixels = 236f;
	public const float RangedSightScopePipCenterYOffsetPixels = 408f;

	/// <summary>Legacy integrated M700 PiP without ranged sight attachment — disabled; use iron sights by default.</summary>
	public const bool M700IntegratedScopePipEnabled = false;

	public const float HideCrosshairBlend = 0.45f;

	/// <summary>Extra view-local forward slide while ADS (fallback when camera bone is unavailable).</summary>
	public const float SniperAdsForwardOffset = 2f;
	public const float RedDotAdsForwardOffset = 3.5f;
	public const float IronSightAdsForwardOffset = 2f;

	/// <summary>Fallback scope eye on weapon_root when model attachments are missing (+X forward, +Y left, +Z up).</summary>
	public static readonly Vector3 M700ScopeEyeWeaponRootOffset = new( 5f, 0f, 10.5f );

	/// <summary>World-units forward from scope-eye anchor to the lens plane for PiP screen projection.</summary>
	public const float M700ScopeLensForwardOffset = 10f;

	public const float M700ScopePipMinRadiusFraction = 0.055f;
	public const float M700ScopePipMaxRadiusFraction = 0.115f;

	/// <summary>During scope-in, ease PiP center from screen center toward the lens (full ADS tracks lens only).</summary>
	public const float M700ScopePipVerticalLockStartBlend = 0.55f;

	/// <summary>Panel-space nudge applied after screen projection (positive X = right, positive Y = down).</summary>
	public const float M700ScopePipCenterXOffsetPixels = 0f;
	public const float M700ScopePipCenterYOffsetPixels = -48f;
	public const float M700ScopePipRadiusScale = 0.660f;

	/// <summary>Half-extent (world units) for projecting scope ring diameter onto screen.</summary>
	public const float SniperScopePipProjectionHalfExtent = 6f;

	/// <summary>Viewmodel-local nudge at full ADS (viewmodel +Z ≈ raises optic on screen).</summary>
	public static readonly Vector3 M700ScopeAdsViewmodelFineTune = new( 0f, 0f, -13.75f );

	/// <summary>Deprecated — lens alignment replaces forward standoff so the scope ring matches PiP.</summary>
	public const float M700ScopeViewmodelStandoff = 0f;

	/// <summary>Attachment-local offset from sight_holographic origin to holo window center (+Z = higher on mesh).</summary>
	public static readonly Vector3 HolographicRedDotEyeAttachmentOffset = new( 1.05f, 0f, 0.45f );

	/// <summary>Legacy fallback when sight_ranged bounds are unavailable.</summary>
	public static readonly Vector3 RangedSightAdsEyeAttachmentOffset = new( 0.35f, 0f, 1.25f );

	/// <summary>Extra offset on sight_ranged lens anchor (+X forward, +Y left, +Z up on mesh / screen).</summary>
	public static readonly Vector3 RangedSightLensBoundsFineTune = new( 0.15f, 0.45f, 0.75f );

	/// <summary>Viewmodel-local nudge for M4 holo ADS (viewmodel +Z ≈ raises window on screen).</summary>
	public static readonly Vector3 HoloSightAdsViewmodelFineTune = new( 0f, 0f, -0.35f );

	/// <summary>Viewmodel-local nudge for M4 raised RMR ADS (+Z raises on screen).</summary>
	public static readonly Vector3 RaisedRedDotAdsViewmodelFineTune = new( 0f, 0f, -0.85f );

	/// <summary>Viewmodel-local nudge for M4 ranged sight ADS (lens-bone alignment handles placement).</summary>
	public static readonly Vector3 M4RangedSightAdsViewmodelFineTune = new( 0f, 0f, 0f );

	/// <summary>Viewmodel-local nudge for M700 ranged sight ADS (+Z raises on screen, −Z lowers).</summary>
	public static readonly Vector3 M700RangedSightAdsViewmodelFineTune = M700ScopeAdsViewmodelFineTune;

	/// <summary>Legacy alias — holo fine tune.</summary>
	public static readonly Vector3 RedDotAdsViewmodelFineTune = HoloSightAdsViewmodelFineTune;

	/// <summary>Legacy fallback if optic anchor discovery fails entirely.</summary>
	public static readonly Vector3 RedDotCameraOffset = new( 0.25f, 0f, 2f );
	public static readonly Vector3 SniperScopeCameraOffset = new( 0.5f, 0f, 12f );

	public const float CameraBonePositionScale = 1f;
	public const float CameraBoneRotationScale = 1f;

	public const float DefaultLookScale = 0.04f;
	public const float SniperLookMultiplier = 0.35f;
	public const float RedDotLookMultiplier = 0.82f;

	/// <summary>Vertical FOV for a given magnification relative to <see cref="HipFieldOfView"/>.</summary>
	public static float VerticalFovFromMagnification( float magnification, float referenceVerticalFov = HipFieldOfView )
	{
		var referenceHalfRad = referenceVerticalFov * MathF.PI / 360f;
		var adsHalfRad = MathF.Atan( MathF.Tan( referenceHalfRad ) / MathF.Max( 0.01f, magnification ) );
		return adsHalfRad * 360f / MathF.PI;
	}

	/// <summary>Vertical ADS FOV for a zoom multiplier relative to <see cref="IronSightAdsFov"/> (1 = irons, 1.2 = holo, 6 = ranged PiP).</summary>
	public static float VerticalFovFromIronSightMagnification( float ironSightMagnification )
	{
		var ironHalfRad = IronSightAdsFov * MathF.PI / 360f;
		var adsHalfRad = MathF.Atan( MathF.Tan( ironHalfRad ) / MathF.Max( 0.01f, ironSightMagnification ) );
		return adsHalfRad * 360f / MathF.PI;
	}

	public static float ResolveAdsFieldOfView( AimboxAdsSightMode mode, IEnumerable<AimboxAttachmentId> attachments )
	{
		switch ( mode )
		{
			case AimboxAdsSightMode.SniperScope:
				return SniperScopeViewFov;
			case AimboxAdsSightMode.RedDot:
				return attachments?.Contains( AimboxAttachmentId.HoloSight ) == true
					? HoloSightAdsFov
					: RedDotAdsFov;
			case AimboxAdsSightMode.IronSight:
			default:
				return IronSightAdsFov;
		}
	}
}
