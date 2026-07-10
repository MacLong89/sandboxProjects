namespace Fauna2;

using Fauna2.UI;

public enum WorldInteractKind
{
	None,
	CollectRevenue,
	InspectAnimal,
	InspectHabitat,
	ClearObstacle,
	InspectShop,
	CatchWildAnimal,
	PlaceCarriedAnimal,
}

/// <summary>Nearest world target the player can interact with using E.</summary>
public readonly struct WorldInteractTarget
{
	public WorldInteractKind Kind { get; init; }
	public string Label { get; init; }
	public string Icon { get; init; }

	public bool IsValid => Kind != WorldInteractKind.None;
}

/// <summary>Proximity interaction — press E near animals, habitats, shops, trees.</summary>
public static class WorldInteractState
{
	public static WorldInteractTarget Current { get; private set; }

	public static void Set( WorldInteractTarget target ) => Current = target;

	public static void Clear() => Current = default;
}

public sealed class WorldInteractSystem : Component
{
	private PlaceableComponent _pendingPlaceable;
	private AnimalComponent _pendingAnimal;
	private HabitatComponent _pendingHabitat;
	private TerrainObstacleComponent _pendingObstacle;
	private WildAnimalComponent _pendingWild;
	private WorldInteractTarget _cachedTarget;
	private TimeUntil _nextScan;

	protected override void OnUpdate()
	{
		if ( CatchSystem.Instance?.MinigameActive == true || WildAttackSystem.Instance?.EncounterActive == true )
		{
			_cachedTarget = default;
			WorldInteractState.Clear();
			return;
		}

		if ( GameManager.Instance is null || !GameManager.Instance.GameStarted )
		{
			_cachedTarget = default;
			WorldInteractState.Clear();
			return;
		}

		if ( BuildController.Instance?.Mode != BuildMode.None || UiState.IsPageOpen || UiState.DebugVisible )
		{
			_cachedTarget = default;
			WorldInteractState.Clear();
			return;
		}

		var player = PlayerState.Local;
		if ( !player.IsValid() )
		{
			_cachedTarget = default;
			WorldInteractState.Clear();
			return;
		}

		if ( _nextScan )
		{
			_cachedTarget = FindBestTarget( player.FeetPosition );
			_nextScan = 0.07f;
		}

		WorldInteractState.Set( _cachedTarget );

		if ( _cachedTarget.IsValid && Input.Pressed( "Interact" ) )
			Execute( _cachedTarget, player.FeetPosition );
	}

	private WorldInteractTarget FindBestTarget( Vector3 feet )
	{
		_pendingPlaceable = null;
		_pendingAnimal = null;
		_pendingHabitat = null;
		_pendingObstacle = null;
		_pendingWild = null;

		var maxRange = GameConstants.InteractionRange;
		WorldInteractTarget best = default;
		var bestScore = float.MaxValue;

		foreach ( var placeable in PlaceableRegistry.All )
		{
			if ( !placeable.IsValid() ) continue;
			var restaurant = placeable.Components.Get<RestaurantComponent>();
			if ( restaurant is null ) continue;

			var dist = CollectibleBuildingHelper.DistanceToFootprint( feet, placeable );
			if ( dist > CollectibleBuildingHelper.CollectibleInteractRange ) continue;

			var hasMoney = restaurant.Uncollected > 0;
			if ( hasMoney )
			{
				TryCandidate(
					ref best,
					ref bestScore,
					WorldInteractKind.CollectRevenue,
					$"Collect ${restaurant.Uncollected:n0}",
					"money",
					dist,
					1,
					() => _pendingPlaceable = placeable );
			}
			else if ( placeable.Definition?.ProvidesShop == true && PlayerState.Local?.IsZooOwner == true )
			{
				TryCandidate(
					ref best,
					ref bestScore,
					WorldInteractKind.InspectShop,
					$"Shop — {placeable.Definition?.DisplayName ?? "Kiosk"}",
					"storefront",
					dist,
					1,
					() => _pendingPlaceable = placeable );
			}
		}

		foreach ( var animal in AnimalRegistry.All )
		{
			if ( !animal.IsValid() ) continue;
			var dist = HorizontalDistance( feet, animal.GameObject.WorldPosition );
			if ( dist > maxRange ) continue;

			TryCandidate(
				ref best,
				ref bestScore,
				WorldInteractKind.InspectAnimal,
				$"Inspect {animal.AnimalName}",
				"pets",
				dist,
				2,
				() => _pendingAnimal = animal );
		}

		foreach ( var habitat in HabitatRegistry.All )
		{
			if ( !habitat.IsValid() ) continue;
			var dist = HorizontalDistance( feet, habitat.GameObject.WorldPosition );
			if ( dist > maxRange ) continue;

			TryCandidate(
				ref best,
				ref bestScore,
				WorldInteractKind.InspectHabitat,
				$"Inspect {habitat.Biome} habitat",
				"build",
				dist,
				3,
				() => _pendingHabitat = habitat );
		}

		var obstacle = TerrainObstacleRegistry.Nearest( feet, GameConstants.ObstaclePickRadius );
		if ( obstacle is not null )
		{
			var dist = HorizontalDistance( feet, obstacle.WorldPosition );
			TryCandidate(
				ref best,
				ref bestScore,
				WorldInteractKind.ClearObstacle,
				"Clear obstacle",
				"interact",
				dist,
				0,
				() => _pendingObstacle = obstacle );
		}

		foreach ( var wild in WildAnimalRegistry.All )
		{
			if ( !wild.IsValid() || wild.Fled ) continue;
			if ( PlayerState.Local?.IsZooOwner != true ) continue;

			var dist = HorizontalDistance( feet, wild.GameObject.WorldPosition );
			if ( dist > GameConstants.WildAnimalInteractRange ) continue;

			var name = wild.Definition?.DisplayName ?? "animal";
			var biome = WildernessBiomeMap.BiomeForPlot( wild.PlotX, wild.PlotY, ZooState.Instance?.StarterBiome ?? Biome.Grassland );
			TryCandidate(
				ref best,
				ref bestScore,
				WorldInteractKind.CatchWildAnimal,
				$"Catch {name} — {WildernessBiomeMap.RegionLabel( biome )}",
				"pets",
				dist,
				-1,
				() => _pendingWild = wild );
		}

		var inv = PlayerState.Local?.Components.Get<PlayerInventory>();
		if ( PlayerState.Local?.IsZooOwner == true && inv is not null && inv.CarriedCount > 0 )
		{
			var habitat = HabitatRegistry.FindAt( feet );
			var species = inv.FirstCarriedSpecies();
			if ( habitat is not null && !string.IsNullOrEmpty( species ) )
			{
				var dist = HorizontalDistance( feet, habitat.GameObject.WorldPosition );
				var def = Defs.Animal( species );
				TryCandidate(
					ref best,
					ref bestScore,
					WorldInteractKind.PlaceCarriedAnimal,
					$"Place {def?.DisplayName ?? "animal"}",
					"fence",
					dist,
					-2,
					() => _pendingHabitat = habitat );
			}
		}

		return best;
	}

	private void TryCandidate(
		ref WorldInteractTarget best,
		ref float bestScore,
		WorldInteractKind kind,
		string label,
		string icon,
		float distance,
		int priority,
		Action bind )
	{
		var score = priority * 1000f + distance;
		if ( score >= bestScore ) return;

		_pendingPlaceable = null;
		_pendingAnimal = null;
		_pendingHabitat = null;
		_pendingObstacle = null;
		_pendingWild = null;
		bind();

		bestScore = score;
		best = new WorldInteractTarget
		{
			Kind = kind,
			Label = label,
			Icon = icon,
		};
	}

	private void Execute( WorldInteractTarget target, Vector3 feet )
	{
		switch ( target.Kind )
		{
			case WorldInteractKind.CollectRevenue:
				_pendingPlaceable?.Components.Get<RestaurantComponent>()?.RequestCollect();
				break;
			case WorldInteractKind.InspectShop:
				UiState.OpenMarketTab( 3 );
				break;
			case WorldInteractKind.InspectAnimal:
				if ( _pendingAnimal is not null )
					UiState.SelectAnimal( _pendingAnimal.AnimalId );
				break;
			case WorldInteractKind.InspectHabitat:
				if ( _pendingHabitat is not null )
					UiState.SelectHabitat( _pendingHabitat.HabitatId );
				break;
			case WorldInteractKind.ClearObstacle:
				if ( _pendingObstacle is not null )
					UiState.SelectObstacle( _pendingObstacle.CellKey );
				break;
			case WorldInteractKind.CatchWildAnimal:
				if ( _pendingWild is not null )
					CatchSystem.Instance?.TryBeginCatch( _pendingWild );
				break;
			case WorldInteractKind.PlaceCarriedAnimal:
			{
				var inv = PlayerState.Local?.Components.Get<PlayerInventory>();
				if ( _pendingHabitat is not null && inv is not null && inv.CarriedCount > 0 )
					CatchSystem.Instance?.RequestPlaceCarried( feet );
				break;
			}
		}

		SocialSystem.Instance?.OnVisitorEngaged();
	}

	private static float HorizontalDistance( Vector3 a, Vector3 b ) =>
		a.WithZ( 0 ).Distance( b.WithZ( 0 ) );
}
