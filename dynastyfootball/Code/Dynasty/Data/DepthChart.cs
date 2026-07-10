using System.Linq;
using Dynasty.Core.Identifiers;

namespace Dynasty.Data;

/// <summary>
/// Depth chart slot helpers. Keys are formation slot identifiers (QB, WR1, MLB, etc.).
/// Index 0 in each list is the starter.
/// </summary>
public static class DepthChart
{
	public static PlayerId GetStarter( Dictionary<string, List<PlayerId>> chart, string slotKey )
	{
		if ( chart == null || string.IsNullOrEmpty( slotKey ) )
			return PlayerId.Empty;

		if ( !chart.TryGetValue( slotKey, out var list ) || list == null || list.Count == 0 )
			return PlayerId.Empty;

		return list[0];
	}

	public static IReadOnlyList<PlayerId> GetDepth( Dictionary<string, List<PlayerId>> chart, string slotKey )
	{
		if ( chart == null || string.IsNullOrEmpty( slotKey ) )
			return Array.Empty<PlayerId>();

		return chart.TryGetValue( slotKey, out var list ) && list != null
			? list
			: Array.Empty<PlayerId>();
	}

	public static void SetStarter( Dictionary<string, List<PlayerId>> chart, string slotKey, PlayerId playerId )
	{
		chart ??= new Dictionary<string, List<PlayerId>>();
		RemovePlayerFromAllSlots( chart, playerId );

		if ( playerId.IsEmpty )
		{
			if ( chart.ContainsKey( slotKey ) )
				chart[slotKey] = new List<PlayerId>();
			return;
		}

		if ( !chart.TryGetValue( slotKey, out var list ) || list == null )
		{
			list = new List<PlayerId>();
			chart[slotKey] = list;
		}

		list.RemoveAll( id => id.Value == playerId.Value );
		list.Insert( 0, playerId );
	}

	public static void RemovePlayerFromAllSlots( Dictionary<string, List<PlayerId>> chart, PlayerId playerId )
	{
		if ( chart == null || playerId.IsEmpty )
			return;

		foreach ( var key in chart.Keys.ToList() )
		{
			if ( !chart.TryGetValue( key, out var list ) || list == null )
				continue;

			list.RemoveAll( id => id.Value == playerId.Value );
		}
	}

	public static void EnsureSlotList( Dictionary<string, List<PlayerId>> chart, string slotKey )
	{
		chart ??= new Dictionary<string, List<PlayerId>>();
		if ( !chart.ContainsKey( slotKey ) )
			chart[slotKey] = new List<PlayerId>();
	}
}
