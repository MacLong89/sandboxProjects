namespace Sandbox;

public sealed class AimboxMatchSystem
{
	public AimboxGameMode Mode { get; private set; } = AimboxGameMode.FreeForAll;
	public float MatchLengthSeconds { get; private set; } = AimboxArenaConfig.MatchDurationSeconds;
	public int ScoreLimit { get; private set; } = AimboxArenaConfig.FfaScoreLimit;
	public TimeSince StartedAt { get; private set; }
	public bool IsClockRunning { get; private set; }
	public bool IsRunning { get; private set; }
	public bool FirstBloodAwarded { get; set; }

	public int SurvivalWave { get; private set; } = 1;
	public int SurvivalWaveBotTarget { get; private set; } = AimboxArenaConfig.SurvivalWave1BotCount;
	public bool SurvivalHardMode { get; private set; }
	public bool SurvivalComplete { get; private set; }
	public bool SurvivalFailed { get; private set; }

	public int DuelRound { get; private set; } = 1;
	public Dictionary<string, int> DuelRoundWins { get; } = new();

	public AimboxAimDrill ActiveAimDrill { get; private set; }

	public void SetActiveAimDrill( AimboxAimDrill level ) => ActiveAimDrill = level;

	public Dictionary<AimboxTeam, int> TeamScores { get; } = new();
	public Dictionary<string, int> PlayerScores { get; } = new();
	public Dictionary<string, int> PlayerAimScores { get; } = new();
	public Dictionary<string, int> PlayerKills { get; } = new();
	public Dictionary<string, int> PlayerDeaths { get; } = new();

	bool _survivalWaveAdvancePending;

	public float TimeRemaining => !IsClockRunning
		? MatchLengthSeconds
		: MathF.Max( 0f, MatchLengthSeconds - StartedAt.Relative );

	public int GetScore( string combatId ) => PlayerScores.GetValueOrDefault( combatId );

	public int GetAimScore( string combatId ) => PlayerAimScores.GetValueOrDefault( combatId );

	public void RegisterAimScore( string combatId, int points )
	{
		if ( !IsRunning || !AimboxAimModeRules.IsAimMode( Mode ) || points <= 0 )
			return;

		PlayerAimScores[combatId] = PlayerAimScores.GetValueOrDefault( combatId ) + points;
		PlayerScores[combatId] = PlayerAimScores[combatId];
	}

	public int GetTeamScore( AimboxTeam team ) => TeamScores.GetValueOrDefault( team );

	public void Start( AimboxGameMode mode )
	{
		Mode = mode;
		ScoreLimit = mode switch
		{
			AimboxGameMode.TeamDeathmatch => AimboxArenaConfig.TdmTeamScoreLimit,
			AimboxGameMode.Duel => AimboxArenaConfig.DuelKillLimit,
			AimboxGameMode.Survival => 0,
			AimboxGameMode.Range => 0,
			_ when AimboxAimModeRules.IsAimMode( mode ) => 0,
			_ => AimboxArenaConfig.FfaScoreLimit
		};

		MatchLengthSeconds = mode switch
		{
			AimboxGameMode.Range => float.MaxValue * 0.5f,
			_ when AimboxAimModeRules.IsAimMode( mode ) => AimboxArenaConfig.AimMatchDurationSeconds,
			_ => AimboxArenaConfig.MatchDurationSeconds
		};

		ActiveAimDrill = AimboxAimModeRules.IsAimMode( mode )
			? AimboxAimModeRules.ToDrill( mode )
			: default;

		StartedAt = 0;
		IsClockRunning = false;
		FirstBloodAwarded = false;
		IsRunning = true;
		DuelRound = 1;
		SurvivalWave = 1;
		SurvivalWaveBotTarget = AimboxArenaConfig.GetSurvivalWaveBotCount( 1 );
		SurvivalHardMode = false;
		SurvivalComplete = false;
		SurvivalFailed = false;
		_survivalWaveAdvancePending = false;
		DuelRoundWins.Clear();
		TeamScores.Clear();
		PlayerScores.Clear();
		PlayerAimScores.Clear();
		PlayerKills.Clear();
		PlayerDeaths.Clear();
		AimboxGame.Instance?.KillFeed.Clear();
	}

	public void RegisterKill(
		IAimboxCombatActor attacker,
		IAimboxCombatActor victim,
		AimboxWeaponId weapon = AimboxWeaponId.Usp,
		bool headshot = false )
	{
		RegisterKillScores( attacker, victim );
		AimboxGame.Instance?.KillFeed.Record( attacker, victim, weapon, headshot );
	}

	public void RegisterKillScores( IAimboxCombatActor attacker, IAimboxCombatActor victim )
	{
		if ( !IsRunning || attacker is null || victim is null )
			return;

		var points = AimboxArenaConfig.PointsPerKill;
		PlayerScores[attacker.CombatId] = PlayerScores.GetValueOrDefault( attacker.CombatId ) + points;
		PlayerKills[attacker.CombatId] = PlayerKills.GetValueOrDefault( attacker.CombatId ) + 1;
		PlayerDeaths[victim.CombatId] = PlayerDeaths.GetValueOrDefault( victim.CombatId ) + 1;

		if ( Mode == AimboxGameMode.TeamDeathmatch )
			TeamScores[attacker.Team] = TeamScores.GetValueOrDefault( attacker.Team ) + points;

		if ( Mode == AimboxGameMode.Duel )
		{
			DuelRound++;
			DuelRoundWins[attacker.CombatId] = DuelRoundWins.GetValueOrDefault( attacker.CombatId ) + 1;
			AimboxGame.Instance?.OnDuelRoundEnded( attacker, victim );
		}
	}

	public void StartClock()
	{
		if ( IsClockRunning )
			return;

		StartedAt = 0;
		IsClockRunning = true;
	}

	public void StopClock() => IsClockRunning = false;

	public void RegisterKill( AimboxPlayerController attacker, AimboxPlayerController victim ) =>
		RegisterKill( attacker as IAimboxCombatActor, victim as IAimboxCombatActor );

	public bool ConsumeSurvivalWaveAdvance()
	{
		if ( !_survivalWaveAdvancePending )
			return false;

		_survivalWaveAdvancePending = false;
		return true;
	}

	public void NotifySurvivalWaveCleared()
	{
		if ( !IsRunning || Mode != AimboxGameMode.Survival || SurvivalComplete || SurvivalFailed )
			return;

		SurvivalWave++;
		SurvivalWaveBotTarget = AimboxArenaConfig.GetSurvivalWaveBotCount( SurvivalWave );
		SurvivalHardMode = SurvivalWave >= AimboxArenaConfig.SurvivalHardModeStartWave;
		_survivalWaveAdvancePending = true;
	}

	public void NotifySurvivalFailed()
	{
		if ( !IsRunning || Mode != AimboxGameMode.Survival || SurvivalComplete || SurvivalFailed )
			return;

		SurvivalFailed = true;
		_survivalWaveAdvancePending = false;
	}

	public bool ShouldEnd()
	{
		if ( !IsRunning )
			return false;

		if ( Mode == AimboxGameMode.Range )
			return false;

		if ( AimboxAimModeRules.IsAimMode( Mode ) )
			return TimeRemaining <= 0;

		if ( Mode == AimboxGameMode.Survival )
			return SurvivalComplete || SurvivalFailed || TimeRemaining <= 0;

		if ( TimeRemaining <= 0 )
			return true;

		if ( Mode == AimboxGameMode.TeamDeathmatch )
			return TeamScores.Values.Any( score => score >= ScoreLimit );

		if ( Mode == AimboxGameMode.Duel )
			return PlayerKills.Values.Any( kills => kills >= AimboxArenaConfig.DuelKillLimit );

		return PlayerScores.Values.Any( score => score >= ScoreLimit );
	}

	public IReadOnlyList<string> Finish( IReadOnlyList<AimboxPlayerController> players )
	{
		IsRunning = false;
		return ResolveWinners( players );
	}

	List<string> ResolveWinners( IReadOnlyList<AimboxPlayerController> players )
	{
		var humans = players.Where( p => !p.IsProxy ).ToList();
		if ( humans.Count == 0 )
			return [];

		if ( Mode == AimboxGameMode.Survival )
		{
			if ( !SurvivalComplete )
				return [];

			return humans.Select( p => p.AccountId ).ToList();
		}

		if ( Mode == AimboxGameMode.Range )
			return humans.Select( p => p.AccountId ).ToList();

		if ( AimboxAimModeRules.IsAimMode( Mode ) )
		{
			var best = PlayerAimScores.Values.DefaultIfEmpty().Max();
			var aimTopIds = PlayerAimScores.Where( x => x.Value == best ).Select( x => x.Key ).ToHashSet();
			return humans.Where( p => aimTopIds.Contains( p.AccountId ) ).Select( p => p.AccountId ).ToList();
		}

		if ( Mode == AimboxGameMode.TeamDeathmatch )
		{
			var bestTeam = TeamScores.OrderByDescending( x => x.Value ).ThenBy( x => x.Key ).Select( x => x.Key ).FirstOrDefault();
			if ( TimeRemaining <= 0 && TeamScores.Count > 0 )
				bestTeam = TeamScores.OrderByDescending( x => x.Value ).ThenBy( x => x.Key ).First().Key;

			return humans.Where( p => p.Team == bestTeam ).Select( p => p.AccountId ).ToList();
		}

		if ( Mode == AimboxGameMode.Duel )
		{
			var bestKills = PlayerKills.Values.DefaultIfEmpty().Max();
			var winnerIds = PlayerKills.Where( x => x.Value == bestKills ).Select( x => x.Key ).ToList();
			return humans.Where( p => winnerIds.Contains( p.AccountId ) ).Select( p => p.AccountId ).ToList();
		}

		var bestFfa = PlayerScores.Values.DefaultIfEmpty().Max();
		var topIds = PlayerScores.Where( x => x.Value == bestFfa ).Select( x => x.Key ).ToHashSet();
		return humans.Where( p => topIds.Contains( p.AccountId ) ).Select( p => p.AccountId ).ToList();
	}
}

public sealed class AimboxRankedSystem
{
	public void ApplyDuelResult( AimboxPlayerData winner, AimboxPlayerData loser )
	{
		var delta = CalculateDelta( winner.Ranked.DuelMmr, loser.Ranked.DuelMmr );
		winner.Ranked.DuelMmr += delta;
		winner.Ranked.DuelWins++;
		winner.Ranked.WinStreak++;

		loser.Ranked.DuelMmr = Math.Max( 100, loser.Ranked.DuelMmr - delta );
		loser.Ranked.DuelLosses++;
		loser.Ranked.WinStreak = 0;

		winner.Ranked.Tier = TierForMmr( winner.Ranked.DuelMmr );
		loser.Ranked.Tier = TierForMmr( loser.Ranked.DuelMmr );
	}

	static int CalculateDelta( int winnerMmr, int loserMmr )
	{
		var expected = 1f / (1f + MathF.Pow( 10f, (loserMmr - winnerMmr) / 400f ));
		return Math.Clamp( (int)MathF.Round( 32f * (1f - expected) ), 8, 32 );
	}

	static AimboxRankTier TierForMmr( int mmr ) => mmr switch
	{
		>= 2400 => AimboxRankTier.Master,
		>= 2000 => AimboxRankTier.Diamond,
		>= 1700 => AimboxRankTier.Platinum,
		>= 1400 => AimboxRankTier.Gold,
		>= 1100 => AimboxRankTier.Silver,
		_ => AimboxRankTier.Bronze
	};
}

public sealed class AimboxLeaderboardSystem
{
	public IReadOnlyList<AimboxPlayerData> TopKills( IEnumerable<AimboxPlayerData> players, int count = 10 ) =>
		players.OrderByDescending( x => x.Kills ).Take( count ).ToList();

	public IReadOnlyList<AimboxPlayerData> TopKd( IEnumerable<AimboxPlayerData> players, int count = 10 ) =>
		players.OrderByDescending( x => x.KdRatio ).Take( count ).ToList();

	public IReadOnlyList<AimboxPlayerData> TopHeadshots( IEnumerable<AimboxPlayerData> players, int count = 10 ) =>
		players.OrderByDescending( x => x.Headshots ).Take( count ).ToList();

	public IReadOnlyList<AimboxPlayerData> TopDuelRating( IEnumerable<AimboxPlayerData> players, int count = 10 ) =>
		players.OrderByDescending( x => x.Ranked.DuelMmr ).Take( count ).ToList();
}
