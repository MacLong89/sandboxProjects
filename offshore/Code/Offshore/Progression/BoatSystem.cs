namespace Offshore;

public sealed class BoatDefinition
{
	public string Id { get; set; }
	public string DisplayName { get; set; }
	public string Description { get; set; }
	public float Price { get; set; }

	/// <summary>Added to cooler capacity when this boat is equipped.</summary>
	public float CapacityBonus { get; set; }

	/// <summary>World units/sec while boarded.</summary>
	public float MoveSpeed { get; set; } = 9f;

	/// <summary>How far from the mooring the boat can travel (trip length).</summary>
	public float TripRange { get; set; } = 28f;

	/// <summary>Seconds of outbound travel before the boat is forced to turn back.</summary>
	public float TripDurationSeconds { get; set; } = 90f;

	/// <summary>Max hook / water depth this hull can fish.</summary>
	public float MaxDepth { get; set; } = 6f;

	/// <summary>Largest fish size (catalog Size) that can be reeled aboard.</summary>
	public float MaxFishSize { get; set; } = 1.2f;

	/// <summary>Cast distance multiplier while aboard.</summary>
	public float CastRangeMul { get; set; } = 1f;

	public string UnlocksLocationId { get; set; } = "";

	/// <summary>Empty boat tied at the pier tip.</summary>
	public string DockSpritePath { get; set; } = OffshoreSprites.Paths.BoatRow;

	/// <summary>Fisherman seated in this boat (player avatar while boarded).</summary>
	public string BoardedSpritePath { get; set; } = OffshoreSprites.Paths.BoatRowBoarded;
}

public static class BoatCatalog
{
	public static IReadOnlyList<BoatDefinition> All { get; } =
	[
		new()
		{
			Id = "rowboat",
			DisplayName = "Rowboat",
			Description = "Quiet Bay starter. Short trips, light cooler, shallow water.",
			Price = 120f,
			CapacityBonus = 2f,
			MoveSpeed = 7.5f,
			TripRange = 22f,
			TripDurationSeconds = 70f,
			MaxDepth = 5f,
			MaxFishSize = 1.15f,
			CastRangeMul = 1.05f,
			UnlocksLocationId = "quiet_bay",
			DockSpritePath = OffshoreSprites.Paths.BoatRow,
			BoardedSpritePath = OffshoreSprites.Paths.BoatRowBoarded,
		},
		new()
		{
			Id = "bay_boat",
			DisplayName = "Bay Boat",
			Description = "Coastal runner. More range, deeper water, bigger fish.",
			Price = 350f,
			CapacityBonus = 4f,
			MoveSpeed = 10f,
			TripRange = 38f,
			TripDurationSeconds = 110f,
			MaxDepth = 9f,
			MaxFishSize = 1.55f,
			CastRangeMul = 1.15f,
			UnlocksLocationId = "coastal",
			DockSpritePath = OffshoreSprites.Paths.BoatBay,
			BoardedSpritePath = OffshoreSprites.Paths.BoatBayBoarded,
		},
		new()
		{
			Id = "sport_fisher",
			DisplayName = "Sport Fisher",
			Description = "Open-ocean sport hull. Fast, deep, trophy-ready.",
			Price = 900f,
			CapacityBonus = 7f,
			MoveSpeed = 13f,
			TripRange = 55f,
			TripDurationSeconds = 150f,
			MaxDepth = 14f,
			MaxFishSize = 2.1f,
			CastRangeMul = 1.3f,
			UnlocksLocationId = "open_ocean",
			DockSpritePath = OffshoreSprites.Paths.BoatSport,
			BoardedSpritePath = OffshoreSprites.Paths.BoatSportBoarded,
		},
		new()
		{
			Id = "trawler",
			DisplayName = "Offshore Trawler",
			Description = "Legendary waters workhorse. Huge hold, max depth & range.",
			Price = 2200f,
			CapacityBonus = 12f,
			MoveSpeed = 11f,
			TripRange = 75f,
			TripDurationSeconds = 220f,
			MaxDepth = 20f,
			MaxFishSize = 3.0f,
			CastRangeMul = 1.45f,
			UnlocksLocationId = "legendary_waters",
			DockSpritePath = OffshoreSprites.Paths.BoatTrawler,
			BoardedSpritePath = OffshoreSprites.Paths.BoatTrawlerBoarded,
		},
	];

	public static BoatDefinition Get( string id )
	{
		if ( string.IsNullOrWhiteSpace( id ) )
			return null;

		foreach ( var b in All )
		{
			if ( !string.Equals( b.Id, id, StringComparison.OrdinalIgnoreCase ) )
				continue;
			NormalizeSpritePaths( b );
			return b;
		}

		return null;
	}

	/// <summary>
	/// Always resolve dock/boarded art from boat id. Prevents stale hot-reload / old path constants
	/// from showing the wrong hull (e.g. empty bay boat as the boarded avatar).
	/// </summary>
	public static void NormalizeSpritePaths( BoatDefinition boat )
	{
		if ( boat is null || string.IsNullOrWhiteSpace( boat.Id ) )
			return;

		switch ( boat.Id.ToLowerInvariant() )
		{
			case "rowboat":
				boat.DockSpritePath = "textures/props/boats/rowboat.png";
				boat.BoardedSpritePath = "textures/props/boats/rowboat_boarded.png";
				break;
			case "bay_boat":
				boat.DockSpritePath = "textures/props/boats/bay_boat.png";
				boat.BoardedSpritePath = "textures/props/boats/bay_boat_boarded.png";
				break;
			case "sport_fisher":
				boat.DockSpritePath = "textures/props/boats/sport_fisher.png";
				boat.BoardedSpritePath = "textures/props/boats/sport_fisher_boarded.png";
				break;
			case "trawler":
				boat.DockSpritePath = "textures/props/boats/trawler.png";
				boat.BoardedSpritePath = "textures/props/boats/trawler_boarded.png";
				break;
		}
	}
}

public static class BoatSystem
{
	private static bool _buyLatched;

	/// <summary>Fired after buy/equip so dock visuals can refresh.</summary>
	public static Action EquippedBoatChanged;

	public static BoatDefinition Equipped( PlayerProgressionData progress )
	{
		var boat = BoatCatalog.Get( progress?.EquippedBoatId );
		BoatCatalog.NormalizeSpritePaths( boat );
		return boat;
	}

	public static bool OwnsAny( PlayerProgressionData progress ) =>
		progress is not null && progress.OwnedBoatIds.Count > 0;

	public static bool TryBuy( OffshoreGameController game, string boatId )
	{
		if ( _buyLatched || game is null )
			return false;

		var boat = BoatCatalog.Get( boatId );
		if ( boat is null )
			return false;

		if ( game.Progression.OwnedBoatIds.Contains( boatId ) )
		{
			game.Progression.EquippedBoatId = boatId;
			ApplyCapacity( game );
			game.SetStatus( $"Equipped {boat.DisplayName}" );
			OffshoreSaveSystem.Save( game.Progression );
			EquippedBoatChanged?.Invoke();
			Log.Info( $"[Offshore Boat] Equipped existing '{boatId}' dock='{boat.DockSpritePath}' boarded='{boat.BoardedSpritePath}'" );
			return true;
		}

		if ( game.Progression.Money < boat.Price )
		{
			game.SetStatus( "Not enough money" );
			return false;
		}

		_buyLatched = true;
		game.Progression.Money -= boat.Price;
		game.Progression.LifetimeMoneySpent += boat.Price;
		game.Progression.OwnedBoatIds.Add( boatId );
		game.Progression.EquippedBoatId = boatId;
		if ( !string.IsNullOrEmpty( boat.UnlocksLocationId ) )
			game.Progression.UnlockedLocationIds.Add( boat.UnlocksLocationId );

		ApplyCapacity( game );
		OffshoreSaveSystem.Save( game.Progression );
		_buyLatched = false;
		game.SetStatus( $"Purchased {boat.DisplayName}! Walk to the pier tip to board." );
		EquippedBoatChanged?.Invoke();
		Log.Info( $"[Offshore Boat] Purchased '{boatId}' dock='{boat.DockSpritePath}' boarded='{boat.BoardedSpritePath}'" );
		return true;
	}

	public static void ApplyCapacity( OffshoreGameController game )
	{
		var bonus = 0f;
		var boat = Equipped( game.Progression );
		if ( boat is not null )
			bonus = boat.CapacityBonus;

		var coolerLv = game.Upgrades.GetLevel( game.Progression, "cooler" );
		var baseCap = Math.Max( 1, BalanceConfig.Defaults.StartingCoolerCapacity );
		game.Progression.CoolerCapacity = Math.Max( 1, (int)(baseCap + coolerLv * 3 + bonus) );
	}

	/// <summary>Depth ceiling for the current session (dock fishing vs boarded boat).</summary>
	public static float ActiveMaxDepth( OffshoreGameController game )
	{
		if ( game?.Player?.Mode == AnglerController.LocomotionMode.InBoat )
		{
			var boat = Equipped( game.Progression );
			if ( boat is not null )
				return boat.MaxDepth;
		}

		return OffshoreConstants.DockFishingMaxDepth;
	}

	public static float ActiveMaxFishSize( OffshoreGameController game )
	{
		if ( game?.Player?.Mode == AnglerController.LocomotionMode.InBoat )
		{
			var boat = Equipped( game.Progression );
			if ( boat is not null )
				return boat.MaxFishSize;
		}

		return OffshoreConstants.DockFishingMaxFishSize;
	}

	public static float ActiveCastRangeMul( OffshoreGameController game )
	{
		if ( game?.Player?.Mode == AnglerController.LocomotionMode.InBoat )
		{
			var boat = Equipped( game.Progression );
			if ( boat is not null )
				return boat.CastRangeMul;
		}

		return 1f;
	}

	public static void CheckAutoUnlocks( OffshoreGameController game )
	{
		_ = game;
	}

	public static void ClearLatch() => _buyLatched = false;
}
