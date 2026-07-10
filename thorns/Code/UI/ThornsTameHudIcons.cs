#nullable disable

using System.Collections.Generic;
using Sandbox.UI;

namespace Sandbox;

/// <summary>
/// Tames tab portrait PNGs under <c>Assets/textures/ui/tames/</c> (logical <c>textures/ui/tames/&lt;species&gt;.png</c>),
/// e.g. <c>wolf.png</c>, <c>deer.png</c> — filename stem is lowercased <see cref="ThornsWildlifeSpeciesKind"/>.
/// </summary>
public static class ThornsTameHudIcons
{
	const int PortraitCacheGeneration = 1;
	static readonly Dictionary<string, Texture> _texByStem = new( StringComparer.OrdinalIgnoreCase );
	static readonly HashSet<string> _stemProbeMissed = new( StringComparer.OrdinalIgnoreCase );
	static int _cacheGenApplied = -1;

	static void EnsureCacheGeneration()
	{
		if ( _cacheGenApplied == PortraitCacheGeneration )
			return;
		_texByStem.Clear();
		_cacheGenApplied = PortraitCacheGeneration;
	}

	public static string PortraitLogicalPath( ThornsWildlifeSpeciesKind kind ) =>
		$"textures/ui/tames/{kind.ToString().ToLowerInvariant()}.png";

	static IEnumerable<string> ExpandCandidatesForFile( string file )
	{
		yield return "Assets/textures/ui/tames/" + file;
		yield return "assets/textures/ui/tames/" + file;
		var logical = $"textures/ui/tames/{file}";
		yield return logical;
		yield return "assets/" + logical;
		yield return "Assets/" + logical;
	}

	static bool IsUsable( Texture t ) =>
		t is not null && !t.IsError
		    && !ReferenceEquals( t, Texture.Invalid )
		    && !ReferenceEquals( t, Texture.Transparent )
		    && t.Width > 0 && t.Height > 0;

	/// <summary>Load portrait for <paramref name="kind"/>; cache key is lowercased species stem.</summary>
	public static bool TryGetPortraitTexture( ThornsWildlifeSpeciesKind kind, out Texture tex )
	{
		tex = null;
		var stem = kind.ToString().ToLowerInvariant();
		EnsureCacheGeneration();

		if ( _texByStem.TryGetValue( stem, out var cached ) && IsUsable( cached ) )
		{
			tex = cached;
			return true;
		}

		if ( _stemProbeMissed.Contains( stem ) )
			return false;

		var file = stem + ".png";
		foreach ( var path in ExpandCandidatesForFile( file ) )
		{
			var loaded = Texture.Load( path );
			if ( !IsUsable( loaded ) )
				continue;
			_texByStem[stem] = loaded;
			tex = loaded;
			return true;
		}

		_stemProbeMissed.Add( stem );
		return false;
	}

	/// <summary>Match tames preview + list portrait framing (<c>contain</c>, centered, no repeat).</summary>
	public static bool TryBindPortraitBackground( Panel host, ThornsWildlifeSpeciesKind kind )
	{
		if ( host is null || !host.IsValid() )
			return false;

		if ( !TryGetPortraitTexture( kind, out var tex ) )
		{
			host.Style.BackgroundImage = null;
			return false;
		}

		ApplyPortraitBackground( host, tex );
		return true;
	}

	public static void ApplyPortraitBackground( Panel host, Texture tex )
	{
		if ( host is null || !host.IsValid() )
			return;

		host.Style.BackgroundImage = tex;
		_ = host.Style.Set( "background-size", "contain" );
		_ = host.Style.Set( "background-repeat", "no-repeat" );
		_ = host.Style.Set( "background-position", "center" );
	}
}
