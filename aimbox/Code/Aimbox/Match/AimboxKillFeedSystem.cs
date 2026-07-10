namespace Sandbox;

public enum AimboxKillFeedRelation
{
	Neutral,
	Local,
	Friendly,
	Enemy
}

public sealed class AimboxKillFeedEntry
{
	public int Id { get; init; }
	public string AttackerName { get; init; }
	public string VictimName { get; init; }
	public AimboxWeaponId Weapon { get; init; }
	public bool Headshot { get; init; }
	public AimboxKillFeedRelation AttackerRelation { get; init; }
	public AimboxKillFeedRelation VictimRelation { get; init; }
	public TimeUntil ExpiresIn { get; init; }

	public string WeaponLabel => AimboxKillFeedFormatting.WeaponLabel( Weapon );

	public string AttackerRelationClass => AimboxKillFeedFormatting.RelationClass( AttackerRelation );

	public string VictimRelationClass => AimboxKillFeedFormatting.RelationClass( VictimRelation );
}

public static class AimboxKillFeedFormatting
{
	public static string WeaponLabel( AimboxWeaponId weapon )
	{
		if ( !AimboxWeapons.All.TryGetValue( weapon, out var def ) )
			return weapon.ToString();

		return def.IsMelee ? "Melee" : def.Name;
	}

	public static string RelationClass( AimboxKillFeedRelation relation ) => relation switch
	{
		AimboxKillFeedRelation.Local => "local",
		AimboxKillFeedRelation.Friendly => "friendly",
		AimboxKillFeedRelation.Enemy => "enemy",
		_ => "neutral"
	};
}

public sealed class AimboxKillFeedSystem
{
	const int MaxEntries = 8;
	const float EntryLifetimeSeconds = 6f;

	readonly List<AimboxKillFeedEntry> _entries = [];
	int _nextId;

	public IReadOnlyList<AimboxKillFeedEntry> Entries => _entries;

	public void Clear() => _entries.Clear();

	public void Record( IAimboxCombatActor attacker, IAimboxCombatActor victim, AimboxWeaponId weapon, bool headshot )
	{
		if ( attacker is null || victim is null )
			return;

		PruneExpired();

		var local = AimboxLocalPlayer.Controller;
		_entries.Insert( 0, new AimboxKillFeedEntry
		{
			Id = _nextId++,
			AttackerName = AimboxCombatActorLabels.KillFeedName( attacker ),
			VictimName = AimboxCombatActorLabels.KillFeedName( victim ),
			Weapon = weapon,
			Headshot = headshot,
			AttackerRelation = ResolveRelation( attacker, local ),
			VictimRelation = ResolveRelation( victim, local ),
			ExpiresIn = EntryLifetimeSeconds
		} );

		while ( _entries.Count > MaxEntries )
			_entries.RemoveAt( _entries.Count - 1 );
	}

	static AimboxKillFeedRelation ResolveRelation( IAimboxCombatActor actor, AimboxPlayerController local )
	{
		if ( actor is null )
			return AimboxKillFeedRelation.Neutral;

		if ( local is null )
			return AimboxKillFeedRelation.Neutral;

		if ( string.Equals( actor.CombatId, local.AccountId, StringComparison.OrdinalIgnoreCase ) )
			return AimboxKillFeedRelation.Local;

		if ( local.IsTeammate( actor ) )
			return AimboxKillFeedRelation.Friendly;

		return AimboxKillFeedRelation.Enemy;
	}

	void PruneExpired()
	{
		for ( var i = _entries.Count - 1; i >= 0; i-- )
		{
			if ( _entries[i].ExpiresIn <= 0f )
				_entries.RemoveAt( i );
		}
	}
}

public static class AimboxCombatActorLabels
{
	public static string KillFeedName( IAimboxCombatActor actor ) =>
		actor switch
		{
			AimboxPlayerController player => player.DisplayName,
			AimboxBotController bot => bot.DisplayName,
			_ => "Unknown"
		};
}
