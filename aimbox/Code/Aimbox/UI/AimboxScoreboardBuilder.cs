namespace Sandbox;

public readonly record struct AimboxScoreboardEntry(
	string Name,
	string SubLabel,
	int Score,
	int Kills,
	int Deaths,
	AimboxTeam Team,
	bool IsHuman,
	bool IsLocal )
{
	public float KdRatio => Deaths <= 0 ? Kills : Kills / (float)Deaths;
}

public sealed class AimboxScoreboardTeamColumn
{
	public AimboxTeam Team { get; init; }
	public string Title { get; init; }
	public int TeamScore { get; init; }
	public List<AimboxScoreboardEntry> Entries { get; init; } = [];
}

public sealed class AimboxScoreboardView
{
	public string Title { get; init; } = "Scoreboard";
	public string Subtitle { get; init; }
	public string MapLabel { get; init; } = AimboxArenaConfig.MapDisplayName;
	public string TimerLabel { get; init; } = "00:00";
	public bool IsFinal { get; set; }
	public List<AimboxScoreboardEntry> Entries { get; init; } = [];
	public List<AimboxScoreboardTeamColumn> TeamColumns { get; init; } = [];
	public bool IsTeamLayout => TeamColumns.Count > 0;
}

public static class AimboxScoreboardBuilder
{
	public static AimboxScoreboardView Build( AimboxGame game )
	{
		if ( game?.Match is { IsRunning: true } )
			return BuildLive( game );

		if ( game?.LastScoreboard is not null )
			return game.LastScoreboard;

		return new AimboxScoreboardView { Subtitle = "No active match" };
	}

	public static AimboxScoreboardView BuildLive( AimboxGame game )
	{
		if ( game?.Match is not { IsRunning: true } match )
			return new AimboxScoreboardView { Subtitle = "No active match", IsFinal = game?.Phase != AimboxSessionPhase.Playing };

		var local = FindLocalPlayer( game );
		var timer = FormatTime( match.TimeRemaining );

		return match.Mode switch
		{
			AimboxGameMode.TeamDeathmatch => BuildTdm( game, match, local, timer ),
			AimboxGameMode.Duel => BuildDuel( game, match, local, timer ),
			AimboxGameMode.Survival => BuildSurvival( game, match, local, timer ),
			AimboxGameMode.Range => BuildRange( game, match, local ),
			_ when AimboxAimModeRules.IsAimMode( match.Mode ) => BuildAim( game, match, local, timer ),
			_ => BuildFfa( game, match, local, timer )
		};
	}

	public static AimboxScoreboardView Snapshot( AimboxGame game )
	{
		var view = BuildLive( game );
		view.IsFinal = true;
		return view;
	}

	static AimboxScoreboardView BuildFfa( AimboxGame game, AimboxMatchSystem match, AimboxPlayerController local, string timer )
	{
		var entries = BuildActorEntries( game, match, local );
		return new AimboxScoreboardView
		{
			Title = "Free For All",
			Subtitle = ModeHeader( match.Mode, game ),
			TimerLabel = timer,
			MapLabel = ResolveMapLabel( game ),
			Entries = entries
		};
	}

	static AimboxScoreboardView BuildTdm( AimboxGame game, AimboxMatchSystem match, AimboxPlayerController local, string timer )
	{
		var redScore = match.GetTeamScore( AimboxTeam.Red );
		var blueScore = match.GetTeamScore( AimboxTeam.Blue );
		var entries = BuildActorEntries( game, match, local );

		return new AimboxScoreboardView
		{
			Title = "Team Deathmatch",
			Subtitle = ModeHeader( match.Mode, game ),
			TimerLabel = timer,
			MapLabel = ResolveMapLabel( game ),
			TeamColumns =
			[
				BuildTeamColumn( AimboxTeam.Red, redScore, entries ),
				BuildTeamColumn( AimboxTeam.Blue, blueScore, entries )
			]
		};
	}

	static AimboxScoreboardView BuildDuel( AimboxGame game, AimboxMatchSystem match, AimboxPlayerController local, string timer )
	{
		var source = BuildActorEntries( game, match, local );
		var entries = new List<AimboxScoreboardEntry>();
		foreach ( var entry in source )
			entries.Add( entry with { Score = entry.Kills } );

		SortByScoreDescending( entries );

		return new AimboxScoreboardView
		{
			Title = "Duel",
			Subtitle = ModeHeader( match.Mode, game ),
			TimerLabel = timer,
			MapLabel = ResolveMapLabel( game ),
			Entries = entries
		};
	}

	static AimboxScoreboardView BuildSurvival( AimboxGame game, AimboxMatchSystem match, AimboxPlayerController local, string timer )
	{
		var waveLabel = match.SurvivalHardMode ? $"Hard wave · {match.SurvivalWaveBotTarget} enemies" : $"Wave {match.SurvivalWave}";
		var statusLabel = match.SurvivalComplete
			? "All waves cleared"
			: match.SurvivalFailed
				? $"Eliminated on {waveLabel}"
				: waveLabel;
		var entries = new List<AimboxScoreboardEntry>();
		foreach ( var player in game.Players )
		{
			if ( player.IsProxy )
				continue;

			entries.Add( CreateEntry(
				DisplayName( player ),
				match,
				player.AccountId,
				player.Team,
				true,
				player == local ) );
		}

		SortByScoreDescending( entries );

		return new AimboxScoreboardView
		{
			Title = "Survival",
			Subtitle = $"{ModeHeader( match.Mode, game )} · {statusLabel}",
			TimerLabel = timer,
			MapLabel = ResolveMapLabel( game ),
			Entries = entries
		};
	}

	static AimboxScoreboardView BuildRange( AimboxGame game, AimboxMatchSystem match, AimboxPlayerController local )
	{
		var entries = new List<AimboxScoreboardEntry>();
		foreach ( var player in game.Players )
		{
			if ( player.IsProxy )
				continue;

			entries.Add( new AimboxScoreboardEntry(
				DisplayName( player ),
				"Practice dummies",
				player.Data?.PracticeKills ?? 0,
				player.Data?.PracticeKills ?? 0,
				0,
				AimboxTeam.None,
				true,
				player == local ) );
		}

		SortByScoreDescending( entries );

		return new AimboxScoreboardView
		{
			Title = "Range",
			Subtitle = $"{ModeHeader( match.Mode, game )} · {AimboxRangeDummySpawner.DummyCount} passive targets",
			TimerLabel = "∞",
			MapLabel = ResolveMapLabel( game ),
			Entries = entries
		};
	}

	static AimboxScoreboardView BuildAim( AimboxGame game, AimboxMatchSystem match, AimboxPlayerController local, string timer )
	{
		var entries = new List<AimboxScoreboardEntry>();
		foreach ( var player in game.Players )
		{
			if ( player.IsProxy )
				continue;

			var score = match.GetAimScore( player.AccountId );
			entries.Add( new AimboxScoreboardEntry(
				DisplayName( player ),
				AimboxAimDrillLabels.Short( match.ActiveAimDrill ),
				score,
				score,
				0,
				AimboxTeam.None,
				true,
				player == local ) );
		}

		SortByScoreDescending( entries );

		return new AimboxScoreboardView
		{
			Title = "Aim",
			Subtitle = $"{ModeHeader( match.Mode, game )} · {AimboxAimDrillLabels.Long( match.ActiveAimDrill )}",
			TimerLabel = timer,
			MapLabel = ResolveMapLabel( game ),
			Entries = entries
		};
	}

	static AimboxScoreboardTeamColumn BuildTeamColumn(
		AimboxTeam team,
		int teamScore,
		IReadOnlyList<AimboxScoreboardEntry> entries )
	{
		var teamEntries = new List<AimboxScoreboardEntry>();
		foreach ( var entry in entries )
		{
			if ( entry.Team == team )
				teamEntries.Add( entry );
		}

		SortByScoreDescending( teamEntries );

		return new AimboxScoreboardTeamColumn
		{
			Team = team,
			Title = AimboxScoreboardUiHelpers.TeamLabel( team ),
			TeamScore = teamScore,
			Entries = teamEntries
		};
	}

	static List<AimboxScoreboardEntry> BuildActorEntries( AimboxGame game, AimboxMatchSystem match, AimboxPlayerController local )
	{
		var entries = new List<AimboxScoreboardEntry>();

		foreach ( var player in game.Players )
		{
			entries.Add( CreateEntry(
				player.IsProxy ? player.AccountId : DisplayName( player ),
				match,
				player.AccountId,
				player.Team,
				true,
				player == local ) );
		}

		foreach ( var bot in game.Bots )
		{
			entries.Add( CreateEntry(
				bot.DisplayName,
				match,
				bot.BotId,
				bot.Team,
				false,
				false ) );
		}

		SortByScoreDescending( entries );
		return entries;
	}

	static AimboxScoreboardEntry CreateEntry(
		string name,
		AimboxMatchSystem match,
		string combatId,
		AimboxTeam team,
		bool isHuman,
		bool isLocal )
	{
		var kills = match.PlayerKills.TryGetValue( combatId, out var killCount ) ? killCount : 0;
		var deaths = match.PlayerDeaths.TryGetValue( combatId, out var deathCount ) ? deathCount : 0;
		return new AimboxScoreboardEntry(
			name,
			$"{kills}K / {deaths}D",
			match.GetScore( combatId ),
			kills,
			deaths,
			team,
			isHuman,
			isLocal );
	}

	static string DisplayName( AimboxPlayerController player )
	{
		var data = player.Data;
		if ( data is not null
		     && data.ActiveLoadoutIndex >= 0
		     && data.ActiveLoadoutIndex < data.Loadouts.Count )
			return data.Loadouts[data.ActiveLoadoutIndex].Name;

		return player.AccountId;
	}

	static AimboxPlayerController FindLocalPlayer( AimboxGame game )
	{
		foreach ( var player in game.Players )
		{
			if ( !player.IsProxy )
				return player;
		}

		return null;
	}

	static void SortByScoreDescending( List<AimboxScoreboardEntry> entries )
	{
		entries.Sort( ( a, b ) =>
		{
			var scoreCompare = b.Score.CompareTo( a.Score );
			return scoreCompare != 0 ? scoreCompare : string.Compare( a.Name, b.Name, StringComparison.Ordinal );
		} );
	}

	static string ResolveMapLabel( AimboxGame game )
	{
		if ( game?.Match.Mode is { } mode && AimboxAimModeRules.IsAimMode( mode ) )
			return "AIM TRAINER";

		return game is null
			? AimboxArenaConfig.MapDisplayName
			: AimboxPlayLobbyUiHelpers.MapDisplayName( game.Lobby.SelectedMapId );
	}

	static string ModeHeader( AimboxGameMode mode, AimboxGame game ) =>
		$"{AimboxGameModeLabels.Long( mode ).ToUpperInvariant()} // {ResolveMapLabel( game )}";

	static string FormatTime( float seconds )
	{
		var total = Math.Max( 0, (int)seconds );
		return $"{total / 60:00}:{total % 60:00}";
	}
}
