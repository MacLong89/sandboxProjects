namespace Terraingen.AI;

using Terraingen.Multiplayer;

/// <summary>Host bandit brain registry and spawn budget helpers.</summary>
public static class ThornsBanditPopulation
{
	[SkipHotload]
	static readonly List<ThornsBanditBrain> Brains = new( 64 );

	public static IReadOnlyList<ThornsBanditBrain> HostBrainsReadOnly => Brains;

	public static int CountLiveBandits()
	{
		PruneInvalid();
		var count = 0;
		for ( var i = 0; i < Brains.Count; i++ )
		{
			var brain = Brains[i];
			if ( brain.IsValid() && !brain.IsDead )
				count++;
		}

		return count;
	}

	static void PruneInvalid()
	{
		for ( var i = Brains.Count - 1; i >= 0; i-- )
		{
			if ( !Brains[i].IsValid() )
				Brains.RemoveAt( i );
		}
	}

	public static void HostRegister( ThornsBanditBrain brain )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || brain is null || !brain.IsValid() )
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
}
