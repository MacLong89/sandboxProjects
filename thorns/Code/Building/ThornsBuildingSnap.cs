namespace Sandbox;

/// <summary>Trace helpers — socket alignment lives in <see cref="ThornsSnapResolver"/>.</summary>
public static class ThornsBuildingSnap
{
	/// <summary>Nudge past hit surface so probes land inside volume / edge zones.</summary>
	public static Vector3 BumpFromTrace( Vector3 hitPosition, Vector3 hitNormal )
	{
		var bump = MathF.Max( 2f, ThornsBuildingModule.Cell * 0.05f );
		return hitPosition + hitNormal * bump;
	}
}
