namespace UnderPressure;

/// <summary>Act III — the hunted (levels 11–15).</summary>
public static partial class CampaignCatalog
{
	/// <summary>Level 11 — timed crash-scene erase under a highway overpass.</summary>
	static JobDef Level11_DarkHighway() => new()
	{
		Name = "Dark Highway",
		Blurb = "Erase a crash scene before police arrive — clock is ticking.",
		Briefing = "Secluded highway underpass in a downpour — clean a fiery car crash before local police or emergency services arrive. "
			+ "Shattered windshield coated in blood, scorch mark from a localized explosion. Flashlights move in the woods. They're watching me work.",
		BriefingTag = "ACT III",
		ActTitle = "ACT III — THE HUNTED",
		Location = "Highway Underpass in Downpour",
		CrimeScene = "Blood-coated shattered windshield and a localized explosion scorch mark on asphalt.",
		RevealHook = "GPS coordinates scratched into the guardrail with a key.",
		TimeLimitSeconds = 420f,
		ValueMultiplier = Pay( 11 ),
		Theme = MapTheme.Highway,
		GroundColor = new Color( 0.26f, 0.27f, 0.29f ),
		GroundSize = new Vector2( 2200f, 1400f ),
		SpawnPosition = new Vector3( -60f, -520f, 0f ),
		SpawnYaw = 90f,
		Panels = new List<PanelDef>
		{
			// Explosion scorch ring around the wreck.
			FlatGround( 400f, 360f, Soot, new Color( 0.26f, 0.27f, 0.29f ), PanelShape.OilSpill, GrimePattern.Splatter,
				position: new Vector3( 120f, 60f, 1f ) ),
			// Blood and windshield glass thrown forward of the car.
			FlatGround( 280f, 220f, Blood, new Color( 0.26f, 0.27f, 0.29f ), PanelShape.OilSpill, GrimePattern.Splatter,
				position: new Vector3( -260f, -60f, 1f ), cellSize: 12f ),
			// Panic skids arcing off the shoulder.
			FlatGround( 760f, 170f, OilBlack, new Color( 0.26f, 0.27f, 0.29f ), PanelShape.Banner, GrimePattern.Streaks,
				position: new Vector3( -420f, -320f, 1f ) ),
			// Guardrail — mud film over the scratched coordinates.
			// 4 units off the guardrail beam face (y=376) so the base layer clears it.
			FlatWall( 680f, 60f, Grime, SteelClean, PanelShape.Banner, GrimePattern.Streaks,
				position: new Vector3( 200f, 372f, 38f ), cellSize: 10f, secrets: new List<SurfaceSecret>
				{
					new()
					{
						Text = "41.2N 73.6W", X = 0.42f, Y = 0.5f, Scale = 1.5f,
						DiscoveryId = "L11_gps_coords",
						Monologue = "Coordinates, scratched with a key by someone who knew they weren't walking away. That's downtown. That's the Meridian tower. Somebody wanted the next cleaner to look up.",
					},
					new() { Symbol = SecretSymbol.Scratches, X = 0.78f, Y = 0.5f, Scale = 1.2f },
				} ),
		},
		Decor = new List<DecorDef>
		{
			D( DecorKind.Overpass, 0f, 240f, size: new Vector3( 1700f, 420f, 330f ) ),
			D( DecorKind.WreckedCar, 120f, 40f, yaw: 24f, size: new Vector3( 1.05f, 1f, 1f ) ),
			D( DecorKind.GuardRail, 200f, 380f, size: new Vector3( 700f, 1f, 1f ) ),
			D( DecorKind.GuardRail, 0f, -620f, size: new Vector3( 1800f, 1f, 1f ) ),
			D( DecorKind.ConcreteBarrier, -700f, 300f, yaw: 8f, size: new Vector3( 300f, 1f, 1f ) ),
			D( DecorKind.ConcreteBarrier, 720f, 260f, yaw: -6f, size: new Vector3( 300f, 1f, 1f ) ),
			D( DecorKind.LampPost, -560f, 460f, size: new Vector3( 1.1f, 1f, 1f ) ),
			D( DecorKind.LampPost, 620f, 460f, size: new Vector3( 1.1f, 1f, 1f ) ),
			D( DecorKind.Floodlight, -820f, -420f, yaw: 55f ),
			D( DecorKind.Floodlight, 860f, -380f, yaw: -55f ),
			D( DecorKind.Tree, -940f, 80f, size: new Vector3( 1.3f, 1f, 1f ), color: new Color( 0.10f, 0.16f, 0.10f ) ),
			D( DecorKind.Tree, 960f, -80f, size: new Vector3( 1.2f, 1f, 1f ), color: new Color( 0.10f, 0.16f, 0.10f ) ),
		},
		Props = new List<PropDef>
		{
			// Lane markings under the overpass.
			P( -400f, 140f, 3f, 240f, 10f, 1f, new Color( 0.85f, 0.72f, 0.20f ) ),
			P( 60f, 140f, 3f, 240f, 10f, 1f, new Color( 0.85f, 0.72f, 0.20f ) ),
			P( 520f, 140f, 3f, 240f, 10f, 1f, new Color( 0.85f, 0.72f, 0.20f ) ),
			P( -400f, -140f, 3f, 240f, 10f, 1f, new Color( 0.85f, 0.72f, 0.20f ) ),
			P( 60f, -140f, 3f, 240f, 10f, 1f, new Color( 0.85f, 0.72f, 0.20f ) ),
			P( 520f, -140f, 3f, 240f, 10f, 1f, new Color( 0.85f, 0.72f, 0.20f ) ),
			// Torn bumper and a hubcap thrown clear.
			P( 340f, -180f, 1f, 90f, 22f, 16f, new Color( 0.16f, 0.14f, 0.13f ), new Angles( 0, 40, 8 ) ),
			P( -80f, 220f, 1f, 34f, 34f, 6f, new Color( 0.60f, 0.62f, 0.66f ) ),
		},
		Enemies = new List<EnemySpawnDef>
		{
			E( EnemyKind.Rat, -500f, 100f ),
			E( EnemyKind.Pigeon, 300f, 300f ),
			E( EnemyKind.Pigeon, -200f, 420f ),
		},
	};

	/// <summary>Level 12 — gutted data center: coolant film, soot, and a free-press execution.</summary>
	static JobDef Level12_ServerFarm() => new()
	{
		Name = "Server Farm",
		Blurb = "Clear coolant leak and fire soot from destroyed server racks.",
		Briefing = "Windowless data center — catastrophic coolant leak and heavy fire soot across rows of destroyed server racks. "
			+ "Servers bashed with axes, blood splattered across flashing LED panels. I'm erasing the evidence of a free-press execution.",
		BriefingTag = "ACT III",
		ActTitle = "ACT III — THE HUNTED",
		Location = "High-Tech Data Center",
		CrimeScene = "Servers bashed with axes; blood splattered across flashing LED control panels.",
		RevealHook = "Journalism outlet logo beneath charred soot: FREE PRESS.",
		ValueMultiplier = Pay( 12 ),
		Theme = MapTheme.Interior,
		GroundColor = new Color( 0.24f, 0.25f, 0.28f ),
		GroundSize = new Vector2( 1600f, 1200f ),
		SpawnPosition = new Vector3( 0f, -460f, 0f ),
		SpawnYaw = 90f,
		Panels = new List<PanelDef>
		{
			// Coolant lake — squeegee the film once the muck is off.
			FlatGround( 620f, 420f, new Color( 0.28f, 0.52f, 0.58f ), DarkFloor, PanelShape.Ellipse, GrimePattern.Organic,
				position: new Vector3( -240f, 120f, 1f ), followUp: ToolType.Squeegee,
				secrets: new List<SurfaceSecret>
				{
					new()
					{
						Text = "FREE PRESS", X = 0.5f, Y = 0.52f, Scale = 2.4f,
						DiscoveryId = "L12_free_press",
						Monologue = "The floor logo comes up under the coolant: FREE PRESS — the outlet that was about to run the Vance story. They didn't hack the servers. They took axes to them, and to the people backing them up.",
					},
				} ),
			// Fire soot fan where the racks burned.
			FlatGround( 460f, 380f, Soot, DarkFloor, PanelShape.OilSpill, GrimePattern.Splatter,
				position: new Vector3( 360f, 260f, 1f ) ),
			// Blood across the still-lit rack faces.
			// Rack LED strips reach y=517 — keep the panel's base layer a unit clear of them.
			FlatWall( 340f, 130f, Blood, new Color( 0.12f, 0.13f, 0.16f ), PanelShape.Full, GrimePattern.Splatter,
				position: new Vector3( -100f, 514f, 90f ), cellSize: 11f ),
		},
		Decor = new List<DecorDef>
		{
			// Standing rack rows.
			D( DecorKind.ServerRack, -500f, 560f, color: new Color( 0.20f, 0.90f, 0.55f ) ),
			D( DecorKind.ServerRack, -300f, 560f, color: new Color( 0.20f, 0.90f, 0.55f ) ),
			D( DecorKind.ServerRack, -100f, 560f, color: new Color( 0.95f, 0.35f, 0.25f ) ),
			D( DecorKind.ServerRack, 100f, 560f, color: new Color( 0.20f, 0.90f, 0.55f ) ),
			D( DecorKind.ServerRack, 300f, 560f, color: new Color( 0.95f, 0.35f, 0.25f ) ),
			D( DecorKind.ServerRack, 500f, 560f, color: new Color( 0.20f, 0.90f, 0.55f ) ),
			D( DecorKind.ServerRack, -560f, 240f, yaw: 90f, color: new Color( 0.20f, 0.90f, 0.55f ) ),
			D( DecorKind.ServerRack, -560f, 40f, yaw: 90f, color: new Color( 0.95f, 0.35f, 0.25f ) ),
			D( DecorKind.ControlDesk, 120f, -260f, yaw: 180f ),
			D( DecorKind.CrateStack, 620f, -200f, yaw: 25f, size: new Vector3( 0.85f, 1f, 1f ), color: new Color( 0.30f, 0.30f, 0.34f ) ),
			D( DecorKind.Floodlight, -420f, -300f, yaw: 30f ),
		},
		Props = new List<PropDef>
		{
			// Toppled racks lying in the coolant.
			P( 140f, 120f, 1f, 170f, 60f, 40f, new Color( 0.12f, 0.13f, 0.16f ), new Angles( 0, 20, 0 ) ),
			P( 420f, 40f, 1f, 170f, 60f, 40f, new Color( 0.10f, 0.11f, 0.14f ), new Angles( 0, -35, 0 ) ),
			// The axe, left behind.
			P( 240f, 180f, 42f, 12f, 70f, 6f, new Color( 0.42f, 0.30f, 0.18f ), new Angles( 0, 60, 15 ) ),
			P( 240f, 210f, 46f, 20f, 16f, 8f, new Color( 0.62f, 0.64f, 0.68f ), new Angles( 0, 60, 15 ) ),
			// Cable trays overhead between rows.
			P( 0f, 400f, 300f, 900f, 40f, 8f, new Color( 0.34f, 0.36f, 0.40f ) ),
			P( 0f, 150f, 300f, 900f, 40f, 8f, new Color( 0.34f, 0.36f, 0.40f ) ),
		},
		Enemies = new List<EnemySpawnDef>
		{
			E( EnemyKind.StickerBandit, 380f, 380f ),
			E( EnemyKind.StickerBandit, -420f, -140f ),
			E( EnemyKind.RivalWasher, 100f, 200f ),
			E( EnemyKind.Rat, -200f, 340f ),
		},
	};

	/// <summary>Level 13 — the penthouse from the guardrail coordinates.</summary>
	static JobDef Level13_Penthouse() => new()
	{
		Name = "Penthouse Suite",
		Blurb = "High-pressure cleanup at the coordinates from the highway.",
		Briefing = "Breathtaking skyscraper penthouse — multi-room crime scene at the GPS coordinates from the highway job. "
			+ "Furniture shattered, mirrors smashed, blood trail to a broken panoramic window. The CEO was thrown out. I'm cleaning his office.",
		BriefingTag = "ACT III",
		ActTitle = "ACT III — THE HUNTED",
		Location = "Luxury Skyscraper Penthouse",
		CrimeScene = "Shattered furniture and mirrors; blood trail to a broken panoramic window.",
		RevealHook = "Laser-etched blueprint under desk varnish: PROJECT CLEAN SLATE.",
		ValueMultiplier = Pay( 13 ),
		Theme = MapTheme.Rooftop,
		GroundColor = new Color( 0.80f, 0.78f, 0.73f ),
		GroundSize = new Vector2( 1400f, 1200f ),
		SpawnPosition = new Vector3( 0f, -460f, 0f ),
		SpawnYaw = 90f,
		Panels = new List<PanelDef>
		{
			// Drag trail across the marble toward the window gap.
			FlatGround( 780f, 230f, Blood, WhiteMarble, PanelShape.Banner, GrimePattern.Streaks,
				position: new Vector3( 0f, 300f, 1f ) ),
			// The office corner — blueprint etched beneath the varnish.
			FlatGround( 320f, 260f, OldBlood, WhiteMarble, PanelShape.Rounded, GrimePattern.Splatter,
				position: new Vector3( 330f, 20f, 1f ), secrets: new List<SurfaceSecret>
				{
					new()
					{
						Text = "PROJECT CLEAN SLATE", X = 0.5f, Y = 0.55f, Scale = 1.4f,
						DiscoveryId = "L13_clean_slate",
						Monologue = "Etched into the floor under his own desk: PROJECT CLEAN SLATE — intake schematics, dosage tables, a city grid. He wasn't hiding it from them. He was hiding it from everyone else.",
					},
					new() { Symbol = SecretSymbol.Eye, X = 0.5f, Y = 0.30f, Scale = 1.6f },
				} ),
			// Surviving window pane — mirror dust and prints, squeegee finish.
			FlatWall( 320f, 170f, new Color( 0.74f, 0.77f, 0.80f ), GlassDark, PanelShape.Window, GrimePattern.Speckled,
				position: new Vector3( -350f, 404f, 140f ), surface: CleanSurface.Glass,
				followUp: ToolType.Squeegee, cellSize: 11f ),
		},
		Decor = new List<DecorDef>
		{
			// Interior walls flanking the blown-out panoramic gap.
			D( DecorKind.TiledWall, -400f, 420f, size: new Vector3( 460f, 24f, 240f ), color: new Color( 0.32f, 0.30f, 0.32f ) ),
			D( DecorKind.TiledWall, 400f, 420f, size: new Vector3( 460f, 24f, 240f ), color: new Color( 0.32f, 0.30f, 0.32f ) ),
			D( DecorKind.Railing, 0f, 420f, size: new Vector3( 340f, 1f, 1f ), color: new Color( 0.85f, 0.86f, 0.88f ) ),
			D( DecorKind.Desk, 330f, 140f, yaw: 195f ),
			D( DecorKind.Sofa, -300f, 60f, yaw: 15f, color: new Color( 0.26f, 0.30f, 0.40f ) ),
			D( DecorKind.Sofa, -160f, -160f, yaw: 100f, color: new Color( 0.26f, 0.30f, 0.40f ) ),
			D( DecorKind.Statue, -520f, 180f, yaw: 30f, size: new Vector3( 0.85f, 1f, 1f ), color: new Color( 0.88f, 0.87f, 0.84f ) ),
			D( DecorKind.DisplayCase, 540f, 240f, size: new Vector3( 0.9f, 1f, 1f ) ),
			D( DecorKind.LampPost, -560f, -320f, size: new Vector3( 0.7f, 1f, 1f ) ),
			D( DecorKind.RoofUnit, 620f, -420f, yaw: 10f ),
		},
		Props = new List<PropDef>
		{
			// Shattered mirror shards and a downed bar cart.
			P( -80f, 180f, 1f, 60f, 40f, 2f, new Color( 0.72f, 0.80f, 0.86f ), new Angles( 0, 25, 0 ) ),
			P( 40f, 240f, 1f, 44f, 30f, 2f, new Color( 0.72f, 0.80f, 0.86f ), new Angles( 0, -40, 0 ) ),
			P( 120f, -120f, 1f, 70f, 44f, 56f, new Color( 0.72f, 0.62f, 0.34f ), new Angles( 0, 30, 80 ) ),
			// Glass teeth left in the window gap.
			P( -100f, 424f, 250f, 40f, 6f, 60f, new Color( 0.55f, 0.66f, 0.72f ), new Angles( 0, 0, 15 ) ),
			P( 110f, 424f, 245f, 34f, 6f, 50f, new Color( 0.55f, 0.66f, 0.72f ), new Angles( 0, 0, -20 ) ),
		},
		Enemies = new List<EnemySpawnDef>
		{
			E( EnemyKind.Pigeon, 0f, 360f ),
			E( EnemyKind.Pigeon, -260f, 240f ),
			E( EnemyKind.Pigeon, 300f, -160f ),
			E( EnemyKind.Wasp, 160f, 120f ),
		},
	};

	/// <summary>Level 14 — freezing meatpacking floor: two kinds of blood.</summary>
	static JobDef Level14_MeatpackingPlant() => new()
	{
		Name = "Meatpacking Plant",
		Blurb = "Scrub a scene hidden among hooks and animal waste.",
		Briefing = "Freezing industrial slaughterhouse — clean a gruesome scene hidden among animal waste and meat hooks. "
			+ "Human blood pools differently than animal blood on freezing metal. I can tell the difference now. That's not a skill I wanted.",
		BriefingTag = "ACT III",
		ActTitle = "ACT III — THE HUNTED",
		Location = "Industrial Meatpacking Plant",
		CrimeScene = "Human blood pooling in distinct patterns separate from animal blood on freezing floors.",
		RevealHook = "Hit list etched into stainless steel — VANCE PRESSURE WASHING at the bottom.",
		ValueMultiplier = Pay( 14 ),
		Theme = MapTheme.Interior,
		GroundColor = new Color( 0.55f, 0.58f, 0.60f ),
		GroundSize = new Vector2( 1600f, 1100f ),
		SpawnPosition = new Vector3( 0f, -440f, 0f ),
		SpawnYaw = 90f,
		Panels = new List<PanelDef>
		{
			// Human pool — tight, dark, dragged once.
			FlatGround( 300f, 260f, Blood, SteelClean, PanelShape.OilSpill, GrimePattern.Splatter,
				position: new Vector3( -380f, 80f, 1f ), cellSize: 12f ),
			// Animal waste smear under the hook lines — wider, browner, everywhere.
			FlatGround( 560f, 320f, new Color( 0.44f, 0.26f, 0.18f ), SteelClean, PanelShape.Ellipse, GrimePattern.Organic,
				position: new Vector3( 260f, 180f, 1f ) ),
			// Floor drain ring, clotted.
			FlatGround( 260f, 260f, OldBlood, SteelClean, PanelShape.Ring, GrimePattern.Organic,
				position: new Vector3( -60f, -200f, 1f ), cellSize: 12f ),
			// Stainless partition — grease over the etched list.
			FlatWall( 340f, 160f, Soot, SteelClean, PanelShape.Rounded, GrimePattern.Streaks,
				position: new Vector3( 440f, 464f, 120f ), cellSize: 11f, secrets: new List<SurfaceSecret>
				{
					new()
					{
						Symbol = SecretSymbol.Target, X = 0.28f, Y = 0.55f, Scale = 1.8f,
						DiscoveryId = "L14_hit_list",
						Monologue = "Names, etched into the steel and crossed out one by one. The reporter. The dock foreman. The aide. And at the bottom, not crossed out yet: VANCE PRESSURE WASHING.",
					},
					new() { Text = "VANCE P.W.", X = 0.62f, Y = 0.35f, Scale = 1.4f },
				} ),
		},
		Decor = new List<DecorDef>
		{
			D( DecorKind.HookRail, -200f, 250f, size: new Vector3( 900f, 1f, 1f ) ),
			D( DecorKind.HookRail, 100f, 440f, size: new Vector3( 1100f, 1f, 1f ) ),
			D( DecorKind.TiledWall, 440f, 480f, size: new Vector3( 400f, 24f, 220f ), color: SteelClean ),
			D( DecorKind.SteelTable, -420f, -180f, yaw: 5f ),
			D( DecorKind.SteelTable, -100f, -60f, yaw: -8f ),
			D( DecorKind.SteelTable, 480f, -160f, yaw: 90f ),
			D( DecorKind.SteamVent, -600f, 300f ),
			D( DecorKind.SteamVent, 620f, 100f, size: new Vector3( 0.8f, 1f, 1f ) ),
			D( DecorKind.BarrelCluster, 640f, -320f, yaw: 30f, color: new Color( 0.30f, 0.34f, 0.38f ) ),
			D( DecorKind.CrateStack, -660f, -120f, yaw: -20f, size: new Vector3( 0.9f, 1f, 1f ) ),
			D( DecorKind.Floodlight, 200f, -320f, yaw: -15f ),
		},
		Props = new List<PropDef>
		{
			// Conveyor stub + cleaver block.
			P( 260f, -20f, 1f, 260f, 60f, 40f, new Color( 0.38f, 0.40f, 0.44f ) ),
			P( -100f, -50f, 84f, 30f, 22f, 10f, new Color( 0.50f, 0.36f, 0.22f ) ),
			// Frost decals creeping from the freezer end.
			P( -700f, 200f, 3f, 200f, 340f, 1f, new Color( 0.82f, 0.88f, 0.94f ) ),
			P( -640f, -260f, 3f, 150f, 220f, 1f, new Color( 0.82f, 0.88f, 0.94f ) ),
		},
		Enemies = new List<EnemySpawnDef>
		{
			E( EnemyKind.Rat, -300f, 200f ),
			E( EnemyKind.Rat, 200f, 300f ),
			E( EnemyKind.Rat, 500f, -60f ),
			E( EnemyKind.OilLeech, -100f, -260f ),
			E( EnemyKind.OilLeech, 340f, 60f ),
		},
	};

	/// <summary>Level 15 — ambushed at your own shop. First combat job.</summary>
	static JobDef Level15_AmbushAtTheShop() => new()
	{
		Name = "Ambush at the Shop",
		Blurb = "Your garage is trashed — hitmen wait in the shadows.",
		Briefing = "I come home to pack my things and find my shop trashed, walls spray-painted with targets. Hitmen waited in the shadows. "
			+ "I don't have a gun — I have my custom rig. Acid-wash strips their cloaking armor and I blast my way out. I'm not the cleaner anymore. I'm a combatant.",
		BriefingTag = "ACT III",
		ActTitle = "ACT III — THE HUNTED",
		Location = "Vance Pressure Washing HQ",
		CrimeScene = "Overturned equipment and target symbols spray-painted across your own walls.",
		RevealHook = "Industrial acid-wash melts cloaking armor off an elite assassin.",
		IsCombatLevel = true,
		ValueMultiplier = Pay( 15 ),
		Theme = MapTheme.Storefront,
		GroundColor = new Color( 0.40f, 0.40f, 0.42f ),
		GroundSize = new Vector2( 1700f, 1400f ),
		SpawnPosition = new Vector3( 0f, -540f, 0f ),
		SpawnYaw = 90f,
		Panels = new List<PanelDef>
		{
			// Target wall, west — they tagged your shop with your own death warrant.
			FlatWall( 300f, 180f, new Color( 0.80f, 0.12f, 0.10f ), new Color( 0.55f, 0.57f, 0.60f ),
				PanelShape.Full, GrimePattern.Splatter,
				position: new Vector3( -520f, 264f, 130f ), cellSize: 12f, graffiti: new List<GraffitiLine>
				{
					new() { Text = "LAST JOB LEO", X = 0.5f, Y = 0.30f, Scale = 1.4f, Color = new Color( 0.95f, 0.95f, 0.92f ) },
				} ),
			// Target wall, east.
			FlatWall( 300f, 180f, new Color( 0.80f, 0.12f, 0.10f ), new Color( 0.55f, 0.57f, 0.60f ),
				PanelShape.Full, GrimePattern.Splatter,
				position: new Vector3( 520f, 264f, 130f ), cellSize: 12f, secrets: new List<SurfaceSecret>
				{
					new()
					{
						Symbol = SecretSymbol.Crosshair, X = 0.5f, Y = 0.55f, Scale = 2.4f,
						DiscoveryId = "L15_marked",
						Monologue = "They painted a crosshair over the spot where I hang my coat. This wasn't a warning. It was a rehearsal.",
					},
				} ),
			// Shop floor — oil, solvent, and everything they tipped over.
			FlatGround( 640f, 420f, OilBlack, AlleyAsphalt, PanelShape.Full, GrimePattern.Organic,
				position: new Vector3( 0f, -20f, 1f ) ),
		},
		Decor = new List<DecorDef>
		{
			D( DecorKind.Warehouse, 0f, 480f, size: new Vector3( 760f, 380f, 240f ), color: new Color( 0.62f, 0.58f, 0.52f ) ),
			D( DecorKind.TiledWall, -520f, 280f, size: new Vector3( 340f, 24f, 220f ), color: new Color( 0.55f, 0.57f, 0.60f ) ),
			D( DecorKind.TiledWall, 520f, 280f, size: new Vector3( 340f, 24f, 220f ), color: new Color( 0.55f, 0.57f, 0.60f ) ),
			D( DecorKind.WreckedCar, -320f, -280f, yaw: -30f, size: new Vector3( 0.95f, 1f, 1f ) ),
			D( DecorKind.CrateStack, 360f, 60f, yaw: 45f ),
			D( DecorKind.BarrelCluster, -280f, 100f, yaw: 15f, color: SafetyOrange ),
			D( DecorKind.Dumpster, 660f, -60f, yaw: 100f, color: new Color( 0.28f, 0.62f, 0.34f ) ),
			D( DecorKind.Floodlight, -140f, -380f, yaw: 20f ),
			D( DecorKind.Floodlight, 240f, -400f, yaw: -30f ),
			D( DecorKind.LampPost, -720f, -460f ),
			D( DecorKind.LampPost, 720f, -460f ),
			D( DecorKind.Fence, 0f, -680f, size: new Vector3( 1500f, 1f, 1f ), color: new Color( 0.45f, 0.45f, 0.48f ) ),
		},
		Props = new List<PropDef>
		{
			// Overturned pressure rigs and a spilled tool chest.
			P( -120f, 140f, 1f, 90f, 50f, 40f, new Color( 0.86f, 0.20f, 0.14f ), new Angles( 0, 25, 75 ) ),
			P( 60f, 180f, 1f, 90f, 50f, 40f, new Color( 0.20f, 0.42f, 0.78f ), new Angles( 0, -50, 82 ) ),
			P( 180f, -160f, 1f, 110f, 50f, 34f, new Color( 0.72f, 0.16f, 0.14f ), new Angles( 0, 15, 100 ) ),
			P( 220f, -200f, 1f, 24f, 8f, 4f, new Color( 0.62f, 0.64f, 0.68f ) ),
			P( 150f, -220f, 1f, 24f, 8f, 4f, new Color( 0.62f, 0.64f, 0.68f ) ),
		},
		Enemies = new List<EnemySpawnDef>
		{
			E( EnemyKind.RivalWasher, -380f, 200f ),
			E( EnemyKind.RivalWasher, 420f, 160f ),
			E( EnemyKind.StickerBandit, 120f, 320f ),
			E( EnemyKind.StickerBandit, -160f, -240f ),
		},
	};
}
