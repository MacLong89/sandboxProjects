using System;
using System.Collections.Generic;
using Sandbox.Diagnostics;
using Terraingen;

namespace Sandbox;

/// <summary>
/// Resolves weapon meshes with <see cref="Model.Load(string)"/> and a dev fallback.
/// Joining clients only see models that are in their mounted content (see <c>terraingen.sbproj</c> <c>PackageReferences</c>).
/// </summary>
public static class ThornsWeaponResourceLoad
{
	static readonly Dictionary<string, Model> LoadedModels = new( StringComparer.OrdinalIgnoreCase );

	public const string FallbackWeaponModelPath = "models/dev/box.vmdl";

	/// <summary>Homegrown bow prop — <c>Assets/models/tools/bow.vmdl</c>.</summary>
	public const string BowModelPath = "models/tools/bow.vmdl";

	/// <summary>Log <see cref="Model.Load"/> outcomes and FP presentation branches. Set <c>false</c> when stable.</summary>
	public static bool FpViewmodelDiagnosticLogs;

	/// <summary>Load a view/world weapon model, or <see cref="FallbackWeaponModelPath"/> if missing or error model.</summary>
	/// <param name="usedFallbackGeometry">True when the dev box was used — skip stock FP animator rigs that expect weapon skeletons.</param>
	public static Model LoadWeaponModelOrFallback(
		string vmdlPath,
		string contextForLog,
		out bool usedFallbackGeometry,
		out bool usedBowStockFpPlaceholder )
	{
		usedBowStockFpPlaceholder = false;
		return LoadWeaponModelOrFallback( vmdlPath, contextForLog, out usedFallbackGeometry );
	}

	/// <inheritdoc cref="LoadWeaponModelOrFallback(string,string,out bool,out bool)"/>
	public static Model LoadWeaponModelOrFallback( string vmdlPath, string contextForLog, out bool usedFallbackGeometry )
	{
		usedFallbackGeometry = false;

		if ( string.IsNullOrWhiteSpace( vmdlPath ) )
		{
			usedFallbackGeometry = true;
			return LoadFallback( contextForLog, "(empty path)" );
		}

		var direct = LoadCachedModel( vmdlPath );
		if ( !IsUsableModel( direct ) && FpViewmodelDiagnosticLogs )
		{
			Log.Warning(
				$"[Thorns][FP-Model] Primary unusable → fallback. path='{vmdlPath}' ctx={contextForLog} valid={direct.IsValid()} error={direct.IsError}" );
		}

		if ( IsUsableModel( direct ) )
			return direct;

		usedFallbackGeometry = true;
		return LoadFallback( contextForLog, vmdlPath );
	}

	public static bool IsBowModelPath( string vmdlPath )
	{
		if ( string.IsNullOrWhiteSpace( vmdlPath ) )
			return false;

		var path = vmdlPath.Trim().Replace( '\\', '/' );
		return string.Equals( path, BowModelPath, StringComparison.OrdinalIgnoreCase )
		       || string.Equals( path, "models/tools/bow", StringComparison.OrdinalIgnoreCase );
	}

	/// <summary>Load the given world <c>.vmdl</c> only if valid — no <see cref="FallbackWeaponModelPath"/> (third-person hides the mesh instead).</summary>
	public static bool TryLoadWeaponWorldModel( string vmdlPath, string contextForLog, out Model worldModel )
	{
		worldModel = default;
		if ( string.IsNullOrWhiteSpace( vmdlPath ) )
			return false;

		var m = LoadCachedModel( vmdlPath );
		if ( !IsUsableModel( m ) )
		{
			Log.Warning( $"[Thorns] {contextForLog}: world model missing or error ('{vmdlPath}')." );
			return false;
		}

		worldModel = m;
		return true;
	}

	static Model LoadCachedModel( string vmdlPath )
	{
		if ( string.IsNullOrWhiteSpace( vmdlPath ) )
			return default;

		var path = vmdlPath.Trim();
		if ( LoadedModels.TryGetValue( path, out var cached ) && IsUsableModel( cached ) )
			return cached;

		var loaded = Model.Load( path );
		if ( IsUsableModel( loaded ) )
			LoadedModels[path] = loaded;

		return loaded;
	}

	static Model LoadFallback( string context, string failed )
	{
		var fb = LoadCachedModel( FallbackWeaponModelPath );
		if ( IsUsableModel( fb ) )
			return fb;

		Log.Error( $"[Thorns] {context}: fallback model failed too (after '{failed}')." );
		return fb;
	}

	static bool IsUsableModel( Model m ) => ThornsModelResourceLoad.IsUsable( m );
}
