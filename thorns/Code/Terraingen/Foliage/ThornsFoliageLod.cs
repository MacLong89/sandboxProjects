namespace Terraingen.Foliage;

using Sandbox;

/// <summary>
/// Per-instance distance LOD: drop shadows then hide before chunk culling kicks in.
/// Harvest trees keep their <see cref="ThornsResourceNode"/> + colliders alive — only the mesh toggles.
/// </summary>
public static class ThornsFoliageLod
{
	public static void ApplyChunk( ThornsFoliageChunkData chunk, Vector3 observer, ThornsFoliageConfig config )
	{
		if ( !chunk.Root.IsValid() || !chunk.Root.Enabled )
			return;

		var shadowDistSq = config.TreeLodShadowDistanceInches * config.TreeLodShadowDistanceInches;
		var treeHideDistSq = config.TreeLodHideDistanceInches * config.TreeLodHideDistanceInches;
		var hysteresis = config.LodDistanceHysteresisInches;
		var treeShadowOuterSq = (config.TreeLodShadowDistanceInches + hysteresis) * (config.TreeLodShadowDistanceInches + hysteresis);
		var treeShadowInnerSq = MathF.Max( config.TreeLodShadowDistanceInches - hysteresis, 0f );
		treeShadowInnerSq *= treeShadowInnerSq;
		var treeHideOuterSq = (config.TreeLodHideDistanceInches + hysteresis) * (config.TreeLodHideDistanceInches + hysteresis);

		foreach ( var child in chunk.Root.Children )
		{
			if ( !child.IsValid() )
				continue;

			var tag = child.Components.Get<ThornsFoliageInstance>();
			if ( tag is null )
				continue;

			var distSq = (child.WorldPosition - observer).LengthSquared;
			ApplyTreeLod( child, tag, distSq, shadowDistSq, treeShadowOuterSq, treeShadowInnerSq, treeHideDistSq, treeHideOuterSq );
		}
	}

	static void ApplyTreeLod( GameObject obj, ThornsFoliageInstance tag, float distSq, float shadowSq, float shadowOuterSq, float shadowInnerSq, float hideSq, float hideOuterSq )
	{
		if ( IsHarvestFoliageTree( obj ) )
		{
			ApplyHarvestTreeLod( obj, tag, distSq, shadowSq, shadowOuterSq, shadowInnerSq, hideSq, hideOuterSq );
			return;
		}

		var state = tag.LodState;

		if ( state == 0 )
		{
			if ( distSq < hideSq )
				SetTreeNear( obj, tag, distSq < shadowSq );
			else
				SetTreeHidden( obj, tag );
			return;
		}

		if ( state == 2 )
		{
			if ( distSq > shadowOuterSq )
				SetTreeFar( obj, tag );
			if ( distSq > hideOuterSq )
				SetTreeHidden( obj, tag );
			return;
		}

		// state == 1 (far, no shadows)
		if ( distSq < shadowInnerSq )
			SetTreeNear( obj, tag, shadows: true );
		else if ( distSq > hideOuterSq )
			SetTreeHidden( obj, tag );
	}

	static bool IsHarvestFoliageTree( GameObject obj ) =>
		obj.Components.Get<ThornsResourceNode>( FindMode.EnabledInSelf ) is { IsValid: true, ResourceKind: ThornsResourceKind.Wood };

	static void ApplyHarvestTreeLod(
		GameObject obj,
		ThornsFoliageInstance tag,
		float distSq,
		float shadowSq,
		float shadowOuterSq,
		float shadowInnerSq,
		float hideSq,
		float hideOuterSq )
	{
		obj.Enabled = true;
		var node = obj.Components.Get<ThornsResourceNode>( FindMode.EnabledInSelf );
		var state = tag.LodState;

		if ( state == 0 )
		{
			if ( distSq < hideSq )
				SetHarvestTreeNear( tag, node, distSq < shadowSq );
			else
				SetHarvestTreeHidden( tag, node );
			return;
		}

		if ( state == 2 )
		{
			if ( distSq > shadowOuterSq )
				SetHarvestTreeFar( tag, node );
			if ( distSq > hideOuterSq )
				SetHarvestTreeHidden( tag, node );
			return;
		}

		if ( distSq < shadowInnerSq )
			SetHarvestTreeNear( tag, node, shadows: true );
		else if ( distSq > hideOuterSq )
			SetHarvestTreeHidden( tag, node );
	}

	static void SetHarvestTreeNear( ThornsFoliageInstance tag, ThornsResourceNode node, bool shadows )
	{
		tag.LodState = shadows ? (byte)2 : (byte)1;
		node?.ApplyFoliageLodVisual( visible: true, castShadows: shadows );
	}

	static void SetHarvestTreeFar( ThornsFoliageInstance tag, ThornsResourceNode node )
	{
		tag.LodState = 1;
		node?.ApplyFoliageLodVisual( visible: true, castShadows: false );
	}

	static void SetHarvestTreeHidden( ThornsFoliageInstance tag, ThornsResourceNode node )
	{
		if ( tag.LodState == 0 )
			return;

		tag.LodState = 0;
		node?.ApplyFoliageLodVisual( visible: false, castShadows: false );
	}

	static void SetTreeNear( GameObject obj, ThornsFoliageInstance tag, bool shadows )
	{
		var renderer = tag.Renderer;
		if ( renderer is null || !renderer.IsValid() )
		{
			tag.LodState = shadows ? (byte)2 : (byte)1;
			obj.Enabled = true;
			return;
		}

		var shadowMode = shadows ? ModelRenderer.ShadowRenderType.On : ModelRenderer.ShadowRenderType.Off;

		if ( tag.LodState == 2 && obj.Enabled && renderer.Enabled && renderer.RenderType == shadowMode )
			return;

		tag.LodState = 2;
		obj.Enabled = true;
		renderer.Enabled = true;
		renderer.RenderType = shadowMode;
	}

	static void SetTreeFar( GameObject obj, ThornsFoliageInstance tag )
	{
		var renderer = tag.Renderer;
		if ( renderer is null || !renderer.IsValid() )
		{
			tag.LodState = 1;
			obj.Enabled = true;
			return;
		}

		if ( tag.LodState == 1 && obj.Enabled && renderer.Enabled && renderer.RenderType == ModelRenderer.ShadowRenderType.Off )
			return;

		tag.LodState = 1;
		obj.Enabled = true;
		renderer.Enabled = true;
		renderer.RenderType = ModelRenderer.ShadowRenderType.Off;
	}

	static void SetTreeHidden( GameObject obj, ThornsFoliageInstance tag )
	{
		if ( tag.LodState == 0 && !obj.Enabled )
			return;

		tag.LodState = 0;
		obj.Enabled = false;
	}
}
