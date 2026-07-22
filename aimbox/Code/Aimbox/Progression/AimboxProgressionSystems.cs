namespace Sandbox;

public sealed record AimboxUnlock( string Label );

static class AimboxGeometricXp
{
	public static int CumulativeToLevel( int level, float baseStep, float growthRate )
	{
		if ( level <= 0 )
			return 0;

		if ( MathF.Abs( growthRate - 1f ) < 0.001f )
			return (int)MathF.Ceiling( baseStep * level );

		var total = baseStep * (MathF.Pow( growthRate, level ) - 1f) / (growthRate - 1f);
		return Math.Max( 0, (int)MathF.Ceiling( total ) );
	}

	public static int StepToReachLevel( int level, float baseStep, float growthRate )
	{
		if ( level <= 0 )
			return 0;

		return CumulativeToLevel( level, baseStep, growthRate ) - CumulativeToLevel( level - 1, baseStep, growthRate );
	}
}

public sealed class AimboxXpSystem
{
	public const float EarnedXpMultiplier = 0.2f;
	public const float FirstSessionEarnedXpMultiplier = 1f;
	public const int FirstSessionMatchLimit = 3;

	public const int KillXp = 100;
	public const int AssistXp = 40;
	public const int HeadshotXp = 25;
	public const int MatchCompleteXp = 150;
	public const int WinXp = 250;

	public const float RankXpBaseStep = 275f;
	public const float RankXpGrowthRate = 1.102f;

	public static bool IsFirstSession( AimboxPlayerData data ) =>
		data is not null && data.MatchesPlayed < FirstSessionMatchLimit;

	public static float EarnedXpMultiplierFor( AimboxPlayerData data ) =>
		IsFirstSession( data ) ? FirstSessionEarnedXpMultiplier : EarnedXpMultiplier;

	public static int ScaleEarnedXp( int amount, AimboxPlayerData data = null ) =>
		amount <= 0 ? 0 : Math.Max( 0, (int)MathF.Round( amount * EarnedXpMultiplierFor( data ) ) );

	public List<AimboxUnlock> AddPlayerXp( AimboxPlayerData data, int amount )
	{
		var unlocks = new List<AimboxUnlock>();
		if ( data.PlayerLevel >= AimboxMw2Catalog.MaxRank )
			return unlocks;

		amount = ScaleEarnedXp( amount, data );
		if ( amount <= 0 )
			return unlocks;

		data.TotalXp += amount;

		while ( data.PlayerLevel < AimboxMw2Catalog.MaxRank && data.TotalXp >= XpForLevel( data.PlayerLevel + 1 ) )
		{
			data.PlayerLevel++;
			unlocks.Add( new AimboxUnlock( $"Rank {data.PlayerLevel}" ) );

			foreach ( var weapon in AimboxWeapons.All.Values.Where( w => w.UnlockLevel == data.PlayerLevel ) )
				unlocks.Add( new AimboxUnlock( weapon.Name ) );

			foreach ( var perk in AimboxMw2Catalog.Perks.Where( p => p.UnlockLevel == data.PlayerLevel ) )
				unlocks.Add( new AimboxUnlock( perk.Name ) );

			foreach ( var streak in AimboxMw2Catalog.Killstreaks.Where( k =>
				         k.UnlockLevel == data.PlayerLevel && AimboxMw2Catalog.IsKillstreakImplemented( k.Id ) ) )
				unlocks.Add( new AimboxUnlock( streak.Name ) );
		}

		if ( data.PlayerLevel >= AimboxMw2Catalog.MaxRank )
			data.TotalXp = Math.Min( data.TotalXp, XpForLevel( AimboxMw2Catalog.MaxRank ) );

		return unlocks;
	}

	public static int XpForLevel( int level ) =>
		AimboxGeometricXp.CumulativeToLevel( level, RankXpBaseStep, RankXpGrowthRate );

	public static int XpStepForLevel( int level ) =>
		AimboxGeometricXp.StepToReachLevel( level, RankXpBaseStep, RankXpGrowthRate );

	public static int XpToNextLevel( AimboxPlayerData data )
	{
		if ( data.PlayerLevel >= AimboxMw2Catalog.MaxRank )
			return 0;

		return XpForLevel( data.PlayerLevel + 1 ) - data.TotalXp;
	}

	public static float LevelProgress( AimboxPlayerData data )
	{
		if ( data.PlayerLevel >= AimboxMw2Catalog.MaxRank )
			return 1f;

		var currentFloor = XpForLevel( data.PlayerLevel );
		var next = XpForLevel( data.PlayerLevel + 1 );
		if ( next <= currentFloor )
			return 1f;

		return (data.TotalXp - currentFloor) / (float)(next - currentFloor);
	}
}

public sealed class AimboxWeaponProgressionSystem
{
	public const int KillMasteryXp = 100;
	public const int HeadshotMasteryBonus = 25;
	public const int MaxMasteryLevel = 10;

	public const float MasteryXpBaseStep = 155f;
	public const float MasteryXpGrowthRate = 1.48f;

	public void RecordWeaponDamage( AimboxPlayerData data, AimboxWeaponId weaponId, int damage )
	{
		data.GetWeapon( weaponId ).DamageDealt += Math.Max( 0, damage );
	}

	public List<AimboxUnlock> AddMasteryXp( AimboxPlayerData data, AimboxWeaponId weaponId, int amount, AimboxAttachmentUnlockSystem attachments )
	{
		var unlocks = new List<AimboxUnlock>();
		amount = AimboxXpSystem.ScaleEarnedXp( amount, data );
		if ( amount <= 0 || !AimboxWeapons.All.ContainsKey( weaponId ) )
			return unlocks;

		var weaponData = data.GetWeapon( weaponId );
		var weaponName = AimboxWeapons.Get( weaponId ).Name;
		weaponData.Xp += amount;

		while ( weaponData.Level < MaxMasteryLevel && weaponData.Xp >= XpForLevel( weaponData.Level + 1 ) )
		{
			weaponData.Level++;
			unlocks.Add( new AimboxUnlock( $"{weaponName} Mastery {weaponData.Level}" ) );
		}

		if ( weaponData.Level >= MaxMasteryLevel )
			weaponData.Xp = Math.Min( weaponData.Xp, XpForLevel( MaxMasteryLevel ) );

		unlocks.AddRange( attachments.EvaluateMasteryUnlocks( data, weaponId ) );

		return unlocks;
	}

	public static int XpForLevel( int level ) =>
		level <= 1
			? 0
			: AimboxGeometricXp.CumulativeToLevel( level - 1, MasteryXpBaseStep, MasteryXpGrowthRate );

	public static int XpStepForLevel( int level ) =>
		level <= 1
			? 0
			: AimboxGeometricXp.StepToReachLevel( level - 1, MasteryXpBaseStep, MasteryXpGrowthRate );

	public static int XpToNextLevel( AimboxWeaponData data )
	{
		if ( data.Level >= MaxMasteryLevel )
			return 0;

		return XpForLevel( data.Level + 1 ) - data.Xp;
	}

	public static float LevelProgress( AimboxWeaponData data )
	{
		if ( data.Level >= MaxMasteryLevel )
			return 1f;

		var currentFloor = XpForLevel( data.Level );
		var next = XpForLevel( data.Level + 1 );
		if ( next <= currentFloor )
			return 1f;

		return (data.Xp - currentFloor) / (float)(next - currentFloor);
	}

	public static string UnlockRequirementText( AimboxAttachmentChallenge challenge ) =>
		$"Mastery Level {challenge.RequiredMasteryLevel}";

	public static float AttachmentUnlockProgress( AimboxWeaponData weaponData, AimboxAttachmentChallenge challenge )
	{
		if ( AimboxUnlockService.BypassProgressionLocks )
			return 1f;

		if ( weaponData.UnlockedAttachments.Contains( challenge.Attachment ) )
			return 1f;

		if ( weaponData.Level >= challenge.RequiredMasteryLevel )
			return 1f;

		if ( challenge.RequiredMasteryLevel <= 1 )
			return 0f;

		var current = weaponData.Level - 1 + LevelProgress( weaponData );
		return (current / (challenge.RequiredMasteryLevel - 1)).Clamp( 0f, 1f );
	}
}

public sealed class AimboxChallenge
{
	public string Id { get; init; }
	public string Label { get; init; }
	public string Stat { get; init; }
	public int Target { get; init; }
	public int RewardXp { get; init; }
}

public sealed class AimboxChallengeSystem
{
	public IReadOnlyList<AimboxChallenge> DailyChallenges { get; } =
	[
		new() { Id = "daily_kills_25", Label = "Get 25 kills", Stat = "kills", Target = 25, RewardXp = 500 },
		new() { Id = "daily_wins_3", Label = "Win 3 matches", Stat = "wins", Target = 3, RewardXp = 700 },
		new() { Id = "daily_headshots_10", Label = "Get 10 headshots", Stat = "headshots", Target = 10, RewardXp = 600 },
		new() { Id = "daily_smg_15", Label = "Get 15 SMG kills", Stat = "smg_kills", Target = 15, RewardXp = 550 }
	];

	public void EnsureDailyReset( AimboxPlayerData data )
	{
		var today = DateTime.UtcNow.Date;
		if ( data.LastChallengeResetUtc.Date >= today )
			return;

		data.LastChallengeResetUtc = today;
		data.ChallengeProgress.Clear();
		data.CompletedChallenges.Clear();
		data.ClaimedRewards.Clear();
	}

	public List<AimboxChallenge> AddProgress( AimboxPlayerData data, string stat, int amount, AimboxXpSystem xp, List<AimboxUnlock> unlockSink )
	{
		EnsureDailyReset( data );
		var completed = new List<AimboxChallenge>();
		foreach ( var challenge in DailyChallenges.Where( c => c.Stat == stat ) )
		{
			if ( data.CompletedChallenges.Contains( challenge.Id ) )
				continue;

			var value = data.ChallengeProgress.GetValueOrDefault( challenge.Id ) + amount;
			data.ChallengeProgress[challenge.Id] = Math.Min( value, challenge.Target );
			if ( value >= challenge.Target )
			{
				data.CompletedChallenges.Add( challenge.Id );
				completed.Add( challenge );

				if ( data.ClaimedRewards.Add( challenge.Id ) )
					unlockSink.AddRange( xp.AddPlayerXp( data, challenge.RewardXp ) );
			}
		}

		return completed;
	}
}

public sealed class AimboxMedalSystem
{
	public List<AimboxMedalId> EvaluateKill( AimboxPlayerController attacker, IAimboxCombatActor victim, bool headshot, float distance )
	{
		var medals = new List<AimboxMedalId>();
		if ( attacker is null || victim is null )
			return medals;

		var match = AimboxGame.Instance?.Match;

		if ( match is not null && !match.FirstBloodAwarded )
		{
			match.FirstBloodAwarded = true;
			medals.Add( AimboxMedalId.FirstBlood );
		}

		if ( headshot )
			medals.Add( AimboxMedalId.Headshot );
		if ( distance >= 1800f )
			medals.Add( AimboxMedalId.Longshot );
		if ( !string.IsNullOrWhiteSpace( attacker.LastKillerAccountId )
		     && attacker.LastKillerAccountId == victim.CombatId )
			medals.Add( AimboxMedalId.Revenge );
		if ( attacker.RecentKillCount >= 2 )
			medals.Add( AimboxMedalId.DoubleKill );
		if ( attacker.RecentKillCount >= 3 )
			medals.Add( AimboxMedalId.TripleKill );
		if ( attacker.KillStreak >= 5 )
			medals.Add( AimboxMedalId.Bloodthirsty );

		return medals;
	}

	public static string FormatLabel( AimboxMedalId medal ) => medal switch
	{
		AimboxMedalId.FirstBlood => "First Blood",
		AimboxMedalId.Headshot => "Headshot",
		AimboxMedalId.Longshot => "Longshot",
		AimboxMedalId.Revenge => "Revenge",
		AimboxMedalId.DoubleKill => "Double Kill",
		AimboxMedalId.TripleKill => "Triple Kill",
		AimboxMedalId.Bloodthirsty => "Bloodthirsty",
		_ => medal.ToString()
	};
}
