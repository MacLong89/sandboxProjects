namespace Terraingen.Boulders;

using Terraingen.Physics;

/// <summary>Single source of truth for sightline boulder sphere physics.</summary>
public static class ThornsBoulderSphereCollision
{
	/// <summary>Fraction of visible XY footprint used as walk collision radius.</summary>
	public const float DefaultRadiusScale = 0.675f;

	const float MinRadiusScale = 0.015f;
	const float MaxRadiusScale = 0.75f;

	/// <summary>Live-tunable radius scale — console: <c>thorns_boulder_radius_scale 0.04</c></summary>
	[ConVar( "thorns_boulder_radius_scale" )]
	public static float LiveRadiusScale { get; set; } = DefaultRadiusScale;

	static float _lastRefreshedScale = float.NaN;

	public static float ResolveRadiusScale()
	{
		if ( LiveRadiusScale <= 0f )
			return DefaultRadiusScale;

		return Math.Clamp( LiveRadiusScale, MinRadiusScale, MaxRadiusScale );
	}

	public static float Apply(
		GameObject obj,
		Model model,
		float uniformScale,
		float radiusScale = DefaultRadiusScale )
	{
		if ( !obj.IsValid() || !model.IsValid() )
			return 0f;

		DestroyCompetingColliders( obj );

		var bounds = ResolveCollisionBounds( model );
		var size = bounds.Size;
		if ( size.LengthSquared < 1e-12f )
			size = new Vector3( 48f, 48f, 72f );

		uniformScale = MathF.Max( uniformScale, 0.01f );
		radiusScale = Math.Clamp( radiusScale, MinRadiusScale, MaxRadiusScale );

		var horizontalExtentModel = MathF.Min( size.x, size.y );
		var localRadius = MathF.Max( horizontalExtentModel * 0.5f * radiusScale, 0.02f );
		var worldRadius = localRadius * uniformScale;
		var groundContactLocal = new Vector3( bounds.Center.x, bounds.Center.y, bounds.Mins.z );

		var collider = obj.Components.Create<SphereCollider>();
		collider.Center = groundContactLocal;
		collider.Radius = localRadius;
		collider.IsTrigger = false;
		collider.Static = true;
		collider.Enabled = true;

		var marker = obj.Components.Get<ThornsBoulderColliderMarker>()
		             ?? obj.Components.Create<ThornsBoulderColliderMarker>();
		marker.UniformScale = uniformScale;
		marker.RadiusScale = radiusScale;
		marker.CollisionModel = model;
		marker.WorldRadius = worldRadius;
		marker.NoteAppliedRadiusScale( radiusScale );

		return worldRadius;
	}

	/// <summary>Rebuild every boulder sphere when the live scale changes.</summary>
	public static void RefreshAllInScene( Scene scene, bool logWhenChanged = false, bool force = false )
	{
		if ( scene is null || !scene.IsValid() )
			return;

		var radiusScale = ResolveRadiusScale();
		var scaleChanged = float.IsNaN( _lastRefreshedScale )
		                   || MathF.Abs( radiusScale - _lastRefreshedScale ) > 1e-5f;

		if ( !force && !scaleChanged )
			return;

		_lastRefreshedScale = radiusScale;
		var refreshed = 0;
		var sampleWorldRadius = 0f;

		foreach ( var marker in scene.GetAllComponents<ThornsBoulderColliderMarker>() )
		{
			if ( !marker.IsValid() || !marker.GameObject.IsValid() )
				continue;

			if ( !marker.OverrideGlobalRadius )
				marker.RadiusScale = radiusScale;

			marker.UniformScale = MathF.Max( marker.GameObject.WorldScale.x, 0.01f );
			marker.RebuildIfReady();
			sampleWorldRadius = marker.WorldRadius;
			refreshed++;
		}

		foreach ( var obj in scene.GetAllObjects( true ) )
		{
			if ( !obj.IsValid() || !obj.Tags.Has( "boulder" ) )
				continue;

			if ( obj.Components.Get<ThornsBoulderColliderMarker>( FindMode.EverythingInSelf ).IsValid() )
				continue;

			var renderer = obj.Components.Get<ModelRenderer>( FindMode.EverythingInSelf );
			if ( !renderer.IsValid() || !renderer.Model.IsValid() )
				continue;

			var uniformScale = MathF.Max( obj.WorldScale.x, 0.01f );
			sampleWorldRadius = Apply( obj, renderer.Model, uniformScale, radiusScale );
			refreshed++;
		}

		if ( logWhenChanged && refreshed > 0 )
			Log.Info( $"[Thorns Boulders] Live collision radiusScale={radiusScale:F3} → {refreshed} boulder(s), sampleWorldRadius≈{sampleWorldRadius:F0}in." );
	}

	public static void InvalidateRefreshCache() => _lastRefreshedScale = float.NaN;

	/// <summary>Reset live scale to code default (ignores persisted console value until changed again).</summary>
	public static void SyncLiveRadiusScaleFromDefaults() => LiveRadiusScale = DefaultRadiusScale;

	public static void SyncLiveRadiusScale( float overrideOrZero )
	{
		LiveRadiusScale = overrideOrZero > 0f ? overrideOrZero : DefaultRadiusScale;
		InvalidateRefreshCache();
	}

	public static void DestroyCompetingColliders( GameObject obj )
	{
		if ( !obj.IsValid() )
			return;

		var rb = obj.Components.Get<Rigidbody>();
		if ( rb.IsValid() )
			rb.Destroy();

		foreach ( var box in obj.Components.GetAll<BoxCollider>( FindMode.EverythingInSelf ) )
			box.Destroy();

		foreach ( var sphere in obj.Components.GetAll<SphereCollider>( FindMode.EverythingInSelf ) )
			sphere.Destroy();

		foreach ( var capsule in obj.Components.GetAll<CapsuleCollider>( FindMode.EverythingInSelf ) )
			capsule.Destroy();

		foreach ( var hull in obj.Components.GetAll<ModelCollider>( FindMode.EverythingInSelf ) )
			hull.Destroy();
	}

	static BBox ResolveCollisionBounds( Model model )
	{
		var bounds = TerraingenAnchoredPhysics.GetTightModelBounds( model );
		if ( bounds.Size.LengthSquared > 1e-12f )
			return bounds;

		if ( model.IsValid() && model.RenderBounds.Size.LengthSquared > 1e-12f )
			return model.RenderBounds;

		return new BBox( new Vector3( -24f, -24f, 0f ), new Vector3( 24f, 24f, 72f ) );
	}
}

/// <summary>Marks procedurally spawned boulders and rebuilds their sphere from code defaults.</summary>
[Title( "Thorns Boulder Collider" )]
[Category( "Terrain" )]
public sealed class ThornsBoulderColliderMarker : Component
{
	[Property] public bool OverrideGlobalRadius { get; set; }
	[Property] public float UniformScale { get; set; } = 1f;
	[Property] public float RadiusScale { get; set; } = ThornsBoulderSphereCollision.DefaultRadiusScale;
	[Property] public Model CollisionModel { get; set; }
	[Property, ReadOnly] public float WorldRadius { get; set; }

	float _appliedRadiusScale = float.NaN;

	protected override void OnUpdate()
	{
		if ( !OverrideGlobalRadius || !GameObject.IsValid() || !CollisionModel.IsValid() )
			return;

		if ( float.IsNaN( _appliedRadiusScale ) || MathF.Abs( RadiusScale - _appliedRadiusScale ) > 1e-5f )
			RebuildIfReady();
	}

	public void NoteAppliedRadiusScale( float radiusScale ) => _appliedRadiusScale = radiusScale;

	public void RebuildIfReady()
	{
		if ( !GameObject.IsValid() || !CollisionModel.IsValid() )
			return;

		UniformScale = MathF.Max( GameObject.WorldScale.x, 0.01f );
		WorldRadius = ThornsBoulderSphereCollision.Apply(
			GameObject,
			CollisionModel,
			UniformScale,
			RadiusScale );
	}
}

/// <summary>Polls live collision scale and refreshes all boulder spheres (attach to terrain bootstrap).</summary>
[Title( "Thorns Boulder Collision Tuning" )]
[Category( "Terrain" )]
public sealed class ThornsBoulderCollisionTuning : Component
{
	[Property, Group( "Collision" ), Range( 0.015f, 0.75f ), Title( "Live radius scale (all boulders)" )]
	public float LiveRadiusScale
	{
		get => ThornsBoulderSphereCollision.LiveRadiusScale;
		set => ThornsBoulderSphereCollision.LiveRadiusScale = value;
	}

	[Property, Group( "Collision" ), Title( "Log when live scale changes" )]
	public bool LogScaleChanges { get; set; }

	TimeUntil _nextRefreshPoll;

	protected override void OnStart()
	{
		ThornsBoulderSphereCollision.RefreshAllInScene( Scene, LogScaleChanges, force: true );
	}

	protected override void OnUpdate()
	{
		if ( _nextRefreshPoll )
			return;

		_nextRefreshPoll = 0.35f;
		ThornsBoulderSphereCollision.RefreshAllInScene( Scene, LogScaleChanges );
	}

	public static void EnsureOn( GameObject host )
	{
		if ( !host.IsValid() )
			return;

		if ( host.Components.Get<ThornsBoulderCollisionTuning>( FindMode.EnabledInSelf ).IsValid() )
			return;

		host.Components.Create<ThornsBoulderCollisionTuning>();
	}
}
