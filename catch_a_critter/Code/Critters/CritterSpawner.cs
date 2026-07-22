namespace CatchACritter;

/// <summary>Host-side critter population manager. Keeps every biome stocked.</summary>
public sealed class CritterSpawner : Component
{
	[Property] public int PerBiome { get; set; } = 13;

	readonly Dictionary<Biome, int> _alive = new();
	TimeUntil _nextTick;

	protected override void OnStart()
	{
		if ( !Networking.IsHost ) return;

		// Initial fill.
		foreach ( var biome in BiomeCatalog.All )
			for ( int i = 0; i < PerBiome; i++ )
				Spawn( biome.Id );
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost || !_nextTick ) return;
		_nextTick = 1.5f;

		Recount();
		foreach ( var biome in BiomeCatalog.All )
		{
			if ( _alive.GetValueOrDefault( biome.Id ) < PerBiome )
				Spawn( biome.Id );
		}
	}

	void Recount()
	{
		_alive.Clear();
		foreach ( var critter in Scene.GetAllComponents<CritterAgent>() )
		{
			if ( critter.Caught || critter.Def is null ) continue;
			_alive[critter.Def.Biome] = _alive.GetValueOrDefault( critter.Def.Biome ) + 1;
		}
	}

	public void OnCritterCaught( CritterAgent critter )
	{
		// Respawn arrives via the periodic tick — a small gap keeps rares special.
	}

	void Spawn( Biome biomeId )
	{
		var biome = BiomeCatalog.Get( biomeId );

		// Lobby-wide luck: the highest luck bonus among connected players nudges spawns.
		var luck = 0f;
		foreach ( var p in Scene.GetAllComponents<CritterPlayer>() )
			luck = MathF.Max( luck, p.SpawnLuck );

		var def = SpeciesCatalog.Roll( biomeId, luck.Clamp( 0f, 1f ) );

		var shinyChance = Balance.ShinyBaseChance * (1f + luck);
		var shiny = Game.Random.Float() < shinyChance;

		var ang = Game.Random.Float( 0f, MathF.Tau );
		var dist = Game.Random.Float( biome.Radius * 0.1f, biome.Radius * 0.8f );
		var pos = new Vector3( biome.Center.x + MathF.Cos( ang ) * dist, biome.Center.y + MathF.Sin( ang ) * dist, 2f );

		var go = new GameObject( true, $"Critter_{def.Id}" );
		go.WorldPosition = pos;
		go.WorldRotation = Rotation.FromYaw( Game.Random.Float( 0f, 360f ) );

		var agent = go.Components.Create<CritterAgent>();
		agent.SpeciesId = def.Id;
		agent.Shiny = shiny;
		agent.SizeRoll = Game.Random.Float( 0.88f, 1.14f );

		go.NetworkSpawn( null );

		_alive[biomeId] = _alive.GetValueOrDefault( biomeId ) + 1;
	}
}
