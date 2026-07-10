namespace RunGun;

public readonly struct EnemySpawnSpec
{
	public EnemyType Type { get; init; }
	public float X { get; init; }
	public float Y { get; init; }
	public float Health { get; init; }
	public bool Elite { get; init; }
}

/// <summary>Deliberate enemy layouts that create positioning and target-priority puzzles.</summary>
public static class EnemyFormation
{
	private enum Pattern
	{
		SoloBrute,
		PairBrute,
		Line,
		SmallSwarm,
		WallGap,
		Pincers,
		Funnel,
		BigSwarm,
		Mixed,
		Horde,
		MegaBrute,
	}

	public static List<EnemySpawnSpec> Generate( float baseX, float meters, RunSection section, int seed )
	{
		var rng = new Random( seed ^ (int)(baseX * 0.1f) );
		var health = GameConstants.EnemyBaseHealth
			+ meters * GameConstants.EnemyHealthPerMeter
			+ meters * meters * GameConstants.EnemyHealthAccel;
		var tier = Difficulty.FormationTier( meters );
		var bonus = Difficulty.PackSizeBonus( meters );   // extra bodies as the run deepens
		var specs = new List<EnemySpawnSpec>();

		// Breather sections are always light: a single easy target, sometimes nothing extra.
		if ( section == RunSection.Breather && RngFloat( rng ) > 0.35f )
		{
			specs.Add( Make( EnemyType.Brute, baseX, RngRange( rng, -60f, 60f ), health * 0.6f ) );
			return specs;
		}

		switch ( PickPattern( tier, rng ) )
		{
			case Pattern.SoloBrute:
				specs.Add( Make( EnemyType.Brute, baseX, RngRange( rng, -110f, 110f ), health ) );
				break;

			case Pattern.PairBrute:
				specs.Add( Make( EnemyType.Brute, baseX, -70f, health ) );
				specs.Add( Make( EnemyType.Brute, baseX + 40f, 70f, health ) );
				break;

			case Pattern.Line:
				var lineCount = Math.Clamp( 3 + bonus / 2, 3, 6 );
				for ( var i = 0; i < lineCount; i++ )
					specs.Add( Make( PickType( meters, rng ), baseX + i * 70f, LaneSlot( i, lineCount ), health ) );
				break;

			case Pattern.SmallSwarm:
				var smallCount = 3 + bonus;
				for ( var i = 0; i < smallCount; i++ )
					specs.Add( Make( EnemyType.Swarm, baseX + i * 40f, RngRange( rng, -120f, 120f ), health * 0.32f ) );
				break;

			case Pattern.WallGap:
				specs.Add( Make( EnemyType.Tank, baseX, -90f, health * 1.4f ) );
				specs.Add( Make( EnemyType.Tank, baseX + 40f, 90f, health * 1.4f ) );
				specs.Add( Make( EnemyType.Rusher, baseX + 80f, 0f, health * 0.7f ) );
				break;

			case Pattern.Pincers:
				specs.Add( Make( EnemyType.Rusher, baseX, -120f, health * 0.8f ) );
				specs.Add( Make( EnemyType.Rusher, baseX + 30f, 120f, health * 0.8f ) );
				specs.Add( Make( EnemyType.Spitter, baseX + 60f, 0f, health ) );
				break;

			case Pattern.Funnel:
				specs.Add( Make( EnemyType.Shielded, baseX, -60f, health * 1.1f ) );
				specs.Add( Make( EnemyType.Shielded, baseX + 50f, 60f, health * 1.1f ) );
				specs.Add( Make( EnemyType.Splitter, baseX + 100f, 0f, health * 0.9f ) );
				break;

			case Pattern.BigSwarm:
				var bigCount = 5 + bonus;
				for ( var i = 0; i < bigCount; i++ )
					specs.Add( Make( EnemyType.Swarm, baseX + i * 35f, RngRange( rng, -130f, 130f ), health * 0.3f ) );
				break;

			case Pattern.Horde:
				// A wide screen-filler: a mob of swarm/rushers fronted by a couple of tanks.
				var hordeCount = 6 + bonus;
				for ( var i = 0; i < hordeCount; i++ )
				{
					var t = RngFloat( rng ) < 0.3f ? EnemyType.Rusher : EnemyType.Swarm;
					specs.Add( Make( t, baseX + (i % 4) * 42f, RngRange( rng, -140f, 140f ), health * 0.32f ) );
				}
				specs.Add( Make( EnemyType.Tank, baseX + 120f, -70f, health * 1.5f ) );
				specs.Add( Make( EnemyType.Tank, baseX + 150f, 70f, health * 1.5f ) );
				break;

			case Pattern.MegaBrute:
				// A single hulking brute — huge body via health-driven scale — with light escorts.
				specs.Add( Make( EnemyType.Brute, baseX + 30f, RngRange( rng, -40f, 40f ), health * 3.5f ) );
				for ( var i = 0; i < 3; i++ )
					specs.Add( Make( EnemyType.Swarm, baseX, RngRange( rng, -130f, 130f ), health * 0.28f ) );
				break;

			default: // Mixed
				specs.Add( Make( PickType( meters, rng ), baseX, RngRange( rng, -120f, 120f ), health ) );
				specs.Add( Make( PickType( meters, rng ), baseX + 85f, RngRange( rng, -120f, 120f ), health * 1.1f ) );
				break;
		}

		// Swarm sections get a bonus trickle, but only once the run has ramped up a bit.
		if ( section == RunSection.Swarm && tier >= 1 )
			specs.Add( Make( EnemyType.Swarm, baseX + 120f, RngRange( rng, -80f, 80f ), health * 0.35f ) );

		// Elites only after the opening stretch. Just tag them — Enemy.Setup applies the
		// health multiplier so we don't double-scale it.
		if ( meters > GameConstants.Tier2Meters && RngFloat( rng ) < GameConstants.EliteSpawnChance )
		{
			var idx = rng.Next( 0, specs.Count );
			specs[idx] = specs[idx] with { Elite = true };
		}

		// Hard perf ceiling — citizen bodies are expensive, so trim overflow.
		if ( specs.Count > GameConstants.MaxFormationSize )
			specs.RemoveRange( GameConstants.MaxFormationSize, specs.Count - GameConstants.MaxFormationSize );

		return specs;
	}

	private static Pattern PickPattern( int tier, Random rng )
	{
		var pool = tier switch
		{
			0 => new[] { Pattern.SoloBrute, Pattern.SoloBrute, Pattern.PairBrute },
			1 => new[] { Pattern.SoloBrute, Pattern.PairBrute, Pattern.Line, Pattern.SmallSwarm },
			2 => new[] { Pattern.PairBrute, Pattern.Line, Pattern.SmallSwarm, Pattern.WallGap, Pattern.Pincers, Pattern.Mixed, Pattern.MegaBrute },
			_ => new[] { Pattern.Line, Pattern.SmallSwarm, Pattern.WallGap, Pattern.Pincers, Pattern.Funnel, Pattern.BigSwarm, Pattern.Mixed, Pattern.Horde, Pattern.Horde, Pattern.MegaBrute },
		};
		return pool[rng.Next( 0, pool.Length )];
	}

	private static EnemyType PickType( float meters, Random rng )
	{
		if ( meters < GameConstants.Tier1Meters ) return EnemyType.Brute;
		var roll = RngFloat( rng );
		if ( meters < GameConstants.Tier2Meters )
			return roll < 0.35f ? EnemyType.Rusher : EnemyType.Brute;

		if ( roll < 0.18f ) return EnemyType.Rusher;
		if ( roll < 0.32f ) return EnemyType.Tank;
		if ( roll < 0.44f ) return EnemyType.Splitter;
		if ( roll < 0.56f ) return EnemyType.Shielded;
		if ( roll < 0.68f ) return EnemyType.Swarm;
		if ( roll < 0.8f ) return EnemyType.Spitter;
		return EnemyType.Brute;
	}

	private static float LaneSlot( int index, int count )
	{
		var t = count <= 1 ? 0.5f : index / (float)(count - 1);
		return MathX.Lerp( -GameConstants.LaneHalf + 50f, GameConstants.LaneHalf - 50f, t );
	}

	private static EnemySpawnSpec Make( EnemyType type, float x, float y, float health ) =>
		new() { Type = type, X = x, Y = y, Health = health };

	private static float RngFloat( Random rng ) => (float)rng.NextDouble();

	private static float RngRange( Random rng, float min, float max ) =>
		min + (float)rng.NextDouble() * (max - min);
}
