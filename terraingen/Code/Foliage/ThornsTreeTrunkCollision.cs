namespace Terraingen.Foliage;

using Terraingen.Physics;

/// <summary>Live-tunable tree trunk walk/chop collision (console + inspector while in-world).</summary>
public static class ThornsTreeTrunkCollision
{
	const float DefaultWidthRatio = 0.30f;
	const float DefaultAreaScale = 0.25f;
	const float DefaultMaxDerivedRadiusInches = 34f;
	const float DefaultMaxRadiusInches = 20f;
	const float DefaultMinRadiusInches = 7f;
	const float DefaultMaxHeightInches = 96f;
	const float DefaultMinHeightInches = 48f;
	const float DefaultHeightFraction = 0.24f;

	/// <summary>Multiply final trunk radius — <c>tree_trunk_radius_scale 0.5</c></summary>
	[ConVar( "tree_trunk_radius_scale" )]
	public static float LiveRadiusScale { get; set; } = 1f;

	/// <summary>Multiply final trunk height — <c>tree_trunk_height_scale 0.8</c></summary>
	[ConVar( "tree_trunk_height_scale" )]
	public static float LiveHeightScale { get; set; } = 1f;

	/// <summary>Footprint area scale vs model-derived width (0.25 = 2× prior 0.125 default area).</summary>
	[ConVar( "tree_trunk_area_scale" )]
	public static float LiveAreaScale { get; set; } = DefaultAreaScale;

	/// <summary>Hard cap on trunk radius in world inches after all scaling.</summary>
	[ConVar( "tree_trunk_max_radius" )]
	public static float LiveMaxRadiusInches { get; set; } = DefaultMaxRadiusInches;

	/// <summary>Floor on trunk radius in world inches after all scaling.</summary>
	[ConVar( "tree_trunk_min_radius" )]
	public static float LiveMinRadiusInches { get; set; } = DefaultMinRadiusInches;

	/// <summary>Hard cap on trunk collision column height in world inches.</summary>
	[ConVar( "tree_trunk_max_height" )]
	public static float LiveMaxHeightInches { get; set; } = DefaultMaxHeightInches;

	static int _lastTuningHash = int.MinValue;

	public static float Apply( GameObject body, Model model, float uniformScale )
	{
		var radius = ApplyCollision( body, model, uniformScale );
		var foliageTag = body.Components.Get<ThornsFoliageInstance>();
		ThornsTreeTrunkVisualAlignment.Apply( body, model, foliageTag );
		return radius;
	}

	/// <summary>Collider only — never shifts mesh alignment (use for live collision tuning / rebuild).</summary>
	public static float ApplyCollision( GameObject body, Model model, float uniformScale )
	{
		if ( !body.IsValid() || !model.IsValid() )
			return 0f;

		var sizing = ResolveSizing( model, uniformScale );
		var collider = TerraingenAnchoredPhysics.ApplyTreeTrunkBox( body, model, uniformScale, sizing );

		var marker = body.Components.Get<ThornsTreeTrunkColliderMarker>()
		             ?? body.Components.Create<ThornsTreeTrunkColliderMarker>();
		marker.CollisionModel = model;
		marker.UniformScale = MathF.Max( uniformScale, 0.01f );
		marker.WorldRadius = sizing.RadiusWorld;
		marker.WorldHeight = sizing.HeightWorld;
		marker.ColliderRef = collider;

		var foliageTag = body.Components.Get<ThornsFoliageInstance>();
		if ( foliageTag is not null )
			foliageTag.Collider = collider;

		if ( !body.Tags.Contains( "tree" ) )
			body.Tags.Add( "tree" );

		if ( collider is { IsValid: true } )
			collider.Enabled = true;

		return sizing.RadiusWorld;
	}

	public static TreeTrunkColliderSizing ResolveSizing( Model model, float uniformScale )
	{
		uniformScale = MathF.Max( uniformScale, 0.01f );
		var bb = TerraingenAnchoredPhysics.GetTightModelBounds( model );
		if ( bb.Size.LengthSquared < 1e-12f )
			bb = new BBox( new Vector3( -8f, -8f, 0f ), new Vector3( 8f, 8f, 16f ) );

		var worldHeight = MathF.Max( bb.Size.z * uniformScale, 1f );
		var worldWidth = MathF.Max( MathF.Max( bb.Size.x, bb.Size.y ) * uniformScale, 1f );

		var areaScale = Math.Clamp( LiveAreaScale, 0.02f, 1f );
		var radiusScale = Math.Clamp( LiveRadiusScale, 0.05f, 4f );
		var heightScale = Math.Clamp( LiveHeightScale, 0.05f, 4f );
		var minRadius = Math.Clamp( LiveMinRadiusInches, 4f, 96f );
		var maxRadius = Math.Clamp( LiveMaxRadiusInches, minRadius, 120f );
		var minHeight = Math.Clamp( DefaultMinHeightInches, 48f, 2880f );
		var maxHeight = Math.Clamp( LiveMaxHeightInches, minHeight, 2880f );

		var derivedRadius = Math.Clamp( worldWidth * DefaultWidthRatio, 0f, DefaultMaxDerivedRadiusInches );
		derivedRadius *= MathF.Sqrt( areaScale );
		derivedRadius *= radiusScale;
		var trunkRadiusWorld = Math.Clamp( derivedRadius, minRadius, maxRadius );

		var trunkHeightWorld = Math.Clamp( worldHeight * DefaultHeightFraction, minHeight, maxHeight );
		trunkHeightWorld *= heightScale;
		trunkHeightWorld = Math.Clamp( trunkHeightWorld, minHeight, maxHeight );

		return new TreeTrunkColliderSizing( trunkRadiusWorld, trunkHeightWorld );
	}

	/// <summary>Rebuild trunk boxes when collision ConVars change — does not touch mesh alignment.</summary>
	public static void RefreshCollisionInScene( Scene scene, bool logWhenChanged = false, bool force = false )
	{
		if ( scene is null || !scene.IsValid() )
			return;

		var hash = ComputeTuningHash();
		if ( !force && hash == _lastTuningHash )
			return;

		_lastTuningHash = hash;
		var refreshed = 0;
		var sampleRadius = 0f;
		var sampleHeight = 0f;

		foreach ( var marker in scene.GetAllComponents<ThornsTreeTrunkColliderMarker>() )
		{
			if ( !marker.IsValid() || !marker.GameObject.IsValid() || !marker.CollisionModel.IsValid() )
				continue;

			sampleRadius = marker.RebuildCollisionIfReady();
			sampleHeight = marker.WorldHeight;
			refreshed++;
		}

		foreach ( var tag in scene.GetAllComponents<ThornsFoliageInstance>() )
		{
			if ( !tag.IsValid() || !tag.GameObject.IsValid() )
				continue;

			if ( tag.GameObject.Components.Get<ThornsTreeTrunkColliderMarker>( FindMode.EverythingInSelf ).IsValid() )
				continue;

			var renderer = tag.Renderer;
			if ( renderer is null || !renderer.IsValid() || !renderer.Model.IsValid() )
				continue;

			sampleRadius = ApplyCollision( tag.GameObject, renderer.Model, MathF.Max( tag.GameObject.WorldScale.x, 0.01f ) );
			sampleHeight = ResolveSizing( renderer.Model, tag.GameObject.WorldScale.x ).HeightWorld;
			refreshed++;
		}

		foreach ( var obj in scene.GetAllObjects( true ) )
		{
			if ( !obj.IsValid() || !obj.Tags.Has( "tree" ) )
				continue;

			if ( obj.Components.Get<ThornsTreeTrunkColliderMarker>( FindMode.EverythingInSelf ).IsValid() )
				continue;

			if ( obj.Components.Get<ThornsFoliageInstance>( FindMode.EverythingInSelf ).IsValid() )
				continue;

			var renderer = obj.Components.Get<ModelRenderer>( FindMode.EverythingInSelf );
			if ( !renderer.IsValid() || !renderer.Model.IsValid() )
				continue;

			sampleRadius = ApplyCollision( obj, renderer.Model, MathF.Max( obj.WorldScale.x, 0.01f ) );
			sampleHeight = ResolveSizing( renderer.Model, obj.WorldScale.x ).HeightWorld;
			refreshed++;
		}

		if ( logWhenChanged && refreshed > 0 )
		{
			Log.Info(
				$"[Thorns Foliage] Live tree trunk collision → {refreshed} tree(s), "
				+ $"sample radius≈{sampleRadius:F1} in height≈{sampleHeight:F0} in "
				+ $"(radiusScale={LiveRadiusScale:F2} heightScale={LiveHeightScale:F2} areaScale={LiveAreaScale:F2} maxR={LiveMaxRadiusInches:F0})." );
		}
	}

	/// <summary>Collision + mesh alignment (spawn / full reset).</summary>
	public static void RefreshAllInScene( Scene scene, bool logWhenChanged = false, bool force = false )
	{
		RefreshCollisionInScene( scene, logWhenChanged, force );
		ThornsTreeTrunkVisualAlignment.RefreshMeshAlignmentInScene( scene, force );
	}

	public static void InvalidateRefreshCache()
	{
		_lastTuningHash = int.MinValue;
		ThornsTreeTrunkVisualAlignment.InvalidateRefreshCache();
	}

	public static void LogTuningHelp()
	{
		Log.Info(
			"[Thorns Foliage] Tree trunk live tuning (no reload): "
			+ "tree_trunk_radius_scale, tree_trunk_height_scale, tree_trunk_area_scale, "
			+ "tree_trunk_max_radius, tree_trunk_min_radius, tree_trunk_max_height, "
			+ "tree_trunk_mesh_offset_x/y/z, tree_trunk_aspen_mesh_offset_x/y (mesh-only, auto live). "
			+ "tree_trunk_refresh_mesh = mesh nudge only; tree_trunk_rebuild = collision only." );
	}

	[ConCmd( "tree_trunk_refresh_mesh" )]
	public static void ConCmdRefreshMesh()
	{
		ThornsTreeTrunkVisualAlignment.InvalidateRefreshCache();
		ThornsTreeTrunkVisualAlignment.RefreshMeshAlignmentInScene( Game.ActiveScene, force: true );
		Log.Info( "[Thorns Foliage] Tree mesh alignment refreshed (colliders unchanged)." );
	}

	[ConCmd( "tree_trunk_rebuild" )]
	public static void ConCmdRebuild()
	{
		InvalidateRefreshCache();
		RefreshCollisionInScene( Game.ActiveScene, logWhenChanged: true, force: true );
		Log.Info( "[Thorns Foliage] Tree trunk collision rebuilt (mesh unchanged — use tree_trunk_refresh_mesh for visual nudge)." );
	}

	[ConCmd( "tree_trunk_help" )]
	public static void ConCmdHelp() => LogTuningHelp();

	[ConCmd( "tree_trunk_reset_defaults" )]
	public static void ConCmdResetDefaults()
	{
		LiveRadiusScale = 1f;
		LiveHeightScale = 1f;
		LiveAreaScale = DefaultAreaScale;
		LiveMaxRadiusInches = DefaultMaxRadiusInches;
		LiveMinRadiusInches = DefaultMinRadiusInches;
		LiveMaxHeightInches = DefaultMaxHeightInches;
		ThornsTreeTrunkVisualAlignment.ResetLiveOffsets();
		InvalidateRefreshCache();
		RefreshAllInScene( Game.ActiveScene, logWhenChanged: true, force: true );
		Log.Info( $"[Thorns Foliage] Tree trunk tuning reset to code defaults (areaScale={DefaultAreaScale}, maxR={DefaultMaxRadiusInches:F0}, maxH={DefaultMaxHeightInches:F0})." );
	}

	static int ComputeTuningHash() =>
		HashCode.Combine(
			LiveRadiusScale,
			LiveHeightScale,
			LiveAreaScale,
			LiveMaxRadiusInches,
			LiveMinRadiusInches,
			LiveMaxHeightInches );
}

/// <summary>Tracks a procedural tree trunk box for live rebuilds.</summary>
public sealed class ThornsTreeTrunkColliderMarker : Component
{
	internal Model CollisionModel { get; set; }
	internal float UniformScale { get; set; } = 1f;
	internal Collider ColliderRef { get; set; }
	[Property, ReadOnly] public float WorldRadius { get; set; }
	[Property, ReadOnly] public float WorldHeight { get; set; }

	public float RebuildCollisionIfReady()
	{
		if ( !GameObject.IsValid() || !CollisionModel.IsValid() )
			return 0f;

		UniformScale = MathF.Max( GameObject.WorldScale.x, 0.01f );
		return ThornsTreeTrunkCollision.ApplyCollision( GameObject, CollisionModel, UniformScale );
	}
}

/// <summary>Polls live trunk tuning and refreshes nearby tree colliders without reloading.</summary>
[Title( "Thorns Tree Trunk Collision Tuning" )]
[Category( "Terrain" )]
public sealed class ThornsTreeTrunkCollisionTuning : Component
{
	[Property, Group( "Collision" ), Range( 0.05f, 4f ), Title( "Radius scale (all trees)" )]
	public float RadiusScale
	{
		get => ThornsTreeTrunkCollision.LiveRadiusScale;
		set => ThornsTreeTrunkCollision.LiveRadiusScale = value;
	}

	[Property, Group( "Collision" ), Range( 0.05f, 4f ), Title( "Height scale (all trees)" )]
	public float HeightScale
	{
		get => ThornsTreeTrunkCollision.LiveHeightScale;
		set => ThornsTreeTrunkCollision.LiveHeightScale = value;
	}

	[Property, Group( "Collision" ), Range( 0.02f, 1f ), Title( "Footprint area scale" )]
	public float AreaScale
	{
		get => ThornsTreeTrunkCollision.LiveAreaScale;
		set => ThornsTreeTrunkCollision.LiveAreaScale = value;
	}

	[Property, Group( "Collision" ), Range( 48f, 2880f ), Title( "Max height (world inches)" )]
	public float MaxHeightInches
	{
		get => ThornsTreeTrunkCollision.LiveMaxHeightInches;
		set => ThornsTreeTrunkCollision.LiveMaxHeightInches = value;
	}

	[Property, Group( "Collision" ), Range( 8f, 120f ), Title( "Max radius (world inches)" )]
	public float MaxRadiusInches
	{
		get => ThornsTreeTrunkCollision.LiveMaxRadiusInches;
		set => ThornsTreeTrunkCollision.LiveMaxRadiusInches = value;
	}

	[Property, Group( "Collision" ), Title( "Log when live tuning changes" )]
	public bool LogTuningChanges { get; set; }

	TimeUntil _nextTuningPoll;

	protected override void OnUpdate()
	{
		if ( _nextTuningPoll )
			return;

		_nextTuningPoll = 0.35f;
		ThornsTreeTrunkCollision.RefreshCollisionInScene( Scene, LogTuningChanges );
		ThornsTreeTrunkVisualAlignment.RefreshMeshAlignmentInScene( Scene );
	}

	public static void EnsureOn( GameObject host )
	{
		if ( !host.IsValid() )
			return;

		if ( host.Components.Get<ThornsTreeTrunkCollisionTuning>( FindMode.EnabledInSelf ).IsValid() )
			return;

		host.Components.Create<ThornsTreeTrunkCollisionTuning>();
	}
}
