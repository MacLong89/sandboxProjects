namespace DeepDive;

public sealed class SellSystem
{
	private bool _soldThisOutcome;
	private readonly List<HaulItemRecord> _pendingItems = new();

	public void ResetSaleLatch()
	{
		_soldThisOutcome = false;
		_pendingItems.Clear();
	}

	/// <summary>Build recap preview without paying out yet.</summary>
	public DiveSummary BuildSuccessfulPreview(
		DiveRunState run,
		HaulInventory haul,
		PlayerProgressionData progression,
		BalanceConfig balance,
		float oxygenFractionRemaining )
	{
		_pendingItems.Clear();
		_pendingItems.AddRange( haul.Items );

		var haulValue = haul.TotalValue + run.BankedHaulValue;
		var bonuses = new List<DiveBonusLine>();
		var bonusTotal = 0f;

		if ( run.BankedHaulValue > 0.5f )
		{
			// Included in haulValue — no separate bonus line to avoid double-count.
		}

		var fullBonus = haul.IsFull && haul.ItemCount > 0;
		if ( run.BrokeDepthRecord )
		{
			var amt = 100f;
			bonuses.Add( new DiveBonusLine { Label = "New Depth Record", Amount = amt } );
			bonusTotal += amt;
		}

		if ( fullBonus )
		{
			var amt = MathF.Round( haulValue * 0.15f );
			bonuses.Add( new DiveBonusLine { Label = "Full Haul", Amount = amt } );
			bonusTotal += amt;
		}

		var damageFrac = balance.MaxHealth > 0.001f
			? run.DamageTaken / balance.MaxHealth
			: 0f;
		if ( damageFrac < 0f ) damageFrac = 0f;
		if ( damageFrac > 1f ) damageFrac = 1f;

		var lowDamage = damageFrac <= 0.2f;
		if ( lowDamage )
		{
			var amt = 50f;
			bonuses.Add( new DiveBonusLine { Label = "Low Damage", Amount = amt } );
			bonusTotal += amt;
		}

		if ( oxygenFractionRemaining >= 0.15f )
		{
			var amt = MathF.Round( 40f + oxygenFractionRemaining * 80f );
			bonuses.Add( new DiveBonusLine
			{
				Label = $"Oxygen Remaining ({(int)(oxygenFractionRemaining * 100f)}%)",
				Amount = amt
			} );
			bonusTotal += amt;
		}

		if ( run.PhotoBonusValue > 0.5f )
		{
			var amt = MathF.Round( run.PhotoBonusValue );
			bonuses.Add( new DiveBonusLine { Label = "Photo Survey", Amount = amt } );
			bonusTotal += amt;
		}

		if ( run.ObjectiveRewardPending || DeepDiveGame.Instance?.Objectives?.AllComplete == true )
		{
			var objs = DeepDiveGame.Instance?.Objectives;
			if ( objs is not null )
			{
				var before = bonuses.Count;
				objs.TryGrantReward( progression, bonuses );
				if ( bonuses.Count > before )
					bonusTotal += objs.RewardGold;
			}
		}

		var top = haul.Items
			.OrderByDescending( i => i.Value )
			.Take( 5 )
			.Select( i => new DiveTopItem
			{
				Name = i.DisplayName,
				Value = i.Value,
				Rarity = i.Rarity
			} )
			.ToList();

		var oxygenUsed = 1f - oxygenFractionRemaining;
		if ( oxygenUsed < 0f ) oxygenUsed = 0f;
		if ( oxygenUsed > 1f ) oxygenUsed = 1f;

		var level = Math.Max( 1, progression.SuccessfulDives );
		var xpInLevel = (progression.SuccessfulDives * 180 + (int)run.MaxDepthMeters) % 3200;

		var artifactIds = progression.DiscoveredCollectibles
			.Concat( haul.Items.Select( i => i.CollectibleId ) )
			.Where( id => !string.IsNullOrWhiteSpace( id ) )
			.Distinct()
			.Count();

		return new DiveSummary
		{
			Success = true,
			DiveNumber = progression.TotalDives,
			MaxDepthMeters = run.MaxDepthMeters,
			DurationSeconds = run.DiveDurationSeconds,
			ItemCount = haul.ItemCount,
			HaulValue = haulValue,
			BonusValue = bonusTotal,
			MoneyEarned = haulValue + bonusTotal,
			BrokeDepthRecord = run.BrokeDepthRecord,
			FullHaulBonus = fullBonus,
			LowDamageBonus = lowDamage,
			OxygenRemainingBonus = oxygenFractionRemaining >= 0.15f,
			OxygenUsedFraction = oxygenUsed,
			OxygenRemainingFraction = oxygenFractionRemaining,
			DamageTakenFraction = damageFrac,
			TopItems = top,
			Bonuses = bonuses,
			ArtifactsFound = artifactIds,
			ArtifactsTotal = CollectibleCatalog.All.Count,
			LocationsFound = progression.DiscoveredZones.Count,
			LocationsTotal = 6,
			CreaturesFound = progression.DiscoveredCreatures.Count,
			CreaturesTotal = CreatureCatalog.TotalCount,
			ProgressionLevel = level,
			ProgressionXp = xpInLevel,
			ProgressionXpMax = 3200,
			NextUnlock = run.MaxDepthMeters >= 200f ? "Hadal Submersible" : "Mini Sub",
			Headline = "Dive complete",
			SaleCommitted = false
		};
	}

	public DiveSummary CommitSuccessfulSale( DiveSummary preview, HaulInventory haul, PlayerProgressionData progression )
	{
		if ( preview is null )
			return null;

		if ( _soldThisOutcome || preview.SaleCommitted )
			return preview;

		_soldThisOutcome = true;
		preview.SaleCommitted = true;

		progression.AddMoney( preview.MoneyEarned );
		DeepDiveGame.Instance?.Objectives?.GrantShellReward( progression );

		foreach ( var item in _pendingItems )
			progression.RegisterDiscovery( item.CollectibleId );

		_pendingItems.Clear();
		haul.Clear();

		preview.Headline = preview.MoneyEarned > 0f
			? $"Sold for ${preview.MoneyEarned:0}"
			: "Empty haul";

		return preview;
	}

	/// <summary>Leave without selling — haul is discarded.</summary>
	public void AbandonPendingHaul( HaulInventory haul )
	{
		_pendingItems.Clear();
		haul.Clear();
		_soldThisOutcome = true;
	}

	public DiveSummary SettleFailedDive( DiveRunState run, HaulInventory haul, PlayerProgressionData progression, BalanceConfig balance )
	{
		_soldThisOutcome = true;
		_pendingItems.Clear();

		var lost = haul.TotalValue;
		var count = haul.ItemCount;
		var top = haul.Items
			.OrderByDescending( i => i.Value )
			.Take( 5 )
			.Select( i => new DiveTopItem { Name = i.DisplayName, Value = i.Value, Rarity = i.Rarity } )
			.ToList();

		var damageFrac = balance.MaxHealth > 0.001f ? run.DamageTaken / balance.MaxHealth : 0f;
		if ( damageFrac < 0f ) damageFrac = 0f;
		if ( damageFrac > 1f ) damageFrac = 1f;

		haul.Clear();

		return new DiveSummary
		{
			Success = false,
			FailureReason = run.FailureReason,
			DiveNumber = progression.TotalDives,
			MaxDepthMeters = run.MaxDepthMeters,
			DurationSeconds = run.DiveDurationSeconds,
			ItemCount = count,
			HaulValue = lost,
			MoneyEarned = 0f,
			BrokeDepthRecord = run.BrokeDepthRecord,
			DamageTakenFraction = damageFrac,
			OxygenUsedFraction = 1f,
			TopItems = top,
			Bonuses = new List<DiveBonusLine>(),
			ArtifactsFound = progression.DiscoveredCollectibles.Count,
			ArtifactsTotal = CollectibleCatalog.All.Count,
			LocationsFound = progression.DiscoveredZones.Count,
			LocationsTotal = 6,
			CreaturesFound = progression.DiscoveredCreatures.Count,
			CreaturesTotal = CreatureCatalog.TotalCount,
			ProgressionLevel = Math.Max( 1, progression.SuccessfulDives ),
			ProgressionXp = 0,
			ProgressionXpMax = 3200,
			NextUnlock = "Survive & sell a haul",
			Headline = "Haul lost",
			SaleCommitted = true
		};
	}
}
