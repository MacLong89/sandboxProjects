namespace Terraingen.Combat;

/// <summary>Color-coded wire overlays for collision debug (H).</summary>
public static class ThornsCollisionDebugDraw
{
	const int RingSegments = 24;

	public enum Category
	{
		Player,
		Bandit,
		Animal,
		Tree,
		Mineral,
		Boulder,
		LootContainer,
		PlayerStructure,
		BuildingFloor,
		BuildingWall,
		BuildingRoof,
		BuildingRamp,
		BuildingTrim,
		BuildingFoundation,
		Furniture,
		BuildingOther,
		ProbeHit,
		Settlement
	}

	public static Color ColorFor( Category category ) => category switch
	{
		Category.Player => Color.Cyan,
		Category.Bandit => Color.Red,
		Category.Animal => Color.Yellow,
		Category.Tree => new Color( 0.2f, 0.85f, 0.25f ),
		Category.Mineral => new Color( 1f, 0.55f, 0.1f ),
		Category.Boulder => new Color( 0.55f, 0.45f, 0.35f ),
		Category.LootContainer => Color.White,
		Category.PlayerStructure => new Color( 1f, 0.35f, 1f ),
		Category.BuildingFloor => new Color( 0.45f, 0.75f, 1f ),
		Category.BuildingWall => new Color( 0.15f, 0.45f, 1f ),
		Category.BuildingRoof => new Color( 0.55f, 0.35f, 0.95f ),
		Category.BuildingRamp => new Color( 0.2f, 0.9f, 0.75f ),
		Category.BuildingTrim => new Color( 0.65f, 0.65f, 0.65f ),
		Category.BuildingFoundation => new Color( 0.45f, 0.3f, 0.75f ),
		Category.Furniture => new Color( 1f, 0.72f, 0.15f ),
		Category.BuildingOther => new Color( 0.35f, 0.55f, 0.85f ),
		Category.ProbeHit => new Color( 1f, 0.1f, 0.1f ),
		Category.Settlement => new Color( 0.85f, 0.85f, 0.35f ),
		_ => Color.White
	};

	public static string LabelFor( Category category ) => category switch
	{
		Category.Player => "Player capsule",
		Category.Bandit => "Bandit capsule",
		Category.Animal => "Animal collider",
		Category.Tree => "Tree trunk",
		Category.Mineral => "Mineral rock",
		Category.Boulder => "Boulder",
		Category.LootContainer => "Loot container",
		Category.PlayerStructure => "Player build",
		Category.BuildingFloor => "Building floor",
		Category.BuildingWall => "Building wall/door/window",
		Category.BuildingRoof => "Building roof",
		Category.BuildingRamp => "Building ramp",
		Category.BuildingTrim => "Building trim/pillar",
		Category.BuildingFoundation => "Building foundation",
		Category.Furniture => "Interior furniture",
		Category.BuildingOther => "Building shell",
		Category.ProbeHit => "Forward probe hit",
		Category.Settlement => "Settlement layout",
		_ => category.ToString()
	};

	public static Category ClassifyObject( GameObject root )
	{
		if ( !root.IsValid() )
			return Category.BuildingOther;

		if ( root.Tags.Has( "furniture" ) || root.Name.StartsWith( "Furniture ", StringComparison.Ordinal ) )
			return Category.Furniture;

		if ( root.Tags.Has( "tree" ) )
			return Category.Tree;

		if ( root.Tags.Has( "mineral" ) )
			return Category.Mineral;

		if ( root.Tags.Has( "boulder" ) )
			return Category.Boulder;

		if ( !root.Tags.Has( "thorns_structure" ) )
			return Category.BuildingOther;

		var name = root.Name;
		if ( name.StartsWith( "floor_", StringComparison.Ordinal ) )
			return Category.BuildingFloor;

		if ( name is "wall" or "door" or "window" )
			return Category.BuildingWall;

		if ( name.StartsWith( "ramp_", StringComparison.Ordinal ) )
			return Category.BuildingRamp;

		if ( name is "foundation" )
			return Category.BuildingFoundation;

		if ( name.StartsWith( "roof", StringComparison.Ordinal ) || name is "roof_deck" )
			return Category.BuildingRoof;

		if ( name.StartsWith( "trim", StringComparison.Ordinal ) || name is "pillar" )
			return Category.BuildingTrim;

		return Category.BuildingOther;
	}

	public static void DrawCitizenCapsule(
		DebugOverlaySystem overlay,
		Vector3 feetWorld,
		float height,
		float radius,
		float duration,
		Category category )
	{
		if ( overlay is null )
			return;

		var color = ColorFor( category );
		var clampedHeight = MathF.Max( height, radius * 2f );
		var axisBottom = feetWorld + Vector3.Up * radius;
		var axisTop = feetWorld + Vector3.Up * (clampedHeight - radius );

		overlay.Capsule( new Capsule( axisBottom, axisTop, radius ), color, duration );
	}

	public static void DrawCollidersOnObject(
		DebugOverlaySystem overlay,
		GameObject root,
		float duration,
		Category? categoryOverride = null,
		bool includeFloors = true )
	{
		if ( overlay is null || !root.IsValid() )
			return;

		var category = categoryOverride ?? ClassifyObject( root );
		if ( !includeFloors && category == Category.BuildingFloor )
			return;

		var color = ColorFor( category );

		foreach ( var collider in root.Components.GetAll<Collider>( FindMode.EverythingInSelf ) )
			DrawCollider( overlay, collider, duration, color );
	}

	public static void DrawCollidersOnHierarchy(
		DebugOverlaySystem overlay,
		GameObject root,
		float duration,
		Category? categoryOverride = null,
		bool includeFloors = true )
	{
		if ( overlay is null || !root.IsValid() )
			return;

		DrawCollidersOnObject( overlay, root, duration, categoryOverride, includeFloors );
		foreach ( var child in root.Children )
		{
			if ( child.IsValid() )
				DrawCollidersOnHierarchy( overlay, child, duration, categoryOverride ?? ClassifyObject( child ), includeFloors );
		}
	}

	public static void DrawColliderComponent(
		DebugOverlaySystem overlay,
		Component collider,
		float duration,
		Category category )
	{
		if ( collider is Collider physicsCollider )
			DrawCollider( overlay, physicsCollider, duration, ColorFor( category ) );
	}

	public static void DrawCollider(
		DebugOverlaySystem overlay,
		Collider collider,
		float duration,
		Color color )
	{
		if ( overlay is null || !collider.IsValid() || collider.IsTrigger )
			return;

		var bounds = collider.GetWorldBounds();
		DrawWorldBounds( overlay, bounds, duration, color );
	}

	/// <summary>Wire box for disabled colliders (LOD off but hull still present).</summary>
	public static void DrawColliderDisabled(
		DebugOverlaySystem overlay,
		Collider collider,
		float duration,
		Color activeColor )
	{
		if ( overlay is null || !collider.IsValid() || collider.IsTrigger )
			return;

		var bounds = collider.GetWorldBounds();
		var dim = activeColor.WithAlpha( 0.35f );
		DrawWorldBounds( overlay, bounds, duration, dim );
	}

	public static void DrawWorldBounds(
		DebugOverlaySystem overlay,
		BBox bounds,
		float duration,
		Color color )
	{
		if ( overlay is null || bounds.Size.LengthSquared < 1e-8f )
			return;

		overlay.Box( bounds.Center, bounds.Size, color, duration );
	}

	public static void DrawHorizontalRing(
		DebugOverlaySystem overlay,
		Vector3 center,
		float radius,
		float duration,
		Color? color = null )
	{
		if ( overlay is null || radius <= 0.01f )
			return;

		var lineColor = color ?? Color.White;
		var step = MathF.PI * 2f / RingSegments;
		var prev = center + new Vector3( radius, 0f, 0f );

		for ( var i = 1; i <= RingSegments; i++ )
		{
			var angle = step * i;
			var next = center + new Vector3( MathF.Cos( angle ) * radius, MathF.Sin( angle ) * radius, 0f );
			overlay.Line( prev, next, lineColor, duration );
			prev = next;
		}
	}

	public static void DrawLegend( DebugOverlaySystem overlay, Vector3 anchor, float duration )
	{
		if ( overlay is null )
			return;

		overlay.Text( anchor, "Collision debug (H)", duration );
		var y = 16f;
		Category[] entries =
		[
			Category.ProbeHit,
			Category.Furniture,
			Category.BuildingWall,
			Category.BuildingFloor,
			Category.BuildingTrim,
			Category.BuildingRamp,
			Category.PlayerStructure,
			Category.Tree,
			Category.Player
		];

		for ( var i = 0; i < entries.Length; i++ )
		{
			var cat = entries[i];
			overlay.Text( anchor + Vector3.Up * y, $"{LabelFor( cat )}", duration );
			y += 14f;
		}
	}
}
