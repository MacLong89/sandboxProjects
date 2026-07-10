#nullable disable

namespace Sandbox;

/// <summary>HUD slot backdrops when no <see cref="ThornsItemRegistry.ThornsItemDefinition.HudIconTexture"/> is set.</summary>
public static class ThornsUiItemSlotVisuals
{
	public static Color EmptySlotBackdrop { get; } = new( 0.06f, 0.07f, 0.09f, 0.95f );

	public static Color FallbackBackdrop( ThornsItemType? type )
	{
		return type switch
		{
			ThornsItemType.Weapon => new Color( 0.12f, 0.16f, 0.22f, 0.92f ),
			ThornsItemType.Tool => new Color( 0.14f, 0.18f, 0.14f, 0.92f ),
			ThornsItemType.Ammo => new Color( 0.22f, 0.18f, 0.08f, 0.92f ),
			ThornsItemType.Consumable => new Color( 0.1f, 0.2f, 0.14f, 0.92f ),
			ThornsItemType.Armor => new Color( 0.16f, 0.12f, 0.22f, 0.92f ),
			ThornsItemType.Resource => new Color( 0.14f, 0.12f, 0.1f, 0.92f ),
			_ => new Color( 0.11f, 0.12f, 0.14f, 0.92f )
		};
	}
}
