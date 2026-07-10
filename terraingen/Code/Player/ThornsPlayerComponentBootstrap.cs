namespace Terraingen.Player;

using Terraingen.Combat;
using Terraingen.TerrainGen;

/// <summary>Single place to attach standard player gameplay/combat components.</summary>
public static class ThornsPlayerComponentBootstrap
{
	public static void EnsureStandardGameplay( GameObject player )
	{
		ThornsTerrainExplorer.EnsureStandardGameplayComponents( player );
	}
}
