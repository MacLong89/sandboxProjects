namespace Terraingen.AI;

using Terraingen.Core;

/// <summary>Host bandit spatial index for perception and peer separation.</summary>
public static class ThornsBanditSpatialGrid
{
	static readonly ThornsSpatialGrid<ThornsBanditBrain> Grid = new( 256f );
	static readonly List<ThornsBanditBrain> Scratch = new( 24 );

	public static void Rebuild( IReadOnlyList<ThornsBanditBrain> brains )
	{
		Grid.Clear();
		if ( brains is null )
			return;

		for ( var i = 0; i < brains.Count; i++ )
		{
			var brain = brains[i];
			if ( brain is null || !brain.IsValid() || brain.IsDead )
				continue;

			Grid.Insert( brain, brain.GameObject.WorldPosition );
		}
	}

	public static void QueryPlanar( Vector3 center, float radius, List<ThornsBanditBrain> results )
		=> Grid.QueryRadius( center, radius, results, planar: true );

	public static void QueryPlanarScratch( Vector3 center, float radius )
		=> QueryPlanar( center, radius, Scratch );

	public static IReadOnlyList<ThornsBanditBrain> ScratchResults => Scratch;
}
