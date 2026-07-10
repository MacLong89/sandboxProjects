namespace Sandbox;

/// <summary>
/// Local peer separation / collision avoidance for wildlife motors.
/// Delegates to brain depenetration and peer separation hooks.
/// </summary>
public static class ThornsAnimalCollisionAvoidance
{
	public const float MinPersonalSpace = 72f;
	public const float SeparationStrength = 1.15f;

	public static Vector3 ComputePeerSeparationWish(
		GameObject self,
		Vector3 currentWish,
		float strengthMul = 1f )
	{
		if ( self is null || !self.IsValid() )
			return currentWish;

		var brain = self.Components.Get<ThornsWildlifeBrain>();
		if ( !brain.IsValid() )
			return currentWish;

		return brain.HostApplyWildlifePeerSeparationToWish( currentWish, strengthMul * SeparationStrength );
	}
}
