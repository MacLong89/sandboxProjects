using Dynasty.Bootstrap;
using Dynasty.Core;
using Dynasty.Core.Events;
using Dynasty.LeagueNet;
using Dynasty.Persistence;
using Dynasty.Services;

namespace Dynasty;

/// <summary>
/// Application composition root. UI and client bridges read from here; only host mutates via LeagueService commands.
/// </summary>
public static class DynastyApp
{
	public static LeagueService League { get; } = new();
	public static PersistenceService Persistence { get; private set; }
	public static GameSession Session { get; } = new();
	public static CommandResultNotification LastCommandResult { get; set; }

	static bool _initialized;

	public static void Initialize( LeagueService service = null )
	{
		if ( service != null && !ReferenceEquals( service, League ) )
			return;

		if ( _initialized )
			return;

		_initialized = true;
		DynastyClientSettings.Load();
		Persistence = new PersistenceService( League );

		League.Events.Subscribe<LeagueStateMutatedEvent>( OnLeagueStateMutated );
	}

	static void OnLeagueStateMutated( LeagueStateMutatedEvent _ ) => Persistence.TryAutoSave();

	public static void ReturnToMainMenu( bool saveFirst = true )
	{
		Initialize();

		if ( saveFirst && !string.IsNullOrEmpty( Persistence?.ActiveSaveSlot ) && League.State != null )
			Persistence.SaveActiveSlot();

		League.UnloadLeague();
		Persistence.ClearActiveSlot();
		Session.EnterMainMenu();
	}
}
