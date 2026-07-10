namespace Sandbox;

/// <summary>
/// Runtime main-menu setup — keeps custom components off scene JSON so startup never shows "Missing Component"
/// when the game assembly is still compiling.
/// </summary>
public static class ThornsMainMenuBootstrap
{
	public static void EnsureOnScene( Scene scene )
	{
		if ( scene is null || !scene.IsValid() || !Game.IsPlaying )
			return;

		var uiRoot = scene.Directory.FindByName( "MainMenuUi" ).FirstOrDefault();
		if ( !uiRoot.IsValid() )
			return;

		EnsureMenuComponents( uiRoot );
	}

	public static void EnsureMenuComponents( GameObject uiRoot )
	{
		if ( !uiRoot.IsValid() )
			return;

		if ( !uiRoot.Components.Get<ThornsMainMenuAtmosphere>( FindMode.EnabledInSelf ).IsValid() )
			_ = uiRoot.Components.Create<ThornsMainMenuAtmosphere>();

		if ( uiRoot.Components.Get<ThornsMainMenuUI>( FindMode.EnabledInSelf ).IsValid() )
			return;

		var ui = uiRoot.Components.Create<ThornsMainMenuUI>();
		if ( !ui.IsValid() )
		{
			Log.Error( "[Thorns] Main menu: could not create ThornsMainMenuUI — fix compile errors and restart the editor." );
			return;
		}

		Log.Info( "[Thorns] Main menu: ThornsMainMenuUI created via ThornsMainMenuBootstrap." );
	}
}
