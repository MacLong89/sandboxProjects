namespace Terraingen.Editor;

using System;
using System.IO;

/// <summary>Logs when the editor loads and whether game code projects are present.</summary>
public static class ThornsProjectCompileGuard
{
	[Event( "tools.package.loaded" )]
	static void OnPackageLoaded()
	{
		var project = Project.Current;
		if ( project?.Config is null )
			return;

		if ( !string.Equals( project.Config.Ident, "terraingen", StringComparison.OrdinalIgnoreCase )
		     && !string.Equals( project.Config.Ident, "thorns", StringComparison.OrdinalIgnoreCase ) )
			return;

		var codeDir = project.GetCodePath();
		var projectRoot = string.IsNullOrWhiteSpace( codeDir ) ? null : Path.GetDirectoryName( codeDir );
		// BOOT FIX: sbproj Ident is "thorns"; assembly/csproj is Code/thorns.csproj (not terraingen.csproj).
		// Wrong CsProjName previously pointed at a missing file → no game assembly → players report "won't boot".
		var csproj = Path.Combine( codeDir, "thorns.csproj" );
		var slnx = string.IsNullOrWhiteSpace( projectRoot )
			? Path.Combine( codeDir, "thorns.slnx" )
			: Path.Combine( projectRoot, "thorns.slnx" );

		if ( !File.Exists( csproj ) )
			Log.Error( $"[Thorns] Missing Code/thorns.csproj at '{csproj}' — game code will not compile. Check terraingen.sbproj CsProjName." );

		if ( !File.Exists( slnx ) )
			Log.Warning( $"[Thorns] Missing thorns.slnx at '{slnx}' — open via thorns.slnx in the editor." );

		var pathToCheck = projectRoot ?? codeDir;
		if ( pathToCheck.Contains( '&' ) )
			Log.Warning(
				"[Thorns] Project path contains '&' (e.g. _s&box). If compile fails or components are missing, " +
				"move the project to a path without '&' and reopen in s&box." );
	}
}
