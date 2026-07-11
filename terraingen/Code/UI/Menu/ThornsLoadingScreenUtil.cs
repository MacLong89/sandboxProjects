namespace Terraingen.UI.Menu;

using Terraingen;

/// <summary>
/// Engine loading overlay — uses the same mounted menu backdrop as the main menu
/// (<see cref="LoadingScreen.Media"/>), not the default blue s&amp;box screen.
/// </summary>
public static class ThornsLoadingScreenUtil
{
	static string _cachedMediaPath;
	static bool _cachedMediaResolved;

	public static void Show( string title, string subtitle = null )
	{
		LoadingScreen.Title = title;
		LoadingScreen.Subtitle = subtitle;
		LoadingScreen.Media = ResolveBackdropMediaPath();
		LoadingScreen.IsVisible = true;
	}

	public static void Dismiss()
	{
		LoadingScreen.Title = null;
		LoadingScreen.Subtitle = null;
		LoadingScreen.Media = null;
		LoadingScreen.IsVisible = false;
	}

	static string ResolveBackdropMediaPath()
	{
		if ( _cachedMediaResolved )
			return _cachedMediaPath;

		_cachedMediaResolved = true;
		foreach ( var path in new[]
		         {
			         ThornsMainMenuBackdrop.DefaultPath,
			         ThornsMainMenuBackdrop.TabMenuPrimaryPath,
			         ThornsMainMenuBackdrop.TabMenuAltPath
		         } )
		{
			if ( string.IsNullOrWhiteSpace( path ) )
				continue;

			foreach ( var attempt in ThornsContentPath.Candidates( path ) )
			{
				if ( !ThornsMountedFiles.Exists( attempt ) )
					continue;

				try
				{
					var tex = Texture.Load( attempt );
					if ( !IsUsable( tex ) )
						continue;

					_cachedMediaPath = attempt;
					return _cachedMediaPath;
				}
				catch
				{
					// Try next candidate path.
				}
			}
		}

		_cachedMediaPath = null;
		return null;
	}

	static bool IsUsable( Texture tex )
	{
		if ( tex is null || tex.IsError )
			return false;

		if ( ReferenceEquals( tex, Texture.Invalid ) || ReferenceEquals( tex, Texture.Transparent ) )
			return false;

		return tex.Width > 0 && tex.Height > 0;
	}
}
