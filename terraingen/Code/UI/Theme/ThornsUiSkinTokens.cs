namespace Terraingen.UI;

/// <summary>Per-skin color tokens consumed by <see cref="ThornsTheme"/> and <see cref="ThornsHudTheme"/>.</summary>
public readonly struct ThornsUiSkinPalette
{
	public Color PanelBg { get; init; }
	public Color OpaquePanelBg { get; init; }
	public Color Border { get; init; }
	public Color TextPrimary { get; init; }
	public Color TextSecondary { get; init; }
	public Color Accent { get; init; }
	public Color Health { get; init; }
	public Color Stamina { get; init; }
	public Color Hunger { get; init; }
	public Color Danger { get; init; }

	public Color GlassBg { get; init; }
	public Color GlassBorder { get; init; }
	public Color Gold { get; init; }
	public Color TextWarm { get; init; }
	public Color TextMuted { get; init; }
	public Color HealthFill { get; init; }
	public Color ThirstFill { get; init; }
	public Color HungerFill { get; init; }
	public Color StaminaFill { get; init; }
	public Color XpFill { get; init; }

	public Color ObjectivesBg { get; init; }
	public Color ObjectivesBorder { get; init; }
	public Color HudBarTrackBg { get; init; }
	public Color HudBarTrackBorder { get; init; }
	public Color HotbarSlotBg { get; init; }
	public Color HotbarSlotBorder { get; init; }
	public Color HotbarFrameBg { get; init; }
	public Color HotbarFrameBorder { get; init; }
	public Color MenuOverlayBg { get; init; }
}

public static class ThornsUiSkinTokens
{
	public static ThornsUiSkinPalette Active => ThornsUiSkin.Active switch
	{
		ThornsUiSkinKind.Survive => Survive,
		ThornsUiSkinKind.Field => Field,
		_ => Classic
	};

	/// <summary>Concept art — parchment menus + minimal HUD (dual visual language).</summary>
	public static ThornsUiSkinPalette Classic => new()
	{
		PanelBg = new Color( 227f / 255f, 217f / 255f, 198f / 255f, 0.98f ),
		OpaquePanelBg = new Color( 227f / 255f, 217f / 255f, 198f / 255f, 1f ),
		Border = new Color( 43f / 255f, 35f / 255f, 29f / 255f, 0.85f ),
		TextPrimary = new Color( 40f / 255f, 28f / 255f, 16f / 255f ),
		TextSecondary = new Color( 70f / 255f, 53f / 255f, 36f / 255f ),
		Accent = new Color( 212f / 255f, 175f / 255f, 90f / 255f ),
		Health = new Color( 204f / 255f, 68f / 255f, 68f / 255f ),
		Stamina = new Color( 85f / 255f, 170f / 255f, 68f / 255f ),
		Hunger = new Color( 221f / 255f, 136f / 255f, 51f / 255f ),
		Danger = new Color( 204f / 255f, 68f / 255f, 68f / 255f ),
		GlassBg = new Color( 0f, 0f, 0f, 0.55f ),
		GlassBorder = new Color( 1f, 1f, 1f, 0.12f ),
		Gold = new Color( 212f / 255f, 175f / 255f, 90f / 255f ),
		TextWarm = new Color( 240f / 255f, 237f / 255f, 229f / 255f ),
		TextMuted = new Color( 180f / 255f, 170f / 255f, 155f / 255f ),
		HealthFill = new Color( 204f / 255f, 68f / 255f, 68f / 255f ),
		StaminaFill = new Color( 85f / 255f, 170f / 255f, 68f / 255f ),
		ThirstFill = new Color( 85f / 255f, 153f / 255f, 221f / 255f ),
		HungerFill = new Color( 221f / 255f, 136f / 255f, 51f / 255f ),
		XpFill = new Color( 74f / 255f, 138f / 255f, 58f / 255f ),
		ObjectivesBg = new Color( 0f, 0f, 0f, 0f ),
		ObjectivesBorder = new Color( 1f, 1f, 1f, 0f ),
		HudBarTrackBg = new Color( 22f / 255f, 19f / 255f, 17f / 255f, 1f ),
		HudBarTrackBorder = new Color( 58f / 255f, 50f / 255f, 44f / 255f, 1f ),
		HotbarSlotBg = new Color( 43f / 255f, 38f / 255f, 34f / 255f, 1f ),
		HotbarSlotBorder = new Color( 58f / 255f, 50f / 255f, 44f / 255f, 1f ),
		HotbarFrameBg = new Color( 0f, 0f, 0f, 0f ),
		HotbarFrameBorder = new Color( 0f, 0f, 0f, 0f ),
		MenuOverlayBg = new Color( 12f / 255f, 9f / 255f, 6f / 255f, 1f )
	};

	/// <summary>Modern atmospheric survival — desaturated earth tones, functional vitals accents.</summary>
	public static ThornsUiSkinPalette Survive => new()
	{
		PanelBg = new Color( 0.055f, 0.063f, 0.071f, 0.94f ),
		OpaquePanelBg = new Color( 0.07f, 0.08f, 0.09f, 0.98f ),
		Border = new Color( 1f, 1f, 1f, 0.06f ),
		TextPrimary = new Color( 0.91f, 0.90f, 0.88f ),
		TextSecondary = new Color( 0.54f, 0.56f, 0.58f ),
		Accent = new Color( 0.62f, 0.72f, 0.38f ),
		Health = new Color( 0.77f, 0.27f, 0.27f ),
		Stamina = new Color( 0.49f, 0.72f, 0.29f ),
		Hunger = new Color( 0.83f, 0.53f, 0.29f ),
		Danger = new Color( 0.88f, 0.33f, 0.33f ),
		GlassBg = new Color( 0.04f, 0.05f, 0.06f, 0.72f ),
		GlassBorder = new Color( 1f, 1f, 1f, 0.05f ),
		Gold = new Color( 0.66f, 0.72f, 0.42f ),
		TextWarm = new Color( 0.94f, 0.93f, 0.90f ),
		TextMuted = new Color( 0.52f, 0.54f, 0.56f ),
		HealthFill = new Color( 0.77f, 0.27f, 0.27f ),
		StaminaFill = new Color( 0.49f, 0.72f, 0.29f ),
		ThirstFill = new Color( 0.29f, 0.62f, 0.83f ),
		HungerFill = new Color( 0.83f, 0.53f, 0.29f ),
		XpFill = new Color( 0.62f, 0.72f, 0.38f ),
		ObjectivesBg = new Color( 0.05f, 0.06f, 0.07f, 0.94f ),
		ObjectivesBorder = new Color( 0.62f, 0.72f, 0.38f, 0.28f ),
		HudBarTrackBg = new Color( 0.02f, 0.02f, 0.03f, 0.65f ),
		HudBarTrackBorder = new Color( 1f, 1f, 1f, 0.06f ),
		HotbarSlotBg = new Color( 0.03f, 0.035f, 0.04f, 0.82f ),
		HotbarSlotBorder = new Color( 1f, 1f, 1f, 0.07f ),
		HotbarFrameBg = new Color( 0.04f, 0.045f, 0.05f, 0.88f ),
		HotbarFrameBorder = new Color( 1f, 1f, 1f, 0.05f ),
		MenuOverlayBg = new Color( 0.02f, 0.025f, 0.03f, 0.88f )
	};

	/// <summary>Expedition field kit — kraft paper, stamped forms, instrument readouts.</summary>
	public static ThornsUiSkinPalette Field => new()
	{
		PanelBg = new Color( 0.16f, 0.14f, 0.11f, 0.94f ),
		OpaquePanelBg = new Color( 0.18f, 0.16f, 0.13f, 0.98f ),
		Border = new Color( 0.55f, 0.48f, 0.38f, 0.55f ),
		TextPrimary = new Color( 0.87f, 0.84f, 0.77f ),
		TextSecondary = new Color( 0.54f, 0.50f, 0.44f ),
		Accent = new Color( 0.77f, 0.65f, 0.42f ),
		Health = new Color( 0.55f, 0.23f, 0.23f ),
		Stamina = new Color( 0.42f, 0.52f, 0.38f ),
		Hunger = new Color( 0.63f, 0.42f, 0.24f ),
		Danger = new Color( 0.72f, 0.28f, 0.24f ),
		GlassBg = new Color( 0.11f, 0.10f, 0.08f, 0.82f ),
		GlassBorder = new Color( 0.55f, 0.48f, 0.38f, 0.45f ),
		Gold = new Color( 0.77f, 0.65f, 0.42f ),
		TextWarm = new Color( 0.92f, 0.89f, 0.82f ),
		TextMuted = new Color( 0.52f, 0.48f, 0.42f ),
		HealthFill = new Color( 0.55f, 0.23f, 0.23f ),
		StaminaFill = new Color( 0.42f, 0.52f, 0.38f ),
		ThirstFill = new Color( 0.29f, 0.42f, 0.48f ),
		HungerFill = new Color( 0.63f, 0.42f, 0.24f ),
		XpFill = new Color( 0.58f, 0.52f, 0.36f ),
		ObjectivesBg = new Color( 0.13f, 0.11f, 0.09f, 0.94f ),
		ObjectivesBorder = new Color( 0.77f, 0.65f, 0.42f, 0.45f ),
		HudBarTrackBg = new Color( 0.08f, 0.07f, 0.06f, 0.75f ),
		HudBarTrackBorder = new Color( 0.45f, 0.40f, 0.32f, 0.55f ),
		HotbarSlotBg = new Color( 0.10f, 0.09f, 0.07f, 0.88f ),
		HotbarSlotBorder = new Color( 0.50f, 0.44f, 0.34f, 0.65f ),
		HotbarFrameBg = new Color( 0.09f, 0.08f, 0.06f, 0.92f ),
		HotbarFrameBorder = new Color( 0.55f, 0.48f, 0.38f, 0.55f ),
		MenuOverlayBg = new Color( 0.08f, 0.07f, 0.06f, 0.94f )
	};
}
