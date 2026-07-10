namespace Terraingen.Player;

using Terraingen.Animals;
using Terraingen.GameData;

public static class ThornsTameFeedHost
{
	public static bool Apply(
		Scene scene,
		string ownerAccountKey,
		ThornsPlayerGameplay gameplay,
		ThornsTameFeedRequest req,
		out string message )
	{
		message = null;
		if ( req is null || scene is null || string.IsNullOrEmpty( ownerAccountKey ) || gameplay is null )
			return false;

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
				message = "This tame cannot be fed right now.";
				return false;
			}

			if ( brain.CurrentHealth >= brain.MaxHealth - 0.01f )
			{
				message = $"{brain.TamedDisplayName} is already at full health.";
				return false;
			}

			if ( !gameplay.HostTryConsumeOneTameFood( out var itemId ) )
			{
				message = "No food in your inventory or hotbar.";
				return false;
			}

			var heal = TameFeedHealAmount( itemId );
			if ( !brain.HostFeed( heal ) )
			{
				message = "Could not feed this tame.";
				return false;
			}

			brain.HostGrantTameExperience( ThornsTameProgression.XpPerFeed );

			message = $"Fed {brain.TamedDisplayName} (+{heal:0} HP).";
			return true;
		}

		message = "Tame not found.";
		return false;
	}

	static float TameFeedHealAmount( string itemId ) => itemId.ToLowerInvariant() switch
	{
		"raw_meat" => 50f,
		"canned_stew" => 45f,
		"field_rations" => 40f,
		"apple" => 30f,
		"food" => 25f,
		_ => 25f
	};
}
