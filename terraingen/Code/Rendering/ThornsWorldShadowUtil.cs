namespace Terraingen.Rendering;

/// <summary>Small shared policy for world-space renderers that should participate in the sun shadow map.</summary>
public static class ThornsWorldShadowUtil
{
	public readonly struct SceneRepairStats
	{
		public SceneRepairStats( int scanned, int enabled, int skipped, int alreadyOn )
		{
			Scanned = scanned;
			Enabled = enabled;
			Skipped = skipped;
			AlreadyOn = alreadyOn;
		}

		public int Scanned { get; }
		public int Enabled { get; }
		public int Skipped { get; }
		public int AlreadyOn { get; }
	}

	public static void EnableWorldShadows( ModelRenderer renderer )
	{
		if ( renderer is null || !renderer.IsValid() )
			return;

		renderer.RenderType = ModelRenderer.ShadowRenderType.On;
	}

	public static void DisableWorldShadows( ModelRenderer renderer )
	{
		if ( renderer is null || !renderer.IsValid() )
			return;

		renderer.RenderType = ModelRenderer.ShadowRenderType.Off;
	}

	public static void EnableWorldShadowsOnHierarchy( GameObject root, FindMode findMode = FindMode.EverythingInSelfAndDescendants )
	{
		if ( root is null || !root.IsValid() )
			return;

		foreach ( var renderer in root.Components.GetAll<ModelRenderer>( findMode ) )
			EnableWorldShadows( renderer );
	}

	public static SceneRepairStats RepairSceneWorldShadows( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return default;

		var scanned = 0;
		var enabled = 0;
		var skipped = 0;
		var alreadyOn = 0;

		foreach ( var renderer in scene.GetAllComponents<ModelRenderer>() )
		{
			if ( renderer is null || !renderer.IsValid() || !renderer.Enabled )
				continue;

			scanned++;
			if ( ShouldStayShadowless( renderer.GameObject ) )
			{
				skipped++;
				continue;
			}

			if ( renderer.RenderType == ModelRenderer.ShadowRenderType.On )
			{
				alreadyOn++;
				continue;
			}

			if ( IsDistanceManagedShadowRoot( renderer.GameObject ) )
			{
				skipped++;
				continue;
			}

			EnableWorldShadows( renderer );
			enabled++;
		}

		return new SceneRepairStats( scanned, enabled, skipped, alreadyOn );
	}

	public static bool ShouldStayShadowless( GameObject go )
	{
		for ( var node = go; node is not null && node.IsValid(); node = node.Parent )
		{
			var name = node.Name ?? "";
			if ( name.Equals( "Thorns Water", StringComparison.OrdinalIgnoreCase )
			     || name.Equals( "Thorns Cloud Layer", StringComparison.OrdinalIgnoreCase )
			     || name.Equals( "Thorns Cloud Puffs", StringComparison.OrdinalIgnoreCase )
			     || name.StartsWith( "Cloud Puff", StringComparison.OrdinalIgnoreCase )
			     || name.Equals( "Clutter Grass", StringComparison.OrdinalIgnoreCase )
			     || name.Equals( "Thorns Build Ghost", StringComparison.OrdinalIgnoreCase )
			     || name.Equals( "WeaponViewmodel", StringComparison.OrdinalIgnoreCase )
			     || name.Equals( "FirstPersonArms", StringComparison.OrdinalIgnoreCase ) )
				return true;

			if ( IsDistanceManagedShadowRoot( node ) )
				return true;
		}

		return false;
	}

	static bool IsDistanceManagedShadowRoot( GameObject node )
	{
		var name = node.Name ?? "";
		if ( name.StartsWith( "ProcBuilding_", StringComparison.Ordinal )
		     || name.Equals( "Thorns Town Buildings", StringComparison.OrdinalIgnoreCase )
		     || name.Equals( "Thorns Boulder Field", StringComparison.OrdinalIgnoreCase )
		     || name.Equals( "Thorns Mineral Scatter", StringComparison.OrdinalIgnoreCase )
		     || name.Equals( "Thorns Minerals", StringComparison.OrdinalIgnoreCase ) )
			return true;

		return node.Tags.Has( "boulder" );
	}
}
