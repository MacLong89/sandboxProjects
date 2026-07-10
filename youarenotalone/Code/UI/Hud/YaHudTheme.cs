namespace Sandbox;

/// <summary>Global HUD palette — tactical teal (#48CBCC), dark panels, white copy.</summary>
public static class YaHudTheme
{
	// Core brand
	public static readonly Color Teal = new( 72f / 255f, 203f / 255f, 204f / 255f, 1f );
	public static readonly Color TealDim = new( 72f / 255f, 203f / 255f, 204f / 255f, 0.45f );
	public static readonly Color TealMuted = new( 72f / 255f, 203f / 255f, 204f / 255f, 0.22f );
	public static readonly Color TealStrong = new( 72f / 255f, 203f / 255f, 204f / 255f, 0.85f );

	// Surfaces
	public static readonly Color BgOverlay = new( 8f / 255f, 10f / 255f, 12f / 255f, 0.94f );
	public static readonly Color Panel = new( 14f / 255f, 17f / 255f, 20f / 255f, 0.96f );
	public static readonly Color PanelDeep = new( 6f / 255f, 10f / 255f, 12f / 255f, 0.98f );
	public static readonly Color TrackBg = new( 5f / 255f, 8f / 255f, 10f / 255f, 1f );

	// Typography
	public static readonly Color TextPrimary = new( 245f / 255f, 248f / 255f, 250f / 255f, 0.98f );
	public static readonly Color TextSecondary = new( 180f / 255f, 190f / 255f, 198f / 255f, 0.88f );
	public static readonly Color TextMuted = new( 120f / 255f, 130f / 255f, 138f / 255f, 0.92f );
	public static readonly Color TextOnTeal = new( 236f / 255f, 240f / 255f, 242f / 255f, 0.95f );

	// Chrome
	public static readonly Color Border = TealMuted;
	public static readonly Color BorderStrong = TealStrong;

	// Hotbar slots
	public static readonly Color SlotEmpty = PanelDeep;
	public static readonly Color SlotHover = new( 18f / 255f, 26f / 255f, 28f / 255f, 0.98f );
	public static readonly Color SlotSelected = new( 22f / 255f, 52f / 255f, 54f / 255f, 0.98f );

	// Ability / meter fills (stay readable on dark tracks)
	public static readonly Color MeterStamina = new( 56f / 255f, 178f / 255f, 186f / 255f, 1f );
	public static readonly Color MeterDash = Teal;
	public static readonly Color MeterMimic = new( 100f / 255f, 220f / 255f, 210f / 255f, 1f );
	public static readonly Color MeterParanoia = new( 140f / 255f, 90f / 255f, 200f / 255f, 0.95f );
	public static readonly Color MeterCharge = new( 72f / 255f, 203f / 255f, 204f / 255f, 0.75f );

	// Gameplay semantics (minimal use)
	public static readonly Color Danger = new( 0.95f, 0.35f, 0.3f, 1f );
	public static readonly Color Success = new( 0.45f, 0.85f, 0.5f, 1f );
	public static readonly Color Health = new( 0.45f, 0.72f, 0.32f, 1f );
	public static readonly Color HealthLow = new( 0.75f, 0.28f, 0.22f, 1f );

	/// <summary>Alone role accent — use <see cref="YaHudRoleTheme.Alone"/> for HUD chrome.</summary>
	public static readonly Color AloneAccent = YaHudRoleTheme.Alone.Accent;

	/// <summary>Third-person Alone mimic when no hunter connection is mirrored (generic hunter silhouette).</summary>
	public static readonly Color MimicGenericHunterTint = new( 0.92f, 0.94f, 0.96f, 1f );
}
