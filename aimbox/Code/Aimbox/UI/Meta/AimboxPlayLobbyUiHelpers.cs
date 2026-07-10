namespace Sandbox;

public sealed record LobbyMapDefinition( string Id, string Name, bool Playable, string AccentColor );

public sealed record LobbyModeCard(
	string Id,
	AimboxGameMode? Mode,
	string Title,
	string Description,
	string PlayerLabel,
	bool Available );

public static class AimboxPlayLobbyUiHelpers
{
	public static string DefaultMapId => AimboxMapCatalog.All[0].Id;

	public static readonly IReadOnlyList<LobbyMapDefinition> Maps =
		AimboxMapCatalog.All
			.Select( m => new LobbyMapDefinition( m.Id, m.DisplayName, true, m.AccentColor ) )
			.ToList();

	public static readonly IReadOnlyList<LobbyModeCard> AimModeCards =
	[
		new( "aim1", AimboxGameMode.AimLevel1, "GRID", "Three spheres — shoot and respawn.", "1:00", true ),
		new( "aim2", AimboxGameMode.AimLevel2, "FLICK", "One sphere — flick and respawn.", "1:00", true ),
		new( "aim3", AimboxGameMode.AimLevel3, "TRACK", "Bouncing sphere — five hits to clear.", "1:00", true )
	];

	public static readonly IReadOnlyList<LobbyModeCard> AimModeCardsRow2 =
	[
		new( "aim4", AimboxGameMode.AimLevel4, "mGRID", "Three smaller spheres — same grid drill.", "1:00", true ),
		new( "aim5", AimboxGameMode.AimLevel5, "mFLICK", "One smaller sphere — same flick drill.", "1:00", true ),
		new( "aim6", AimboxGameMode.AimLevel6, "mTRACK", "Smaller bouncing sphere — same track drill.", "1:00", true )
	];

	public static readonly IReadOnlyList<LobbyModeCard> ModeCards =
	[
		new( "tdm", AimboxGameMode.TeamDeathmatch, "TEAM DEATHMATCH", "Eliminate the enemy team.", "5v5", true ),
		new( "ctf", null, "CAPTURE THE FLAG", "Steal the enemy flag.", "6v6", false ),
		new( "range", AimboxGameMode.Range, "RANGE", "Practice on passive dummies — no time limit.", "SOLO", true ),
		new( "ffa", AimboxGameMode.FreeForAll, "FREE FOR ALL", "Every player for themselves.", "FFA", true ),
		new( "duel", AimboxGameMode.Duel, "DUEL", "First to the kill limit wins.", "1v1", true ),
		new( "survival", AimboxGameMode.Survival, "SURVIVAL", "Clear waves of enemies.", "CO-OP", true )
	];

	public static bool IsMapPlayable( string mapId ) =>
		Maps.Any( m => m.Id == NormalizeMapId( mapId ) && m.Playable );

	public static string NormalizeMapId( string mapId ) =>
		AimboxMapCatalog.NormalizeMapId( mapId );

	public static IReadOnlyList<LobbyMapDefinition> PlayableMaps =>
		Maps.Where( m => m.Playable ).ToList();

	public static LobbyMapDefinition GetMap( string mapId )
	{
		mapId = NormalizeMapId( mapId );
		return Maps.FirstOrDefault( m => m.Id == mapId ) ?? Maps[0];
	}

	public static string MapDisplayName( string mapId ) => GetMap( mapId ).Name;

	public static string MatchTimeLabel() =>
		FormatClock( AimboxArenaConfig.MatchDurationSeconds );

	public static string MatchTimeLabel( AimboxGameMode mode ) => mode switch
	{
		AimboxGameMode.Range => "∞",
		_ when AimboxAimModeRules.IsAimMode( mode ) => FormatClock( AimboxArenaConfig.AimMatchDurationSeconds ),
		_ => MatchTimeLabel()
	};

	public static string ScoreLimitLabel( AimboxGameMode mode ) => mode switch
	{
		AimboxGameMode.TeamDeathmatch => AimboxArenaConfig.TdmTeamScoreLimit.ToString( "N0" ),
		AimboxGameMode.Duel => AimboxArenaConfig.DuelKillLimit.ToString(),
		AimboxGameMode.Survival => "WAVES",
		AimboxGameMode.Range => "∞",
		_ when AimboxAimModeRules.IsAimMode( mode ) => "TIME",
		_ => (AimboxArenaConfig.FfaScoreLimit / AimboxArenaConfig.PointsPerKill).ToString()
	};

	public static string PlayersLabel( AimboxGameMode mode ) =>
		AimboxGameModeLabels.RosterLabel( mode );

	public static string ModeDescription( AimboxGameMode mode ) => mode switch
	{
		AimboxGameMode.TeamDeathmatch => "Work with your squad to eliminate the enemy team and reach the score limit.",
		AimboxGameMode.Duel => "Face a single opponent. First to the kill limit takes the round.",
		AimboxGameMode.Survival => "Survive escalating enemy waves with your loadout.",
		AimboxGameMode.Range => "Shoot passive dummies at your own pace. They respawn quickly and never fight back.",
		AimboxGameMode.AimLevel1 => AimboxAimDrillLabels.Description( AimboxAimDrill.Triple ) + " Private trainer room, 60 seconds.",
		AimboxGameMode.AimLevel2 => AimboxAimDrillLabels.Description( AimboxAimDrill.Flick ) + " Private trainer room, 60 seconds.",
		AimboxGameMode.AimLevel3 => AimboxAimDrillLabels.Description( AimboxAimDrill.Bounce ) + " Private trainer room, 60 seconds.",
		AimboxGameMode.AimLevel4 => AimboxAimDrillLabels.Description( AimboxAimDrill.MicroTriple ) + " Private trainer room, 60 seconds.",
		AimboxGameMode.AimLevel5 => AimboxAimDrillLabels.Description( AimboxAimDrill.MicroFlick ) + " Private trainer room, 60 seconds.",
		AimboxGameMode.AimLevel6 => AimboxAimDrillLabels.Description( AimboxAimDrill.MicroBounce ) + " Private trainer room, 60 seconds.",
		_ => "Eliminate enemies to reach the score limit. No teams, no allies."
	};

	public static string PlayerDisplayName( AimboxPlayerController player )
	{
		if ( player is null )
			return "UNKNOWN";

		return player.DisplayName.ToUpperInvariant();
	}

	public static int PlayerDisplayLevel( AimboxPlayerController player ) =>
		player?.DisplayLevel ?? 1;

	public static string BotDisplayName( AimboxBotController bot ) =>
		bot?.DisplayName?.ToUpperInvariant() ?? "BOT";

	public static int TeamCapacity( AimboxGameMode mode ) =>
		mode == AimboxGameMode.TeamDeathmatch ? AimboxArenaConfig.TdmRosterPerTeam : 0;

	public static string TeamCountLabel( int count, int capacity ) =>
		capacity > 0 ? $"{count}/{capacity}" : count.ToString();

	public static string FormatClock( float seconds )
	{
		var total = Math.Max( 0, (int)seconds );
		var minutes = total / 60;
		var secs = total % 60;
		return $"{minutes}:{secs:00}";
	}
}
