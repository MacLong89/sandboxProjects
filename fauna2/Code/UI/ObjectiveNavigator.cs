using Fauna2;

namespace Fauna2.UI;

/// <summary>Opens the UI panel that matches the current guided objective.</summary>
public static class ObjectiveNavigator
{
	public static void Open( ObjectiveAction action )
	{
		switch ( action )
		{
			case ObjectiveAction.FindWildlife:
				UiState.CloseModals();
				UiState.PushToast( "Walk into the wilderness and get close to a wild animal.", "travel_explore" );
				break;
			case ObjectiveAction.ClearLand:
				UiState.CloseModals();
				UiState.PushToast( "Click a tree or rock on your land, then press Clear in the panel.", "forest" );
				break;
			case ObjectiveAction.BuildHabitats:
				UiState.BeginPlaceStarterHabitat();
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
			case ObjectiveAction.BuildShop:
				UiState.BeginPlace( "kiosk" );
				break;
			case ObjectiveAction.Market:
				UiState.OpenMarketForStarterAnimal();
				var animal = StarterGoalGuide.RecommendedAdoptableAnimal();
				if ( animal is not null )
					UiState.PushToast( $"Adopt a {BiomeLabel()} animal — {animal.DisplayName} is a good fit.", "pets" );
				break;
			case ObjectiveAction.PlaceAnimal:
			{
				var inv = PlayerState.Local?.Components.Get<PlayerInventory>();
				if ( !StarterGoalGuide.HasTutorialHabitat() )
				{
					UiState.BeginPlaceStarterHabitat();
					UiState.PushToast( "Place a habitat first, then release your catch with E.", "fence" );
				}
				else if ( inv is not null && inv.CarriedCount > 0 )
				{
					UiState.CloseModals();
					UiState.OpenMarketTab( 1 );
					UiState.PushToast( "Walk to your habitat and press E — or place from the Backpack tab.", "pets" );
				}
				else if ( (ZooState.Instance?.TotalAnimalsCaught ?? 0) <= 0 )
				{
					UiState.CloseModals();
					UiState.PushToast( "Catch a wild animal first, then bring it home to your habitat.", "forest" );
				}
				else
				{
					UiState.CloseModals();
					UiState.PushToast( "Stand next to your habitat and press E to release a carried animal.", "fence" );
				}
				break;
			}
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
				else if ( PlaceableRegistry.ShopCount == 0 )
					UiState.BeginPlace( "kiosk" );
				else
					UiState.OpenPage( UiPage.Stats );
				break;
			case ObjectiveAction.StatsFinances:
				UiState.OpenPage( UiPage.Stats );
				break;
			case ObjectiveAction.StatsOverview:
				UiState.OpenPage( UiPage.Stats );
				UiState.PushToast( "Stats — check your rating and guest wants on Overview / Guests.", "star" );
				break;
			case ObjectiveAction.BreedHelp:
				var starter = StarterGoalGuide.RecommendedAdoptableAnimal();
				var starterId = starter is null ? "" : Defs.IdOf( starter );
				var matchingAdults = AnimalRegistry.All.Count( a =>
					a.DefinitionId == starterId && a.IsAdult && !a.IsElder );
				if ( starter is not null && matchingAdults < 2 )
				{
					UiState.OpenMarketForStarterAnimal();
					UiState.PushToast( $"Adopt a second {starter.DisplayName} and place it with the first. Your first pair breeds quickly.", "pets" );
				}
				else
				{
					UiState.OpenPage( UiPage.Market );
					UiState.OpenMarketTab( 2 );
					UiState.PushToast( "Keep two happy adults of the same species together. Your first pair breeds quickly.", "pets" );
				}
				break;
			case ObjectiveAction.CatchHelp:
				UiState.OpenMarketTab( 3 );
				UiState.PushToast( "Catch Tools — buy a Tranquilizer, then walk into the wild and press E near an animal.", "shopping_bag" );
				break;
		}
	}

	private static string BiomeLabel() => StarterGoalGuide.BiomeLabel();
}
