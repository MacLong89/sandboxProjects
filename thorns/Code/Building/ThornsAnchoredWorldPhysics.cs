namespace Sandbox;

/// <summary>
/// World-static collision for placeholder <c>models/dev/box.vmdl</c> props.
/// Terrain collision works because <see cref="ThornsTerrainGeometry"/> adds an explicit <see cref="ModelBuilder.AddCollisionMesh"/> —
/// the stock dev box model often has no usable collision shapes for <see cref="ModelCollider"/>, so we use <see cref="BoxCollider"/>
/// sized from <see cref="Model.PhysicsBounds"/> / <see cref="Model.Bounds"/> instead (same idea: explicit hull for traces).
/// <para>
/// For many shipped props, <see cref="Model.PhysicsBounds"/> is a conservative hull much larger than <see cref="Model.Bounds"/> (visible mesh).
/// When both are valid we pick whichever AABB has smaller volume; optional <c>hullExtentScale</c> can shrink further (e.g. trees).
/// </para>
/// </summary>
public static class ThornsAnchoredWorldPhysics
{
	static Model _cachedDevBoxModel;

	/// <summary>Shared <c>models/dev/box.vmdl</c> reference for placeholder harvest props / crates.</summary>
	public static Model DevBoxCollisionModel
	{
		get
		{
			if ( _cachedDevBoxModel.IsValid() )
				return _cachedDevBoxModel;
			var m = Model.Load( "models/dev/box.vmdl" );
			_cachedDevBoxModel = m;
			return m;
		}
	}

	/// <summary>Same as <see cref="EnsureAnchoredBoxPhysics"/> with the project dev box model.</summary>
	public static void EnsureAnchoredDevBoxPhysics( GameObject visualBody ) =>
		EnsureAnchoredBoxPhysics( visualBody, DevBoxCollisionModel );

	/// <summary>Same tags as the terrain chunk root (<see cref="ThornsTerrainSystem.TrySpawnChunk"/>) so collision rules / traces stay consistent.</summary>
	public static void EnsureWorldSolidTags( GameObject go ) =>
		ThornsCollisionTags.EnsureWorldSolidTriplet( go );

	/// <summary>
	/// Bounds for scaling placeable vmdls — prefers the smaller of render vs physics AABB (Tripo physics hulls are often huge).
	/// Falls back to a generic Tripo-ish box when both are empty.
	/// </summary>
	public static BBox ResolvePlaceableVisualBounds( Model model )
	{
		if ( !model.IsValid() || model.IsError )
			return PlaceableFallbackBounds;

		var bb = PickHullBBox( model );
		return bb.Size.LengthSquared > 1e-8f ? bb : PlaceableFallbackBounds;
	}

	static readonly BBox PlaceableFallbackBounds =
		new( new Vector3( -12f, -12f, 0f ), new Vector3( 12f, 12f, 32f ) );

	/// <summary>
	/// When both are valid, use whichever AABB has smaller volume (usually render bounds for trees where physics hull is conservative).
	/// </summary>
	static BBox PickHullBBox( Model collisionModel )
	{
		var rend = collisionModel.Bounds;
		var phys = collisionModel.PhysicsBounds;
		var rVol = rend.Volume;
		var pVol = phys.Volume;
		const float eps = 1e-6f;

		if ( pVol > eps && rVol > eps )
			return pVol < rVol ? phys : rend;

		if ( pVol > eps )
			return phys;
		if ( rVol > eps )
			return rend;
		return rend;
	}

	/// <summary>
	/// Uses <see cref="Model.Bounds"/> (visible mesh AABB in model space). Prefer this over <see cref="PickHullBBox"/> for irregular rocks where the
	/// engine physics hull is an unrelated tight cage (smaller volume but wrong shape vs art).
	/// </summary>
	static BBox PickRenderMeshBoundsOrFallback( Model collisionModel )
	{
		var rend = collisionModel.Bounds;
		if ( rend.Size.LengthSquared > 1e-12f )
			return rend;

		var phys = collisionModel.PhysicsBounds;
		if ( phys.Size.LengthSquared > 1e-12f )
			return phys;

		return rend;
	}

	/// <param name="hullExtentScale">Uniform scale on hull size after picking bounds (e.g. &lt; 1 for wood trunks where mesh AABB still pads the silhouette).</param>
	/// <param name="hullAxisMultiplier">Optional per-axis multiplier on that size (e.g. &lt; 1 on X/Y for a thinner trunk footprint; Z often left at 1).</param>
	public static void EnsureAnchoredBoxPhysics(
		GameObject visualBody,
		Model collisionModel,
		float hullExtentScale = 1f,
		Vector3? hullAxisMultiplier = null )
	{
		if ( !visualBody.IsValid() || !collisionModel.IsValid() )
			return;

		EnsureWorldSolidTags( visualBody );

		var rb = visualBody.Components.Get<Rigidbody>();
		if ( rb.IsValid() )
			rb.Destroy();

		var mc = visualBody.Components.Get<ModelCollider>();
		if ( mc.IsValid() )
			mc.Destroy();

		var hullBb = PickHullBBox( collisionModel );
		if ( hullBb.Size.LengthSquared < 1e-12f )
			hullBb = new BBox( new Vector3( -16f, -16f, -16f ), new Vector3( 16f, 16f, 16f ) );

		var scaled = hullBb.Size * hullExtentScale;
		var axis = hullAxisMultiplier ?? Vector3.One;
		var scale = new Vector3( scaled.x * axis.x, scaled.y * axis.y, scaled.z * axis.z );
		if ( scale.LengthSquared < 1e-12f )
			scale = new Vector3( 32f, 32f, 32f );

		var bc = visualBody.Components.GetOrCreate<BoxCollider>();
		bc.Center = hullBb.Center;
		bc.Scale = scale;
		bc.IsTrigger = false;
		bc.Static = true;
		bc.Enabled = true;
	}

	/// <summary>
	/// Narrow trunk column at the mesh base for scaled foliage2 trees. Full mesh AABB × <paramref name="worldUniformScale"/> on the
	/// <see cref="GameObject"/> would create a canopy-sized walk blocker; caller sets collision tags via
	/// <see cref="ThornsCollisionTags.EnsureWoodTreeTrunkSolidCollision"/>.
	/// </summary>
	public static void EnsureAnchoredWoodTrunkBoxPhysics(
		GameObject visualBody,
		Model collisionModel,
		float worldUniformScale,
		float trunkRadiusScale = 0.055f,
		float trunkHeightScale = 0.24f )
	{
		if ( !visualBody.IsValid() || !collisionModel.IsValid() )
			return;

		var rb = visualBody.Components.Get<Rigidbody>();
		if ( rb.IsValid() )
			rb.Destroy();

		var mc = visualBody.Components.Get<ModelCollider>();
		if ( mc.IsValid() )
			mc.Destroy();

		var uniform = Math.Max( worldUniformScale, 1f );
		var bb = PickRenderMeshBoundsOrFallback( collisionModel );
		if ( bb.Size.LengthSquared < 1e-12f )
			bb = new BBox( new Vector3( -8f, -8f, 0f ), new Vector3( 8f, 8f, 16f ) );

		var trunkRadiusWorld = Math.Clamp( uniform * trunkRadiusScale, 32f, 120f );
		var trunkHeightWorld = Math.Clamp( uniform * trunkHeightScale, 120f, 680f );
		var localDiameter = MathF.Max( (trunkRadiusWorld * 2f) / uniform, 0.12f );
		var localHeight = MathF.Max( trunkHeightWorld / uniform, 0.12f );
		var footX = (bb.Mins.x + bb.Maxs.x) * 0.5f;
		var footY = (bb.Mins.y + bb.Maxs.y) * 0.5f;
		var centerZ = bb.Mins.z + localHeight * 0.5f;

		var bc = visualBody.Components.GetOrCreate<BoxCollider>();
		bc.Center = new Vector3( footX, footY, centerZ );
		bc.Scale = new Vector3( localDiameter, localDiameter, localHeight );
		bc.IsTrigger = false;
		bc.Static = true;
		bc.Enabled = true;
	}

	/// <summary>
	/// Static box hull aligned to the <b>render mesh</b> bounds (plus optional shrink). Best-effort match for terrain boulders vs <see cref="EnsureAnchoredBoxPhysics"/>.
	/// </summary>
	public static void EnsureAnchoredBoxPhysicsMatchVisualMesh(
		GameObject visualBody,
		Model collisionModel,
		float hullExtentScale = 1f,
		Vector3? hullAxisMultiplier = null )
	{
		if ( !visualBody.IsValid() || !collisionModel.IsValid() )
			return;

		EnsureWorldSolidTags( visualBody );

		var rb = visualBody.Components.Get<Rigidbody>();
		if ( rb.IsValid() )
			rb.Destroy();

		var mc = visualBody.Components.Get<ModelCollider>();
		if ( mc.IsValid() )
			mc.Destroy();

		var hullBb = PickRenderMeshBoundsOrFallback( collisionModel );
		if ( hullBb.Size.LengthSquared < 1e-12f )
			hullBb = new BBox( new Vector3( -16f, -16f, -16f ), new Vector3( 16f, 16f, 16f ) );

		var scaled = hullBb.Size * hullExtentScale;
		var axis = hullAxisMultiplier ?? Vector3.One;
		var scale = new Vector3( scaled.x * axis.x, scaled.y * axis.y, scaled.z * axis.z );
		if ( scale.LengthSquared < 1e-12f )
			scale = new Vector3( 32f, 32f, 32f );

		var bc = visualBody.Components.GetOrCreate<BoxCollider>();
		bc.Center = hullBb.Center;
		bc.Scale = scale;
		bc.IsTrigger = false;
		bc.Static = true;
		bc.Enabled = true;
	}

	/// <summary>
	/// Fauna body hull for movement blocking + hit traces. Uses <see cref="ThornsCollisionTags.WildlifeHull"/> (not <c>solid</c>/<c>world</c>)
	/// so terrain-follow probes skip it, while <c>thorns_wildlife_hull</c> × <c>creature</c> collision rules block other fauna capsules.
	/// </summary>
	public static void EnsureWildlifeHullBoxPhysics(
		GameObject hullBody,
		Model collisionModel,
		float hullExtentScale = 1.12f,
		Vector3? hullAxisMultiplier = null ) =>
		EnsureWildlifeHullBoxPhysicsInternal( hullBody, collisionModel, useRenderBounds: false, hullExtentScale, hullAxisMultiplier );

	/// <inheritdoc cref="EnsureWildlifeHullBoxPhysics"/>
	public static void EnsureWildlifeHullBoxPhysicsMatchVisualMesh(
		GameObject hullBody,
		Model collisionModel,
		float hullExtentScale = 1.12f,
		Vector3? hullAxisMultiplier = null ) =>
		EnsureWildlifeHullBoxPhysicsInternal( hullBody, collisionModel, useRenderBounds: true, hullExtentScale, hullAxisMultiplier );

	static void EnsureWildlifeHullBoxPhysicsInternal(
		GameObject hullBody,
		Model collisionModel,
		bool useRenderBounds,
		float hullExtentScale,
		Vector3? hullAxisMultiplier )
	{
		if ( !hullBody.IsValid() || !collisionModel.IsValid() )
			return;

		ThornsCollisionTags.EnsureWildlifeHullTag( hullBody );

		var rb = hullBody.Components.Get<Rigidbody>();
		if ( rb.IsValid() )
			rb.Destroy();

		var mc = hullBody.Components.Get<ModelCollider>();
		if ( mc.IsValid() )
			mc.Destroy();

		var hullBb = useRenderBounds ? PickRenderMeshBoundsOrFallback( collisionModel ) : PickHullBBox( collisionModel );
		if ( hullBb.Size.LengthSquared < 1e-12f )
			hullBb = new BBox( new Vector3( -16f, -16f, -16f ), new Vector3( 16f, 16f, 16f ) );

		var scaled = hullBb.Size * hullExtentScale;
		var axis = hullAxisMultiplier ?? Vector3.One;
		var scale = new Vector3( scaled.x * axis.x, scaled.y * axis.y, scaled.z * axis.z );
		if ( scale.LengthSquared < 1e-12f )
			scale = new Vector3( 32f, 32f, 32f );

		var bc = hullBody.Components.GetOrCreate<BoxCollider>();
		bc.Center = hullBb.Center;
		bc.Scale = scale;
		bc.IsTrigger = false;
		bc.Static = true;
		bc.Enabled = true;
	}
}
