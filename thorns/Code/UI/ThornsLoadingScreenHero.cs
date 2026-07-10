namespace Sandbox;

/// <summary>
/// Engine loading overlay (<see cref="LoadingScreen"/>) background using main-menu hero stills.
/// </summary>
public static class ThornsLoadingScreenHero
{
	static string _cachedMediaPath;
	static bool _cachedMediaResolved;

	public static void Show( string title, string subtitle = null )
	{
		LoadingScreen.Title = title;
		LoadingScreen.Subtitle = subtitle;
		LoadingScreen.Media = ResolveHeroMediaPath();
	}

	public static void Clear()
	{
		LoadingScreen.Title = null;
		LoadingScreen.Subtitle = null;
		LoadingScreen.Media = null;
	}

	static string ResolveHeroMediaPath()
	{
		if ( _cachedMediaResolved )
			return _cachedMediaPath;

		_cachedMediaResolved = true;
		foreach ( var path in ThornsMainMenuPresentation.DefaultHeroTexturePaths )
		{
			if ( string.IsNullOrWhiteSpace( path ) )
				continue;

			var trimmed = path.Trim();
			var tex = Texture.Load( trimmed );
			if ( !IsUsable( tex ) )
				continue;

			_cachedMediaPath = trimmed;
			return _cachedMediaPath;
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
