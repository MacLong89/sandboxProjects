using System;
using System.Collections.Generic;
using System.IO;
using Sandbox.UI;

namespace Sandbox;

/// <summary>
/// Resolves HUD slot texture paths (<see cref="Texture.Load"/>). Author PNGs under <c>Assets/textures/ui/item_icons/</c> (logical <c>textures/ui/item_icons/&lt;name&gt;.png</c>); published games need <c>Resources</c> in <c>thorns.sbproj</c> so those files ship.
/// Prefer <see cref="ThornsItemRegistry.ThornsItemDefinition.HudIconTexture"/> (basename may differ from item id, e.g. leather.png for leather_scrap);
/// otherwise use <c>textures/ui/item_icons/&lt;itemId&gt;.png</c> for non-weapons. <c>.tools/_gen_item_icons.ps1</c> only creates missing placeholders (never overwrites existing PNGs).
/// </summary>
/// <remarks>
/// Live <c>.vmdl</c> renders in hotbar slots would need a per-slot <see cref="Sandbox.UI.ScenePanel"/> / render target setup and are not built-in —
/// authored 2D icons (offline bake from the weapon mesh) stay cheap and predictable.
/// <para>
/// Default <c>item_icons/&lt;weapon&gt;.png</c> files from <c>_gen_item_icons.ps1</c> are text badges (e.g. SG); toolbar skips those so slots use the same unicode glyphs as inventory until <see cref="ThornsItemRegistry.ThornsItemDefinition.HudIconTexture"/> is set.
/// </para>
/// </remarks>
public static class ThornsItemHudIcons
{
	static readonly Dictionary<string, Texture> _toolbarIconTextureByPath = new( StringComparer.OrdinalIgnoreCase );

	/// <summary>Bump when load candidate order changes so cached <see cref="Texture"/> entries are not reused across algorithm fixes.</summary>
	const int HudIconCacheGeneration = 9;

	static int _hudIconCacheGenerationApplied = -1;

	static void EnsureHudIconCacheGeneration()
	{
		if ( _hudIconCacheGenerationApplied == HudIconCacheGeneration )
			return;
		_toolbarIconTextureByPath.Clear();
		_hudIconCacheGenerationApplied = HudIconCacheGeneration;
	}

	public static string ResolveLoadPath( ThornsItemRegistry.ThornsItemDefinition def ) =>
		ResolveLoadPath( def, def?.Id );

	/// <summary>Registry row when known; otherwise <paramref name="itemId"/> (e.g. inspect before sync).</summary>
	public static string ResolveLoadPath( ThornsItemRegistry.ThornsItemDefinition def, string itemId )
	{
		if ( def is not null )
		{
			if ( !string.IsNullOrWhiteSpace( def.HudIconTexture ) )
				return NormalizeItemHudIconLogicalPath( def.HudIconTexture.Trim() );

			if ( !string.IsNullOrWhiteSpace( def.Id ) )
			{
				if ( def.ItemType == ThornsItemType.Weapon )
					return "";

				return ResolveItemIdIconPath( def.Id );
			}
		}

		return ResolveItemIdIconPath( itemId );
	}

	static string ResolveItemIdIconPath( string itemId )
	{
		if ( string.IsNullOrWhiteSpace( itemId ) )
			return "";

		itemId = itemId.Trim();
		if ( itemId.EndsWith( "_kit", StringComparison.OrdinalIgnoreCase ) )
		{
			var baseName = itemId[..^4];
			if ( !string.IsNullOrEmpty( baseName ) )
				return NormalizeItemHudIconLogicalPath( $"textures/ui/item_icons/{baseName}.png" );
		}

		return NormalizeItemHudIconLogicalPath( $"textures/ui/item_icons/{itemId}.png" );
	}

	/// <summary>Single on-disk root: <c>Assets/textures/ui/item_icons/&lt;basename&gt;</c>. Strips legacy <c>icons/...</c> prefixes to the same basename.</summary>
	public static string NormalizeItemHudIconLogicalPath( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return "";
		path = path.Trim().Replace( '\\', '/' );
		var file = Path.GetFileName( path );
		return string.IsNullOrEmpty( file ) ? "" : $"textures/ui/item_icons/{file}";
	}

	/// <summary>Toolbar / debug hotbar: cache successful loads per logical path; failures retry until a candidate resolves (so new PNGs pick up without restarting).</summary>
	public static bool TryGetToolbarTexture( string path, out Texture tex )
	{
		tex = null;
		if ( string.IsNullOrWhiteSpace( path ) )
			return false;

		path = NormalizeItemHudIconLogicalPath( path.Trim().Replace( '\\', '/' ) );
		if ( string.IsNullOrWhiteSpace( path ) )
			return false;
		EnsureHudIconCacheGeneration();

		if ( _toolbarIconTextureByPath.TryGetValue( path, out var cached ) )
		{
			if ( IsUsableHudTexture( cached ) )
			{
				tex = cached;
				return true;
			}

			_toolbarIconTextureByPath.Remove( path );
		}

		foreach ( var candidate in ExpandHudIconLoadCandidates( path ) )
		{
			var loaded = Texture.Load( candidate );
			if ( !IsUsableHudTexture( loaded ) )
				continue;

			_toolbarIconTextureByPath[path] = loaded;
			tex = loaded;
			return true;
		}

		return false;
	}

	/// <summary>
	/// <see cref="Texture.Load"/> resolves through game mounts; raw <c>textures/...</c> paths often map to <c>/textures/...</c> which may not match PNGs under <c>Assets/</c>. Try disk-style paths first.
	/// </summary>
	static bool IsUsableHudTexture( Texture t )
	{
		if ( t is null || t.IsError )
			return false;
		if ( ReferenceEquals( t, Texture.Invalid ) || ReferenceEquals( t, Texture.Transparent ) )
			return false;
		// Load can return a non-null placeholder that paints nothing in UI — prefer glyph fallback instead.
		return t.Width > 0 && t.Height > 0;
	}

	static IEnumerable<string> ExpandHudIconLoadCandidates( string normalizedPath )
	{
		var canonical = NormalizeItemHudIconLogicalPath( normalizedPath );
		var file = Path.GetFileName( canonical );
		if ( string.IsNullOrEmpty( file ) )
			yield break;

		// Addon loose PNGs: try disk paths before VFS-only /textures/... mounts.
		yield return "Assets/textures/ui/item_icons/" + file;
		yield return "assets/textures/ui/item_icons/" + file;
		yield return canonical;
		if ( !string.Equals( normalizedPath, canonical, StringComparison.OrdinalIgnoreCase ) )
			yield return normalizedPath;
		yield return "assets/" + canonical;
		yield return "Assets/" + canonical;

		foreach ( var alt in ExpandPlaceableModelIconCandidates( file ) )
			yield return alt;
	}

	/// <summary>When UI PNGs fail to mount, fall back to placeable model albedo textures (always shipped with the vmdl).</summary>
	static IEnumerable<string> ExpandPlaceableModelIconCandidates( string iconFile )
	{
		var stem = Path.GetFileNameWithoutExtension( iconFile );
		if ( string.IsNullOrEmpty( stem ) )
			yield break;

		string modelStem = stem switch
		{
			"player_chest" => "chest",
			_ => stem
		};

		var modelFile = modelStem + "_basecolor.png";
		yield return "Assets/models/placeables/" + modelFile;
		yield return "assets/models/placeables/" + modelFile;
		yield return "models/placeables/" + modelFile;
	}

	/// <summary>Matches <see cref="ThornsUiGridSlot"/> quantity badge rules for dock hotbar.</summary>
	public static string ToolbarStackBadgeText( ThornsItemRegistry.ThornsItemDefinition def, int qty )
	{
		if ( qty <= 0 )
			return "";
		if ( def is null )
			return qty > 1 ? $"{qty}" : "";
		if ( (def.ItemType == ThornsItemType.Weapon || def.ItemType == ThornsItemType.Tool) && def.MaxStack <= 1 )
			return "";
		if ( def.ItemType == ThornsItemType.Ammo )
			return $"{qty}";
		return qty > 1 ? $"{qty}" : "";
	}

	/// <summary>Debug HUD hotbar cell: PNG when available, else <see cref="ThornsUiInventoryFormatting.ItemGlyph"/>.</summary>
	public static void BindDebugToolbarHotbarCell( Panel iconFg, Label glyph, Label qty, in ThornsInventorySlotNet net )
	{
		if ( string.IsNullOrWhiteSpace( net.ItemId ) || net.Quantity <= 0 )
		{
			iconFg.Style.BackgroundImage = null;
			glyph.Text = "";
			glyph.SetClass( "tb-slot-icon-glyph-v2--hidden", true );
			qty.Text = "";
			return;
		}

		ThornsItemRegistry.TryGet( net.ItemId, out var def );
		var path = ResolveLoadPath( def );
		if ( TryGetToolbarTexture( path, out var tex ) )
		{
			iconFg.Style.BackgroundImage = tex;
			glyph.Text = "";
			glyph.SetClass( "tb-slot-icon-glyph-v2--hidden", true );
		}
		else
		{
			iconFg.Style.BackgroundImage = null;
			glyph.Text = ThornsUiInventoryFormatting.ItemGlyph( net.ItemId );
			glyph.SetClass( "tb-slot-icon-glyph-v2--hidden", false );
		}

		qty.Text = ToolbarStackBadgeText( def, net.Quantity );
	}
}
