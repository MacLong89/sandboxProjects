using System;

namespace Sandbox;

/// <summary>Host-only tame bond perks derived from level / upgrades (intel TTL, pack damage, follow speed).</summary>
public static class ThornsTameBondPerks
{
	const double BaseIntelTtlSeconds = 55.0;

	public static double ResolveAssistIntelTtlSeconds( GameObject ownerPawnRoot )
	{
		if ( ownerPawnRoot is null || !ownerPawnRoot.IsValid() )
			return BaseIntelTtlSeconds;

		var cid = ownerPawnRoot.Network.OwnerId;
		if ( cid == Guid.Empty )
			return BaseIntelTtlSeconds;

		var accountKey = ThornsPersistenceIdentity.GetStableAccountKey( Connection.Find( cid ) );
		var bestExtra = 0;
		ThornsWildlifeTameRegistry.ForEachOwnedBy( cid, accountKey, wid =>
		{
			if ( wid is null || !wid.IsValid() )
				return;
			var lv = wid.ComputeTameLevel();
			if ( lv < 2 )
				return;
			var extra = Math.Min( 24, Math.Max( 0, lv - 1 ) * 3 );
			if ( extra > bestExtra )
				bestExtra = extra;
		} );

		return BaseIntelTtlSeconds + bestExtra;
	}

	public static float PackWarChantDamageMul( ThornsWildlifeIdentity self )
	{
		if ( self is null || !self.IsValid() || !self.HostIsTamed )
			return 1f;
		if ( self.ComputeTameLevel() < 8 || self.TameDmgUpgradeSteps < 2 )
			return 1f;

		var n = CountAlliedTamesInRadius( self, 320f );
		return 1f + Math.Min( 0.03f, n * 0.01f );
	}

	public static float FollowPackStrideSpeedMul( ThornsWildlifeIdentity self, Scene scene )
	{
		if ( self is null || !self.IsValid() || !self.HostIsTamed )
			return 1f;
		if ( self.TameSpdUpgradeSteps < 2 )
			return 1f;

		return CountAlliedTamesInRadius( self, 280f ) > 0 ? 1.045f : 1f;
	}

	static int CountAlliedTamesInRadius( ThornsWildlifeIdentity self, float radius )
	{
		var r2 = radius * radius;
		var myGo = self.GameObject;
		if ( !myGo.IsValid() )
			return 0;

		var flat = self.GameObject.WorldPosition.WithZ( 0 );
		var count = 0;
		var accountKey = self.TameOwnerAccountKeySync ?? "";
		var ownerConn = self.TameOwnerConnectionId;

		ThornsWildlifeTameRegistry.ForEachOwnedBy( ownerConn, accountKey, other =>
		{
			if ( other is null || !other.IsValid() || other == self )
				return;

			var ogo = other.GameObject;
			if ( !ogo.IsValid() )
				return;

			var d = ( ogo.WorldPosition.WithZ( 0 ) - flat ).LengthSquared;
			if ( d > 0.5f && d <= r2 )
				count++;
		} );

		return count;
	}
}
