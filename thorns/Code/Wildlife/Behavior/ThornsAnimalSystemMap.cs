namespace Sandbox;

/// <summary>
/// Maps the modular animal AI design names to existing Thorns wildlife types (extend, do not duplicate).
/// </summary>
/// <remarks>
/// AnimalController → <see cref="ThornsWildlifeBrain"/> + <see cref="ThornsWildlifeMotor"/><br/>
/// AnimalBrain → <see cref="ThornsWildlifeBrain"/><br/>
/// AnimalStats → <see cref="ThornsWildlifeSpeciesDefinition"/> + <see cref="ThornsAnimalBehaviorProfile"/><br/>
/// AnimalPerception → <see cref="ThornsWildlifePerception"/> + <see cref="ThornsAnimalPerceptionService"/><br/>
/// AnimalRelationshipTable → <see cref="ThornsAnimalRelationshipTable"/><br/>
/// AnimalMovement → <see cref="ThornsWildlifeMotor"/> + <see cref="ThornsAnimalMotorWishService"/><br/>
/// AnimalCombat → <see cref="ThornsWildlifeCombat"/><br/>
/// AnimalAnimationController → <see cref="ThornsWildlifeAnimSync"/> + <see cref="ThornsWildlifeLocomotionAnimSelector"/><br/>
/// AnimalCollisionAvoidance → <see cref="ThornsAnimalCollisionAvoidance"/><br/>
/// AnimalPackCoordinator → <see cref="ThornsAnimalPackCoordinator"/><br/>
/// AnimalHerdCoordinator → <see cref="ThornsAnimalHerdCoordinator"/><br/>
/// AnimalState → <see cref="ThornsWildlifeAiState"/>
/// </remarks>
public static class ThornsAnimalSystemMap
{
}
