namespace UnderPressure;

/// <summary>Act IV — tactical forensics (levels 16–25).</summary>
public static partial class CampaignCatalog
{
	/// <summary>Level 16 — crumbling maintenance tunnels: sludge-locked doors, fallen crews.</summary>
	static JobDef Level16_AbandonedUnderground() => new()
	{
		Name = "Abandoned Underground",
		Blurb = "Fight mercenaries while clearing bio-sludge to open drainage doors.",
		Briefing = "Crumbling city maintenance tunnels — fight corporate mercenaries while blasting bio-sludge blocks to open automated drainage doors. "
			+ "Remains of previous cleaning crews litter the tunnels. I'm not the first washer they hired. I'm the first one still breathing.",
		BriefingTag = "ACT IV",
		ActTitle = "ACT IV — TACTICAL FORENSICS",
		Location = "Old City Maintenance Tunnels",
		CrimeScene = "Remains of previous cleaning crews who failed litter the tunnels.",
		RevealHook = "Hidden graffiti map showing a secret flank path past a mercenary turret.",
		IsCombatLevel = true,
		ValueMultiplier = Pay( 16 ),
		Theme = MapTheme.Underground,
		GroundColor = new Color( 0.24f, 0.24f, 0.26f ),
		GroundSize = new Vector2( 2000f, 1000f ),
		SpawnPosition = new Vector3( -820f, -280f, 0f ),
		SpawnYaw = 30f,
		Panels = new List<PanelDef>
		{
			// The sludge block sealing the drainage doors.
			FlatGround( 320f, 280f, ToxicGreen, DarkFloor, PanelShape.Rounded, GrimePattern.Organic,
				position: new Vector3( 660f, 0f, 1f ), followUp: ToolType.ScrubBrush,
				followUpColor: new Color( 0.20f, 0.38f, 0.10f ), followUpWet: false ),
			// Tunnel wall — soot hiding the survivors' map.
			// Snugged to the enclosed-shell wall (face y=500) instead of floating 21 out.
			FlatWall( 420f, 220f, Soot, new Color( 0.20f, 0.20f, 0.23f ), PanelShape.Full, GrimePattern.Organic,
				position: new Vector3( -300f, 496f, 150f ), secrets: new List<SurfaceSecret>
				{
					new()
					{
						Symbol = SecretSymbol.Compass, X = 0.34f, Y = 0.56f, Scale = 2.2f,
						DiscoveryId = "L16_flank_map",
						Monologue = "Spray-paint under the soot: a crude map with an arrow around the north gallery — LEFT PAST THE EYE. The last crew found a way around the turret before they didn't.",
					},
					new() { Text = "LEFT PAST THE EYE", X = 0.62f, Y = 0.30f, Scale = 1.2f },
				} ),
			// What's left of crew three.
			FlatGround( 280f, 240f, OldBlood, DarkFloor, PanelShape.OilSpill, GrimePattern.Splatter,
				position: new Vector3( -480f, -160f, 1f ), cellSize: 12f ),
		},
		Decor = new List<DecorDef>
		{
			D( DecorKind.BlastDoor, 880f, 0f, yaw: -90f, size: new Vector3( 260f, 30f, 280f ) ),
			D( DecorKind.WaterChannel, -100f, 210f, size: new Vector3( 1500f, 130f, 1f ), color: new Color( 0.12f, 0.20f, 0.18f ) ),
			D( DecorKind.PipeRun, -100f, -420f, size: new Vector3( 1600f, 0f, 100f ), color: new Color( 0.40f, 0.36f, 0.32f ) ),
			D( DecorKind.Rubble, -700f, 320f, yaw: 20f, size: new Vector3( 1.4f, 1f, 1f ) ),
			D( DecorKind.Rubble, 300f, 360f, yaw: -50f, size: new Vector3( 1.1f, 1f, 1f ) ),
			D( DecorKind.Rubble, 500f, -320f, yaw: 75f ),
			D( DecorKind.SteamVent, 100f, 120f ),
			D( DecorKind.SteamVent, 420f, -180f, size: new Vector3( 0.85f, 1f, 1f ) ),
			D( DecorKind.BarrelCluster, -240f, -280f, yaw: 40f, color: RustBrown ),
			D( DecorKind.Floodlight, -560f, 100f, yaw: 120f ),
			D( DecorKind.Floodlight, 480f, 140f, yaw: -140f ),
			D( DecorKind.CrateStack, 720f, 300f, yaw: 15f, size: new Vector3( 0.8f, 1f, 1f ), color: new Color( 0.34f, 0.28f, 0.20f ) ),
		},
		Props = new List<PropDef>
		{
			// Abandoned washer rig + helmet from the last crew.
			P( -440f, -60f, 1f, 80f, 46f, 36f, new Color( 0.72f, 0.16f, 0.14f ), new Angles( 0, 30, 70 ) ),
			P( -380f, -240f, 1f, 26f, 26f, 20f, new Color( 0.90f, 0.86f, 0.30f ) ),
			// The "eye" — a dead security camera on the north wall.
			P( -120f, 470f, 260f, 30f, 24f, 22f, new Color( 0.14f, 0.14f, 0.16f ), new Angles( 20, 200, 0 ) ),
			// Hazard chevrons at the blast door.
			P( 760f, 150f, 3f, 120f, 20f, 1f, BollardYellow, new Angles( 0, -45, 0 ) ),
			P( 760f, -150f, 3f, 120f, 20f, 1f, BollardYellow, new Angles( 0, 45, 0 ) ),
		},
		Enemies = new List<EnemySpawnDef>
		{
			E( EnemyKind.RivalWasher, 400f, 60f ),
			E( EnemyKind.RivalWasher, -60f, -220f ),
			E( EnemyKind.StickerBandit, 200f, 280f ),
			E( EnemyKind.StickerBandit, -360f, 300f ),
			E( EnemyKind.OilLeech, 620f, -120f ),
			E( EnemyKind.OilLeech, -600f, 60f ),
		},
	};

	/// <summary>Level 17 — Aegis bio-lab sector C: the code is written in blood.</summary>
	static JobDef Level17_BioLab() => new()
	{
		Name = "Aegis Bio-Lab",
		Blurb = "Infiltrate Sector C for the antidote — melt locks with acid-wash.",
		Briefing = "Sterile hidden corporate laboratory — infiltrate to steal the physical antidote and evidence. Bodies of scientists and guards line the hallways. "
			+ "Blast doors locked down. The keypad code is written in blood on the observation glass.",
		BriefingTag = "ACT IV",
		ActTitle = "ACT IV — TACTICAL FORENSICS",
		Location = "Aegis Bio-Lab Sector C",
		CrimeScene = "Aftermath of an internal breach — bodies of scientists and guards in the hallways.",
		RevealHook = "Door code 7734 written in blood on the observation window.",
		IsCombatLevel = true,
		ValueMultiplier = Pay( 17 ),
		Theme = MapTheme.Interior,
		GroundColor = new Color( 0.76f, 0.78f, 0.78f ),
		GroundSize = new Vector2( 1500f, 1200f ),
		SpawnPosition = new Vector3( 0f, -460f, 0f ),
		SpawnYaw = 90f,
		Panels = new List<PanelDef>
		{
			// Main hallway — the breach happened here.
			FlatGround( 720f, 280f, Blood, WhiteTile, PanelShape.Banner, GrimePattern.Splatter,
				position: new Vector3( 0f, -60f, 1f ) ),
			// Observation glass — film off first, the code is underneath.
			FlatWall( 340f, 180f, new Color( 0.68f, 0.72f, 0.74f ), GlassDark, PanelShape.Window, GrimePattern.Streaks,
				position: new Vector3( -380f, 404f, 150f ), surface: CleanSurface.Glass,
				followUp: ToolType.Squeegee, cellSize: 11f, secrets: new List<SurfaceSecret>
				{
					new()
					{
						Text = "7734", X = 0.5f, Y = 0.52f, Scale = 2.6f, Color = Blood,
						DiscoveryId = "L17_door_code",
						Monologue = "Four digits finger-painted from the inside of the glass. Whoever wrote it was watching the door seal while they did. 7734. I won't waste it.",
					},
				} ),
			// Spilled compound near the sample fridges (kept clear of the hallway panel so
			// the two grime sheets never overlap on the same plane).
			FlatGround( 340f, 300f, NeonBlue, WhiteTile, PanelShape.OilSpill, GrimePattern.Organic,
				position: new Vector3( 400f, 240f, 1f ), followUp: ToolType.ScrubBrush,
				followUpColor: new Color( 0.10f, 0.48f, 0.66f ), followUpWet: false ),
		},
		Decor = new List<DecorDef>
		{
			D( DecorKind.BlastDoor, 0f, 540f, size: new Vector3( 240f, 30f, 260f ) ),
			D( DecorKind.TiledWall, -380f, 420f, size: new Vector3( 420f, 24f, 260f ), color: WhiteTile ),
			D( DecorKind.TiledWall, 380f, 420f, size: new Vector3( 420f, 24f, 260f ), color: WhiteTile ),
			D( DecorKind.LabBench, -420f, 120f, yaw: 5f ),
			D( DecorKind.LabBench, -420f, -160f, yaw: -3f ),
			D( DecorKind.LabBench, 160f, 300f, yaw: 90f ),
			D( DecorKind.ServerRack, 620f, 300f, color: new Color( 0.25f, 0.85f, 1f ) ),
			D( DecorKind.ServerRack, 620f, 100f, color: new Color( 0.95f, 0.35f, 0.25f ) ),
			D( DecorKind.DisplayCase, 0f, 420f, size: new Vector3( 1.1f, 1f, 1f ) ),
			D( DecorKind.SteelTable, 340f, -240f, yaw: 12f ),
			D( DecorKind.Floodlight, -200f, -320f, yaw: 30f ),
			D( DecorKind.HoldingCell, -640f, 420f, size: new Vector3( 200f, 160f, 210f ) ),
		},
		Props = new List<PropDef>
		{
			// Covered gurneys in the hallway.
			P( -180f, 60f, 1f, 150f, 54f, 40f, new Color( 0.88f, 0.88f, 0.90f ) ),
			P( 220f, 20f, 1f, 150f, 54f, 40f, new Color( 0.88f, 0.88f, 0.90f ), new Angles( 0, 15, 0 ) ),
			// Scattered papers and a dropped keycard.
			P( 60f, -200f, 1f, 30f, 22f, 1f, new Color( 0.94f, 0.94f, 0.90f ), new Angles( 0, 30, 0 ) ),
			P( -60f, -260f, 1f, 30f, 22f, 1f, new Color( 0.94f, 0.94f, 0.90f ), new Angles( 0, -50, 0 ) ),
			P( 120f, -300f, 1f, 16f, 10f, 1f, new Color( 0.25f, 0.85f, 1f ) ),
		},
		Enemies = new List<EnemySpawnDef>
		{
			E( EnemyKind.RivalWasher, -300f, 240f ),
			E( EnemyKind.RivalWasher, 360f, -80f ),
			E( EnemyKind.StickerBandit, 100f, 160f ),
			E( EnemyKind.StickerBandit, -500f, -260f ),
			E( EnemyKind.Wasp, 0f, 60f ),
			E( EnemyKind.Wasp, 300f, 340f ),
		},
	};

	/// <summary>Level 18 — chemical distribution warehouse: glowing pools and a shipping manifest.</summary>
	static JobDef Level18_ChemicalWarehouse() => new()
	{
		Name = "Chemical Warehouse",
		Blurb = "Fight snipers on catwalks; chain-react chemical barrels.",
		Briefing = "Massive industrial distribution center — mercenaries sniping from upper catwalks, glowing pools of raw neon-blue bioweapon on the floor. "
			+ "Blast the shipping manifest clean and read the delivery schedule: the compound ships to the city reservoir tonight.",
		BriefingTag = "ACT IV",
		ActTitle = "ACT IV — TACTICAL FORENSICS",
		Location = "Industrial Chemical Warehouse",
		CrimeScene = "Massive glowing pools of raw neon-blue bioweapon compound on warehouse floors.",
		RevealHook = "Shipping manifest: bioweapon deployed to the city reservoir tonight.",
		IsCombatLevel = true,
		ValueMultiplier = Pay( 18 ),
		Theme = MapTheme.Industrial,
		GroundColor = new Color( 0.34f, 0.34f, 0.37f ),
		GroundSize = new Vector2( 2200f, 1800f ),
		SpawnPosition = new Vector3( 0f, -680f, 0f ),
		SpawnYaw = 90f,
		Panels = new List<PanelDef>
		{
			// Main compound pool.
			FlatGround( 500f, 420f, NeonBlue, DarkFloor, PanelShape.OilSpill, GrimePattern.Organic,
				position: new Vector3( -440f, 40f, 1f ), followUp: ToolType.ScrubBrush,
				followUpColor: new Color( 0.10f, 0.48f, 0.66f ), followUpWet: false ),
			// Secondary spill by the barrel stacks.
			FlatGround( 380f, 340f, NeonBlue, DarkFloor, PanelShape.OilSpill, GrimePattern.Splatter,
				position: new Vector3( 380f, -260f, 1f ), followUp: ToolType.ScrubBrush,
				followUpColor: new Color( 0.10f, 0.48f, 0.66f ), followUpWet: false ),
			// Manifest board on the warehouse face — the schedule is under the grime.
			// Near the warehouse face (y=380) but in front of its ribs (reaching y=375).
			FlatWall( 260f, 190f, Grime, new Color( 0.55f, 0.57f, 0.60f ), PanelShape.Rounded, GrimePattern.Streaks,
				position: new Vector3( 640f, 372f, 150f ), cellSize: 11f, secrets: new List<SurfaceSecret>
				{
					new()
					{
						Text = "RESERVOIR 03:00", X = 0.5f, Y = 0.56f, Scale = 1.4f,
						DiscoveryId = "L18_manifest",
						Monologue = "Manifest 8841: forty drums, destination CITY RESERVOIR, load-out 03:00 tonight. They're not hiding a murder anymore. They're scheduling one for the whole city.",
					},
					new() { Text = "40 DRUMS", X = 0.5f, Y = 0.32f, Scale = 1.2f },
				} ),
		},
		Decor = new List<DecorDef>
		{
			D( DecorKind.Warehouse, 0f, 620f, size: new Vector3( 1400f, 480f, 320f ) ),
			D( DecorKind.Catwalk, 0f, 180f, size: new Vector3( 1300f, 100f, 200f ) ),
			D( DecorKind.Catwalk, -200f, -420f, size: new Vector3( 900f, 100f, 200f ) ),
			D( DecorKind.BarrelCluster, 500f, -120f, yaw: 10f, color: NeonBlue ),
			D( DecorKind.BarrelCluster, 640f, -420f, yaw: -25f, color: NeonBlue ),
			D( DecorKind.BarrelCluster, -700f, -260f, yaw: 55f, color: new Color( 0.80f, 0.66f, 0.16f ) ),
			D( DecorKind.Container, -820f, 240f, yaw: 90f, color: new Color( 0.62f, 0.28f, 0.20f ) ),
			D( DecorKind.Container, 860f, 100f, yaw: 90f, color: new Color( 0.30f, 0.32f, 0.55f ) ),
			D( DecorKind.CrateStack, 200f, 40f, yaw: 30f, size: new Vector3( 1.2f, 1f, 1f ) ),
			D( DecorKind.CrateStack, -300f, -300f, yaw: -15f ),
			D( DecorKind.Generator, 780f, -600f, yaw: 5f ),
			D( DecorKind.PipeRun, -640f, 420f, size: new Vector3( 700f, 0f, 120f ), color: new Color( 0.55f, 0.58f, 0.62f ) ),
			D( DecorKind.Floodlight, -160f, -560f, yaw: 15f ),
			D( DecorKind.Floodlight, 300f, -540f, yaw: -20f ),
			D( DecorKind.LampPost, -940f, -620f ),
			D( DecorKind.LampPost, 940f, -620f ),
		},
		Props = new List<PropDef>
		{
			// Forklift left mid-load.
			P( 60f, -160f, 1f, 120f, 70f, 70f, SafetyOrange ),
			P( 140f, -160f, 1f, 60f, 50f, 10f, new Color( 0.20f, 0.20f, 0.22f ) ),
			// Glow decals under the pools' rims.
			P( -440f, 40f, 3f, 560f, 470f, 1f, new Color( 0.10f, 0.35f, 0.48f ) ),
			P( 380f, -260f, 3f, 430f, 380f, 1f, new Color( 0.10f, 0.35f, 0.48f ) ),
		},
		Enemies = new List<EnemySpawnDef>
		{
			E( EnemyKind.RivalWasher, -400f, 300f ),
			E( EnemyKind.RivalWasher, 500f, 60f ),
			E( EnemyKind.RivalWasher, 0f, -300f ),
			E( EnemyKind.Wasp, -200f, 180f ),
			E( EnemyKind.Wasp, 260f, 200f ),
			E( EnemyKind.Wasp, 0f, -480f ),
			E( EnemyKind.OilLeech, -520f, -60f ),
			E( EnemyKind.OilLeech, 420f, -340f ),
		},
	};

	/// <summary>Level 19 — storm-lit rail yard: leaking tankers and a radio frequency.</summary>
	static JobDef Level19_RailYardSiege() => new()
	{
		Name = "Rail Yard Siege",
		Blurb = "Hijack a moving train of chemical shipments in a thunderstorm.",
		Briefing = "Open-air train yard at night in a violent thunderstorm — hijack a moving train carrying chemical shipments. "
			+ "Train cars leak bio-chemicals; railway workers who saw too much didn't make it off the tracks.",
		BriefingTag = "ACT IV",
		ActTitle = "ACT IV — TACTICAL FORENSICS",
		Location = "Open-Air Rail Yard",
		CrimeScene = "Train cars leaking bio-chemicals; bodies of railway workers who saw too much.",
		RevealHook = "Tracking frequency on the locomotive intercepts black-ops radio chatter.",
		IsCombatLevel = true,
		ValueMultiplier = Pay( 19 ),
		Theme = MapTheme.Industrial,
		GroundColor = new Color( 0.30f, 0.30f, 0.32f ),
		GroundSize = new Vector2( 2400f, 1600f ),
		SpawnPosition = new Vector3( -100f, -620f, 0f ),
		SpawnYaw = 90f,
		Panels = new List<PanelDef>
		{
			// Tanker leak spreading between the rails.
			FlatGround( 440f, 280f, NeonBlue, new Color( 0.30f, 0.30f, 0.32f ), PanelShape.Ellipse, GrimePattern.Organic,
				position: new Vector3( 0f, 280f, 1f ), followUp: ToolType.ScrubBrush,
				followUpColor: new Color( 0.10f, 0.48f, 0.66f ), followUpWet: false ),
			// Boxcar flank — rust and road film over the livery.
			// Boxcar hull face is y=95 with the sliding door reaching 92.5 — clear both.
			FlatWall( 380f, 110f, RustBrown, new Color( 0.46f, 0.24f, 0.18f ), PanelShape.Banner, GrimePattern.Rust,
				position: new Vector3( -140f, 89f, 95f ), cellSize: 11f ),
			// Where the workers fell.
			FlatGround( 260f, 220f, Blood, new Color( 0.30f, 0.30f, 0.32f ), PanelShape.OilSpill, GrimePattern.Splatter,
				position: new Vector3( 520f, 20f, 1f ), cellSize: 12f ),
			// Locomotive flank — the frequency is stenciled under the soot.
			FlatWall( 300f, 120f, Soot, new Color( 0.16f, 0.28f, 0.40f ), PanelShape.Banner, GrimePattern.Streaks,
				position: new Vector3( 780f, 87f, 110f ), cellSize: 11f, secrets: new List<SurfaceSecret>
				{
					new()
					{
						Text = "462.550 MHZ", X = 0.5f, Y = 0.52f, Scale = 1.6f,
						DiscoveryId = "L19_frequency",
						Monologue = "A maintenance stencil, painted over in a hurry: 462.550 MHz. I tune the van radio to it and the storm fills with calm, clipped voices calling out my position.",
					},
				} ),
		},
		Decor = new List<DecorDef>
		{
			D( DecorKind.RailTrack, 0f, 150f, size: new Vector3( 2300f, 1f, 1f ) ),
			D( DecorKind.RailTrack, 0f, 400f, size: new Vector3( 2300f, 1f, 1f ) ),
			D( DecorKind.RailTrack, 0f, -100f, size: new Vector3( 2300f, 1f, 1f ) ),
			D( DecorKind.TrainCar, -640f, 150f, color: new Color( 0.46f, 0.24f, 0.18f ) ),
			D( DecorKind.TrainCar, -140f, 150f, color: new Color( 0.46f, 0.24f, 0.18f ) ),
			D( DecorKind.TrainCar, 340f, 150f, color: new Color( 0.30f, 0.36f, 0.30f ) ),
			D( DecorKind.Locomotive, 800f, 150f, color: new Color( 0.16f, 0.28f, 0.40f ) ),
			D( DecorKind.TrainCar, 100f, 400f, color: new Color( 0.24f, 0.30f, 0.34f ) ),
			D( DecorKind.LampPost, -800f, -300f, size: new Vector3( 1.15f, 1f, 1f ) ),
			D( DecorKind.LampPost, 200f, -320f, size: new Vector3( 1.15f, 1f, 1f ) ),
			D( DecorKind.LampPost, 900f, -300f, size: new Vector3( 1.15f, 1f, 1f ) ),
			D( DecorKind.Floodlight, -400f, -200f, yaw: 30f ),
			D( DecorKind.Floodlight, 600f, -220f, yaw: -40f ),
			D( DecorKind.CrateStack, -900f, -160f, yaw: 20f ),
			D( DecorKind.BarrelCluster, 980f, -140f, yaw: -35f, color: NeonBlue ),
			D( DecorKind.ConcreteBarrier, -300f, -440f, yaw: 4f, size: new Vector3( 320f, 1f, 1f ) ),
			D( DecorKind.GuardRail, 0f, -740f, size: new Vector3( 2000f, 1f, 1f ) ),
		},
		Props = new List<PropDef>
		{
			// Signal mast + relay boxes.
			P( -520f, -180f, 1f, 16f, 16f, 260f, new Color( 0.30f, 0.32f, 0.36f ) ),
			P( -520f, -180f, 262f, 44f, 12f, 30f, new Color( 0.85f, 0.20f, 0.16f ) ),
			P( 400f, -260f, 1f, 60f, 40f, 80f, new Color( 0.44f, 0.46f, 0.50f ) ),
			// Dropped lantern and hard hats by the blood.
			P( 470f, -80f, 1f, 16f, 16f, 20f, new Color( 0.95f, 0.82f, 0.12f ) ),
			P( 560f, -60f, 1f, 26f, 26f, 14f, SafetyOrange ),
		},
		Enemies = new List<EnemySpawnDef>
		{
			E( EnemyKind.RivalWasher, -300f, -40f ),
			E( EnemyKind.RivalWasher, 500f, 260f ),
			E( EnemyKind.StickerBandit, 100f, -240f ),
			E( EnemyKind.StickerBandit, -700f, 280f ),
			E( EnemyKind.Raccoon, 800f, -400f ),
			E( EnemyKind.Rat, -450f, -300f ),
		},
	};

	/// <summary>Level 20 — the black site: cells, steam, and layered stains.</summary>
	static JobDef Level20_BlackSite() => new()
	{
		Name = "Black Site",
		Blurb = "Breach an unregistered detention facility; rescue whistleblowers.",
		Briefing = "Unregistered underground government detention facility — breach to rescue surviving whistleblowers. "
			+ "A literal torture chamber; concrete permanently stained with layers of old and fresh blood. Steam vents blind whole rooms of guards.",
		BriefingTag = "ACT IV",
		ActTitle = "ACT IV — TACTICAL FORENSICS",
		Location = "Underground Black Site",
		CrimeScene = "Torture chamber — concrete permanently stained with layers of old and fresh blood.",
		RevealHook = "Structural blueprint on a pillar reveals a secret ventilation shaft bypass.",
		IsCombatLevel = true,
		ValueMultiplier = Pay( 20 ),
		Theme = MapTheme.Underground,
		GroundColor = new Color( 0.28f, 0.28f, 0.30f ),
		GroundSize = new Vector2( 1800f, 1200f ),
		SpawnPosition = new Vector3( -700f, -420f, 0f ),
		SpawnYaw = 60f,
		Panels = new List<PanelDef>
		{
			// The chamber floor: fresh over old — wash the fresh, brush the years.
			FlatGround( 520f, 460f, Blood, DarkFloor, PanelShape.Rounded, GrimePattern.Splatter,
				position: new Vector3( 0f, 40f, 1f ), followUp: ToolType.ScrubBrush,
				followUpColor: OldBlood, followUpWet: false ),
			// Cell 2 — someone kept count on the floor.
			FlatGround( 190f, 120f, OldBlood, DarkFloor, PanelShape.Full, GrimePattern.Organic,
				position: new Vector3( -560f, 420f, 1f ), cellSize: 11f, secrets: new List<SurfaceSecret>
				{
					new() { Symbol = SecretSymbol.Scratches, X = 0.5f, Y = 0.5f, Scale = 2.0f, DiscoveryId = "L20_tally",
						Monologue = "Tally marks gouged into the concrete under the stain. Forty-one days. The forty-second mark is half finished." },
				} ),
			// North wall — grime over the structural blueprint.
			// Snugged to the enclosed-shell wall (face y=600) instead of floating 21 out.
			FlatWall( 380f, 220f, Soot, new Color( 0.20f, 0.20f, 0.23f ), PanelShape.Full, GrimePattern.Organic,
				position: new Vector3( 340f, 596f, 160f ), secrets: new List<SurfaceSecret>
				{
					new()
					{
						Symbol = SecretSymbol.Compass, X = 0.32f, Y = 0.55f, Scale = 2.0f,
						DiscoveryId = "L20_vent_bypass",
						Monologue = "A construction blueprint still glued to the wall: VENT SHAFT B-2 runs from the cells straight past the guard floor. That's how the survivors get out without a firefight.",
					},
					new() { Text = "VENT B-2", X = 0.66f, Y = 0.32f, Scale = 1.4f },
				} ),
		},
		Decor = new List<DecorDef>
		{
			D( DecorKind.HoldingCell, -560f, 460f, size: new Vector3( 220f, 160f, 210f ) ),
			D( DecorKind.HoldingCell, -300f, 460f, size: new Vector3( 220f, 160f, 210f ) ),
			D( DecorKind.HoldingCell, -40f, 460f, size: new Vector3( 220f, 160f, 210f ) ),
			D( DecorKind.BlastDoor, -820f, 0f, yaw: 90f, size: new Vector3( 240f, 30f, 260f ) ),
			D( DecorKind.SteamVent, 240f, 240f ),
			D( DecorKind.SteamVent, -180f, -200f ),
			D( DecorKind.SteamVent, 500f, -100f, size: new Vector3( 1.2f, 1f, 1f ) ),
			D( DecorKind.TiledWall, 620f, 200f, yaw: 90f, size: new Vector3( 500f, 24f, 220f ), color: new Color( 0.34f, 0.34f, 0.38f ) ),
			D( DecorKind.SteelTable, 120f, -320f, yaw: 8f ),
			D( DecorKind.Floodlight, -400f, 140f, yaw: 60f ),
			D( DecorKind.Floodlight, 400f, 420f, yaw: -120f ),
			D( DecorKind.ServerRack, 780f, -380f, color: new Color( 0.95f, 0.35f, 0.25f ) ),
			D( DecorKind.CrateStack, -700f, -240f, yaw: 35f, size: new Vector3( 0.85f, 1f, 1f ), color: new Color( 0.30f, 0.30f, 0.34f ) ),
		},
		Props = new List<PropDef>
		{
			// The chair, bolted down, and the drain beside it.
			P( 0f, 60f, 1f, 50f, 50f, 60f, new Color( 0.20f, 0.21f, 0.24f ) ),
			P( 0f, 62f, 62f, 50f, 10f, 50f, new Color( 0.20f, 0.21f, 0.24f ) ),
			P( 90f, -40f, 2f, 44f, 44f, 3f, new Color( 0.14f, 0.14f, 0.16f ) ),
			// Restraint hooks on the tiled wall.
			P( 606f, 120f, 150f, 6f, 20f, 20f, new Color( 0.55f, 0.57f, 0.60f ) ),
			P( 606f, 260f, 150f, 6f, 20f, 20f, new Color( 0.55f, 0.57f, 0.60f ) ),
		},
		Enemies = new List<EnemySpawnDef>
		{
			E( EnemyKind.RivalWasher, 300f, 100f ),
			E( EnemyKind.RivalWasher, -200f, 240f ),
			E( EnemyKind.RivalWasher, 500f, -280f ),
			E( EnemyKind.StickerBandit, -400f, -160f ),
			E( EnemyKind.StickerBandit, 100f, 400f ),
		},
	};

	/// <summary>Level 21 — broadcast tower roof in a lightning storm.</summary>
	static JobDef Level21_TowerRooftop() => new()
	{
		Name = "Broadcast Tower Exterior",
		Blurb = "Fight up scaffolding; blast jamming gear off satellite dishes.",
		Briefing = "Skyscraper roof and broadcasting antenna during a lightning storm — fight up exterior scaffolding and blast jamming equipment welded to satellite dishes. "
			+ "Original security force piled on the roof. I need the backup generator online.",
		BriefingTag = "ACT IV",
		ActTitle = "ACT IV — TACTICAL FORENSICS",
		Location = "Broadcast Tower Rooftop",
		CrimeScene = "Bodies of the building's original security force piled on the roof.",
		RevealHook = "Hidden override switch on the backup generator restores antenna power.",
		IsCombatLevel = true,
		ValueMultiplier = Pay( 21 ),
		Theme = MapTheme.Rooftop,
		GroundColor = new Color( 0.24f, 0.24f, 0.27f ),
		GroundSize = new Vector2( 1500f, 1300f ),
		SpawnPosition = new Vector3( 0f, -500f, 0f ),
		SpawnYaw = 90f,
		Panels = new List<PanelDef>
		{
			// Jamming-rig sludge welded around the west dish.
			FlatGround( 300f, 300f, OilBlack, new Color( 0.24f, 0.24f, 0.27f ), PanelShape.Circle, GrimePattern.Streaks,
				position: new Vector3( -400f, 140f, 1f ) ),
			// The security team (kept south of the rust ring so the sheets never overlap).
			FlatGround( 360f, 320f, Blood, new Color( 0.24f, 0.24f, 0.27f ), PanelShape.OilSpill, GrimePattern.Splatter,
				position: new Vector3( 180f, 50f, 1f ), cellSize: 12f ),
			// Rust ring around the antenna anchors.
			FlatGround( 400f, 400f, RustBrown, new Color( 0.24f, 0.24f, 0.27f ), PanelShape.Ring, GrimePattern.Rust,
				position: new Vector3( 0f, 420f, 1f ) ),
			// Generator apron — the override is under the grease.
			FlatGround( 240f, 180f, Grime, new Color( 0.24f, 0.24f, 0.27f ), PanelShape.Rounded, GrimePattern.Organic,
				position: new Vector3( 380f, -280f, 1f ), cellSize: 11f, secrets: new List<SurfaceSecret>
				{
					new()
					{
						Text = "OVERRIDE 22B", X = 0.5f, Y = 0.52f, Scale = 1.5f,
						DiscoveryId = "L21_override",
						Monologue = "Stenciled on the deck under the grease: OVERRIDE 22B — the maintenance crew's cheat code for the backup generator. Power to the antenna. Power to the truth.",
					},
				} ),
		},
		Decor = new List<DecorDef>
		{
			D( DecorKind.Antenna, 0f, 420f, size: new Vector3( 110f, 0f, 560f ) ),
			D( DecorKind.SatelliteDish, -400f, 260f, yaw: 25f, size: new Vector3( 1.1f, 1f, 1f ) ),
			D( DecorKind.SatelliteDish, 420f, 300f, yaw: -30f, size: new Vector3( 0.95f, 1f, 1f ) ),
			D( DecorKind.Generator, 380f, -160f, yaw: 180f ),
			D( DecorKind.RoofUnit, -540f, -220f, yaw: 5f ),
			D( DecorKind.RoofUnit, -260f, -380f, yaw: -12f ),
			D( DecorKind.RoofUnit, 600f, 60f, yaw: 8f, size: new Vector3( 1.2f, 1f, 1f ) ),
			D( DecorKind.Catwalk, -100f, -140f, size: new Vector3( 700f, 80f, 130f ) ),
			D( DecorKind.Floodlight, -160f, 300f, yaw: 160f ),
			D( DecorKind.Floodlight, 240f, 340f, yaw: -150f ),
			D( DecorKind.Railing, 0f, 620f, size: new Vector3( 1400f, 1f, 1f ), color: new Color( 0.60f, 0.62f, 0.66f ) ),
			D( DecorKind.CrateStack, -620f, 380f, yaw: 40f, size: new Vector3( 0.8f, 1f, 1f ), color: new Color( 0.34f, 0.36f, 0.40f ) ),
		},
		Props = new List<PropDef>
		{
			// Cable snakes from the dishes to the antenna.
			P( -220f, 260f, 2f, 320f, 14f, 3f, new Color( 0.12f, 0.12f, 0.14f ), new Angles( 0, 18, 0 ) ),
			P( 240f, 340f, 2f, 300f, 14f, 3f, new Color( 0.12f, 0.12f, 0.14f ), new Angles( 0, -20, 0 ) ),
			// Welded jamming rig on the west dish plinth.
			P( -400f, 190f, 30f, 60f, 40f, 46f, new Color( 0.10f, 0.11f, 0.14f ) ),
			P( -400f, 190f, 78f, 8f, 8f, 60f, new Color( 0.10f, 0.11f, 0.14f ) ),
			// Riot shields dropped by the pile.
			P( 300f, 140f, 1f, 40f, 6f, 60f, new Color( 0.16f, 0.18f, 0.24f ), new Angles( 0, 30, 75 ) ),
			P( 100f, -20f, 1f, 40f, 6f, 60f, new Color( 0.16f, 0.18f, 0.24f ), new Angles( 0, -60, 80 ) ),
		},
		Enemies = new List<EnemySpawnDef>
		{
			E( EnemyKind.RivalWasher, -300f, 60f ),
			E( EnemyKind.RivalWasher, 300f, 240f ),
			E( EnemyKind.Pigeon, 0f, 200f ),
			E( EnemyKind.Pigeon, -200f, 400f ),
			E( EnemyKind.Pigeon, 400f, -60f ),
			E( EnemyKind.Wasp, 100f, -200f ),
			E( EnemyKind.Wasp, -440f, -100f ),
		},
	};

	/// <summary>Level 22 — hold the studio while the upload runs.</summary>
	static JobDef Level22_TowerStudio() => new()
	{
		Name = "Broadcast Tower Interior",
		Blurb = "Hold the control room while evidence uploads to the public.",
		Briefing = "Television studios and control rooms — hold the line against a massive assault while data files upload. "
			+ "Pristine modern studio turned warzone: plaster dust, bullet holes, blood. Upload completes. The conspiracy leaks globally.",
		BriefingTag = "ACT IV",
		ActTitle = "ACT IV — TACTICAL FORENSICS",
		Location = "Broadcast Tower Control Room",
		CrimeScene = "Studio warzone — plaster dust, bullet holes, and blood across the set.",
		RevealHook = "Live security feed: Senator Vance fleeing toward the dam reservoir.",
		IsCombatLevel = true,
		ValueMultiplier = Pay( 22 ),
		Theme = MapTheme.Interior,
		GroundColor = new Color( 0.28f, 0.28f, 0.31f ),
		GroundSize = new Vector2( 1400f, 1100f ),
		SpawnPosition = new Vector3( 0f, -420f, 0f ),
		SpawnYaw = 90f,
		Panels = new List<PanelDef>
		{
			// Plaster dust blown across the studio floor — pale over dark.
			FlatGround( 620f, 420f, new Color( 0.78f, 0.76f, 0.72f ), new Color( 0.28f, 0.28f, 0.31f ),
				PanelShape.Full, GrimePattern.Speckled,
				position: new Vector3( -60f, -140f, 1f ) ),
			// Blood on the interview stage.
			FlatGround( 380f, 200f, Blood, new Color( 0.55f, 0.42f, 0.30f ), PanelShape.Rounded, GrimePattern.Splatter,
				position: new Vector3( 0f, 170f, 22f ), surface: CleanSurface.Wood, cellSize: 12f ),
			// Shot-up backdrop — the feed monitor is behind the scorch.
			FlatWall( 500f, 210f, Soot, new Color( 0.16f, 0.24f, 0.44f ), PanelShape.Full, GrimePattern.Splatter,
				position: new Vector3( 0f, 464f, 150f ), secrets: new List<SurfaceSecret>
				{
					new()
					{
						Text = "CAM 4 — DAM", X = 0.5f, Y = 0.55f, Scale = 1.7f,
						DiscoveryId = "L22_vance_feed",
						Monologue = "The security wall clears and camera four is still live: a silver convoy crossing the dam access road. She's running to the reservoir — to watch it happen in person.",
					},
					new() { Symbol = SecretSymbol.Eye, X = 0.5f, Y = 0.28f, Scale = 1.5f },
				} ),
		},
		Decor = new List<DecorDef>
		{
			D( DecorKind.Platform, 0f, 190f, size: new Vector3( 520f, 300f, 20f ), color: new Color( 0.50f, 0.38f, 0.26f ) ),
			D( DecorKind.TiledWall, 0f, 480f, size: new Vector3( 720f, 24f, 260f ), color: new Color( 0.16f, 0.24f, 0.44f ) ),
			D( DecorKind.Sofa, -80f, 250f, yaw: 180f, size: new Vector3( 1.0f, 1f, 1f ), color: new Color( 0.60f, 0.20f, 0.24f ), z: 21f ),
			D( DecorKind.Sofa, 130f, 250f, yaw: 160f, size: new Vector3( 0.9f, 1f, 1f ), color: new Color( 0.60f, 0.20f, 0.24f ), z: 21f ),
			D( DecorKind.StudioCamera, -240f, 0f, yaw: 200f ),
			D( DecorKind.StudioCamera, 220f, -40f, yaw: 150f ),
			D( DecorKind.ControlDesk, -420f, -260f, yaw: 60f ),
			D( DecorKind.ServerRack, -600f, 300f, color: new Color( 0.20f, 0.90f, 0.55f ) ),
			D( DecorKind.ServerRack, 600f, 300f, color: new Color( 0.95f, 0.35f, 0.25f ) ),
			D( DecorKind.Floodlight, 420f, -240f, yaw: -45f ),
			D( DecorKind.CrateStack, 560f, -380f, yaw: 30f, size: new Vector3( 0.8f, 1f, 1f ), color: new Color( 0.30f, 0.30f, 0.34f ) ),
			D( DecorKind.Rubble, -560f, 60f, yaw: 45f, size: new Vector3( 0.9f, 1f, 1f ), color: new Color( 0.70f, 0.68f, 0.64f ) ),
		},
		Props = new List<PropDef>
		{
			// Fallen ceiling light rig across the floor.
			P( 120f, -260f, 1f, 400f, 26f, 20f, new Color( 0.16f, 0.17f, 0.20f ), new Angles( 0, 25, 0 ) ),
			P( 40f, -300f, 22f, 40f, 40f, 24f, new Color( 0.95f, 0.92f, 0.80f ), new Angles( 0, 25, 30 ) ),
			// Teleprompter and toppled chairs.
			P( -160f, -60f, 1f, 50f, 40f, 90f, new Color( 0.14f, 0.15f, 0.18f ) ),
			P( 320f, 120f, 1f, 46f, 46f, 10f, new Color( 0.12f, 0.13f, 0.16f ), new Angles( 0, 45, 80 ) ),
			// Sandbag barricade the defenders built at the door.
			P( 0f, -480f, 1f, 260f, 50f, 40f, new Color( 0.52f, 0.46f, 0.34f ) ),
		},
		Enemies = new List<EnemySpawnDef>
		{
			E( EnemyKind.RivalWasher, -300f, 100f ),
			E( EnemyKind.RivalWasher, 340f, 260f ),
			E( EnemyKind.StickerBandit, 160f, -80f ),
			E( EnemyKind.StickerBandit, -480f, -160f ),
			E( EnemyKind.Wasp, 0f, 300f ),
		},
	};

	/// <summary>Level 23 — museum gala: marble, statues, and the senator's security.</summary>
	static JobDef Level23_MuseumGala() => new()
	{
		Name = "Senator Vance Gala",
		Blurb = "Infiltrate a museum fundraiser to corner the senator.",
		Briefing = "Grand historic museum hosting a high-society political fundraiser — infiltrate to corner Senator Vance. "
			+ "Marble floors and priceless statues shattered, blood sprayed as her elite security turns weapons on me. She escapes to a helicopter. Pumps at the reservoir are already running.",
		BriefingTag = "ACT IV",
		ActTitle = "ACT IV — TACTICAL FORENSICS",
		Location = "Historic Museum Political Gala",
		CrimeScene = "Shattered marble floors and statues; blood sprayed as private security engages you.",
		RevealHook = "Security key card beneath catering wine and blood on the exhibition plaque.",
		IsCombatLevel = true,
		ValueMultiplier = Pay( 23 ),
		Theme = MapTheme.Interior,
		GroundColor = new Color( 0.84f, 0.82f, 0.77f ),
		GroundSize = new Vector2( 1600f, 1300f ),
		SpawnPosition = new Vector3( 0f, -520f, 0f ),
		SpawnYaw = 90f,
		Panels = new List<PanelDef>
		{
			// Wine and blood across the west gallery floor.
			FlatGround( 440f, 380f, new Color( 0.42f, 0.08f, 0.14f ), WhiteMarble, PanelShape.OilSpill, GrimePattern.Splatter,
				position: new Vector3( -360f, 120f, 1f ) ),
			// Second spray at the dais steps.
			FlatGround( 300f, 260f, Blood, WhiteMarble, PanelShape.OilSpill, GrimePattern.Splatter,
				position: new Vector3( 220f, 320f, 1f ), cellSize: 12f ),
			// Exhibition plaque — the keycard reveal.
			FlatWall( 280f, 150f, new Color( 0.42f, 0.08f, 0.14f ), WhiteMarble, PanelShape.Rounded, GrimePattern.Splatter,
				position: new Vector3( 0f, 584f, 130f ), cellSize: 11f, secrets: new List<SurfaceSecret>
				{
					new()
					{
						Symbol = SecretSymbol.Interlock, X = 0.34f, Y = 0.52f, Scale = 1.8f,
						DiscoveryId = "L23_keycard",
						Monologue = "Taped behind the donor plaque, soaked in cabernet: a dam facility keycard, clearance DELTA-7. Her own security chief stashed an exit she never got to use.",
					},
					new() { Text = "DELTA-7", X = 0.66f, Y = 0.34f, Scale = 1.5f },
				} ),
			// Toppled statue scar dragged across the east floor.
			FlatGround( 480f, 200f, new Color( 0.55f, 0.52f, 0.48f ), WhiteMarble, PanelShape.Banner, GrimePattern.Streaks,
				position: new Vector3( 420f, -120f, 1f ) ),
		},
		Decor = new List<DecorDef>
		{
			D( DecorKind.Column, -520f, 420f, size: new Vector3( 70f, 0f, 340f ), color: new Color( 0.90f, 0.88f, 0.82f ) ),
			D( DecorKind.Column, -180f, 420f, size: new Vector3( 70f, 0f, 340f ), color: new Color( 0.90f, 0.88f, 0.82f ) ),
			D( DecorKind.Column, 180f, 420f, size: new Vector3( 70f, 0f, 340f ), color: new Color( 0.90f, 0.88f, 0.82f ) ),
			D( DecorKind.Column, 520f, 420f, size: new Vector3( 70f, 0f, 340f ), color: new Color( 0.90f, 0.88f, 0.82f ) ),
			D( DecorKind.Column, -520f, -280f, size: new Vector3( 70f, 0f, 340f ), color: new Color( 0.90f, 0.88f, 0.82f ) ),
			D( DecorKind.Column, 520f, -280f, size: new Vector3( 70f, 0f, 340f ), color: new Color( 0.90f, 0.88f, 0.82f ) ),
			D( DecorKind.TiledWall, 0f, 600f, size: new Vector3( 900f, 24f, 280f ), color: WhiteMarble ),
			D( DecorKind.Statue, -300f, 440f, yaw: 20f, size: new Vector3( 1.1f, 1f, 1f ) ),
			D( DecorKind.Statue, 360f, 460f, yaw: -15f ),
			D( DecorKind.Rubble, 480f, -60f, yaw: 30f, color: new Color( 0.88f, 0.86f, 0.80f ) ),
			D( DecorKind.DisplayCase, -600f, -80f ),
			D( DecorKind.DisplayCase, -600f, -300f, size: new Vector3( 0.9f, 1f, 1f ) ),
			D( DecorKind.DisplayCase, 620f, 200f ),
			D( DecorKind.Platform, 0f, 460f, size: new Vector3( 420f, 200f, 16f ), color: new Color( 0.62f, 0.14f, 0.18f ) ),
			D( DecorKind.LampPost, -680f, -480f, size: new Vector3( 0.7f, 1f, 1f ) ),
			D( DecorKind.LampPost, 680f, -480f, size: new Vector3( 0.7f, 1f, 1f ) ),
		},
		Props = new List<PropDef>
		{
			// Red carpet from the entrance to the dais.
			P( 0f, -80f, 3f, 200f, 850f, 1f, new Color( 0.55f, 0.10f, 0.14f ) ),
			// Catering tables and dropped trays.
			P( -240f, -320f, 1f, 180f, 60f, 80f, new Color( 0.92f, 0.92f, 0.90f ) ),
			P( 260f, -340f, 1f, 180f, 60f, 80f, new Color( 0.92f, 0.92f, 0.90f ) ),
			P( -160f, -240f, 1f, 40f, 40f, 3f, new Color( 0.78f, 0.78f, 0.82f ), new Angles( 0, 30, 0 ) ),
			P( 120f, -260f, 1f, 40f, 40f, 3f, new Color( 0.78f, 0.78f, 0.82f ), new Angles( 0, -45, 0 ) ),
			// The toppled statue itself.
			P( 420f, -180f, 1f, 260f, 60f, 60f, new Color( 0.90f, 0.89f, 0.84f ), new Angles( 0, 15, 85 ) ),
		},
		Enemies = new List<EnemySpawnDef>
		{
			E( EnemyKind.RivalWasher, -300f, 260f ),
			E( EnemyKind.RivalWasher, 300f, 60f ),
			E( EnemyKind.RivalWasher, 0f, -240f ),
			E( EnemyKind.StickerBandit, -520f, -180f ),
			E( EnemyKind.StickerBandit, 560f, 340f ),
		},
	};

	/// <summary>Level 24 — the dam crest: neon channels and crusted override valves.</summary>
	static JobDef Level24_Reservoir() => new()
	{
		Name = "Reservoir Infiltration",
		Blurb = "Stop automated pumps dumping lethal compound into the water.",
		Briefing = "Massive multi-tiered concrete dam — fight through mercenaries protecting automated pumps dumping the final lethal dose. "
			+ "Pristine water channels turning terrifying neon-blue. Clear bio-crust off emergency valves to reach manual override levers.",
		BriefingTag = "ACT IV",
		ActTitle = "ACT IV — TACTICAL FORENSICS",
		Location = "City Dam & Water Reservoir",
		CrimeScene = "Pristine water channels turning a terrifying glowing neon-blue.",
		RevealHook = "Manual override levers beneath toxic bio-crust on emergency release valves.",
		IsCombatLevel = true,
		ValueMultiplier = Pay( 24 ),
		Theme = MapTheme.Dam,
		GroundColor = new Color( 0.56f, 0.56f, 0.58f ),
		GroundSize = new Vector2( 2000f, 1500f ),
		SpawnPosition = new Vector3( 0f, -580f, 0f ),
		SpawnYaw = 90f,
		Panels = new List<PanelDef>
		{
			// West valve cluster — crust off, levers underneath.
			FlatGround( 340f, 340f, ToxicGreen, PaleConcrete, PanelShape.Cross, GrimePattern.Speckled,
				position: new Vector3( -500f, 170f, 1f ), followUp: ToolType.ScrubBrush,
				followUpColor: new Color( 0.20f, 0.38f, 0.10f ), followUpWet: false,
				secrets: new List<SurfaceSecret>
				{
					new()
					{
						Text = "PULL TO PURGE", X = 0.5f, Y = 0.52f, Scale = 1.4f,
						DiscoveryId = "L24_override",
						Monologue = "The lever housings are stamped PULL TO PURGE. Two stations, both crusted shut with bio-growth. Clear them both and the pumps choke on their own poison.",
					},
				} ),
			// East valve cluster.
			FlatGround( 340f, 340f, ToxicGreen, PaleConcrete, PanelShape.Cross, GrimePattern.Speckled,
				position: new Vector3( 500f, -170f, 1f ), followUp: ToolType.ScrubBrush,
				followUpColor: new Color( 0.20f, 0.38f, 0.10f ), followUpWet: false ),
			// Neon overspill along the center channel curb (short of the west valve panel
			// so the two grime sheets never overlap on the same plane).
			FlatGround( 640f, 150f, NeonBlue, PaleConcrete, PanelShape.Banner, GrimePattern.Streaks,
				position: new Vector3( 0f, 105f, 1f ) ),
			// Pump-house wall — dosage chart under the spray.
			FlatWall( 320f, 180f, NeonBlue, new Color( 0.48f, 0.50f, 0.53f ), PanelShape.Rounded, GrimePattern.Splatter,
				position: new Vector3( 0f, 447f, 140f ), cellSize: 12f ),
		},
		Decor = new List<DecorDef>
		{
			D( DecorKind.WaterChannel, 0f, 0f, size: new Vector3( 1700f, 140f, 1f ), color: NeonBlue ),
			D( DecorKind.WaterChannel, 0f, 340f, size: new Vector3( 1700f, 140f, 1f ), color: new Color( 0.16f, 0.44f, 0.55f ) ),
			D( DecorKind.WaterChannel, 0f, -340f, size: new Vector3( 1700f, 140f, 1f ), color: new Color( 0.12f, 0.55f, 0.72f ) ),
			D( DecorKind.MachineCore, 0f, 560f, size: new Vector3( 0.9f, 1f, 1f ), color: NeonBlue ),
			D( DecorKind.Catwalk, 560f, 170f, size: new Vector3( 600f, 90f, 160f ) ),
			D( DecorKind.Catwalk, -560f, -170f, size: new Vector3( 600f, 90f, 160f ) ),
			D( DecorKind.ValveStation, -420f, 240f, size: new Vector3( 1.1f, 1f, 1f ) ),
			D( DecorKind.ValveStation, -580f, 100f ),
			D( DecorKind.ValveStation, 420f, -240f, size: new Vector3( 1.1f, 1f, 1f ) ),
			D( DecorKind.ValveStation, 580f, -100f ),
			D( DecorKind.PipeRun, -300f, 480f, size: new Vector3( 700f, 0f, 110f ), color: new Color( 0.55f, 0.58f, 0.62f ) ),
			D( DecorKind.Railing, 0f, 700f, size: new Vector3( 1900f, 1f, 1f ), color: new Color( 0.60f, 0.62f, 0.66f ) ),
			D( DecorKind.ConcreteBarrier, -700f, -480f, yaw: 12f, size: new Vector3( 280f, 1f, 1f ) ),
			D( DecorKind.Floodlight, -240f, -440f, yaw: 30f ),
			D( DecorKind.Floodlight, 300f, -460f, yaw: -25f ),
			D( DecorKind.Generator, 760f, 420f, yaw: -8f ),
			D( DecorKind.LampPost, -880f, -600f, size: new Vector3( 1.1f, 1f, 1f ) ),
			D( DecorKind.LampPost, 880f, -600f, size: new Vector3( 1.1f, 1f, 1f ) ),
		},
		Props = new List<PropDef>
		{
			// Pump intakes dumping into the center channel.
			P( -200f, 60f, 30f, 60f, 120f, 40f, new Color( 0.38f, 0.40f, 0.46f ) ),
			P( 200f, 60f, 30f, 60f, 120f, 40f, new Color( 0.38f, 0.40f, 0.46f ) ),
			// Warning stripes at each crossing.
			P( -500f, 0f, 3f, 160f, 24f, 1f, BollardYellow ),
			P( 500f, 0f, 3f, 160f, 24f, 1f, BollardYellow ),
		},
		Enemies = new List<EnemySpawnDef>
		{
			E( EnemyKind.RivalWasher, -400f, 60f ),
			E( EnemyKind.RivalWasher, 460f, -60f ),
			E( EnemyKind.RivalWasher, 0f, 240f ),
			E( EnemyKind.OilLeech, -560f, 240f ),
			E( EnemyKind.OilLeech, 560f, -260f ),
			E( EnemyKind.OilLeech, 100f, -140f ),
			E( EnemyKind.Wasp, -200f, 340f ),
		},
	};

	/// <summary>Level 25 — the core pump room. Last job. Biggest stain.</summary>
	static JobDef Level25_CleanSlate() => new()
	{
		Name = "Ultimate Clean Slate",
		Blurb = "Final boss — strip the Commander's exosuit plating with your rig.",
		Briefing = "Core pump room of the dam — Aegis command center covered in blood, burning fuel, and neon-blue toxin. "
			+ "The Commander of the black-ops cleanup division fights in an armored exosuit with chemical flamethrowers. Blast the ballistic plating off and hit the glowing core. Last job. Biggest stain.",
		BriefingTag = "ACT IV",
		ActTitle = "ACT IV — TACTICAL FORENSICS",
		Location = "Dam Command Center / Aegis HQ",
		CrimeScene = "Command center covered in blood, burning fuel, and neon-blue toxin.",
		RevealHook = "Blast ballistic plating off the Commander's suit to expose the vulnerable core.",
		IsCombatLevel = true,
		ValueMultiplier = Pay( 25 ),
		Theme = MapTheme.Underground,
		GroundColor = new Color( 0.22f, 0.22f, 0.26f ),
		GroundSize = new Vector2( 1700f, 1400f ),
		SpawnPosition = new Vector3( 0f, -580f, 0f ),
		SpawnYaw = 90f,
		Panels = new List<PanelDef>
		{
			// The fight's aftermath, written in blood (kept clear of the toxin lake so the
			// two grime sheets never overlap on the same plane).
			FlatGround( 480f, 400f, Blood, DarkFloor, PanelShape.OilSpill, GrimePattern.Splatter,
				position: new Vector3( -440f, 40f, 1f ) ),
			// Burning fuel slick, scorched black.
			FlatGround( 520f, 360f, Soot, DarkFloor, PanelShape.OilSpill, GrimePattern.Splatter,
				position: new Vector3( 340f, -140f, 1f ) ),
			// Toxin lake at the machine's feet.
			FlatGround( 460f, 380f, NeonBlue, DarkFloor, PanelShape.Ellipse, GrimePattern.Organic,
				position: new Vector3( 60f, 280f, 1f ), followUp: ToolType.ScrubBrush,
				followUpColor: new Color( 0.10f, 0.48f, 0.66f ), followUpWet: false ),
			// The core plate — strip the ballistic paint, expose the glow.
			FlatWall( 300f, 280f, new Color( 0.38f, 0.40f, 0.46f ), NeonBlue, PanelShape.Window, GrimePattern.Streaks,
				position: new Vector3( 0f, 386f, 180f ), cellSize: 11f, secrets: new List<SurfaceSecret>
				{
					new()
					{
						Text = "CLEAN SLATE", X = 0.5f, Y = 0.52f, Scale = 1.8f,
						DiscoveryId = "L25_core",
						Monologue = "Under the plating, etched around the core housing: PROJECT CLEAN SLATE — FINAL PHASE. Not tonight. Tonight the only thing getting wiped is this machine.",
					},
					new() { Symbol = SecretSymbol.Target, X = 0.5f, Y = 0.26f, Scale = 1.6f },
				} ),
		},
		Decor = new List<DecorDef>
		{
			D( DecorKind.MachineCore, 0f, 540f, size: new Vector3( 1.5f, 1f, 1f ), color: NeonBlue ),
			D( DecorKind.BlastDoor, 0f, -660f, yaw: 180f, size: new Vector3( 280f, 30f, 280f ) ),
			D( DecorKind.PipeRun, -560f, 420f, size: new Vector3( 500f, 0f, 130f ), color: NeonBlue ),
			D( DecorKind.PipeRun, 560f, 420f, size: new Vector3( 500f, 0f, 130f ), color: new Color( 0.55f, 0.58f, 0.62f ) ),
			D( DecorKind.ControlDesk, -520f, -320f, yaw: 40f ),
			D( DecorKind.ServerRack, -700f, -120f, color: new Color( 0.95f, 0.35f, 0.25f ) ),
			D( DecorKind.ServerRack, 700f, -80f, color: new Color( 0.25f, 0.85f, 1f ) ),
			D( DecorKind.Rubble, 560f, 200f, yaw: 65f, size: new Vector3( 1.3f, 1f, 1f ) ),
			D( DecorKind.Rubble, -620f, 260f, yaw: -30f ),
			D( DecorKind.BarrelCluster, 420f, -420f, yaw: 20f, color: new Color( 0.80f, 0.30f, 0.10f ) ),
			D( DecorKind.SteamVent, -220f, -100f, size: new Vector3( 1.2f, 1f, 1f ) ),
			D( DecorKind.SteamVent, 200f, 100f ),
			D( DecorKind.Catwalk, 0f, -300f, size: new Vector3( 1100f, 90f, 190f ) ),
			D( DecorKind.Floodlight, -300f, -460f, yaw: 40f ),
			D( DecorKind.Floodlight, 320f, -480f, yaw: -35f ),
		},
		Props = new List<PropDef>
		{
			// Burning fuel drums — ember glow decals around the scorch.
			P( 420f, -60f, 1f, 44f, 44f, 60f, new Color( 0.55f, 0.18f, 0.08f ), new Angles( 0, 20, 10 ) ),
			P( 500f, -220f, 1f, 44f, 44f, 60f, new Color( 0.55f, 0.18f, 0.08f ), new Angles( 0, -35, 0 ) ),
			P( 380f, -140f, 3f, 200f, 160f, 1f, new Color( 0.95f, 0.45f, 0.10f ) ),
			// Exosuit plating fragments blasted off in the fight.
			P( -140f, 160f, 1f, 60f, 40f, 8f, new Color( 0.38f, 0.40f, 0.46f ), new Angles( 0, 30, 8 ) ),
			P( 80f, 60f, 1f, 50f, 34f, 8f, new Color( 0.38f, 0.40f, 0.46f ), new Angles( 0, -55, 5 ) ),
			P( -40f, -60f, 1f, 44f, 30f, 8f, new Color( 0.38f, 0.40f, 0.46f ), new Angles( 0, 70, 12 ) ),
		},
		Enemies = new List<EnemySpawnDef>
		{
			E( EnemyKind.StrayDog, 0f, 380f ),
			E( EnemyKind.RivalWasher, -360f, 200f ),
			E( EnemyKind.RivalWasher, 360f, 160f ),
			E( EnemyKind.RivalWasher, 0f, -200f ),
			E( EnemyKind.Wasp, -200f, 0f ),
			E( EnemyKind.Wasp, 240f, 60f ),
		},
	};
}
