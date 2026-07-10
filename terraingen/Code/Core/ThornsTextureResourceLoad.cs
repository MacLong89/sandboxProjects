namespace Terraingen;

/// <summary>Texture presence checks for materials that reference loose PNG paths.</summary>
public static class ThornsTextureResourceLoad
{
	public const string CloudPuffAlbedoPath = "materials/skybox/cloud_puff_rgba.png";
	public const string CloudPuffAlphaPath = "materials/skybox/cloud_puff_trans.png";

	public static readonly (string Albedo, string Alpha)[] CloudVariantPaths =
	{
		( "materials/skybox/cloud_puff_rgba.png", "materials/skybox/cloud_puff_trans.png" ),
		( "materials/skybox/cloud_puff_rgba_02.png", "materials/skybox/cloud_puff_trans_02.png" ),
		( "materials/skybox/cloud_puff_rgba_03.png", "materials/skybox/cloud_puff_trans_03.png" ),
	};

	public static readonly string[] CloudVariantMaterialPaths =
	{
		"materials/skybox/thorns_cloud_sprite.vmat",
		"materials/skybox/thorns_cloud_sprite_02.vmat",
		"materials/skybox/thorns_cloud_sprite_03.vmat",
	};
	public const string TreeLodPinePath = "materials/foliage/pine.png";
	public const string TreeLodAspenPath = "materials/foliage/birch.png";
	public const string TreeLodOakPath = "materials/foliage/oak.png";

	public static bool Exists( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return false;

		if ( ThornsMountedFiles.Exists( path ) )
			return true;

		try
		{
			var tex = Texture.Load( ThornsContentPath.Normalize( path ) );
			return tex is not null && tex.IsValid;
		}
		catch
		{
			return false;
		}
	}

	public static bool AreCloudTexturesReady() => TryLoadCloudVariant( 0, out _, out _, out _ );

	public static int CountReadyCloudVariants()
	{
		var count = 0;
		for ( var i = 0; i < CloudVariantPaths.Length; i++ )
		{
			if ( TryLoadCloudVariant( i, out _, out _, out _ ) )
				count++;
		}

		return count;
	}

	public static bool TryLoadCloudVariant( int index, out Texture albedo, out Texture alpha, out string detail )
	{
		albedo = default;
		alpha = default;
		detail = "invalid index";

		if ( index < 0 || index >= CloudVariantPaths.Length )
			return false;

		var (albedoPath, alphaPath) = CloudVariantPaths[index];
		if ( !TryLoadTexture( albedoPath, out albedo, out var albedoDetail ) )
		{
			detail = albedoDetail;
			return false;
		}

		if ( !TryLoadTexture( alphaPath, out alpha, out var alphaDetail ) )
		{
			detail = alphaDetail;
			return false;
		}

		detail = $"{albedoPath} + {alphaPath}";
		return true;
	}

	public static bool TryLoadCloudTexture( out Texture texture, out string detail ) =>
		TryLoadTexture( CloudPuffAlbedoPath, out texture, out detail );

	static bool TryLoadTexture( string path, out Texture texture, out string detail )
	{
		texture = default;
		detail = path;

		try
		{
			texture = Texture.Load( ThornsContentPath.Normalize( path ) );
		}
		catch ( Exception ex )
		{
			detail = $"{path} load threw: {ex.Message}";
			return false;
		}

		if ( texture is null || !texture.IsValid )
		{
			detail = $"{path} Texture.Load returned invalid texture.";
			return false;
		}

		if ( texture.Width <= 0 || texture.Height <= 0 )
		{
			detail = $"{path} loaded but has zero dimensions ({texture.Width}x{texture.Height}). PNG may be corrupt.";
			return false;
		}

		detail = $"{path} ok {texture.Width}x{texture.Height}";
		return true;
	}

	public static bool IsMaterialUsable( Material material, string colorTexturePath = null )
	{
		if ( material is null || !material.IsValid() )
			return false;

		try
		{
			if ( material.Shader is not null && material.Shader.IsValid() )
			{
				var shaderName = material.Shader.ResourceName ?? "";
				if ( shaderName.Contains( "error", StringComparison.OrdinalIgnoreCase ) )
					return false;
			}
		}
		catch
		{
			return false;
		}

		if ( !string.IsNullOrWhiteSpace( colorTexturePath ) )
			return MaterialHasBoundTexture( material, colorTexturePath );

		return MaterialHasAnyColorTexture( material );
	}

	public static bool IsMaterialUsable( Material material, Texture expectedTexture )
	{
		if ( material is null || !material.IsValid() )
			return false;

		if ( expectedTexture is null || !expectedTexture.IsValid )
			return MaterialHasAnyColorTexture( material );

		try
		{
			if ( material.Shader is not null && material.Shader.IsValid() )
			{
				var shaderName = material.Shader.ResourceName ?? "";
				if ( shaderName.Contains( "error", StringComparison.OrdinalIgnoreCase ) )
					return false;
			}
		}
		catch
		{
			return false;
		}

		return MaterialHasBoundTexture( material, expectedTexture );
	}

	static bool MaterialHasAnyColorTexture( Material material )
	{
		try
		{
			var tex = GetBoundColorTexture( material );
			return tex is not null && tex.IsValid;
		}
		catch
		{
			return false;
		}
	}

	static bool MaterialHasBoundTexture( Material material, string colorTexturePath )
	{
		if ( !Exists( colorTexturePath ) )
			return false;

		try
		{
			var expected = Texture.Load( ThornsContentPath.Normalize( colorTexturePath ) );
			if ( expected is null || !expected.IsValid )
				return false;

			return MaterialHasBoundTexture( material, expected );
		}
		catch
		{
			return false;
		}
	}

	static bool MaterialHasBoundTexture( Material material, Texture expectedTexture )
	{
		try
		{
			var bound = GetBoundColorTexture( material );
			if ( bound is not null && bound.IsValid )
			{
				if ( ReferenceEquals( bound, expectedTexture ) )
					return true;

				return bound.Width == expectedTexture.Width
				       && bound.Height == expectedTexture.Height;
			}

			// Runtime Material.Set() bindings are not always visible via GetTexture().
			return expectedTexture.IsValid;
		}
		catch
		{
			return false;
		}
	}

	static Texture GetBoundColorTexture( Material material )
	{
		return material.GetTexture( "TextureColor" )
		       ?? material.GetTexture( "CloudTexture" )
		       ?? material.GetTexture( "g_tColor" )
		       ?? material.GetTexture( "color" );
	}
}
