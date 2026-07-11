namespace FinalOutpost;

/// <summary>Net passive income rates for the top-bar resource display.</summary>
public static class ColonyIncomeRates
{
	public readonly struct Snapshot
	{
		public float ScrapPerSec { get; init; }
		public float WoodPerSec { get; init; }
		public float StonePerSec { get; init; }
		public float WaterPerSec { get; init; }
		public float FoodPerSec { get; init; }
		public float SuppliesPerSec { get; init; }
		public float KnowledgePerSec { get; init; }
	}

	public static Snapshot Get( GameCore core )
	{
		if ( core?.Save is null || core.Phase is not GamePhase.Day and not GamePhase.Night )
			return default;

		var scrap = 0f;
		var wood = 0f;
		var stone = 0f;
		var water = 0f;
		var food = 0f;
		var supplies = 0f;
		var knowledge = 0f;

		if ( core.IsCure && OutpostManager.Instance?.CoreHealth > 0f )
			scrap += CureConstants.CommandPostScrapPerSec;

		var incomeMult = (float)GameConstants.ScrapIncomeMult;
		var farms = 0;
		var factories = 0;
		var libraries = 0;
		var schools = 0;
		var shops = 0;

		foreach ( var b in BuildManager.Instance?.Buildings ?? Array.Empty<PlacedBuilding>() )
		{
			if ( b.IsDestroyed ) continue;
			switch ( b.Type )
			{
				case BuildableId.Farm: farms++; break;
				case BuildableId.Factory: factories++; break;
				case BuildableId.Library: libraries++; break;
				case BuildableId.School: schools++; break;
				case BuildableId.Shop: shops++; break;
			}
		}

		if ( core.IsCure )
		{
			var labMult = TechTreeCatalog.IsUnlocked( core.Save, "synthesis" ) ? 1.25f : 1f;

			food += farms * CureConstants.FarmFoodPerSec;
			supplies += factories * CureConstants.FactorySuppliesPerSec;
			food += factories * CureConstants.FactoryFoodPerSec;
			knowledge += libraries * CureConstants.LibraryKnowledgePerSec * labMult;
			knowledge += schools * CureConstants.SchoolKnowledgePerSec * labMult;
			scrap += shops * CureConstants.ShopScrapPerSec * incomeMult;

			var population = (DefenderManager.Instance?.Count ?? 0) + (WorkerManager.Instance?.Count ?? 0);
			if ( population > 0 )
				food -= population * 0.08f;
		}

		foreach ( var u in WorkerManager.Instance?.Units ?? Array.Empty<WorkerManager.WorkerUnit>() )
		{
			switch ( u.Role )
			{
				case WorkerRole.Forager:
					AddForagerRate( core, u, ref wood, ref stone, ref water );
					break;
				case WorkerRole.Craftsman:
					if ( !core.IsCure )
						scrap += GameConstants.CraftsmanConvertPerSec
							* (float)GameConstants.CraftsmanScrapPerResource * incomeMult;
					break;
				case WorkerRole.Farmer:
					food += CureConstants.FarmerFoodPerSec;
					break;
				case WorkerRole.Scholar:
					knowledge += CureConstants.ScholarKnowledgePerSec
						* (TechTreeCatalog.IsUnlocked( core.Save, "synthesis" ) ? 1.25f : 1f);
					break;
				case WorkerRole.Operator:
					supplies += CureConstants.OperatorSuppliesPerSec;
					break;
				case WorkerRole.Merchant:
					if ( core.IsCure )
						scrap += CureConstants.MerchantScrapPerSec * incomeMult;
					break;
			}
		}

		return new Snapshot
		{
			ScrapPerSec = scrap,
			WoodPerSec = wood,
			StonePerSec = stone,
			WaterPerSec = water,
			FoodPerSec = food,
			SuppliesPerSec = supplies,
			KnowledgePerSec = knowledge
		};
	}

	private static void AddForagerRate(
		GameCore core,
		WorkerManager.WorkerUnit unit,
		ref float wood,
		ref float stone,
		ref float water )
	{
		if ( !unit.HasPlot || !unit.IsWorking ) return;

		var rate = GameConstants.ForagerHarvestPerSec;
		if ( core.IsCure )
		{
			rate *= TeamBonuses.ForagerYieldMult( core );
			var sickness = core.Save.ColonySickness / CureConstants.MaxSickness;
			rate *= MathF.Max( 0.4f, 1f - sickness * CureConstants.SicknessWorkerPenalty * 100f );
		}

		switch ( unit.PlotResource )
		{
			case ResourceKind.Wood: wood += rate; break;
			case ResourceKind.Stone: stone += rate; break;
			case ResourceKind.Water: water += rate; break;
		}
	}

	public static string FormatRate( float rate )
	{
		if ( MathF.Abs( rate ) < 0.05f )
			return null;

		var sign = rate >= 0f ? "+" : "";
		var abs = MathF.Abs( rate );
		var text = abs >= 10f ? abs.ToString( "0" ) : abs.ToString( "0.#" );
		return $"{sign}{text}/s";
	}
}
