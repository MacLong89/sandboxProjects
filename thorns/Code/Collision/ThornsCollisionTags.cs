namespace Sandbox;

/// <summary>
/// Canonical gameplay / world tags used for traces, filtering, and collision debug.
/// Keep in sync with terrain scatter, resource nodes, structures, and hitscan ignores.
/// </summary>
public static class ThornsCollisionTags
{
	public const string TerrainChunk = "thorns_terrain";

	public const string Solid = "solid";

	public const string World = "world";

	public const string Structure = "thorns_structure";

	public const string ResourceNode = "thorns_resource_node";

	public const string Boulder = "thorns_boulder";

	public const string InteriorFurniture = "thorns_interior_furniture";

	/// <summary>Dev gallery walk surface (<see cref="ThornsFurnitureScaleGallery"/>).</summary>
	public const string FurnitureGalleryFloor = "thorns_furniture_gallery_floor";

	public const string InteriorRadioRoot = ThornsWorldUseAim.InteriorRadioRootTag;

	public const string WildlifeHull = "thorns_wildlife_hull";

	/// <summary>Fauna body hull — blocks other creatures via Collision.config; terrain-follow probes still treat this tag as passthrough in code.</summary>
	public static void EnsureWildlifeHullTag( GameObject go )
	{
		if ( go is null || !go.IsValid() )
			return;

		AddTagIfMissing( go, WildlifeHull );
	}

	public static void EnsureWorldSolidTriplet( GameObject go )
	{
		if ( go is null || !go.IsValid() )
			return;

		AddTagIfMissing( go, TerrainChunk );
		AddTagIfMissing( go, Solid );
		AddTagIfMissing( go, World );
	}

	public static void EnsureResourceNodeMovementPassthrough( GameObject go )
	{
		if ( go is null || !go.IsValid() )
			return;

		RemoveTagIfPresent( go, TerrainChunk );
		RemoveTagIfPresent( go, Solid );
		RemoveTagIfPresent( go, World );
		AddTagIfMissing( go, ResourceNode );
	}

	/// <summary>
	/// Foliage2 wood trees — trunk <see cref="BoxCollider"/> blocks player movement (<c>solid</c>/<c>world</c>)
	/// while keeping <see cref="ResourceNode"/> for harvest routing and terrain-follow passthrough probes.
	/// </summary>
	public static void EnsureWoodTreeTrunkSolidCollision( GameObject go )
	{
		if ( go is null || !go.IsValid() )
			return;

		EnsureWorldSolidTriplet( go );
		AddTagIfMissing( go, ResourceNode );
	}

	/// <summary>Stone / ore harvest nodes block player movement (solid world hull). Wood uses <see cref="EnsureWoodTreeTrunkSolidCollision"/>.</summary>
	public static void EnsureMineralHarvestSolidCollision( GameObject go )
	{
		if ( go is null || !go.IsValid() )
			return;

		EnsureWorldSolidTriplet( go );
		RemoveTagIfPresent( go, ResourceNode );
	}

	static void AddTagIfMissing( GameObject go, string tag )
	{
		foreach ( var t in go.Tags )
		{
			if ( t == tag )
				return;
		}

		go.Tags.Add( tag );
	}

	static void RemoveTagIfPresent( GameObject go, string tag )
	{
		foreach ( var t in go.Tags )
		{
			if ( t != tag )
				continue;

			go.Tags.Remove( tag );
			return;
		}
	}
}
