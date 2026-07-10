namespace Sandbox;



public enum ThornsCelestialSunRiseDirection

{

	East,

	North,

	West,

	South

}



/// <summary>How the visible sky dome is driven.</summary>

public enum ThornsCelestialSkyRenderMode

{

	/// <summary>Deprecated alias — preset .vmat swapping is disabled; runtime uniforms drive the sky.</summary>

	BakedPresets,

	/// <summary>Camera background fill from <see cref="ThornsCelestialState"/> (default — SkyBox2D off).</summary>
	RuntimeTexture,

	/// <summary>Custom thorns_celestial_sky.shader + per-frame uniform push.</summary>

	RuntimeUniforms

}

