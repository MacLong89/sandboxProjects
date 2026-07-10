namespace Sandbox;

/// <summary>Passive bonus granted only when a tame rolls <see cref="ThornsLootRarity.Legendary"/> quality.</summary>
public enum ThornsTameLegendaryAbility : byte
{
	None = 0,

	/// <summary>+12% effective max health.</summary>
	PrimordialVitality = 1,

	/// <summary>+14% melee damage.</summary>
	ApexHunter = 2,

	/// <summary>+14% chase / movement speed.</summary>
	GaleRunner = 3,

	/// <summary>+8% health, damage, and speed.</summary>
	PackTyrant = 4,

	/// <summary>+10% health — resilient frame.</summary>
	Ironhide = 5
}

/// <summary>Stat multipliers from <see cref="ThornsTameLegendaryAbility"/> (stack with bloodline affinity rolls).</summary>
public static class ThornsTameLegendaryAbilityDefs
{
	public static float HealthMul( ThornsTameLegendaryAbility a ) =>
		a switch
		{
			ThornsTameLegendaryAbility.PrimordialVitality => 1.12f,
			ThornsTameLegendaryAbility.PackTyrant => 1.08f,
			ThornsTameLegendaryAbility.Ironhide => 1.10f,
			_ => 1f
		};

	public static float DamageMul( ThornsTameLegendaryAbility a ) =>
		a switch
		{
			ThornsTameLegendaryAbility.ApexHunter => 1.14f,
			ThornsTameLegendaryAbility.PackTyrant => 1.08f,
			_ => 1f
		};

	public static float SpeedMul( ThornsTameLegendaryAbility a ) =>
		a switch
		{
			ThornsTameLegendaryAbility.GaleRunner => 1.14f,
			ThornsTameLegendaryAbility.PackTyrant => 1.08f,
			_ => 1f
		};

	public static string DisplayName( ThornsTameLegendaryAbility a ) =>
		a switch
		{
			ThornsTameLegendaryAbility.PrimordialVitality => "Primordial Vitality",
			ThornsTameLegendaryAbility.ApexHunter => "Apex Hunter",
			ThornsTameLegendaryAbility.GaleRunner => "Gale Runner",
			ThornsTameLegendaryAbility.PackTyrant => "Pack Tyrant",
			ThornsTameLegendaryAbility.Ironhide => "Ironhide",
			_ => ""
		};

	public static string Description( ThornsTameLegendaryAbility a ) =>
		a switch
		{
			ThornsTameLegendaryAbility.PrimordialVitality => "+12% max health.",
			ThornsTameLegendaryAbility.ApexHunter => "+14% melee damage.",
			ThornsTameLegendaryAbility.GaleRunner => "+14% movement and chase speed.",
			ThornsTameLegendaryAbility.PackTyrant => "+8% health, damage, and speed.",
			ThornsTameLegendaryAbility.Ironhide => "+10% max health.",
			_ => ""
		};
}
