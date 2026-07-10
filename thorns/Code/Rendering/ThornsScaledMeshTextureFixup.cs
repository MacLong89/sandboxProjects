namespace Sandbox;

/// <summary>
/// Re-applies <see cref="ThornsModelMaterialUvScale"/> when a scaled mesh spawns (host + clients).
/// Add to prefabs or let <see cref="ThornsModelMaterialUvScale.ApplyToHierarchy"/> attach it at runtime.
/// </summary>
[Title( "Thorns — Scaled Mesh Texture Fixup" )]
[Category( "Thorns/Rendering" )]
[Icon( "texture" )]
public sealed class ThornsScaledMeshTextureFixup : Component
{
	[Property] public bool IncludeChildren { get; set; } = true;

	[Property] public string ModelAssetPathOverride { get; set; } = "";

	protected override void OnStart()
	{
		if ( !Game.IsPlaying )
			return;

		ApplyNow();
	}

	public void ApplyNow() =>
		ThornsModelMaterialUvScale.ApplyToHierarchy( GameObject, IncludeChildren, ModelAssetPathOverride );
}
