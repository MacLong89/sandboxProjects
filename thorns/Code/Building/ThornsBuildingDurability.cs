namespace Sandbox;

/// <summary>
/// Player-built structure HP and C4 breaching — one direct center detonation = one "C4 hit" worth of damage.
/// Wood 2, stone 4, metal 8 charges to destroy.
/// </summary>
public static class ThornsBuildingDurability
{
	public const float DamagePerDirectC4 = 100f;

	public static int C4HitsToDestroy( ThornsBuildingMaterialTier tier ) => tier switch
	{
		ThornsBuildingMaterialTier.Wood => 2,
		ThornsBuildingMaterialTier.Stone => 4,
		ThornsBuildingMaterialTier.Metal => 8,
		_ => 2
	};

	public static float MaxHealthForMaterialTier( int materialTier )
	{
		var tier = (ThornsBuildingMaterialTier)Math.Clamp( materialTier, 0, 2 );
		return DamagePerDirectC4 * C4HitsToDestroy( tier );
	}

	public static float MaxHealthForStructure( string structureDefId, int materialTier )
	{
		if ( string.Equals( structureDefId, "base_core", StringComparison.OrdinalIgnoreCase ) )
			return MaxHealthForMaterialTier( (int)ThornsBuildingMaterialTier.Metal );

		if ( ThornsBuildingDefinitions.IsPortableKitPlaceableId( structureDefId ) )
			return MaxHealthForMaterialTier( (int)ThornsBuildingMaterialTier.Wood );

		return MaxHealthForMaterialTier( materialTier );
	}

	/// <summary>Host-only: apply explosive (or other) damage; removes the piece when HP reaches zero.</summary>
	public static bool HostApplyDamage( ThornsPlacedStructure ps, float amount )
	{
		if ( !Networking.IsHost || ps is null || !ps.IsValid() || !ps.GameObject.IsValid() )
			return false;

		if ( amount <= 0.01f )
			return false;

		ps.CurrentHealth = MathF.Max( 0f, ps.CurrentHealth - amount );
		if ( ps.CurrentHealth > 0.01f )
			return false;

		HostRemoveStructure( ps, "damage" );
		return true;
	}

	public static void HostRemoveStructure( ThornsPlacedStructure ps, string reason )
	{
		if ( ps is null || !ps.IsValid() || !ps.GameObject.IsValid() )
			return;

		var id = ps.StructureDefId;
		if ( string.Equals( id, "base_core", StringComparison.OrdinalIgnoreCase ) )
			ThornsBuildingAuthority.HostUnregisterPlacedBaseCore( ps.OwnerConnectionId );

		Log.Info( $"[Thorns] Structure removed ({reason}) instance={ps.InstanceId} def={id} tier={ps.MaterialTier}" );
		ps.GameObject.Destroy();
		ThornsWorldPersistence.HostNotifyStructureDestroyedByDemolish();
		ThornsWorldPersistence.HostNotifyWorldStructuresDirty();
	}

	public static void HostApplyMaxHealthFromDurability( ThornsPlacedStructure ps, bool refillToFull = true )
	{
		if ( ps is null || !ps.IsValid() )
			return;

		var max = MaxHealthForStructure( ps.StructureDefId, ps.MaterialTier );
		ps.MaxHealthSync = max;
		if ( refillToFull )
			ps.CurrentHealth = max;
		else
			ps.CurrentHealth = Math.Clamp( ps.CurrentHealth, 0f, max );
	}
}
