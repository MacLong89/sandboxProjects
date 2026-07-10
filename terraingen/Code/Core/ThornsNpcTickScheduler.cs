namespace Terraingen.Core;

using Terraingen.AI;
using Terraingen.Animals;

/// <summary>Round-robin NPC simulation budgets — keeps host tick cost bounded at scale.</summary>
public static class ThornsNpcTickScheduler
{
	public const int MaxAnimalSimulationsPerFrame = 28;
	public const int MaxBanditSimulationsPerFrame = 14;

	public static int RunRoundRobin<T>(
		IReadOnlyList<T> items,
		ref int cursor,
		int budget,
		Func<T, bool> shouldSkip,
		Action<T, float> tick )
	{
		if ( items is null || items.Count == 0 || budget <= 0 || tick is null )
			return 0;

		var count = items.Count;
		cursor = ((cursor % count) + count) % count;

		var processed = 0;
		for ( var step = 0; step < count && processed < budget; step++ )
		{
			var item = items[(cursor + step) % count];
			if ( item is null || shouldSkip( item ) )
				continue;

			tick( item, Time.Delta );
			processed++;
		}

		cursor = (cursor + Math.Max( 1, processed )) % count;
		return processed;
	}

	public static bool ShouldSkipAnimalSimulation( ThornsAnimalBrain brain )
	{
		if ( brain is null || !brain.IsValid() || brain.IsDead )
			return true;

		if ( brain.IsMounted )
			return false;

		if ( brain.LodTier == ThornsNpcLodTier.Sleeping && !brain.HostRequiresActiveSimulation )
			return true;

		return false;
	}

	public static bool ShouldSkipBanditSimulation( ThornsBanditBrain brain )
		=> brain is null || !brain.IsValid() || brain.IsDead;
}
