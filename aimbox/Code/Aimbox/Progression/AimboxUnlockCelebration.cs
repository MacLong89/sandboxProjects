namespace Sandbox;

public enum AimboxUnlockCelebrationKind
{
	None,
	RankUp,
	WeaponUnlock,
	AttachmentUnlock,
	MasteryUp,
	PerkUnlock,
	KillstreakUnlock
}

public sealed record AimboxUnlockCelebrationMoment(
	AimboxUnlockCelebrationKind Kind,
	string Kicker,
	string Title,
	string Detail,
	IReadOnlyList<string> Extras );

public static class AimboxUnlockCelebration
{
	public static AimboxUnlockCelebrationMoment Resolve(
		IReadOnlyList<AimboxUnlock> unlocks,
		int playerLevel,
		string xpDetail )
	{
		if ( unlocks.Count == 0 )
			return null;

		var rankUp = unlocks.FirstOrDefault( AimboxUnlockLabels.IsRankUp );
		var weapons = unlocks.Where( AimboxUnlockLabels.IsWeaponUnlock ).ToList();
		var attachments = unlocks.Where( AimboxUnlockLabels.IsAttachmentUnlock ).ToList();
		var mastery = unlocks.Where( AimboxUnlockLabels.IsMasteryUnlock ).ToList();
		var perks = unlocks.Where( AimboxUnlockLabels.IsPerkUnlock ).ToList();
		var streaks = unlocks.Where( AimboxUnlockLabels.IsKillstreakUnlock ).ToList();

		if ( rankUp is not null )
		{
			var extras = CollectExtras( weapons, attachments, mastery, perks, streaks );
			return new(
				AimboxUnlockCelebrationKind.RankUp,
				"RANK UP",
				$"Rank {playerLevel:D2}",
				xpDetail,
				extras );
		}

		if ( weapons.Count > 0 )
		{
			var extras = CollectExtras( [], attachments, mastery, perks, streaks );
			var title = weapons.Count > 1
				? string.Join( " · ", weapons.Select( w => w.Label ) )
				: weapons[0].Label;
			return new(
				AimboxUnlockCelebrationKind.WeaponUnlock,
				weapons.Count > 1 ? "NEW WEAPONS" : "NEW WEAPON",
				title,
				$"Equip in Loadouts · {xpDetail}",
				extras );
		}

		if ( attachments.Count > 0 )
		{
			var attachment = attachments[^1];
			var (weaponName, attachmentName) = AimboxUnlockLabels.SplitAttachmentLabel( attachment.Label );
			var extras = CollectExtras( [], [], mastery, perks, streaks );
			return new(
				AimboxUnlockCelebrationKind.AttachmentUnlock,
				attachments.Count > 1 ? "ATTACHMENTS UNLOCKED" : "ATTACHMENT UNLOCKED",
				attachmentName,
				string.IsNullOrWhiteSpace( weaponName ) ? xpDetail : $"{weaponName} · {xpDetail}",
				extras );
		}

		if ( mastery.Count > 0 )
		{
			var entry = mastery[^1];
			var (weaponName, levelText) = AimboxUnlockLabels.SplitMasteryLabel( entry.Label );
			return new(
				AimboxUnlockCelebrationKind.MasteryUp,
				"MASTERY UP",
				weaponName,
				string.IsNullOrWhiteSpace( levelText ) ? xpDetail : $"{levelText} · {xpDetail}",
				[] );
		}

		if ( perks.Count > 0 )
		{
			var perk = perks[^1];
			return new(
				AimboxUnlockCelebrationKind.PerkUnlock,
				perks.Count > 1 ? "PERKS UNLOCKED" : "PERK UNLOCKED",
				perk.Label,
				xpDetail,
				perks.Count > 1 ? perks.Select( p => p.Label ).ToList() : [] );
		}

		if ( streaks.Count > 0 )
		{
			var streak = streaks[^1];
			return new(
				AimboxUnlockCelebrationKind.KillstreakUnlock,
				streaks.Count > 1 ? "KILLSTREAKS UNLOCKED" : "KILLSTREAK UNLOCKED",
				streak.Label,
				xpDetail,
				streaks.Count > 1 ? streaks.Select( s => s.Label ).ToList() : [] );
		}

		return null;
	}

	static List<string> CollectExtras(
		IReadOnlyList<AimboxUnlock> weapons,
		IReadOnlyList<AimboxUnlock> attachments,
		IReadOnlyList<AimboxUnlock> mastery,
		IReadOnlyList<AimboxUnlock> perks,
		IReadOnlyList<AimboxUnlock> streaks )
	{
		var extras = new List<string>();
		extras.AddRange( weapons.Select( w => w.Label ) );
		extras.AddRange( attachments.Select( a => AimboxUnlockLabels.SplitAttachmentLabel( a.Label ).AttachmentName ) );
		extras.AddRange( mastery.Select( m => m.Label ) );
		extras.AddRange( perks.Select( p => p.Label ) );
		extras.AddRange( streaks.Select( s => s.Label ) );
		return extras;
	}
}

public static class AimboxUnlockLabels
{
	static readonly HashSet<string> WeaponNames = AimboxWeapons.All.Values.Select( w => w.Name ).ToHashSet();
	static readonly HashSet<string> PerkNames = AimboxMw2Catalog.Perks.Select( p => p.Name ).ToHashSet();
	static readonly HashSet<string> KillstreakNames = AimboxMw2Catalog.Killstreaks.Select( k => k.Name ).ToHashSet();

	public static bool IsRankUp( AimboxUnlock unlock ) =>
		unlock.Label.StartsWith( "Rank " );

	public static bool IsMasteryUnlock( AimboxUnlock unlock ) =>
		unlock.Label.Contains( " Mastery " );

	public static bool IsAttachmentUnlock( AimboxUnlock unlock ) =>
		unlock.Label.Contains( ':' );

	public static bool IsWeaponUnlock( AimboxUnlock unlock ) =>
		!IsRankUp( unlock ) && !IsMasteryUnlock( unlock ) && !IsAttachmentUnlock( unlock )
		                        && WeaponNames.Contains( unlock.Label );

	public static bool IsPerkUnlock( AimboxUnlock unlock ) =>
		!IsRankUp( unlock ) && !IsMasteryUnlock( unlock ) && !IsAttachmentUnlock( unlock )
		                        && PerkNames.Contains( unlock.Label );

	public static bool IsKillstreakUnlock( AimboxUnlock unlock ) =>
		!IsRankUp( unlock ) && !IsMasteryUnlock( unlock ) && !IsAttachmentUnlock( unlock )
		                        && KillstreakNames.Contains( unlock.Label );

	public static (string WeaponName, string AttachmentName) SplitAttachmentLabel( string label )
	{
		var split = label.Split( ':', 2 );
		return split.Length == 2
			? (split[0].Trim(), split[1].Trim())
			: ("", label.Trim());
	}

	public static (string WeaponName, string LevelText) SplitMasteryLabel( string label )
	{
		var split = label.Split( " Mastery ", 2 );
		return split.Length == 2
			? (split[0].Trim(), $"Level {split[1].Trim()}")
			: (label.Trim(), "");
	}
}
