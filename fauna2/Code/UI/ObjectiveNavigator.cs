using Fauna2;

namespace Fauna2.UI;

/// <summary>Opens the UI panel that matches the current guided objective.</summary>
public static class ObjectiveNavigator
{
	public static void Open( ObjectiveAction action )
	{
		switch ( action )
		{
			case ObjectiveAction.ClearLand:
				UiState.CloseModals();
				UiState.PushToast( "Click a tree or rock on your land, then press Clear in the panel.", "forest" );
				break;
			case ObjectiveAction.BuildHabitats:
				UiState.BeginPlaceStarterHabitat();
				UiState.PushToast( $"Place a small {StarterGoalGuide.BiomeLabel()} habitat.", "fence" );
				break;
			case ObjectiveAction.BuildEntrance:
				UiState.BeginPlace( "entrance" );
				break;
			case ObjectiveAction.BuildPaths:
				UiState.BeginPlace( "path_straight" );
				break;
			case ObjectiveAction.BuildUtility:
			case ObjectiveAction.BuildAmenities:
			case ObjectiveAction.BuildRestroom:
				UiState.BeginPlace( "restroom" );
				break;
			case ObjectiveAction.BuildRestaurant:
				UiState.BeginPlace( "restaurant" );
				break;
			case ObjectiveAction.Market:
				UiState.OpenMarketForStarterAnimal();
				var animal = StarterGoalGuide.RecommendedAdoptableAnimal();
				if ( animal is not null )
					UiState.PushToast( $"Adopt a {BiomeLabel()} animal — {animal.DisplayName} is a good fit.", "pets" );
				break;
			case ObjectiveAction.Codex:
				UiState.OpenPage( UiPage.Codex );
				break;
			case ObjectiveAction.Progression:
			case ObjectiveAction.PrestigeHelp:
				UiState.OpenPage( UiPage.Progression );
				break;
			case ObjectiveAction.ExpandLand:
				UiState.CloseModals();
				BuildController.Instance?.BeginExpandLand();
				break;
			case ObjectiveAction.StatsGuests:
				if ( !PathNetwork.HasGuestAccess )
					UiState.BeginPlace( "path_straight" );
				else if ( PlaceableRegistry.RestroomCount == 0 )
					UiState.BeginPlace( "restroom" );
				else if ( PlaceableRegistry.RestaurantCount == 0 )
					UiState.BeginPlace( "restaurant" );
				else
					UiState.OpenPage( UiPage.Stats );
				break;
			case ObjectiveAction.StatsFinances:
				UiState.OpenPage( UiPage.Stats );
				break;
			case ObjectiveAction.StatsOverview:
				var harm = ZooStatsReport.GetRatingHarms().FirstOrDefault();
				if ( harm.Title is not null )
					InsightNavigator.Open( harm );
				else
					UiState.OpenPage( UiPage.Stats );
				break;
			case ObjectiveAction.BreedHelp:
				UiState.OpenPage( UiPage.Market );
				UiState.OpenMarketTab( 2 );
				UiState.PushToast( "Place two happy adults of the same species in one habitat to breed.", "pets" );
				break;
			case ObjectiveAction.CatchHelp:
				UiState.CloseModals();
				UiState.OpenMarketTab( 3 );
				UiState.PushToast( "Stock catch tools, then head into the wilderness and press E on wildlife.", "forest" );
				break;
		}
	}

	private static string BiomeLabel() => StarterGoalGuide.BiomeLabel();
}
