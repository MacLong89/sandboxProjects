namespace Sandbox;

/// <summary>
/// Tunable species behavior layer — extends <see cref="ThornsWildlifeSpeciesDefinition"/> without breaking spawn/tame/combat.
/// Maps to suggested AnimalStats fields (detection, fear, pack/herd, stalk, courage).
/// </summary>
public readonly record struct ThornsAnimalBehaviorProfile(
	ThornsWildlifeSpeciesKind Species,
	float DetectionRadiusMul,
	float ThreatDetectionMul,
	float StealthMultiplier,
	float Fearfulness,
	float Aggression,
	float Courage,
	float PackPreference,
	float HerdPreference,
	float StalkPreference,
	int MinPackMembersToHunt,
	float StandGroundRadius,
	float ChargeDamage,
	float ChargeRange,
	float ScanIntervalSeconds,
	float StateChangeCooldownSeconds )
{
	public static ThornsAnimalBehaviorProfile Get( ThornsWildlifeSpeciesKind species ) =>
		species switch
		{
			ThornsWildlifeSpeciesKind.Wolf => Wolf,
			ThornsWildlifeSpeciesKind.Panther => Panther,
			ThornsWildlifeSpeciesKind.Elk => Elk,
			ThornsWildlifeSpeciesKind.Moose => Moose,
			ThornsWildlifeSpeciesKind.Deer => Deer,
			ThornsWildlifeSpeciesKind.Rabbit => Rabbit,
			_ => Default( species ),
		};

	static ThornsAnimalBehaviorProfile Default( ThornsWildlifeSpeciesKind species )
	{
		var def = ThornsWildlifeDefinitions.Get( species );
		var predator = def.IsPredator;
		return new ThornsAnimalBehaviorProfile(
			Species: species,
			DetectionRadiusMul: predator ? 1f : 1f,
			ThreatDetectionMul: predator ? 1f : 1.05f,
			StealthMultiplier: predator ? 1f : 1f,
			Fearfulness: predator ? 0.15f : 0.72f,
			Aggression: predator ? 0.78f : 0.08f,
			Courage: predator ? 0.55f : 0.35f,
			PackPreference: species == ThornsWildlifeSpeciesKind.Wolf ? 0.95f : 0f,
			HerdPreference: predator ? 0f : 0.55f,
			StalkPreference: 0f,
			MinPackMembersToHunt: 1,
			StandGroundRadius: 0f,
			ChargeDamage: 0f,
			ChargeRange: 0f,
			ScanIntervalSeconds: 0.38f,
			StateChangeCooldownSeconds: 1.2f );
	}

	public static readonly ThornsAnimalBehaviorProfile Wolf = new(
		Species: ThornsWildlifeSpeciesKind.Wolf,
		DetectionRadiusMul: 1.05f,
		ThreatDetectionMul: 1f,
		StealthMultiplier: 0.92f,
		Fearfulness: 0.18f,
		Aggression: 0.82f,
		Courage: 0.58f,
		PackPreference: 0.98f,
		HerdPreference: 0f,
		StalkPreference: 0.12f,
		MinPackMembersToHunt: 2,
		StandGroundRadius: 0f,
		ChargeDamage: 0f,
		ChargeRange: 0f,
		ScanIntervalSeconds: 0.32f,
		StateChangeCooldownSeconds: 1.4f );

	public static readonly ThornsAnimalBehaviorProfile Panther = new(
		Species: ThornsWildlifeSpeciesKind.Panther,
		DetectionRadiusMul: 1.08f,
		ThreatDetectionMul: 0.92f,
		StealthMultiplier: 0.58f,
		Fearfulness: 0.12f,
		Aggression: 0.88f,
		Courage: 0.72f,
		PackPreference: 0f,
		HerdPreference: 0f,
		StalkPreference: 0.95f,
		MinPackMembersToHunt: 1,
		StandGroundRadius: 0f,
		ChargeDamage: 0f,
		ChargeRange: 0f,
		ScanIntervalSeconds: 0.36f,
		StateChangeCooldownSeconds: 1.6f );

	public static readonly ThornsAnimalBehaviorProfile Elk = new(
		Species: ThornsWildlifeSpeciesKind.Elk,
		DetectionRadiusMul: 1.22f,
		ThreatDetectionMul: 1.28f,
		StealthMultiplier: 1f,
		Fearfulness: 0.92f,
		Aggression: 0.05f,
		Courage: 0.28f,
		PackPreference: 0f,
		HerdPreference: 0.92f,
		StalkPreference: 0f,
		MinPackMembersToHunt: 1,
		StandGroundRadius: 0f,
		ChargeDamage: 0f,
		ChargeRange: 0f,
		ScanIntervalSeconds: 0.28f,
		StateChangeCooldownSeconds: 0.9f );

	public static readonly ThornsAnimalBehaviorProfile Moose = new(
		Species: ThornsWildlifeSpeciesKind.Moose,
		DetectionRadiusMul: 1.08f,
		ThreatDetectionMul: 1.05f,
		StealthMultiplier: 1f,
		Fearfulness: 0.38f,
		Aggression: 0.42f,
		Courage: 0.82f,
		PackPreference: 0.15f,
		HerdPreference: 0.35f,
		StalkPreference: 0f,
		MinPackMembersToHunt: 1,
		StandGroundRadius: 520f,
		ChargeDamage: ThornsWildlifeVsPlayerBalance.MeleeFromReferenceMul( 1.05f ),
		ChargeRange: 118f,
		ScanIntervalSeconds: 0.42f,
		StateChangeCooldownSeconds: 2.2f );

	public static readonly ThornsAnimalBehaviorProfile Deer = Elk with
	{
		Species = ThornsWildlifeSpeciesKind.Deer,
		DetectionRadiusMul = 1.12f,
		HerdPreference = 0.62f,
		Fearfulness = 0.88f,
	};

	public static readonly ThornsAnimalBehaviorProfile Rabbit = Elk with
	{
		Species = ThornsWildlifeSpeciesKind.Rabbit,
		DetectionRadiusMul = 1.18f,
		Fearfulness = 0.96f,
		HerdPreference = 0.45f,
	};
}
