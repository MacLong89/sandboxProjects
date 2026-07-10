namespace Terraingen.UI.Menu;

using System.Threading.Tasks;

/// <summary>
/// Boots main-menu UI when the startup scene loads. Uses GameObjectSystem so menu setup still runs
/// if scene-serialized bootstrap components fail to bind during a hotload race.
/// </summary>
public sealed class ThornsMainMenuSceneSystem : GameObjectSystem<ThornsMainMenuSceneSystem>, ISceneStartup
{
	bool _bootstrapStarted;

	public ThornsMainMenuSceneSystem( Scene scene ) : base( scene )
	{
	}

	void ISceneStartup.OnHostInitialize() => _ = EnsureMainMenuAsync();

	void ISceneStartup.OnClientInitialize() => _ = EnsureMainMenuAsync();

	static bool IsMainMenuScene( Scene scene )
	{
		if ( scene is null || !scene.IsValid )
			return false;

		if ( ThornsMenuSceneLoader.IsInGameplayScene( scene ) )
			return false;

		return FindMainMenuUiRoot( scene ) is not null;
	}

	static GameObject FindMainMenuUiRoot( Scene scene )
	{
		foreach ( var obj in scene.GetAllObjects( true ) )
		{
			if ( obj is null || !obj.IsValid || obj.Name != "MainMenuUi" )
				continue;

			return obj;
		}

		return null;
	}

	async Task EnsureMainMenuAsync()
	{
		if ( _bootstrapStarted || !Game.IsPlaying || Application.IsDedicatedServer )
			return;

		if ( !IsMainMenuScene( Scene ) )
			return;

		for ( var attempt = 0; attempt < 120; attempt++ )
		{
			if ( !Game.IsPlaying || !Scene.IsValid )
				return;

			var hostGo = FindMainMenuUiRoot( Scene );
			if ( hostGo is null || !hostGo.IsValid )
			{
				await Task.Delay( 50 );
				continue;
			}

			if ( hostGo.Components.Get<MainMenuHost>( FindMode.EverythingInSelf ) is { IsValid: true } menuHost
			     && menuHost.IsPanelReady && menuHost.IsUiBuilt )
			{
				_bootstrapStarted = true;
				return;
			}

			if ( hostGo.Components.Get<ThornsMenuSceneBootstrap>( FindMode.EverythingInSelf ) is { IsValid: true } )
			{
				_bootstrapStarted = true;
				return;
			}

			_bootstrapStarted = true;
			Log.Info( "[Thorns Menu] Scene system bootstrap — creating menu UI." );

			try
			{
				await ThornsMainMenuBootstrap.EnsureMenuUiAsync( hostGo, Scene );
				return;
			}
			catch ( System.Exception e )
			{
				Log.Error( e, "[Thorns Menu] Scene system bootstrap failed." );
				_bootstrapStarted = false;
				await Task.Delay( 100 );
			}
		}

		Log.Warning( "[Thorns Menu] Scene system could not bootstrap main menu UI." );
	}
}
