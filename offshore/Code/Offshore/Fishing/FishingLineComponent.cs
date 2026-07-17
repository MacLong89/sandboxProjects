namespace Offshore;

/// <summary>
/// Draws rod-tip → hook line.
/// Dashed aim arc only while charging a cast; flight trail while the bobber is in the air.
/// </summary>
public sealed class FishingLineComponent : Component
{
	[Property] public int SegmentCount { get; set; } = 12;

	public GameObject RodTip { get; set; }
	public HookComponent Hook { get; set; }
	public CastComponent Cast { get; set; }
	public WaterVolumeComponent Water { get; set; }

	protected override void OnUpdate()
	{
		if ( RodTip is null || !RodTip.IsValid() || Hook is null )
			return;

		var tip = RodTip.WorldPosition;
		var state = OffshoreGameController.Instance?.State ?? FishingSessionState.DockIdle;

		// Dashed aim arc while holding / charging cast (not while idle aiming).
		if ( state == FishingSessionState.ChargingCast && Cast is not null && Cast.Charge > 0.01f )
		{
			DrawAimPreview( tip );
			return;
		}

		if ( state == FishingSessionState.Casting && Cast is not null && Cast.IsFlying )
		{
			DrawFlightCurve( tip );
			return;
		}

		if ( Hook.IsVisible )
			DrawTautLine( tip, Hook.WorldPosition );
	}

	private void DrawAimPreview( Vector3 tip )
	{
		if ( Cast is null )
			return;

		var end = Cast.PreviewLanding( tip, Water, Cast.Charge );
		var peakZ = tip.z + (end.x - tip.x) * OffshoreConstants.ArcPeakScale * 0.5f;
		var peak = new Vector3( (tip.x + end.x) * 0.5f, tip.y, peakZ );
		DrawCurve( tip, peak, end, OffshoreConstants.AimPreviewColor, dashed: true );
	}

	private void DrawFlightCurve( Vector3 tip )
	{
		var count = Math.Max( 2, SegmentCount );
		Vector3 prev = tip;
		for ( var i = 1; i <= count; i++ )
		{
			var t = (i / (float)count) * Cast.FlightProgress;
			var next = Cast.EvaluateFlight( t );
			DebugOverlay.Line( prev, next, OffshoreConstants.LineColor, 0f );
			prev = next;
		}
	}

	private void DrawTautLine( Vector3 tip, Vector3 hook )
	{
		var count = Math.Max( 2, SegmentCount );
		for ( var i = 0; i < count; i++ )
		{
			var t0 = i / (float)count;
			var t1 = (i + 1) / (float)count;
			DebugOverlay.Line( Vector3.Lerp( tip, hook, t0 ), Vector3.Lerp( tip, hook, t1 ), OffshoreConstants.LineColor, 0f );
		}
	}

	private void DrawCurve( Vector3 a, Vector3 b, Vector3 c, Color color, bool dashed )
	{
		var count = Math.Max( 2, SegmentCount );
		for ( var i = 0; i < count; i++ )
		{
			if ( dashed && (i % 2 == 1) )
				continue;

			var t0 = i / (float)count;
			var t1 = (i + 1) / (float)count;
			DebugOverlay.Line( QuadBez( a, b, c, t0 ), QuadBez( a, b, c, t1 ), color, 0f );
		}
	}

	private static Vector3 QuadBez( Vector3 a, Vector3 b, Vector3 c, float t )
	{
		var ab = Vector3.Lerp( a, b, t );
		var bc = Vector3.Lerp( b, c, t );
		return Vector3.Lerp( ab, bc, t );
	}
}
