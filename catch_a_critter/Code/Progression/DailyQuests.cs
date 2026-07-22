namespace CatchACritter;

/// <summary>Deterministic daily quest generation + progress hooks.</summary>
public static class DailyQuestGen
{
	public static void EnsureToday( SaveData data )
	{
		var today = DateTime.UtcNow.ToString( "yyyy-MM-dd" );
		if ( data.DailyDate == today && data.DailyQuests.Count > 0 ) return;

		data.DailyDate = today;
		data.DailyQuests = Generate( data );
	}

	static List<QuestData> Generate( SaveData data )
	{
		var rng = new Random( DateTime.UtcNow.Date.GetHashCode() ^ 7127 );
		var quests = new List<QuestData>();

		var unlocked = BiomeCatalog.All
			.Where( b => data.UnlockedZones.Contains( b.Id.ToString() ) )
			.ToList();
		var scale = 1.0 + data.Crowns * 0.8;

		// 1. Volume quest — always achievable.
		quests.Add( new QuestData { Kind = QuestKind.CatchAny, Target = 12 + rng.Next( 10 ) } );

		// 2. Biome-targeted quest in an unlocked zone.
		var biome = unlocked[rng.Next( unlocked.Count )];
		quests.Add( new QuestData { Kind = QuestKind.CatchBiome, Param = (int)biome.Id, Target = 6 + rng.Next( 5 ) } );

		// 3. Variety slot.
		switch ( rng.Next( 3 ) )
		{
			case 0:
				quests.Add( new QuestData { Kind = QuestKind.CatchRare, Param = (int)Rarity.Rare, Target = 2 + rng.Next( 2 ) } );
				break;
			case 1:
				var best = unlocked[^1];
				quests.Add( new QuestData { Kind = QuestKind.SellCoins, Target = Math.Max( 200, best.BaseValue * 45 * scale ) } );
				break;
			default:
				quests.Add( new QuestData { Kind = QuestKind.HatchEgg, Target = 1 } );
				break;
		}

		return quests;
	}

	public static void OnCatch( SaveData data, SpeciesDef def, bool shiny )
	{
		foreach ( var q in data.DailyQuests )
		{
			if ( q.Claimed ) continue;
			switch ( q.Kind )
			{
				case QuestKind.CatchAny: q.Progress++; break;
				case QuestKind.CatchBiome when (int)def.Biome == q.Param: q.Progress++; break;
				case QuestKind.CatchRare when (int)def.Rarity >= q.Param: q.Progress++; break;
			}
		}
	}

	public static void OnSell( SaveData data, double coins )
	{
		foreach ( var q in data.DailyQuests )
			if ( !q.Claimed && q.Kind == QuestKind.SellCoins )
				q.Progress += coins;
	}

	public static void OnHatch( SaveData data )
	{
		foreach ( var q in data.DailyQuests )
			if ( !q.Claimed && q.Kind == QuestKind.HatchEgg )
				q.Progress++;
	}

	public static double CoinReward( SaveData data, QuestData quest )
	{
		var unlocked = BiomeCatalog.All
			.Where( b => data.UnlockedZones.Contains( b.Id.ToString() ) )
			.ToList();
		var best = unlocked[^1];
		return best.BaseValue * 30 * (1.0 + data.Crowns * 0.5);
	}

	public static void Claim( PlayerProgress progress, QuestData quest )
	{
		if ( quest.Claimed || !quest.Complete ) return;
		quest.Claimed = true;

		var coins = CoinReward( progress.Data, quest );
		progress.AddCoins( coins );
		progress.Data.Gems += Balance.DailyGemReward;
		progress.AddToast( $"Quest complete! +{Balance.Fmt( coins )} coins, +{Balance.DailyGemReward} 💎", "🎁" );
		Sfx.Play( "unlock" );
		progress.RequestSave();
	}
}
