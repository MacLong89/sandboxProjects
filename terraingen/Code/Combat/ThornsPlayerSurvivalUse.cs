namespace Terraingen.Combat;

using Sandbox.Network;
using Terraingen.Player;
using Terraingen.TerrainGen;

/// <summary>Use (E) for world interactions only — survival consume is hotbar RMB hold.</summary>
[Title( "Thorns Player Survival Use" )]
[Category( "Player" )]
public sealed class ThornsPlayerSurvivalUse : Component
{
	ThornsPlayerAnimalTaming _taming;

	protected override void OnAwake()
	{
		_taming = Components.Get<ThornsPlayerAnimalTaming>();
	}

	protected override void OnUpdate()
	{
		_ = IsLocallyControlled();
	}

	bool IsLocallyControlled()
	{
		if ( !Networking.IsActive )
			return true;

		return Network.Owner == Connection.Local;
	}
}
