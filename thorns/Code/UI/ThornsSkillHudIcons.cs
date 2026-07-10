#nullable disable

using System;
using System.Collections.Generic;
using Sandbox.UI;

namespace Sandbox;

/// <summary>
/// Loads per-skill PNGs from <c>Assets/icons/skills/</c> (see <see cref="IconStem"/> for stems).
/// Tries compact filenames (no underscores, e.g. <c>irongut.png</c>) before snake_case (e.g. <c>iron_gut.png</c>).
/// </summary>
public static class ThornsSkillHudIcons
{
	static readonly Dictionary<string, Texture> _textureByStem = new( StringComparer.OrdinalIgnoreCase );
	/// <summary>Stems we already probed — all candidate paths failed (avoids Texture.Load + log spam every UI tick).</summary>
	static readonly HashSet<string> _stemProbeMissed = new( StringComparer.OrdinalIgnoreCase );
	/// <summary>Categories with no icon file after probing all stems.</summary>
	static readonly HashSet<ThornsUpgradeCategory> _categoryProbeMissed = new();

	/// <summary>Primary file stem (without extension) under <c>icons/skills/</c>.</summary>
	public static string IconStem( ThornsUpgradeCategory c ) => c switch
	{
		ThornsUpgradeCategory.Hydration => "hydration",
		ThornsUpgradeCategory.IronGut => "iron_gut",
		ThornsUpgradeCategory.StrongStomach => "strong_stomach",
		ThornsUpgradeCategory.Weathered => "weathered",
		ThornsUpgradeCategory.ThickHide => "thick_hide",
		ThornsUpgradeCategory.Endurance => "endurance",
		ThornsUpgradeCategory.Ghost => "ghost",
		ThornsUpgradeCategory.Beastmaster => "beastmaster",
		ThornsUpgradeCategory.Hardened => "hardened",
		ThornsUpgradeCategory.LuckyChamber => "lucky_chamber",
		ThornsUpgradeCategory.Lumberjack => "lumberjack",
		ThornsUpgradeCategory.Miner => "miner",
		ThornsUpgradeCategory.Scavenger => "scavenger",
		ThornsUpgradeCategory.Reinforced => "reinforced",
		ThornsUpgradeCategory.Technician => "technician",
		_ => c.ToString().ToLowerInvariant()
	};

	static IEnumerable<string> StemsToTry( ThornsUpgradeCategory c )
	{
		var primary = IconStem( c );
		// Prefer irongut.png / strongstomach.png etc. under Assets/icons/skills/ before snake_case-only filenames.
		var compact = primary.Replace( "_", "" );
		if ( compact.Length > 0
		     && !string.Equals( compact, primary, StringComparison.OrdinalIgnoreCase ) )
			yield return compact;
		yield return primary;
		var lower = c.ToString().ToLowerInvariant();
		if ( !string.Equals( lower, primary, StringComparison.OrdinalIgnoreCase )
		     && !string.Equals( lower, compact, StringComparison.OrdinalIgnoreCase ) )
			yield return lower;
		var pascal = c.ToString();
		if ( !string.Equals( pascal, primary, StringComparison.OrdinalIgnoreCase )
		     && !string.Equals( pascal, lower, StringComparison.OrdinalIgnoreCase )
		     && !string.Equals( pascal, compact, StringComparison.OrdinalIgnoreCase ) )
			yield return pascal;
	}

	static IEnumerable<string> ExpandLoadCandidates( string stem )
	{
		var file = stem + ".png";
		yield return "Assets/icons/skills/" + file;
		yield return "assets/icons/skills/" + file;
		yield return "icons/skills/" + file;
		yield return "Assets/" + file;
		yield return "assets/" + file;
	}

	static bool IsUsableTexture( Texture t )
	{
		if ( t is null || t.IsError )
			return false;
		if ( ReferenceEquals( t, Texture.Invalid ) || ReferenceEquals( t, Texture.Transparent ) )
			return false;
		return t.Width > 0 && t.Height > 0;
	}

	/// <summary>Return a cached texture for this skill, or load the first matching PNG on disk / mount.</summary>
	public static bool TryGetTexture( ThornsUpgradeCategory c, out Texture tex )
	{
		if ( _categoryProbeMissed.Contains( c ) )
		{
			tex = null;
			return false;
		}

		foreach ( var stem in StemsToTry( c ) )
		{
			if ( _stemProbeMissed.Contains( stem ) )
				continue;

			if ( _textureByStem.TryGetValue( stem, out var cached ) && IsUsableTexture( cached ) )
			{
				tex = cached;
				return true;
			}

			foreach ( var path in ExpandLoadCandidates( stem ) )
			{
				var loaded = Texture.Load( path );
				if ( !IsUsableTexture( loaded ) )
					continue;

				_textureByStem[stem] = loaded;
				tex = loaded;
				return true;
			}

			_stemProbeMissed.Add( stem );
		}

		_categoryProbeMissed.Add( c );
		tex = null;
		return false;
	}

	/// <summary>Apply a skill icon to a panel’s <see cref="PanelStyle.BackgroundImage"/> (or clear on failure).</summary>
	public static bool TryBindBackground( ThornsUpgradeCategory c, Panel host )
	{
		if ( host is null || !host.IsValid )
			return false;

		if ( TryGetTexture( c, out var tex ) )
		{
			host.Style.BackgroundImage = tex;
			return true;
		}

		host.Style.BackgroundImage = null;
		return false;
	}
}
