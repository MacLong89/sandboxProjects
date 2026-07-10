namespace Terraingen.UI;

using Terraingen.Core;

public static class ThornsMinimapDiagnostics
{
	/// <summary>Minimap pan/blip diagnostics — enable with <c>thorns_debug_minimap 1</c>.</summary>
	[ConVar( "thorns_debug_minimap" )]
	public static bool Enabled { get; set; }

	static TimeUntil _nextLog;
	static Vector3 _lastPos;
	static float _lastU;
	static float _lastV;

	public static void LogUpdate(
		Vector3 worldPos,
		float boundsMinX,
		float boundsMinY,
		float boundsMaxX,
		float boundsMaxY,
		float u,
		float v,
		float panLeft,
		float panTop,
		float configuredViewportPx,
		float rectInnerW,
		float rectInnerH )
	{
		if ( !Enabled )
		{
			ThornsDebugState.MinimapLine = "";
			return;
		}

		var moved = (worldPos - _lastPos).LengthSquared > 64f;
		var uvChanged = MathF.Abs( u - _lastU ) > 0.0005f || MathF.Abs( v - _lastV ) > 0.0005f;
		if ( !moved && !uvChanged && _nextLog )
			return;

		_nextLog = 0.5f;

		var du = u - _lastU;
		var dv = v - _lastV;
		var dx = worldPos.x - _lastPos.x;
		var dy = worldPos.y - _lastPos.y;

		_lastPos = worldPos;
		_lastU = u;
		_lastV = v;

		var line =
			$"u={u:F3} v={v:F3} du={du:F4} dv={dv:F4} pan=({panLeft:F3},{panTop:F3}) " +
			$"pos=({worldPos.x:F0},{worldPos.y:F0}) d=({dx:F0},{dy:F0}) " +
			$"bounds=({boundsMinX:F0}..{boundsMaxX:F0},{boundsMinY:F0}..{boundsMaxY:F0}) " +
			$"vp={configuredViewportPx:F0} rect={rectInnerW:F0}x{rectInnerH:F0}";

		ThornsDebugState.MinimapLine = line;
		Log.Info( $"[Thorns Minimap] {line}" );
	}
}
