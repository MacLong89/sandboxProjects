namespace Sandbox;

/// <summary>Client-side skip for wildlife mesh animation when far from the local pawn.</summary>
public static class ThornsWildlifeAnimDistanceLod
{
	public const float SkipAnimUpdateBeyondDistanceWorld = 5200f;

	static float SkipBeyondDistanceSq =>
		SkipAnimUpdateBeyondDistanceWorld * SkipAnimUpdateBeyondDistanceWorld;

	public static bool ShouldSkipClientAnimUpdate( GameObject root )
	{
		if ( !Game.IsPlaying || root is null || !root.IsValid() )
			return true;

		var local = ThornsPawn.Local;
		if ( !local.IsValid() || !local.GameObject.IsValid() )
			return false;

		return (root.WorldPosition - local.GameObject.WorldPosition).LengthSquared > SkipBeyondDistanceSq;
	}
}
