namespace Sandbox;

/// <summary>Shared mutable context for all bandit state machines.</summary>
public sealed class ThornsBanditBrainContext
{
	public ThornsBanditBrain Brain { get; init; }
	public GameObject Self => Brain?.GameObject;

	public ThornsBanditArchetypeConfig Archetype { get; set; } = ThornsBanditArchetypeConfig.Scavenger();
	public ThornsBanditMotor Motor { get; set; }
	public ThornsBanditCombat Combat { get; set; }
	public ThornsHealth Health { get; set; }

	public ThornsBanditAiState CurrentState { get; set; }
	public ThornsBanditAiState PreviousState { get; set; }

	public GameObject CurrentTarget;
	public Vector3 LastKnownTargetPosition;
	public Vector3 HomePosition;
	public Vector3 SpawnPosition;
	public Vector3 AssignedGuardPoint;
	public GameObject AlertedBy;

	public int AlertLevel;
	public int GroupId;
	public float ThreatScore;

	public Vector3 WanderGoal;
	public Vector3 InvestigatePoint;
	public Vector3 CoverPoint;
	public int PatrolIndex;

	public double AlertReactionUntilRealtime;
	public double SearchUntilRealtime;
	public double InvestigateUntilRealtime;
	public double LastStateChangeRealtime;
	public double LastDetectionRealtime;
	public double LastThreatRefreshRealtime;
	public double LastHeardThreatRealtime;
	public double LastShotRealtime;
	public double LastSeenTargetRealtime;
	public double NoPlayerNearbySinceRealtime;

	public bool IsDead;

	public readonly List<Vector3> PatrolPoints = new();

	public void BindComponents()
	{
		if ( !Brain.IsValid() )
			return;

		Motor = Brain.Components.Get<ThornsBanditMotor>();
		Combat = Brain.Components.Get<ThornsBanditCombat>();
		Health = Brain.Components.Get<ThornsHealth>();
	}
}
