namespace Sandbox;

/// <summary>Guild friendly-fire rules — paired with <see cref="ThornsWildlifeIdentity.HostShouldSuppressTameFriendlyFire"/>.</summary>
public static class ThornsGuildCombat
{
	/// <summary>
	/// Host-only: no damage when the attacker has the victim on their guild roster (you don't hurt guild mates you added).
	/// </summary>
	public static bool HostShouldSuppressGuildFriendlyFire( GameObject victimRoot, GameObject attackerRoot )
	{
		if ( !victimRoot.IsValid() || !attackerRoot.IsValid() )
			return false;

		var victimPawn = victimRoot.Components.GetInAncestorsOrSelf<ThornsPawn>( true );
		var atkPawn = attackerRoot.Components.GetInAncestorsOrSelf<ThornsPawn>( true );
		if ( !victimPawn.IsValid() || !atkPawn.IsValid() )
			return false;

		if ( victimPawn.GameObject == atkPawn.GameObject )
			return false;

		if ( !ThornsGuildRoster.TryGetAccountKeyForPawnRoot( victimPawn.GameObject, out var victimKey ) )
			return false;

		var atkRoster = atkPawn.GameObject.Components.Get<ThornsGuildRoster>( FindMode.EnabledInSelf );
		if ( !atkRoster.IsValid() )
			return false;

		return atkRoster.ContainsAccountKey( victimKey );
	}
}
