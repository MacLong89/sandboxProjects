namespace Terraingen.Buildings;

/// <summary>Max HP for player-built structures — tier scaling enables raid progression.</summary>
public static class ThornsStructureHealthRules
{
	public static bool HasHealth( string structureId )
	{
		if ( string.IsNullOrWhiteSpace( structureId ) )
			return false;

		return !string.Equals( structureId, "c4_charge", StringComparison.OrdinalIgnoreCase );
	}

	public static float ResolveMaxHealth( string structureId, int materialTier )
	{
		if ( !HasHealth( structureId ) )
			return 0f;

		var tier = Math.Clamp( materialTier, 0, ThornsPlacedBuildStructure.MaxMaterialTier );
		var tierScale = 1f + tier * 0.65f;
		var baseHp = ResolveBaseHealth( structureId );
		return MathF.Max( 50f, baseHp * tierScale );
	}

	static float ResolveBaseHealth( string structureId ) => structureId switch
	{
		"wood_foundation" => 520f,
		"wood_wall" => 380f,
		"wood_window" => 320f,
		"wood_doorframe" => 340f,
		"wood_ramp" => 300f,
		"storage_chest" => 260f,
		"workbench" => 220f,
		"research" => 240f,
		"campfire" => 160f,
		"bed" => 180f,
		_ when structureId.StartsWith( "wood_", StringComparison.OrdinalIgnoreCase ) => 350f,
		_ => 200f
	};
}
