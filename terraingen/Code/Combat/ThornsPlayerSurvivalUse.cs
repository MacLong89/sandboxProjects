namespace Terraingen.Combat;

using Sandbox.Network;
using Terraingen.Player;
using Terraingen.TerrainGen;

/// <summary>
/// AUDIT NOTE: stub retained for prefab/component attach expectations.
/// Survival consume moved to <see cref="ThornsPlayerHotbarConsumeUse"/> (RMB hold).
/// World Use is handled by per-target components (container, door, stations, …).
/// Safe to remove once EnsurePlayerHealth / prefabs no longer Create this component.
/// </summary>
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
		// Intentionally empty — do not re-add consume/Use logic here without checking hotbar RMB + container Use.
		_ = IsLocallyControlled();
		_ = _taming;
	}

	bool IsLocallyControlled()
	{
		if ( !Networking.IsActive )
			return true;

		return Network.Owner == Connection.Local;
	}
}
