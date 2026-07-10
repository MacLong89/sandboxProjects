namespace Terraingen.Minerals;

/// <summary>Per-prop mineral shadow bands — avoids chunk-center toggles that pop whole scatter groups.</summary>
public static class ThornsMineralShadowLod
{
	public static bool WantsShadowAtDistance( float distanceSq, in ThornsMineralConfig config, bool isStone )
	{
		if ( isStone )
		{
			var closeOff = config.MineralStoneShadowCloseOffInches;
			if ( closeOff > 0f && distanceSq <= closeOff * closeOff )
				return false;
		}

		var shadowDist = config.MineralShadowDistanceInches;
		return distanceSq <= shadowDist * shadowDist;
	}

	public static bool ShouldCastShadow(
		float distanceSq,
		bool shadowsCurrentlyEnabled,
		in ThornsMineralConfig config,
		bool isStone )
	{
		if ( isStone )
		{
			var closeOff = config.MineralStoneShadowCloseOffInches;
			if ( closeOff > 0f && distanceSq <= closeOff * closeOff )
				return false;
		}

		var shadowDist = config.MineralShadowDistanceInches;
		var hysteresis = config.ShadowLodHysteresisInches;
		var outerSq = (shadowDist + hysteresis) * (shadowDist + hysteresis);
		var inner = MathF.Max( shadowDist - hysteresis, 0f );
		var innerSq = inner * inner;

		return shadowsCurrentlyEnabled
			? distanceSq <= outerSq
			: distanceSq <= innerSq;
	}
}
