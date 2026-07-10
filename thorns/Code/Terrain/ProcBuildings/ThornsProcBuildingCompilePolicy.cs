namespace Sandbox;

/// <summary>Controls how <see cref="ThornsProcTileBlueprintCompiler"/> and world-gen resolve layouts.</summary>
public enum ThornsProcBuildingCompilePolicy
{
	/// <summary>Compile must pass connectivity, validation, and ramp rules before returning a layout.</summary>
	Strict,

	/// <summary>Compile returns after geometry/door assignment without validation (debug only).</summary>
	LenientDebug,

	/// <summary>Compile geometry only; caller runs strict validation and may substitute a validated fallback.</summary>
	FallbackAllowed
}
