namespace Terraingen.Foliage;

using Terraingen;

/// <summary>Shared plane + PNG materials for distant tree billboard impostors.</summary>
public static class ThornsTreeBillboardAssets
{
	public const float PlaneBaseSizeInches = 100f;
	public const float BillboardWidthToHeight = 0.68f;

	const string DefaultPlaneModel = "models/dev/plane.vmdl";

	static ThornsFoliageConfig _config;
	static Model _planeModel;
	static Material _pineMaterial;
	static Material _aspenMaterial;
	static Material _oakMaterial;
	static bool _initialized;

	public static bool IsReady => _initialized && _planeModel.IsValid;

	public static void Configure( ThornsFoliageConfig config )
	{
		if ( config is null )
			return;

		if ( _config == config && _initialized )
			return;

		_config = config;
		_initialized = false;
		_planeModel = default;
		_pineMaterial = null;
		_aspenMaterial = null;
		_oakMaterial = null;

		if ( Application.IsDedicatedServer || Application.IsHeadless )
			return;

		if ( !config.UseTreeBillboardLod )
			return;

		_planeModel = Model.Load( config.TreeBillboardPlaneModel ?? DefaultPlaneModel );
		if ( !_planeModel.IsValid )
		{
			Log.Warning( $"[Thorns Foliage] Tree billboard plane unavailable: '{config.TreeBillboardPlaneModel}'." );
			return;
		}

		_pineMaterial = LoadBillboardMaterial( config.TreeBillboardPineMaterial, ThornsTextureResourceLoad.TreeLodPinePath );
		_aspenMaterial = LoadBillboardMaterial( config.TreeBillboardAspenMaterial, ThornsTextureResourceLoad.TreeLodAspenPath );
		_oakMaterial = LoadBillboardMaterial( config.TreeBillboardOakMaterial, ThornsTextureResourceLoad.TreeLodOakPath );

		if ( _pineMaterial is null || _aspenMaterial is null || _oakMaterial is null )
		{
			Log.Warning( "[Thorns Foliage] One or more tree billboard materials failed to load." );
			return;
		}

		_initialized = true;
	}

	public static Model PlaneModel => _planeModel;

	public static Material GetMaterial( FoliageSpecies species ) => species switch
	{
		FoliageSpecies.Oak => _oakMaterial,
		FoliageSpecies.Aspen => _aspenMaterial,
		_ => _pineMaterial,
	};

	public static Vector3 ComputePlaneScale( float worldHeightInches ) =>
		new(
			worldHeightInches * BillboardWidthToHeight / PlaneBaseSizeInches,
			worldHeightInches / PlaneBaseSizeInches,
			1f );

	public static Vector3 ComputeBillboardCenterOffset( float worldHeightInches ) =>
		Vector3.Up * (worldHeightInches * 0.5f);

	public static Rotation FaceCameraRotation( Vector3 worldPosition, Vector3 cameraPosition )
	{
		var flat = (cameraPosition - worldPosition).WithZ( 0f );
		if ( flat.LengthSquared < 1f )
			flat = Vector3.Forward;

		var yaw = Rotation.LookAt( flat.Normal, Vector3.Up ).Yaw();
		return Rotation.From( 90f, yaw, 0f );
	}

	public static Transform BuildInstancedTransform(
		Vector3 worldPosition,
		Vector3 cameraPosition,
		Vector3 treeUniformScale,
		FoliageSpecies species,
		ThornsFoliageConfig config,
		Model treeModel )
	{
		var height = EstimateWorldHeightInches( treeUniformScale.x, species, config, treeModel );
		var center = worldPosition + ComputeBillboardCenterOffset( height );
		return new Transform(
			center,
			FaceCameraRotation( worldPosition, cameraPosition ),
			ComputePlaneScale( height ) );
	}

	public static float EstimateWorldHeightInches( float uniformScale, FoliageSpecies species, ThornsFoliageConfig config, Model model = default )
	{
		if ( ThornsFoliageCloudModels.HasRenderableMesh( model ) )
			return ThornsFoliageCloudModels.EstimateWorldHeightInches( model, uniformScale, GetTargetHeight( species, config ), config );

		var target = GetTargetHeight( species, config );
		return target * uniformScale;
	}

	static float GetTargetHeight( FoliageSpecies species, ThornsFoliageConfig config ) => species switch
	{
		FoliageSpecies.Oak => config.OakTargetHeightInches,
		FoliageSpecies.Aspen => config.AspenTargetHeightInches,
		_ => config.PineTargetHeightInches,
	};

	public static void EnsureBillboardChild(
		GameObject treeRoot,
		ThornsFoliageInstance tag,
		ThornsFoliageConfig config )
	{
		if ( treeRoot is null || !treeRoot.IsValid() || tag is null || config is null )
			return;

		if ( !config.UseTreeBillboardLod )
			return;

		Configure( config );
		if ( !IsReady )
			return;

		if ( tag.BillboardRenderer is { IsValid: true } )
			return;

		var height = tag.BillboardWorldHeight > 0f
			? tag.BillboardWorldHeight
			: EstimateWorldHeightInches( treeRoot.WorldScale.x, tag.Species, config, tag.Renderer?.Model ?? default );
		tag.BillboardWorldHeight = height;

		var child = treeRoot.Children.FirstOrDefault( c => c.Name == "TreeBillboardLod" );
		if ( child is null || !child.IsValid() )
		{
			child = treeRoot.Scene.CreateObject( true );
			child.Name = "TreeBillboardLod";
			child.Parent = treeRoot;
		}

		child.LocalPosition = ComputeBillboardCenterOffset( height );
		child.LocalScale = ComputePlaneScale( height );

		var renderer = child.Components.Get<ModelRenderer>() ?? child.Components.Create<ModelRenderer>();
		renderer.Model = _planeModel;
		renderer.MaterialOverride = GetMaterial( tag.Species );
		renderer.RenderType = ModelRenderer.ShadowRenderType.Off;
		renderer.Enabled = false;

		tag.BillboardRenderer = renderer;
	}

	static Material LoadBillboardMaterial( string path, string colorTexturePath )
	{
		var material = LoadMaterialCopy( path );
		if ( material is null || !material.IsValid() )
			return null;

		if ( !ThornsTextureResourceLoad.IsMaterialUsable( material, colorTexturePath ) )
		{
			Log.Warning( $"[Thorns Foliage] Tree billboard material not ready: '{path}' (texture '{colorTexturePath}')." );
			return null;
		}

		return material;
	}

	static Material LoadMaterialCopy( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return null;

		var source = Material.Load( path );
		if ( source is null || !source.IsValid() )
			return null;

		var copy = source.CreateCopy();
		return copy.IsValid() ? copy : source;
	}
}
