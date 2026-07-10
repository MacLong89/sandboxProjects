namespace RunGun;

public enum MissionType
{
	KillElites,
	ReachDistance,
	CrossMultGates,
	KillCount,
	ComboStreak,
}

public sealed class MissionDef
{
	public MissionType Type { get; init; }
	public string Description { get; init; }
	public int Target { get; init; }
	public double Reward { get; init; }
}

public sealed class ActiveMission
{
	public MissionDef Def { get; init; }
	public int Progress { get; set; }
	public bool Complete => Progress >= Def.Target;
}

/// <summary>Three rotating per-run objectives that grant bonus cash on completion.</summary>
public sealed class MissionSystem
{
	private static readonly MissionDef[] Pool =
	[
		new() { Type = MissionType.KillElites, Description = "Kill {0} elites", Target = 3, Reward = 400 },
		new() { Type = MissionType.KillElites, Description = "Kill {0} elites", Target = 5, Reward = 700 },
		new() { Type = MissionType.ReachDistance, Description = "Reach {0}m", Target = 500, Reward = 350 },
		new() { Type = MissionType.ReachDistance, Description = "Reach {0}m", Target = 800, Reward = 600 },
		new() { Type = MissionType.CrossMultGates, Description = "Cross {0} mult gates", Target = 4, Reward = 450 },
		new() { Type = MissionType.CrossMultGates, Description = "Cross {0} mult gates", Target = 8, Reward = 800 },
		new() { Type = MissionType.KillCount, Description = "Kill {0} brutes", Target = 15, Reward = 300 },
		new() { Type = MissionType.KillCount, Description = "Kill {0} brutes", Target = 30, Reward = 550 },
		new() { Type = MissionType.ComboStreak, Description = "Hit a {0} combo", Target = 10, Reward = 400 },
		new() { Type = MissionType.ComboStreak, Description = "Hit a {0} combo", Target = 20, Reward = 750 },
	];

	public IReadOnlyList<ActiveMission> Active { get; private set; } = Array.Empty<ActiveMission>();
	public double PendingReward { get; private set; }

	public void BeginRun( int seed )
	{
		var rng = new Random( seed );
		var picks = Pool.OrderBy( _ => rng.Next() ).Take( 3 ).ToArray();
		Active = picks.Select( d => new ActiveMission { Def = d } ).ToList();
		PendingReward = 0;
	}

	public void Update( RunState run )
	{
		foreach ( var mission in Active )
		{
			if ( mission.Complete ) continue;

			mission.Progress = mission.Def.Type switch
			{
				MissionType.KillElites => run.EliteKillCount,
				MissionType.ReachDistance => (int)run.DistanceMeters,
				MissionType.CrossMultGates => run.MultGatesCrossed,
				MissionType.KillCount => run.KillCount,
				MissionType.ComboStreak => run.PeakCombo,
				_ => mission.Progress,
			};

			if ( mission.Complete )
				PendingReward += mission.Def.Reward;
		}
	}

	public string FormatDescription( ActiveMission mission ) =>
		string.Format( mission.Def.Description, mission.Def.Target );
}
