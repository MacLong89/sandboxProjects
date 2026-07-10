namespace Fauna2;

/// <summary>Wild animal in the wilderness — roams its plot until caught or scared off.</summary>
public sealed class WildAnimalComponent : Component
{
	[Sync( SyncFlags.FromHost )] public string SpeciesId { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public string WildId { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public bool Fled { get; set; }
	[Sync( SyncFlags.FromHost )] public int PlotX { get; set; }
	[Sync( SyncFlags.FromHost )] public int PlotY { get; set; }

	private GameObject _visual;
	private TimeUntil _respawnAfterFlee;
	private TimeUntil _nextRetarget;
	private TimeUntil _nextAttackCheck;
	private TimeUntil _attackCooldown;
	private Vector3 _moveTarget;
	private Vector3 _fleePosition;
	private bool _moving;

	public AnimalDefinition Definition => Defs.Animal( SpeciesId );

	protected override void OnAwake()
	{
		if ( string.IsNullOrEmpty( WildId ) )
			WildId = Guid.NewGuid().ToString( "N" );
	}

	protected override void OnStart()
	{
		RebuildVisual();
		EnsurePickCollider();
		WildAnimalRegistry.Register( this );

		if ( !IsProxy )
			PickRoamTarget();
	}

	protected override void OnDestroy()
	{
		WildAnimalRegistry.Unregister( this );
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy )
			return;

		if ( Fled )
		{
			if ( !_respawnAfterFlee )
			{
				Fled = false;
				GameObject.WorldPosition = _fleePosition;
				RebuildVisual();
				PickRoamTarget();
			}

			return;
		}

		if ( IsBeingCaught() || IsAttackingPlayer() )
		{
			_moving = false;
			return;
		}

		TryStartAttack();

		if ( !_moving )
			PickRoamTarget();

		TickMovement();
	}

	public void KickMovement()
	{
		if ( IsProxy ) return;
		if ( Definition is null ) return;

		if ( !_moving || _moveTarget == Vector3.Zero )
		{
			PickRoamTarget();
		}
	}

	public void Flee()
	{
		if ( Fled ) return;
		Fled = true;
		_moving = false;
		_fleePosition = GameObject.WorldPosition;
		_visual?.Destroy();
		_visual = null;
		_respawnAfterFlee = GameConstants.WildAnimalRespawnDuration;
	}

	private void EnsurePickCollider()
	{
		if ( GameObject.Components.Get<SphereCollider>() is not null ) return;

		var def = Definition;
		if ( def is null ) return;

		var collider = GameObject.AddComponent<SphereCollider>();
		WorldSpriteCatalog.ConfigurePickSphere( collider, WorldSpriteCatalog.AnimalWorldSize( def ) );
	}

	private bool IsBeingCaught() =>
		CatchSystem.Instance?.MinigameActive == true
		&& CatchSystem.Instance.ActiveWildId == WildId;

	private bool IsAttackingPlayer() =>
		WildAttackSystem.Instance?.EncounterActive == true
		&& WildAttackSystem.Instance.ActiveWildId == WildId;

	private void TryStartAttack()
	{
		var def = Definition;
		if ( def is null || def.WildAggression <= 0f ) return;
		if ( WildAttackSystem.Instance?.EncounterActive == true ) return;
		if ( CatchSystem.Instance?.MinigameActive == true ) return;
		if ( _attackCooldown ) return;
		if ( _nextAttackCheck ) return;

		_nextAttackCheck = GameConstants.WildAnimalAttackCheckInterval;

		var player = FindOwnerPlayerInRange( GameConstants.WildAnimalAttackRange );
		if ( player is null ) return;

		var distance = player.FeetPosition.WithZ( 0 ).Distance( GameObject.WorldPosition.WithZ( 0 ) );
		var closeness = (1f - distance / GameConstants.WildAnimalAttackRange).Clamp( 0f, 1f );
		var chance = (def.WildAggression * (0.10f + closeness * 0.18f)).Clamp( 0f, 0.45f );
		if ( Game.Random.Float( 0f, 1f ) > chance ) return;

		_attackCooldown = GameConstants.WildAnimalAttackCooldownSeconds;
		WildAttackSystem.Instance?.TryBeginAttack( this, player );
	}

	private PlayerState FindOwnerPlayerInRange( float range )
	{
		var local = PlayerState.Local;
		if ( local.IsValid() && local.IsZooOwner )
		{
			var dist = local.FeetPosition.WithZ( 0 ).Distance( GameObject.WorldPosition.WithZ( 0 ) );
			if ( dist <= range )
				return local;
		}

		PlayerState best = null;
		var bestDist = range;

		foreach ( var player in Game.ActiveScene.GetAllComponents<PlayerState>() )
		{
			if ( !player.IsValid() || !player.IsZooOwner || player == local ) continue;

			var dist = player.FeetPosition.WithZ( 0 ).Distance( GameObject.WorldPosition.WithZ( 0 ) );
			if ( dist > bestDist ) continue;

			bestDist = dist;
			best = player;
		}

		return best;
	}

	private void PickRoamTarget()
	{
		var def = Definition;
		if ( def is null ) return;

		var starter = ZooState.Instance?.StarterBiome ?? Biome.Grassland;
		for ( var i = 0; i < 12; i++ )
		{
			var candidate = RandomPointNear( GameObject.WorldPosition, PlotX, PlotY );
			if ( BiomeEcology.CanWildAnimalAt( def, candidate, starter ) )
			{
				_moveTarget = candidate;
				_moving = true;
				_nextRetarget = Game.Random.Float(
					GameConstants.WildAnimalRetargetMinSeconds,
					GameConstants.WildAnimalRetargetMaxSeconds );
				return;
			}
		}

		_moveTarget = GameObject.WorldPosition;
		_moving = false;
		_nextRetarget = 0.75f;
	}

	private void TickMovement()
	{
		var def = Definition;
		if ( def is null || !_moving ) return;

		var pos = GameObject.WorldPosition;
		var delta = _moveTarget.WithZ( 0 ) - pos.WithZ( 0 );
		var distance = delta.Length;
		if ( distance < 10f )
		{
			PickRoamTarget();
			return;
		}

		if ( !_nextRetarget && distance < GameConstants.Tiles( 3f ) )
		{
			PickRoamTarget();
			return;
		}

		var dir = delta / distance;
		var speed = def.MoveSpeed
			* WildMovementSpeedMultiplier( def.Locomotion )
			* GameConstants.AnimalMoveSpeedMultiplier
			* GameConstants.WildAnimalRoamSpeedMultiplier;
		var next = pos + dir * MathF.Min( speed * Time.Delta, distance );
		next = ClampToPlot( next, PlotX, PlotY );

		var starter = ZooState.Instance?.StarterBiome ?? Biome.Grassland;
		if ( !BiomeEcology.CanWildAnimalAt( def, next, starter ) )
		{
			PickRoamTarget();
			return;
		}

		GameObject.WorldPosition = next.WithZ( 0f );

		if ( dir.LengthSquared > 0.0001f )
		{
			GameObject.WorldRotation = Rotation.Lerp(
				GameObject.WorldRotation,
				Rotation.LookAt( dir ),
				Time.Delta * 5f );
		}
	}

	private static float WildMovementSpeedMultiplier( AnimalLocomotion locomotion ) => locomotion switch
	{
		AnimalLocomotion.Predator => 1.16f,
		AnimalLocomotion.Hopper => 1.2f,
		AnimalLocomotion.Bird => 1.25f,
		AnimalLocomotion.Marine => 0.72f,
		AnimalLocomotion.Heavy => 0.68f,
		AnimalLocomotion.Grazer => 0.9f,
		_ => 1f,
	};

	public static Vector3 RandomPointOnPlot( int px, int py )
	{
		var center = PlotSystem.PlotCenter( px, py );
		var margin = GameConstants.TileSize * 5f;
		var half = GameConstants.PlotSize * 0.5f - margin;
		return center + new Vector3(
			Game.Random.Float( -half, half ),
			Game.Random.Float( -half, half ),
			0f );
	}

	public static Vector3 RandomValidPointOnPlot( int px, int py, Biome starterBiome, AnimalDefinition def )
	{
		for ( var i = 0; i < 24; i++ )
		{
			var point = RandomPointOnPlot( px, py );
			if ( BiomeEcology.CanWildAnimalAt( def, point, starterBiome ) )
				return point;
		}

		return Vector3.Zero;
	}

	private static Vector3 RandomPointNear( Vector3 origin, int px, int py )
	{
		var radius = GameConstants.Tiles( Game.Random.Float(
			GameConstants.WildAnimalRoamRadiusMinTiles,
			GameConstants.WildAnimalRoamRadiusMaxTiles ) );
		var angle = Game.Random.Float( 0f, MathF.PI * 2f );
		var offset = new Vector3( MathF.Cos( angle ) * radius, MathF.Sin( angle ) * radius, 0f );
		return ClampToPlot( origin + offset, px, py );
	}

	public static Vector3 ClampToPlot( Vector3 position, int px, int py )
	{
		var center = PlotSystem.PlotCenter( px, py );
		var margin = GameConstants.TileSize * 4f;
		var half = GameConstants.PlotSize * 0.5f - margin;
		return new Vector3(
			position.x.Clamp( center.x - half, center.x + half ),
			position.y.Clamp( center.y - half, center.y + half ),
			position.z );
	}

	private void RebuildVisual()
	{
		_visual?.Destroy();
		var def = Definition;
		if ( def is null ) return;
		_visual = CritterSpriteVisual.Build( GameObject, def, def.BodyTint );
	}
}
