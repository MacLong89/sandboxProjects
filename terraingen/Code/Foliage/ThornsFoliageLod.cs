namespace Terraingen.Foliage;

using Terraingen.Physics;

/// <summary>
/// Per-instance distance LOD: mesh shadows, PNG billboards, then hide before chunk culling.
/// Trunk colliders are never toggled here — walk/chop/hitbox blocking stays on until the tree is destroyed.
/// </summary>
public static class ThornsFoliageLod
{
	public static void ApplyChunk( ThornsFoliageChunkData chunk, Vector3 observer, ThornsFoliageConfig config )
	{
		if ( !chunk.Root.IsValid() || !chunk.Root.Enabled )
			return;

		// Avoid distance LOD before a valid player/observer exists (spawn bootstrap).
		if ( observer.LengthSquared < 1f )
			return;

		if ( config.UseTreeBillboardLod )
			ThornsTreeBillboardAssets.Configure( config );

		var shadowDist = config.TreeLodShadowDistanceInches;
		var billboardDist = config.TreeLodBillboardDistanceInches;
		var hideDist = config.TreeLodHideDistanceInches;
		var hysteresis = config.LodDistanceHysteresisInches;

		var shadowSq = shadowDist * shadowDist;
		var billboardSq = billboardDist * billboardDist;
		var hideSq = hideDist * hideDist;
		var shadowInnerSq = Sq( MathF.Max( shadowDist - hysteresis, 0f ) );
		var shadowOuterSq = Sq( shadowDist + hysteresis );
		var billboardInnerSq = Sq( MathF.Max( billboardDist - hysteresis, 0f ) );
		var billboardOuterSq = Sq( billboardDist + hysteresis );
		var hideOuterSq = Sq( hideDist + hysteresis );

		if ( chunk.LodInstances is { Count: > 0 } instances )
		{
			for ( var i = 0; i < instances.Count; i++ )
			{
				var tag = instances[i];
				if ( tag is null || !tag.IsValid() || !tag.GameObject.IsValid() )
					continue;

				ApplyTreeInstanceLod(
					tag.GameObject,
					tag,
					observer,
					config,
					shadowInnerSq,
					shadowOuterSq,
					billboardInnerSq,
					billboardOuterSq,
					hideSq,
					hideOuterSq,
					shadowSq );
			}

			return;
		}

		foreach ( var child in chunk.Root.Children )
		{
			if ( !child.IsValid() )
				continue;

			var tag = child.Components.Get<ThornsFoliageInstance>();
			if ( tag is null )
				continue;

			ApplyTreeInstanceLod( child, tag, observer, config, shadowInnerSq, shadowOuterSq, billboardInnerSq, billboardOuterSq, hideSq, hideOuterSq, shadowSq );
		}
	}

	static void ApplyTreeInstanceLod(
		GameObject child,
		ThornsFoliageInstance tag,
		Vector3 observer,
		ThornsFoliageConfig config,
		float shadowInnerSq,
		float shadowOuterSq,
		float billboardInnerSq,
		float billboardOuterSq,
		float hideSq,
		float hideOuterSq,
		float shadowSq )
	{
		var distSq = (child.WorldPosition - observer).LengthSquared;
		var planarDistSq = PlanarDistSq( child.WorldPosition, observer );
		if ( config.UseTreeBillboardLod )
		{
			ApplyTreeLodWithBillboards(
				child,
				tag,
				distSq,
				planarDistSq,
				config,
				shadowInnerSq,
				shadowOuterSq,
				billboardInnerSq,
				billboardOuterSq,
				hideSq,
				hideOuterSq );
		}
		else
		{
			ApplyTreeLodMeshOnly(
				child,
				tag,
				distSq,
				planarDistSq,
				shadowSq,
				shadowOuterSq,
				shadowInnerSq,
				hideSq,
				hideOuterSq,
				config );
		}

		UpdateBillboardFacing( tag, observer );
	}

	static void UpdateBillboardFacing( ThornsFoliageInstance tag, Vector3 observer )
	{
		var billboard = tag.BillboardRenderer;
		if ( billboard is not { IsValid: true, Enabled: true } )
			return;

		var billboardGo = billboard.GameObject;
		if ( !billboardGo.IsValid() )
			return;

		billboardGo.WorldRotation = ThornsTreeBillboardAssets.FaceCameraRotation( billboardGo.WorldPosition, observer );
	}

	static void ApplyTreeLodWithBillboards(
		GameObject obj,
		ThornsFoliageInstance tag,
		float distSq,
		float planarDistSq,
		ThornsFoliageConfig config,
		float shadowInnerSq,
		float shadowOuterSq,
		float billboardInnerSq,
		float billboardOuterSq,
		float hideSq,
		float hideOuterSq )
	{
		switch ( tag.LodState )
		{
			case 0:
				if ( distSq >= hideOuterSq )
					return;

				if ( distSq >= billboardOuterSq )
					SetTreeBillboard( obj, tag, planarDistSq, config );
				else if ( distSq >= shadowOuterSq )
					SetTreeMeshFar( obj, tag, planarDistSq, config );
				else
					SetTreeMeshNear( obj, tag, shadows: true, planarDistSq, config );
				return;

			case 1:
				if ( distSq >= hideOuterSq )
					SetTreeHidden( obj, tag, config );
				else if ( distSq < billboardInnerSq )
					SetTreeMeshFar( obj, tag, planarDistSq, config );
				return;

			case 2:
				if ( distSq >= hideOuterSq )
					SetTreeHidden( obj, tag, config );
				else if ( distSq >= billboardOuterSq )
					SetTreeBillboard( obj, tag, planarDistSq, config );
				else if ( distSq < shadowInnerSq )
					SetTreeMeshNear( obj, tag, shadows: true, planarDistSq, config );
				return;

			case 3:
				if ( distSq >= hideOuterSq )
					SetTreeHidden( obj, tag, config );
				else if ( distSq >= billboardOuterSq )
					SetTreeBillboard( obj, tag, planarDistSq, config );
				else if ( distSq >= shadowOuterSq )
					SetTreeMeshFar( obj, tag, planarDistSq, config );
				return;
		}
	}

	static void ApplyTreeLodMeshOnly(
		GameObject obj,
		ThornsFoliageInstance tag,
		float distSq,
		float planarDistSq,
		float shadowSq,
		float shadowOuterSq,
		float shadowInnerSq,
		float hideSq,
		float hideOuterSq,
		ThornsFoliageConfig config )
	{
		switch ( tag.LodState )
		{
			case 0:
				if ( distSq < hideSq )
					SetTreeMeshNear( obj, tag, distSq < shadowSq, planarDistSq, config );
				else
					SetTreeHidden( obj, tag, config );
				return;

			case 3:
				if ( distSq > shadowOuterSq )
					SetTreeMeshFar( obj, tag, planarDistSq, config );
				if ( distSq > hideOuterSq )
					SetTreeHidden( obj, tag, config );
				return;

			case 2:
				if ( distSq < shadowInnerSq )
					SetTreeMeshNear( obj, tag, shadows: true, planarDistSq, config );
				else if ( distSq > hideOuterSq )
					SetTreeHidden( obj, tag, config );
				return;

			case 1:
				if ( distSq < shadowInnerSq )
					SetTreeMeshNear( obj, tag, shadows: true, planarDistSq, config );
				else if ( distSq > hideOuterSq )
					SetTreeHidden( obj, tag, config );
				return;
		}
	}

	static void SetTreeMeshNear(
		GameObject obj,
		ThornsFoliageInstance tag,
		bool shadows,
		float planarDistSq,
		ThornsFoliageConfig config )
	{
		var renderer = ResolveMeshRenderer( tag );
		var shadowMode = shadows ? ModelRenderer.ShadowRenderType.On : ModelRenderer.ShadowRenderType.Off;
		var targetState = (byte)(shadows ? 3 : 2);

		if ( tag.LodState == targetState
		     && obj.Enabled
		     && renderer is { IsValid: true, Enabled: true, RenderType: var mode }
		     && mode == shadowMode
		     && tag.BillboardRenderer is not { Enabled: true } )
			return;

		tag.LodState = targetState;
		obj.Enabled = true;
		SetMeshVisible( tag, shadowMode );
		SetBillboardVisible( tag, false );
	}

	static void SetTreeBillboard( GameObject obj, ThornsFoliageInstance tag, float planarDistSq, ThornsFoliageConfig config )
	{
		ThornsTreeBillboardAssets.EnsureBillboardChild( obj, tag, config );
		if ( tag.BillboardRenderer is not { IsValid: true } )
		{
			SetTreeMeshFar( obj, tag, planarDistSq, config );
			return;
		}

		if ( tag.LodState == 1
		     && obj.Enabled
		     && tag.Renderer is { IsValid: true, Enabled: false }
		     && tag.BillboardRenderer is { IsValid: true, Enabled: true } )
			return;

		tag.LodState = 1;
		obj.Enabled = true;
		SetMeshVisible( tag, ModelRenderer.ShadowRenderType.Off, enabled: false );
		SetBillboardVisible( tag, true );
	}

	static void SetTreeHidden( GameObject obj, ThornsFoliageInstance tag, ThornsFoliageConfig config )
	{
		if ( tag.LodState == 0
		     && ResolveMeshRenderer( tag ) is { Enabled: false }
		     && tag.BillboardRenderer is not { Enabled: true } )
			return;

		tag.LodState = 0;
		obj.Enabled = true;
		SetMeshVisible( tag, ModelRenderer.ShadowRenderType.Off, enabled: false );
		SetBillboardVisible( tag, false );
	}

	static void SetTreeMeshFar( GameObject obj, ThornsFoliageInstance tag, float planarDistSq, ThornsFoliageConfig config ) =>
		SetTreeMeshNear( obj, tag, shadows: false, planarDistSq, config );

	static Collider ResolveColliderRef( ThornsFoliageInstance tag )
	{
		if ( tag.Collider is { IsValid: true } existing )
			return existing;

		if ( !tag.GameObject.IsValid() )
			return null;

		tag.Collider = TerraingenAnchoredPhysics.FindTreeTrunkCollider( tag.GameObject );
		return tag.Collider is { IsValid: true } resolved ? resolved : null;
	}

	static ModelRenderer ResolveMeshRenderer( ThornsFoliageInstance tag )
	{
		if ( tag.Renderer is { IsValid: true } cached )
			return cached;

		if ( !tag.GameObject.IsValid() )
			return null;

		foreach ( var child in tag.GameObject.Children )
		{
			if ( !child.IsValid() || child.Name != ThornsTreeTrunkVisualAlignment.TreeVisualChildName )
				continue;

			var visualRenderer = child.Components.Get<ModelRenderer>();
			if ( visualRenderer is not { IsValid: true } )
				continue;

			tag.Renderer = visualRenderer;
			return visualRenderer;
		}

		return null;
	}

	/// <summary>One-shot after populate — rebuild legacy child hulls and any missing boxes.</summary>
	public static void MigrateLegacyTrunkColliders( IReadOnlyList<ThornsFoliageChunkData> chunks )
	{
		if ( chunks is null || chunks.Count == 0 )
			return;

		foreach ( var chunk in chunks )
		{
			if ( !chunk.Root.IsValid() )
				continue;

			foreach ( var child in chunk.Root.Children )
			{
				if ( !child.IsValid() )
					continue;

				var tag = child.Components.Get<ThornsFoliageInstance>();
				if ( tag is null )
					continue;

				EnsureTrunkColliderExists( child, tag );
			}
		}
	}

	/// <summary>
	/// Enable trunk physics only near the observer (matches instanced-tree harvest proxies).
	/// Distant colliders stay off so hitscan traces are not blocked by thousands of static boxes.
	/// </summary>
	public static void SyncTrunkColliderProximity(
		IReadOnlyList<ThornsFoliageChunkData> chunks,
		Vector3 observer,
		float rangeInches )
	{
		if ( chunks is null || chunks.Count == 0 || rangeInches <= 0f || observer.LengthSquared < 1f )
			return;

		var rangeSq = rangeInches * rangeInches;

		foreach ( var chunk in chunks )
		{
			if ( !chunk.Root.IsValid() || !chunk.Root.Enabled )
				continue;

			if ( chunk.LodInstances is { Count: > 0 } instances )
			{
				for ( var i = 0; i < instances.Count; i++ )
				{
					var tag = instances[i];
					if ( tag is null || !tag.IsValid() || !tag.GameObject.IsValid() )
						continue;

					SyncTrunkColliderForInstance( tag.GameObject, tag, observer, rangeSq );
				}

				continue;
			}

			foreach ( var child in chunk.Root.Children )
			{
				if ( !child.IsValid() )
					continue;

				var tag = child.Components.Get<ThornsFoliageInstance>();
				if ( tag is null )
					continue;

				SyncTrunkColliderForInstance( child, tag, observer, rangeSq );
			}
		}
	}

	static void SyncTrunkColliderForInstance(
		GameObject tree,
		ThornsFoliageInstance tag,
		Vector3 observer,
		float rangeSq )
	{
		var collider = ResolveColliderRef( tag );
		if ( collider is not { IsValid: true } )
		{
			EnsureTrunkColliderExists( tree, tag );
			collider = ResolveColliderRef( tag );
		}

		if ( collider is not { IsValid: true } )
			return;

		var enable = PlanarDistSq( tree.WorldPosition, observer ) <= rangeSq;
		if ( collider.Enabled != enable )
			collider.Enabled = enable;
	}

	static void EnsureTrunkColliderExists( GameObject tree, ThornsFoliageInstance tag )
	{
		var collider = ResolveColliderRef( tag );
		if ( IsLegacyTrunkColliderChild( tree, collider ) )
		{
			var marker = tree.Components.Get<ThornsTreeTrunkColliderMarker>();
			if ( marker is { IsValid: true } && marker.CollisionModel.IsValid() )
			{
				var uniform = MathF.Max( marker.UniformScale, 0.01f );
				ThornsTreeTrunkCollision.ApplyCollision( tree, marker.CollisionModel, uniform );
				return;
			}
		}

		if ( collider is { IsValid: true } )
			return;

		var rebuildMarker = tree.Components.Get<ThornsTreeTrunkColliderMarker>();
		if ( rebuildMarker is not { IsValid: true } || !rebuildMarker.CollisionModel.IsValid() )
			return;

		var rebuildUniform = MathF.Max( rebuildMarker.UniformScale, 0.01f );
		ThornsTreeTrunkCollision.ApplyCollision( tree, rebuildMarker.CollisionModel, rebuildUniform );
	}

	static bool IsLegacyTrunkColliderChild( GameObject tree, Collider collider ) =>
		collider is { IsValid: true }
		&& collider.GameObject.IsValid()
		&& collider.GameObject != tree
		&& collider.GameObject.Name == "TreeTrunkCollider";

	/// <summary>Re-enable trunk colliders on a chunk after culling toggles the root back on.</summary>
	public static void RestoreChunkColliders( ThornsFoliageChunkData chunk, Vector3 observer, float rangeInches )
	{
		if ( !chunk.Root.IsValid() || !chunk.Root.Enabled || rangeInches <= 0f || observer.LengthSquared < 1f )
			return;

		SyncTrunkColliderProximity( new[] { chunk }, observer, rangeInches );
	}

	static float PlanarDistSq( Vector3 treeWorld, Vector3 observer ) =>
		(treeWorld.WithZ( 0f ) - observer.WithZ( 0f )).LengthSquared;

	static void SetMeshVisible( ThornsFoliageInstance tag, ModelRenderer.ShadowRenderType shadowMode, bool? enabled = true )
	{
		var renderer = ResolveMeshRenderer( tag );
		if ( renderer is null || !renderer.IsValid() )
			return;

		if ( enabled.HasValue )
			renderer.Enabled = enabled.Value;

		if ( renderer.Enabled )
			renderer.RenderType = shadowMode;
	}

	static void SetBillboardVisible( ThornsFoliageInstance tag, bool visible )
	{
		var renderer = tag.BillboardRenderer;
		if ( renderer is null || !renderer.IsValid() )
			return;

		renderer.Enabled = visible;
	}

	static float Sq( float value ) => value * value;
}
