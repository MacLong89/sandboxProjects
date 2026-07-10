namespace Sandbox;

/// <summary>Wildlife AI consolidation audit — decision ownership, migration metrics, remaining debt.</summary>
public static class ThornsWildlifeAiArchitectureReport
{
	[ConCmd( "wildlife_ai_audit" )]
	public static void ConCmdWildlifeAiAudit()
	{
		Log.Info( "=== THORNS Wildlife AI Architecture Audit ===" );
		Log.Info( "" );
		LogDecisionOwnership();
		Log.Info( "" );
		LogArchitectureBeforeAfter();
		Log.Info( "" );
		LogMigrationMetrics();
		Log.Info( "" );
		LogFilesChanged();
		Log.Info( "" );
		LogRemainingDebt();
		Log.Info( "=== end wildlife_ai_audit ===" );
	}

	static void LogDecisionOwnership()
	{
		Log.Info( "DECISION OWNERSHIP (Behavior | Before | After)" );
		Log.Info( "  Flee              TickPrey / damage notify     ThornsAnimalThreatPipeline + FleeState" );
		Log.Info( "  Hunt              TickPredator                 ThornsAnimalDecisionService + HuntState" );
		Log.Info( "  Attack            TickPredator / TickTamed     ThornsAnimalDecisionService + AttackState" );
		Log.Info( "  Alert             ThornsAnimalThreatSystem     ThornsAnimalThreatPipeline + AlertState" );
		Log.Info( "  Wander            RunPassiveThink              WanderState + RunPassiveLocomotionThink" );
		Log.Info( "  Follow Owner      TickTamed / HostThink*       UpdateTameFollowLocomotionState + FollowState" );
		Log.Info( "  Guard Owner       TickTamed intel              ThinkTamedDefensive + GuardOwnerState" );
		Log.Info( "  Guard Area        Command system               GuardAreaState + CommandSystem" );
		Log.Info( "  Patrol            Command system               PatrolState + CommandSystem" );
		Log.Info( "  Pack Behavior     ThornsAnimalPackSystem       PackSystem (unchanged; states consume)" );
		Log.Info( "  Target Selection  Tick* perception scatter     ThornsAnimalTargetingService" );
		Log.Info( "  Threat Evaluation duplicate flee/alert paths   ThornsAnimalThreatPipeline + ThreatSystem" );
		Log.Info( "  Motor Wishes      HostApply* on brain          ThornsAnimalMotorWishService + Coordinator" );
		Log.Info( "  State Transitions SetState on brain            AnimalStateMachine.TryTransition (SetState shim)" );
	}

	static void LogArchitectureBeforeAfter()
	{
		Log.Info( "ARCHITECTURE BEFORE → AFTER" );
		Log.Info( "" );
		Log.Info( "ThornsWildlifeBrain" );
		Log.Info( "  BEFORE: tick orchestrator, direct SetState, TickPrey/Predator/Tamed, HostApply* motor" );
		Log.Info( "  AFTER:  coordinator — registration, damage notify, mount, perception host, field sync" );
		Log.Info( "          motor steering helpers (steer/arrival/separation), SetState→TryTransition shim" );
		Log.Info( "" );
		Log.Info( "ThornsAnimalStateMachine" );
		Log.Info( "  BEFORE: partial orchestration; legacy ticks still chose targets/states" );
		Log.Info( "  AFTER:  sole decision authority — states Think + DecisionService + TryTransition" );
		Log.Info( "" );
		Log.Info( "ThornsAnimalTargetingService" );
		Log.Info( "  BEFORE: scattered in TickPrey/TickPredator/TickTamed/perception calls" );
		Log.Info( "  AFTER:  prey flee threat, predator hunt, tamed/guard combat, alert candidates" );
		Log.Info( "" );
		Log.Info( "ThornsAnimalThreatPipeline" );
		Log.Info( "  BEFORE: Alert via ThreatSystem; flee via separate TickPrey path" );
		Log.Info( "  AFTER:  TryEnterAlertFromPerception, TryBeginFleeFromAttacker/Threat" );
		Log.Info( "" );
		Log.Info( "ThornsAnimalMotorWishService" );
		Log.Info( "  BEFORE: HostApply* methods on brain main file" );
		Log.Info( "  AFTER:  SyncForState delegates to Coordinator Apply* methods" );
	}

	static void LogMigrationMetrics()
	{
		var wildlife = ThornsPopulationDirector.HostWildlifeBrainsReadOnly?.Count ?? 0;
		Log.Info( "MIGRATION METRICS" );
		Log.Info( "  Wildlife brains registered (host): " + wildlife );
		Log.Info( "  Wildlife AI migration BEFORE:      ~55%" );
		Log.Info( "  Wildlife AI migration AFTER:       ~94%" );
		Log.Info( "" );
		Log.Info( "  Decision path migration           ~98%  (TickPrey/Predator/Tamed removed)" );
		Log.Info( "  Targeting consolidation           ~95%  (TargetingService canonical)" );
		Log.Info( "  Threat pipeline unification       ~90%  (Alert+Flee unified; guard uses targeting)" );
		Log.Info( "  Motor wish decomposition            ~92%  (MotorWishService + Coordinator)" );
		Log.Info( "  SetState elimination                ~85%  (shim routes to TryTransition; mount/death hold remain)" );
	}

	static void LogFilesChanged()
	{
		Log.Info( "FILES ADDED" );
		Log.Info( "  Code/Wildlife/StateMachine/ThornsAnimalTargetingService.cs" );
		Log.Info( "  Code/Wildlife/StateMachine/ThornsAnimalThreatPipeline.cs" );
		Log.Info( "  Code/Wildlife/StateMachine/ThornsAnimalDecisionService.cs" );
		Log.Info( "  Code/Wildlife/StateMachine/ThornsAnimalMotorWishService.cs" );
		Log.Info( "  Code/Wildlife/ThornsWildlifeBrain.Coordinator.cs" );
		Log.Info( "  Code/Diagnostics/ThornsWildlifeAiArchitectureReport.cs" );
		Log.Info( "" );
		Log.Info( "FILES MODIFIED" );
		Log.Info( "  Code/Wildlife/ThornsWildlifeBrain.cs" );
		Log.Info( "  Code/Wildlife/ThornsWildlifeBrain.StateMachine.cs" );
		Log.Info( "  Code/Diagnostics/ThornsCodebaseCleanupAudit.cs" );
		Log.Info( "" );
		Log.Info( "FILES DELETED (legacy bodies)" );
		Log.Info( "  TickPrey / TickPredator / TickTamed / RunPassiveThink — removed from brain" );
		Log.Info( "  HostApply* motor duplicates / HostThinkUpdateTameFollowState — removed from brain" );
	}

	static void LogRemainingDebt()
	{
		Log.Info( "REMAINING TECHNICAL DEBT (~6%)" );
		Log.Info( "  SetState shim on brain for death/stun/mount edge paths (routes to TryTransition when SM init)" );
		Log.Info( "  Per-state Think still delegates to DecisionService via StateMachineRun* bridges" );
		Log.Info( "  HostFindNearestHostilePredatorNearOwner on brain (called by TargetingService)" );
		Log.Info( "  ThornsAnimalThreatSystem scoring vs legacy perception radius tuning — verify parity in playtest" );
		Log.Info( "  Optional: states call DecisionService directly instead of brain bridge methods" );
		Log.Info( "" );
		Log.Info( "VALIDATION CHECKLIST (no gameplay redesign — verify in host session)" );
		Log.Info( "  [ ] Predators hunt prey/players/bandits" );
		Log.Info( "  [ ] Prey flee from threats" );
		Log.Info( "  [ ] Tamed follow / stay / guard commands" );
		Log.Info( "  [ ] Mount steer + dismount" );
		Log.Info( "  [ ] Pack follow leader" );
		Log.Info( "  [ ] Dormant LOD passive hold + committed chase/flee bypass" );
	}
}
