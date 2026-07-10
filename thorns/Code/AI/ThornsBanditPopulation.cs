using System.Collections.Generic;

namespace Sandbox;

/// <summary>
/// INTERNAL implementation — host bandit brain registry.
/// Public access: <see cref="ThornsPopulationDirector"/> only. Do not reference from gameplay code.
/// </summary>
public static class ThornsBanditPopulation
{
	static readonly List<ThornsBanditBrain> Brains = new();

	public static void HostRegister( ThornsBanditBrain brain )
	{
		if ( !Networking.IsHost || brain is null || !brain.IsValid() )
			return;

		if ( !Brains.Contains( brain ) )
			Brains.Add( brain );
	}

	public static void HostUnregister( ThornsBanditBrain brain )
	{
		if ( brain is null )
			return;

		Brains.Remove( brain );
	}

	public static IReadOnlyList<ThornsBanditBrain> HostBrainsReadOnly => Brains;
}
