namespace FinalOutpost;



/// <summary>Cure-mode colony sim tick — food upkeep, civic building output, hospital healing.</summary>

public sealed class ColonyEconomy : Component

{

	public static ColonyEconomy Instance { get; private set; }



	protected override void OnAwake() => Instance = this;



	protected override void OnDestroy()

	{

		if ( Instance == this ) Instance = null;

	}



	protected override void OnUpdate()

	{

		var core = GameCore.Instance;

		if ( core is null || !core.IsCure ) return;

		if ( core.Phase is not GamePhase.Day and not GamePhase.Night ) return;



		var dt = Time.Delta;

		TickProduction( core, dt );

		TickUpkeep( core, dt );

	}



	private static void TickProduction( GameCore core, float dt )

	{

		var buildings = BuildManager.Instance?.Buildings;

		if ( buildings is null ) return;



		var farms = 0;

		var factories = 0;

		var libraries = 0;

		var schools = 0;

		var hospitals = 0;

		var shops = 0;



		foreach ( var b in buildings )

		{

			if ( b.IsDestroyed ) continue;

			switch ( b.Type )

			{

				case BuildableId.Farm: farms++; break;

				case BuildableId.Factory: factories++; break;

				case BuildableId.Library: libraries++; break;

				case BuildableId.School: schools++; break;

				case BuildableId.Hospital: hospitals++; break;

				case BuildableId.Shop: shops++; break;

			}

		}



		var labMult = TechTreeCatalog.IsUnlocked( core.Save, "synthesis" ) ? 1.25f : 1f;

		var healMult = TeamBonuses.HealRateMult( core );



		if ( farms > 0 )

			core.Resources.Add( ResourceKind.Food, farms * CureConstants.FarmFoodPerSec * dt );



		if ( factories > 0 )

		{

			core.Resources.Add( ResourceKind.Supplies, factories * CureConstants.FactorySuppliesPerSec * dt );

			core.Resources.Add( ResourceKind.Food, factories * CureConstants.FactoryFoodPerSec * dt );

		}



		if ( libraries > 0 )

			core.Resources.Add( ResourceKind.Knowledge, libraries * CureConstants.LibraryKnowledgePerSec * dt * labMult );



		if ( schools > 0 )

			core.Resources.Add( ResourceKind.Knowledge, schools * CureConstants.SchoolKnowledgePerSec * dt * labMult );



		if ( shops > 0 )

		{

			var scrap = shops * CureConstants.ShopScrapPerSec * dt;

			if ( scrap >= 0.01 )

				core.Wallet.Earn( scrap );

		}



		if ( hospitals > 0 )

		{

			var heal = CureConstants.HospitalRecruitHealPerSec * healMult * dt;

			var defenders = DefenderManager.Instance;

			foreach ( var b in buildings )

			{

				if ( b.IsDestroyed || b.Type != BuildableId.Hospital ) continue;

				defenders?.HealInRadius( b.WorldPosition, CureConstants.HospitalHealRadius, heal );

			}



			if ( core.Save.ColonySickness > 0 )

			{

				var sicknessHeal = hospitals * CureConstants.HospitalSicknessHealPerSec * healMult * dt;

				core.Save.ColonySickness = MathF.Max( 0f, core.Save.ColonySickness - sicknessHeal );

			}

		}

	}



	private static void TickUpkeep( GameCore core, float dt )

	{

		var population = (DefenderManager.Instance?.Count ?? 0) + (WorkerManager.Instance?.Count ?? 0);

		if ( population <= 0 ) return;



		var foodNeed = population * 0.08 * dt;

		if ( core.Resources.Get( ResourceKind.Food ) >= foodNeed )

		{

			core.Resources.TrySpend( ResourceKind.Food, foodNeed );

			return;

		}



		core.Save.ColonySickness = MathF.Min( CureConstants.MaxSickness,

			core.Save.ColonySickness + 0.002f * dt * population );

	}

}


