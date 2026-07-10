namespace Terraingen.Animals;

/// <summary>Mount rules for tamed deer and moose.</summary>
public static class ThornsAnimalMounting
{
	public const float MountHoldSeconds = 0.75f;
	public const float MountMaxRange = 240f;
	public const float MountAimDotThreshold = 0.35f;
	public const float MountJumpSpeed = 420f;
	public const float MountGravity = 1400f;
	public const float MountGroundStickInches = 10f;
	public const float MountTurnDegreesPerSecond = 95f;
	public const float MountedPlayerSprintSpeedMultiplier = 2f;

	public static bool IsMountableSpecies( ThornsAnimalBrain brain )
	{
		if ( brain is null || !brain.IsValid() || !brain.IsTamed || brain.IsDead )
			return false;

		if ( !ThornsAnimalSpeciesRegistry.TryGet( brain.SpeciesId, out var species ) )
			return false;

		return string.Equals( species.Key, "deer", StringComparison.OrdinalIgnoreCase )
		       || string.Equals( species.Key, "moose", StringComparison.OrdinalIgnoreCase );
	}

	public static Vector3 SeatLocalOffset( ushort speciesId )
	{
		if ( speciesId == ThornsAnimalSpeciesCatalog.MooseId )
			return new Vector3( 0f, -12f, 150f );

		return new Vector3( 0f, -18f, 118f );
	}

	public static bool IsOwnedByAccount( ThornsAnimalBrain brain, string ownerAccountKey )
	{
		if ( brain is null || string.IsNullOrWhiteSpace( ownerAccountKey ) )
			return false;

		return string.Equals( brain.TamedOwnerAccountKey, ownerAccountKey, StringComparison.OrdinalIgnoreCase );
	}
}
