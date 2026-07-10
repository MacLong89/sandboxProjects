namespace Terraingen.Player;

using Terraingen.Animals;
using Terraingen.GameData;

public static class ThornsTameStatUpgradeHost
{
	public static bool Apply(
		Scene scene,
		string ownerAccountKey,
		ThornsTameStatUpgradeRequest req,
		out string message )
	{
		message = null;
		if ( req is null || scene is null || string.IsNullOrEmpty( ownerAccountKey ) )
			return false;

		if ( !ThornsTameProgression.TryParseStat( req.StatKey, out var stat ) )
		{
			message = "Unknown attribute.";
			return false;
		}

		foreach ( var brain in scene.GetAllComponents<ThornsAnimalBrain>() )
		{
			if ( !brain.IsValid() || brain.GameObject.Id != req.TameEntityId )
				continue;

			if ( !brain.IsTamed || brain.TamedOwnerAccountKey != ownerAccountKey )
			{
				message = "You do not own this tame.";
				return false;
			}

			if ( brain.IsDead )
			{
				message = "This tame cannot be upgraded right now.";
				return false;
			}

			if ( brain.UnspentStatPoints <= 0 )
			{
				message = "No available XP to spend.";
				return false;
			}

			if ( !brain.HostTryUpgradeStat( stat ) )
			{
				message = "Could not upgrade this attribute.";
				return false;
			}

			message = $"Upgraded {ThornsTameProgression.StatLabel( stat )}.";
			return true;
		}

		message = "Tame not found.";
		return false;
	}
}
