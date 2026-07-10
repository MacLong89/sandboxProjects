namespace Sandbox;

public static class ThornsBanditDetectionSystem
{
	public static bool IsInVisionCone( Vector3 selfFlat, Rotation selfRot, Vector3 targetFlat, float coneDegrees )
	{
		var toTarget = ( targetFlat - selfFlat ).Normal;
		var forward = selfRot.Forward.WithZ( 0 ).Normal;
		if ( forward.LengthSquared < 1e-6f || toTarget.LengthSquared < 1e-6f )
			return true;

		var dot = Vector3.Dot( forward, toTarget );
		var halfCone = MathF.Cos( coneDegrees * 0.5f * MathF.PI / 180f );
		return dot >= halfCone;
	}

	public static bool TryRefreshDetection(
		ThornsBanditBrainContext ctx,
		ThornsBanditDirector director,
		Vector3 selfFlat,
		out GameObject seenTarget )
	{
		seenTarget = default;
		var now = Time.Now;
		if ( now - ctx.LastDetectionRealtime < ctx.Archetype.DetectionRefreshIntervalSeconds )
			return ctx.CurrentTarget.IsValid();

		ctx.LastDetectionRealtime = now;
		return ctx.Brain.StateMachineTryAcquireVisibleTarget( ctx, director, selfFlat, out seenTarget );
	}

	public static bool TryRefreshHearing( ThornsBanditBrainContext ctx, Vector3 selfFlat, out Vector3 heardPoint )
	{
		heardPoint = default;
		if ( !ThornsBanditHearingHub.TryGetNearestHeardEvent(
			     selfFlat,
			     ctx.Archetype,
			     maxAgeSeconds: 8.0,
			     out var ev,
			     out _ ) )
			return false;

		heardPoint = ev.World;
		ctx.LastHeardThreatRealtime = ev.Time;
		ctx.InvestigatePoint = heardPoint;
		return true;
	}
}
