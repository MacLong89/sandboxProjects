namespace Sandbox;

public readonly record struct ThornsSnapWorldPivot( Vector3 Position, Vector3 ForwardHorizontal );

/// <summary>World-space authored socket frames from <see cref="ThornsSnapSocketBlueprint"/> + structure transform.</summary>
public static class ThornsSnapTransforms
{
	public static ThornsSnapWorldPivot WorldPivot( ThornsPlacedStructure ps, ThornsSnapSocketBlueprint socket )
	{
		var wt = ps.GameObject.WorldPosition;
		var wr = ps.GameObject.WorldRotation;
		var p = wt + wr * socket.LocalPosition;
		var f = wr * socket.LocalForwardHorizontal;
		var fwd = new Vector3( f.x, f.y, 0f );
		var fl = fwd.Length > 1e-4f ? fwd.Normal : new Vector3( 1f, 0f, 0f );
		return new ThornsSnapWorldPivot( p, fl );
	}

	public static float ScoreAimProximity( ThornsSnapWorldPivot pivot, Vector3 aimApprox ) =>
		(pivot.Position - aimApprox).Length;
}
