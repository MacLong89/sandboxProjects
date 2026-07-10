using System;
using System.Collections.Generic;

namespace Sandbox;

/// <summary>Host-side list of world-generated procedural buildings for city-defender AI (cleared when terrain regens sites).</summary>
public static class ThornsProcBuildingNpcRegistry
{
	public readonly struct Entry
	{
		public readonly GameObject Root;
		public readonly int WidthCells;
		public readonly int DepthCells;
		public readonly int Stories;
		public readonly int MaterialTier;

		public Entry( GameObject root, int widthCells, int depthCells, int stories, int materialTier )
		{
			Root = root;
			WidthCells = widthCells;
			DepthCells = depthCells;
			Stories = stories;
			MaterialTier = materialTier;
		}
	}

	static readonly List<Entry> Buildings = new();

	public static void HostClear()
	{
		Buildings.Clear();
	}

	public static void HostRegister( GameObject root, int widthCells, int depthCells, int stories, int materialTier )
	{
		if ( root is null || !root.IsValid() )
			return;

		Buildings.Add( new Entry( root, widthCells, depthCells, stories, materialTier ) );
	}

	public static int HostBuildingCount => Buildings.Count;

	public static void HostPruneInvalid()
	{
		for ( var i = Buildings.Count - 1; i >= 0; i-- )
		{
			if ( !Buildings[i].Root.IsValid() )
				Buildings.RemoveAt( i );
		}
	}

	public static void HostForEach( Action<Entry> fn )
	{
		for ( var i = 0; i < Buildings.Count; i++ )
			fn( Buildings[i] );
	}
}
