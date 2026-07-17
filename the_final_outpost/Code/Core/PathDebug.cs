namespace FinalOutpost;

/// <summary>
/// Rate-limited console logs for humanoid pathing. Toggle with <see cref="Enabled"/>.
/// </summary>
public static class PathDebug
{
	/// <summary>
	/// Master switch. AUDIT FIX M4 (2026-07): defaulted to true and flooded combat nights.
	/// Leave false for play; flip true only while debugging pathing.
	/// </summary>
	public static bool Enabled { get; set; } = false;

	/// <summary>Minimum seconds between repeat logs for the same agent + event.</summary>
	public static float MinInterval { get; set; } = 0.5f;

	static readonly Dictionary<string, double> LastLog = new();

	public static void Event( string agent, string evt, string detail = null )
	{
		if ( !Enabled )
			return;

		var key = $"{agent}|{evt}";
		var now = Time.Now;
		if ( LastLog.TryGetValue( key, out var prev ) && now - prev < MinInterval )
			return;

		LastLog[key] = now;
		var suffix = string.IsNullOrWhiteSpace( detail ) ? "" : $" — {detail}";
		Log.Info( $"[Path] {agent} {evt}{suffix}" );
	}

	public static void Warn( string agent, string evt, string detail = null )
	{
		if ( !Enabled )
			return;

		var key = $"{agent}|{evt}";
		var now = Time.Now;
		if ( LastLog.TryGetValue( key, out var prev ) && now - prev < MinInterval )
			return;

		LastLog[key] = now;
		var suffix = string.IsNullOrWhiteSpace( detail ) ? "" : $" — {detail}";
		Log.Warning( $"[Path] {agent} {evt}{suffix}" );
	}

	public static string Fmt( Vector3 v ) =>
		$"({v.x:0},{v.y:0},{v.z:0})";

	public static string Cell( Vector3 world )
	{
		BuildGrid.WorldToCell( world, out var cx, out var cy );
		return $"[{cx},{cy}]";
	}
}
