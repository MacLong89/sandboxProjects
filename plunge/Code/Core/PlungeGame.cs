using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Plunge;

public sealed class PlungeGame : Component
{
	public static PlungeGame Instance { get; private set; }

	public PlungeSaveData Save { get; private set; }
	public GameScreen Screen { get; private set; } = GameScreen.Hub;
	public HubTab Tab { get; set; } = HubTab.Diver;
	public string SelectedGearSlot { get; set; } = "Helmet";
	public string SelectedGearId { get; set; } = "helmet_2";
	public string SelectedSubId { get; set; } = "seeker";
	public DiveRecord LastResult { get; private set; }

	public float Health { get; private set; }
	public float MaxHealth { get; private set; }
	public float Oxygen { get; private set; }
	public float MaxOxygen { get; private set; }
	public float Energy { get; private set; }
	public float Depth { get; private set; }
	public float MaxDepth { get; private set; }
	public float DiveTime { get; private set; }
	public float DepthRating { get; private set; }
	public int HotbarSlot { get; private set; } = 1;
	public bool LightOn { get; private set; } = true;
	public string ObjectiveText { get; private set; } = "Find the Research Drone";
	public int ObjectiveProgress { get; private set; }
	public int ObjectiveTarget { get; private set; } = 1;
	public string LatestNotice { get; private set; } = "";
	public string NoticeClass { get; private set; } = "info";
	public float NoticeAge { get; private set; } = 99;
	public TutorialTipDef ActiveTutorialTip { get; private set; }
	public string TipToast { get; private set; } = "";
	public bool TutorialTipsHidden => Save?.HideTutorialTips ?? false;
	public IReadOnlyList<HaulItem> Haul => CurrentHaul;
	public int CargoCapacity { get; private set; }
	public bool UsingSub { get; private set; }

	private CameraComponent Camera;
	private GameObject Player;
	private PixelActor PlayerSprite;
	private GameObject Backdrop;
	private SpriteRenderer BackdropSprite;
	private OceanTerrain Terrain;
	private float SurfacePromptAge = 99;
	private readonly List<DiveEntity> Entities = new();
	private readonly List<HaulItem> CurrentHaul = new();
	private readonly List<string> Discoveries = new();
	private readonly Random Rng = new();
	private DiverStats DiveStats;
	private float AttackCooldown;
	private float Invulnerable;
	private float PlayerFacing = 1;
	private float SpawnClock;
	private float LastWarn;
	private bool Settled;
	private TimeUntil _tipToastHide;

	protected override void OnStart()
	{
		Instance = this;
		Save = PlungeSave.Load();
		Save.TutorialTipsShown ??= new List<string>();
		Camera = Components.Get<CameraComponent>();
		SelectedGearId = Catalog.GearFor( SelectedGearSlot ).Skip( 1 ).First().Id;
		RefreshTutorialTips();
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}

	protected override void OnUpdate()
	{
		NoticeAge += Time.Delta;

		if ( _tipToastHide )
			TipToast = "";

		if ( Input.Keyboard.Pressed( "h" ) || Input.Keyboard.Pressed( "H" ) )
			ToggleTutorialTipsHidden();

		RefreshTutorialTips();

		if ( Screen != GameScreen.Dive )
			return;

		UpdateDive();
	}

	public void RefreshTutorialTips()
	{
		if ( Save.HideTutorialTips || Screen != GameScreen.Hub )
		{
			ActiveTutorialTip = null;
			return;
		}

		if ( ActiveTutorialTip is not null )
			return;

		ActiveTutorialTip = TutorialTips.PickNext( Save );
	}

	public void DismissTutorialTip( bool hideAll = false )
	{
		if ( ActiveTutorialTip is not null )
		{
			TutorialTips.MarkShown( Save, ActiveTutorialTip.Id );
			ActiveTutorialTip = null;
		}

		if ( hideAll )
		{
			Save.HideTutorialTips = true;
			TipToast = "Tips hidden — press H to show again";
			_tipToastHide = 3f;
		}

		PlungeSave.Write( Save );
	}

	public void ToggleTutorialTipsHidden()
	{
		Save.HideTutorialTips = !Save.HideTutorialTips;
		if ( Save.HideTutorialTips )
		{
			ActiveTutorialTip = null;
			TipToast = "Tips hidden — press H to show again";
		}
		else
		{
			TipToast = "Tips enabled";
		}

		_tipToastHide = 3f;
		PlungeSave.Write( Save );
		RefreshTutorialTips();
	}

	public DiverStats GetDiverStats()
	{
		var stats = new DiverStats();
		foreach ( var slot in Catalog.Slots )
		{
			if ( !Save.Equipped.TryGetValue( slot, out var id ) )
				continue;
			var gear = Catalog.GearById( id );
			if ( gear is null )
				continue;
			stats.Health += gear.Health;
			stats.Oxygen += gear.Oxygen;
			stats.Speed += gear.Speed;
			stats.Carry += gear.Carry;
			stats.Resistance += gear.Resistance;
			stats.Light += gear.Light;
			stats.Damage += gear.Damage;
		}
		return stats;
	}

	public void StartDive( bool useSub )
	{
		ClearWorld();
		UsingSub = useSub && !string.IsNullOrEmpty( Save.EquippedSub );
		DiveStats = GetDiverStats();

		if ( UsingSub )
		{
			var sub = Catalog.SubById( Save.EquippedSub );
			MaxHealth = sub.Hull + (Save.SubLevel - 1) * 15 + UpgradeLevel( "Hull" ) * 15;
			MaxOxygen = sub.Oxygen + UpgradeLevel( "O2 Tank" ) * 20;
			DepthRating = sub.Depth + UpgradeLevel( "Hull" ) * 12;
			CargoCapacity = (int)sub.Cargo + UpgradeLevel( "Storage" ) * 2;
		}
		else
		{
			MaxHealth = DiveStats.Health;
			MaxOxygen = DiveStats.Oxygen;
			DepthRating = DiveStats.DepthRating;
			CargoCapacity = (int)DiveStats.Carry;
		}

		Health = MaxHealth;
		Oxygen = MaxOxygen;
		Energy = 100;
		Depth = 0;
		MaxDepth = 0;
		DiveTime = 0;
		LightOn = true;
		HotbarSlot = 1;
		ObjectiveText = "Find the Research Drone";
		ObjectiveProgress = 0;
		ObjectiveTarget = 1;
		CurrentHaul.Clear();
		Discoveries.Clear();
		Settled = false;
		Screen = GameScreen.Dive;

		CreateBackdrop();
		Terrain = new OceanTerrain( Scene, Save.TotalDives + 17 );
		Terrain.Build();
		CreatePlayer();
		SpawnInitialWorld();
		Notice( "Swim down the slopes. Reach the surface to end your dive.", "discover" );
	}

	public void ReturnToHub()
	{
		if ( Screen == GameScreen.Dive )
			SettleDive( false );
		ClearWorld();
		Screen = GameScreen.Hub;
		RefreshTutorialTips();
	}

	public void ContinueFromResults()
	{
		ClearWorld();
		Screen = GameScreen.Hub;
		Tab = HubTab.Diver;
		RefreshTutorialTips();
	}

	public void BuyOrEquipGear( string id )
	{
		var gear = Catalog.GearById( id );
		if ( gear is null || Save.Level < gear.RequiredLevel )
			return;

		if ( !Save.OwnedGear.Contains( id ) )
		{
			if ( Save.Gold < gear.Cost )
			{
				Notice( "Not enough credits.", "warning" );
				return;
			}
			Save.Gold -= gear.Cost;
			Save.OwnedGear.Add( id );
			Save.HasShopPurchase = true;
		}

		Save.Equipped[gear.Slot] = id;
		SelectedGearSlot = gear.Slot;
		SelectedGearId = id;
		PlungeSave.Write( Save );
		RefreshTutorialTips();
	}

	public void SelectGear( string slot, string id )
	{
		SelectedGearSlot = slot;
		SelectedGearId = id;
	}

	public void BuyOrEquipSub( string id )
	{
		var sub = Catalog.SubById( id );
		if ( sub is null || Save.Level < sub.RequiredLevel )
			return;
		if ( !Save.OwnedSubs.Contains( id ) )
		{
			if ( Save.Gold < sub.Cost )
			{
				Notice( "Not enough credits.", "warning" );
				return;
			}
			Save.Gold -= sub.Cost;
			Save.OwnedSubs.Add( id );
			Save.HasShopPurchase = true;
		}
		Save.EquippedSub = id;
		SelectedSubId = id;
		PlungeSave.Write( Save );
		RefreshTutorialTips();
	}

	public void UpgradeSub()
	{
		var cost = 500 + Save.SubLevel * Save.SubLevel * 250;
		if ( Save.Gold < cost || string.IsNullOrEmpty( Save.EquippedSub ) )
			return;
		Save.Gold -= cost;
		Save.SubLevel++;
		Save.HasShopPurchase = true;
		PlungeSave.Write( Save );
		RefreshTutorialTips();
	}

	public void UpgradeSubPart( string part )
	{
		if ( string.IsNullOrEmpty( Save.EquippedSub ) )
			return;
		var level = UpgradeLevel( part );
		var cost = 160 + level * level * 90;
		if ( Save.Gold < cost || level >= 10 )
			return;
		Save.Gold -= cost;
		Save.SubUpgrades[part] = level + 1;
		Save.HasShopPurchase = true;
		PlungeSave.Write( Save );
		RefreshTutorialTips();
	}

	public int UpgradeLevel( string part ) =>
		Save.SubUpgrades.TryGetValue( part, out var level ) ? level : 0;

	public string TimeText
	{
		get
		{
			var hour24 = (Save.MinuteOfDay / 60) % 24;
			var minute = Save.MinuteOfDay % 60;
			var suffix = hour24 >= 12 ? "PM" : "AM";
			var hour = ((hour24 + 11) % 12) + 1;
			return $"{hour:00}:{minute:00} {suffix}";
		}
	}

	private void CreatePlayer()
	{
		Player = Scene.CreateObject();
		Player.Name = UsingSub ? "Player Submarine" : "Player Diver";
		Player.WorldPosition = new Vector3( 0, 0, OceanTerrain.SurfaceZ - 40f );
		if ( Terrain is not null )
			Player.WorldPosition = Terrain.Resolve( Player.WorldPosition, 16f );
		PlayerSprite = Player.AddComponent<PixelActor>();
		PlayerSprite.Animation = UsingSub ? "sub" : "diver";
		PlayerSprite.Size = UsingSub ? new Vector2( 128, 74 ) : new Vector2( 72, 72 );
		PlayerSprite.FramesPerSecond = UsingSub ? 8 : 6;
	}

	private void CreateBackdrop()
	{
		Backdrop = Scene.CreateObject();
		Backdrop.Name = "Ocean Backdrop";
		Backdrop.WorldPosition = new Vector3( 0, 80, 120 );
		BackdropSprite = Backdrop.AddComponent<SpriteRenderer>();
		PixelActor.ConfigureRenderer( BackdropSprite );
		BackdropSprite.Size = new Vector2( 1600, 900 );
		SetBackdrop( "shallows" );
		ApplyOceanClear( "shallows" );
	}

	private void SetBackdrop( string zone )
	{
		if ( BackdropSprite is null )
			return;

		var sprite = PixelActor.LoadSprite( $"backgrounds/{zone}.png" );
		if ( sprite is not null )
			BackdropSprite.Sprite = sprite;

		ApplyOceanClear( zone );
	}

	private void ApplyOceanClear( string zone )
	{
		if ( Camera is null )
			return;

		Camera.BackgroundColor = zone switch
		{
			"reef" => new Color( 0.05f, 0.22f, 0.35f ),
			"cavern" => new Color( 0.03f, 0.12f, 0.24f ),
			"abyss" => new Color( 0.02f, 0.05f, 0.10f ),
			_ => new Color( 0.08f, 0.35f, 0.48f )
		};
		Camera.ZNear = 1;
		Camera.Orthographic = true;
		Camera.OrthographicHeight = 540;
	}

	private void SpawnInitialWorld()
	{
		// Starter fish near the descent
		for ( var i = 0; i < 8; i++ )
		{
			var fish = CreateEntity( EntityKind.Fish, "fish_common", new Vector2( 48, 28 ), 4 );
			fish.Loot = Catalog.Fish[0];
			fish.Radius = 18;
			fish.Speed = 28;
			fish.Velocity = new Vector3( i % 2 == 0 ? -36 : 36, 0, 0 );
			var p = Terrain?.RandomOpenPoint( 15, 70 ) ?? new Vector3( -80 + i * 40f, 0, OceanTerrain.SurfaceZ - 80f );
			fish.Object.WorldPosition = p;
		}

		// Chests and artifacts on real ledges
		if ( Terrain is not null )
		{
			foreach ( var ledge in Terrain.LedgePoints.Take( 10 ) )
			{
				if ( !Terrain.IsOpen( ledge + new Vector3( 0, 0, 20f ), 16f ) )
					continue;
				var kind = Rng.NextDouble() < 0.45 ? EntityKind.Chest : EntityKind.Artifact;
				var sprite = kind == EntityKind.Chest ? "chest" : (Rng.Next( 2 ) == 0 ? "crystal" : "idol");
				var loot = CreateEntity( kind, sprite, new Vector2( 48, 48 ), 1 );
				loot.Radius = 24;
				loot.Object.WorldPosition = ledge + new Vector3( 0, 0, 22f );
				if ( kind == EntityKind.Artifact )
					loot.Loot = Catalog.Artifacts[Rng.Next( 2 )];
			}

			// Research drone tucked in the first cave
			var cave = Terrain.CavePoints.FirstOrDefault();
			var drone = CreateEntity( EntityKind.Drone, "drone", new Vector2( 56, 56 ), 1 );
			drone.Object.WorldPosition = cave == default
				? Terrain.RandomOpenPoint( 160, 220 )
				: cave;
			drone.Loot = Catalog.Artifacts.First( x => x.Id == "research_drone" );
			drone.Radius = 30;
		}

		for ( var i = 0; i < 20; i++ ) SpawnFish( Rng.NextDouble() < 0.18 );
		for ( var i = 0; i < 7; i++ ) SpawnEnemy( i % 3 == 0 );
		for ( var i = 0; i < 6; i++ ) SpawnTreasure( i );
	}

	private DiveEntity CreateEntity( EntityKind kind, string animation, Vector2 size, int frames )
	{
		var go = Scene.CreateObject();
		go.Name = kind.ToString();
		var sprite = go.AddComponent<PixelActor>();
		sprite.Animation = animation;
		sprite.Size = size;
		sprite.FrameCount = frames;
		var entity = new DiveEntity { Object = go, Sprite = sprite, Kind = kind };
		Entities.Add( entity );
		return entity;
	}

	private void SpawnFish( bool rare )
	{
		var depth = (float)Rng.NextDouble() * 340;
		var choices = Catalog.Fish.Where( x => x.MinDepth <= depth ).ToArray();
		var loot = rare ? choices.Last() : choices[Rng.Next( choices.Length )];
		var anim = loot.Sprite;
		var entity = CreateEntity( rare ? EntityKind.RareFish : EntityKind.Fish, anim, new Vector2( 44, 26 ), 4 );
		entity.Loot = loot;
		entity.Radius = 18;
		entity.Speed = 30 + (float)Rng.NextDouble() * 30;
		entity.Velocity = new Vector3( Rng.Next( 0, 2 ) == 0 ? -entity.Speed : entity.Speed, 0, 0 );
		entity.Object.WorldPosition = RandomWorldPosition( depth );
	}

	private void SpawnEnemy( bool shark )
	{
		var entity = CreateEntity(
			shark ? EntityKind.Shark : EntityKind.Jelly,
			shark ? "shark" : "jelly",
			shark ? new Vector2( 84, 38 ) : new Vector2( 38, 58 ),
			4
		);
		entity.MaxHealth = entity.Health = shark ? 65 : 24;
		entity.Damage = shark ? 18 : 8;
		entity.Speed = shark ? 72 : 25;
		entity.Radius = shark ? 35 : 24;
		entity.Object.WorldPosition = RandomWorldPosition( shark ? 150 + (float)Rng.NextDouble() * 220 : 50 + (float)Rng.NextDouble() * 220 );
		entity.Velocity = new Vector3( entity.Speed, 0, 0 );
	}

	private void SpawnTreasure( int index )
	{
		var kind = (index % 4) switch
		{
			0 => EntityKind.Chest,
			1 => EntityKind.Crate,
			_ => EntityKind.Artifact
		};
		var sprite = kind == EntityKind.Chest ? "chest" : kind == EntityKind.Crate ? "crate" : (index % 2 == 0 ? "crystal" : "idol");
		var entity = CreateEntity( kind, sprite, new Vector2( 44, 44 ), 1 );
		entity.Radius = 25;
		var depth = 40 + (float)Rng.NextDouble() * 340;
		entity.Object.WorldPosition = RandomWorldPosition( depth );
		if ( kind == EntityKind.Artifact )
			entity.Loot = Catalog.Artifacts[index % 2];
	}

	private Vector3 RandomWorldPosition( float depth )
	{
		if ( Terrain is not null )
			return Terrain.RandomOpenPoint( MathF.Max( 10, depth - 40 ), depth + 40 );

		var x = -1500 + (float)Rng.NextDouble() * 3000;
		return new Vector3( x, 0, OceanTerrain.SurfaceZ - depth * 2.2f );
	}

	private void UpdateDive()
	{
		if ( Player is null )
			return;

		var dt = Time.Delta;
		DiveTime += dt;
		AttackCooldown = MathF.Max( 0, AttackCooldown - dt );
		Invulnerable = MathF.Max( 0, Invulnerable - dt );

		var input = Input.AnalogMove;
		// AnalogMove uses +X forward and +Y left; map that onto our side-on X/Z plane.
		var move = new Vector3( -input.y, 0, input.x );
		if ( move.Length > 1 ) move = move.Normal;
		var speed = UsingSub
			? Catalog.SubById( Save.EquippedSub ).Speed + UpgradeLevel( "Engine" ) * 8
			: DiveStats.Speed;
		if ( Input.Down( "Boost" ) && Energy > 0 )
		{
			speed *= 1.55f;
			Energy = MathF.Max( 0, Energy - 20 * dt );
		}
		else
		{
			Energy = MathF.Min( 100, Energy + 5 * dt );
		}

		Player.WorldPosition += move * speed * dt;
		var p = Player.WorldPosition;
		p.y = 0;
		if ( Terrain is not null )
			p = Terrain.Resolve( p, UsingSub ? 28f : 16f );
		else
		{
			p.x = Math.Clamp( p.x, -1750, 1750 );
			p.z = Math.Clamp( p.z, OceanTerrain.FloorZ + 20f, OceanTerrain.SurfaceZ );
		}
		Player.WorldPosition = p;
		if ( MathF.Abs( move.x ) > 0.05f ) PlayerFacing = MathF.Sign( move.x );
		if ( PlayerSprite is not null )
			PlayerSprite.Flip = PlayerFacing < 0;

		Depth = Terrain?.DepthMeters( p ) ?? MathF.Max( 0, (OceanTerrain.SurfaceZ - p.z) / 2.2f );
		MaxDepth = MathF.Max( MaxDepth, Depth );
		var zone = Catalog.ZoneIdAt( Depth );
		SetBackdrop( zone );
		if ( Backdrop.IsValid() )
			Backdrop.WorldPosition = new Vector3( p.x, 80, p.z );
		if ( Save.BiomeLog.Add( zone ) )
		{
			Discoveries.Add( Catalog.ZoneAt( Depth ) );
			Notice( $"New biome: {Catalog.ZoneAt( Depth )}", "discover" );
		}

		SurfacePromptAge += dt;
		var atSurface = Terrain?.AtSurface( p ) ?? p.z >= OceanTerrain.SurfaceZ - 14f;
		if ( atSurface )
		{
			if ( SurfacePromptAge > 4f )
			{
				SurfacePromptAge = 0;
				Notice( "Surface reached — press R or keep swimming up to exit.", "info" );
			}

			if ( move.z > 0.35f )
			{
				SettleDive( false );
				return;
			}
		}

		var drain = (1.8f + Depth / 150f) * dt;
		if ( Depth > DepthRating ) drain *= 1 + (Depth - DepthRating) / 60f;
		Oxygen = MathF.Max( 0, Oxygen - drain );
		if ( Oxygen <= 0 ) Health -= 14 * dt;
		if ( Depth > DepthRating + 20 )
			Health -= (5 + (Depth - DepthRating) * 0.12f) * (1 - DiveStats.Resistance / 100f) * dt;

		if ( Oxygen < MaxOxygen * 0.25f && DiveTime - LastWarn > 8 )
		{
			LastWarn = DiveTime;
			Notice( "O₂ low — return to shallower water.", "warning" );
		}

		UpdateEntities( dt );

		if ( Input.Pressed( "Attack1" ) ) Attack();
		if ( Input.Pressed( "Light" ) )
		{
			LightOn = !LightOn;
			Notice( LightOn ? "Light on" : "Light off", "info" );
		}
		if ( Input.Pressed( "Camera" ) ) TakePhoto();
		if ( Input.Pressed( "Slot1" ) ) HotbarSlot = 0;
		if ( Input.Pressed( "Slot2" ) ) HotbarSlot = 1;
		if ( Input.Pressed( "Slot3" ) ) HotbarSlot = 2;
		if ( Input.Pressed( "Surface" ) )
		{
			SettleDive( false );
			return;
		}

		if ( Health <= 0 )
		{
			Health = 0;
			SettleDive( true );
			return;
		}

		var cameraPos = Camera.GameObject.WorldPosition;
		var target = new Vector3( p.x, -1000, p.z );
		Camera.GameObject.WorldPosition = Vector3.Lerp( cameraPos, target, Math.Clamp( dt * 5, 0, 1 ) );

		SpawnClock += dt;
		if ( SpawnClock > 12 )
		{
			SpawnClock = 0;
			SpawnFish( Rng.NextDouble() < 0.2 );
		}
	}

	private void UpdateEntities( float dt )
	{
		foreach ( var entity in Entities.ToArray() )
		{
			if ( !entity.Object.IsValid() )
				continue;

			entity.AttackCooldown = MathF.Max( 0, entity.AttackCooldown - dt );
			var delta = Player.WorldPosition - entity.Object.WorldPosition;
			var distance = delta.Length;

			if ( entity.Kind is EntityKind.Fish or EntityKind.RareFish )
			{
				var next = entity.Object.WorldPosition + entity.Velocity * dt;
				if ( Terrain is not null )
					next = Terrain.Resolve( next, entity.Radius * 0.6f );
				entity.Object.WorldPosition = next;
				if ( MathF.Abs( entity.Object.WorldPosition.x ) > 1750 )
					entity.Velocity = new Vector3( -entity.Velocity.x, entity.Velocity.y, entity.Velocity.z );
				entity.Sprite.Flip = entity.Velocity.x < 0;
				if ( distance < 38 ) Collect( entity );
			}
			else if ( entity.Kind is EntityKind.Shark or EntityKind.Jelly )
			{
				if ( distance < (entity.Kind == EntityKind.Shark ? 260 : 100) )
				{
					entity.Velocity = delta.Normal * entity.Speed;
					entity.Sprite.Flip = entity.Velocity.x < 0;
				}
				var next = entity.Object.WorldPosition + entity.Velocity * dt;
				if ( Terrain is not null )
					next = Terrain.Resolve( next, entity.Radius * 0.7f );
				entity.Object.WorldPosition = next;
				if ( distance < entity.Radius + 20 && entity.AttackCooldown <= 0 && Invulnerable <= 0 )
				{
					var damage = entity.Damage * (1 - DiveStats.Resistance / 100f);
					Health -= damage;
					Invulnerable = 0.8f;
					entity.AttackCooldown = 1;
					Notice( $"{(entity.Kind == EntityKind.Shark ? "Shark" : "Jelly")} hit: -{damage:0}", "warning" );
				}
			}
			else if ( entity.Kind != EntityKind.Decor && distance < entity.Radius + 22 )
			{
				Collect( entity );
			}
		}
	}

	private void Attack()
	{
		if ( AttackCooldown > 0 )
			return;
		AttackCooldown = HotbarSlot == 1 ? 0.28f : 0.5f;
		var range = HotbarSlot == 0 ? 170 : 62;
		var damage = DiveStats.Damage * (HotbarSlot == 0 ? 1.35f : 1f);

		foreach ( var entity in Entities.Where( x => x.Kind is EntityKind.Shark or EntityKind.Jelly ).ToArray() )
		{
			var delta = entity.Object.WorldPosition - Player.WorldPosition;
			if ( delta.Length > range || delta.x * PlayerFacing < -10 )
				continue;
			entity.Health -= damage;
			if ( entity.Health <= 0 )
			{
				var loot = new HaulItem
				{
					Id = entity.Kind.ToString().ToLowerInvariant(),
					Name = entity.Kind == EntityKind.Shark ? "Reef Shark Sample" : "Jelly Membrane",
					Value = entity.Kind == EntityKind.Shark ? 55 : 22,
					Xp = entity.Kind == EntityKind.Shark ? 45 : 18,
					Sprite = entity.Kind == EntityKind.Shark ? "shark" : "jelly",
					Rarity = "Creature"
				};
				if ( AddHaul( loot ) )
				{
					Save.CreatureLog.Add( loot.Id );
					Discoveries.Add( loot.Name );
				}
				DestroyEntity( entity );
			}
			break;
		}
	}

	private void TakePhoto()
	{
		var target = Entities
			.Where( x => x.Kind is EntityKind.Shark or EntityKind.Jelly or EntityKind.RareFish )
			.OrderBy( x => (x.Object.WorldPosition - Player.WorldPosition).Length )
			.FirstOrDefault();
		if ( target is null || (target.Object.WorldPosition - Player.WorldPosition).Length > 180 )
		{
			Notice( "Nothing interesting in frame.", "info" );
			return;
		}
		var name = target.Kind.ToString();
		if ( Save.CreatureLog.Add( $"photo_{name}" ) )
			Discoveries.Add( $"Photo: {name}" );
		Notice( $"Photo logged: {name}", "discover" );
	}

	private void Collect( DiveEntity entity )
	{
		if ( entity.Kind == EntityKind.Drone )
		{
			if ( AddHaul( ToHaul( entity.Loot ) ) )
			{
				ObjectiveProgress = 1;
				ObjectiveText = "Drone recovered — surface with R";
				Save.ArtifactLog.Add( entity.Loot.Id );
				Discoveries.Add( entity.Loot.Name );
				Notice( "Research Drone recovered!", "discover" );
				DestroyEntity( entity );
			}
			return;
		}

		if ( entity.Kind is EntityKind.Chest or EntityKind.Crate )
		{
			var pool = Depth > 100 ? Catalog.Artifacts.Cast<LootDef>().Concat( Catalog.Fish ) : Catalog.Fish;
			var loot = pool.OrderBy( _ => Rng.Next() ).First( x => x.MinDepth <= MathF.Max( 60, Depth ) );
			if ( AddHaul( ToHaul( loot ) ) )
			{
				LogDiscovery( loot );
				Notice( $"{entity.Kind} opened: {loot.Name}", "discover" );
				DestroyEntity( entity );
			}
			return;
		}

		if ( entity.Loot is not null && AddHaul( ToHaul( entity.Loot ) ) )
		{
			LogDiscovery( entity.Loot );
			DestroyEntity( entity );
		}
	}

	private void LogDiscovery( LootDef loot )
	{
		var target = Catalog.Fish.Contains( loot ) ? Save.FishLog : Save.ArtifactLog;
		if ( target.Add( loot.Id ) )
		{
			Discoveries.Add( loot.Name );
			Notice( $"New log entry: {loot.Name}", "discover" );
		}
	}

	private HaulItem ToHaul( LootDef loot ) => new()
	{
		Id = loot.Id,
		Name = loot.Name,
		Value = loot.Value,
		Xp = loot.Xp,
		Sprite = loot.Sprite,
		Rarity = loot.Rarity
	};

	private bool AddHaul( HaulItem loot )
	{
		if ( CurrentHaul.Count >= CargoCapacity )
		{
			Notice( "Cargo full — surface to auto-sell.", "warning" );
			return false;
		}
		CurrentHaul.Add( loot );
		return true;
	}

	private void SettleDive( bool knockedOut )
	{
		if ( Settled )
			return;
		Settled = true;

		var multiplier = knockedOut ? 0.4f : 1f;
		var credits = (int)(CurrentHaul.Sum( x => x.Value ) * multiplier);
		var xp = CurrentHaul.Sum( x => x.Xp ) + (int)(MaxDepth / 4);
		Save.Gold += credits;
		AddXp( xp );
		Save.TotalDives++;
		Save.TotalDiveTime += DiveTime;
		Save.TotalItems += CurrentHaul.Count;
		Save.TotalCredits += credits;
		Save.DeepestDive = MathF.Max( Save.DeepestDive, MaxDepth );
		Save.LongestDive = MathF.Max( Save.LongestDive, DiveTime );
		Save.MostItems = Math.Max( Save.MostItems, CurrentHaul.Count );
		Save.MostCredits = Math.Max( Save.MostCredits, credits );

		LastResult = new DiveRecord
		{
			Number = Save.TotalDives,
			Day = Save.Day,
			Biome = Catalog.ZoneAt( MaxDepth ),
			BiomeId = Catalog.ZoneIdAt( MaxDepth ),
			Success = !knockedOut,
			MaxDepth = MaxDepth,
			Duration = DiveTime,
			Items = CurrentHaul.Count,
			Credits = credits,
			OxygenUsed = (int)((1 - Oxygen / MaxOxygen) * 100),
			Haul = CurrentHaul.ToList(),
			Discoveries = Discoveries.Distinct().ToList()
		};
		Save.DiveHistory.Insert( 0, LastResult );
		if ( Save.DiveHistory.Count > 30 ) Save.DiveHistory.RemoveAt( Save.DiveHistory.Count - 1 );
		Save.MinuteOfDay += Math.Max( 8, (int)(DiveTime / 4) );
		while ( Save.MinuteOfDay >= 1440 ) { Save.MinuteOfDay -= 1440; Save.Day++; }
		PlungeSave.Write( Save );
		Screen = GameScreen.Results;
		RefreshTutorialTips();
	}

	private void AddXp( int amount )
	{
		Save.Xp += amount;
		while ( Save.Xp >= Save.XpTarget )
		{
			Save.Xp -= Save.XpTarget;
			Save.Level++;
			Save.XpTarget = 100 + Save.Level * 45 + Save.Level * Save.Level * 8;
		}
	}

	private void DestroyEntity( DiveEntity entity )
	{
		Entities.Remove( entity );
		entity.Object.Destroy();
	}

	private void ClearWorld()
	{
		foreach ( var entity in Entities.ToArray() )
			if ( entity.Object.IsValid() ) entity.Object.Destroy();
		Entities.Clear();
		Terrain?.Clear();
		Terrain = null;
		if ( Player.IsValid() ) Player.Destroy();
		if ( Backdrop.IsValid() ) Backdrop.Destroy();
		Player = null;
		Backdrop = null;
	}

	private void Notice( string text, string cssClass )
	{
		LatestNotice = text;
		NoticeClass = cssClass;
		NoticeAge = 0;
	}
}
