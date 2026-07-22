namespace SkyEmpire;

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
		var rng = new Random( DateTime.UtcNow.Date.GetHashCode() ^ 9241 );
		var quests = new List<QuestData>();

		// Scale cash targets to where the player actually is.
		var rate = Math.Max( 0.4, PurchaseCatalog.IncomePerSecond( data.Purchased, RebirthCatalog.IncomeMult( data.Rebirths ) ) );

		quests.Add( new QuestData { Kind = QuestKind.CollectOrbs, Target = 80 + rng.Next( 60 ) } );
		quests.Add( new QuestData { Kind = QuestKind.EarnCash, Target = Math.Max( 500, rate * (500 + rng.Next( 400 )) ) } );

		if ( rng.Next( 2 ) == 0 )
			quests.Add( new QuestData { Kind = QuestKind.GoldenOrbs, Target = 2 + rng.Next( 3 ) } );
		else
			quests.Add( new QuestData { Kind = QuestKind.BuyThings, Target = 4 + rng.Next( 4 ) } );

		return quests;
	}

	public static void OnOrb( SaveData data, double value, bool golden )
	{
		foreach ( var q in data.DailyQuests )
		{
			if ( q.Claimed ) continue;
			switch ( q.Kind )
			{
				case QuestKind.CollectOrbs: q.Progress++; break;
				case QuestKind.EarnCash: q.Progress += value; break;
				case QuestKind.GoldenOrbs when golden: q.Progress++; break;
			}
		}
	}

	public static void OnBuy( SaveData data )
	{
		foreach ( var q in data.DailyQuests )
			if ( !q.Claimed && q.Kind == QuestKind.BuyThings )
				q.Progress++;
	}

	public static double CashReward( SaveData data )
	{
		var rate = Math.Max( 0.4, PurchaseCatalog.IncomePerSecond( data.Purchased, RebirthCatalog.IncomeMult( data.Rebirths ) ) );
		return Math.Max( 400, rate * 240 );
	}

	public static void Claim( PlayerProgress progress, QuestData quest )
	{
		if ( quest.Claimed || !quest.Complete ) return;
		quest.Claimed = true;

		var cash = CashReward( progress.Data );
		progress.Data.Cash += cash;
		progress.Data.LifetimeCash += cash;
		progress.Data.Gems += Balance.DailyGemReward;
		progress.AddToast( $"Quest complete! +{Balance.Fmt( cash )} cash, +{Balance.DailyGemReward} 💎" );
		Sfx.Play( "quest" );
		progress.RequestSave();
	}
}
