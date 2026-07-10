namespace Terraingen.GameData;

using System;

/// <summary>Item lookup and FP viewmodel pose helpers (thorns-compatible surface for <see cref="ThornsViewModelController"/>).</summary>
public static class ThornsItemRegistry
{
	public static readonly Vector3 FpViewmodelRootLocalScaleOne = ThornsFpItemHelpers.FpViewmodelRootLocalScaleOne;

	public static readonly Vector3 FpHarvestAxePickaxeViewmodelRootOffset = ThornsFpItemHelpers.FpHarvestAxePickaxeViewmodelRootOffset;

	public static readonly Vector3 FpHarvestAxeViewmodelRootEulerDegrees = ThornsFpItemHelpers.FpHarvestAxeViewmodelRootEulerDegrees;

	public static readonly Vector3 FpHarvestPickaxeViewmodelRootEulerDegrees = ThornsFpItemHelpers.FpHarvestPickaxeViewmodelRootEulerDegrees;

	public static readonly Vector3 FpHarvestStonePickaxeViewmodelRootEulerDegrees = ThornsFpItemHelpers.FpHarvestStonePickaxeViewmodelRootEulerDegrees;

	public static readonly Vector3 FpHarvestToolViewmodelRootScale = ThornsFpItemHelpers.FpHarvestToolViewmodelRootScale;

	public static readonly Vector3 FpMedkitViewmodelRootOffset = ThornsFpItemHelpers.FpMedkitViewmodelRootOffset;

	public static bool TryGet( string itemId, out ThornsItemDefinition def )
	{
		ThornsDefinitionRegistry.EnsureInitialized();
		def = ThornsDefinitionRegistry.GetItem( itemId ?? "" );
		return def is not null && !string.IsNullOrWhiteSpace( def.Id );
	}

	public static bool IsUsableConsumable( ThornsItemDefinition def ) =>
		def is not null
		&& def.ItemType == ThornsItemType.Consumable
		&& def.Category == ThornsItemCategory.Consumable;

	public static Vector3 ResolveFpViewmodelRootScale( Vector3 scale ) =>
		scale.LengthSquared < 1e-8f ? FpViewmodelRootLocalScaleOne : scale;

	public static bool IsHarvestToolViewModelPath( string modelPath )
	{
		if ( string.IsNullOrWhiteSpace( modelPath ) )
			return false;

		return modelPath.Trim().Replace( '\\', '/' )
			.StartsWith( "models/tools/", StringComparison.OrdinalIgnoreCase );
	}

	public static bool UsesHarvestAxeOrPickaxeFpPose( in ThornsItemDefinition def ) =>
		def.HarvestToolKind is ThornsHarvestToolKind.Axe or ThornsHarvestToolKind.Pickaxe;

	public static Vector3 ComposeFpHarvestToolViewmodelOffset( in ThornsItemDefinition def )
	{
		if ( UsesHarvestAxeOrPickaxeFpPose( in def ) )
			return FpHarvestAxePickaxeViewmodelRootOffset;

		return def.FpViewmodelRootLocalOffset;
	}

	public static Vector3 ResolveFpHarvestToolViewmodelEulerDegrees( in ThornsItemDefinition def )
	{
		if ( string.Equals( def.Id, "stone_pickaxe", StringComparison.OrdinalIgnoreCase ) )
			return FpHarvestStonePickaxeViewmodelRootEulerDegrees;

		return def.HarvestToolKind switch
		{
			ThornsHarvestToolKind.Axe => FpHarvestAxeViewmodelRootEulerDegrees,
			ThornsHarvestToolKind.Pickaxe => FpHarvestPickaxeViewmodelRootEulerDegrees,
			_ => def.FpViewmodelRootLocalEulerDegrees
		};
	}

	public static Vector3 ResolveFpHarvestToolViewmodelScale( in ThornsItemDefinition def )
	{
		var baseScale = ResolveFpViewmodelRootScale( def.FpViewmodelRootLocalScale );
		return UsesHarvestAxeOrPickaxeFpPose( in def )
			? baseScale * FpHarvestToolViewmodelRootScale.x
			: baseScale;
	}

	public static bool IsBowViewModelPath( string modelPath )
		=> ThornsWeaponResourceLoad.IsBowModelPath( modelPath );

	/// <summary>Custom item meshes (bow, tools, held consumables) — not Facepunch stock <c>v_*</c> rigs.</summary>
	public static bool UsesDirectFpViewmodelScale( string modelPath )
		=> IsBowViewModelPath( modelPath ) || IsHarvestToolViewModelPath( modelPath );

	static bool FpViewModelPathEquals( string a, string b )
	{
		if ( string.IsNullOrWhiteSpace( a ) || string.IsNullOrWhiteSpace( b ) )
			return false;

		var na = a.Trim().Replace( '\\', '/' );
		var nb = b.Trim().Replace( '\\', '/' );
		return string.Equals( na, nb, StringComparison.OrdinalIgnoreCase );
	}

	/// <summary>Canonical FP pose from item catalog — bypasses stale <see cref="ThornsFpPresentation"/> mirror fields.</summary>
	public static bool TryResolveFpViewmodelPoseForModelPath(
		string loadedModelPath,
		out Vector3 offset,
		out Vector3 scale,
		out Vector3 eulerDegrees )
	{
		offset = default;
		scale = FpViewmodelRootLocalScaleOne;
		eulerDegrees = default;

		if ( string.IsNullOrWhiteSpace( loadedModelPath ) )
			return false;

		if ( IsBowViewModelPath( loadedModelPath ) )
		{
			offset = ThornsFpItemHelpers.FpBowViewmodelRootOffset;
			scale = ResolveFpViewmodelRootScale( ThornsFpItemHelpers.FpBowViewmodelRootScale );
			eulerDegrees = ThornsFpItemHelpers.FpBowViewmodelRootEulerDegrees;
			return true;
		}

		ThornsDefinitionRegistry.EnsureInitialized();
		foreach ( var pair in ThornsDefinitionRegistry.AllItems )
		{
			var def = pair.Value;
			if ( def is null || string.IsNullOrWhiteSpace( def.ViewModelAsset ) )
				continue;

			if ( !FpViewModelPathEquals( def.ViewModelAsset, loadedModelPath ) )
				continue;

			if ( def.ItemType == ThornsItemType.Tool )
			{
				offset = ComposeFpHarvestToolViewmodelOffset( in def );
				scale = ResolveFpHarvestToolViewmodelScale( in def );
				eulerDegrees = ResolveFpHarvestToolViewmodelEulerDegrees( in def );
				return true;
			}

			if ( IsUsableConsumable( def ) )
			{
				offset = def.FpViewmodelRootLocalOffset;
				scale = ResolveFpViewmodelRootScale( def.FpViewmodelRootLocalScale );
				eulerDegrees = def.FpViewmodelRootLocalEulerDegrees;
				return true;
			}
		}

		return false;
	}
}
