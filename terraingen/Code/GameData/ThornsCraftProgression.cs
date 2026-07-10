namespace Terraingen.GameData;

/// <summary>Crafting tier from the Technician upgrade skill (+1 tier per rank).</summary>
public static class ThornsCraftProgression
{
	public const string TechnicianSkillId = "technician";

	public static int ResolveCraftTier( ThornsSkillsSnapshotDto skills )
	{
		var rank = 0;
		if ( skills?.Ranks is not null )
		{
			foreach ( var entry in skills.Ranks )
			{
				if ( entry is null || !string.Equals( entry.SkillId, TechnicianSkillId, StringComparison.OrdinalIgnoreCase ) )
					continue;

				rank = Math.Max( 0, entry.Rank );
				break;
			}
		}

		return 1 + rank;
	}

	public static bool MeetsCraftTier( int playerCraftTier, ThornsRecipeDefinition recipe )
		=> recipe is not null && Math.Max( 1, playerCraftTier ) >= Math.Max( 1, recipe.RequiredCraftTier );

	public static string FormatRequiredTier( int tier ) => $"Craft Tier {Math.Max( 1, tier )}";
}
