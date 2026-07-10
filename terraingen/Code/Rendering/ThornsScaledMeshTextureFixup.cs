namespace Terraingen.Rendering;

using Sandbox;
using Terraingen.Buildings;

/// <summary>
/// Re-applies <see cref="ThornsModelMaterialUvScale"/> when a scaled mesh spawns (host + clients).
/// Add to prefabs or let <see cref="ThornsModelMaterialUvScale.ApplyToHierarchy"/> attach it at runtime.
/// </summary>
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

	public void ApplyNow()
	{
		ThornsFurnitureMaterialDebug.Write(
			$"Fixup ApplyNow go={GameObject.Name} pathOverride={ModelAssetPathOverride} localScale={GameObject.LocalScale}" );
		ThornsModelMaterialUvScale.ApplyToHierarchy( GameObject, IncludeChildren, ModelAssetPathOverride );

		var mr = GameObject.Components.Get<ModelRenderer>();
		if ( mr.IsValid() )
		{
			ThornsFurnitureMaterialDebug.Write(
				$"Fixup done go={GameObject.Name} override={ThornsFurnitureMaterialDebug.DescribeMaterial( mr.MaterialOverride )} "
				+ $"model={mr.Model.Name} modelError={mr.Model.IsError}" );
		}
	}
}
