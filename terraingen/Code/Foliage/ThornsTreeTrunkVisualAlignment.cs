namespace Terraingen.Foliage;

using Terraingen.Physics;

/// <summary>Shifts tree mesh visuals so the trunk sits on the spawn-centered hitbox (corner-pivot models).</summary>
public static class ThornsTreeTrunkVisualAlignment
{
	public const string TreeVisualChildName = "TreeVisual";

	/// <summary>Extra mesh shift in model-local units (live tune after <c>tree_trunk_reset_defaults</c>).</summary>
	[ConVar( "tree_trunk_mesh_offset_x" )]
	public static float LiveMeshOffsetX { get; set; }

	[ConVar( "tree_trunk_mesh_offset_y" )]
	public static float LiveMeshOffsetY { get; set; }

	[ConVar( "tree_trunk_mesh_offset_z" )]
	public static float LiveMeshOffsetZ { get; set; }

	/// <summary>Aspen-only mesh nudge in world inches (parent-local = value / tree scale).</summary>
	[ConVar( "tree_trunk_aspen_mesh_offset_x" )]
	public static float AspenMeshOffsetXWorld { get; set; } = 6f;

	[ConVar( "tree_trunk_aspen_mesh_offset_y" )]
	public static float AspenMeshOffsetYWorld { get; set; }

	static int _lastTuningHash = int.MinValue;

	public static Vector3 ComputeModelLocalOffset( Model model, FoliageSpecies species, float uniformScale )
	{
		var bb = TerraingenAnchoredPhysics.GetTreeTrunkAnchorBounds( model );
		// XY only — ThornsFoliageSurface.ComputeGroundLift already embeds bounds.Mins.z into spawn height.
		var auto = new Vector3( -bb.Center.x, -bb.Center.y, 0f );
		auto += ResolveSpeciesMeshOffsetLocal( species, uniformScale );
		return auto + new Vector3( LiveMeshOffsetX, LiveMeshOffsetY, LiveMeshOffsetZ );
	}

	static Vector3 ResolveSpeciesMeshOffsetLocal( FoliageSpecies species, float uniformScale )
	{
		if ( species != FoliageSpecies.Aspen )
			return Vector3.Zero;

		uniformScale = MathF.Max( uniformScale, 0.01f );
		return new Vector3( AspenMeshOffsetXWorld, AspenMeshOffsetYWorld, 0f ) / uniformScale;
	}

	public static ModelRenderer Apply( GameObject instance, Model model, ThornsFoliageInstance tag = null )
	{
		if ( !instance.IsValid() || !model.IsValid() )
			return null;

		var uniform = MathF.Max( instance.WorldScale.x, 0.01f );
		var species = tag?.Species ?? InferSpecies( instance, model );
		var visual = EnsureVisualChild( instance );
		visual.LocalPosition = ComputeModelLocalOffset( model, species, uniform );
		visual.LocalRotation = Rotation.Identity;
		visual.LocalScale = Vector3.One;

		var rootRenderer = instance.Components.Get<ModelRenderer>();
		var visualRenderer = visual.Components.Get<ModelRenderer>() ?? visual.Components.Create<ModelRenderer>();

		if ( rootRenderer is { IsValid: true } && rootRenderer.GameObject == instance )
		{
			CopyRendererSettings( rootRenderer, visualRenderer, model );
			rootRenderer.Destroy();
		}
		else if ( !visualRenderer.Model.IsValid() )
		{
			visualRenderer.Model = model;
			visualRenderer.Enabled = true;
			visualRenderer.RenderType = ModelRenderer.ShadowRenderType.On;
		}

		if ( tag is not null )
			tag.Renderer = visualRenderer;

		return visualRenderer;
	}

	public static void RefreshMeshAlignmentInScene( Scene scene, bool force = false )
	{
		if ( scene is null || !scene.IsValid() )
			return;

		var hash = HashCode.Combine( LiveMeshOffsetX, LiveMeshOffsetY, LiveMeshOffsetZ, AspenMeshOffsetXWorld, AspenMeshOffsetYWorld );
		if ( !force && hash == _lastTuningHash )
			return;

		_lastTuningHash = hash;
		var refreshed = 0;

		foreach ( var tag in scene.GetAllComponents<ThornsFoliageInstance>() )
		{
			if ( !tag.IsValid() || !tag.GameObject.IsValid() )
				continue;

			var renderer = tag.Renderer;
			var model = renderer is { IsValid: true } ? renderer.Model : default;
			if ( !model.IsValid() )
				continue;

			if ( RefreshMeshPosition( tag.GameObject, model, tag ) )
				refreshed++;
		}

		if ( refreshed > 0 && force )
			Log.Info( $"[Thorns Foliage] Tree mesh alignment refreshed on {refreshed} tree(s) (colliders unchanged)." );
	}

	/// <summary>Legacy alias.</summary>
	public static void RefreshAllInScene( Scene scene, bool force = false ) =>
		RefreshMeshAlignmentInScene( scene, force );

	/// <summary>Move TreeVisual only — never recreates or moves colliders.</summary>
	public static bool RefreshMeshPosition( GameObject instance, Model model, ThornsFoliageInstance tag = null )
	{
		if ( !instance.IsValid() || !model.IsValid() )
			return false;

		var uniform = MathF.Max( instance.WorldScale.x, 0.01f );
		var species = tag?.Species ?? InferSpecies( instance, model );
		var offset = ComputeModelLocalOffset( model, species, uniform );

		foreach ( var child in instance.Children )
		{
			if ( !child.IsValid() || child.Name != TreeVisualChildName )
				continue;

			child.LocalPosition = offset;
			return true;
		}

		// First-time setup still needs the full visual child path.
		Apply( instance, model, tag );
		return true;
	}

	static FoliageSpecies InferSpecies( GameObject instance, Model model )
	{
		var name = instance.Name;
		if ( name.Contains( "Aspen", StringComparison.OrdinalIgnoreCase ) )
			return FoliageSpecies.Aspen;

		if ( name.Contains( "Oak", StringComparison.OrdinalIgnoreCase ) )
			return FoliageSpecies.Oak;

		return FoliageSpecies.Pine;
	}

	public static void InvalidateRefreshCache() => _lastTuningHash = int.MinValue;

	public static void ResetLiveOffsets()
	{
		LiveMeshOffsetX = 0f;
		LiveMeshOffsetY = 0f;
		LiveMeshOffsetZ = 0f;
		AspenMeshOffsetXWorld = 6f;
		AspenMeshOffsetYWorld = 0f;
		InvalidateRefreshCache();
	}

	static GameObject EnsureVisualChild( GameObject instance )
	{
		foreach ( var child in instance.Children )
		{
			if ( child.IsValid() && child.Name == TreeVisualChildName )
				return child;
		}

		var created = instance.Scene.CreateObject( true );
		created.Name = TreeVisualChildName;
		created.Parent = instance;
		return created;
	}

	static void CopyRendererSettings( ModelRenderer from, ModelRenderer to, Model model )
	{
		to.Model = from.Model.IsValid() ? from.Model : model;
		to.Enabled = from.Enabled;
		to.RenderType = from.RenderType;
		to.Tint = from.Tint;
	}
}
