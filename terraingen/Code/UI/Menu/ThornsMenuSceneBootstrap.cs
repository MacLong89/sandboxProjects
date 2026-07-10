namespace Terraingen.UI.Menu;

using Terraingen.UI;
using Terraingen.UI.Core;

/// <summary>Bootstraps the main menu scene — static backdrop and <see cref="MainMenuHost"/> UI.</summary>
[Title( "Thorns Menu Scene Bootstrap" )]
[Category( "Thorns/Menu" )]
[Icon( "play_circle" )]
public sealed class ThornsMenuSceneBootstrap : Component
{
	[Property] public bool CreateMenuUi { get; set; } = true;

	[Property] public GameObject MenuCameraObject { get; set; }

	bool _started;

	protected override void OnAwake()
	{
		ThornsUiCursor.EnsureMainMenuVisible();
		UiRevisionBus.ResetMenuListeners();
		Log.Info( "[Thorns Menu] Bootstrap awake." );
	}

	protected override void OnStart()
	{
		Log.Info( "[Thorns Menu] Bootstrap OnStart." );
		_ = StartMenuAsync();
	}

	async System.Threading.Tasks.Task StartMenuAsync()
	{
		try
		{
			await System.Threading.Tasks.Task.Yield();
			await System.Threading.Tasks.Task.Yield();

			if ( _started || !IsValid )
				return;

			_started = true;
			Log.Info( "[Thorns Menu] Main menu scene starting." );

			if ( !CreateMenuUi )
				return;

			await ThornsMainMenuBootstrap.EnsureMenuUiAsync( GameObject, Scene );
			Log.Info( "[Thorns Menu] MainMenuHost ready." );
		}
		catch ( System.Exception e )
		{
			Log.Error( e, "[Thorns Menu] Bootstrap failed." );
		}
	}
}
