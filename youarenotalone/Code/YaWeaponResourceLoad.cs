using System;

namespace Sandbox;

/// <summary>
/// Resolves weapon meshes with <see cref="Model.Load(string)"/> and a dev fallback.
/// Joining clients only see models that are in their mounted content (see <c>thorns.sbproj</c> <c>PackageReferences</c>
/// and <see cref="ThornsGameManager.WeaponContentPackageIdents"/>); we do not use <see cref="Cloud.Model(string)"/> here
/// so the compiler does not need to resolve Asset Party idents at build time.
/// </summary>
public static class YaWeaponResourceLoad
{
	public const string FallbackWeaponModelPath = "models/dev/box.vmdl";

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
		if ( IsUsableModel( direct ) )
			return direct;

		Log.Warning(
			$"[YA] {contextForLog}: could not load '{vmdlPath}' — add PackageReferences / mount packages, or copy assets into this game. " +
			$"Using fallback '{FallbackWeaponModelPath}'." );
		usedFallbackGeometry = true;
		return LoadFallback( contextForLog, vmdlPath );
	}

	static Model LoadFallback( string context, string failed )
	{
		var fb = Model.Load( FallbackWeaponModelPath );
		if ( IsUsableModel( fb ) )
			return fb;

		Log.Error( $"[YA] {context}: fallback model failed too (after '{failed}')." );
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
