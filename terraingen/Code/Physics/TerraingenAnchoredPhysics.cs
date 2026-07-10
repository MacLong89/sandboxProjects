using Sandbox;

namespace Terraingen.Physics;

/// <summary>World-space trunk collision column dimensions for a tree instance.</summary>
public readonly record struct TreeTrunkColliderSizing( float RadiusWorld, float HeightWorld );

/// <summary>
/// Static world collision for scattered foliage and mineral props (box hulls sized from model bounds × local scale).
/// </summary>
public static class TerraingenAnchoredPhysics
{
	static bool _loggedTreeTrunkSample;

	public static void ResetTreeTrunkCollisionDebugLog() => _loggedTreeTrunkSample = false;

	public static void EnsureSolidTags( GameObject go )
	{
		if ( !go.IsValid() )
			return;

		AddTagIfMissing( go, "solid" );
		AddTagIfMissing( go, "world" );
	}

	const string TreeTrunkColliderChildName = "TreeTrunkCollider";

	/// <summary>Narrow trunk column at the spawn pivot (walk + chop; lower trunk only for gunplay clearance).</summary>
	public static Collider ApplyTreeTrunkBox(
		GameObject body,
		Model collisionProfileModel,
		float localUniformScale,
		TreeTrunkColliderSizing sizing )
	{
		if ( !body.IsValid() || !collisionProfileModel.IsValid() )
			return null;

		EnsureSolidTags( body );
		DestroyTreeTrunkColliders( body );

		var uniform = Math.Max( localUniformScale, 0.01f );
		var anchorBb = TerraingenAnchoredPhysics.GetTreeTrunkAnchorBounds( collisionProfileModel );
		var trunkRadiusWorld = MathF.Max( sizing.RadiusWorld, 1f );
		var trunkHeightWorld = MathF.Max( sizing.HeightWorld, 1f );
		var worldDiameter = trunkRadiusWorld * 2f;
		var localDiameter = worldDiameter / uniform;
		var localHeight = trunkHeightWorld / uniform;
		var localHalfHeight = localHeight * 0.5f;
		var centerZ = anchorBb.Mins.z + localHalfHeight;

		if ( !_loggedTreeTrunkSample )
			_loggedTreeTrunkSample = true;

		// Root collider in model-local units (same pattern as boulder spheres) — parent scale → world inches.
		var bc = body.Components.Create<BoxCollider>();
		bc.Center = new Vector3( 0f, 0f, centerZ );
		bc.Scale = new Vector3( localDiameter, localDiameter, localHeight );
		bc.IsTrigger = false;
		bc.Static = true;
		bc.Enabled = true;
		return bc;
	}

	/// <summary>Trunk collider on the tree root (preferred) or legacy child box.</summary>
	public static Collider FindTreeTrunkCollider( GameObject body )
	{
		if ( !body.IsValid() )
			return null;

		var rootBox = body.Components.Get<BoxCollider>();
		if ( rootBox is { IsValid: true } )
			return rootBox;

		foreach ( var child in body.Children )
		{
			if ( !child.IsValid() || child.Name != TreeTrunkColliderChildName )
				continue;

			var childBox = child.Components.Get<BoxCollider>();
			if ( childBox is { IsValid: true } )
				return childBox;
		}

		var sphere = body.Components.Get<SphereCollider>();
		if ( sphere is { IsValid: true } )
			return sphere;

		var capsule = body.Components.Get<CapsuleCollider>();
		return capsule is { IsValid: true } ? capsule : null;
	}

	static void DestroyTreeTrunkColliders( GameObject body )
	{
		DestroyDynamicPhysics( body );

		foreach ( var child in body.Children.ToArray() )
		{
			if ( child.IsValid() && child.Name == TreeTrunkColliderChildName )
				child.Destroy();
		}

		foreach ( var box in body.Components.GetAll<BoxCollider>( FindMode.EverythingInSelf ) )
			box.Destroy();

		foreach ( var sphere in body.Components.GetAll<SphereCollider>( FindMode.EverythingInSelf ) )
			sphere.Destroy();

		foreach ( var capsule in body.Components.GetAll<CapsuleCollider>( FindMode.EverythingInSelf ) )
			capsule.Destroy();
	}

	/// <summary>Static prop/building hull — axis-aligned box from model bounds (avoids mesh-collider jitter).</summary>
	public static void EnsureStaticModelBoxCollider(
		GameObject body,
		Model model,
		float hullExtentScale = 1f ) =>
		EnsureVisualMeshBox( body, model, hullExtentScale );

	/// <summary>
	/// Placeable / proc-interior furniture — render AABB box (imported models often lack physics hulls for traces).
	/// </summary>
	public static void EnsureFurnitureCollider( GameObject body, Model model ) =>
		EnsureStaticModelBoxCollider( body, model, hullExtentScale: 1f );

	/// <summary>Mesh-accurate static hull — matches rendered geometry (proc buildings, furniture).</summary>
	public static void EnsureStaticModelMeshCollider( GameObject body, Model model )
	{
		if ( !body.IsValid() || !model.IsValid() )
			return;

		EnsureSolidTags( body );

		var box = body.Components.Get<BoxCollider>();
		if ( box.IsValid() )
			box.Destroy();

		var rb = body.Components.Get<Rigidbody>();
		if ( rb.IsValid() )
			rb.Destroy();

		var mc = body.Components.GetOrCreate<ModelCollider>();
		mc.Model = model;
		mc.IsTrigger = false;
		mc.Static = true;
		mc.Enabled = true;
	}

	/// <summary>Rock scatter / ore — hull follows visible mesh bounds.</summary>
	public static void EnsureVisualMeshBox(
		GameObject body,
		Model model,
		float hullExtentScale = 0.72f )
	{
		if ( !body.IsValid() || !model.IsValid() )
			return;

		EnsureSolidTags( body );
		DestroyDynamicPhysics( body );

		var hullBb = RenderBoundsOrFallback( model );
		var scale = hullBb.Size * Math.Max( hullExtentScale, 0.05f );
		if ( scale.LengthSquared < 1e-12f )
			scale = new Vector3( 32f, 32f, 32f );

		var bc = body.Components.GetOrCreate<BoxCollider>();
		bc.Center = hullBb.Center;
		bc.Scale = scale;
		bc.IsTrigger = false;
		bc.Static = true;
		bc.Enabled = true;
	}

	static void DestroyDynamicPhysics( GameObject body )
	{
		var rb = body.Components.Get<Rigidbody>();
		if ( rb.IsValid() )
			rb.Destroy();

		var mc = body.Components.Get<ModelCollider>();
		if ( mc.IsValid() )
			mc.Destroy();
	}

	/// <summary>Visible mesh AABB, or physics bounds when render bounds are empty.</summary>
	public static BBox GetModelRenderBounds( Model model ) => RenderBoundsOrFallback( model );

	/// <summary>Smallest non-empty bounds volume on the model (closest to visible silhouette).</summary>
	public static BBox GetTightModelBounds( Model model )
	{
		if ( !model.IsValid() )
			return new BBox( new Vector3( -8f, -8f, 0f ), new Vector3( 8f, 8f, 16f ) );

		if ( model.RenderBounds.Size.LengthSquared > 1e-12f )
			return model.RenderBounds;

		BBox best = default;
		var bestVolume = float.MaxValue;
		var any = false;

		TryCandidate( model.Bounds );
		TryCandidate( model.PhysicsBounds );

		if ( any )
			return best;

		return new BBox( new Vector3( -8f, -8f, 0f ), new Vector3( 8f, 8f, 16f ) );

		void TryCandidate( BBox box )
		{
			var size = box.Size;
			var vol = size.x * size.y * size.z;
			if ( vol < 1e-12f )
				return;

			if ( !any || vol < bestVolume )
			{
				any = true;
				bestVolume = vol;
				best = box;
			}
		}
	}

	public static BBox GetTreeTrunkAnchorBounds( Model model )
	{
		if ( !model.IsValid() )
			return new BBox( new Vector3( -8f, -8f, 0f ), new Vector3( 8f, 8f, 16f ) );

		if ( model.RenderBounds.Size.LengthSquared > 1e-12f )
			return model.RenderBounds;

		if ( model.Bounds.Size.LengthSquared > 1e-12f )
			return model.Bounds;

		return RenderBoundsOrFallback( model );
	}

	static BBox RenderBoundsOrFallback( Model model )
	{
		var tight = GetTightModelBounds( model );
		if ( tight.Size.LengthSquared > 1e-12f )
			return tight;

		return new BBox( new Vector3( -8f, -8f, 0f ), new Vector3( 8f, 8f, 16f ) );
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
}
