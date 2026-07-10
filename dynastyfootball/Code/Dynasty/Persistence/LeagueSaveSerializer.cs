using System.Text.Json;
using System.Text.Json.Serialization;
using Dynasty.Domain.League;
using Dynasty.Domain.Teams;

namespace Dynasty.Persistence;

public static class LeagueSaveSerializer
{
	public const int CurrentSaveFormatVersion = 1;

	static readonly JsonSerializerOptions Options = new()
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		Converters =
		{
			new JsonStringEnumConverter( JsonNamingPolicy.CamelCase ),
			new LeagueIdJsonConverter(),
			new TeamIdJsonConverter(),
			new PlayerIdJsonConverter(),
			new CoachIdJsonConverter(),
			new GameIdJsonConverter(),
			new DraftPickIdJsonConverter()
		}
	};

	public static string Serialize( LeagueState state, string saveSlotId = "" )
	{
		var envelope = new LeagueSaveEnvelope
		{
			SavedUtc = DateTime.UtcNow,
			SaveSlotId = saveSlotId,
			LeagueName = state.Settings.LeagueName,
			League = state
		};

		return JsonSerializer.Serialize( envelope, Options );
	}

	public static LeagueState Deserialize( string json )
	{
		var envelope = JsonSerializer.Deserialize<LeagueSaveEnvelope>( json, Options );
		if ( envelope == null || envelope.League == null )
			throw new Exception( "Invalid league save file." );

		return Migrate( envelope );
	}

	public static LeagueState Migrate( LeagueSaveEnvelope envelope )
	{
		var league = envelope.League;
		if ( league?.Settings == null )
			return league;

		if ( league.Settings.RookieDraftRounds <= 0 )
			league.Settings.RookieDraftRounds = 7;

		// Older expansion saves incorrectly stored 52 in draftRounds; rookie draft is always 7.
		if ( league.Settings.RookieDraftRounds > 7 )
			league.Settings.RookieDraftRounds = 7;

		if ( league.Settings.ExpansionDraftRounds <= 0 )
			league.Settings.ExpansionDraftRounds = 52;

		if ( league.History?.SeasonRecords == null )
			league.History.SeasonRecords = new();

		foreach ( var team in league.Teams.Values )
			team.LifetimeRecord ??= new TeamLifetimeRecord();

		league.FranchiseProgress ??= new Domain.Franchise.FranchiseProgressState();
		league.FranchiseProgress.MilestonesReached ??= new();
		league.PendingTradeOffers ??= new();

		if ( league.SchemaVersion < LeagueState.CurrentSchemaVersion )
			league.SchemaVersion = LeagueState.CurrentSchemaVersion;

		return league;
	}

	public static void SaveToFile( LeagueState state, string virtualPath, string saveSlotId = "" )
	{
		GameSaveStorage.EnsureSaveDirectory();
		GameSaveStorage.WriteText( virtualPath, Serialize( state, saveSlotId ) );
	}

	public static LeagueState LoadFromFile( string virtualPath )
	{
		var json = GameSaveStorage.ReadText( virtualPath );
		if ( string.IsNullOrEmpty( json ) )
			throw new Exception( $"Save file not found or empty: {virtualPath}" );

		return Deserialize( json );
	}
}
