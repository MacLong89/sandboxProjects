namespace Sandbox;

/// <summary>Shared mutable AI context for all animal state machines (wild + tamed).</summary>
public sealed class ThornsAnimalBrainContext
{
	public ThornsWildlifeBrain Brain { get; init; }
	public GameObject Self => Brain?.GameObject;
	public ThornsWildlifeIdentity Identity { get; set; }
	public ThornsWildlifeSpeciesDefinition Definition { get; set; }
	public ThornsWildlifeMotor Motor { get; set; }
	public ThornsWildlifeCombat Combat { get; set; }
	public ThornsWildlifeAnimSync AnimSync { get; set; }
	public ThornsHealth Health { get; set; }

	public ThornsWildlifeAiState CurrentState { get; set; }
	public ThornsWildlifeAiState PreviousState { get; set; }

	public GameObject CurrentTarget;
	public GameObject OwnerPlayer;
	public GameObject LeaderAnimal;
	public Vector3 HomePosition;
	public readonly List<Vector3> PatrolPoints = new();

	public ThornsAnimalBehaviorMode BehaviorMode = ThornsAnimalBehaviorMode.Passive;

	public Vector3 SpawnFlat;
	public Vector3 WanderGoalFlat;
	public GameObject FleeThreatRoot;
	public GameObject FocusTarget;
	public ThornsWildlifeBrain PreyFocusBrain;

	public Vector3 FleeWishPlanar;
	public double FleeUntilRealtime;
	public double HuntAbandonAfterRealtime;
	public double PredatorPeaceUntilRealtime;
	public double RecentAttackerUntilRealtime;
	public GameObject RecentAttackerRoot;

	public bool DormantPassiveHold;
	public double NextTameOwnerNearUnstickRealtime;
	public double TameWanderGoalPickedAtRealtime;

	public float ThreatScore;
	public float ThinkIntervalSeconds = 0.14f;
	public float TargetRefreshIntervalSeconds = 0.35f;
	public double NextTargetRefreshRealtime;

	public float SpeedMultiplier = 1f;

	public ThornsAnimalBehaviorProfile BehaviorProfile;
	public int NearbyPackMembers;
	public ThornsAnimalRelationshipKind LastRelationship = ThornsAnimalRelationshipKind.Ignore;
	public string LastRelationshipLabel = "none";
	public double NextStateChangeAllowedRealtime;

	public void BindComponents()
	{
		if ( !Brain.IsValid() )
			return;

		Identity = Brain.Components.Get<ThornsWildlifeIdentity>();
		Definition = Identity.IsValid() ? Identity.Definition : null;
		Motor = Brain.Components.Get<ThornsWildlifeMotor>();
		Combat = Brain.Components.Get<ThornsWildlifeCombat>();
		AnimSync = Brain.Components.Get<ThornsWildlifeAnimSync>();
		Health = Brain.Components.Get<ThornsHealth>();
		if ( Identity.IsValid() )
			BehaviorProfile = ThornsAnimalBehaviorProfile.Get( Identity.Species );
	}

	public void RefreshPackAndProfileStats()
	{
		if ( !Identity.IsValid() || Identity.HostIsTamed )
		{
			NearbyPackMembers = 0;
			return;
		}

		BehaviorProfile = ThornsAnimalBehaviorProfile.Get( Identity.Species );
		if ( BehaviorProfile.PackPreference > 0.45f )
			NearbyPackMembers = ThornsAnimalPackCoordinator.CountPackMembers( Self, Identity.Species );
		else
			NearbyPackMembers = 0;
	}

	public void RefreshBehaviorModeFromDefinition()
	{
		if ( Definition is null )
			return;

		if ( Identity.IsValid() && Identity.HostIsTamed )
		{
			BehaviorMode = Definition.IsPredator
				? ThornsAnimalBehaviorMode.Aggressive
				: ThornsAnimalBehaviorMode.Defensive;
			return;
		}

		BehaviorMode = Definition.IsPredator
			? ThornsAnimalBehaviorMode.Predator
			: ThornsAnimalBehaviorMode.Passive;
	}
}
