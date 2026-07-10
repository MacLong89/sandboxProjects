namespace Sandbox;

/// <summary>Role-tinted HUD chrome — Alone: red / black / white, Not Alone: teal / black / white.</summary>
public static class YaHudRoleTheme
{
	public readonly struct Palette
	{
		public Color Accent { get; init; }
		public Color AccentDim { get; init; }
		public Color AccentMuted { get; init; }
		public Color AccentStrong { get; init; }
		public Color Border { get; init; }
		public Color BorderStrong { get; init; }
		public Color SlotSelected { get; init; }
		public Color SlotReadyBg { get; init; }
		public Color MeterPrimary { get; init; }
		public Color MeterSecondary { get; init; }
	}

	public static readonly Palette Alone = new()
	{
		Accent = new Color( 235f / 255f, 56f / 255f, 56f / 255f, 1f ),
		AccentDim = new Color( 235f / 255f, 56f / 255f, 56f / 255f, 0.5f ),
		AccentMuted = new Color( 235f / 255f, 56f / 255f, 56f / 255f, 0.24f ),
		AccentStrong = new Color( 235f / 255f, 56f / 255f, 56f / 255f, 0.9f ),
		Border = new Color( 235f / 255f, 56f / 255f, 56f / 255f, 0.22f ),
		BorderStrong = new Color( 235f / 255f, 56f / 255f, 56f / 255f, 0.85f ),
		SlotSelected = new Color( 58f / 255f, 14f / 255f, 14f / 255f, 0.98f ),
		SlotReadyBg = new Color( 34f / 255f, 10f / 255f, 10f / 255f, 0.98f ),
		MeterPrimary = new Color( 235f / 255f, 56f / 255f, 56f / 255f, 1f ),
		MeterSecondary = new Color( 180f / 255f, 48f / 255f, 48f / 255f, 0.85f )
	};

	public static readonly Palette Hunter = new()
	{
		Accent = YaHudTheme.Teal,
		AccentDim = YaHudTheme.TealDim,
		AccentMuted = YaHudTheme.TealMuted,
		AccentStrong = YaHudTheme.TealStrong,
		Border = YaHudTheme.TealMuted,
		BorderStrong = YaHudTheme.TealStrong,
		SlotSelected = YaHudTheme.SlotSelected,
		SlotReadyBg = new Color( 18f / 255f, 26f / 255f, 28f / 255f, 0.98f ),
		MeterPrimary = YaHudTheme.MeterStamina,
		MeterSecondary = YaHudTheme.Teal
	};

	public static Palette For( YaPlayerRole role ) =>
		role == YaPlayerRole.Alone ? Alone : Hunter;

	/// <summary>Kill feed / roster name tint by the player's team.</summary>
	public static Color TeamNameColor( YaPlayerRole team ) => team switch
	{
		YaPlayerRole.Alone => Alone.Accent,
		YaPlayerRole.NotAlone => Hunter.Accent,
		_ => YaHudTheme.TextSecondary
	};
}
