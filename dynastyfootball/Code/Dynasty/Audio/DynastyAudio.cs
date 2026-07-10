using Dynasty.Bootstrap;
using Dynasty.Core;
using Dynasty.Core.Enums;
using Dynasty.Core.Events;

namespace Dynasty.Audio;

/// <summary>
/// Central audio playback for Dynasty UI and franchise events.
/// </summary>
public static class DynastyAudio
{
	const string ButtonSound = "sounds/button.sound";
	const string EconomySound = "sounds/economy.sound";
	const string MilestoneSound = "sounds/milestoneprogressionlevelup.sound";
	const string MenuMusicPath = "sounds/menu_music.mp3";

	static MusicPlayer _menuMusic;
	static bool _eventsRegistered;

	public static bool Enabled { get; set; } = true;
	public static float MenuMusicVolume { get; set; } = 0.175f;
	public static float SfxVolume { get; set; } = 0.5f;
	const float EconomyVolumeScale = 0.2f;
	const float DraftMilestoneVolumeScale = 0.3f;

	static long _lastButtonSoundTicks;

	public static void PlayButton()
	{
		if ( !Enabled )
			return;

		var ticks = DateTime.UtcNow.Ticks;
		if ( ticks - _lastButtonSoundTicks < TimeSpan.TicksPerSecond / 12 )
			return;

		_lastButtonSoundTicks = ticks;
		PlayUiSound( ButtonSound );
	}

	public static void PlayEconomy() => PlayUiSound( EconomySound, SfxVolume * EconomyVolumeScale );

	public static void PlayMilestone() => PlayUiSound( MilestoneSound );

	public static void PlayDraftMilestone() => PlayUiSound( MilestoneSound, SfxVolume * DraftMilestoneVolumeScale );

	public static void StartMenuMusic()
	{
		if ( !Enabled || _menuMusic != null )
			return;

		_menuMusic = MusicPlayer.Play( FileSystem.Mounted, MenuMusicPath );
		if ( _menuMusic == null )
			return;

		_menuMusic.Repeat = true;
		_menuMusic.Volume = MenuMusicVolume;
	}

	public static void StopMenuMusic()
	{
		if ( _menuMusic == null )
			return;

		_menuMusic.Stop();
		_menuMusic = null;
	}

	public static void RegisterEventHandlers()
	{
		if ( _eventsRegistered )
			return;

		_eventsRegistered = true;
		DynastyApp.Initialize();

		var events = DynastyApp.League.Events;
		events.Subscribe<TradeCompletedEvent>( OnTradeCompleted );
		events.Subscribe<ContractSignedEvent>( OnContractSigned );
		events.Subscribe<ChampionshipWonEvent>( OnChampionshipWon );
		events.Subscribe<DraftPickMadeEvent>( OnDraftPick );
		events.Subscribe<PhaseChangedEvent>( OnPhaseChanged );
	}

	static void OnTradeCompleted( TradeCompletedEvent _ ) => PlayEconomy();

	static void OnContractSigned( ContractSignedEvent _ ) => PlayEconomy();

	static void OnChampionshipWon( ChampionshipWonEvent _ ) => PlayMilestone();

	static void OnDraftPick( DraftPickMadeEvent e )
	{
		var state = DynastyApp.League.State;
		if ( state == null )
			return;

		var humanId = GmAssignmentHelper.GetHumanTeamId( state );
		if ( humanId.IsEmpty || e.TeamId.Value != humanId.Value )
			return;

		PlayDraftMilestone();
	}

	static void OnPhaseChanged( PhaseChangedEvent e )
	{
		if ( e.NewPhase is LeaguePhase.Playoffs or LeaguePhase.Draft or LeaguePhase.FreeAgency )
			PlayMilestone();
	}

	static void PlayUiSound( string soundPath, float? volume = null )
	{
		if ( !Enabled || string.IsNullOrWhiteSpace( soundPath ) )
			return;

		var handle = Sound.Play( soundPath );
		handle.Volume = volume ?? SfxVolume;
		handle.SpacialBlend = 0f;
	}
}
