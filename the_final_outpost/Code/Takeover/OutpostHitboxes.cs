namespace FinalOutpost;

/// <summary>Aimbox-style citizen capsule hit volume for FP hitscan targets.</summary>
public static class OutpostHitboxes
{
	public const float CitizenRadius = 18f;
	public const float CitizenFeetZ = 0f;
	public const float CitizenHeadTopZ = 78f;
	public const float CitizenHeadshotMinZ = 53f;

	public static void ApplyCitizenHitbox( CapsuleCollider collider, float scale = 1f )
	{
		if ( collider is null ) return;
		var radius = CitizenRadius * MathF.Max( 0.55f, scale );
		collider.Start = new Vector3( 0f, 0f, CitizenFeetZ + radius );
		collider.End = new Vector3( 0f, 0f, CitizenHeadTopZ * scale - radius );
		collider.Radius = radius;
		// Solid so Scene.Trace hitscan can hit them. FP movement is kinematic (BuildingCollision only)
		// and never uses physics push against these capsules.
		collider.IsTrigger = false;
		collider.Enabled = true;
	}

	/// <summary>Toggle colliders under a humanoid (used to ghost allies during FP possession).</summary>
	public static void SetHierarchyCollisionEnabled( GameObject root, bool enabled )
	{
		if ( root is null || !root.IsValid() ) return;

		foreach ( var col in root.Components.GetAll<Collider>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !col.IsValid() ) continue;
			col.Enabled = enabled;
		}
	}

	public static bool IsHeadshot( Vector3 hitPosition, Vector3 rootPosition, float scale = 1f ) =>
		hitPosition.z >= rootPosition.z + CitizenHeadshotMinZ * MathF.Max( 0.55f, scale );
}

/// <summary>Links a capsule collider GameObject back to its <see cref="ZombieInstance"/>.</summary>
public sealed class ZombieHitTarget : Component
{
	public ZombieInstance Zombie { get; set; }

	public static ZombieHitTarget Attach( GameObject go, ZombieInstance zombie, float scale = 1f )
	{
		if ( go is null || !go.IsValid() || zombie is null ) return null;
		var existing = go.Components.Get<ZombieHitTarget>();
		if ( existing is not null )
		{
			existing.Zombie = zombie;
			return existing;
		}

		var hit = go.Components.Create<ZombieHitTarget>();
		hit.Zombie = zombie;
		var capsule = go.Components.GetOrCreate<CapsuleCollider>();
		OutpostHitboxes.ApplyCitizenHitbox( capsule, scale );
		return hit;
	}

	public static ZombieInstance FindZombie( GameObject hit )
	{
		if ( hit is null || !hit.IsValid() ) return null;
		var target = hit.Components.Get<ZombieHitTarget>( FindMode.EverythingInSelfAndParent );
		if ( target?.Zombie is { Dead: false } z )
			return z;
		return null;
	}
}

/// <summary>Links a capsule collider GameObject back to a <see cref="HostileUnit"/>.</summary>
public sealed class HostileHitTarget : Component
{
	public HostileUnit Unit { get; set; }

	public static HostileHitTarget Attach( GameObject go, HostileUnit unit )
	{
		if ( go is null || !go.IsValid() || unit is null ) return null;
		var existing = go.Components.Get<HostileHitTarget>();
		if ( existing is not null )
		{
			existing.Unit = unit;
			return existing;
		}

		var hit = go.Components.Create<HostileHitTarget>();
		hit.Unit = unit;
		var capsule = go.Components.GetOrCreate<CapsuleCollider>();
		OutpostHitboxes.ApplyCitizenHitbox( capsule );
		return hit;
	}

	public static HostileUnit FindUnit( GameObject hit )
	{
		if ( hit is null || !hit.IsValid() ) return null;
		var target = hit.Components.Get<HostileHitTarget>( FindMode.EverythingInSelfAndParent );
		if ( target?.Unit is { } u && u.IsAlive )
			return u;
		return null;
	}
}
