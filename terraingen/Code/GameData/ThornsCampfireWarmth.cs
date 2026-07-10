namespace Terraingen.GameData;

using Terraingen.Buildings;
using Terraingen.Player;

// Campfire proximity warmth — passive health recovery near placed campfires.
public static class ThornsCampfireWarmth
{
	public const float WarmthRadiusInches = 480f;
	public const float HealthRegenPerSecond = 2.75f;

	public static bool IsPlayerNearCampfire( GameObject playerRoot )
	{
		if ( !playerRoot.IsValid() )
			return false;

		var pos = playerRoot.WorldPosition;
		var radiusSq = WarmthRadiusInches * WarmthRadiusInches;

		foreach ( var placed in ThornsPlacedBuildStructure.Registry )
		{
			if ( !placed.IsValid() || string.IsNullOrWhiteSpace( placed.InstanceKey ) )
				continue;

			if ( !string.Equals( placed.StructureId, "campfire", StringComparison.OrdinalIgnoreCase ) )
				continue;

			if ( ( placed.GameObject.WorldPosition - pos ).LengthSquared <= radiusSq )
				return true;
		}

		return false;
	}

	public static float ResolveHealthRegenPerSecond( bool nearCampfire, bool wellFed, ThornsSkillsSnapshotDto skills )
	{
		if ( !nearCampfire && !wellFed )
			return 0f;

		var rate = 0f;
		if ( nearCampfire )
			rate += HealthRegenPerSecond;

		if ( wellFed )
			rate += ThornsPlayerSurvivalStats.HealthRegenPerSecond( skills );

		return rate;
	}
}
