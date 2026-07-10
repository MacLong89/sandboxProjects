using System;
using System.Collections.Generic;

namespace Sandbox;

/// <summary>
/// Optional runtime mounts so <c>models/weapons/...</c> paths resolve on join clients (same as editor host).
/// Set package idents on <see cref="ThornsGameManager.WeaponContentPackageIdents"/> from Asset Browser (org.ident).
/// </summary>
public static class YaWeaponContentBootstrap
{
	public static async Task MountOptionalPackagesAsync( IList<string> packageIdents )
	{
		if ( packageIdents is null || packageIdents.Count == 0 )
			return;

		foreach ( var raw in packageIdents )
		{
			var id = raw?.Trim();
			if ( string.IsNullOrEmpty( id ) )
				continue;

			try
			{
				// `true` — allow the client to pull the package if it is not on disk (required for many joiners).
				var pkg = await Package.Fetch( id, true );
				if ( pkg is null || pkg.Revision is null )
				{
					Log.Warning( $"[YA] Weapon content package not found or no revision: '{id}'" );
					continue;
				}

				await pkg.MountAsync();
				Log.Info( $"[YA] Mounted weapon content package '{id}'." );
			}
			catch ( Exception e )
			{
				Log.Warning( $"[YA] Mount package '{id}' failed: {e.Message}" );
			}
		}
	}
}
