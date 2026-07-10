using System;
using System.Collections.Generic;

namespace Sandbox;

/// <summary>
/// Optional runtime mounts so <c>models/weapons/...</c> paths resolve on join clients (same as editor host).
/// Set package idents on <see cref="ThornsGameManager.WeaponContentPackageIdents"/> from Asset Browser (org.ident).
/// </summary>
public static class ThornsWeaponContentBootstrap
{
	static readonly HashSet<string> MountedPackageIdents = new( StringComparer.OrdinalIgnoreCase );

	public static bool IsPackageMounted( string packageIdent )
	{
		var id = packageIdent?.Trim();
		return !string.IsNullOrEmpty( id ) && MountedPackageIdents.Contains( id );
	}

	public static async Task MountOptionalPackagesAsync( IList<string> packageIdents )
	{
		if ( packageIdents is null || packageIdents.Count == 0 )
			return;

		foreach ( var raw in packageIdents )
		{
			var id = raw?.Trim();
			if ( string.IsNullOrEmpty( id ) )
				continue;

			if ( MountedPackageIdents.Contains( id ) )
				continue;

			try
			{
				// `true` — allow the client to pull the package if it is not on disk (required for many joiners).
				var pkg = await Package.Fetch( id, true );
				if ( pkg is null || pkg.Revision is null )
				{
					continue;
				}

				await pkg.MountAsync();
				MountedPackageIdents.Add( id );
			}
			catch ( Exception )
			{
			}
		}
	}
}
