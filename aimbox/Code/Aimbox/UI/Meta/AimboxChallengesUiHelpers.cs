namespace Sandbox;

public enum AimboxChallengesSection
{
	Daily,
	Attachments
}

public static class AimboxChallengesUiHelpers
{
	public static int DailyTotal( AimboxGame game ) =>
		game?.Challenges.DailyChallenges.Count ?? 0;

	public static int DailyCompleted( AimboxPlayerData data, AimboxGame game )
	{
		if ( data is null || game is null )
			return 0;

		var completed = 0;
		foreach ( var challenge in game.Challenges.DailyChallenges )
		{
			if ( data.CompletedChallenges.Contains( challenge.Id ) )
				completed++;
		}

		return completed;
	}

	public static float DailyProgress( AimboxPlayerData data, AimboxGame game )
	{
		var total = DailyTotal( game );
		return total <= 0 ? 0f : DailyCompleted( data, game ) / (float)total;
	}

	public static int DailyRemainingXp( AimboxPlayerData data, AimboxGame game )
	{
		if ( game is null )
			return 0;

		var xp = 0;
		foreach ( var challenge in game.Challenges.DailyChallenges )
		{
			if ( data is null || !data.CompletedChallenges.Contains( challenge.Id ) )
				xp += challenge.RewardXp;
		}

		return xp;
	}

	public static string DailyRewardSummary( AimboxPlayerData data, AimboxGame game )
	{
		var completed = DailyCompleted( data, game );
		var total = DailyTotal( game );
		if ( total <= 0 )
			return "No daily challenges available";

		if ( completed >= total )
			return "All daily challenges complete";

		return $"{total - completed} remaining · up to +{DailyRemainingXp( data, game ):N0} XP";
	}

	public static int AttachmentUnlockedCount( AimboxPlayerData data, AimboxWeaponId weapon )
	{
		if ( data is null )
			return 0;

		var count = 0;
		foreach ( var challenge in AimboxMw2Catalog.GetChallengesForWeapon( weapon ) )
		{
			if ( IsAttachmentUnlocked( data, challenge ) )
				count++;
		}

		return count;
	}

	public static int AttachmentTotalCount( AimboxWeaponId weapon )
	{
		var count = 0;
		foreach ( var _ in AimboxMw2Catalog.GetChallengesForWeapon( weapon ) )
			count++;

		return count;
	}

	public static int AttachmentUnlockedTotal( AimboxPlayerData data )
	{
		if ( data is null )
			return 0;

		var count = 0;
		foreach ( var weapon in AimboxAttachmentCatalog.AttachmentCapableWeapons )
			count += AttachmentUnlockedCount( data, weapon );

		return count;
	}

	public static int AttachmentTotalAll()
	{
		var total = 0;
		foreach ( var weapon in AimboxAttachmentCatalog.AttachmentCapableWeapons )
			total += AttachmentTotalCount( weapon );

		return total;
	}

	public static bool IsDailyComplete( AimboxPlayerData data, AimboxChallenge challenge ) =>
		data?.CompletedChallenges.Contains( challenge.Id ) == true;

	public static int DailyProgressValue( AimboxPlayerData data, AimboxChallenge challenge )
	{
		if ( data is null || !data.ChallengeProgress.TryGetValue( challenge.Id, out var progress ) )
			return 0;

		return progress;
	}

	public static float DailyProgressPercent( AimboxPlayerData data, AimboxChallenge challenge )
	{
		if ( challenge.Target <= 0 )
			return 0f;

		var progress = DailyProgressValue( data, challenge );
		return Math.Min( progress, challenge.Target ) / (float)challenge.Target;
	}

	public static bool IsAttachmentUnlocked( AimboxPlayerData data, AimboxAttachmentChallenge challenge )
	{
		var weaponData = data?.GetWeapon( challenge.Weapon );
		return weaponData is not null
			&& AimboxUnlockService.IsAttachmentUnlocked( challenge.Weapon, weaponData, challenge.Attachment );
	}

	public static float AttachmentProgress( AimboxPlayerData data, AimboxAttachmentChallenge challenge )
	{
		var weaponData = data?.GetWeapon( challenge.Weapon );
		if ( weaponData is null || AimboxGame.Instance is null )
			return 0f;

		return AimboxGame.Instance.AttachmentUnlocks.GetAttachmentProgress( weaponData, challenge );
	}

	public static string AttachmentRequirementText( AimboxAttachmentChallenge challenge ) =>
		AimboxWeaponProgressionSystem.UnlockRequirementText( challenge );

	public static string AttachmentProgressDetail( AimboxPlayerData data, AimboxAttachmentChallenge challenge )
	{
		var weaponData = data?.GetWeapon( challenge.Weapon );
		if ( weaponData is null )
			return "Mastery Level 1";

		if ( AimboxUnlockService.BypassProgressionLocks
		     || weaponData.UnlockedAttachments.Contains( challenge.Attachment ) )
			return "Unlocked — equip in loadouts";

		return $"Mastery {weaponData.Level} / {challenge.RequiredMasteryLevel}";
	}

	public static string ProgressFill( float percent ) => $"{(int)(percent * 100f)}%";

	public static string PercentLabel( float progress ) => $"{(int)(progress * 100)}%";

	public static string AttachmentIconCode( AimboxAttachmentId attachment )
	{
		var label = AimboxAttachmentCatalog.Label( attachment );
		return label.Length <= 3 ? label.ToUpperInvariant() : label[..3].ToUpperInvariant();
	}
}
