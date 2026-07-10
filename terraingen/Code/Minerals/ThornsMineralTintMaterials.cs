namespace Terraingen.Minerals;

/// <summary>Tinted material instances so stone and ore read clearly on shared scatter meshes.</summary>
static class ThornsMineralTintMaterials
{
	static readonly Dictionary<string, (Material Stone, Material Ore)> Cache = new( StringComparer.OrdinalIgnoreCase );

	const string OreBaseMaterialPath = "materials/building_materials/metal.vmat";

	public static Material Get( Model model, MineralKind kind, ThornsMineralConfig config )
	{
		if ( !model.IsValid )
			return default;

		var key = string.IsNullOrWhiteSpace( model.ResourcePath ) ? model.GetHashCode().ToString() : model.ResourcePath;
		if ( !Cache.TryGetValue( key, out var pair ) || !pair.Stone.IsValid || !pair.Ore.IsValid )
		{
			var stoneBase = ResolveBaseMaterial( model );
			var oreBase = ResolveOreBaseMaterial( model );
			pair = (
				CreateTinted( stoneBase, config.StoneTint, metal: false ),
				CreateTinted( oreBase, config.OreTint, metal: true ) );
			Cache[key] = pair;
		}

		return kind == MineralKind.Ore ? pair.Ore : pair.Stone;
	}

	public static void ClearCache() => Cache.Clear();

	static Material ResolveBaseMaterial( Model model )
	{
		foreach ( var material in model.Materials )
		{
			if ( material.IsValid )
				return material;
		}

		var loaded = Material.Load( "materials/default/default.vmat" );
		return loaded.IsValid ? loaded : Material.FromShader( "shaders/complex.shader" );
	}

	static Material ResolveOreBaseMaterial( Model model )
	{
		var metal = Material.Load( OreBaseMaterialPath );
		if ( metal.IsValid )
			return metal;

		return ResolveBaseMaterial( model );
	}

	static Material CreateTinted( Material source, Color tint, bool metal )
	{
		if ( !source.IsValid )
			source = Material.FromShader( "shaders/complex.shader" );

		var copy = source.CreateCopy();
		if ( !copy.IsValid )
			copy = source;

		PushTint( copy, tint, metal );
		return copy;
	}

	static void PushTint( Material material, Color tint, bool metal )
	{
		if ( !material.IsValid )
			return;

		material.Set( "g_flModelTintAmount", 1f );
		material.Set( "g_vColorTint", tint );
		material.Attributes?.Set( "g_flModelTintAmount", 1f );
		material.Attributes?.Set( "g_vColorTint", tint );

		if ( metal )
		{
			material.Set( "g_flMetalness", 0.42f );
			material.Set( "g_flRoughnessScaleFactor", 0.52f );
			material.Attributes?.Set( "g_flMetalness", 0.42f );
			material.Attributes?.Set( "g_flRoughnessScaleFactor", 0.52f );
		}
	}
}
