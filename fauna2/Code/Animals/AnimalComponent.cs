namespace Fauna2;

public enum AnimalActivity
{
	Idle,
	Roam,
	Eat,
	Sleep,
	Socialize,
}

/// <summary>
/// A living animal in the zoo. Host simulates needs and a tiny activity state
/// machine; clients receive position via the networked transform and rebuild
/// visuals from synced definition/variant ids. Thinking is throttled and
/// staggered by AnimalSystem so hundreds of animals stay cheap.
/// </summary>
public sealed class AnimalComponent : Component
{
	// ── Identity (synced once) ──────────────────────────────
	[Sync( SyncFlags.FromHost )] public string AnimalId { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public string DefinitionId { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public string VariantId { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public string AnimalName { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public string HabitatId { get; set; } = "";

	// ── Live state (synced, host written) ───────────────────
	[Sync( SyncFlags.FromHost )] public float Hunger { get; set; } = 80f;
	[Sync( SyncFlags.FromHost )] public float Happiness { get; set; } = 70f;
	[Sync( SyncFlags.FromHost )] public float Health { get; set; } = 100f;
	[Sync( SyncFlags.FromHost )] public float AgeSeconds { get; set; }
	[Sync( SyncFlags.FromHost )] public AnimalActivity Activity { get; set; } = AnimalActivity.Idle;

	/// <summary>Host-only heritable data; persisted in saves.</summary>
	public AnimalGenome Genome { get; set; } = new();

	public AnimalDefinition Definition => _definition ??= Defs.Animal( DefinitionId );
	public VariantDefinition Variant => string.IsNullOrEmpty( VariantId ) ? null : (_variant ??= Defs.Variant( VariantId ));
	public HabitatComponent Habitat => HabitatRegistry.Find( HabitatId );

	public bool IsAdult => Definition is not null && AgeSeconds >= Definition.AdultAge;
	public bool IsElder => Definition is not null && AgeSeconds >= Definition.ElderAge;

	/// <summary>Host-side breeding cooldown.</summary>
	public TimeSince TimeSinceBred { get; set; } = float.MaxValue;

	/// <summary>Effective guest appeal including variant + genetics + mood.</summary>
	public float EffectiveAppeal
	{
		get
		{
			if ( Definition is null ) return 0f;
			var appeal = Definition.GuestAppeal;
			appeal *= Variant?.AppealMultiplier ?? 1f;
			appeal *= Genome.Stat( "appeal" );
			appeal *= 0.5f + Happiness / 200f;

			var state = ZooState.Instance;
			if ( state.IsValid() && Definition.Biome == state.StarterBiome )
				appeal *= 1f + state.NativeGuestAppealBonus;

			return appeal;
		}
	}

	/// <summary>What this animal is worth if sold.</summary>
	public int SellValue
	{
		get
		{
			if ( Definition is null ) return 0;
			var value = Definition.BaseValue * GameConstants.SellAnimalRefundFraction;
			value *= Variant?.ValueMultiplier ?? 1f;
			if ( !IsAdult ) value *= 0.5f;
			return (int)value;
		}
	}

	private AnimalDefinition _definition;
	private VariantDefinition _variant;
	private GameObject _visualRoot;

	// AI working state (host only)
	private Vector3 _moveTarget;
	private bool _moving;
	private TimeUntil _activityEnds;
	public TimeUntil NextThink { get; set; }

	protected override void OnStart()
	{
		BuildVisuals();
		AnimalRegistry.Register( this );

		if ( !IsProxy )
		{
			NextThink = Game.Random.Float( 0f, GameConstants.AnimalThinkInterval );
		}
	}

	protected override void OnDestroy()
	{
		AnimalRegistry.Unregister( this );
		_visualRoot?.Destroy();
	}

	// ── Visuals ─────────────────────────────────────────────

	private void BuildVisuals()
	{
		if ( _visualRoot.IsValid() ) return;
		if ( Definition is null ) return;

		var tint = Variant?.Tint ?? Definition.BodyTint;
		_visualRoot = CritterSpriteVisual.Build( GameObject, Definition, tint );
		EnsurePickCollider();
	}

	private void EnsurePickCollider()
	{
		if ( GameObject.Components.Get<BoxCollider>() is not null ) return;
		if ( Definition is null ) return;

		var collider = GameObject.AddComponent<BoxCollider>();
		WorldSpriteCatalog.ConfigurePickBox( collider, WorldSpriteCatalog.AnimalWorldSize( Definition ) );
	}

	protected override void OnUpdate()
	{
		// Cosmetic growth + sleep squash — cheap, runs everywhere.
		if ( Definition is null ) return;

		var growth = Definition.AdultAge <= 0f ? 1f
			: MathF.Min( 1f, AgeSeconds / Definition.AdultAge );
		var size = GameConstants.BabyScale + (1f - GameConstants.BabyScale) * growth;

		if ( _visualRoot.IsValid() )
		{
			var squash = Activity == AnimalActivity.Sleep ? 0.6f : 1f;
			_visualRoot.LocalScale = new Vector3( size, size, size * squash );
		}
	}

	// ── Host simulation ─────────────────────────────────────

	/// <summary>Called by AnimalSystem on the host at a throttled cadence.</summary>
	public void Think( float deltaTime )
	{
		if ( Definition is null ) return;

		AgeSeconds += deltaTime;

		// Needs decay.
		Hunger = (Hunger - GameConstants.AtGamePace( GameConstants.HungerDecayPerSecond ) * deltaTime).Clamp( 0f, 100f );

		if ( Activity == AnimalActivity.Eat )
			Hunger = (Hunger + GameConstants.EatRestorePerSecond * deltaTime).Clamp( 0f, 100f );
		if ( Activity == AnimalActivity.Sleep )
			Health = (Health + 1.5f * deltaTime).Clamp( 0f, 100f );

		RecomputeMood( deltaTime );

		// Activity transitions.
		if ( _activityEnds )
			PickNextActivity();
	}

	private void RecomputeMood( float deltaTime )
	{
		var habitatScore = Habitat?.Score ?? 30f;

		var social = 100f;
		if ( Definition.IsSocial )
		{
			var friends = AnimalRegistry.CountInHabitat( HabitatId, DefinitionId ) - 1;
			social = friends > 0 ? 100f : 45f;
		}

		var hungerFactor = Hunger.Clamp( 0f, 100f );
		var target = hungerFactor * 0.45f + habitatScore * 0.35f + social * 0.20f;

		var state = ZooState.Instance;
		if ( state.IsValid()
			&& Definition is not null
			&& Definition.Biome == state.StarterBiome
			&& Habitat?.Biome == state.StarterBiome )
		{
			target += 100f * state.NativeBiomeHappinessBonus;
		}

		var weather = WeatherSeasonSystem.Instance;
		if ( weather is not null )
			target += BiomeIdentity.AnimalHappinessBonus( Definition.Biome, weather.Season, weather.Weather );

		target += ResearchSystem.Instance?.AnimalCareBonus ?? 0f;
		target += StaffSystem.Instance?.VetHealthBonus ?? 0f;

		Happiness = Happiness.LerpTo( target, deltaTime * 0.25f ).Clamp( 0f, 100f );

		var healthTarget = 40f + Happiness * 0.6f;
		healthTarget += StaffSystem.Instance?.VetHealthBonus ?? 0f;
		Health = Health.LerpTo( healthTarget, deltaTime * 0.05f ).Clamp( 0f, 100f );
	}

	private void PickNextActivity()
	{
		var habitat = Habitat;

		// Hungry animals go eat near the habitat's food point.
		if ( Hunger < GameConstants.HungerSeekFoodThreshold )
		{
			Activity = AnimalActivity.Eat;
			_activityEnds = Game.Random.Float( 6f, 10f );
			SetMoveTarget( habitat?.FoodPoint ?? GameObject.WorldPosition );
			return;
		}

		var roll = Game.Random.Float();

		var sleepChance = Definition.Locomotion is AnimalLocomotion.Heavy or AnimalLocomotion.Marine ? 0.05f : 0.03f;
		var idleChance = Definition.Locomotion is AnimalLocomotion.Predator ? 0.12f : 0.08f;
		var socialChance = Definition.Locomotion is AnimalLocomotion.Grazer or AnimalLocomotion.Bird or AnimalLocomotion.Marine ? 0.42f : 0.34f;

		if ( roll < sleepChance )
		{
			Activity = AnimalActivity.Sleep;
			_activityEnds = Game.Random.Float( 3f, Definition.Locomotion == AnimalLocomotion.Heavy ? 9f : 6f );
			_moving = false;
		}
		else if ( roll < idleChance )
		{
			Activity = AnimalActivity.Idle;
			_activityEnds = Game.Random.Float( 0.5f, 1.5f );
			_moving = false;
		}
		else if ( roll < socialChance && Definition.IsSocial )
		{
			Activity = AnimalActivity.Socialize;
			_activityEnds = Game.Random.Float( 16f, 28f );
			var friend = AnimalRegistry.RandomInHabitat( HabitatId, DefinitionId, this );
			SetMoveTarget( friend?.WorldPosition ?? RandomPointInHabitat( habitat ) );
		}
		else
		{
			Activity = AnimalActivity.Roam;
			_activityEnds = Game.Random.Float( 18f, 32f );
			SetMoveTarget( RandomPointInHabitat( habitat ) );
		}
	}

	private Vector3 RandomPointInHabitat( HabitatComponent habitat )
	{
		if ( habitat is null ) return GameObject.WorldPosition;
		return habitat.RandomPointInside();
	}

	private void SetMoveTarget( Vector3 target )
	{
		_moveTarget = target.WithZ( 0f );
		_moving = true;
	}

	protected override void OnFixedUpdate()
	{
		// Host moves animals; clients interpolate the networked transform.
		if ( IsProxy || !_moving || Definition is null ) return;

		var pos = GameObject.WorldPosition;
		var delta = _moveTarget - pos;
		var distance = delta.Length;

		if ( distance < 12f )
		{
			if ( Activity is AnimalActivity.Roam or AnimalActivity.Socialize )
			{
				if ( Activity == AnimalActivity.Socialize )
				{
					var friend = AnimalRegistry.RandomInHabitat( HabitatId, DefinitionId, this );
					SetMoveTarget( friend?.WorldPosition ?? RandomPointInHabitat( Habitat ) );
				}
				else
					SetMoveTarget( RandomPointInHabitat( Habitat ) );
			}
			else
				_moving = false;

			return;
		}

		var dir = delta / distance;
		var speed = Definition.MoveSpeed * MovementSpeedMultiplier( Definition.Locomotion ) * GameConstants.AnimalMoveSpeedMultiplier * (IsAdult ? 1f : 0.7f);
		var next = pos + dir * MathF.Min( speed * Time.Delta, distance );
		var habitat = Habitat;
		if ( habitat.IsValid() )
			next = habitat.ClampInside( next );
		GameObject.WorldPosition = next.WithZ( 0f );
		GameObject.WorldRotation = Rotation.Lerp(
			GameObject.WorldRotation,
			Rotation.LookAt( dir.WithZ( 0f ) ),
			Time.Delta * 6f );
	}

	private static float MovementSpeedMultiplier( AnimalLocomotion locomotion ) => locomotion switch
	{
		AnimalLocomotion.Predator => 1.12f,
		AnimalLocomotion.Hopper => 1.18f,
		AnimalLocomotion.Bird => 1.22f,
		AnimalLocomotion.Swimmer => 0.95f,
		AnimalLocomotion.Marine => 0.78f,
		AnimalLocomotion.Heavy => 0.72f,
		AnimalLocomotion.Grazer => 0.92f,
		_ => 1f,
	};
}
