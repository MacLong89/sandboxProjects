namespace Terraingen.UI.Core;

using Terraingen.GameData;
using Terraingen.UI;

/// <summary>Resolves readable place names for world-event alert copy.</summary>
public static class ThornsWorldEventLocation
{
	static readonly HashSet<ThornsMapMarkerKind> NamedPlaceKinds = new()
	{
		ThornsMapMarkerKind.Metropolis,
		ThornsMapMarkerKind.City,
		ThornsMapMarkerKind.Town,
		ThornsMapMarkerKind.Settlement,
		ThornsMapMarkerKind.Suburb,
		ThornsMapMarkerKind.RuralPoi,
		ThornsMapMarkerKind.MilitaryPoi,
		ThornsMapMarkerKind.CabinSite,
		ThornsMapMarkerKind.Farmstead,
		ThornsMapMarkerKind.NpcGuildOutpost,
		ThornsMapMarkerKind.SpecialEvent,
		ThornsMapMarkerKind.Boss
	};

	public static string ResolveNear( float worldX, float worldY )
	{
		var markers = ThornsUiClientState.Snapshot?.Map?.Markers;
		if ( markers is null or { Count: 0 } )
			return "the wasteland";

		var bestDist = float.MaxValue;
		string bestName = null;
		for ( var i = 0; i < markers.Count; i++ )
		{
			var marker = markers[i];
			if ( !NamedPlaceKinds.Contains( marker.Kind ) )
				continue;

			var dx = marker.WorldX - worldX;
			var dy = marker.WorldY - worldY;
			var dist = dx * dx + dy * dy;
			if ( dist >= bestDist )
				continue;

			bestDist = dist;
			bestName = FormatMarkerName( marker );
		}

		return string.IsNullOrWhiteSpace( bestName ) ? "the wasteland" : bestName;
	}

	static string FormatMarkerName( ThornsMapMarkerDto marker )
	{
		if ( string.IsNullOrWhiteSpace( marker.Label ) )
			return ThornsMapMarkerStyle.GetLegendTitle( marker.Kind );

		var label = marker.Label.Trim();
		var paren = label.IndexOf( '(', StringComparison.Ordinal );
		if ( paren > 0 )
			label = label[..paren].Trim();

		var lastSpace = label.LastIndexOf( ' ' );
		if ( lastSpace > 0 && lastSpace + 1 < label.Length && char.IsDigit( label[lastSpace + 1] ) )
			label = label[..lastSpace].Trim();

		return string.IsNullOrWhiteSpace( label )
			? Terraingen.UI.ThornsMapMarkerStyle.GetLegendTitle( marker.Kind )
			: label;
	}
}
