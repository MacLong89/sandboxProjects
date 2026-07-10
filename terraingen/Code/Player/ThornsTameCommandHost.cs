namespace Terraingen.Player;

using Terraingen.Animals;
using Terraingen.GameData;

public static class ThornsTameCommandHost
{
	static readonly Dictionary<Guid, ThornsTameCommand> Commands = new();

	public static ThornsTameCommand GetCommand( Guid entityId ) =>
		Commands.GetValueOrDefault( entityId, ThornsTameCommand.Follow );

	public static void Apply( Scene scene, string ownerAccountKey, ThornsTameCommandRequest req )
	{
		if ( req is null || scene is null )
			return;

		var brain = ThornsAnimalManager.TryGetByObjectId( req.TameEntityId );
		if ( !brain.IsValid() )
			return;

		if ( !brain.IsTamed || brain.TamedOwnerAccountKey != ownerAccountKey )
			return;

		if ( req.Command == ThornsTameCommand.Summon )
		{
			ThornsTameSummonUtil.HostSummonNearOwner( brain );
			return;
		}

		Commands[req.TameEntityId] = req.Command;
		brain.HostApplyTameCommand( req.Command );
	}

	public static void HostRegisterCommand( Guid entityId, ThornsTameCommand command ) =>
		Commands[entityId] = command;
}
