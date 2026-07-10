namespace Terraingen.Clutter;

/// <summary>Tracks mesh clutter model pick vs placement for mix diagnostics.</summary>
public sealed class ThornsClutterMixDebug
{
	readonly Dictionary<string, int> _picked = new( StringComparer.OrdinalIgnoreCase );
	readonly Dictionary<string, int> _placed = new( StringComparer.OrdinalIgnoreCase );
	readonly Dictionary<string, int> _rejectMaterial = new( StringComparer.OrdinalIgnoreCase );
	int _rejectAccept;
	int _rejectPick;
	int _rejectRay;

	public void Reset()
	{
		_picked.Clear();
		_placed.Clear();
		_rejectMaterial.Clear();
		_rejectAccept = 0;
		_rejectPick = 0;
		_rejectRay = 0;
	}

	public void RecordPick( string label )
	{
		if ( string.IsNullOrWhiteSpace( label ) )
			label = "(unknown)";
		Inc( _picked, label );
	}

	public void RecordPlaced( string label )
	{
		if ( string.IsNullOrWhiteSpace( label ) )
			label = "(unknown)";
		Inc( _placed, label );
	}

	public void RecordRejectMaterial( string label )
	{
		if ( string.IsNullOrWhiteSpace( label ) )
			label = "(unknown)";
		Inc( _rejectMaterial, label );
	}

	public void RecordRejectAccept() => _rejectAccept++;

	public void RecordRejectPick() => _rejectPick++;

	public void RecordRejectRay() => _rejectRay++;

	public string FormatCounts( string title, IReadOnlyDictionary<string, int> counts )
	{
		if ( counts.Count == 0 )
			return $"{title}: (none)";

		var total = counts.Values.Sum();
		var parts = counts
			.OrderByDescending( pair => pair.Value )
			.Select( pair => $"{ShortLabel( pair.Key )}={pair.Value} ({Pct( pair.Value, total )})" );
		return $"{title}: {string.Join( ", ", parts )}";
	}

	static string ShortLabel( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return "?";

		var slash = path.LastIndexOf( '/' );
		var label = slash >= 0 ? path[(slash + 1)..] : path;
		return label.Replace( ".vmdl", "", StringComparison.OrdinalIgnoreCase );
	}

	public string BuildSummary( int activeMeshInstances )
	{
		var pickedTotal = _picked.Values.Sum();
		var placedTotal = _placed.Values.Sum();
		return string.Join(
			" | ",
			$"mesh active={activeMeshInstances} picked={pickedTotal} placed={placedTotal}",
			FormatCounts( "picked", _picked ),
			FormatCounts( "placed", _placed ),
			FormatCounts( "reject-material", _rejectMaterial ),
			$"reject accept/pick/ray={_rejectAccept}/{_rejectPick}/{_rejectRay}" );
	}

	static void Inc( Dictionary<string, int> dict, string label )
	{
		dict.TryGetValue( label, out var count );
		dict[label] = count + 1;
	}

	static string Pct( int value, int total )
		=> total <= 0 ? "0%" : $"{100f * value / total:F1}%";
}
