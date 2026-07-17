namespace Fauna2;

using Fauna2.UI;

/// <summary>Click animals, habitat fences, and obstacles to inspect; collect tips from shops.</summary>
public sealed class AnimalPicker : Component
{
	protected override void OnUpdate()
	{
		if ( GameManager.Instance is null || !GameManager.Instance.GameStarted )
			return;

		// AUDIT FIX B11: share world-input gate (intro / loading / catch).
		if ( !UiState.CanWorldInput )
			return;

		if ( BuildController.Instance?.Mode != BuildMode.None )
			return;

		if ( UiState.IsPageOpen || UiState.DebugVisible || UiState.CatchMinigameOpen )
			return;

		if ( UiState.PointerOverUI || !Input.Pressed( "attack1" ) )
			return;

		if ( CollectibleBuildingHelper.TryCollectAtMouse( Scene ) )
			return;

		if ( WorldPicker.TryPick( Scene, default, out var animal, out var habitat, out var obstacle ) )
		{
			if ( animal is not null )
				UiState.SelectAnimal( animal.AnimalId );
			else if ( habitat is not null )
				UiState.SelectHabitat( habitat.HabitatId );
			else if ( obstacle is not null )
				UiState.SelectObstacle( obstacle.CellKey );
			return;
		}

		UiState.ClearWorldSelection();
	}
}
