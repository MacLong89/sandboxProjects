namespace Sandbox;

/// <summary>
/// Tripo / scaled meshes stretch albedo UVs when <see cref="GameObject.LocalScale"/> ≠ 1.
/// Repeats via <c>g_vTexCoordScale</c> on a material copy so texel density stays near ModelDoc / gallery tuning.
/// </summary>
public static class ThornsModelMaterialUvScale
{
	const string BaseColorVmatSuffix = "_basecolor.vmat";

	/// <summary>Apply UV tiling when planar scale exceeds this (scaled-up props).</summary>
	public const float DefaultMinScaleToCompensate = 1.01f;

	/// <summary>Extra UV repeat on all Thorns meshes to offset mip softness when scaled large.</summary>
	public const float TextureSharpnessBoost = 1.12f;

	/// <summary>Max <c>g_vTexCoordScale</c> for placeables — Tripo bounds often read ~1 unit and would drive 100+ repeats (muddy mips).</summary>
	const float PlaceableMaxTexCoordRepeat = 12f;

	/// <summary>Bounds below this (inches) with large scale → suspect import units; clamp UV repeat.</summary>
	const float PlaceableSuspiciousBoundsMaxInches = 16f;

	/// <summary>Legacy alias — same boost for placeables and everything else.</summary>
	public const float PlaceableSharpnessBoost = TextureSharpnessBoost;

	static readonly Dictionary<string, string> ModelPathToBaseColorVmat = new( StringComparer.OrdinalIgnoreCase )
	{
		["models/clutter/rock1.vmdl"] = "models/clutter/rock1_basecolor.vmat",
		["models/clutter/rock2.vmdl"] = "models/clutter/rock2_basecolor.vmat",
		["models/resources/stone_node_a.vmdl"] = "models/resources/stone_harvest.vmat",
		["models/resources/stone_node_b.vmdl"] = "models/resources/stone_harvest.vmat",
		["models/resources/ore_node_a.vmdl"] = "models/resources/ore_harvest.vmat",
		["models/resources/ore_node_b.vmdl"] = "models/resources/ore_harvest.vmat",
		["models/placeables/radio.vmdl"] = "models/placeables/radiotable_basecolor.vmat",
		["models/wolf/wolf.vmdl"] = "models/wolf/bloomwolf_basecolor.vmat",
		["models/panther/panther.vmdl"] = "models/panther/bloomed_panther_basecolor.vmat",
		["models/panther/bloomed_panther.vmdl"] = "models/panther/bloomed_panther_basecolor.vmat",
		["models/deer/deer.vmdl"] = "models/deer/bloomdeer_basecolor.vmat",
		["models/moose/moose.vmdl"] = "models/moose/bloommoose_basecolor.vmat",
		["models/elk/elk.vmdl"] = "models/elk/elk_basecolor.vmat",
	};

	static readonly Dictionary<string, Material> MaterialCache = new( StringComparer.OrdinalIgnoreCase );

	static readonly Dictionary<string, Material> ScaledMaterialCopyCache = new( StringComparer.OrdinalIgnoreCase );

	static readonly HashSet<string> LoggedMissingMaterials = new( StringComparer.OrdinalIgnoreCase );

	/// <summary>Apply <c>g_vTexCoordScale</c> on a material copy so texel density stays near the authored preview when scaled up.</summary>
	public static void ApplyForLocalScale(
		ModelRenderer renderer,
		Vector3 localScale,
		string modelAssetPath = null,
		Material sourceMaterial = default,
		float minScaleToCompensate = DefaultMinScaleToCompensate ) =>
		Apply( renderer, localScale, modelAssetPath, sourceMaterial, minScaleToCompensate );

	/// <summary>Placeable furniture — explicit mesh scale + placeable vmats (same UV math as all scaled meshes).</summary>
	public static void ApplyForPlaceableFurniture(
		ModelRenderer renderer,
		Model model,
		string modelAssetPath,
		Vector3 meshLocalScale,
		Material sourceMaterial = default )
	{
		if ( renderer is null || !renderer.IsValid() )
			return;

		if ( string.IsNullOrWhiteSpace( modelAssetPath ) && model.IsValid() )
			modelAssetPath = model.Name;

		var host = renderer.GameObject;
		var scaleForUv = host.IsValid() ? host.WorldScale : meshLocalScale;
		if ( scaleForUv.LengthSquared < 1e-8f )
			scaleForUv = meshLocalScale;

		ApplyScaledMesh( renderer, scaleForUv, model, modelAssetPath, sourceMaterial, isPlaceable: true );
	}

	/// <summary>Uses <see cref="GameObject.LocalScale"/> (mesh scale), not parent world scale.</summary>
	public static void ApplyForGameObject(
		ModelRenderer renderer,
		GameObject scaledObject,
		string modelAssetPath = null,
		Material sourceMaterial = default,
		float minScaleToCompensate = DefaultMinScaleToCompensate )
	{
		if ( renderer is null || !renderer.IsValid() )
			return;

		var scaleHost = scaledObject.IsValid() ? scaledObject : renderer.GameObject;
		if ( !scaleHost.IsValid() )
			return;

		var model = renderer.Model;
		if ( string.IsNullOrWhiteSpace( modelAssetPath ) && model.IsValid() )
			modelAssetPath = model.Name;

		ApplyScaledMesh(
			renderer,
			scaleHost.LocalScale,
			model,
			modelAssetPath,
			sourceMaterial,
			minScaleToCompensate );
	}

	/// <summary>Convenience after assigning <paramref name="model"/> on a scaled prop.</summary>
	public static void ApplyForScaledModel(
		ModelRenderer renderer,
		GameObject scaledObject,
		Model model,
		string modelAssetPath = null,
		Material sourceMaterial = default,
		float minScaleToCompensate = DefaultMinScaleToCompensate )
	{
		if ( renderer is null || !renderer.IsValid() )
			return;

		if ( string.IsNullOrWhiteSpace( modelAssetPath ) && model.IsValid() )
			modelAssetPath = model.Name;

		var scaleHost = scaledObject.IsValid() ? scaledObject : renderer.GameObject;
		var scale = scaleHost.IsValid() ? scaleHost.LocalScale : Vector3.One;
		ApplyScaledMesh( renderer, scale, model, modelAssetPath, sourceMaterial, minScaleToCompensate );
	}

	/// <summary>Alias for spawn/visual helpers that set model + UV in one step.</summary>
	public static void ApplyScaledModelPresentation(
		ModelRenderer renderer,
		GameObject scaledObject,
		Model model,
		string modelAssetPath = null,
		Material sourceMaterial = default,
		float minScaleToCompensate = DefaultMinScaleToCompensate ) =>
		ApplyForScaledModel( renderer, scaledObject, model, modelAssetPath, sourceMaterial, minScaleToCompensate );

	/// <summary>Wildlife / menu skinned meshes scaled on the root transform.</summary>
	public static void ApplyForSkinnedModel(
		SkinnedModelRenderer renderer,
		GameObject scaledObject,
		Model model,
		string modelAssetPath = null,
		Material sourceMaterial = default,
		float minScaleToCompensate = DefaultMinScaleToCompensate )
	{
		if ( renderer is null || !renderer.IsValid() )
			return;

		if ( string.IsNullOrWhiteSpace( modelAssetPath ) && model.IsValid() )
			modelAssetPath = model.Name;

		var scaleHost = scaledObject.IsValid() ? scaledObject : renderer.GameObject;
		var scale = scaleHost.IsValid() ? scaleHost.LocalScale : Vector3.One;
		if ( ShouldSkipModelAssetPath( modelAssetPath ) )
			return;

		var uvScale = ComputeTexCoordScaleForMesh( scale, model );
		if ( !NeedsUvCompensation( uvScale, minScaleToCompensate ) )
			return;

		var src = sourceMaterial.IsValid()
			? sourceMaterial
			: ResolveSourceMaterial( renderer.MaterialOverride, modelAssetPath );
		ApplyMaterialWithTexCoordScale( renderer, src, uvScale, modelAssetPath );
	}

	/// <summary>Alias for weapon/NPC skinned meshes after model + scale are assigned.</summary>
	public static void ApplyScaledSkinnedPresentation(
		SkinnedModelRenderer renderer,
		GameObject scaledObject,
		Model model,
		string modelAssetPath = null,
		Material sourceMaterial = default,
		float minScaleToCompensate = DefaultMinScaleToCompensate ) =>
		ApplyForSkinnedModel( renderer, scaledObject, model, modelAssetPath, sourceMaterial, minScaleToCompensate );

	public static void Apply(
		ModelRenderer renderer,
		Vector3 scale,
		string modelAssetPath = null,
		Material sourceMaterial = default,
		float minScaleToCompensate = DefaultMinScaleToCompensate )
	{
		if ( renderer is null || !renderer.IsValid() )
			return;

		var model = renderer.Model;
		if ( string.IsNullOrWhiteSpace( modelAssetPath ) && model.IsValid() )
			modelAssetPath = model.Name;

		ApplyScaledMesh( renderer, scale, model, modelAssetPath, sourceMaterial, minScaleToCompensate );
	}

	/// <summary>All <see cref="ModelRenderer"/> + <see cref="SkinnedModelRenderer"/> under <paramref name="root"/>.</summary>
	public static void ApplyToHierarchy(
		GameObject root,
		bool includeChildren = true,
		string modelAssetPathOverride = null )
	{
		if ( root is null || !root.IsValid() )
			return;

		var findMode = includeChildren
			? FindMode.EnabledInSelfAndDescendants
			: FindMode.EnabledInSelf;

		foreach ( var mr in root.Components.GetAll<ModelRenderer>( findMode ) )
		{
			if ( !mr.IsValid() )
				continue;

			var path = modelAssetPathOverride;
			if ( string.IsNullOrWhiteSpace( path ) && mr.Model.IsValid() )
				path = mr.Model.Name;

			if ( IsPlaceableModelPath( path ) )
				ApplyForPlaceableFurniture( mr, mr.Model, path, mr.GameObject.IsValid() ? mr.GameObject.WorldScale : Vector3.One );
			else
				ApplyForScaledModel( mr, mr.GameObject, mr.Model, path );
		}

		foreach ( var smr in root.Components.GetAll<SkinnedModelRenderer>( findMode ) )
		{
			if ( !smr.IsValid() )
				continue;

			var path = modelAssetPathOverride;
			if ( string.IsNullOrWhiteSpace( path ) && smr.Model.IsValid() )
				path = smr.Model.Name;

			var scaleHost = smr.GameObject;
			ApplyForSkinnedModel( smr, scaleHost, smr.Model, path );
		}
	}

	/// <summary>Attach fixup + apply once (spawn helpers).</summary>
	public static void EnsureFixupOnHierarchy( GameObject root, bool includeChildren = true )
	{
		if ( root is null || !root.IsValid() )
			return;

		if ( !root.Components.Get<ThornsScaledMeshTextureFixup>( FindMode.EnabledInSelf ).IsValid() )
		{
			var fixup = root.Components.Create<ThornsScaledMeshTextureFixup>();
			fixup.IncludeChildren = includeChildren;
		}

		ApplyToHierarchy( root, includeChildren );
	}

	static void ApplyScaledMesh(
		ModelRenderer renderer,
		Vector3 meshLocalScale,
		Model model,
		string modelAssetPath,
		Material sourceMaterial,
		float minScaleToCompensate = DefaultMinScaleToCompensate,
		bool isPlaceable = false )
	{
		if ( ShouldSkipModelAssetPath( modelAssetPath ) )
			return;

		var uvScale = isPlaceable
			? ComputeTexCoordScaleForPlaceable( meshLocalScale, model )
			: ComputeTexCoordScaleForMesh( meshLocalScale, model );
		if ( !NeedsUvCompensation( uvScale, minScaleToCompensate ) )
			return;

		var src = sourceMaterial.IsValid()
			? sourceMaterial
			: isPlaceable
				? LoadMaterialForModel( modelAssetPath )
				: ResolveSourceMaterial( renderer.MaterialOverride, modelAssetPath );
		ApplyMaterialWithTexCoordScale( renderer, src, uvScale, modelAssetPath );
	}

	static string BuildScaledMaterialCacheKey( Material src, Vector2 uvScale, string modelAssetPath )
	{
		var path = string.IsNullOrWhiteSpace( modelAssetPath ) ? "" : modelAssetPath.Trim();
		if ( !string.IsNullOrEmpty( path ) )
			return $"{path}|{uvScale.x:F3}|{uvScale.y:F3}";

		return $"{src.GetHashCode()}|{uvScale.x:F3}|{uvScale.y:F3}";
	}

	static bool NeedsUvCompensation( Vector2 uvScale, float minScaleToCompensate ) =>
		uvScale.x >= minScaleToCompensate || uvScale.y >= minScaleToCompensate;

	static void ApplyMaterialWithTexCoordScale(
		ModelRenderer renderer,
		Material src,
		Vector2 uvScale,
		string modelAssetPath )
	{
		if ( renderer is null || !renderer.IsValid() )
			return;

		if ( !TryCreateScaledMaterialCopy( src, uvScale, modelAssetPath, out var copy ) )
			return;

		renderer.MaterialOverride = copy;
	}

	static void ApplyMaterialWithTexCoordScale(
		SkinnedModelRenderer renderer,
		Material src,
		Vector2 uvScale,
		string modelAssetPath )
	{
		if ( renderer is null || !renderer.IsValid() )
			return;

		if ( !TryCreateScaledMaterialCopy( src, uvScale, modelAssetPath, out var copy ) )
			return;

		renderer.MaterialOverride = copy;
	}

	static bool TryCreateScaledMaterialCopy(
		Material src,
		Vector2 uvScale,
		string modelAssetPath,
		out Material copy )
	{
		copy = default;
		if ( !src.IsValid() )
		{
			TryLogMissingMaterial( modelAssetPath );
			return false;
		}

		var cacheKey = BuildScaledMaterialCacheKey( src, uvScale, modelAssetPath );
		if ( ScaledMaterialCopyCache.TryGetValue( cacheKey, out var cached ) && cached.IsValid() )
		{
			copy = cached;
			return true;
		}

		copy = src.CreateCopy();
		if ( !copy.IsValid() )
			return false;

		if ( copy.Attributes is not null )
			copy.Attributes.Set( "g_vTexCoordScale", uvScale );

		ScaledMaterialCopyCache[cacheKey] = copy;
		return true;
	}

	/// <summary>UV repeat from mesh local scale — bounds-aware for non-uniform props.</summary>
	public static Vector2 ComputeTexCoordScaleForMesh( Vector3 meshLocalScale, Model model = default )
	{
		var uv = ComputeTexCoordScale( meshLocalScale );

		if ( model.IsValid() && !model.IsError )
		{
			var bounds = model.Bounds.Size;
			if ( bounds.LengthSquared > 1e-8f )
			{
				var sx = MathF.Abs( meshLocalScale.x );
				var sy = MathF.Abs( meshLocalScale.y );
				var sz = MathF.Abs( meshLocalScale.z );
				var u = MathF.Max( sx, sy );
				var v = MathF.Max( sz, MathF.Min( sx, sy ) );
				uv = new Vector2( u, v );
			}
		}

		return new Vector2(
			MathF.Max( 1f, uv.x * TextureSharpnessBoost ),
			MathF.Max( 1f, uv.y * TextureSharpnessBoost ) );
	}

	/// <summary>Placeable UV repeat — same base math as meshes, with clamp when import bounds are far too small.</summary>
	public static Vector2 ComputeTexCoordScaleForPlaceable( Vector3 meshWorldScale, Model model = default )
	{
		var uv = ComputeTexCoordScaleForMesh( meshWorldScale, model );

		if ( !model.IsValid() || model.IsError )
			return uv;

		var bounds = model.Bounds.Size;
		var maxBound = MathF.Max( bounds.x, MathF.Max( bounds.y, bounds.z ) );
		if ( maxBound >= PlaceableSuspiciousBoundsMaxInches )
			return uv;

		if ( uv.x <= PlaceableMaxTexCoordRepeat && uv.y <= PlaceableMaxTexCoordRepeat )
			return uv;

		var maxScale = MathF.Max(
			MathF.Abs( meshWorldScale.x ),
			MathF.Max( MathF.Abs( meshWorldScale.y ), MathF.Abs( meshWorldScale.z ) ) );
		var extent = maxBound * maxScale;
		const float refCatalogExtentInches = 120f;
		var extentT = Math.Clamp( extent / refCatalogExtentInches, 0.35f, 1.25f );
		var cap = MathF.Max( 1f, PlaceableMaxTexCoordRepeat * extentT );

		return new Vector2(
			MathF.Min( uv.x, cap ),
			MathF.Min( uv.y, cap ) );
	}

	/// <summary>XY tiling from horizontal extent; V also considers height so tall props don't stretch vertically.</summary>
	public static Vector2 ComputeTexCoordScale( Vector3 scale )
	{
		var sx = MathF.Abs( scale.x );
		var sy = MathF.Abs( scale.y );
		var sz = MathF.Abs( scale.z );
		var planar = MathF.Max( sx, sy );
		if ( planar < 1e-6f )
			planar = MathF.Max( sz, 1f );

		var vertical = MathF.Max( planar, sz );
		return new Vector2( planar, vertical );
	}

	public static Material LoadPlaceableMaterial( string modelAssetPath ) => LoadMaterialForModel( modelAssetPath );

	/// <summary>Harvest nodes + clutter rocks — always bind project <c>models/clutter/rock*_basecolor.vmat</c> (solid color, no PNG).</summary>
	public static void ApplyClutterRockMaterial(
		ModelRenderer renderer,
		GameObject scaleHost,
		Model model,
		string modelAssetPath )
	{
		if ( renderer is null || !renderer.IsValid() )
			return;

		var vmatPath = ResolveBaseColorVmatPath( modelAssetPath );
		if ( string.IsNullOrWhiteSpace( vmatPath ) )
			return;

		var src = TryLoadMaterial( vmatPath, allowMaterialLoad: true );
		if ( !src.IsValid() )
		{
			TryLogMissingMaterial( modelAssetPath );
			return;
		}

		var scale = scaleHost.IsValid() ? scaleHost.LocalScale : Vector3.One;
		var uvScale = ComputeTexCoordScaleForMesh( scale, model );
		ApplyMaterialWithTexCoordScale( renderer, src, uvScale, modelAssetPath );
	}

	public static Material LoadMaterialForModel( string modelAssetPath )
	{
		var vmatPath = ResolveBaseColorVmatPath( modelAssetPath );
		if ( string.IsNullOrWhiteSpace( vmatPath ) )
			return default;

		if ( MaterialCache.TryGetValue( vmatPath, out var cached ) && cached.IsValid() )
			return cached;

		var mat = TryLoadMaterial( vmatPath, allowMaterialLoad: true );
		if ( mat.IsValid() )
			MaterialCache[vmatPath] = mat;

		return mat;
	}

	static bool IsPlaceableModelPath( string modelAssetPath )
	{
		if ( string.IsNullOrWhiteSpace( modelAssetPath ) )
			return false;

		var path = modelAssetPath.Trim().Replace( '\\', '/' );
		return path.Contains( "/placeables/", StringComparison.OrdinalIgnoreCase )
		       || path.StartsWith( "models/placeables/", StringComparison.OrdinalIgnoreCase );
	}

	static bool ShouldSkipModelAssetPath( string modelAssetPath )
	{
		if ( string.IsNullOrWhiteSpace( modelAssetPath ) )
			return false;

		var path = modelAssetPath.Trim().Replace( '\\', '/' );
		if ( path.Contains( "facepunch.", StringComparison.OrdinalIgnoreCase ) )
			return true;

		// sboxweapons / mounted packages — materials live on the vmdl; Tripo _basecolor.vmat inference breaks TP guns.
		if ( path.StartsWith( "models/weapons/", StringComparison.OrdinalIgnoreCase )
		     || path.Contains( "/weapons/", StringComparison.OrdinalIgnoreCase ) )
			return true;

		// Procedural building pieces bake UVs in mesh geometry — do not double-tile via material.
		if ( path.StartsWith( "thorns/building/", StringComparison.OrdinalIgnoreCase ) )
			return true;

		return false;
	}

	static Material ResolveSourceMaterial( Material existingOverride, string modelAssetPath )
	{
		if ( existingOverride is { IsValid: true } )
			return existingOverride;

		return LoadMaterialForModel( modelAssetPath );
	}

	static string ResolveBaseColorVmatPath( string modelAssetPath )
	{
		if ( string.IsNullOrWhiteSpace( modelAssetPath ) )
			return "";

		var path = modelAssetPath.Trim().Replace( '\\', '/' );
		if ( ModelPathToBaseColorVmat.TryGetValue( path, out var mapped ) )
			return mapped;

		return InferBaseColorVmatPath( path );
	}

	static Material TryLoadMaterial( string vmatPath, bool allowMaterialLoad )
	{
		if ( string.IsNullOrWhiteSpace( vmatPath ) )
			return default;

		var trimmed = vmatPath.Trim();
		var fromLibrary = ResourceLibrary.Get<Material>( trimmed );
		if ( fromLibrary is { IsValid: true } )
			return fromLibrary;

		if ( !allowMaterialLoad )
			return default;

		var loaded = Material.Load( trimmed );
		return loaded.IsValid() ? loaded : default;
	}

	static void TryLogMissingMaterial( string modelAssetPath )
	{
		if ( string.IsNullOrWhiteSpace( modelAssetPath ) )
			return;

		var vmat = ResolveBaseColorVmatPath( modelAssetPath );
		if ( string.IsNullOrEmpty( vmat ) || !LoggedMissingMaterials.Add( vmat ) )
			return;

		Log.Warning( $"[Thorns] Scaled mesh UV: material not found '{vmat}' — textures may look blurry when scaled." );
	}

	/// <summary><c>models/foo/bar.vmdl</c> → <c>models/foo/bar_basecolor.vmat</c> (Thorns Tripo convention).</summary>
	public static string InferBaseColorVmatPath( string modelAssetPath )
	{
		if ( string.IsNullOrWhiteSpace( modelAssetPath ) )
			return "";

		var path = modelAssetPath.Trim().Replace( '\\', '/' );
		if ( path.EndsWith( ".vmdl", StringComparison.OrdinalIgnoreCase ) )
			return path[..^5] + BaseColorVmatSuffix;

		return "";
	}
}
