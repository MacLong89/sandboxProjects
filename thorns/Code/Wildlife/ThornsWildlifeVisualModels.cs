namespace Sandbox;

using Terraingen.Foliage;

/// <summary>
/// Resolves creature meshes. FBX must be compiled to <c>.vmdl</c> in the editor — code only loads the result.
/// </summary>
public static class ThornsWildlifeVisualModels
{
	public const string DevBoxPath = "models/dev/box.vmdl";

	/// <summary>Default compiled model path when <c>Assets/models/wolf/wolf.fbx</c> builds successfully.</summary>
	public const string WolfCompiledPath = "models/wolf/wolf.vmdl";

	public const string PantherCompiledPath = "models/panther/panther.vmdl";

	public const string ElkCompiledPath = "models/elk/elk.vmdl";

	public const string DeerCompiledPath = "models/deer/deer.vmdl";

	public const string MooseCompiledPath = "models/moose/moose.vmdl";

	public static Model LoadForSpecies( ThornsWildlifeSpeciesKind species, out bool usedDevBoxFallback )
	{
		usedDevBoxFallback = true;
		var path = PrimaryPathForSpecies( species );
		if ( string.IsNullOrWhiteSpace( path ) )
			return LoadDevBoxOrError();

		var m = ThornsFoliageModelCache.Load( path.Trim() );
		if ( !IsUsableModel( m ) )
			return LoadDevBoxOrError();

		usedDevBoxFallback = false;
		return m;
	}

	static string PrimaryPathForSpecies( ThornsWildlifeSpeciesKind species ) =>
		species switch
		{
			ThornsWildlifeSpeciesKind.Wolf => WolfCompiledPath,
			ThornsWildlifeSpeciesKind.Panther => PantherCompiledPath,
			ThornsWildlifeSpeciesKind.Elk => ElkCompiledPath,
			ThornsWildlifeSpeciesKind.Deer => DeerCompiledPath,
			ThornsWildlifeSpeciesKind.Moose => MooseCompiledPath,
			_ => null
		};

	static Model LoadDevBoxOrError() => ThornsFoliageModelCache.Load( DevBoxPath );

	static bool IsUsableModel( Model m ) =>
		m.IsValid() && !m.IsError;
}
