namespace Terraingen.Foliage;

/// <summary>Caps tree instances spawned per populate frame to avoid load hitches.</summary>
public sealed class ThornsFoliageSpawnBudget
{
	public int Remaining { get; set; }

	public ThornsFoliageSpawnBudget( int maxInstances ) =>
		Remaining = Math.Max( 0, maxInstances );

	public bool CanSpawn => Remaining > 0;

	public bool TryConsume( int count = 1 )
	{
		if ( Remaining <= 0 )
			return false;

		Remaining = Math.Max( 0, Remaining - count );
		return true;
	}
}
