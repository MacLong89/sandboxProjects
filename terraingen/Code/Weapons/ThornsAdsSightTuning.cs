namespace Sandbox;

/// <summary>ADS presentation — physical optics (viewmodel stays up, anim drives alignment).</summary>
public static class ThornsAdsSightTuning
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

	/// <summary>ADS blend when scope-in finishes — classic overlay/zoom waits for the viewmodel motion.</summary>
	public const float SniperClassicScopeEnterBlend = 0.82f;

	/// <summary>ADS blend when scope-out restores hip presentation (ring drops at halfway un-ADS).</summary>
	public const float SniperClassicScopeExitBlend = 0.5f;

	/// <summary>Extra hold after reaching enter blend before the scope ring appears.</summary>
	public const float SniperClassicScopeOverlayDelaySeconds = 0.06f;

	/// <summary>Below this ADS blend, viewmodel uses overlay pass (hip fire).</summary>
	public const float WorldPassAdsBlend = 0.2f;

	public const float HideCrosshairBlend = 0.45f;

	/// <summary>Extra view-local forward slide while ADS (fallback when camera bone is unavailable).</summary>
	public const float SniperAdsForwardOffset = 2f;
	public const float RedDotAdsForwardOffset = 3.5f;
	public const float IronSightAdsForwardOffset = 2f;

	/// <summary>Fallback scope eye on weapon_root when model attachments are missing (+X forward, +Y left, +Z up).</summary>
	public static readonly Vector3 M700ScopeEyeWeaponRootOffset = new( 5f, 0f, 10.5f );

	/// <summary>World-units forward from scope-eye anchor to the lens plane for PiP screen projection.</summary>
	public const float M700ScopeLensForwardOffset = 10f;

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

	/// <summary>Viewmodel-local nudge for M700 ranged sight ADS (+Z raises on screen, −Z lowers).</summary>
	public static readonly Vector3 M700RangedSightAdsViewmodelFineTune = new( 0f, 0f, -13.75f );

	/// <summary>Legacy fallback if optic anchor discovery fails entirely.</summary>
	public static readonly Vector3 RedDotCameraOffset = new( 0.25f, 0f, 2f );
	public static readonly Vector3 SniperScopeCameraOffset = new( 0.5f, 0f, 12f );

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

	/// <summary>Vertical ADS FOV for a zoom multiplier relative to <see cref="IronSightAdsFov"/> (1 = irons, 1.2 = holo, 4 = ranged).</summary>
	public static float VerticalFovFromIronSightMagnification( float ironSightMagnification )
	{
		var ironHalfRad = IronSightAdsFov * MathF.PI / 360f;
		var adsHalfRad = MathF.Atan( MathF.Tan( ironHalfRad ) / MathF.Max( 0.01f, ironSightMagnification ) );
		return adsHalfRad * 360f / MathF.PI;
	}

	public static float ResolveAdsFieldOfView(
		Terraingen.Combat.Attachments.ThornsAdsSightMode mode,
		IEnumerable<Terraingen.Combat.Attachments.ThornsAttachmentId> attachments )
	{
		switch ( mode )
		{
			case Terraingen.Combat.Attachments.ThornsAdsSightMode.SniperScope:
				return SniperScopeViewFov;
			case Terraingen.Combat.Attachments.ThornsAdsSightMode.RedDot:
				return attachments?.Contains( Terraingen.Combat.Attachments.ThornsAttachmentId.HoloSight ) == true
					? HoloSightAdsFov
					: RedDotAdsFov;
			case Terraingen.Combat.Attachments.ThornsAdsSightMode.IronSight:
			default:
				return IronSightAdsFov;
		}
	}
}
