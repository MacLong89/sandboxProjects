using System;

namespace Sandbox;

/// <summary>
/// Procedural terrain building pieces are <see cref="NetworkMode.Object"/> with runtime-built <see cref="Model"/>s.
/// Host mesh materials often do not replicate to joiners (ERROR / orange box); rebuild from <see cref="ThornsBuildingVisuals"/> on every peer in <see cref="OnStart"/>.
/// </summary>
[Title( "Thorns — Proc building piece (client mesh fixup)" )]
[Category( "Thorns/Building" )]
[Icon( "foundation" )]
public sealed class ThornsProcBuildingPieceFixup : Component
{
	protected override void OnStart()
	{
		if ( !Game.IsPlaying )
			return;

		if ( !TryParsePieceName( GameObject.Name, out var materialSlug, out var defId ) )
			return;

		var mr = Components.Get<ModelRenderer>( FindMode.EnabledInSelf );
		if ( !mr.IsValid() )
			return;

		var model = ThornsBuildingVisuals.StructureModel( defId, materialSlug );
		mr.Model = model;
		mr.Tint = Color.White;
		ThornsBuildingVisuals.ApplyProcBuildingPieceUvScale( mr, GameObject, materialSlug, defId );
		var pieceMat = mr.MaterialOverride;
		if ( !pieceMat.IsValid() )
			pieceMat = ThornsBuildingVisuals.ResolveProcBuildingPieceMaterial( defId, materialSlug );
		if ( pieceMat.IsValid() )
			mr.MaterialOverride = pieceMat;

		var mc = Components.Get<ModelCollider>( FindMode.EnabledInSelf );
		if ( mc.IsValid() )
			mc.Model = model;
	}

	/// <summary>Name format: <c>Proc_{materialIndex:D2}_{defId}</c> e.g. <c>Proc_07_brick_wood_wall</c> is invalid — <c>Proc_07_wood_wall</c>.</summary>
	public static bool TryParsePieceName( string name, out string materialSlug, out string defId )
	{
		materialSlug = ThornsProcBuildingMaterialPalette.AllSlugs[0];
		defId = "";
		const string prefix = "Proc_";
		if ( string.IsNullOrEmpty( name ) || !name.StartsWith( prefix, StringComparison.Ordinal ) )
			return false;

		var rest = name[prefix.Length..];
		var u = rest.IndexOf( '_' );
		if ( u <= 0 )
			return false;

		if ( !int.TryParse( rest[..u], out var materialIndex ) )
			return false;

		if ( materialIndex < 0 || materialIndex >= ThornsProcBuildingMaterialPalette.SlugCount )
			return false;

		materialSlug = ThornsProcBuildingMaterialPalette.SlugFromIndex( materialIndex );
		defId = rest[( u + 1 )..];
		return !string.IsNullOrEmpty( defId );
	}
}
