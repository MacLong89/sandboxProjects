namespace Sandbox;

/// <summary>
/// M700 scope optics use <c>v_ranged_sight</c> — housing and lens share one mesh/material (no separate glass slot).
/// Scope PiP is composited in UI only; this helper may clear stale material overrides on equip.
/// </summary>
public static class AimboxM700ScopeLensPresentation
{
	public const bool LogMaterialNamesOnce = false;

	static readonly string[] ScopeMaterialHints =
	[
		"v_ranged_sight",
		"ranged_sight",
		"sight_ranged"
	];

	static Material _hiddenLensMaterial;
	static bool _loggedDrawCalls;
	static bool _scopeMeshHidden;

	public static void LogDrawCalls( ModelRenderer renderer )
	{
		if ( !LogMaterialNamesOnce || _loggedDrawCalls || !renderer.IsValid() || !renderer.Model.IsValid() )
			return;

		_loggedDrawCalls = true;
		var modelName = renderer.Model.ResourceName ?? renderer.Model.Name ?? "unknown";
		Log.Info( $"[Aimbox M700 Scope Lens] {modelName} draw calls={renderer.Model.MeshCount} material slots={renderer.Materials.Count}" );

		for ( var drawCall = 0; drawCall < renderer.Model.MeshCount; drawCall++ )
		{
			var material = renderer.Model.Materials.ElementAtOrDefault( drawCall );
			var name = material.IsValid() ? material.ResourceName : "(null)";
			var verts = TryGetDrawCallVertices( renderer.Model, drawCall );
			var scope = IsScopeMaterial( name );
			Log.Info(
				$"[Aimbox M700 Scope Lens] drawCall={drawCall} mat='{name}' verts={verts} scopeMat={scope} " +
				$"override={renderer.Materials.HasOverride( drawCall )}" );
		}
	}

	public static void EnsureStockScopeVisible( ModelRenderer renderer )
	{
		if ( !renderer.IsValid() || !renderer.Model.IsValid() || renderer.Model.IsError )
			return;

		LogDrawCalls( renderer );

		var hadHiddenOverride = _scopeMeshHidden;
		_scopeMeshHidden = false;

		for ( var drawCall = 0; drawCall < renderer.Model.MeshCount; drawCall++ )
		{
			if ( !IsScopeDrawCall( renderer, drawCall ) )
				continue;

			renderer.Materials.SetOverride( drawCall, null );
		}

		if ( hadHiddenOverride )
			Log.Info( "[Aimbox M700 Scope Lens] Restored stock integrated scope mesh." );
	}

	public static void Apply( ModelRenderer renderer, bool hideScopeLens, float adsBlend = 1f )
	{
		if ( hideScopeLens )
		{
			if ( !renderer.IsValid() || !renderer.Model.IsValid() || renderer.Model.IsError )
				return;

			LogDrawCalls( renderer );

			var hidden = GetHiddenLensMaterial();
			for ( var drawCall = 0; drawCall < renderer.Model.MeshCount; drawCall++ )
			{
				if ( !IsScopeDrawCall( renderer, drawCall ) )
					continue;

				if ( hidden.IsValid() )
					renderer.Materials.SetOverride( drawCall, hidden );
			}

			if ( !_scopeMeshHidden )
			{
				_scopeMeshHidden = true;
				Log.Info( $"[Aimbox M700 Scope Lens] Hiding integrated scope mesh at blend={adsBlend:F2}." );
			}

			return;
		}

		EnsureStockScopeVisible( renderer );
	}

	static int TryGetDrawCallVertices( Model model, int drawCall )
	{
		var meshInfo = model.MeshInfo;
		if ( meshInfo.Meshes is not { Length: > 0 } meshes )
			return 0;

		var index = 0;
		foreach ( var mesh in meshes )
		{
			if ( mesh.DrawCalls is { Length: > 0 } drawCalls )
			{
				foreach ( var call in drawCalls )
				{
					if ( index == drawCall )
						return call.Vertices;

					index++;
				}

				continue;
			}

			if ( index == drawCall )
				return mesh.Vertices;

			index++;
		}

		return 0;
	}

	static bool IsScopeDrawCall( ModelRenderer renderer, int drawCall )
	{
		var material = renderer.Model.Materials.ElementAtOrDefault( drawCall );
		var materialName = material.IsValid() ? material.ResourceName : "";
		return IsScopeMaterial( materialName );
	}

	static bool IsScopeMaterial( string materialName ) => IsScopeMaterialForDebug( materialName );

	internal static bool IsScopeMaterialForDebug( string materialName )
	{
		if ( string.IsNullOrWhiteSpace( materialName ) )
			return false;

		foreach ( var hint in ScopeMaterialHints )
		{
			if ( materialName.Contains( hint, StringComparison.OrdinalIgnoreCase ) )
				return true;
		}

		return false;
	}

	static Material GetHiddenLensMaterial()
	{
		if ( _hiddenLensMaterial.IsValid() )
			return _hiddenLensMaterial;

		Material source = default;
		foreach ( var path in AimboxOpticLensPresentation.HiddenLensMaterialSources )
		{
			var loaded = Material.Load( path );
			if ( !loaded.IsValid() || loaded.IsError )
				continue;

			source = loaded;
			break;
		}

		if ( !source.IsValid() )
			source = Material.FromShader( "shaders/sprite/sprite.shader" );

		if ( !source.IsValid() || source.IsError )
			return default;

		_hiddenLensMaterial = source.CreateCopy( "aimbox_hidden_m700_scope_lens" );
		_hiddenLensMaterial.SetFeature( "F_TRANSLUCENT", 1 );
		_hiddenLensMaterial.Set( "g_flOpacityScale", 0f );
		_hiddenLensMaterial.Set( "g_vColor", new Vector4( 0f, 0f, 0f, 0f ) );
		return _hiddenLensMaterial;
	}
}
