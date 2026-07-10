namespace Sandbox;

/// <summary>Shared capsule / velocity queries for human pawns (<see cref="PlayerController"/>) and legacy CC NPCs.</summary>
public static class ThornsPawnLocomotion
{
	/// <summary>Spawn / snap clearance above terraingen surface (matches coastal respawn — not capsule height).</summary>
	public const float DefaultFeetAboveTerrainSpawn = 18f;
	public static Vector3 TryGetVelocity( GameObject root )
	{
		if ( root is null || !root.IsValid() )
			return Vector3.Zero;

		var pc = root.Components.GetInAncestorsOrSelf<PlayerController>( true );
		if ( pc.IsValid() )
			return pc.Velocity;

		var cc = root.Components.GetInAncestorsOrSelf<CharacterController>( true );
		return cc.IsValid() ? cc.Velocity : Vector3.Zero;
	}

	public static bool TryGetHumanoidCapsule( GameObject root, out float radius, out float height )
	{
		radius = 12.8f;
		height = 72f;
		if ( root is null || !root.IsValid() )
			return false;

		var pc = root.Components.GetInAncestorsOrSelf<PlayerController>( true );
		if ( pc.IsValid() )
		{
			radius = Math.Max( 8f, pc.BodyRadius );
			height = Math.Max( 8f, pc.BodyHeight );
			return true;
		}

		var cc = root.Components.GetInAncestorsOrSelf<CharacterController>( true );
		if ( !cc.IsValid() )
			return false;

		radius = Math.Max( 8f, cc.Radius );
		height = Math.Max( 8f, cc.Height );
		return true;
	}

	public static float TryGetHumanoidHeight( GameObject root )
	{
		if ( TryGetHumanoidCapsule( root, out _, out var height ) )
			return height;

		return 72f;
	}
}
