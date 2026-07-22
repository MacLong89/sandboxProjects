namespace FinalOutpost;

/// <summary>
/// Contextual coach marks for early Road to a Cure seasons — seasons, tech, civic buildings,
/// specialists, and cure research.
/// </summary>
public static class CureTutorialTips
{
	public const int MaxSeason = 8;

	public static readonly IReadOnlyList<TutorialTipDef> All = new List<TutorialTipDef>
	{
		new()
		{
			Id = "cure_welcome", MinNight = 1, MaxNight = 1, Priority = 100, Icon = "coronavirus",
			Title = "Road to a Cure",
			Body = "Seasons advance on their own. Zombie threats arrive between days — build defenses, "
				+ "grow your colony, and research the cure before sickness overwhelms everyone."
		},
		new()
		{
			Id = "cure_calendar", MinNight = 1, MaxNight = 2, Priority = 95, Icon = "calendar_month",
			Title = "Watch the calendar",
			Body = "The badge at the top shows your team, year, season, and day. "
				+ "Winter brings tougher threats and faster sickness — prepare before it arrives."
		},
		new()
		{
			Id = "cure_resources", MinNight = 1, MaxNight = 3, Priority = 92, Icon = "inventory_2",
			Title = "Colony resources",
			Body = "Scrap, wood, and stone pay for buildings. Every person and building burns scrap upkeep — "
				+ "grow too fast and you'll go into debt until Commerce (Shops/Merchants) covers it. "
				+ "Food drains with population; Farms keep you fed. Knowledge unlocks the Tech Tree."
		},
		new()
		{
			Id = "cure_defense", MinNight = 1, MaxNight = 3, Priority = 90, Icon = "adjust",
			Title = "Place defenses",
			Body = "Select Gun Tower or Wall Segment in the build dock and click open ground. "
				+ "Advanced towers unlock through the Tech Tree. Towers auto-fire during threats."
		},
		new()
		{
			Id = "cure_threats", MinNight = 1, MaxNight = 2, Priority = 88, Icon = "warning",
			Title = "Threats arrive automatically",
			Body = "You do not start nights manually. After enough days pass, a threat wave hits. "
				+ "Repair damage afterward and keep recruits near the Command Post."
		},
		new()
		{
			Id = "cure_barracks", MinNight = 1, MaxNight = 4, Priority = 85, Icon = "groups",
			Title = "Build a Barracks first",
			Body = "Recruits require a Barracks. Each one holds up to 3 soldiers, heals them during the day, "
				+ "and fully restores nearby defenders after a threat. Fallen recruits are gone for good. "
				+ "Once you have a Recruit, you'll have the ability to play in First Person as one of them during the nights!"
		},
		new()
		{
			Id = "cure_recruit", MinNight = 1, MaxNight = 4, Priority = 82, Icon = "military_tech",
			Title = "Train recruits",
			Body = "With a Barracks built, open Recruits in the build dock. "
				// AUDIT FIX M2: tip said Right-click; UnitOrderController is wired on Attack1 (LMB) in BuildManager.
				+ "Left-click the ground with a recruit selected to move them — useful for plugging gaps after a threat."
		},
		new()
		{
			Id = "cure_knowledge_early", MinNight = 1, MaxNight = 4, Priority = 81, Icon = "lightbulb",
			Title = "Earn Knowledge first",
			Body = "Knowledge ticks up slowly on its own, and you gain more by building, claiming/clearing plots, hiring, and surviving threats. "
				+ "Tech Ruins (+50) and allying with neighbors (+25) are big early boosts. Click Knowledge or Tech to research Agriculture."
		},
		new()
		{
			Id = "cure_lab", MinNight = 1, MaxNight = 4, Priority = 80, Icon = "science",
			Title = "Research Lab",
			Body = "Research Literacy to unlock Research Labs. Labs generate cure lab points for the Cure panel — "
				+ "not Farms or Factories. Scholars and higher lab levels boost output. Spend lab points under Cure, not Tech."
		},
		new()
		{
			Id = "cure_tech", MinNight = 1, MaxNight = 3, Priority = 78, Icon = "account_tree",
			Title = "Tech tree",
			Body = "Open Tech (or click the Knowledge counter). Almost everything beyond starter defenses unlocks here — "
				+ "towers, weapons, civic buildings, and specialists. Path: Agriculture → Industry, or Field Tactics for guns."
		},
		new()
		{
			Id = "cure_farm", MinNight = 2, MaxNight = 5, Priority = 76, Icon = "agriculture",
			Title = "Farm & Farmers",
			Body = $"Each Farm produces +{CureConstants.FarmFoodPerSec:0.#} food/s. Food always drains "
				+ $"(base + {CureConstants.FoodPerPersonPerSec:0.##}/s per worker and recruit). "
				+ "Hire Farmers for more output. Starvation raises colony sickness."
		},
		new()
		{
			Id = "cure_factory", MinNight = 2, MaxNight = 6, Priority = 74, Icon = "factory",
			Title = "Factory & Operators",
			Body = "Factories produce supplies and a little food, and speed up repair jobs. "
				+ "Operators add supplies per second and help the repair queue — unlock with Industry tech."
		},
		new()
		{
			Id = "cure_knowledge", MinNight = 3, MaxNight = 7, Priority = 72, Icon = "menu_book",
			Title = "Library & School",
			Body = "Libraries and Schools boost Knowledge income for the tech tree. "
				+ "Scholars add Knowledge per second and also boost Research Lab output."
		},
		new()
		{
			Id = "cure_hospital", MinNight = 3, MaxNight = 7, Priority = 70, Icon = "local_hospital",
			Title = "Hospital & Medics",
			Body = "Hospitals heal injured recruits nearby and reduce colony sickness. "
				+ "Medics actively heal soldiers around the base — unlock with Medicine tech."
		},
		new()
		{
			Id = "cure_shop", MinNight = 3, MaxNight = 7, Priority = 68, Icon = "storefront",
			Title = "Shop & Merchants",
			Body = "As your colony grows, scrap upkeep climbs. Shops and Merchants (Commerce tech) are how you "
				+ "stay in the black — unlock after Industry."
		},
		new()
		{
			Id = "cure_workers", MinNight = 2, MaxNight = 6, Priority = 66, Icon = "engineering",
			Title = "Specialist workers",
			Body = "Open Workers in the build dock to hire colony specialists. Each role boosts a resource "
				+ "or service — Farmers, Scholars, Operators, Medics, and Merchants match their building lines."
		},
		new()
		{
			Id = "cure_repair", MinNight = 2, MaxNight = 5, Priority = 64, Icon = "build",
			Title = "Repair after threats",
			Body = "Use Repair All in the build dock or click damaged structures. "
				+ "Operators and Factories finish repairs faster."
		},
		new()
		{
			Id = "cure_cure_tiers", MinNight = 2, MaxNight = 8, Priority = 62, Icon = "biotech",
			Title = "Cure research tiers",
			Body = "Open Cure in the top bar (or Research in the dock) to spend lab points on cure tiers. "
				+ "Labs feed this track — civic buildings still come from the Tech Tree."
		},
		new()
		{
			Id = "cure_scouts", MinNight = 2, MaxNight = 6, Priority = 60, Icon = "explore",
			Title = "Send scouts",
			Body = "Research Field Tactics for short scout trips, then Diplomacy for long expeditions. "
				+ "Scouts bring scrap, resources, and specimens — essential for later cure tiers."
		},
		new()
		{
			Id = "cure_sickness", MinNight = 3, MaxNight = 8, Priority = 58, Icon = "sick",
			Title = "Colony sickness",
			Body = "Sickness rises each season and spikes after threats. High sickness slows workers and research. "
				+ "Hospitals, Medics, and your team choice help keep it in check."
		},
		new()
		{
			Id = "cure_plots", MinNight = 4, MaxNight = 8, Priority = 55, Icon = "map",
			Title = "Expand your territory",
			Body = "Click frontier plots to claim land for more towers and civic buildings. "
				+ "Special plots may hold food caches, ruins, neutral colonies, or boss lairs."
		},
		new()
		{
			Id = "cure_done", MinNight = 7, MaxNight = 8, Priority = 45, Icon = "check_circle",
			Title = "You know the basics",
			Body = "Keep researching tech, staffing specialists, and pushing cure tiers. "
				+ "Press H any time to hide these tips on future runs."
		}
	};

	public static bool ShouldRun( SaveData save ) =>
		save is not null && !save.HideTutorialTips;

	public static TutorialTipDef PickNext( GameCore core )
	{
		if ( core?.Save is null || !ShouldRun( core.Save ) )
			return null;

		var season = CureConstants.ProgressSeason( core.Save );
		if ( season > MaxSeason )
			return null;

		if ( core.Phase != GamePhase.Day )
			return null;

		var shown = core.Save.TutorialTipsShown;
		TutorialTipDef best = null;

		foreach ( var tip in All.OrderByDescending( t => t.Priority ) )
		{
			if ( shown.Contains( tip.Id ) )
				continue;
			if ( season < tip.MinNight || season > tip.MaxNight )
				continue;
			if ( !MeetsCondition( tip, core ) )
				continue;

			best = tip;
			break;
		}

		return best;
	}

	private static bool MeetsCondition( TutorialTipDef tip, GameCore core ) =>
		tip.Id switch
		{
			"cure_defense" => !HasDefenseTower(),
			"cure_barracks" => !HasBarracks(),
			"cure_recruit" => HasBarracks() && (core.Save.Recruits.Count == 0),
			"cure_lab" => !HasBuilding( BuildableId.Lab ),
			"cure_knowledge_early" => !HasAnyTech() && KnowledgeAmount() < 40,
			"cure_tech" => !HasAnyTech(),
			"cure_farm" => HasTech( "agriculture" ) && !HasBuilding( BuildableId.Farm ),
			"cure_factory" => HasTech( "industry" ) && !HasBuilding( BuildableId.Factory ),
			"cure_knowledge" => HasTech( "literacy" ) && !HasBuilding( BuildableId.Library ) && !HasBuilding( BuildableId.School ),
			"cure_hospital" => HasTech( "medicine" ) && !HasBuilding( BuildableId.Hospital ),
			"cure_shop" => HasTech( "commerce" ) && !HasBuilding( BuildableId.Shop ),
			"cure_workers" => AnySpecialistUnlocked( core.Save ) && (WorkerManager.Instance?.Count ?? 0) == 0,
			"cure_threats" => core.Save.TotalThreatsSurvived <= 0,
			"cure_cure_tiers" => HasBuilding( BuildableId.Lab ) && core.Save.CureResearchTier <= 0,
			"cure_scouts" => CureUnlocks.IsShortExpeditionUnlocked( core.Save ),
			"cure_sickness" => core.Save.ColonySickness >= 12f,
			"cure_plots" => core.Save.OwnedPlots.Count <= 1 && HasBarracks(),
			"cure_done" => core.Save.TutorialTipsShown.Contains( "cure_plots" )
				|| CureConstants.ProgressSeason( core.Save ) >= 7,
			_ => true
		};

	private static bool HasDefenseTower() =>
		BuildManager.Instance?.Buildings.Any( b => b.IsDefense && !b.IsDestroyed ) == true;

	private static bool HasBarracks() =>
		BuildManager.Instance?.Buildings.Any( b => b.Type == BuildableId.Barracks && !b.IsDestroyed ) == true;

	private static bool HasBuilding( BuildableId id ) =>
		BuildManager.Instance?.Buildings.Any( b => b.Type == id && !b.IsDestroyed ) == true;

	private static bool HasAnyTech() =>
		GameCore.Instance?.Save?.UnlockedTech?.Count > 0;

	private static bool HasTech( string id ) =>
		TechTreeCatalog.IsUnlocked( GameCore.Instance?.Save, id );

	private static double KnowledgeAmount() =>
		GameCore.Instance?.Resources.Get( ResourceKind.Knowledge ) ?? 0;

	private static bool AnySpecialistUnlocked( SaveData save ) =>
		CureUnlocks.IsWorkerUnlocked( save, WorkerRole.Farmer )
		|| CureUnlocks.IsWorkerUnlocked( save, WorkerRole.Scholar )
		|| CureUnlocks.IsWorkerUnlocked( save, WorkerRole.Operator )
		|| CureUnlocks.IsWorkerUnlocked( save, WorkerRole.Medic )
		|| CureUnlocks.IsWorkerUnlocked( save, WorkerRole.Merchant );
}
