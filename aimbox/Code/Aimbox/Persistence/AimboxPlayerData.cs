using System.Text.Json.Serialization;

namespace Sandbox;

public sealed class AimboxPlayerData
{
	public const int CurrentProgressionVersion = 13;

	public string AccountId { get; set; } = "offline";
	public int ProgressionVersion { get; set; } = CurrentProgressionVersion;
	public int PlayerLevel { get; set; }
	public int TotalXp { get; set; }
	public int ActiveLoadoutIndex { get; set; }
	public DateTime LastChallengeResetUtc { get; set; } = DateTime.MinValue;
	public int MatchesPlayed { get; set; }
	public int Wins { get; set; }
	public int Losses { get; set; }
	public int Kills { get; set; }
	public int PracticeKills { get; set; }
	public int Deaths { get; set; }
	public int Assists { get; set; }
	public int Headshots { get; set; }
	public int ShotsFired { get; set; }
	public int ShotsHit { get; set; }
	public int LongestKillStreak { get; set; }
	public float TimePlayed { get; set; }
	public RankedData Ranked { get; set; } = new();
	public List<AimboxLoadoutData> Loadouts { get; set; } = AimboxLoadoutData.CreateDefaultSet();
	public Dictionary<AimboxWeaponId, AimboxWeaponData> Weapons { get; set; } = new();
	public Dictionary<string, int> ChallengeProgress { get; set; } = new();
	public HashSet<string> CompletedChallenges { get; set; } = [];
	public HashSet<string> ClaimedRewards { get; set; } = [];
	public HashSet<string> CosmeticUnlocks { get; set; } = [];
	public Dictionary<AimboxGameMode, int> AimModeBestScores { get; set; } = new();

	[JsonIgnore] public float Accuracy => ShotsFired <= 0 ? 0f : ShotsHit / (float)ShotsFired;
	[JsonIgnore] public float KdRatio => Deaths <= 0 ? Kills : Kills / (float)Deaths;

	public AimboxWeaponData GetWeapon( AimboxWeaponId id )
	{
		if ( Weapons.TryGetValue( id, out var data ) )
			return data;

		data = new AimboxWeaponData { Weapon = id };
		Weapons[id] = data;
		return data;
	}

	public void Validate()
	{
		TotalXp = Math.Max( 0, TotalXp );
		SyncPlayerLevelFromXp();
		PlayerLevel = Math.Clamp( PlayerLevel, 0, AimboxMw2Catalog.MaxRank );
		foreach ( var weapon in AimboxWeapons.All.Keys )
		{
			var weaponData = GetWeapon( weapon );
			weaponData.Validate();
		}

		ActiveLoadoutIndex = Math.Clamp( ActiveLoadoutIndex, 0, AimboxMw2Catalog.LoadoutCount - 1 );
		while ( Loadouts.Count < AimboxMw2Catalog.LoadoutCount )
			Loadouts.Add( FreshLoadout( Loadouts.Count + 1 ) );

		foreach ( var loadout in Loadouts )
		{
			if ( loadout.PrimaryWeapon == AimboxWeaponId.Bow )
				loadout.PrimaryWeapon = AimboxWeaponId.Usp;
		}

		if ( Loadouts.Count > AimboxMw2Catalog.LoadoutCount )
			Loadouts.RemoveRange( AimboxMw2Catalog.LoadoutCount, Loadouts.Count - AimboxMw2Catalog.LoadoutCount );

		AimModeBestScores ??= new Dictionary<AimboxGameMode, int>();
		var sanitizedAimScores = new Dictionary<AimboxGameMode, int>();
		foreach ( var (mode, score) in AimModeBestScores )
		{
			if ( !AimboxAimModeRules.IsAimMode( mode ) || score <= 0 )
				continue;

			sanitizedAimScores[mode] = score;
		}

		AimModeBestScores = sanitizedAimScores;
	}

	public static AimboxPlayerData CreateFreshStart( string accountId )
	{
		var data = new AimboxPlayerData
		{
			AccountId = accountId,
			ProgressionVersion = CurrentProgressionVersion,
			PlayerLevel = 0,
			TotalXp = 0,
			ActiveLoadoutIndex = 0,
			Loadouts = Enumerable.Range( 1, AimboxMw2Catalog.LoadoutCount ).Select( FreshLoadout ).ToList(),
			Weapons = new Dictionary<AimboxWeaponId, AimboxWeaponData>(),
			ChallengeProgress = new Dictionary<string, int>(),
			CompletedChallenges = [],
			ClaimedRewards = [],
			CosmeticUnlocks = [],
			Ranked = new RankedData()
		};
		ResetWeaponProgression( data );
		data.Validate();
		return data;
	}

	public static void ResetWeaponProgression( AimboxPlayerData data )
	{
		data.Weapons.Clear();
		foreach ( var weapon in AimboxWeapons.All.Keys )
		{
			data.Weapons[weapon] = new AimboxWeaponData
			{
				Weapon = weapon,
				Level = 1,
				Xp = 0,
				Kills = 0,
				Headshots = 0,
				DamageDealt = 0,
				UnlockedAttachments = [],
				EquippedAttachments = []
			};
		}
	}

	public static void ResetXpProgression( AimboxPlayerData data )
	{
		data.TotalXp = 0;
		data.PlayerLevel = 0;
		ResetWeaponProgression( data );
		data.Validate();
	}

	void SyncPlayerLevelFromXp()
	{
		var level = 0;
		while ( level < AimboxMw2Catalog.MaxRank && TotalXp >= AimboxXpSystem.XpForLevel( level + 1 ) )
			level++;

		PlayerLevel = level;
	}

	static AimboxLoadoutData FreshLoadout( int index ) => new()
	{
		Name = $"Custom {index}",
		PrimaryWeapon = AimboxWeaponId.M4A1,
		SecondaryWeapon = AimboxWeaponId.Usp,
		Perk1 = AimboxPerkId.None,
		Perk2 = AimboxPerkId.None,
		Perk3 = AimboxPerkId.None,
		Killstreak1 = AimboxKillstreakId.None,
		Killstreak2 = AimboxKillstreakId.None,
		Killstreak3 = AimboxKillstreakId.None,
		Attachments = new Dictionary<AimboxWeaponId, List<AimboxAttachmentId>>()
	};
}

public sealed class AimboxWeaponData
{
	public AimboxWeaponId Weapon { get; set; }
	public int Xp { get; set; }
	public int Level { get; set; } = 1;
	public int Kills { get; set; }
	public int Headshots { get; set; }
	public int DamageDealt { get; set; }
	public HashSet<AimboxAttachmentId> UnlockedAttachments { get; set; } = [];
	public HashSet<AimboxAttachmentId> EquippedAttachments { get; set; } = [];

	public void Validate()
	{
		Xp = Math.Max( 0, Xp );
		Level = 1;
		while ( Level < AimboxWeaponProgressionSystem.MaxMasteryLevel
		        && Xp >= AimboxWeaponProgressionSystem.XpForLevel( Level + 1 ) )
			Level++;

		Level = Math.Clamp( Level, 1, AimboxWeaponProgressionSystem.MaxMasteryLevel );
		if ( Level >= AimboxWeaponProgressionSystem.MaxMasteryLevel )
			Xp = Math.Min( Xp, AimboxWeaponProgressionSystem.XpForLevel( AimboxWeaponProgressionSystem.MaxMasteryLevel ) );
	}
}

public sealed class AimboxLoadoutData
{
	public string Name { get; set; } = "Custom 1";
	public AimboxWeaponId PrimaryWeapon { get; set; } = AimboxWeaponId.M4A1;
	public AimboxWeaponId SecondaryWeapon { get; set; } = AimboxWeaponId.Usp;
	public string LethalGrenade { get; set; } = "Frag";
	public string TacticalGrenade { get; set; } = "Flash";
	public AimboxPerkId Perk1 { get; set; } = AimboxPerkId.None;
	public AimboxPerkId Perk2 { get; set; } = AimboxPerkId.None;
	public AimboxPerkId Perk3 { get; set; } = AimboxPerkId.None;
	public AimboxKillstreakId Killstreak1 { get; set; } = AimboxKillstreakId.None;
	public AimboxKillstreakId Killstreak2 { get; set; } = AimboxKillstreakId.None;
	public AimboxKillstreakId Killstreak3 { get; set; } = AimboxKillstreakId.None;
	public Dictionary<AimboxWeaponId, List<AimboxAttachmentId>> Attachments { get; set; } = new();

	public static AimboxLoadoutData Default( int index = 1 ) => new() { Name = $"Custom {index}" };

	public static List<AimboxLoadoutData> CreateDefaultSet()
	{
		var loadouts = new List<AimboxLoadoutData>();
		for ( var i = 1; i <= AimboxMw2Catalog.LoadoutCount; i++ )
			loadouts.Add( Default( i ) );
		return loadouts;
	}
}

public sealed class RankedData
{
	public int DuelMmr { get; set; } = 1000;
	public AimboxRankTier Tier { get; set; } = AimboxRankTier.Bronze;
	public int DuelWins { get; set; }
	public int DuelLosses { get; set; }
	public int WinStreak { get; set; }
	public List<string> SeasonalHistory { get; set; } = [];
}
