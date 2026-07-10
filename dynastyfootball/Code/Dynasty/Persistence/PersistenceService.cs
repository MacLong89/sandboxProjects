using System.Text.Json;
using Dynasty.Core.Enums;
using Dynasty.Domain.League;
using Dynasty.Services;

namespace Dynasty.Persistence;

public sealed class PersistenceService
{
	private readonly LeagueService _leagueService;

	public PersistenceService( LeagueService leagueService ) => _leagueService = leagueService;

	public string ActiveSaveSlot { get; private set; }

	public string GetSaveDirectory() => GameSaveStorage.GetDisplayPath();

	public static string SanitizeSlotId( string name )
	{
		if ( string.IsNullOrWhiteSpace( name ) )
			return "save";

		var chars = name.Trim().ToLowerInvariant()
			.Select( c => char.IsLetterOrDigit( c ) ? c : '-' )
			.ToArray();

		var slot = new string( chars ).Trim( '-' );
		while ( slot.Contains( "--" ) )
			slot = slot.Replace( "--", "-" );

		return string.IsNullOrEmpty( slot ) ? "save" : slot;
	}

	public IReadOnlyList<SaveSlotInfo> ListSaveSlots()
	{
		var slots = new List<SaveSlotInfo>();

		foreach ( var fileName in GameSaveStorage.ListSaveFileNames() )
		{
			try
			{
				slots.Add( ReadSlotInfo( fileName ) );
			}
			catch ( Exception ex )
			{
				Log.Warning( $"Skipping corrupt save '{fileName}': {ex.Message}" );
			}
		}

		return slots.OrderByDescending( s => s.SavedUtc ).ToList();
	}

	public static SaveSlotInfo ReadSlotInfo( string fileName )
	{
		var path = $"{GameSaveStorage.SaveRoot}/{fileName}";
		var json = GameSaveStorage.ReadText( path );
		if ( string.IsNullOrEmpty( json ) )
			throw new Exception( $"Save file is empty: {fileName}" );

		using var doc = JsonDocument.Parse( json );
		var root = doc.RootElement;
		var league = root.GetProperty( "league" );
		var slotId = GameSaveStorage.SlotIdFromFileName( fileName );

		var phase = league.GetProperty( "phase" ).GetString() ?? "";

		return new SaveSlotInfo
		{
			SlotId = slotId,
			FilePath = path,
			LeagueName = root.TryGetProperty( "leagueName", out var ln ) ? ln.GetString() : "Unknown League",
			Season = league.GetProperty( "currentSeason" ).GetInt32(),
			Week = league.GetProperty( "currentWeek" ).GetInt32(),
			Phase = phase,
			UrgentLabel = BuildUrgentLabel( phase, league ),
			SavedUtc = root.TryGetProperty( "savedUtc", out var saved ) ? saved.GetDateTime() : DateTime.UtcNow
		};
	}

	static string BuildUrgentLabel( string phase, JsonElement league )
	{
		if ( phase == nameof( LeaguePhase.Draft ) )
		{
			if ( league.TryGetProperty( "draft", out var draft )
				&& draft.TryGetProperty( "isActive", out var active )
				&& active.GetBoolean() )
				return "On the clock!";
		}

		return phase switch
		{
			nameof( LeaguePhase.FreeAgency ) => "FA open!",
			nameof( LeaguePhase.Playoffs ) => "Playoffs!",
			_ => ""
		};
	}

	public void StartNewGame( string slotName, string leagueName, string teamAbbreviation = "", DynastyStartMode startMode = DynastyStartMode.RookieDraft, ChallengeMode challengeMode = ChallengeMode.Standard, bool enableMultiGm = false )
	{
		if ( !GameNetworking.IsHost && GameNetworking.IsActive )
			throw new InvalidOperationException( "Only the host can create a new league." );

		var slotId = SanitizeSlotId( slotName );
		var settings = new LeagueSettings
		{
			LeagueName = string.IsNullOrWhiteSpace( leagueName ) ? slotName : leagueName.Trim(),
			HumanTeamAbbreviation = teamAbbreviation?.Trim() ?? "",
			StartMode = startMode,
			ChallengeMode = challengeMode,
			EnableMultiGm = enableMultiGm,
			IsFtueExperience = true,
			FtuePreseasonWeeks = 1
		};

		ActiveSaveSlot = slotId;
		_leagueService.CreateNewLeague( settings );
		SaveActiveSlot();
		DynastyLeaderboardService.SubmitFromLeague( _leagueService.State, slotId );
	}

	public void LoadGame( string slotId )
	{
		if ( !GameNetworking.IsHost && GameNetworking.IsActive )
			throw new InvalidOperationException( "Only the host can load a league." );

		if ( !GameSaveStorage.SlotExists( slotId ) )
			throw new Exception( $"Save slot '{slotId}' not found." );

		ActiveSaveSlot = slotId;
		var state = LeagueSaveSerializer.LoadFromFile( GameSaveStorage.GetSlotPath( slotId ) );
		_leagueService.LoadLeague( state );
	}

	public void SaveActiveSlot()
	{
		if ( string.IsNullOrEmpty( ActiveSaveSlot ) )
			return;

		if ( _leagueService.State == null )
			return;

		if ( !GameNetworking.IsHost && GameNetworking.IsActive )
			return;

		SaveSlot( ActiveSaveSlot, _leagueService.State );
		DynastyLeaderboardService.SubmitFromLeague( _leagueService.State, ActiveSaveSlot );
	}

	public void SaveSlot( string slotId, LeagueState state )
	{
		var path = GameSaveStorage.GetSlotPath( slotId );
		LeagueSaveSerializer.SaveToFile( state, path, slotId );
		ActiveSaveSlot = slotId;
	}

	public bool DeleteSaveSlot( string slotId )
	{
		if ( !GameSaveStorage.SlotExists( slotId ) )
			return false;

		GameSaveStorage.DeleteSlot( slotId );

		if ( ActiveSaveSlot == slotId )
			ActiveSaveSlot = null;

		return true;
	}

	public void ClearActiveSlot() => ActiveSaveSlot = null;

	public void TryAutoSave()
	{
		if ( string.IsNullOrEmpty( ActiveSaveSlot ) )
			return;

		try
		{
			SaveActiveSlot();
		}
		catch ( Exception ex )
		{
			Log.Error( $"Auto-save failed for '{ActiveSaveSlot}': {ex.Message}" );
		}
	}
}
