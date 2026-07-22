namespace SkyEmpire;

/// <summary>
/// Renders one player's island tycoon and runs its orb simulation. Purely
/// local/cosmetic for other players' plots; for the local player it also owns
/// the walk-on buy pads and awards cash when orbs reach the furnace.
/// Everything is positioned in plot-local space (belt runs along +X, hub is +Y).
/// </summary>
public sealed class PlotVisual : Component
{
	public TycoonPlayer Player { get; set; }

	sealed class Orb
	{
		public GameObject Go;
		public float X;
		public double Value;
		public bool Golden;
	}

	sealed class BuyPad
	{
		public PurchaseDef Def;
		public GameObject Root;
		public ModelRenderer Disc;
		public TextRenderer Label;
		public Vector3 LocalPos;
	}

	GameObject _staticRoot;
	GameObject _whale;
	GameObject _auroraRing;
	string _builtCsv;
	string _builtName;
	int _builtRebirths = -1;

	readonly List<Orb> _orbs = new();
	readonly Dictionary<string, float> _spawnTimers = new();
	readonly List<BuyPad> _pads = new();
	readonly HashSet<string> _owned = new();

	TimeSince _lastCashSound = 10f;
	TimeSince _lastDeniedNudge = 10f;
	int _orbCounter;

	bool IsLocalPlot => Player.IsValid() && !Player.IsProxy;

	protected override void OnStart()
	{
		if ( !Player.IsValid() ) return;
		var index = Player.PlotIndex;
		WorldPosition = WorldBuilder.PlotCenter( index );
		WorldRotation = Rotation.FromYaw( WorldBuilder.PlotYaw( index ) );
	}

	protected override void OnDestroy()
	{
		foreach ( var orb in _orbs ) orb.Go?.Destroy();
		_orbs.Clear();
	}

	protected override void OnUpdate()
	{
		if ( !Player.IsValid() ) return;

		var csv = Player.PurchasedCsv ?? "";
		if ( _builtCsv != csv || _builtRebirths != Player.Rebirths || _builtName != Player.DisplayName )
			Rebuild( csv, Player.Rebirths );

		UpdateOrbs();
		UpdateAnimatedDecor();

		if ( IsLocalPlot )
			UpdateBuyPads();
	}

	// ================= Static geometry =================

	void Rebuild( string csv, int rebirths )
	{
		_builtCsv = csv;
		_builtRebirths = rebirths;
		_builtName = Player.DisplayName;

		_owned.Clear();
		foreach ( var id in csv.Split( ',', StringSplitOptions.RemoveEmptyEntries ) )
			_owned.Add( id );

		// Geometry rebuild wipes the belt — pay out in-flight orbs so nothing is lost.
		var progress = IsLocalPlot ? PlayerProgress.Local : null;
		foreach ( var orb in _orbs )
		{
			orb.Go?.Destroy();
			progress?.CollectOrb( orb.Value, orb.Golden );
		}
		_orbs.Clear();

		_staticRoot?.Destroy();
		_staticRoot = new GameObject( true, "PlotStatics" );
		_staticRoot.SetParent( GameObject );
		_staticRoot.LocalPosition = Vector3.Zero;
		_staticRoot.LocalRotation = Rotation.Identity;
		_whale = null;
		_auroraRing = null;

		var tier = RebirthCatalog.Tier( rebirths );

		BuildGroundDressing( tier, rebirths );
		BuildNameSign( tier );
		BuildBelt( tier );
		BuildFurnace( tier );
		BuildTower( tier );

		foreach ( var def in PurchaseCatalog.All )
		{
			if ( !_owned.Contains( def.Id ) ) continue;
			switch ( def.Kind )
			{
				case PurchaseKind.Dropper: BuildDropper( def, tier ); break;
				case PurchaseKind.Upgrader: BuildArch( def, tier ); break;
				case PurchaseKind.Decor: BuildDecor( def, tier ); break;
			}
		}

		if ( IsLocalPlot )
			RebuildBuyPads();
	}

	void BuildGroundDressing( RebirthTierDef tier, int rebirths )
	{
		// Rebirth re-skin: overlay the pad top with the tier's grass color.
		if ( rebirths > 0 )
			Kit.Sphere( _staticRoot, "TierGrass", new Vector3( 0, 0, -19f ), new Vector3( Balance.PlotRadius * 2f, Balance.PlotRadius * 2f, 46f ), tier.Grass );

		// Rim posts around the pad edge, tinted by tier.
		for ( int i = 0; i < 14; i++ )
		{
			var ang = i / 14f * MathF.Tau;
			var pos = new Vector3( MathF.Cos( ang ), MathF.Sin( ang ), 0 ) * (Balance.PlotRadius - 55f);
			Kit.Box( _staticRoot, "RimPost", pos.WithZ( 2f ), new Vector3( 12f, 12f, 40f ), tier.Accent.Darken( 0.25f ) );
		}
	}

	void BuildNameSign( RebirthTierDef tier )
	{
		var sign = new GameObject( true, "NameSign" );
		sign.SetParent( _staticRoot );
		sign.LocalPosition = new Vector3( 0, Balance.PlotRadius - 130f, 2f );

		Kit.Box( sign, "PostL", new Vector3( -85f, 0, 0 ), new Vector3( 10f, 10f, 120f ), tier.Cliff );
		Kit.Box( sign, "PostR", new Vector3( 85f, 0, 0 ), new Vector3( 10f, 10f, 120f ), tier.Cliff );
		Kit.Box( sign, "Panel", new Vector3( 0, 0, 74f ), new Vector3( 200f, 8f, 56f ), tier.Cliff.Darken( 0.15f ) );

		var name = Player.DisplayName ?? "Sky Baron";
		var badge = Player.Rebirths > 0 ? $"[{Player.Rebirths}] " : "";
		Kit.Label( sign, "Name", new Vector3( 0, -6f, 102f ), $"{badge}{name}'s Island", new Color( 1f, 0.95f, 0.75f ), 0.2f, 38 );
		Kit.Label( sign, "Tier", new Vector3( 0, -6f, 72f ), tier.Name, tier.Accent, 0.14f, 30 );
	}

	void BuildBelt( RebirthTierDef tier )
	{
		var length = Balance.BeltEndX - Balance.BeltStartX;
		var midX = (Balance.BeltStartX + Balance.BeltEndX) * 0.5f;
		var frame = new Color( 0.30f, 0.32f, 0.36f );

		// Frame + running surface (top at BeltTopZ).
		Kit.Box( _staticRoot, "BeltFrame", new Vector3( midX, Balance.BeltY, 12f ), new Vector3( length, 84f, Balance.BeltTopZ - 14f ), frame );
		Kit.Box( _staticRoot, "BeltTop", new Vector3( midX, Balance.BeltY, Balance.BeltTopZ - 2f ), new Vector3( length, 70f, 4f ), new Color( 0.16f, 0.17f, 0.2f ) );

		// Legs.
		for ( float x = Balance.BeltStartX + 40f; x < Balance.BeltEndX; x += 150f )
		{
			Kit.Box( _staticRoot, "BeltLeg", new Vector3( x, Balance.BeltY - 34f, 0f ), new Vector3( 14f, 10f, 14f ), frame.Darken( 0.15f ) );
			Kit.Box( _staticRoot, "BeltLeg", new Vector3( x, Balance.BeltY + 34f, 0f ), new Vector3( 14f, 10f, 14f ), frame.Darken( 0.15f ) );
		}

		// Direction markers on the near side, accent tinted.
		for ( float x = Balance.BeltStartX + 70f; x < Balance.BeltEndX - 40f; x += 210f )
			Kit.BoxCentered( _staticRoot, "Chevron", new Vector3( x, Balance.BeltY + 43f, 26f ), new Vector3( 34f, 3f, 10f ), tier.Accent );
	}

	void BuildFurnace( RebirthTierDef tier )
	{
		var f = new GameObject( true, "Furnace" );
		f.SetParent( _staticRoot );
		f.LocalPosition = new Vector3( Balance.FurnaceX + 60f, Balance.BeltY, 0f );

		var body = new Color( 0.36f, 0.30f, 0.30f );
		Kit.Box( f, "Body", Vector3.Zero, new Vector3( 150f, 150f, 170f ), body, collider: true );
		Kit.Box( f, "Rim", new Vector3( 0, 0, 170f ), new Vector3( 160f, 160f, 16f ), body.Darken( 0.15f ) );
		Kit.Box( f, "Chimney", new Vector3( 30f, 30f, 186f ), new Vector3( 42f, 42f, 90f ), body.Darken( 0.25f ) );

		// Glowing mouth facing the belt (−X side), flame color follows rebirth tier.
		Kit.BoxCentered( f, "Mouth", new Vector3( -76f, 0, Balance.BeltTopZ ), new Vector3( 6f, 84f, 64f ), tier.Flame );
		Kit.Sphere( f, "Glow", new Vector3( -60f, 0, Balance.BeltTopZ ), new Vector3( 50f, 60f, 50f ), tier.Flame.WithAlpha( 0.55f ) );
	}

	void BuildTower( RebirthTierDef tier )
	{
		// The sky tower rises behind the belt; one story per unlocked floor.
		var floors = 1;
		if ( _owned.Contains( "f2" ) ) floors = 2;
		if ( _owned.Contains( "f3" ) ) floors = 3;
		if ( _owned.Contains( "f4" ) ) floors = 4;

		var t = new GameObject( true, "Tower" );
		t.SetParent( _staticRoot );
		t.LocalPosition = new Vector3( -80f, -400f, 0f );

		var z = 0f;
		for ( int i = 0; i < floors; i++ )
		{
			var w = 360f - i * 50f;
			var d = 260f - i * 30f;
			var h = 140f;
			var shade = tier.Cliff.Lighten( 0.12f * i );
			Kit.Box( t, $"Story{i}", new Vector3( 0, 0, z ), new Vector3( w, d, h ), shade, collider: i == 0 );

			// Window band, accent glow.
			Kit.Box( t, $"Windows{i}", new Vector3( 0, d * 0.5f - 2f, z + 48f ), new Vector3( w - 60f, 6f, 34f ), tier.Accent.WithAlpha( 0.9f ) );
			z += h + 1f; // +1 stacking nudge, per depth rules
		}

		// Roof beacon; antenna once the Cosmic Crown floor exists.
		Kit.Sphere( t, "Beacon", new Vector3( 0, 0, z + 26f ), new Vector3( 52f, 52f, 52f ), tier.Accent );
		if ( floors >= 4 )
		{
			Kit.Box( t, "Antenna", new Vector3( 60f, 0, z ), new Vector3( 8f, 8f, 180f ), tier.Cliff.Darken( 0.2f ) );
			Kit.Sphere( t, "AntennaTip", new Vector3( 60f, 0, z + 192f ), new Vector3( 20f, 20f, 20f ), tier.Flame );
		}
	}

	void BuildDropper( PurchaseDef def, RebirthTierDef tier )
	{
		var d = new GameObject( true, $"Dropper_{def.Id}" );
		d.SetParent( _staticRoot );
		d.LocalPosition = new Vector3( def.BeltX, Balance.BeltY, 0f );

		var floorShade = 0.12f * (def.Floor - 1);
		var body = new Color( 0.42f, 0.46f, 0.52f ).Lighten( floorShade );

		// Gantry over the belt.
		Kit.Box( d, "LegA", new Vector3( 0, -62f, 0 ), new Vector3( 14f, 14f, 150f ), body.Darken( 0.2f ) );
		Kit.Box( d, "LegB", new Vector3( 0, 62f, 0 ), new Vector3( 14f, 14f, 150f ), body.Darken( 0.2f ) );
		Kit.Box( d, "Cross", new Vector3( 0, 0, 150f ), new Vector3( 20f, 140f, 14f ), body.Darken( 0.1f ) );

		// Hopper + spout, accent-striped so higher droppers look meaner.
		Kit.Box( d, "Hopper", new Vector3( 0, 0, 165f ), new Vector3( 66f, 66f, 60f ), body );
		Kit.Box( d, "Stripe", new Vector3( 0, 0, 226f ), new Vector3( 68f, 68f, 10f ), tier.Accent );
		Kit.BoxCentered( d, "Spout", new Vector3( 0, 0, 152f ), new Vector3( 26f, 26f, 30f ), body.Darken( 0.3f ) );

		Kit.Label( d, "Tag", new Vector3( 0, 0, 262f ), def.Name, new Color( 0.92f, 0.95f, 1f ), 0.11f, 26 );
	}

	void BuildArch( PurchaseDef def, RebirthTierDef tier )
	{
		var a = new GameObject( true, $"Arch_{def.Id}" );
		a.SetParent( _staticRoot );
		a.LocalPosition = new Vector3( def.BeltX, Balance.BeltY, 0f );

		var stone = new Color( 0.5f, 0.44f, 0.56f );
		Kit.Box( a, "PillarA", new Vector3( 0, -58f, 0 ), new Vector3( 22f, 22f, 108f ), stone );
		Kit.Box( a, "PillarB", new Vector3( 0, 58f, 0 ), new Vector3( 22f, 22f, 108f ), stone );
		Kit.Box( a, "Top", new Vector3( 0, 0, 108f ), new Vector3( 26f, 138f, 22f ), stone.Darken( 0.1f ) );

		// Glow plane the orbs pass through.
		Kit.BoxCentered( a, "Field", new Vector3( 0, 0, 72f ), new Vector3( 4f, 100f, 58f ), tier.Accent.WithAlpha( 0.45f ) );
		Kit.Label( a, "Mult", new Vector3( 0, 0, 152f ), $"×{def.Value:0.##}", tier.Accent, 0.16f, 34 );
	}

	void BuildDecor( PurchaseDef def, RebirthTierDef tier )
	{
		var rng = new Random( def.Id.GetHashCode() );
		switch ( def.Id )
		{
			case "dec1": // Sky Garden — flower bed on the west edge
				for ( int i = 0; i < 8; i++ )
					Kit.Flower( _staticRoot, new Vector3( -480f + i % 4 * 45f, 180f + i / 4 * 50f, 2f ), new ColorHsv( rng.Next( 360 ), 0.6f, 0.95f ), rng );
				Kit.Rock( _staticRoot, new Vector3( -520f, 260f, 2f ), new Color( 0.6f, 0.62f, 0.6f ), 0.5f, rng );
				break;

			case "dec2": // Crystal Fountain
			{
				var fnt = new GameObject( true, "CrystalFountain" );
				fnt.SetParent( _staticRoot );
				fnt.LocalPosition = new Vector3( 420f, 220f, 2f );
				Kit.Box( fnt, "Basin", Vector3.Zero, new Vector3( 130f, 130f, 26f ), new Color( 0.7f, 0.72f, 0.75f ), collider: true );
				Kit.Box( fnt, "Water", new Vector3( 0, 0, 27f ), new Vector3( 110f, 110f, 6f ), new Color( 0.45f, 0.8f, 0.95f, 0.9f ) );
				Kit.BoxCentered( fnt, "Spire", new Vector3( 0, 0, 80f ), new Vector3( 22f, 22f, 95f ), tier.Accent, Rotation.From( 6f, 30f, 4f ) );
				break;
			}

			case "dec3": // Star Beacon
			{
				var b = new GameObject( true, "StarBeacon" );
				b.SetParent( _staticRoot );
				b.LocalPosition = new Vector3( -520f, -160f, 2f );
				Kit.Box( b, "Base", Vector3.Zero, new Vector3( 70f, 70f, 24f ), new Color( 0.55f, 0.5f, 0.45f ), collider: true );
				Kit.Box( b, "Mast", new Vector3( 0, 0, 24f ), new Vector3( 14f, 14f, 190f ), new Color( 0.4f, 0.38f, 0.36f ) );
				Kit.Sphere( b, "Star", new Vector3( 0, 0, 240f ), new Vector3( 46f, 46f, 46f ), new Color( 1f, 0.95f, 0.55f ) );
				break;
			}

			case "dec4": // Cloud Whale — animated in UpdateAnimatedDecor
			{
				_whale = new GameObject( true, "CloudWhale" );
				_whale.SetParent( _staticRoot );
				_whale.LocalPosition = new Vector3( 0, 0, 420f );
				var white = new Color( 0.93f, 0.96f, 1f );
				Kit.Sphere( _whale, "Body", Vector3.Zero, new Vector3( 220f, 110f, 100f ), white );
				Kit.Sphere( _whale, "Head", new Vector3( 120f, 0, -10f ), new Vector3( 110f, 90f, 80f ), white );
				Kit.BoxCentered( _whale, "Tail", new Vector3( -130f, 0, 20f ), new Vector3( 70f, 90f, 14f ), white.Darken( 0.06f ), Rotation.From( 0, 0, 18f ) );
				Kit.Sphere( _whale, "Eye", new Vector3( 165f, -38f, 5f ), new Vector3( 14f, 14f, 14f ), new Color( 0.15f, 0.15f, 0.2f ) );
				break;
			}

			case "dec5": // Aurora Ring — rotating halo above the tower
			{
				_auroraRing = new GameObject( true, "AuroraRing" );
				_auroraRing.SetParent( _staticRoot );
				_auroraRing.LocalPosition = new Vector3( -80f, -400f, 760f );
				for ( int i = 0; i < 10; i++ )
				{
					var ang = i / 10f * MathF.Tau;
					var pos = new Vector3( MathF.Cos( ang ) * 240f, MathF.Sin( ang ) * 240f, 0 );
					Kit.BoxCentered( _auroraRing, $"Seg{i}", pos, new Vector3( 90f, 26f, 12f ), tier.Accent.WithAlpha( 0.8f ), Rotation.FromYaw( ang.RadianToDegree() + 90f ) );
				}
				break;
			}

			case "dec6": // Comet Fountain — angled glowing streaks
			{
				var c = new GameObject( true, "CometFountain" );
				c.SetParent( _staticRoot );
				c.LocalPosition = new Vector3( 480f, -300f, 2f );
				Kit.Box( c, "Base", Vector3.Zero, new Vector3( 90f, 90f, 30f ), new Color( 0.45f, 0.42f, 0.5f ), collider: true );
				for ( int i = 0; i < 4; i++ )
					Kit.BoxCentered( c, $"Streak{i}", new Vector3( 0, 0, 90f + i * 34f ), new Vector3( 12f, 12f, 110f ), tier.Flame.WithAlpha( 0.85f ), Rotation.From( 22f + i * 8f, i * 90f + 45f, 0 ) );
				break;
			}
		}
	}

	// ================= Orb simulation =================

	void UpdateOrbs()
	{
		if ( _staticRoot is null || !_staticRoot.IsValid() ) return;

		var motors = PurchaseCatalog.BeltMotorCount( _owned );
		var speed = Balance.BeltBaseSpeed * (1f + motors * Balance.BeltSpeedPerMotor);
		var progress = IsLocalPlot ? PlayerProgress.Local : null;
		var rateMult = progress?.FrenzyActive == true ? Balance.FrenzyRateMult : 1f;
		var islandMult = PurchaseCatalog.IslandMult( _owned );
		var goldenChance = PurchaseCatalog.GoldenChance( _owned );

		// Spawn from each owned dropper.
		foreach ( var def in PurchaseCatalog.All )
		{
			if ( def.Kind != PurchaseKind.Dropper || !_owned.Contains( def.Id ) ) continue;

			var t = _spawnTimers.GetValueOrDefault( def.Id, def.Interval );
			t -= Time.Delta * rateMult;
			if ( t <= 0f )
			{
				t += def.Interval;
				SpawnOrb( def, islandMult, goldenChance );
			}
			_spawnTimers[def.Id] = t;
		}

		// Move orbs, apply arch fields, collect at the furnace.
		for ( int i = _orbs.Count - 1; i >= 0; i-- )
		{
			var orb = _orbs[i];
			var prevX = orb.X;
			orb.X += speed * Time.Delta;

			foreach ( var arch in PurchaseCatalog.All )
			{
				if ( arch.Kind != PurchaseKind.Upgrader || !_owned.Contains( arch.Id ) ) continue;
				if ( prevX < arch.BeltX && orb.X >= arch.BeltX )
					orb.Value *= arch.Value;
			}

			if ( orb.Go.IsValid() )
				orb.Go.LocalPosition = new Vector3( orb.X, Balance.BeltY, Balance.BeltTopZ + 13f + MathF.Sin( Time.Now * 9f + i ) * 2f );

			if ( orb.X >= Balance.BeltEndX )
			{
				CollectOrb( orb, progress );
				_orbs.RemoveAt( i );
			}
		}

		// Belt overflow safety: auto-collect the oldest orbs silently.
		while ( _orbs.Count > Balance.MaxOrbsPerPlot )
		{
			var oldest = _orbs[0];
			_orbs.RemoveAt( 0 );
			oldest.Go?.Destroy();
			progress?.CollectOrb( oldest.Value, oldest.Golden );
		}
	}

	void SpawnOrb( PurchaseDef def, double islandMult, double goldenChance )
	{
		var golden = Game.Random.Float() < (float)goldenChance;
		var value = def.Value * islandMult * (golden ? Balance.GoldenValueMult : 1.0);

		var go = new GameObject( true, "Orb" );
		go.SetParent( _staticRoot );
		go.LocalPosition = new Vector3( def.BeltX, Balance.BeltY, Balance.BeltTopZ + 13f );

		var size = golden ? 34f : 24f;
		var color = golden ? new Color( 1f, 0.83f, 0.25f ) : new Color( 0.65f, 0.85f, 1f );
		var renderer = go.Components.Create<ModelRenderer>();
		renderer.Model = Model.Load( Kit.SphereModel );
		renderer.MaterialOverride = Material.Load( Kit.DefaultMaterial );
		renderer.Tint = color;
		go.LocalScale = new Vector3( size / 50f );

		_orbs.Add( new Orb { Go = go, X = def.BeltX, Value = value, Golden = golden } );

		if ( golden && IsLocalPlot )
		{
			Effects.FloatText( go.WorldPosition + Vector3.Up * 40f, "GOLDEN ORB!", new Color( 1f, 0.85f, 0.3f ), 0.2f );
			Sfx.Play( "golden", go.WorldPosition );
		}
	}

	void CollectOrb( Orb orb, PlayerProgress progress )
	{
		var pos = orb.Go.IsValid() ? orb.Go.WorldPosition : WorldPosition;
		orb.Go?.Destroy();

		if ( progress is null )
			return; // Someone else's island — purely cosmetic.

		progress.CollectOrb( orb.Value, orb.Golden );

		_orbCounter++;
		var paid = orb.Value * progress.RebirthMult * progress.BoostMult;
		if ( orb.Golden || _orbCounter % 4 == 0 )
			Effects.FloatText( pos + Vector3.Up * 40f, $"+{Balance.Fmt( paid )}", orb.Golden ? new Color( 1f, 0.85f, 0.3f ) : new Color( 0.8f, 1f, 0.6f ), orb.Golden ? 0.2f : 0.12f );

		if ( _lastCashSound > 0.18f )
		{
			_lastCashSound = 0f;
			Sfx.Play( orb.Golden ? "golden" : "cash", pos );
		}
	}

	// ================= Buy pads (local plot only) =================

	void RebuildBuyPads()
	{
		foreach ( var pad in _pads ) pad.Root?.Destroy();
		_pads.Clear();

		var yardSlot = 0;
		foreach ( var def in PurchaseCatalog.Available( _owned ) )
		{
			Vector3 pos;
			if ( def.Kind is PurchaseKind.Dropper or PurchaseKind.Upgrader )
			{
				// Pad sits right where the machine will be built, in front of the belt.
				pos = new Vector3( def.BeltX, Balance.BeltY + 150f, 2f );
			}
			else
			{
				pos = new Vector3( -300f + yardSlot % 4 * 200f, 240f + yardSlot / 4 * 180f, 2f );
				yardSlot++;
			}

			var root = new GameObject( true, $"BuyPad_{def.Id}" );
			root.SetParent( _staticRoot );
			root.LocalPosition = pos;

			var disc = Kit.Sphere( root, "Disc", new Vector3( 0, 0, 7f ), new Vector3( 110f, 110f, 16f ), Color.Gray );
			var labelGo = Kit.Label( root, "Label", new Vector3( 0, 0, 92f ), "", Color.White, 0.13f, 30 );

			_pads.Add( new BuyPad
			{
				Def = def,
				Root = root,
				Disc = disc.Components.Get<ModelRenderer>(),
				Label = labelGo.Components.Get<TextRenderer>(),
				LocalPos = pos,
			} );
		}
	}

	void UpdateBuyPads()
	{
		var progress = PlayerProgress.Local;
		var player = TycoonPlayer.Local;
		if ( progress is null || !player.IsValid() ) return;

		foreach ( var pad in _pads )
		{
			if ( !pad.Root.IsValid() ) continue;

			var affordable = progress.Data.Cash >= pad.Def.Cost;
			var free = pad.Def.Cost <= 0;

			if ( pad.Label.IsValid() )
			{
				var costLine = free ? "FREE — STEP HERE!" : $"$ {Balance.Fmt( pad.Def.Cost )}";
				pad.Label.Text = $"{pad.Def.Name}\n{costLine}";
				pad.Label.Color = affordable ? new Color( 0.6f, 1f, 0.6f ) : new Color( 1f, 0.85f, 0.7f );
			}

			if ( pad.Disc.IsValid() )
			{
				var pulse = 0.5f + MathF.Sin( Time.Now * 5f ) * 0.15f;
				pad.Disc.Tint = affordable
					? new Color( 0.25f, 0.9f, 0.4f ) * (0.85f + pulse * 0.3f)
					: new Color( 0.45f, 0.45f, 0.5f );
			}

			// Walk-on purchase.
			var dist = Vector3.DistanceBetween( player.WorldPosition, pad.Root.WorldPosition );
			if ( dist > 75f ) continue;

			if ( affordable )
			{
				var pos = pad.Root.WorldPosition;
				if ( progress.TryPurchase( pad.Def ) )
					Effects.FloatText( pos + Vector3.Up * 60f, $"{pad.Def.Name}!", new Color( 0.7f, 1f, 0.75f ), 0.18f );
			}
			else if ( _lastDeniedNudge > 1.2f )
			{
				_lastDeniedNudge = 0f;
				Effects.FloatText( pad.Root.WorldPosition + Vector3.Up * 60f,
					$"Need {Balance.Fmt( pad.Def.Cost - progress.Data.Cash )} more", new Color( 1f, 0.6f, 0.5f ), 0.14f );
				Sfx.Play( "deny" );
			}
		}
	}

	// ================= Animated decor =================

	void UpdateAnimatedDecor()
	{
		if ( _whale.IsValid() )
		{
			var t = Time.Now * 0.14f + Player.PlotIndex;
			var pos = new Vector3( MathF.Cos( t ) * 780f, MathF.Sin( t ) * 780f, 430f + MathF.Sin( Time.Now * 0.5f ) * 30f );
			_whale.LocalPosition = pos;
			_whale.LocalRotation = Rotation.FromYaw( t.RadianToDegree() + 90f );
		}

		if ( _auroraRing.IsValid() )
			_auroraRing.LocalRotation = Rotation.FromYaw( Time.Now * 18f );
	}
}
