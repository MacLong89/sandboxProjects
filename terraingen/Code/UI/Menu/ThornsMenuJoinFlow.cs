namespace Terraingen.UI.Menu;

using Terraingen.UI;
using Terraingen.UI.Core;

/// <summary>Real join/load stage labels — no fake progress bar.</summary>
public enum ThornsMenuJoinStage
{
	Idle,
	Connecting,
	LoadingWorld,
	GeneratingTerrain,
	SyncCharacter,
	SyncInventory,
	SyncProgression,
	EnteringWorld
}

public static class ThornsMenuJoinFlow
{
	static string _customMessage = "";

	public static ThornsMenuJoinStage CurrentStage { get; private set; } = ThornsMenuJoinStage.Idle;

	public static event Action<ThornsMenuJoinStage> StageChanged;

	public static bool IsProgressVisible =>
		CurrentStage != ThornsMenuJoinStage.Idle || !string.IsNullOrWhiteSpace( _customMessage );

	public static string ProgressMessage
	{
		get
		{
			if ( !string.IsNullOrWhiteSpace( _customMessage ) )
				return _customMessage;

			return StageLabel( CurrentStage );
		}
	}

	public static void SetProgressMessage( string message )
	{
		_customMessage = message?.Trim() ?? "";
		StageChanged?.Invoke( CurrentStage );
		UiRevisionBus.Publish( UiRevisionChannel.Menu );
	}

	public static void ClearProgressMessage()
	{
		if ( string.IsNullOrWhiteSpace( _customMessage ) )
			return;

		_customMessage = "";
		StageChanged?.Invoke( CurrentStage );
		UiRevisionBus.Publish( UiRevisionChannel.Menu );
	}

	public static void SetStage( ThornsMenuJoinStage stage )
	{
		if ( CurrentStage == stage )
			return;

		CurrentStage = stage;

		if ( stage == ThornsMenuJoinStage.Idle )
			_customMessage = "";
		else if ( stage != ThornsMenuJoinStage.Connecting )
			_customMessage = "";

		StageChanged?.Invoke( stage );
		UiRevisionBus.Publish( UiRevisionChannel.Menu );
	}

	public static void ResetForMainMenu()
	{
		CurrentStage = ThornsMenuJoinStage.Idle;
		_customMessage = "";
		StageChanged?.Invoke( CurrentStage );
	}

	public static void Reset()
	{
		CurrentStage = ThornsMenuJoinStage.Idle;
		_customMessage = "";
		StageChanged?.Invoke( CurrentStage );
		UiRevisionBus.Publish( UiRevisionChannel.Menu );
	}

	/// <summary>Clears join progress and switches UI/input to active gameplay.</summary>
	public static void CompleteEnterWorld()
	{
		Reset();
		ThornsLoadingScreenUtil.Dismiss();
		ThornsUiManager.SetContext( ThornsUiManager.UiContext.Gameplay );
		ThornsUiCursor.SyncForActiveContext();
	}

	public static string StageLabel( ThornsMenuJoinStage stage ) => stage switch
	{
		ThornsMenuJoinStage.Connecting => "Connecting to Server...",
		ThornsMenuJoinStage.LoadingWorld => "Loading World...",
		ThornsMenuJoinStage.GeneratingTerrain => "Generating Terrain...",
		ThornsMenuJoinStage.SyncCharacter => "Synchronizing Character...",
		ThornsMenuJoinStage.SyncInventory => "Synchronizing Inventory...",
		ThornsMenuJoinStage.SyncProgression => "Synchronizing Progression...",
		ThornsMenuJoinStage.EnteringWorld => "Entering World...",
		_ => ""
	};
}
