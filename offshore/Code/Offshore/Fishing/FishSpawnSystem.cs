namespace Offshore;

/// <summary>Picks a fish for the current hook location. Single selection path for all fishing.</summary>
public static class FishSpawnSystem
{
	public static FishDefinition Select(
		string locationId,
		float hookDepth,
		BalanceConfig balance,
		PlayerProgressionData progress = null,
		IReadOnlyDictionary<string, float> proximityWeightMul = null,
		FishConditionContext conditions = null )
	{
		progress ??= OffshoreGameController.Instance?.Progression;
		conditions ??= new FishConditionContext
		{
			BaitId = progress?.SelectedBaitId ?? "worm",
			TimeOfDay = progress?.TimeOfDay ?? TimeOfDay.Day,
			Weather = progress?.Weather ?? WeatherType.Clear,
			LocationId = locationId,
			HookDepth = hookDepth
		};

		var rareBonus = balance?.RareFishBonus ?? 0f;
		var bait = conditions.BaitId ?? "worm";
		var game = OffshoreGameController.Instance;
		var boatMaxDepth = BoatSystem.ActiveMaxDepth( game );
		var boatMaxSize = BoatSystem.ActiveMaxFishSize( game );
		var offshore01 = conditions.Offshore01;

		var candidates = new List<(FishDefinition Fish, float Weight)>();
		foreach ( var fish in FishCatalog.ForLocation( locationId ) )
		{
			if ( !PassesGates( fish, locationId, hookDepth, boatMaxDepth, boatMaxSize, offshore01 ) )
				continue;

			var weight = BuildWeight( fish, bait, progress, rareBonus, proximityWeightMul, conditions );
			candidates.Add( (fish, weight) );
		}

		// Far from the pier: uncommon+ species from other zones can visit as rare strays.
		if ( offshore01 >= 0.4f )
		{
			foreach ( var fish in FishCatalog.All )
			{
				if ( string.Equals( fish.RequiredLocationId, locationId, StringComparison.OrdinalIgnoreCase ) )
					continue;
				if ( fish.Rarity < FishRarity.Uncommon )
					continue;

				var needOffshore = fish.Rarity switch
				{
					FishRarity.Uncommon => 0.4f,
					FishRarity.Rare => 0.55f,
					FishRarity.Epic => 0.7f,
					FishRarity.Legendary => 0.85f,
					_ => 1f
				};
				if ( offshore01 < needOffshore )
					continue;
				if ( !PassesGates( fish, locationId, hookDepth, boatMaxDepth, boatMaxSize, offshore01 ) )
					continue;

				var weight = BuildWeight( fish, bait, progress, rareBonus, proximityWeightMul, conditions );
				// Visitors stay scarce — distance opens the door, not a flood.
				weight *= (0.12f + 0.35f * offshore01) * (fish.IsLegendary ? 0.45f : 1f);
				candidates.Add( (fish, weight) );
			}
		}

		if ( candidates.Count == 0 )
		{
			foreach ( var fish in FishCatalog.ForLocation( locationId ) )
			{
				if ( fish.MinOffshore01 > offshore01 + 0.05f )
					continue;
				candidates.Add( (fish, fish.SpawnWeight) );
			}
		}

		if ( candidates.Count == 0 && FishCatalog.All.Count > 0 )
			return FishCatalog.All[0];

		var total = 0f;
		foreach ( var c in candidates )
			total += c.Weight;

		var roll = Game.Random.Float( 0f, total );
		var cursor = 0f;
		foreach ( var c in candidates )
		{
			cursor += c.Weight;
			if ( roll <= cursor )
				return c.Fish;
		}

		return candidates[^1].Fish;
	}

	public static FishEncounter CreateEncounter(
		FishDefinition def,
		float hookDepth,
		string locationId,
		FishConditionContext conditions = null )
	{
		conditions ??= new FishConditionContext { HookDepth = hookDepth, LocationId = locationId };

		var sizeT = Game.Random.Float( 0f, 1f );
		// Mild trophy bias — stronger the further offshore you are.
		var trophyPow = MathX.Lerp( 0.85f, 0.7f, conditions.Offshore01 );
		sizeT = MathF.Pow( sizeT, trophyPow );
		var maxSize = Math.Min( def.MaxSize, BoatSystem.ActiveMaxFishSize( OffshoreGameController.Instance ) );
		var minSize = Math.Min( def.MinSize, maxSize );
		var size = MathX.Lerp( minSize, maxSize, sizeT );
		var weightT = Math.Clamp( sizeT + Game.Random.Float( -0.1f, 0.1f ), 0f, 1f );
		var weight = MathX.Lerp( def.MinWeight, def.MaxWeight, weightT );

		var interaction = FishInteractionSystem.Evaluate( def, conditions );
		var value = FishValueCalculator.Calculate( def, size, weight, conditions, interaction.ValueMul );

		var offshorePct = (int)MathF.Round( conditions.Offshore01 * 100f );
		var note = interaction.Label;
		if ( conditions.Offshore01 >= 0.25f )
		{
			var offshoreBit = offshorePct >= 70 ? "Deep offshore" : offshorePct >= 40 ? "Offshore" : "Leaving the pier";
			note = string.IsNullOrEmpty( note ) ? $"{offshoreBit} (+{offshorePct}%)" : $"{note}  -  {offshoreBit}";
		}

		return new FishEncounter
		{
			Definition = def,
			Size = size,
			Weight = weight,
			BaseValue = def.BaseValue,
			FinalValue = value,
			HookDepth = hookDepth,
			LocationId = locationId,
			Offshore01 = conditions.Offshore01,
			BaitId = conditions.BaitId,
			TimeOfDay = conditions.TimeOfDay,
			Weather = conditions.Weather,
			ConditionNote = note ?? "",
			MaxStamina = MathF.Max( 0.35f, def.Stamina ),
			Stamina = MathF.Max( 0.35f, def.Stamina ),
			Strength = MathF.Max( 0.25f, def.Strength ) * (0.85f + 0.3f * sizeT),
			Speed = MathF.Max( 0.25f, def.Speed ),
			EscapeDifficulty = Math.Clamp( def.EscapeDifficulty, 0f, 1f )
		};
	}

	private static bool PassesGates(
		FishDefinition fish,
		string locationId,
		float hookDepth,
		float boatMaxDepth,
		float boatMaxSize,
		float offshore01 )
	{
		if ( offshore01 + 0.02f < fish.MinOffshore01 )
			return false;
		if ( hookDepth + (LocationCatalog.Get( locationId )?.DepthBias ?? 0f) < fish.MinDepth - 0.5f )
			return false;
		if ( hookDepth > fish.MaxDepth + 2f )
			return false;
		if ( fish.MinDepth > boatMaxDepth + 0.75f )
			return false;
		if ( fish.MinSize > boatMaxSize + 0.05f )
			return false;
		return true;
	}

	private static float BuildWeight(
		FishDefinition fish,
		string bait,
		PlayerProgressionData progress,
		float rareBonus,
		IReadOnlyDictionary<string, float> proximityWeightMul,
		FishConditionContext conditions )
	{
		var weight = MathF.Max( 0.01f, fish.SpawnWeight );
		weight *= BaitSystem.WeightMultiplier( bait, fish );
		if ( progress is not null )
			weight *= TimeWeatherSystem.SpawnWeightMultiplier( progress, fish );

		var interaction = FishInteractionSystem.Evaluate( fish, conditions );
		weight *= interaction.SpawnMul;
		weight *= OffshoreDistance.RaritySpawnMultiplier( fish.Rarity, conditions.Offshore01 );

		// Species that only appear far from the dock get an extra push once unlocked.
		if ( fish.MinOffshore01 > 0.05f )
			weight *= 1f + (conditions.Offshore01 - fish.MinOffshore01) * 1.4f;

		if ( fish.Rarity >= FishRarity.Rare )
			weight *= 1f + rareBonus;

		if ( fish.IsLegendary && progress is not null )
		{
			var finder = progress.UpgradeLevels.TryGetValue( "finder", out var fl ) ? fl : 0;
			if ( finder < 1 && bait != "deep_lure" )
				weight *= 0.15f;
			if ( progress.TimeOfDay != TimeOfDay.Dusk && progress.TimeOfDay != TimeOfDay.Night && fish.Id == "great_white" )
				weight *= 0.4f;
		}

		if ( proximityWeightMul is not null
		     && proximityWeightMul.TryGetValue( fish.Id, out var proxMul )
		     && proxMul > 1f )
			weight *= proxMul;

		return MathF.Max( 0.001f, weight );
	}
}
