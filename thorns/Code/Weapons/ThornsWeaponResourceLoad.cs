using System;
using Sandbox.Diagnostics;

namespace Sandbox;

/// <summary>
/// Resolves weapon meshes with <see cref="Model.Load(string)"/> and a dev fallback.
/// Joining clients only see models that are in their mounted content (see <c>thorns.sbproj</c> <c>PackageReferences</c>
/// and <see cref="ThornsGameManager.WeaponContentPackageIdents"/>); we do not use <see cref="Cloud.Model(string)"/> here
/// so the compiler does not need to resolve Asset Party idents at build time.
/// </summary>
public static class ThornsWeaponResourceLoad
{
	public const string FallbackWeaponModelPath = "models/dev/box.vmdl";

	/// <summary>Log <see cref="Model.Load"/> outcomes and FP presentation branches. Set <c>false</c> when stable.</summary>
	public static bool FpViewmodelDiagnosticLogs;

	/// <summary>Load a view/world weapon model, or <see cref="FallbackWeaponModelPath"/> if missing or error model.</summary>
	/// <param name="usedFallbackGeometry">True when the dev box was used — skip stock FP animator rigs that expect weapon skeletons.</param>
	public static Model LoadWeaponModelOrFallback( string vmdlPath, string contextForLog, out bool usedFallbackGeometry )
	{
		usedFallbackGeometry = false;

		if ( string.IsNullOrWhiteSpace( vmdlPath ) )
		{
			usedFallbackGeometry = true;
			return LoadFallback( contextForLog, "(empty path)" );
		}

		var direct = Model.Load( vmdlPath );
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

	/// <summary>Load the given world <c>.vmdl</c> only if valid — no <see cref="FallbackWeaponModelPath"/> (third-person hides the mesh instead).</summary>
	public static bool TryLoadWeaponWorldModel( string vmdlPath, string contextForLog, out Model worldModel )
	{
		worldModel = default;
		if ( string.IsNullOrWhiteSpace( vmdlPath ) )
			return false;

		var m = Model.Load( vmdlPath );
		if ( !IsUsableModel( m ) )
		{
			Log.Warning( $"[Thorns] {contextForLog}: world model missing or error ('{vmdlPath}')." );
			return false;
		}

		worldModel = m;
		return true;
	}

	static Model LoadFallback( string context, string failed )
	{
		var fb = Model.Load( FallbackWeaponModelPath );
		if ( IsUsableModel( fb ) )
			return fb;

		Log.Error( $"[Thorns] {context}: fallback model failed too (after '{failed}')." );
		return fb;
	}

	static bool IsUsableModel( Model m )
	{
		if ( !m.IsValid() )
			return false;

		if ( m.IsError )
			return false;

		return true;
	}
}
