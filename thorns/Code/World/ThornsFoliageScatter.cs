using System.Collections.Generic;
using Terraingen.Clutter;

namespace Sandbox;

/// <summary>
/// Decorative world foliage (non-harvest). Props are parented under the terrain chunk and rebuilt locally from <see cref="ThornsTerrainNetSpec"/> — not replicated entities.
/// </summary>
public static class ThornsFoliageScatter
{
	public const string DefaultMushroomModelPath = "models/foliage/mushroom.vmdl";
	public const string DefaultGrassModelPath = "models/clutter/grass_common_short.vmdl";

	/// <summary>Decor grass prefix — single <c>.vmdl</c> (not numbered <c>grass1</c> variants).</summary>
	public const string ClutterGrassDecorPrefix = "models/clutter/grass_common_short";

	static ThornsClutterConfig _clutterGrassScaleDefaults;

	/// <summary>Shared scale tuning for <see cref="ComputeClutterGrassUniformScale"/> (matches terraingen clutter).</summary>
	public static ThornsClutterConfig ClutterGrassScaleDefaults =>
		_clutterGrassScaleDefaults ??= new ThornsClutterConfig();

	/// <summary>True when <paramref name="modelPathOrPrefix"/> is <c>grass_common_short</c> (handled only by <see cref="Terraingen.Clutter.ClientGrassRenderer"/>).</summary>
	public static bool IsClutterGrassDecorPath( string modelPathOrPrefix )
	{
		if ( string.IsNullOrWhiteSpace( modelPathOrPrefix ) )
			return false;

		var p = NormalizeDecorModelPathPrefix( modelPathOrPrefix );
		return p.StartsWith( ClutterGrassDecorPrefix, StringComparison.OrdinalIgnoreCase );
	}

	/// <summary>
	/// Retired <c>models/foliage/grass*</c> decor (large numbered blades). Materials often fail after moving to <c>LegacyModels</c> — renders as white untextured meshes.
	/// </summary>
	public static bool IsLegacyTerrainGrassDecorPath( string modelPathOrPrefix )
	{
		if ( string.IsNullOrWhiteSpace( modelPathOrPrefix ) )
			return false;

		var p = NormalizeDecorModelPathPrefix( modelPathOrPrefix );
		if ( p.Contains( "grass_lots", StringComparison.OrdinalIgnoreCase ) )
			return true;

		return p.StartsWith( "models/foliage/grass", StringComparison.OrdinalIgnoreCase );
	}

	static string NormalizeDecorModelPathPrefix( string modelPathOrPrefix )
	{
		var p = modelPathOrPrefix.Trim();
		if ( p.EndsWith( ".vmdl", StringComparison.OrdinalIgnoreCase ) )
			p = p[..^5];

		// Strip trailing variant index so "models/foliage/grass3" still matches.
		while ( p.Length > 0 && char.IsDigit( p[^1] ) )
			p = p[..^1];

		return p;
	}

	/// <summary>Bounds-based uniform scale — same formula as client terraingen clutter grass.</summary>
	public static float ComputeClutterGrassUniformScale( Model model, Random rng ) =>
		ThornsClutterSurface.ComputeUniformScale( model, isGrass: true, ClutterGrassScaleDefaults, rng );

	/// <summary>Neutral foliage tint (no atmosphere color grading).</summary>
	public static readonly Color FoliageBlueGlowTint = Color.White;

	/// <summary>
	/// Applied after bottom-on-ground alignment (and optional terrain offsets) — sinks props slightly into the mesh for grounding.
	/// </summary>
	public const float FoliagePostAlignSinkWorldZ = 20f;

	/// <summary>
	/// Legacy entry point — decorative foliage is <b>not</b> replicated; prefer <see cref="SpawnLocalDecorFoliage"/> parented under the terrain chunk.
	/// </summary>
	public static GameObject SpawnHostDecorProp(
		Scene scene,
		Vector3 worldPosition,
		Rotation worldRotation,
		Vector3 localScale,
		Model model,
		Color? tint = null )
	{
		_ = scene;

		if ( Networking.IsActive && !Networking.IsHost )
			return default;

		if ( !model.IsValid() || model.IsError )
			return default;

		var go = new GameObject( true, "ThornsFoliageDecor" );
		go.NetworkMode = NetworkMode.Never;
		go.WorldPosition = worldPosition;
		go.WorldRotation = worldRotation;
		go.LocalScale = localScale;
		go.Tags.Add( "thorns_foliage" );

		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = model;
		mr.Tint = tint ?? Color.White;
		ThornsModelMaterialUvScale.ApplyForScaledModel( mr, go, model, model.Name );
		var proxy = go.Components.Create<ThornsFoliageCullProxy>();
		proxy.TargetRenderer = mr;

		return go;
	}

	/// <summary>Client-local decorative prop (no <see cref="NetworkMode.Object"/>), parented for chunk lifecycle.</summary>
	public static GameObject SpawnLocalDecorFoliage(
		GameObject parent,
		Vector3 worldPosition,
		Rotation worldRotation,
		Vector3 localScale,
		Model model,
		Color? tint = null )
	{
		if ( !parent.IsValid() || !model.IsValid() || model.IsError )
			return default;

		var go = new GameObject( true, "ThornsFoliageDecor" );
		go.NetworkMode = NetworkMode.Never;
		go.SetParent( parent );
		go.WorldPosition = worldPosition;
		go.WorldRotation = worldRotation;
		go.LocalScale = localScale;
		go.Tags.Add( "thorns_foliage" );

		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = model;
		mr.Tint = tint ?? Color.White;
		ThornsModelMaterialUvScale.ApplyForScaledModel( mr, go, model, model.Name );

		var proxy = go.Components.Create<ThornsFoliageCullProxy>();
		proxy.TargetRenderer = mr;

		return go;
	}

	public static bool TryLoadMushroomModel( string vmdlPath, out Model model ) =>
		TryLoadDecorModel( vmdlPath, out model );

	public static bool TryLoadDecorModel( string vmdlPath, out Model model )
	{
		model = default;
		if ( string.IsNullOrWhiteSpace( vmdlPath ) )
			return false;

		if ( IsLegacyTerrainGrassDecorPath( vmdlPath ) )
			return false;

		var m = Terraingen.Foliage.ThornsFoliageModelCache.Load( vmdlPath.Trim() );
		if ( !m.IsValid() || m.IsError )
			return false;

		model = m;
		return true;
	}

	/// <summary>
	/// Raises/lowers pivot along world +Z so the lowest point of the scaled mesh AABB (after <paramref name="worldRotation"/>)
	/// meets <paramref name="terrainSnappedPivot"/>.z — same idea as wood harvest nodes, but supports arbitrary mushroom tilt.
	/// </summary>
	public static Vector3 AlignPivotWorldPositionMeshBottomOnGround(
		Vector3 terrainSnappedPivot,
		Model model,
		Vector3 localScale,
		Rotation worldRotation )
	{
		if ( !model.IsValid() )
			return terrainSnappedPivot;

		var bb = model.Bounds;
		if ( bb.Size.LengthSquared < 1e-18f )
			return terrainSnappedPivot;

		var mn = bb.Center - bb.Size * 0.5f;
		var mx = bb.Center + bb.Size * 0.5f;

		var minWorldZ = float.MaxValue;
		for ( var corner = 0; corner < 8; corner++ )
		{
			var scaled = new Vector3(
				((corner & 1) == 0 ? mn.x : mx.x) * localScale.x,
				((corner & 2) == 0 ? mn.y : mx.y) * localScale.y,
				((corner & 4) == 0 ? mn.z : mx.z) * localScale.z );
			var w = worldRotation * scaled;
			if ( w.z < minWorldZ )
				minWorldZ = w.z;
		}

		if ( minWorldZ > 1e29f )
			return terrainSnappedPivot;

		return terrainSnappedPivot + Vector3.Up * (-minWorldZ);
	}
}
