namespace RunGun;

/// <summary>
/// Enemy archetypes with distinct movement, durability, and behaviors. The gate-vs-enemy
/// attention split only works when brutes aren't interchangeable.
/// </summary>
public sealed class Enemy : Component
{
	public EnemyType Type { get; private set; } = EnemyType.Brute;
	public bool IsElite { get; private set; }
	public float Health { get; private set; }
	public float MaxHealth { get; private set; }
	public bool Dead { get; private set; }

	public float X => WorldPosition.x;
	public float Y => WorldPosition.y;

	private UI.WorldLabel _label;
	private CitizenVisual _visual;
	private float _spitTimer;
	private float _hitFlash;

	public void Setup( EnemySpawnSpec spec, DailyChallengeSystem daily )
	{
		Type = spec.Elite && spec.Type != EnemyType.Boss ? EnemyType.Elite : spec.Type;
		IsElite = spec.Elite || Type == EnemyType.Elite;

		var health = spec.Health;
		if ( daily.ActiveModifier is DailyModifier.DoubleEnemyHp or DailyModifier.HardMode )
			health *= daily.ActiveModifier == DailyModifier.HardMode ? 1.6f : 2f;
		if ( Type == EnemyType.Tank ) health *= GameConstants.TankHealthMult;
		if ( Type == EnemyType.Swarm ) health *= GameConstants.SwarmHealthMult;
		if ( IsElite ) health *= GameConstants.EliteHealthMult;

		Health = health;
		MaxHealth = health;
		WorldPosition = new Vector3( spec.X, spec.Y, 0f );
		WorldRotation = Rotation.FromYaw( 180f );

		_visual = Components.Create<CitizenVisual>();
		_visual.BodyTint = EnemyTypePresentation.TintFor( Type, IsElite );
		_visual.BodyScale = EnemyTypePresentation.ScaleFor( Type, Health, IsElite );
		if ( Type == EnemyType.Swarm ) _visual.GunModel = "";

		var labelGo = new GameObject( GameObject, true, "Label" );
		labelGo.LocalPosition = new Vector3( 0f, 0f, GameConstants.BodyHeight + 55f );

		var wp = labelGo.Components.Create<Sandbox.WorldPanel>();
		wp.PanelSize = new Vector2( 220f, 120f );
		wp.RenderScale = 1f;
		wp.LookAtCamera = true;
		wp.InteractionRange = 0f;
		wp.RenderOptions.Game = true;

		_label = labelGo.Components.Create<UI.WorldLabel>();
		_label.Accent = new Color( 1f, 0.85f, 0.85f );
		RefreshLabel();
	}

	public bool Hit( float damage, bool fromFront, out float dealt )
	{
		dealt = 0f;
		if ( Dead ) return false;

		if ( Type == EnemyType.Shielded && fromFront )
			damage *= 1f - GameConstants.ShieldedFrontArmor;

		dealt = damage;
		Health = MathF.Max( 0f, Health - damage );
		_hitFlash = 0.12f;
		RefreshLabel();
		FlashHit();

		if ( Health <= 0f )
		{
			Dead = true;
			return true;
		}

		return false;
	}

	public void Advance( float dt, DailyChallengeSystem daily, float speedMult = 1f )
	{
		if ( Dead ) return;

		var speed = GameConstants.EnemyAdvanceSpeed * speedMult;
		speed *= Type switch
		{
			EnemyType.Rusher => GameConstants.RusherSpeedMult,
			EnemyType.Tank or EnemyType.Boss => GameConstants.TankSpeedMult,
			EnemyType.Swarm => 1.25f,
			_ => 1f,
		};

		if ( daily.ActiveModifier is DailyModifier.FastEnemies or DailyModifier.HardMode )
			speed *= daily.ActiveModifier == DailyModifier.HardMode ? 1.35f : 1.5f;

		WorldPosition = WorldPosition.WithX( X - speed * dt );

		if ( _hitFlash > 0f )
		{
			_hitFlash -= dt;
			if ( _hitFlash <= 0f && _visual.IsValid() )
				_visual.BodyTint = EnemyTypePresentation.TintFor( Type, IsElite );
		}
	}

	public bool TrySpit( Vector3 playerPos, TrackManager track )
	{
		if ( Dead || Type != EnemyType.Spitter && Type != EnemyType.Boss ) return false;
		if ( X - playerPos.x > GameConstants.SpitterRange || X < playerPos.x ) return false;

		_spitTimer -= Time.Delta;
		if ( _spitTimer > 0f ) return false;

		_spitTimer = Type == EnemyType.Boss ? GameConstants.BossAttackInterval * 0.6f : GameConstants.SpitterCooldown;
		var dir = (playerPos - WorldPosition).WithZ( 0f ).Normal;
		track.SpawnProjectile( WorldPosition.WithZ( GameConstants.BodyHeight * 0.5f ), dir,
			Type == EnemyType.Boss ? GameConstants.ProjectileDamage * 1.4f : GameConstants.ProjectileDamage,
			EnemyTypePresentation.TintFor( Type, IsElite ) );
		return true;
	}

	public double CoinValue( UpgradeSystem upgrades, DailyChallengeSystem daily )
	{
		var baseCoins = MaxHealth * GameConstants.EnemyCoinPerHealth * upgrades.CoinMult;
		if ( IsElite ) baseCoins *= GameConstants.EliteCoinMult;
		if ( Type == EnemyType.Boss ) baseCoins *= GameConstants.BossCoinMult;
		if ( daily.ActiveModifier == DailyModifier.BonusCoins ) baseCoins *= 2;
		return baseCoins;
	}

	private void FlashHit()
	{
		if ( !_visual.IsValid() ) return;
		_visual.BodyTint = Color.White;
	}

	private void RefreshLabel()
	{
		if ( _label is null ) return;
		var prefix = IsElite ? "★ " : Type == EnemyType.Boss ? "BOSS " : "";
		_label.Text = prefix + ((int)MathF.Ceiling( Health )).ToString();
	}
}
