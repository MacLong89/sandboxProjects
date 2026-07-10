namespace Sandbox;

public enum AimboxProgressionCategory
{
	Overview,
	Perks,
	Weapons,
	Attachments,
	KillEffects
}

public enum AimboxProgressionRewardKind
{
	Perk,
	Weapon,
	Attachment,
	Killstreak,
	LevelGap
}

public sealed record AimboxProgressionRewardEntry(
	string Key,
	AimboxProgressionRewardKind Kind,
	int Level,
	string Name,
	string TypeLabel,
	string Description,
	string IconCode,
	string AccentColor,
	bool IsUnlocked,
	AimboxWeaponId? Weapon = null,
	AimboxAttachmentId? Attachment = null,
	AimboxPerkId? Perk = null,
	AimboxKillstreakId? Killstreak = null )
{
	public bool IsFuture => !IsUnlocked && Level > 0;
}

public static class AimboxProgressionUiHelpers
{
	public static readonly IReadOnlyList<(AimboxProgressionCategory Id, string Label, string Accent)> Categories =
	[
		(AimboxProgressionCategory.Perks, "PERKS", "#ff2a6d"),
		(AimboxProgressionCategory.Weapons, "WEAPONS", "#2a9dff"),
		(AimboxProgressionCategory.Attachments, "ATTACHMENTS", "#ffb82a"),
		(AimboxProgressionCategory.KillEffects, "KILL EFFECTS", "#2ad4c8")
	];

	public static (int Unlocked, int Total) CategoryProgress( AimboxPlayerData data, AimboxProgressionCategory category )
	{
		if ( data is null )
			return (0, 0);

		switch ( category )
		{
			case AimboxProgressionCategory.Perks:
			{
				var unlocked = 0;
				foreach ( var perk in AimboxMw2Catalog.Perks )
				{
					if ( AimboxUnlockService.IsPerkUnlocked( data, perk.Id ) )
						unlocked++;
				}

				return (unlocked, AimboxMw2Catalog.Perks.Count);
			}
			case AimboxProgressionCategory.Weapons:
			{
				var unlocked = 0;
				var weapons = ProgressionWeapons();
				foreach ( var weapon in weapons )
				{
					if ( AimboxUnlockService.IsWeaponUnlocked( data, weapon ) )
						unlocked++;
				}

				return (unlocked, weapons.Count);
			}
			case AimboxProgressionCategory.Attachments:
				return (CountUnlockedAttachments( data ), AimboxMw2Catalog.AttachmentChallenges.Count);
			case AimboxProgressionCategory.KillEffects:
			{
				var unlocked = 0;
				foreach ( var streak in AimboxMw2Catalog.Killstreaks )
				{
					if ( AimboxUnlockService.IsKillstreakUnlocked( data, streak.Id ) )
						unlocked++;
				}

				return (unlocked, AimboxMw2Catalog.Killstreaks.Count);
			}
			default:
				return (0, 0);
		}
	}

	public static IReadOnlyList<AimboxProgressionRewardEntry> BuildTimeline(
		AimboxPlayerData data,
		AimboxProgressionCategory category,
		int count = 6 )
	{
		if ( data is null )
			return [];

		if ( category == AimboxProgressionCategory.Attachments )
			return BuildAttachmentTimeline( data, count );

		var entries = new List<AimboxProgressionRewardEntry>();
		var startLevel = Math.Max( 1, data.PlayerLevel - 2 );
		var level = startLevel;

		while ( entries.Count < count && level <= AimboxMw2Catalog.MaxRank )
		{
			var atLevel = RewardsAtLevel( data, level, category );
			if ( atLevel.Count > 0 )
				entries.Add( PickTimelineReward( atLevel, category ) );
			else if ( category == AimboxProgressionCategory.Overview && level > data.PlayerLevel )
				entries.Add( LevelGapEntry( level ) );

			level++;
		}

		return entries;
	}

	public static IReadOnlyList<AimboxProgressionRewardEntry> BuildCategoryInventory(
		AimboxPlayerData data,
		AimboxProgressionCategory category )
	{
		if ( data is null || category is AimboxProgressionCategory.Overview )
			return [];

		switch ( category )
		{
			case AimboxProgressionCategory.Perks:
			{
				var entries = new List<AimboxProgressionRewardEntry>();
				foreach ( var perk in AimboxMw2Catalog.Perks )
					entries.Add( PerkEntry( data, perk ) );

				SortEntriesByLevelName( entries );
				return entries;
			}
			case AimboxProgressionCategory.Weapons:
			{
				var entries = new List<AimboxProgressionRewardEntry>();
				foreach ( var weapon in ProgressionWeapons() )
					entries.Add( WeaponEntry( data, weapon ) );

				SortEntriesByLevelName( entries );
				return entries;
			}
			case AimboxProgressionCategory.Attachments:
			{
				var entries = BuildAllAttachmentEntries( data );
				entries.Sort( ( a, b ) =>
				{
					var unlockedCompare = (a.IsUnlocked ? 0 : 1).CompareTo( b.IsUnlocked ? 0 : 1 );
					return unlockedCompare != 0 ? unlockedCompare : string.Compare( a.Name, b.Name, StringComparison.Ordinal );
				} );

				return entries;
			}
			case AimboxProgressionCategory.KillEffects:
			{
				var entries = new List<AimboxProgressionRewardEntry>();
				foreach ( var streak in AimboxMw2Catalog.Killstreaks )
					entries.Add( KillstreakEntry( data, streak ) );

				SortEntriesByLevelName( entries );
				return entries;
			}
			default:
				return [];
		}
	}

	public static AimboxProgressionRewardEntry DefaultSelection(
		AimboxPlayerData data,
		AimboxProgressionCategory category,
		IReadOnlyList<AimboxProgressionRewardEntry> timeline )
	{
		if ( data is null || timeline.Count == 0 )
			return null;

		foreach ( var entry in timeline )
		{
			if ( !entry.IsUnlocked && entry.Kind != AimboxProgressionRewardKind.LevelGap )
				return entry;
		}

		for ( var i = timeline.Count - 1; i >= 0; i-- )
		{
			var entry = timeline[i];
			if ( entry.Level <= data.PlayerLevel && entry.Kind != AimboxProgressionRewardKind.LevelGap )
				return entry;
		}

		return timeline[0];
	}

	public static string StatusLabel( AimboxPlayerData data, AimboxProgressionRewardEntry entry )
	{
		if ( entry is null )
			return "";

		if ( entry.IsUnlocked )
			return "UNLOCKED";

		return entry.Kind switch
		{
			AimboxProgressionRewardKind.Attachment => AttachmentStatus( data, entry ),
			AimboxProgressionRewardKind.LevelGap => $"LEVEL {entry.Level}",
			_ => entry.Level > 0 ? $"LEVEL {entry.Level}" : "LOCKED"
		};
	}

	public static string EquipPreviewLabel( AimboxProgressionRewardEntry entry )
	{
		if ( entry is null || !entry.IsUnlocked )
			return "[R] LOCKED";

		return "[R] OPEN LOADOUTS";
	}

	public static int HeadshotPercent( AimboxPlayerData data ) =>
		data is null || data.Kills <= 0 ? 0 : (int)MathF.Round( data.Headshots * 100f / data.Kills );

	public static int WinRatePercent( AimboxPlayerData data ) =>
		data is null || data.MatchesPlayed <= 0 ? 0 : (int)MathF.Round( data.Wins * 100f / data.MatchesPlayed );

	public static int AccuracyPercent( AimboxPlayerData data ) =>
		data is null ? 0 : (int)MathF.Round( data.Accuracy * 100f );

	static IReadOnlyList<AimboxWeaponId> ProgressionWeapons()
	{
		var weapons = new List<AimboxWeaponId>();
		foreach ( var weapon in AimboxMw2Catalog.PrimaryWeapons )
			AddUniqueWeapon( weapons, weapon );

		foreach ( var weapon in AimboxMw2Catalog.SecondaryWeapons )
			AddUniqueWeapon( weapons, weapon );

		return weapons;
	}

	static void AddUniqueWeapon( List<AimboxWeaponId> weapons, AimboxWeaponId weapon )
	{
		foreach ( var existing in weapons )
		{
			if ( existing == weapon )
				return;
		}

		weapons.Add( weapon );
	}

	static int CountUnlockedAttachments( AimboxPlayerData data )
	{
		var count = 0;
		foreach ( var challenge in AimboxMw2Catalog.AttachmentChallenges )
		{
			if ( AimboxUnlockService.IsAttachmentUnlocked( challenge.Weapon, data.GetWeapon( challenge.Weapon ), challenge.Attachment ) )
				count++;
		}

		return count;
	}

	static IReadOnlyList<AimboxProgressionRewardEntry> BuildAttachmentTimeline( AimboxPlayerData data, int count )
	{
		var entries = new List<AimboxProgressionRewardEntry>();
		foreach ( var entry in BuildAllAttachmentEntries( data ) )
		{
			if ( !entry.IsUnlocked )
				entries.Add( entry );
		}

		entries.Sort( ( a, b ) =>
		{
			var distanceCompare = AttachmentDistance( data, a ).CompareTo( AttachmentDistance( data, b ) );
			return distanceCompare != 0 ? distanceCompare : string.Compare( a.Name, b.Name, StringComparison.Ordinal );
		} );

		if ( entries.Count <= count )
			return entries;

		var limited = new List<AimboxProgressionRewardEntry>();
		for ( var i = 0; i < count; i++ )
			limited.Add( entries[i] );

		return limited;
	}

	static float AttachmentDistance( AimboxPlayerData data, AimboxProgressionRewardEntry entry )
	{
		if ( entry.Weapon is not { } weapon || entry.Attachment is not { } attachment )
			return float.MaxValue;

		AimboxAttachmentChallenge challenge = null;
		foreach ( var candidate in AimboxMw2Catalog.AttachmentChallenges )
		{
			if ( candidate.Weapon == weapon && candidate.Attachment == attachment )
			{
				challenge = candidate;
				break;
			}
		}

		if ( challenge is null )
			return float.MaxValue;

		var weaponData = data.GetWeapon( weapon );
		return Math.Max( 0, challenge.RequiredMasteryLevel - weaponData.Level );
	}

	static List<AimboxProgressionRewardEntry> BuildAllAttachmentEntries( AimboxPlayerData data )
	{
		var entries = new List<AimboxProgressionRewardEntry>();
		foreach ( var challenge in AimboxMw2Catalog.AttachmentChallenges )
			entries.Add( AttachmentEntry( data, challenge ) );

		return entries;
	}

	static List<AimboxProgressionRewardEntry> RewardsAtLevel(
		AimboxPlayerData data,
		int level,
		AimboxProgressionCategory category )
	{
		var rewards = new List<AimboxProgressionRewardEntry>();

		if ( category is AimboxProgressionCategory.Overview or AimboxProgressionCategory.Weapons )
		{
			foreach ( var weapon in ProgressionWeapons() )
			{
				if ( AimboxWeapons.Get( weapon ).UnlockLevel == level )
					rewards.Add( WeaponEntry( data, weapon ) );
			}
		}

		if ( category is AimboxProgressionCategory.Overview or AimboxProgressionCategory.Perks )
		{
			foreach ( var perk in AimboxMw2Catalog.Perks )
			{
				if ( perk.UnlockLevel == level )
					rewards.Add( PerkEntry( data, perk ) );
			}
		}

		if ( category is AimboxProgressionCategory.Overview or AimboxProgressionCategory.KillEffects )
		{
			foreach ( var streak in AimboxMw2Catalog.Killstreaks )
			{
				if ( streak.UnlockLevel == level )
					rewards.Add( KillstreakEntry( data, streak ) );
			}
		}

		return rewards;
	}

	static AimboxProgressionRewardEntry PickTimelineReward(
		IReadOnlyList<AimboxProgressionRewardEntry> entries,
		AimboxProgressionCategory category )
	{
		if ( category == AimboxProgressionCategory.Perks )
			return FirstEntryOfKind( entries, AimboxProgressionRewardKind.Perk ) ?? entries[0];

		if ( category == AimboxProgressionCategory.Weapons )
			return FirstEntryOfKind( entries, AimboxProgressionRewardKind.Weapon ) ?? entries[0];

		if ( category == AimboxProgressionCategory.KillEffects )
			return FirstEntryOfKind( entries, AimboxProgressionRewardKind.Killstreak ) ?? entries[0];

		var best = entries[0];
		var bestPriority = RewardPriority( best.Kind );
		for ( var i = 1; i < entries.Count; i++ )
		{
			var priority = RewardPriority( entries[i].Kind );
			if ( priority < bestPriority )
			{
				best = entries[i];
				bestPriority = priority;
			}
		}

		return best;
	}

	static AimboxProgressionRewardEntry FirstEntryOfKind(
		IReadOnlyList<AimboxProgressionRewardEntry> entries,
		AimboxProgressionRewardKind kind )
	{
		foreach ( var entry in entries )
		{
			if ( entry.Kind == kind )
				return entry;
		}

		return null;
	}

	static int RewardPriority( AimboxProgressionRewardKind kind ) => kind switch
	{
		AimboxProgressionRewardKind.Weapon => 0,
		AimboxProgressionRewardKind.Perk => 1,
		AimboxProgressionRewardKind.Killstreak => 2,
		_ => 3
	};

	static void SortEntriesByLevelName( List<AimboxProgressionRewardEntry> entries )
	{
		entries.Sort( ( a, b ) =>
		{
			var levelCompare = a.Level.CompareTo( b.Level );
			return levelCompare != 0 ? levelCompare : string.Compare( a.Name, b.Name, StringComparison.Ordinal );
		} );
	}

	static AimboxProgressionRewardEntry LevelGapEntry( int level ) => new(
		$"gap:{level}",
		AimboxProgressionRewardKind.LevelGap,
		level,
		"LOCKED",
		"REWARD",
		"No unlock scheduled at this rank yet. Keep grinding.",
		"??",
		"#666666",
		false );

	static AimboxProgressionRewardEntry PerkEntry( AimboxPlayerData data, AimboxPerkDefinition perk ) => new(
		$"perk:{perk.Id}",
		AimboxProgressionRewardKind.Perk,
		perk.UnlockLevel,
		perk.Name.ToUpperInvariant(),
		"PERK",
		perk.Description,
		AimboxClassUiHelpers.PerkShortCode( perk.Id ),
		"#ff2a6d",
		AimboxUnlockService.IsPerkUnlocked( data, perk.Id ),
		Perk: perk.Id );

	static AimboxProgressionRewardEntry WeaponEntry( AimboxPlayerData data, AimboxWeaponId weapon )
	{
		var def = AimboxWeapons.Get( weapon );
		return new(
			$"weapon:{weapon}",
			AimboxProgressionRewardKind.Weapon,
			def.UnlockLevel,
			def.Name.ToUpperInvariant(),
			AimboxClassUiHelpers.WeaponClassLabel( weapon ),
			$"Unlocks at rank {def.UnlockLevel}. {BuildWeaponBlurb( def )}",
			AimboxClassUiHelpers.WeaponShortCode( weapon ),
			AimboxClassUiHelpers.WeaponAccent( weapon ),
			AimboxUnlockService.IsWeaponUnlocked( data, weapon ),
			Weapon: weapon );
	}

	static AimboxProgressionRewardEntry KillstreakEntry( AimboxPlayerData data, AimboxKillstreakDefinition streak ) => new(
		$"killstreak:{streak.Id}",
		AimboxProgressionRewardKind.Killstreak,
		streak.UnlockLevel,
		streak.Name.ToUpperInvariant(),
		"KILLSTREAK",
		$"{streak.KillThreshold} kills · {streak.Description}",
		AimboxClassUiHelpers.KillstreakShortCode( streak.Id ),
		"#2ad4c8",
		AimboxUnlockService.IsKillstreakUnlocked( data, streak.Id ),
		Killstreak: streak.Id );

	static AimboxProgressionRewardEntry AttachmentEntry( AimboxPlayerData data, AimboxAttachmentChallenge challenge )
	{
		var weaponData = data.GetWeapon( challenge.Weapon );
		var unlocked = AimboxUnlockService.IsAttachmentUnlocked( challenge.Weapon, weaponData, challenge.Attachment );
		var weaponName = AimboxWeapons.Get( challenge.Weapon ).Name;
		var label = AimboxAttachmentCatalog.Label( challenge.Attachment );
		return new(
			$"attachment:{challenge.Weapon}:{challenge.Attachment}",
			AimboxProgressionRewardKind.Attachment,
			0,
			challenge.Label.ToUpperInvariant(),
			"ATTACHMENT",
			unlocked
				? $"Unlocked on {weaponName}. Equip in loadouts."
				: $"{weaponName} · {AimboxWeaponProgressionSystem.UnlockRequirementText( challenge )}",
			label.Length <= 3 ? label.ToUpperInvariant() : label[..3].ToUpperInvariant(),
			"#ffb82a",
			unlocked,
			Weapon: challenge.Weapon,
			Attachment: challenge.Attachment );
	}

	static string AttachmentStatus( AimboxPlayerData data, AimboxProgressionRewardEntry entry )
	{
		if ( entry.Weapon is not { } weapon || entry.Attachment is not { } attachment )
			return "LOCKED";

		AimboxAttachmentChallenge challenge = null;
		foreach ( var candidate in AimboxMw2Catalog.AttachmentChallenges )
		{
			if ( candidate.Weapon == weapon && candidate.Attachment == attachment )
			{
				challenge = candidate;
				break;
			}
		}

		if ( challenge is null )
			return "LOCKED";

		var weaponData = data.GetWeapon( weapon );
		return $"MASTERY {weaponData.Level}/{challenge.RequiredMasteryLevel}";
	}

	static string BuildWeaponBlurb( AimboxWeaponDefinition def )
	{
		if ( def.IsMelee )
			return "Melee weapon.";

		return $"{def.MagazineSize} round mag · {(int)MathF.Round( 1f / MathF.Max( 0.05f, def.FireDelay ) )} RPM class damage profile.";
	}
}
