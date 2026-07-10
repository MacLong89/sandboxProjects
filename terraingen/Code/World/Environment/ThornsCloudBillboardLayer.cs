namespace Terraingen.World.Environment;

using Terraingen;

/// <summary>
/// Camera-followed cloud puffs made from simple generated meshes.
/// </summary>
[Title( "Thorns Cloud Billboard Layer" )]
[Category( "Environment" )]
[Icon( "cloud" )]
public sealed class ThornsCloudBillboardLayer : Component
{
	const string DefaultPlaneModel = "runtime/generated_cloud_blob";
	const string DefaultMaterialPath = "materials/skybox/thorns_cloud_sprite.vmat";
	const string TexturedMaterialPath = "materials/skybox/thorns_cloud_sprite.vmat";
	const string SolidMaterialPath = "materials/skybox/thorns_cloud_solid.vmat";
	const float PlaneBaseSizeInches = 100f;

	[Property, Group( "Assets" )] public string PlaneModelPath { get; set; } = DefaultPlaneModel;
	[Property, Group( "Assets" )] public string MaterialPath { get; set; } = DefaultMaterialPath;

	[Property, Group( "Layer" )] public float CloudAltitudeAboveObserverInches { get; set; } = 6000f;
	[Property, Group( "Layer" ), Range( 4, 48 )] public int CloudCount { get; set; } = 28;
	[Property, Group( "Layer" )] public float SpreadRadiusInches { get; set; } = 18000f;
	[Property, Group( "Layer" )] public float MinDiameterInches { get; set; } = 2800f;
	[Property, Group( "Layer" )] public float MaxDiameterInches { get; set; } = 6200f;
	[Property, Group( "Layer" ), Range( 0f, 1f )] public float PositionJitter { get; set; } = 0.92f;
	[Property, Group( "Layer" )] public float AltitudeJitterInches { get; set; } = 450f;
	[Property, Group( "Layer" ), Range( 1, 8 )] public int GuaranteedOverheadClouds { get; set; } = 4;
	[Property, Group( "Layer" ), Range( 0f, 1f )] public float MinimumVisualOpacity { get; set; } = 0.24f;
	[Property, Group( "Layer" )] public float EdgeFadeDistanceInches { get; set; } = 5200f;
	[Property, Group( "Layer" )] public int LayoutSeed { get; set; } = 42069;

	[Property, Group( "Wind" )] public Vector2 WindDirection { get; set; } = new( 1f, 0.22f );
	[Property, Group( "Wind" ), Range( 0f, 3f )] public float WindSpeed { get; set; } = 1f;
	[Property, Group( "Wind" )] public float DriftSpeedInchesPerSecond { get; set; } = 320f;

	[Property, Group( "References" )] public ThornsCloudController CloudController { get; set; }

	readonly List<CloudTile> _tiles = new();
	readonly List<Material> _materialTemplates = new();

	GameObject _root;
	Model _planeModel;
	GameObject _observerObject;
	CameraComponent _observerCamera;
	TimeUntil _nextObserverRefresh;
	Vector2 _driftOffset;
	Vector3 _lastObserver;
	Vector3 _rootWorldPosition;
	Color _cloudTint = Color.White;
	float _cloudOpacity = 0.94f;
	bool _built;
	bool _visible = true;
	bool _loggedFirstFollow;
	string _buildDetail = "not built";

	protected override void OnStart()
	{
		if ( CloudController is null || !CloudController.IsValid() )
			CloudController = Components.Get<ThornsCloudController>( FindMode.EverythingInSelfAndParent );

		TryBuild();
	}

	protected override void OnEnabled() => TryBuild();

	protected override void OnDisabled() => Clear();

	protected override void OnDestroy() => Clear();

	protected override void OnUpdate()
	{
		if ( !ShouldRenderClouds() || !_built || !_visible || _tiles.Count == 0 || _root is null || !_root.IsValid() )
			return;

		if ( Terraingen.UI.Core.ThornsMenuPerformance.IsOverlayUiOpen )
			return;

		_lastObserver = ResolveObserver();
		_rootWorldPosition = _lastObserver + Vector3.Up * CloudAltitudeAboveObserverInches;
		_root.WorldPosition = _rootWorldPosition;
		if ( !_loggedFirstFollow && _lastObserver != Vector3.Zero )
		{
			_loggedFirstFollow = true;
			Log.Info( $"[Thorns Clouds] Following observer at {_lastObserver} root={_rootWorldPosition} renderers={_tiles.Count}." );
		}

		var wind = WindDirection.LengthSquared > 1e-6f
			? WindDirection.Normal
			: new Vector2( 1f, 0.18f ).Normal;

		var wrap = Math.Max( SpreadRadiusInches, 1f ) * 2f;
		_driftOffset += wind * (DriftSpeedInchesPerSecond * WindSpeed) * Time.Delta;
		_driftOffset = new Vector2(
			WrapOffset( _driftOffset.x, wrap ),
			WrapOffset( _driftOffset.y, wrap ) );

		var half = wrap * 0.5f;

		foreach ( var tile in _tiles )
		{
			if ( tile.Object is null || !tile.Object.IsValid() )
				continue;

			var local = tile.LocalOffset + _driftOffset;
			local = new Vector2(
				WrapOffset( local.x + half, wrap ) - half,
				WrapOffset( local.y + half, wrap ) - half );

			tile.Object.LocalPosition = new Vector3( local.x, local.y, tile.AltitudeOffset );

			if ( tile.Renderer is not null && tile.Renderer.IsValid() )
				ApplyTileTint( tile, ComputeEdgeFade( local, half ) );
		}
	}

	public void ApplyEnvironment( ThornsEnvironmentState state )
	{
		if ( !_built )
			TryBuild();

		if ( !_built )
			return;

		_cloudTint = state.CloudColor;
		_cloudOpacity = Math.Clamp(
			MathX.Lerp( 0.24f, 0.48f, state.CloudOpacity.Clamp( 0f, 1f ) ),
			MinimumVisualOpacity,
			0.56f );
		_visible = _cloudOpacity > 0.02f;

		if ( _root is not null && _root.IsValid() )
			_root.Enabled = _visible;
	}

	public void Clear()
	{
		foreach ( var tile in _tiles )
		{
			if ( tile.Object is not null && tile.Object.IsValid() )
				tile.Object.Destroy();
		}

		_tiles.Clear();
		_materialTemplates.Clear();
		_built = false;
		_visible = true;
		_loggedFirstFollow = false;
		_buildDetail = "cleared";
		_driftOffset = Vector2.Zero;

		if ( _root is not null && _root.IsValid() )
			_root.Destroy();

		DestroyOrphanedCloudObjects();
		_root = null;
		_planeModel = default;
	}

	public void ForceRebuild()
	{
		Clear();
		TryBuild();
	}

	internal string BuildDebugStatus()
	{
		return
			$"built={_built} visible={_visible} tiles={_tiles.Count} materials={_materialTemplates.Count} "
			+ $"observer={_lastObserver} root={_rootWorldPosition} "
			+ $"altitude={CloudAltitudeAboveObserverInches:F0} spread={SpreadRadiusInches:F0} detail={_buildDetail}";
	}

	void TryBuild()
	{
		if ( _built || !ShouldRenderClouds() )
			return;

		if ( Scene is null || !Scene.IsValid() )
		{
			_buildDetail = "scene unavailable";
			return;
		}

		DestroyOrphanedCloudObjects();

		_planeModel = LoadPlaneModel();
		if ( !_planeModel.IsValid || _planeModel.IsError )
		{
			_buildDetail = $"plane load failed: '{PlaneModelPath}'";
			Log.Warning( $"[Thorns Clouds] {_buildDetail}" );
			return;
		}

		if ( !LoadMaterialVariants() )
		{
			_buildDetail = "cloud texture material missing";
			Log.Warning( "[Thorns Clouds] Cloud texture material missing; refusing white fallback clouds." );
			return;
		}

		_root = Scene.CreateObject( true );
		_root.Name = "Thorns Cloud Puffs";
		_root.NetworkMode = NetworkMode.Never;
		_root.Parent = GameObject;
		_root.Tags.Add( ThornsEnvironmentDirector.EnvironmentTag );

		BuildScatteredClouds();

		_built = _tiles.Count > 0;
		_buildDetail = _built ? "ok" : "scatter produced zero clouds";

		if ( !_built )
		{
			Log.Warning( "[Thorns Clouds] Scatter build produced zero cloud tiles." );
			Clear();
			return;
		}

		var shaderName = "(unknown)";
		var sample = _materialTemplates.FirstOrDefault();
		if ( sample is not null && sample.IsValid()
		     && sample.Shader is not null && sample.Shader.IsValid() )
		{
			shaderName = sample.Shader.ResourceName;
		}

		Log.Info(
			$"[Thorns Clouds] Built {_tiles.Count} textured cloud mesh(es) "
			+ $"(shader={shaderName}, materials={_materialTemplates.Count}, "
			+ $"diameter={MinDiameterInches:F0}-{MaxDiameterInches:F0}\", +{CloudAltitudeAboveObserverInches:F0}\" above observer)." );
	}

	Model LoadPlaneModel()
	{
		return ThornsCloudQuadModel.GetOrCreate();
	}

	bool LoadMaterialVariants()
	{
		_materialTemplates.Clear();

		var textured = CreateTexturedCloudMaterial( "thorns_cloud_textured_template" );
		if ( textured is not null && textured.IsValid() )
			_materialTemplates.Add( textured );

		return _materialTemplates.Count > 0;
	}

	void BuildScatteredClouds()
	{
		_tiles.Clear();

		var count = Math.Max( CloudCount, 1 );
		var overheadCount = Math.Clamp( GuaranteedOverheadClouds, 1, count );
		var gridCount = Math.Max( 1, count - overheadCount );
		var cols = (int)Math.Ceiling( Math.Sqrt( gridCount ) );
		var rows = (int)Math.Ceiling( gridCount / (float)cols );
		var wrap = Math.Max( SpreadRadiusInches, 1f ) * 2f;
		var cellW = wrap / cols;
		var cellH = wrap / rows;
		var rng = new Random( LayoutSeed );
		var minDiameter = Math.Min( MinDiameterInches, MaxDiameterInches );
		var maxDiameter = Math.Max( MinDiameterInches, MaxDiameterInches );

		for ( var i = 0; i < count; i++ )
		{
			var localOffset = BuildCloudOffset( i, overheadCount, cols, rows, cellW, cellH, rng );
			var isOverhead = i < overheadCount;
			var diameter = isOverhead
				? MathX.Lerp( maxDiameter * 0.72f, maxDiameter, rng.NextSingle() )
				: MathX.Lerp( minDiameter, maxDiameter, rng.NextSingle() );
			var aspect = MathX.Lerp( 0.72f, 1.28f, rng.NextSingle() );
			var yaw = rng.NextSingle() * 360f;
			var flipX = rng.Next( 0, 2 ) == 0 ? 1f : -1f;
			var flipY = rng.Next( 0, 2 ) == 0 ? 1f : -1f;
			var altitudeJitter = isOverhead ? AltitudeJitterInches * 0.35f : AltitudeJitterInches;
			var altitudeOffset = (rng.NextSingle() * 2f - 1f) * altitudeJitter;
			var material = CreateCloudMaterial( i );
			if ( material is null || !material.IsValid() )
				continue;

			var model = ThornsCloudQuadModel.Create( material, $"thorns_cloud_tile_model_{i:D2}" );
			if ( !model.IsValid || model.IsError )
				continue;

			var scale = new Vector3(
				diameter * aspect * flipX / PlaneBaseSizeInches,
				diameter * flipY / PlaneBaseSizeInches,
				1f );

			var tile = Scene.CreateObject( true );
			tile.Name = $"Cloud Tile {i:D2}";
			tile.NetworkMode = NetworkMode.Never;
			tile.Parent = _root;
			tile.LocalPosition = new Vector3( localOffset.x, localOffset.y, altitudeOffset );
			tile.LocalScale = scale;
			tile.LocalRotation = Rotation.From( 180f, yaw, 0f );

			var renderer = tile.Components.Create<ModelRenderer>();
			renderer.Model = model;
			renderer.MaterialOverride = material;
			renderer.RenderType = ModelRenderer.ShadowRenderType.Off;
			renderer.RenderOptions.Game = true;
			renderer.RenderOptions.Overlay = false;
			renderer.Tint = Color.White;
			renderer.Enabled = true;

			var tileState = new CloudTile(
				tile,
				renderer,
				material,
				model,
				localOffset,
				altitudeOffset );
			ApplyTileTint( tileState, 1f );
			_tiles.Add( tileState );
		}
	}

	Vector2 BuildCloudOffset(
		int index,
		int overheadCount,
		int cols,
		int rows,
		float cellW,
		float cellH,
		Random rng )
	{
		if ( index < overheadCount )
		{
			if ( index == 0 )
				return Vector2.Zero;

			var angle = (index - 1) / Math.Max( 1f, overheadCount - 1f ) * MathF.PI * 2f;
			angle += (rng.NextSingle() * 2f - 1f) * 0.28f;
			var radius = MathX.Lerp( 2800f, 7600f, index / Math.Max( 1f, overheadCount - 1f ) );
			radius *= MathX.Lerp( 0.82f, 1.18f, rng.NextSingle() );
			return new Vector2( MathF.Cos( angle ), MathF.Sin( angle ) ) * radius;
		}

		var gridIndex = index - overheadCount;
		var col = gridIndex % cols;
		var row = gridIndex / cols;
		var centerX = (col - cols * 0.5f + 0.5f) * cellW;
		var centerY = (row - rows * 0.5f + 0.5f) * cellH;
		var jitterX = (rng.NextSingle() * 2f - 1f) * cellW * 0.5f * PositionJitter;
		var jitterY = (rng.NextSingle() * 2f - 1f) * cellH * 0.5f * PositionJitter;
		return new Vector2( centerX + jitterX, centerY + jitterY );
	}

	Material CreateCloudMaterial( int tileIndex )
	{
		return CreateTexturedCloudMaterial( $"thorns_cloud_textured_{tileIndex:D2}" );
	}

	void ApplyTileTint( CloudTile tile, float edgeFade )
	{
		var alpha = Math.Clamp( _cloudOpacity * edgeFade, 0f, 1f );
		var tint = Color.Lerp( _cloudTint, Color.White, 0.16f );
		tint = new Color(
			Math.Clamp( tint.r, 0.52f, 0.82f ),
			Math.Clamp( tint.g, 0.58f, 0.88f ),
			Math.Clamp( tint.b, 0.64f, 0.94f ),
			alpha );

		tile.Renderer.Enabled = _visible && alpha > 0.01f;
		tile.Renderer.Tint = tint;

		var material = tile.Material;
		if ( material is null || !material.IsValid() )
			return;

		material.Set( "g_flModelTintAmount", 1f );
		material.Set( "g_vColorTint", tint );
		material.Set( "g_flOpacityScale", alpha );
		material.Set( "CloudTint", new Vector3( tint.r, tint.g, tint.b ) );
		material.Set( "CloudOpacity", alpha );
		material.Attributes?.Set( "g_flModelTintAmount", 1f );
		material.Attributes?.Set( "g_vColorTint", tint );
		material.Attributes?.Set( "g_flOpacityScale", alpha );
		material.Attributes?.Set( "CloudTint", new Vector3( tint.r, tint.g, tint.b ) );
		material.Attributes?.Set( "CloudOpacity", alpha );
	}

	float ComputeEdgeFade( Vector2 local, float halfExtent )
	{
		if ( halfExtent <= 1f )
			return 1f;

		var fadeDistance = Math.Clamp( EdgeFadeDistanceInches, 1f, halfExtent );
		var remaining = halfExtent - Math.Max( Math.Abs( local.x ), Math.Abs( local.y ) );
		var t = Math.Clamp( remaining / fadeDistance, 0f, 1f );
		return t * t * (3f - 2f * t);
	}

	static Material CreateSolidCloudMaterial( string name )
	{
		var source = Material.Load( SolidMaterialPath );
		if ( source is null || !source.IsValid() )
			source = Material.Load( "materials/default/default.vmat" );
		if ( source is null || !source.IsValid() )
			source = Material.Load( "materials/building_materials/concrete.vmat" );
		if ( source is null || !source.IsValid() )
			source = Material.FromShader( "shaders/complex.shader" );

		if ( source is null || !source.IsValid() )
			return null;

		var material = source.CreateCopy( string.IsNullOrWhiteSpace( name ) ? "thorns_cloud_solid" : name );
		if ( material is null || !material.IsValid() )
			material = source;

		var whiteTexture = Texture.Load( "materials/default/default.tga" );
		if ( whiteTexture is not null && whiteTexture.IsValid )
		{
			material.Set( "TextureColor", whiteTexture );
			material.Set( "g_tColor", whiteTexture );
			material.Attributes?.Set( "TextureColor", whiteTexture );
			material.Attributes?.Set( "g_tColor", whiteTexture );
		}

		material.Set( "g_flAmbientOcclusionDirectDiffuse", 0f );
		material.Set( "g_flModelTintAmount", 0.35f );
		material.Set( "g_vColorTint", Color.White );
		material.Set( "g_bFogEnabled", false );
		material.Set( "g_flMetalness", 0f );
		material.Set( "g_flRoughnessScaleFactor", 1f );
		material.Attributes?.Set( "g_flAmbientOcclusionDirectDiffuse", 0f );
		material.Attributes?.Set( "g_flModelTintAmount", 0.35f );
		material.Attributes?.Set( "g_vColorTint", Color.White );
		material.Attributes?.Set( "g_bFogEnabled", false );
		material.Attributes?.Set( "g_flMetalness", 0f );
		material.Attributes?.Set( "g_flRoughnessScaleFactor", 1f );
		return material;
	}

	static Material CreateTexturedCloudMaterial( string name )
	{
		var source = Material.Load( TexturedMaterialPath );
		if ( source is null || !source.IsValid() || source.IsError )
			source = Material.FromShader( "shaders/complex.shader" );
		if ( source is null || !source.IsValid() )
			return null;

		var material = source.CreateCopy( string.IsNullOrWhiteSpace( name ) ? "thorns_cloud_textured" : name );
		if ( material is null || !material.IsValid() )
			material = source;

		material.SetFeature( "F_TRANSLUCENT", 1 );
		material.SetFeature( "F_RENDER_BACKFACES", 1 );

		if ( !ThornsTextureResourceLoad.TryLoadCloudVariant( 0, out var color, out var alpha, out _ ) )
			return null;

		if ( color is not null && color.IsValid )
		{
			material.Set( "TextureColor", color );
			material.Set( "g_tColor", color );
			material.Attributes?.Set( "TextureColor", color );
			material.Attributes?.Set( "g_tColor", color );
		}

		if ( alpha is not null && alpha.IsValid )
		{
			material.Set( "CloudAlphaTexture", alpha );
			material.Attributes?.Set( "CloudAlphaTexture", alpha );
		}

		var tint = new Color( 0.82f, 0.88f, 0.94f, 0.42f );
		material.Set( "g_flAmbientOcclusionDirectDiffuse", 0f );
		material.Set( "g_flModelTintAmount", 1f );
		material.Set( "g_vColorTint", tint );
		material.Set( "g_bFogEnabled", false );
		material.Set( "g_flMetalness", 0f );
		material.Set( "g_flRoughnessScaleFactor", 1f );
		material.Set( "g_flOpacityScale", 0.42f );
		material.Set( "CloudTint", new Vector3( tint.r, tint.g, tint.b ) );
		material.Set( "CloudOpacity", 0.42f );
		material.Attributes?.Set( "g_flAmbientOcclusionDirectDiffuse", 0f );
		material.Attributes?.Set( "g_flModelTintAmount", 1f );
		material.Attributes?.Set( "g_vColorTint", tint );
		material.Attributes?.Set( "g_bFogEnabled", false );
		material.Attributes?.Set( "g_flMetalness", 0f );
		material.Attributes?.Set( "g_flRoughnessScaleFactor", 1f );
		material.Attributes?.Set( "g_flOpacityScale", 0.42f );
		material.Attributes?.Set( "CloudTint", new Vector3( tint.r, tint.g, tint.b ) );
		material.Attributes?.Set( "CloudOpacity", 0.42f );
		return material;
	}

	static bool ShouldRenderClouds() =>
		Game.IsPlaying && !Application.IsDedicatedServer && !Application.IsHeadless;

	Vector3 ResolveObserver()
	{
		var observer = ThornsSceneObserver.Resolve( Scene, ref _observerObject, ref _observerCamera, ref _nextObserverRefresh );
		if ( observer != Vector3.Zero )
			return observer;

		if ( ThornsSceneObserver.TryGetMainCamera( Scene, out var camera )
		     && camera.GameObject is not null && camera.GameObject.IsValid() )
			return camera.GameObject.WorldPosition;

		return _lastObserver;
	}

	void DestroyOrphanedCloudObjects()
	{
		if ( Scene is null || !Scene.IsValid() )
			return;

		var old = new List<GameObject>();
		foreach ( var obj in Scene.GetAllObjects( true ) )
		{
			if ( obj is null || !obj.IsValid() || obj == _root )
				continue;

			if ( string.Equals( obj.Name, "Thorns Cloud Puffs", StringComparison.OrdinalIgnoreCase )
			     || string.Equals( obj.Name, "Thorns Cloud Layer", StringComparison.OrdinalIgnoreCase )
			     || obj.Name.StartsWith( "Cloud Tile ", StringComparison.OrdinalIgnoreCase ) )
			{
				old.Add( obj );
			}
		}

		foreach ( var obj in old )
		{
			if ( obj.IsValid() )
				obj.Destroy();
		}
	}

	static float WrapOffset( float value, float size )
	{
		if ( size <= 0f )
			return value;

		value %= size;
		if ( value < 0f )
			value += size;

		return value;
	}

	sealed class CloudTile
	{
		public CloudTile(
			GameObject obj,
			ModelRenderer renderer,
			Material material,
			Model model,
			Vector2 localOffset,
			float altitudeOffset )
		{
			Object = obj;
			Renderer = renderer;
			Material = material;
			Model = model;
			LocalOffset = localOffset;
			AltitudeOffset = altitudeOffset;
		}

		public GameObject Object { get; }
		public ModelRenderer Renderer { get; }
		public Material Material { get; }
		public Model Model { get; }
		public Vector2 LocalOffset { get; }
		public float AltitudeOffset { get; }
	}

}

static class ThornsCloudQuadModel
{
	static Model _cached;

	public static Model GetOrCreate()
	{
		if ( _cached.IsValid() && !_cached.IsError )
			return _cached;

		var material = Material.Load( "materials/default/default.vmat" );
		if ( material is null || !material.IsValid() )
			material = Material.FromShader( "shaders/complex.shader" );

		var vb = new VertexBuffer();
		vb.Init( false );
		var def = vb.Default;
		def.Color = Color32.White;
		vb.Default = def;

		var h = 50f;
		AddTri(
			vb,
			new Vector3( -h, -h, 0f ),
			new Vector3( h, -h, 0f ),
			new Vector3( h, h, 0f ),
			new Vector2( 0f, 1f ),
			new Vector2( 1f, 1f ),
			new Vector2( 1f, 0f ) );
		AddTri(
			vb,
			new Vector3( -h, -h, 0f ),
			new Vector3( h, h, 0f ),
			new Vector3( -h, h, 0f ),
			new Vector2( 0f, 1f ),
			new Vector2( 1f, 0f ),
			new Vector2( 0f, 0f ) );

		var mesh = new Mesh( material, MeshPrimitiveType.Triangles );
		mesh.CreateBuffers( vb, true );

		var builder = new ModelBuilder();
		builder.WithName( "thorns_cloud_quad" );
		builder.WithMass( 0f );
		builder.WithSurface( "default" );
		builder.AddMesh( mesh );
		_cached = builder.Create();
		return _cached;
	}

	public static Model Create( Material material, string name )
	{
		if ( material is null || !material.IsValid() )
			return GetOrCreate();

		return CreateModel( material, string.IsNullOrWhiteSpace( name ) ? "thorns_cloud_quad" : name );
	}

	static Model CreateModel( Material material, string name )
	{
		var vb = new VertexBuffer();
		vb.Init( false );
		var def = vb.Default;
		def.Color = Color32.White;
		vb.Default = def;

		AddCloudBlob( vb );

		var mesh = new Mesh( material, MeshPrimitiveType.Triangles );
		mesh.CreateBuffers( vb, true );

		var builder = new ModelBuilder();
		builder.WithName( name );
		builder.WithMass( 0f );
		builder.WithSurface( "default" );
		builder.AddMesh( mesh );
		return builder.Create();
	}

	static void AddCloudBlob( VertexBuffer vb )
	{
		var outline = new[]
		{
			new Vector2( -54f, -10f ),
			new Vector2( -48f, -23f ),
			new Vector2( -34f, -29f ),
			new Vector2( -22f, -25f ),
			new Vector2( -12f, -34f ),
			new Vector2( 4f, -36f ),
			new Vector2( 16f, -27f ),
			new Vector2( 30f, -30f ),
			new Vector2( 45f, -20f ),
			new Vector2( 52f, -5f ),
			new Vector2( 46f, 8f ),
			new Vector2( 55f, 18f ),
			new Vector2( 40f, 28f ),
			new Vector2( 24f, 25f ),
			new Vector2( 12f, 36f ),
			new Vector2( -2f, 30f ),
			new Vector2( -18f, 34f ),
			new Vector2( -29f, 24f ),
			new Vector2( -43f, 22f ),
			new Vector2( -56f, 9f ),
		};

		var center = Vector3.Zero;
		for ( var i = 0; i < outline.Length; i++ )
		{
			var a = outline[i];
			var b = outline[(i + 1) % outline.Length];
			AddSolidTri( vb, center, new Vector3( a.x, a.y, 0f ), new Vector3( b.x, b.y, 0f ), Vector3.Down );
		}
	}

	static void AddSolidTri( VertexBuffer vb, Vector3 a, Vector3 b, Vector3 c, Vector3 normal )
	{
		var tangent = MathF.Abs( Vector3.Dot( normal, Vector3.Up ) ) > 0.95f
			? Vector3.Right
			: Vector3.Cross( Vector3.Up, normal ).Normal;

		vb.Add( new Vertex( a, normal, tangent, CloudUv( a ) ) );
		vb.Add( new Vertex( b, normal, tangent, CloudUv( b ) ) );
		vb.Add( new Vertex( c, normal, tangent, CloudUv( c ) ) );
	}

	static Vector4 CloudUv( Vector3 position )
	{
		const float minX = -60f;
		const float maxX = 60f;
		const float minY = -40f;
		const float maxY = 40f;
		var u = Math.Clamp( (position.x - minX) / (maxX - minX), 0f, 1f );
		var v = Math.Clamp( 1f - (position.y - minY) / (maxY - minY), 0f, 1f );
		return new Vector4( u, v, 0f, 0f );
	}

	static void AddTri(
		VertexBuffer vb,
		Vector3 a,
		Vector3 b,
		Vector3 c,
		Vector2 uva,
		Vector2 uvb,
		Vector2 uvc )
	{
		var normal = Vector3.Up;
		var tangent = Vector3.Right;
		vb.Add( new Vertex( a, normal, tangent, new Vector4( uva.x, uva.y, 0f, 0f ) ) );
		vb.Add( new Vertex( b, normal, tangent, new Vector4( uvb.x, uvb.y, 0f, 0f ) ) );
		vb.Add( new Vertex( c, normal, tangent, new Vector4( uvc.x, uvc.y, 0f, 0f ) ) );
	}
}
