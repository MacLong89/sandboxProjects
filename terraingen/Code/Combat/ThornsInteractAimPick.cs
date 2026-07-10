namespace Terraingen.Combat;

using Terraingen;

/// <summary>Crosshair aim rays and ray-vs-bounds helpers for world interact prompts.</summary>
public static class ThornsInteractAimPick
{
	public const float DefaultSphereTraceRadius = 28f;

	public static bool TryResolveCrosshairAimRay( GameObject playerRoot, out Vector3 origin, out Vector3 forward )
	{
		origin = default;
		forward = default;

		if ( !playerRoot.IsValid() )
			return false;

		if ( ThornsSceneObserver.TryResolveLocalAimRay( playerRoot, out origin, out forward, useScreenCenter: true ) )
			return true;

		var controller = playerRoot.Components.Get<PlayerController>( FindMode.EverythingInSelf );
		if ( !controller.IsValid() )
			return false;

		origin = playerRoot.WorldPosition + Vector3.Up * 64f;
		forward = controller.EyeAngles.ToRotation().Forward.Normal;
		return forward.Length >= 0.95f;
	}

	public static bool TryRaySphere( Vector3 origin, Vector3 direction, Vector3 center, float radius, out float distance )
	{
		distance = float.MaxValue;
		var dir = direction.Normal;
		if ( dir.Length < 0.95f )
			return false;

		var oc = origin - center;
		var b = 2f * Vector3.Dot( oc, dir );
		var c = oc.LengthSquared - radius * radius;
		var discriminant = b * b - 4f * c;
		if ( discriminant < 0f )
			return false;

		var sqrt = MathF.Sqrt( discriminant );
		var t0 = (-b - sqrt) * 0.5f;
		var t1 = (-b + sqrt) * 0.5f;
		distance = t0 >= 0f ? t0 : t1;
		return distance >= 0f;
	}
}
