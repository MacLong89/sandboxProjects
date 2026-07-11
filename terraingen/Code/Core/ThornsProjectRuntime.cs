namespace Terraingen;

/// <summary>Project identity helpers for dev vs release packaging.</summary>
public static class ThornsProjectRuntime
{
	/// <summary>
	/// The active development package (<c>terraingen</c>) stays hidden from public discovery
	/// until the release ident (<c>thorns</c>) is published separately.
	/// </summary>
	public static bool IsHiddenDevBuild =>
		string.Equals( Game.Ident, "terraingen", StringComparison.OrdinalIgnoreCase );

	public static bool IsReleaseIdent =>
		string.Equals( Game.Ident, "thorns", StringComparison.OrdinalIgnoreCase );
}
