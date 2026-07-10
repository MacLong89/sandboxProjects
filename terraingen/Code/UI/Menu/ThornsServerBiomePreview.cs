namespace Terraingen.UI.Menu;

using Sandbox.UI;
using Terraingen;

/// <summary>v1 biome stills for server detail cards (map renders later).</summary>
public static class ThornsServerBiomePreview
{
	static readonly Dictionary<string, string> BiomeToPath = new( StringComparer.OrdinalIgnoreCase )
	{
		["Forest"] = "ui/menu/biome_forest.png",
		["Snow"] = "ui/menu/biome_snow.png",
		["Mountain"] = "ui/menu/biome_mountain.png",
		["Lake"] = "ui/menu/biome_lake.png",
		["Plains"] = "ui/menu/biome_plains.png"
	};

	public static void ApplyToPanel( Panel panel, string biome )
	{
		if ( panel is null || !panel.IsValid )
			return;

		var path = ResolvePath( biome );
		var tex = TryLoadTexture( path );
		if ( tex is null || !tex.IsValid )
			tex = TryLoadTexture( "map/co_height.png" );

		panel.Style.BackgroundImage = tex is { IsValid: true } ? tex : null;
		panel.AddClass( "mainmenu-biome-preview" );
	}

	public static string ResolvePath( string biome )
	{
		if ( string.IsNullOrWhiteSpace( biome ) )
			biome = "Forest";

		if ( BiomeToPath.TryGetValue( biome.Trim(), out var path ) )
			return path;

		return BiomeToPath["Forest"];
	}

	static Texture TryLoadTexture( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) || !ThornsMountedFiles.Exists( path ) )
			return default;

		try
		{
			var tex = Texture.Load( path );
			return tex is not null && tex.IsValid ? tex : default;
		}
		catch
		{
			return default;
		}
	}
}
