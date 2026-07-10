using System.Collections.Generic;

namespace Sandbox;

/// <summary>Loads and cycles main-menu hero stills for <see cref="ThornsMainMenuUI"/>.</summary>
public static class ThornsMainMenuHeroArt
{
	static readonly List<Texture> Loaded = new();
	static readonly HashSet<string> TriedPaths = new( StringComparer.OrdinalIgnoreCase );

	static int _slideIndex;
	static float _nextSlideTime;
	static bool _anyLoaded;

	public static bool HasSlides => _anyLoaded;

	public static Texture CurrentSlide =>
		Loaded.Count > 0 ? Loaded[_slideIndex % Loaded.Count] : default;

	public static void Reset()
	{
		Loaded.Clear();
		TriedPaths.Clear();
		_slideIndex = 0;
		_nextSlideTime = 0f;
		_anyLoaded = false;
	}

	public static void EnsureLoaded( IReadOnlyList<string> paths )
	{
		if ( paths is null || paths.Count == 0 )
			paths = ThornsMainMenuPresentation.DefaultHeroTexturePaths;

		foreach ( var path in paths )
		{
			if ( string.IsNullOrWhiteSpace( path ) || TriedPaths.Contains( path ) )
				continue;

			TriedPaths.Add( path );
			var tex = Texture.Load( path.Trim() );
			if ( !IsUsable( tex ) )
				continue;

			Loaded.Add( tex );
			_anyLoaded = true;
		}
	}

	public static void TickSlides( float intervalSeconds )
	{
		if ( Loaded.Count <= 1 )
			return;

		if ( Time.Now < _nextSlideTime )
			return;

		_nextSlideTime = Time.Now + MathF.Max( 2f, intervalSeconds );
		_slideIndex = ( _slideIndex + 1 ) % Loaded.Count;
	}

	public static Texture TryLoadOptional( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return default;

		var tex = Texture.Load( path.Trim() );
		return IsUsable( tex ) ? tex : default;
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
