using System.Collections.Generic;
using System.Linq;

namespace Sandbox;

/// <summary>
/// Queries active player roots / roles. Does not hold authoritative state — roles live on <see cref="YaPlayerRoleComponent"/>.
/// Use for UI, win checks, and extensions (spectators, perks).
/// </summary>
public static class YaTeamSystem
{
	/// <summary>All player session roots in the active scene (one per connection).</summary>
	public static IEnumerable<YaPlayerSession> EnumerateSessions( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			yield break;

		foreach ( var s in scene.GetAllComponents<YaPlayerSession>() )
		{
			if ( s.IsValid() && s.OwnerConnection is not null )
				yield return s;
		}
	}

	public static int CountConnectedPlayers( Scene scene ) => EnumerateSessions( scene ).Count();

	public static IEnumerable<GameObject> EnumeratePlayerRoots( Scene scene )
	{
		foreach ( var s in EnumerateSessions( scene ) )
		{
			if ( s.GameObject.IsValid() )
				yield return s.GameObject;
		}
	}

	public static YaPlayerRole GetRole( GameObject playerRoot )
	{
		var role = playerRoot.Components.Get<YaPlayerRoleComponent>( FindMode.EnabledInSelf );
		return role.IsValid() ? role.Role : YaPlayerRole.Unassigned;
	}

	/// <summary>Pawn root that currently has <see cref="YaPlayerRole.Alone"/>, if any.</summary>
	public static GameObject FindAloneRoot( Scene scene )
	{
		foreach ( var root in EnumeratePlayerRoots( scene ) )
		{
			if ( GetRole( root ) == YaPlayerRole.Alone )
				return root;
		}

		return null;
	}
}
