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
		var csproj = Path.Combine( codeDir, "terraingen.csproj" );
		var slnx = string.IsNullOrWhiteSpace( projectRoot )
			? Path.Combine( codeDir, "terraingen.slnx" )
			: Path.Combine( projectRoot, "terraingen.slnx" );

		if ( !File.Exists( csproj ) )
			Log.Error( $"[Thorns] Missing Code/terraingen.csproj at '{csproj}' — game code will not compile." );

		if ( !File.Exists( slnx ) )
			Log.Warning( $"[Thorns] Missing terraingen.slnx at '{slnx}' — open via terraingen.slnx in the editor." );

		var pathToCheck = projectRoot ?? codeDir;
		if ( pathToCheck.Contains( '&' ) )
			Log.Warning(
				"[Thorns] Project path contains '&' (e.g. _s&box). If compile fails or components are missing, " +
				"move the project to a path without '&' and reopen in s&box." );
	}
}
