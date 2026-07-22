namespace FinalOutpost;

/// <summary>Cure-mode colony sim — production, food/scrap upkeep, hospital healing.</summary>
public sealed class ColonyEconomy : Component
{
	public static ColonyEconomy Instance { get; private set; }

	private bool _lowFoodWarned;

	public static int Population =>
		(DefenderManager.Instance?.Count ?? 0) + (WorkerManager.Instance?.Count ?? 0);

	public static int BuildingCount
	{
		get
		{
			var n = 0;
			foreach ( var b in BuildManager.Instance?.Buildings ?? Array.Empty<PlacedBuilding>() )
			{
				if ( !b.IsDestroyed ) n++;
			}
			return n;
		}
	}

	/// <summary>Net food demand per second (always drains; farms/farmers offset this).</summary>
	public static float FoodDrainPerSec() =>
		CureConstants.BaseFoodDrainPerSec
		+ Population * CureConstants.FoodPerPersonPerSec;

	/// <summary>Scrap maintenance burn — grows with people and buildings.</summary>
	public static float ScrapUpkeepPerSec() =>
		CureConstants.BaseScrapUpkeepPerSec
		+ Population * CureConstants.ScrapPerPersonPerSec
		+ BuildingCount * CureConstants.ScrapPerBuildingPerSec;

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
	}

	protected override void OnUpdate()
	{
		var core = GameCore.Instance;
		if ( core is null || !core.IsCure ) return;
		if ( core.IsUiBlocking ) return;
		if ( core.Phase is not GamePhase.Day and not GamePhase.Night ) return;

		var dt = Time.Delta;
		TickProduction( core, dt );
		TickFoodUpkeep( core, dt );
		TickScrapUpkeep( core, dt );
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
		var observatories = 0;
		var universities = 0;

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
				case BuildableId.Observatory: observatories++; break;
				case BuildableId.University: universities++; break;
			}
		}

		var labMult = TechTreeCatalog.IsUnlocked( core.Save, "synthesis" ) ? 1.25f : 1f;
		var healMult = TeamBonuses.HealRateMult( core );
		var agriculture = TechTreeCatalog.IsUnlocked( core.Save, "agriculture" );

		// Farms only produce after Agriculture (even if a farm somehow exists earlier).
		if ( agriculture && farms > 0 )
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

		if ( observatories > 0 )
			core.Resources.Add( ResourceKind.Knowledge, observatories * CureConstants.ObservatoryKnowledgePerSec * dt * labMult );

		if ( universities > 0 )
			core.Resources.Add( ResourceKind.Knowledge, universities * CureConstants.UniversityKnowledgePerSec * dt * labMult );

		core.Resources.Add( ResourceKind.Knowledge, CureConstants.BaseKnowledgePerSec * dt * labMult );

		var allies = PlotCivActions.AlliedCount( core.Save );
		if ( allies > 0 )
		{
			core.Resources.Add( ResourceKind.Food, allies * CureConstants.AllianceFoodPerSec * dt );
			var allyScrap = allies * CureConstants.AllianceScrapPerSec * dt;
			if ( allyScrap >= 0.01 )
				core.Wallet.Earn( allyScrap );
		}

		if ( shops > 0 )
		{
			var scrap = shops * CureConstants.ShopScrapPerSec * dt;
			if ( scrap >= 0.01 )
				core.Wallet.Earn( scrap );
		}

		var boostScrap = PlotBoosts.ScrapPerSec( core.Save ) * dt;
		if ( boostScrap >= 0.01 )
			core.Wallet.Earn( boostScrap, applyIncomeScale: false );

		core.Resources.Add( ResourceKind.Food, PlotBoosts.FoodPerSec( core.Save ) * dt );
		core.Resources.Add( ResourceKind.Knowledge, PlotBoosts.KnowledgePerSec( core.Save ) * dt * labMult );

		if ( PlotBoosts.SicknessHealPerSec( core.Save ) > 0 && core.Save.ColonySickness > 0 )
		{
			core.Save.ColonySickness = MathF.Max( 0f,
				core.Save.ColonySickness - PlotBoosts.SicknessHealPerSec( core.Save ) * dt );
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

	private void TickFoodUpkeep( GameCore core, float dt )
	{
		var foodNeed = FoodDrainPerSec() * dt;
		if ( foodNeed <= 0 ) return;

		if ( core.Resources.Get( ResourceKind.Food ) >= foodNeed )
		{
			core.Resources.TrySpend( ResourceKind.Food, foodNeed );
			MaybeWarnLowFood( core );
			return;
		}

		var left = core.Resources.Get( ResourceKind.Food );
		if ( left > 0 )
			core.Resources.TrySpend( ResourceKind.Food, left );

		MaybeWarnLowFood( core );

		var population = Math.Max( 1, Population );
		core.Save.ColonySickness = MathF.Min( CureConstants.MaxSickness,
			core.Save.ColonySickness + 0.002f * dt * population );
	}

	private void MaybeWarnLowFood( GameCore core )
	{
		var food = core.Resources.Get( ResourceKind.Food );
		if ( food >= CureConstants.FoodLowToastThreshold )
		{
			_lowFoodWarned = false;
			return;
		}

		if ( _lowFoodWarned )
			return;

		_lowFoodWarned = true;
		core.ShowToast( food <= 0.01
			? "Out of food — colony sickness is rising!"
			: $"Food low ({food:0}) — build farms after Agriculture" );
	}

	private static void TickScrapUpkeep( GameCore core, float dt )
	{
		var burn = ScrapUpkeepPerSec() * dt;
		if ( burn <= 0 ) return;
		core.Wallet.Drain( burn );
	}
}
