using System.Collections.Generic;

namespace Sandbox;

/// <summary>
/// Host-only: who last hurt the player, and who the player last hurt — tamed pets use this for assist (THORNS tames).
/// </summary>
public static class ThornsTameHostIntel
{
	const float MaxWorldAssistRange = 50000f;

	readonly record struct TimedRoot( GameObject Root, double ExpireAt );

	static readonly Dictionary<string, TimedRoot> _lastThreatToOwner = new();
	static readonly Dictionary<string, TimedRoot> _lastTargetByOwner = new();

	/// <summary>Pawn / wildlife / bandit root so pets chase the correct networked object.</summary>
	public static GameObject HostResolveCombatChaseRoot( GameObject start )
	{
		if ( start is null || !start.IsValid() )
			return default;

		var pawn = start.Components.GetInAncestorsOrSelf<ThornsPawn>( true );
		if ( pawn.IsValid() )
			return pawn.GameObject;

		var wid = start.Components.GetInAncestorsOrSelf<ThornsWildlifeIdentity>( true );
		if ( wid.IsValid() )
			return wid.GameObject;

		var bandit = start.Components.GetInAncestorsOrSelf<ThornsBanditBrain>( true );
		if ( bandit.IsValid() )
			return bandit.GameObject;

		return start;
	}

	static string HostKeyForPawnRoot( GameObject pawnRoot )
	{
		if ( pawnRoot is null || !pawnRoot.IsValid() )
			return "";

		var conn = Connection.Find( pawnRoot.Network.OwnerId );
		if ( conn is not null )
		{
			var ak = ThornsPersistenceIdentity.GetStableAccountKey( conn );
			if ( !string.IsNullOrEmpty( ak ) )
				return $"a:{ak}";
		}

		return $"c:{pawnRoot.Network.OwnerId:D}";
	}

	/// <summary>Host: player pawn took combat damage — attacker becomes pet priority.</summary>
	public static void HostNotifyOwnerThreatened( GameObject ownerPawnRoot, GameObject attackerRoot )
	{
		if ( !Networking.IsHost || ownerPawnRoot is null || !ownerPawnRoot.IsValid()
		     || attackerRoot is null || !attackerRoot.IsValid() )
			return;

		var atk = HostResolveCombatChaseRoot( attackerRoot );
		if ( !atk.IsValid() || atk == ownerPawnRoot )
			return;

		var key = HostKeyForPawnRoot( ownerPawnRoot );
		if ( string.IsNullOrEmpty( key ) )
			return;

		var ttl = ThornsTameBondPerks.ResolveAssistIntelTtlSeconds( ownerPawnRoot );
		_lastThreatToOwner[key] = new TimedRoot( atk, Time.Now + ttl );
	}

	/// <summary>Host: pawn dealt weapon damage — victim becomes pet assist target.</summary>
	public static void HostNotifyOwnerDamagedTarget( GameObject ownerPawnRoot, GameObject victimDamageReceiver )
	{
		if ( !Networking.IsHost || ownerPawnRoot is null || !ownerPawnRoot.IsValid()
		     || victimDamageReceiver is null || !victimDamageReceiver.IsValid() )
			return;

		var vic = HostResolveCombatChaseRoot( victimDamageReceiver );
		if ( !vic.IsValid() || vic == ownerPawnRoot )
			return;

		var key = HostKeyForPawnRoot( ownerPawnRoot );
		if ( string.IsNullOrEmpty( key ) )
			return;

		var ttl = ThornsTameBondPerks.ResolveAssistIntelTtlSeconds( ownerPawnRoot );
		_lastTargetByOwner[key] = new TimedRoot( vic, Time.Now + ttl );
	}

	public static bool HostTryResolveAssistTarget(
		GameObject ownerPawnRoot,
		ThornsWildlifeIdentity selfTame,
		GameObject selfPetRoot,
		Vector3 ownerFlat,
		Vector3 petFlat,
		out GameObject targetRoot )
	{
		targetRoot = null;
		if ( !Networking.IsHost || ownerPawnRoot is null || !ownerPawnRoot.IsValid()
		     || selfTame is null || !selfTame.IsValid()
		     || selfPetRoot is null || !selfPetRoot.IsValid() )
			return false;

		var key = HostKeyForPawnRoot( ownerPawnRoot );
		if ( string.IsNullOrEmpty( key ) )
			return false;

		PurgeStaleForKey( key );

		if ( _lastThreatToOwner.TryGetValue( key, out var thr )
		     && thr.ExpireAt >= Time.Now )
		{
			var v = ValidateAssistTarget( ownerPawnRoot, selfTame, selfPetRoot, thr.Root, ownerFlat, petFlat );
			if ( v.IsValid() )
			{
				targetRoot = v;
				return true;
			}
		}

		if ( _lastTargetByOwner.TryGetValue( key, out var eng )
		     && eng.ExpireAt >= Time.Now )
		{
			var v = ValidateAssistTarget( ownerPawnRoot, selfTame, selfPetRoot, eng.Root, ownerFlat, petFlat );
			if ( v.IsValid() )
			{
				targetRoot = v;
				return true;
			}
		}

		return false;
	}

	static void PurgeStaleForKey( string key )
	{
		if ( _lastThreatToOwner.TryGetValue( key, out var t ) && t.ExpireAt < Time.Now )
			_lastThreatToOwner.Remove( key );
		if ( _lastTargetByOwner.TryGetValue( key, out var e ) && e.ExpireAt < Time.Now )
			_lastTargetByOwner.Remove( key );
	}

	static GameObject ValidateAssistTarget(
		GameObject ownerRoot,
		ThornsWildlifeIdentity selfId,
		GameObject selfPet,
		GameObject candidate,
		Vector3 ownerFlat,
		Vector3 petFlat )
	{
		if ( candidate is null || !candidate.IsValid() )
			return default;

		if ( candidate == ownerRoot || candidate == selfPet )
			return default;

		var hp = candidate.Components.GetInAncestorsOrSelf<ThornsHealth>( true );
		if ( !hp.IsValid() || !hp.IsAlive || hp.IsDeadState )
			return default;

		var wid = candidate.Components.GetInAncestorsOrSelf<ThornsWildlifeIdentity>( true );
		if ( wid.IsValid() && wid.HostIsTamed && ThornsWildlifeIdentity.HostTamesShareOwner( selfId, wid ) )
			return default;

		var candFlat = candidate.WorldPosition.WithZ( 0 );
		if ( ( candFlat - ownerFlat ).Length > MaxWorldAssistRange
		     && ( candFlat - petFlat ).Length > MaxWorldAssistRange )
			return default;

		return candidate;
	}
}
