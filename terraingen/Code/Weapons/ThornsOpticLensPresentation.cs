namespace Sandbox;

using Terraingen.Combat.Attachments;

/// <summary>
/// sboxweapons optics use opaque lens meshes. During world-pass ADS we hide only lens draw calls
/// (not housing/reticle) by overriding them with a zero-opacity material copy.
/// </summary>
public static class ThornsOpticLensPresentation
{
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

	public static void Apply( ModelRenderer renderer, bool hideLens, ThornsOpticLensProfile profile = ThornsOpticLensProfile.Generic )
	{
		if ( !renderer.IsValid() || !renderer.Model.IsValid() || renderer.Model.IsError )
			return;

		var hidden = hideLens ? GetHiddenLensMaterial() : default;
		var drawCallCount = Math.Max( renderer.Model.MeshCount, renderer.Materials.Count );

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

	static bool IsLensDrawCall( ModelRenderer renderer, int drawCall, ThornsOpticLensProfile profile )
	{
		var modelName = renderer.Model.ResourceName ?? renderer.Model.Name ?? "";
		var onHolographicSightModel = modelName.Contains( "sight_holographic", StringComparison.OrdinalIgnoreCase );

		var material = renderer.Model.Materials.ElementAtOrDefault( drawCall );
		var name = material.IsValid() ? material.ResourceName ?? "" : "";

		if ( profile == ThornsOpticLensProfile.HolographicAttachment )
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

	static bool MatchesHolographicLensMaterial( string materialName )
	{
		foreach ( var hint in HolographicLensMaterialIncludeHints )
		{
			if ( materialName.Contains( hint, StringComparison.OrdinalIgnoreCase ) )
				return true;
		}

		return false;
	}

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

	public static ThornsOpticLensProfile GetRedDotLensProfile( ThornsAttachmentId attachment ) => attachment switch
	{
		ThornsAttachmentId.HoloSight => ThornsOpticLensProfile.HolographicAttachment,
		_ => ThornsOpticLensProfile.RedDotAttachment
	};

	public static bool ShouldAlwaysHideM4RedDotLens( ThornsOpticLensProfile profile ) =>
		profile == ThornsOpticLensProfile.HolographicAttachment;

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
			source = Material.FromShader( "shaders/sprite/sprite.shader" );

		if ( !source.IsValid() || source.IsError )
		{
			if ( !_loggedMissingMaterial )
			{
				_loggedMissingMaterial = true;
				Log.Warning( "[Thorns Optics] Could not create hidden lens material — lenses stay opaque." );
			}

			return default;
		}

		_hiddenLensMaterial = source.CreateCopy( "thorns_hidden_optic_lens" );
		_hiddenLensMaterial.SetFeature( "F_TRANSLUCENT", 1 );
		_hiddenLensMaterial.Set( "g_flOpacityScale", 0f );
		_hiddenLensMaterial.Set( "g_vColor", new Vector4( 0f, 0f, 0f, 0f ) );
		return _hiddenLensMaterial;
	}
}

public enum ThornsOpticLensProfile
{
	Generic,
	RedDotAttachment,
	HolographicAttachment
}
