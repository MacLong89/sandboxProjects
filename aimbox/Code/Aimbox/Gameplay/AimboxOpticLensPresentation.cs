namespace Sandbox;

/// <summary>
/// sboxweapons optics use opaque lens meshes. During world-pass ADS we hide only lens draw calls
/// (not housing/reticle) by overriding them with a zero-opacity material copy.
/// </summary>
public static class AimboxOpticLensPresentation
{
	public static bool LogMaterialNamesOnce = false;

	static readonly string[] LensMaterialIncludeHints =
	[
		"glass",
		"rmr_glass",
		"lens",
		"scope_glass",
		"scope_lens",
		"optic_glass"
	];

	static readonly string[] LensMaterialExcludeHints =
	[
		"red_dot",
		"reddot",
		"reticle",
		"_dot",
		"dot_vmat"
	];

	// Lens/reticle planes on sight_holographic — not housing (v_holosight.vmat / holosight_body).
	static readonly string[] HolographicLensMaterialIncludeHints =
	[
		"holosight_glass",
		"holosight_red_dot",
		"holosight_lens"
	];

	internal static readonly string[] HiddenLensMaterialSources =
	[
		"materials/dev/primary_white_trans.vmat",
		"materials/dev/primary_white_emissive_trans.vmat"
	];

	static Material _hiddenLensMaterial;
	static bool _loggedMissingMaterial;
	static readonly HashSet<string> _loggedModelMaterials = [];

	public static void Apply( ModelRenderer renderer, bool hideLens, AimboxOpticLensProfile profile = AimboxOpticLensProfile.Generic )
	{
		if ( !renderer.IsValid() || !renderer.Model.IsValid() || renderer.Model.IsError )
			return;

		LogModelMaterialsOnce( renderer, profile );

		var hidden = hideLens ? GetHiddenLensMaterial() : default;
		var drawCallCount = GetDrawCallCount( renderer );

		for ( var drawCall = 0; drawCall < drawCallCount; drawCall++ )
		{
			if ( !IsLensDrawCall( renderer, drawCall, profile ) )
				continue;

			if ( hideLens && hidden.IsValid() )
				renderer.Materials.SetOverride( drawCall, hidden );
			else
				renderer.Materials.SetOverride( drawCall, null );
		}
	}

	static int GetDrawCallCount( ModelRenderer renderer ) =>
		Math.Max( renderer.Model.MeshCount, renderer.Materials.Count );

	static void LogModelMaterialsOnce( ModelRenderer renderer, AimboxOpticLensProfile profile )
	{
		if ( !LogMaterialNamesOnce )
			return;

		var modelName = renderer.Model.ResourceName ?? renderer.Model.Name ?? "unknown";
		if ( !_loggedModelMaterials.Add( modelName ) )
			return;

		var drawCallCount = GetDrawCallCount( renderer );
		Log.Info( $"[Aimbox Optics] {modelName} draw calls={drawCallCount} material slots={renderer.Materials.Count} profile={profile}" );

		for ( var drawCall = 0; drawCall < drawCallCount; drawCall++ )
		{
			var material = renderer.Model.Materials.ElementAtOrDefault( drawCall );
			var name = material.IsValid() ? material.ResourceName : "(null)";
			var lens = IsLensDrawCall( renderer, drawCall, profile );
			Log.Info(
				$"[Aimbox Optics] {modelName} drawCall={drawCall} mat='{name}' hideLens={lens} " +
				$"override={renderer.Materials.HasOverride( drawCall )}" );
		}
	}

	static bool IsLensDrawCall( ModelRenderer renderer, int drawCall, AimboxOpticLensProfile profile )
	{
		var modelName = renderer.Model.ResourceName ?? renderer.Model.Name ?? "";
		var onHolographicSightModel = IsHolographicSightModel( modelName );

		var material = renderer.Model.Materials.ElementAtOrDefault( drawCall );
		var name = material.IsValid() ? material.ResourceName ?? "" : "";

		if ( profile == AimboxOpticLensProfile.HolographicAttachment )
		{
			if ( onHolographicSightModel )
				return !IsHolographicHousingMaterial( name );

			if ( !string.IsNullOrWhiteSpace( name ) && MatchesHolographicLensMaterial( name ) )
				return true;

			return false;
		}

		if ( !material.IsValid() || string.IsNullOrWhiteSpace( name ) )
			return false;

		foreach ( var exclude in LensMaterialExcludeHints )
		{
			if ( name.Contains( exclude, StringComparison.OrdinalIgnoreCase ) )
				return false;
		}

		foreach ( var hint in LensMaterialIncludeHints )
		{
			if ( name.Contains( hint, StringComparison.OrdinalIgnoreCase ) )
				return true;
		}

		return false;
	}

	static bool IsHolographicSightModel( string modelName ) =>
		modelName.Contains( "sight_holographic", StringComparison.OrdinalIgnoreCase );

	static bool MatchesHolographicLensMaterial( string materialName )
	{
		foreach ( var hint in HolographicLensMaterialIncludeHints )
		{
			if ( materialName.Contains( hint, StringComparison.OrdinalIgnoreCase ) )
				return true;
		}

		return false;
	}

	/// <summary>Metal housing on sight_holographic — keep visible; hide lens/reticle/error slots.</summary>
	static bool IsHolographicHousingMaterial( string materialName )
	{
		if ( string.IsNullOrWhiteSpace( materialName ) )
			return false;

		if ( MatchesHolographicLensMaterial( materialName ) )
			return false;

		if ( materialName.Contains( "holosight_body", StringComparison.OrdinalIgnoreCase ) )
			return true;

		if ( materialName.EndsWith( "/v_holosight.vmat", StringComparison.OrdinalIgnoreCase )
		     || materialName.EndsWith( "v_holosight.vmat", StringComparison.OrdinalIgnoreCase ) )
			return true;

		return materialName.Contains( "holosight", StringComparison.OrdinalIgnoreCase )
		       && !materialName.Contains( "glass", StringComparison.OrdinalIgnoreCase )
		       && !materialName.Contains( "red_dot", StringComparison.OrdinalIgnoreCase )
		       && !materialName.Contains( "lens", StringComparison.OrdinalIgnoreCase );
	}

	public static AimboxOpticLensProfile GetRedDotLensProfile( AimboxAttachmentId attachment ) => attachment switch
	{
		AimboxAttachmentId.HoloSight => AimboxOpticLensProfile.HolographicAttachment,
		_ => AimboxOpticLensProfile.RedDotAttachment
	};

	public static AimboxOpticLensProfile GetM4RedDotLensProfile( AimboxAttachmentId attachment ) =>
		GetRedDotLensProfile( attachment );

	/// <summary>Holo sight ships v_holosight_* materials whose textures often fail to mount — keep lens draw calls hidden.</summary>
	public static bool ShouldAlwaysHideM4RedDotLens( AimboxOpticLensProfile profile ) =>
		profile == AimboxOpticLensProfile.HolographicAttachment;

	/// <summary>Debug — would generic holo/RMR lens hide rule match this material name?</summary>
	internal static bool IsGenericLensMaterialForDebug( string materialName )
	{
		if ( string.IsNullOrWhiteSpace( materialName ) )
			return false;

		foreach ( var exclude in LensMaterialExcludeHints )
		{
			if ( materialName.Contains( exclude, StringComparison.OrdinalIgnoreCase ) )
				return false;
		}

		foreach ( var hint in LensMaterialIncludeHints )
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
		foreach ( var path in HiddenLensMaterialSources )
		{
			var loaded = Material.Load( path );
			if ( !loaded.IsValid() || loaded.IsError )
				continue;

			source = loaded;
			break;
		}

		if ( !source.IsValid() )
		{
			source = Material.FromShader( "shaders/sprite/sprite.shader" );
		}

		if ( !source.IsValid() || source.IsError )
		{
			if ( !_loggedMissingMaterial )
			{
				_loggedMissingMaterial = true;
				Log.Warning( "[Aimbox Optics] Could not create hidden lens material — lenses stay opaque." );
			}

			return default;
		}

		_hiddenLensMaterial = source.CreateCopy( "aimbox_hidden_optic_lens" );
		_hiddenLensMaterial.SetFeature( "F_TRANSLUCENT", 1 );
		_hiddenLensMaterial.Set( "g_flOpacityScale", 0f );
		_hiddenLensMaterial.Set( "g_vColor", new Vector4( 0f, 0f, 0f, 0f ) );
		return _hiddenLensMaterial;
	}
}

public enum AimboxOpticLensProfile
{
	Generic,
	RedDotAttachment,
	HolographicAttachment
}
