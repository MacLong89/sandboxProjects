namespace Sandbox;

/// <summary>Lightweight ray counters when <see cref="CountRays"/> is enabled (dev tools).</summary>
public static class ThornsTraceDiagnostics
{
	public static bool CountRays { get; set; }

	static readonly int[] _byProfile = new int[64];

	public static void BumpRay( ThornsTraceProfile profile )
	{
		if ( !CountRays )
			return;

		var i = (int)profile;
		if ( (uint)i < (uint)_byProfile.Length )
			_byProfile[i]++;
	}

	public static int GetRayCount( ThornsTraceProfile profile )
	{
		var i = (int)profile;
		return (uint)i < (uint)_byProfile.Length ? _byProfile[i] : 0;
	}
}
