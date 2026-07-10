namespace Terraingen.Player;

using Terraingen.Animals;
using Terraingen.GameData;

public static class ThornsTameRenameHost
{
	public static void Apply( Scene scene, string ownerAccountKey, ThornsTameRenameRequest req )
	{
		if ( req is null || scene is null || string.IsNullOrEmpty( ownerAccountKey ) )
			return;

		foreach ( var brain in scene.GetAllComponents<ThornsAnimalBrain>() )
		{
			if ( !brain.IsValid() || brain.GameObject.Id != req.TameEntityId )
				continue;

			if ( !brain.IsTamed || brain.TamedOwnerAccountKey != ownerAccountKey )
				return;

			brain.HostSetTamedDisplayName( req.DisplayName );
			return;
		}
	}
}
